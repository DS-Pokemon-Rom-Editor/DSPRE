using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using DSPRE.Avalonia.Data;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Pokemon
{
    /// <summary>Plays a follower reaction the way the game does: movement, bubble, line, wait, then a question.</summary>
    public sealed class FollowerTalkPreviewViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        private readonly FollowerTalkEditorViewModel _editor;
        private readonly DispatcherTimer _timer;

        /// <summary>The field runs at 30 frames a second.</summary>
        public const int TicksPerSecond = 30;

        public FollowerTalkPreviewViewModel() { }

        public FollowerTalkPreviewViewModel(FollowerTalkEditorViewModel editor)
        {
            _editor = editor;
            for (int s = 1; s <= FollowerTalk.SpeciesCount; s++) Species.Add(new FollowerTalkEditorViewModel.Choice(s, $"{s:D3} {editor.SpeciesName(s)}"));
            _species = Species.FirstOrDefault(c => c.Value == 213) ?? Species.FirstOrDefault();
            _nickname = editor.SpeciesName(_species?.Value ?? 1);
            _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(1000.0 / TicksPerSecond), DispatcherPriority.Render, (_, _) => Advance());
            _timer.Start();
            editor.SlotSelected += row => { if (row?.Data is { IsEmpty: false } s) Play(s.Reaction); };
            editor.ReactionSelected += Play;
            editor.MoveSelected += PlayMove;
            editor.PlaceChanged += UpdateStage;
            UpdateStage();
        }

        public Stage CurrentStage { get; private set; }
        public event Action StageChanged;

        private void UpdateStage()
        {
            CurrentStage = StageFor(_editor.CurrentPlace);
            StageChanged?.Invoke();
        }

        public void Detach() => _timer?.Stop();

        // ── Where it plays ────────────────────────────────────────────────────

        /// <summary>A header, the global tile the player stands on, and the header's camera.</summary>
        public sealed record Stage(int Header, int X, int Z, FieldCameraEntry Camera);

        private static Dictionary<int, List<int>> _headersByPlace;
        private static string _headersFor;

        // Each place's headers, read once per project.
        private static List<int> HeadersOf(int place)
        {
            if (_headersByPlace == null || _headersFor != WorkDir)
            {
                _headersFor = WorkDir;
                _headersByPlace = new Dictionary<int, List<int>>();
                bool dynamic = HeaderLabels.DynamicHeaders;
                for (int h = 0, n = MapHeader.GetHeaderCount(); h < n; h++)
                    if (MapHeader.TryReadLocationNameIndex((ushort)h, dynamic, out int p))
                    {
                        if (!_headersByPlace.TryGetValue(p, out List<int> list)) _headersByPlace[p] = list = new List<int>();
                        list.Add(h);
                    }
            }
            return _headersByPlace.TryGetValue(place, out List<int> l) ? l : new List<int>();
        }

        /// <summary>
        /// Where the place is shown: its fly spot, else one tile up from its first warp, else the starting town's fly spot.
        /// </summary>
        public Stage StageFor(int place)
        {
            try
            {
                List<FlyTable.Spot> spots = FlyTable.Spots();
                List<int> headers = place >= 0 ? HeadersOf(place) : new List<int>();
                (int header, int x, int z)? at = null;
                foreach (int h in headers)
                {
                    int s = spots.FindIndex(f => f.HeaderId == h);
                    if (s >= 0) { at = (h, spots[s].X, spots[s].Z); break; }
                }
                if (at == null)
                    foreach (int h in headers)
                    {
                        MapHeader header = MapHeader.GetMapHeader((ushort)h);
                        EventFile events = header == null ? null : new EventFile(header.EventFileID);
                        Warp w = events?.Warps.FirstOrDefault();
                        if (w == null) continue;
                        at = (h, w.XMapPosition + MapFile.MapSize * w.XMatrixPosition, w.YMapPosition + MapFile.MapSize * w.YMatrixPosition - 1);
                        break;
                    }
                if (at == null && spots.Count > 0) at = (spots[0].HeaderId, spots[0].X, spots[0].Z);
                if (at is not (int hh, int xx, int zz)) return null;
                MapHeader picked = MapHeader.GetMapHeader((ushort)hh);
                return new Stage(hh, xx, zz, FieldCamera.Entry(picked?.CameraAngleID ?? 0, GameFamily));
            }
            catch (Exception e) when (e is IOException || e is InvalidDataException || e is ArgumentException)
            {
                AppLogger.WarnOnce("FollowerTalkPreview.Stage", e);
                return null;
            }
        }

        // ── Who is talking ────────────────────────────────────────────────────

        public List<FollowerTalkEditorViewModel.Choice> Species { get; } = new List<FollowerTalkEditorViewModel.Choice>();

        private FollowerTalkEditorViewModel.Choice _species;
        public FollowerTalkEditorViewModel.Choice SelectedSpecies
        {
            get => _species;
            set
            {
                if (value == null || ReferenceEquals(value, _species)) return;
                bool named = _nickname == _editor.SpeciesName(_species?.Value ?? 0);
                _species = value;
                if (named) Nickname = _editor.SpeciesName(value.Value);
                _followerEntry = -1;
                Raise();
                Redraw?.Invoke();
            }
        }

        private string _nickname = "";
        public string Nickname { get => _nickname; set { _nickname = value ?? ""; Raise(); } }
        private string _playerName = "Ethan";
        public string PlayerName { get => _playerName; set { _playerName = value ?? ""; Raise(); } }
        private string _heldItem = "Oran Berry";
        public string HeldItem { get => _heldItem; set { _heldItem = value ?? ""; Raise(); } }
        private bool _sound = true;
        public bool Sound { get => _sound; set { _sound = value; Raise(); } }

        private int _followerEntry = -1;
        /// <summary>The overworld entry the follower walks as, or -1 when the follower tables can't be read.</summary>
        public int FollowerEntry
        {
            get
            {
                if (_followerEntry >= 0 || _species == null) return _followerEntry;
                try
                {
                    if (HgssFollowers.WhyNot() == null)
                    {
                        List<(int index, string label)> models = HgssFollowers.ModelsOf(_species.Value);
                        if (models.Count > 0) _followerEntry = HgssFollowers.FirstSprite + models[0].index;
                    }
                }
                catch (Exception e) when (e is IOException || e is InvalidDataException) { AppLogger.WarnOnce("FollowerTalkPreview.Entry", e); }
                return _followerEntry;
            }
        }

        // ── What the scene shows ──────────────────────────────────────────────

        /// <summary>Where the follower stands, in pixels from its starting spot; Hop is its height above the ground.</summary>
        public int X { get; private set; }
        public int Z { get; private set; }
        public int Hop { get; private set; }
        /// <summary>0 up, 1 down, 2 left, 3 right. It starts facing the player on its left.</summary>
        public int Facing { get; private set; } = HomeFacing;
        public const int HomeFacing = 2;
        /// <summary>1-14, or 0 for none.</summary>
        public int Bubble { get; private set; }
        public int Tick { get; private set; }
        private int _bubbleStart;
        public int BubbleFrame => ((Tick - _bubbleStart) >> 3) & 1;

        public string Text { get; private set; } = "";
        public bool BoxOpen { get; private set; }
        public bool HasMore { get; private set; }
        public bool Asking { get; private set; }
        public bool WaitingForA { get; private set; }
        public string Status { get; private set; } = "";
        public string After { get; private set; } = "";
        public string SoundNote { get; private set; } = "";

        /// <summary>Raised every tick the scene changes.</summary>
        public event Action Redraw;

        private void RaiseAll()
        {
            foreach (string n in new[] { nameof(Text), nameof(BoxOpen), nameof(HasMore), nameof(Asking), nameof(WaitingForA), nameof(Status), nameof(After), nameof(SoundNote) }) Raise(n);
            Redraw?.Invoke();
        }

        // ── Playback ──────────────────────────────────────────────────────────

        private readonly List<(int Due, TaskCompletionSource<bool> Done)> _waits = new List<(int, TaskCompletionSource<bool>)>();
        private CancellationTokenSource _run;
        private TaskCompletionSource<bool> _a, _answer;
        private int _soundShownUntil;

        private void Advance()
        {
            Tick++;
            List<(int Due, TaskCompletionSource<bool> Done)> due = _waits.Where(w => w.Due <= Tick).ToList();
            foreach ((int, TaskCompletionSource<bool> Done) w in due) { _waits.Remove(w); w.Done.TrySetResult(true); }
            if (SoundNote.Length > 0 && Tick >= _soundShownUntil) { SoundNote = ""; Raise(nameof(SoundNote)); }
            Redraw?.Invoke();
        }

        private async Task Wait(int ticks, CancellationToken stop)
        {
            TaskCompletionSource<bool> done = new TaskCompletionSource<bool>();
            _waits.Add((Tick + Math.Max(1, ticks), done));
            using (stop.Register(() => done.TrySetCanceled())) await done.Task;
        }

        public void PressA() => _a?.TrySetResult(true);
        public void Answer(bool yes) => _answer?.TrySetResult(yes);

        public void Stop()
        {
            _run?.Cancel();
            _run = null;
            Reset();
            Status = "";
            RaiseAll();
        }

        private void Reset()
        {
            X = Z = Hop = 0; Facing = HomeFacing; Bubble = 0;
            Text = ""; BoxOpen = HasMore = Asking = WaitingForA = false;
        }

        private CancellationToken Begin()
        {
            _run?.Cancel();
            _run = new CancellationTokenSource();
            Reset();
            After = "";
            RaiseAll();
            return _run.Token;
        }

        private int _lastReaction;
        public void Replay() { if (_lastMove > 0 && _lastReaction == 0) PlayMove(_lastMove); else if (_lastReaction > 0) Play(_lastReaction); }

        public void Play(int reaction)
        {
            _lastReaction = reaction; _lastMove = 0;
            CancellationToken stop = Begin();
            _ = RunReaction(reaction, stop);
        }

        private int _lastMove;
        public void PlayMove(int move)
        {
            _lastMove = move; _lastReaction = 0;
            CancellationToken stop = Begin();
            _ = RunMove(move, stop);
        }

        private async Task RunMove(int move, CancellationToken stop)
        {
            try
            {
                Status = $"Movement {move}"; RaiseAll();
                await Move(move, FollowerTalk.Cry, stop);
                Status = "Done"; RaiseAll();
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { Failed(e); }
        }

        private async Task Move(int move, int sound, CancellationToken stop)
        {
            FollowerTalk.MoveData m = _editor.Move(move);
            if (m == null) return;
            int x = X, z = Z, facing = Facing;
            Hop = 0;
            for (int f = 0; f < m.Frames; f++)
            {
                X += m.X(f); Z += m.Z(f); Hop += m.Hop(f);
                if (m.PlaysSound(f) && sound != 0) PlaySound(sound);
                if (m.Facing(f) is >= 1 and <= 4) Facing = m.Facing(f) - 1;
                Redraw?.Invoke();
                await Wait(m.Length(f), stop);
            }
            // The game puts the follower back where it stood when the movement ends.
            X = x; Z = z; Facing = facing; Hop = 0;
            Redraw?.Invoke();
        }

        private async Task RunReaction(int reaction, CancellationToken stop)
        {
            int friendship = 0, mood = 0;
            List<string> after = new List<string>();
            try
            {
                while (reaction != 0)
                {
                    FollowerTalk.ReactionData r = _editor.Reaction(reaction);
                    if (r == null) break;
                    Status = $"Reaction {reaction}"; RaiseAll();
                    int next = 0, steps = r.Steps;
                    for (int i = 0; i < steps; i++)
                    {
                        if (r.Move(i) != 0) await Move(r.Move(i), r.Sound(i), stop);
                        if (r.Bubble(i) is > 0 and <= FollowerTalk.BubbleCount)
                        {
                            Bubble = r.Bubble(i); _bubbleStart = Tick; Redraw?.Invoke();
                            await Wait(40, stop);
                            Bubble = 0; Redraw?.Invoke();
                        }
                        if (r.Line(i) != 0)
                        {
                            bool last = i == steps - 1;
                            List<string> pages = Pages(r.Line(i));
                            BoxOpen = true;
                            for (int p = 0; p < pages.Count; p++)
                            {
                                HasMore = false; Text = ""; RaiseAll();
                                foreach (char c in pages[p])
                                {
                                    Text += c;
                                    Raise(nameof(Text));
                                    if (c != '\n') await Wait(1, stop);
                                }
                                if (p == pages.Count - 1 && last && r.Question) break;
                                HasMore = true; WaitingForA = true; RaiseAll();
                                _a = new TaskCompletionSource<bool>();
                                using (stop.Register(() => _a.TrySetCanceled())) await _a.Task;
                                WaitingForA = false;
                            }
                            HasMore = false;
                            if (!(last && r.Question)) { BoxOpen = false; Text = ""; }
                            RaiseAll();
                        }
                        if (r.Wait(i) != 0) await Wait(r.Wait(i), stop);
                    }
                    friendship += r.FriendshipChange; mood += r.MoodChange;
                    if (r.Question)
                    {
                        Asking = true; RaiseAll();
                        _answer = new TaskCompletionSource<bool>();
                        bool yes;
                        using (stop.Register(() => _answer.TrySetCanceled())) yes = await _answer.Task;
                        Asking = false; BoxOpen = false; Text = "";
                        next = yes ? r.YesReaction : r.NoReaction;
                        after.Add((yes ? "Yes" : "No") + (next == 0 ? " ends the talk" : $" goes to reaction {next}"));
                    }
                    else if (r.Accessory != 0) after.Add($"Gives accessory {r.Accessory}");
                    else if (r.Leaf != 0) after.Add($"Gives Shiny Leaf {r.Leaf}");
                    reaction = next;
                }
                if (friendship != 0) after.Add($"Friendship {friendship:+0;-0}");
                if (mood != 0) after.Add($"Mood {mood:+0;-0}");
                After = after.Count == 0 ? "Friendship and mood stay the same, and there's no gift" : string.Join(" · ", after);
                Status = "Done";
                RaiseAll();
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { Failed(e); }
        }

        // A preview that can't play says so and leaves the editor usable.
        private void Failed(Exception e)
        {
            AppLogger.WarnOnce("FollowerTalkPreview.Play", e);
            Reset();
            Status = "The preview stopped: " + e.Message;
            RaiseAll();
        }

        private static readonly Regex VarCode = new Regex(@"\{STRVAR_1, \d+, (\d+), \d+\}");

        /// <summary>A line as pages, with the follower's, player's, place's and item's names filled in.</summary>
        private List<string> Pages(int line)
        {
            string[] names = { Nickname, _editor.SpeciesName(_species?.Value ?? 0), PlayerName, _editor.CurrentPlaceName, HeldItem };
            string s = VarCode.Replace(_editor.LineText(line), m => int.TryParse(m.Groups[1].Value, out int k) && k < names.Length ? names[k] : "");
            s = Regex.Replace(s, @"\{[A-Z_]+[^}]*\}", "");
            return s.Split(new[] { "\\f", "\\r" }, StringSplitOptions.None).Select(p => p.Replace("\\n", "\n")).ToList();
        }

        private void PlaySound(int id)
        {
            SoundNote = id == FollowerTalk.Cry ? "Cry" : id == FollowerTalk.WeakCry ? "Weak cry" : $"Sound {id}";
            _soundShownUntil = Tick + 20;
            Raise(nameof(SoundNote));
            if (!_sound) return;
            int species = _species?.Value ?? 0;
            _ = Task.Run(() =>
            {
                try
                {
                    // The game plays the weak cry with its low-HP pattern; the preview plays the plain cry.
                    short[] pcm = id is FollowerTalk.Cry or FollowerTalk.WeakCry
                        ? SoundArchive.RenderCry(species)
                        : SseqPlayer.Render(SoundArchive.Load(), id);
                    if (pcm != null && pcm.Length > 0) AudioOutput.Current.Start(pcm, AudioOutput.RenderRate);
                }
                catch (Exception e) { AppLogger.WarnOnce("FollowerTalkPreview.Sound", e); }
            });
        }
    }
}
