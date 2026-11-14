using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using DSPRE.Avalonia.Data;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Graphics
{
    /// <summary>The Preview tab draws this view's unsaved edits.</summary>
    public interface IScreenPreviewSource
    {
        event Action Changed;
        IDisposable DrawScope();
    }

    public sealed class ScreenTabViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        private readonly GameTextScreens.Screen _screen;
        private bool _filled;

        public ScreenTabViewModel(GameTextScreens.Screen screen)
        {
            _screen = screen;
            int[] banks = screen.Banks();
            foreach (int bank in banks) Archives.Add(new ScreenArchiveChoice(bank, banks.Length > 1 ? FirstLineOf(bank) : null));
            States = screen.States ?? Array.Empty<string>();
            if (HasSample) PartySample.Changed += OnSampleChanged;
            _picturesSaved = AppEvents.WhenSaved(() => { if (_filled) _ = RedrawAsync(); }, dir => _shot?.Pieces.Any(p => p.Dir == dir) == true);
            TextArchive.Saved += OnTextSaved;
        }

        private void OnTextSaved(object sender, int bank)
        {
            if (Archives.All(a => a.Bank != bank)) return;
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                _archiveLines.Remove(bank);
                if (!_filled) return;
                if (_archive?.Bank == bank) FillLines(_line?.Index ?? _screen.FirstLine);
                else _ = RedrawAsync();
            });
        }

        private readonly IDisposable _picturesSaved;

        public bool HasSample => _screen.Key == "party";

        private readonly Dictionary<object, IScreenPreviewSource> _sources = new();
        private IScreenPreviewSource[] _drawn = Array.Empty<IScreenPreviewSource>();

        /// <summary>Draws a view's unsaved edits from now on; null stops following that view.</summary>
        public void Follow(object view, IScreenPreviewSource source)
        {
            if (_sources.TryGetValue(view, out IScreenPreviewSource was))
            {
                if (ReferenceEquals(was, source)) return;
                was.Changed -= OnLayoutChanged;
                _sources.Remove(view);
            }
            if (source != null)
            {
                _sources[view] = source;
                source.Changed += OnLayoutChanged;
            }
            _drawn = _sources.Values.ToArray();
            OnLayoutChanged();
        }

        private void OnLayoutChanged()
        {
            if (_filled) _ = RedrawAsync();
        }

        private IDisposable DrawScope() => new DrawScopes(_drawn.Select(s => s.DrawScope()).ToArray());

        private void OnSampleChanged()
        {
            RaiseSample();
            if (_filled) _ = RedrawAsync();
        }

        public void Detach()
        {
            if (HasSample) PartySample.Changed -= OnSampleChanged;
            _picturesSaved.Dispose();
            TextArchive.Saved -= OnTextSaved;
        }

        private List<string> _species;
        /// <summary>Species names by number; 0 leaves the slot empty.</summary>
        public List<string> SpeciesNames
        {
            get
            {
                if (_species != null) return _species;
                try { _species = new TextArchive(PokemonNamesTextNumbers[0]).Messages.Take(494).ToList(); }
                catch (Exception ex) { AppLogger.WarnOnce("ScreensEditor.Species", ex); _species = new List<string>(); }
                if (_species.Count > 0) _species[0] = "Empty";
                return _species;
            }
        }

        // A fixed list: rebuilding it resets the selection, which sets the Pokémon again and loops.
        public List<string> SampleSlots { get; } = Enumerable.Range(1, 6).Select(i => $"Pokémon {i}").ToList();

        private int _slot;
        public int SampleIndex
        {
            get => _slot;
            set
            {
                if (value < 0 || value > 5) return;
                _slot = value;
                RaiseSample();
                _litBySlot = true;
                _scrollToLit = true;
                LightSlot();
            }
        }

        private PartySample.Pokemon Who => PartySample.Party[_slot];

        private void Change(Action<PartySample.Pokemon> change)
        {
            change(Who);
            Who.Hp = Math.Clamp(Who.Hp, 0, Math.Max(1, Who.MaxHp));
            PartySample.Raise();
        }

        public int SampleSpecies { get => Who.Species; set { if (value >= 0 && value != Who.Species) Change(w => w.Species = value); } }
        public decimal SampleLevel { get => Who.Level; set { if ((int)value != Who.Level) Change(w => w.Level = (int)value); } }
        public decimal SampleHp { get => Who.Hp; set { if ((int)value != Who.Hp) Change(w => w.Hp = (int)value); } }
        public decimal SampleMaxHp { get => Who.MaxHp; set { if ((int)value != Who.MaxHp) Change(w => w.MaxHp = Math.Max(1, (int)value)); } }
        public int SampleGender { get => Who.Gender; set { if (value >= 0 && value != Who.Gender) Change(w => w.Gender = value); } }
        public int SampleCondition { get => Who.Condition; set { if (value >= 0 && value != Who.Condition) Change(w => w.Condition = value); } }
        public int SampleItem { get => Who.Item; set { if (value >= 0 && value != Who.Item) Change(w => w.Item = value); } }
        public bool SampleEgg { get => Who.Egg; set { if (value != Who.Egg) Change(w => w.Egg = value); } }

        private void RaiseSample()
        {
            foreach (string n in new[] { nameof(SampleIndex),nameof(SampleSpecies), nameof(SampleLevel), nameof(SampleHp),
                                         nameof(SampleMaxHp), nameof(SampleGender), nameof(SampleCondition), nameof(SampleItem), nameof(SampleEgg) })
                Raise(n);
        }

        /// <summary>The states the screen can be shown in; empty when it only has one.</summary>
        public string[] States { get; }
        public bool HasStates => States.Length > 0;

        private int _state;
        public int StateIndex
        {
            get => _state;
            set
            {
                if (value < 0 || value == _state) return;
                _state = value;
                Raise();
                _stop = 0;
                _ = RedrawAsync();
            }
        }

        // Picking a line printed in one state shows that state.
        private void FollowLine()
        {
            int wanted = _screen.StateOf == null || _line == null || _archive == null ? -1 : _screen.StateOf(_archive.Bank, _line.Index);
            if (wanted < 0 || wanted == _state) return;
            _state = wanted;
            Raise(nameof(StateIndex));
        }

        // An archive's first line stands in for its name in the list.
        private static string FirstLineOf(int bank)
        {
            try
            {
                List<string> lines = new TextArchive(bank).Messages;
                string first = lines.Count > 0 ? lines[0] ?? "" : "";
                first = ScreenLineRow.Readable(first);
                return first.Length > 32 ? first.Substring(0, 32) + "…" : first;
            }
            catch (Exception ex) { AppLogger.WarnOnce("ScreensEditor.FirstLine", ex); return null; }
        }

        public string Key => _screen.Key;
        public string Title => _screen.Title;

        public ObservableCollection<ScreenArchiveChoice> Archives { get; } = new ObservableCollection<ScreenArchiveChoice>();
        public bool HasSeveralArchives => Archives.Count > 1;

        private ScreenArchiveChoice _archive;
        public ScreenArchiveChoice SelectedArchive
        {
            get => _archive;
            set
            {
                if (ReferenceEquals(_archive, value) || value == null) return;
                _archive = value;
                Raise();
                FillLines(_screen.FirstLine);
            }
        }

        public ObservableCollection<ScreenLineRow> Lines { get; } = new ObservableCollection<ScreenLineRow>();
        public string StatusText => Lines.Count == 1 ? "1 line" : $"{Lines.Count} lines";

        private ScreenLineRow _line;
        public ScreenLineRow SelectedLine
        {
            get => _line;
            set
            {
                if (ReferenceEquals(_line, value)) return;
                _line = value;
                _stop = 0;
                Raise();
                FollowLine();
                _ = RedrawAsync();
            }
        }

        private Bitmap _picture;
        public Bitmap Picture { get => _picture; private set { _picture = value; Raise(); } }

        private string _pictureName = "";
        public string PictureName { get => _pictureName; private set { _pictureName = value; Raise(); } }

        private string _note = "";
        /// <summary>Shown when the chosen line isn't printed on this screen.</summary>
        public string Note { get => _note; private set { _note = value; Raise(); Raise(nameof(HasNote)); } }
        public bool HasNote => !string.IsNullOrEmpty(_note);

        public ObservableCollection<ScreenPieceRow> Pieces { get; } = new ObservableCollection<ScreenPieceRow>();

        private GameTextScreens.Shot _shot;
        private bool[][] _areas;
        private HashSet<(DirNames, int)> _lit = new HashSet<(DirNames, int)>();
        private bool _litBySlot, _scrollToLit;

        /// <summary>Raised when a piece lights up, so the pieces list can scroll to it.</summary>
        public event Action<ScreenPieceRow> Lit;

        private Bitmap _outline;
        public Bitmap Outline { get => _outline; private set { _outline = value; Raise(); } }

        /// <summary>Lights the pieces that draw the screen pixel at x, y.</summary>
        public void PickAt(int x, int y)
        {
            if (_areas == null || x < 0 || y < 0 || x >= 256 || y >= 192) return;
            int at = y * 256 + x;
            _lit = new HashSet<(DirNames, int)>();
            for (int i = 0; i < Pieces.Count && i < _areas.Length; i++)
                if (_areas[i] != null && _areas[i][at]) _lit.Add((Pieces[i].Dir, Pieces[i].Member));
            _litBySlot = false;
            _scrollToLit = true;
            ApplyLit();
        }

        public void PointAt(ScreenPieceRow row)
        {
            int i = row == null ? -1 : Pieces.IndexOf(row);
            Outline = _areas == null || i < 0 || i >= _areas.Length ? null : OutlineOf(_areas[i]);
        }

        private void LightSlot()
        {
            if (!_litBySlot || _shot == null) return;
            _lit = _shot.PokemonPieces.Where(p => p.Who == _slot).Select(p => (p.Dir, p.Member)).ToHashSet();
            ApplyLit();
        }

        private void ApplyLit()
        {
            ScreenPieceRow first = null;
            foreach (ScreenPieceRow row in Pieces)
            {
                row.IsLit = _lit.Contains((row.Dir, row.Member));
                if (row.IsLit) first ??= row;
            }
            if (_scrollToLit && first != null) Lit?.Invoke(first);
            _scrollToLit = false;
        }

        // Tinted, with a solid edge so thin pieces still show.
        private static Bitmap OutlineOf(bool[] area)
        {
            if (area == null || !area.Contains(true)) return null;
            const int w = 256, h = 192;
            byte[] rgba = new byte[w * h * 4];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (!area[y * w + x]) continue;
                    bool edge = x == 0 || y == 0 || x == w - 1 || y == h - 1
                                || !area[y * w + x - 1] || !area[y * w + x + 1] || !area[(y - 1) * w + x] || !area[(y + 1) * w + x];
                    int at = (y * w + x) * 4;
                    rgba[at] = 255; rgba[at + 1] = 40; rgba[at + 2] = 160; rgba[at + 3] = (byte)(edge ? 255 : 90);
                }
            return ImageConverter.FromRgba(rgba, w, h);
        }

        private async Task FindAreasAsync(int asked, GameTextScreens.Shot shot, int bank, int line, string text, int stop, int state)
        {
            bool[][] areas = null;
            try
            {
                areas = await PreviewFileGate.RunAsync(() =>
                {
                    using (DrawScope()) return GameTextScreens.Areas(_screen, bank, line, text, ArchiveLine, stop, state, shot, () => asked != _asked);
                });
            }
            catch (Exception ex) { AppLogger.WarnOnce("ScreensEditor.Areas", ex); }
            if (asked == _asked) _areas = areas;
        }

        private int _stop, _stops = 1;
        private bool _byItself;
        public bool HasStops => _stops > 1;
        public bool WaitsForPress => _stops > 1 && !_byItself;
        public string StopText => _stops > 1 ? $"Box {_stop % _stops + 1} of {_stops}" : "";

        public void NextStop()
        {
            if (_stops < 2) return;
            _stop = (_stop + 1) % _stops;
            _ = RedrawAsync();
        }

        private global::Avalonia.Threading.DispatcherTimer _play;

        /// <summary>Reads the lines on first show and starts screens that move on by themselves.</summary>
        public void Enter()
        {
            if (!_filled)
            {
                _filled = true;
                _archive = Archives.FirstOrDefault();
                Raise(nameof(SelectedArchive));
                FillLines(_screen.FirstLine);
            }
            UpdatePlay();
        }

        public void Leave()
        {
            _play?.Stop();
            _play = null;
        }

        public void Show(int bank, int line)
        {
            Enter();
            ScreenArchiveChoice archive = Archives.FirstOrDefault(a => a.Bank == bank);
            if (archive != null && !ReferenceEquals(archive, _archive))
            {
                _archive = archive;
                Raise(nameof(SelectedArchive));
                FillLines(line >= 0 ? line : _screen.FirstLine);
                return;
            }
            ScreenLineRow row = Lines.FirstOrDefault(l => l.Index == line);
            if (row != null) SelectedLine = row;
        }

        private readonly Dictionary<int, List<string>> _archiveLines = new Dictionary<int, List<string>>();

        private List<string> Read(int bank)
        {
            if (_archiveLines.TryGetValue(bank, out List<string> lines)) return lines;
            try { lines = new TextArchive(bank).Messages; } catch (Exception ex) { AppLogger.WarnOnce("ScreensEditor.Read", ex); lines = new List<string>(); }
            _archiveLines[bank] = lines;
            return lines;
        }

        private string ArchiveLine(int bank, int line)
        {
            List<string> lines = Read(bank);
            return line >= 0 && line < lines.Count ? lines[line] : "";
        }

        private void FillLines(int pick)
        {
            Lines.Clear();
            if (_archive == null) return;
            _archiveLines.Remove(_archive.Bank);
            List<string> lines = Read(_archive.Bank);
            for (int i = 0; i < lines.Count; i++)
                if (!string.IsNullOrWhiteSpace(lines[i])) Lines.Add(new ScreenLineRow(i, lines[i]));
            _line = null;
            SelectedLine = Lines.FirstOrDefault(l => l.Index >= pick) ?? Lines.LastOrDefault();
            Raise(nameof(StatusText));
        }

        private int _asked;

        private async Task RedrawAsync()
        {
            int asked = ++_asked;
            ScreenLineRow row = _line;
            ScreenArchiveChoice archive = _archive;
            if (row == null || archive == null) { Picture = null; return; }
            int stop = _stop, state = HasStates ? _state : -1;
            GameTextScreens.Shot shot = null;
            try
            {
                shot = await PreviewFileGate.RunAsync(() =>
                {
                    using (DrawScope()) return GameTextScreens.Draw(_screen, archive.Bank, row.Index, row.Text, ArchiveLine, stop, state);
                });
            }
            catch (Exception ex) { AppLogger.WarnOnce("ScreensEditor.Draw", ex); }
            if (asked != _asked) return;

            Pieces.Clear();
            _shot = shot;
            _areas = null;
            Outline = null;
            if (shot == null)
            {
                Picture = null;
                PictureName = "";
                Note = "Not on this screen";
                _stops = 1;
            }
            else
            {
                Picture = ImageConverter.FromRgba(shot.Rgba, 256, 192);
                PictureName = GameTextScreens.Caption(shot.Name);
                Note = "";
                _stops = Math.Max(1, shot.Stops);
                _byItself = shot.MovesOnByItself;
                // The archive is only named when pieces come from more than one.
                bool several = shot.Pieces.Select(p => p.Dir).Distinct().Count() > 1;
                foreach ((DirNames dir, int member, string kind) in shot.Pieces) Pieces.Add(new ScreenPieceRow(dir, member, kind, several));
                if (_litBySlot) LightSlot(); else ApplyLit();
                _ = FindAreasAsync(asked, shot, archive.Bank, row.Index, row.Text, stop, state);
            }
            Raise(nameof(HasStops));
            Raise(nameof(WaitsForPress));
            Raise(nameof(StopText));
            UpdatePlay();
        }

        private void UpdatePlay()
        {
            bool play = _stops > 1 && _byItself;
            if (play && _play == null)
            {
                _play = new global::Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                _play.Tick += (_, _) => NextStop();
                _play.Start();
            }
            else if (!play) Leave();
        }

        public void EditText()
        {
            if (_archive != null && _line != null) AvaloniaEditorLauncher.OpenTextEditorAt(_archive.Bank, _line.Index);
        }
    }
}
