using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using DSPRE.Avalonia.Data;
using DSPRE.Avalonia.Gl;
using DSPRE.Avalonia.ViewModels;
using DSPRE.Avalonia.Views.Controls;
using DSPRE.ROMFiles;
using global::Avalonia.Controls;
using global::Avalonia.Input;
using global::Avalonia.Interactivity;
using global::Avalonia.Threading;

namespace DSPRE.Avalonia.Views.Battle
{
    /// <summary>
    /// A map running on its own: the water moves and the people turn and wander the way their movement code
    /// says they should.
    /// </summary>
    public partial class AnimatedPreviewWindow : Window
    {
        private readonly AnimatedPreviewViewModel _vm = new AnimatedPreviewViewModel();
        private DispatcherTimer _clock;
        private DateTime _lastTick;
        private readonly FieldFrameClock _frames = new FieldFrameClock();

        private Gl3DPointerNavigation _nav;

        /// <summary>The preview's view model, for whoever opens the window to hand it the ROM's lookups.</summary>
        public AnimatedPreviewViewModel ViewModel => _vm;

        public AnimatedPreviewWindow()
        {
            InitializeComponent();
            DataContext = _vm;

            // Left-drag pans, right-drag orbits, wheel zooms, the same as the map and event editors.
            _nav = new Gl3DPointerNavigation(GlHost, GlView);
            // Dragging with "Place" on drops the walk's starting point wherever the pointer is, the way
            // dragging a pin about a map does. The camera stays put while that is going on.
            _nav.IsPaintModeActive = () => _vm != null && _vm.PlacingStart;
            _nav.PaintAt = PlaceStartAt;
            Opened += (_, _) => { Start(); Focus(); };
            // Catch the keys on the way down rather than on the way back up.
            AddHandler(KeyDownEvent, OnKey, RoutingStrategies.Tunnel);
            AddHandler(KeyUpEvent, OnKeyUp, RoutingStrategies.Tunnel);
            Closed += (_, _) => Stop();

            // The log follows the script as it plays.
            _vm.ScriptLines.CollectionChanged += (_, _) =>
                Dispatcher.UIThread.Post(() => { if (_vm.ScriptLines.Count > 0) ScriptLog.ScrollIntoView(_vm.ScriptLines.Count - 1); },
                                         DispatcherPriority.Background);

            // The question needs the keyboard, so it gets it.
            _vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(AnimatedPreviewViewModel.HasStatePrompt) && !_vm.HasStatePrompt) Focus();
            };
        }

        /// <summary>Shows the scene the editor already has on screen. </summary>
        public void ShowFor(Window owner, NsbmdRenderModel scene, AreaData area, EventFile events,
                         Func<Overworld, (float x, float y, float z)> footFinder,
                         MapCollisionGrid collision = null,
                         Func<float, float, (float x, float y, float z)> tileToWorld = null,
                         Func<int, ScriptWalker> walkerFor = null,
                         Func<int, int> walkerStartId = null,
                         Func<int, string> scriptHome = null,
                         int cameraId = 0,
                         int musicDayId = 0, int musicNightId = 0,
                         Func<int, IReadOnlyList<ScriptAction>> actionsFor = null,
                         LevelScriptFile levelScripts = null,
                         IEnumerable<FieldStringVar> stringVars = null)
        {
            bool indoor = area != null && area.areaType == AreaData.TYPE_INDOOR;
            _vm.CameraId = cameraId;
            _vm.LevelScripts = levelScripts;
            _vm.SetStringVars(stringVars);
            _vm.MusicDayId = musicDayId;
            _vm.MusicNightId = musicNightId;
            _vm.PlaySound = PlayFieldSound;
            // The game's own letters and borders, straight out of the ROM that is open, so edited ones show.
            FieldMessageBoxView.Font = FieldFont.LoadTalkFont();
            FieldMenuWindowView.Font = FieldFont.LoadSystemFont();
            FieldMenuWindowView.Frame = FieldWindowFrame.LoadStandard();
            FieldMenuWindowView.Colours = FieldWindowFrame.LoadSystemFontColours();
            PoketchView.Screen = DSPRE.Avalonia.Data.PoketchScreen.Load();
            Poketch.PlaySound = id => PlayFieldSound(ScriptEffectKind.SoundEffect, id);
            HgssTouchScreenView.Screen = DSPRE.Avalonia.Data.HgssTouchScreen.Load();
            HgssTouchScreenView.Font = FieldFont.LoadFromArchive(DSPRE.Avalonia.Data.HgssTouchScreen.FontEntry) ?? FieldFont.LoadSystemFont();
            HgssTouchScreenView.Text = TouchMenuText;
            TouchMenu.PlaySound = id => PlayFieldSound(ScriptEffectKind.SoundEffect, id);
            TouchMenu.ChoiceTouched = index => _vm.TouchChoice(index);
            TouchMenu.APressed = () => { _vm.Interact(); Apply(); };
            TouchMenu.AReleased = () => _vm.ReleaseA();
            if (_vm.BorderNames.Count == 0)
            {
                for (int i = 0; i < FieldWindowFrame.FrameCount; i++) _vm.BorderNames.Add($"Frame {i + 1}");
                _vm.BorderChanged += (_, _) =>
                {
                    FieldMessageBoxView.Frame = FieldWindowFrame.Load(_vm.BorderIndex);
                    MessageBox.InvalidateVisual();
                };
                // The list filled after the box was bound, which leaves it showing nothing picked.
                int picked = _vm.BorderIndex;
                _vm.BorderIndex = -1;
                _vm.BorderIndex = picked;
            }
            FieldMessageBoxView.Frame = FieldWindowFrame.Load(_vm.BorderIndex);
            _vm.MessageFontNote = FieldMessageBoxView.Font == null
                ? "Stand-in letters: this ROM's font could not be read."
                : null;
            // Wrap with the same measurements the box draws with, so lines land where they are put.
            _vm.MeasureText = FieldMessageBoxView.Measure;
            _vm.Load(scene, GroundAnimationSet.ForArea(area), events, indoor, collision, 0, tileToWorld,
                     walkerFor, walkerStartId, scriptHome, actionsFor);
            _vm.PlaceNpcs(footFinder);
            GlView.SetModel(scene);
            // This is a preview of the map running, so buildings hide whoever is behind them.
            GlView.SpritesSeeThroughGeometry = false;
            Apply();
            _vm.FrameAdvanced += (_, _) => Apply();
            _vm.MapMusicChanged += (_, _) => MapMusicChanged();
            Closed += (_, _) => AudioOutput.Current.Stop();
            Show(owner);
        }

        private void Apply()
        {
            if (_vm.StepInto) PlaceCameraBehindPlayer();
            else if (_wasSteppedIn) RestoreFreeCamera();
            _wasSteppedIn = _vm.StepInto;
            GlView.SetTextureMatrices(_vm.TextureMatrices);
            GlView.SetTextureSwaps(_vm.TextureSwaps);
            GlView.SetMovedParts(_vm.MovedParts);
            GlView.SetMaterialFades(_vm.MaterialFades);
            GlView.SetSprites(_vm.Sprites);
        }

        private void Start()
        {
            _lastTick = DateTime.UtcNow;
            _clock = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000.0 / AnimatedPreviewViewModel.FramesPerSecond) };
            _clock.Tick += (_, _) =>
            {
                var now = DateTime.UtcNow;
                double seconds = (now - _lastTick).TotalSeconds;
                _lastTick = now;

                // Drive off the wall clock rather than assuming the timer is exact, so a busy machine
                // slows the preview down instead of running it out of step with the real timings.
                int frames = _frames.Tick(seconds, _vm.Speed);
                if (frames > 0) _vm.Advance(frames);
            };
            _clock.Start();
        }

        private void Stop() { _clock?.Stop(); _clock = null; }

        /// <summary>
        /// Puts the camera where the games put it while you are walking about: behind the player, looking
        /// down at the angle the field camera uses, and following them as they go.
        /// </summary>
        private bool _wasSteppedIn;

        /// <summary>Back to the editor's own view when the player stops walking about. </summary>
        private void RestoreFreeCamera()
        {
            GlView.Orthographic = false;
            GlView.VerticalFieldOfViewDegrees = NsbmdGlControl.DefaultFovDegrees;
        }

        private void PlaceCameraBehindPlayer()
        {
            var player = _vm.Player;
            var scene = _vm.Scene;
            if (player == null || scene == null) return;

            var cam = _vm.CameraEntry;
            float tile = scene.CellStrideX / MapFile.mapSize;
            float unit = tile * scene.Scale;

            // The shift moves the camera and what it looks at together, so it lands on the target.
            var (x, y, z) = _vm.CameraTarget();
            // A shake pushes what the camera looks at, which is what the games move too.
            GlView.LookAt(x + (cam.ShiftXInTiles + _vm.CameraShiftX + _vm.ShakeOffsetX) * unit,
                          y + (cam.ShiftYInTiles + _vm.CameraShiftY + _vm.ShakeOffsetY) * unit,
                          z + (cam.ShiftZInTiles + _vm.CameraShiftZ) * unit);

            // The camera keeps the same heading whatever way the player turns: nothing in the field code
            // points it at the player's facing, and the games' own camera entry has no turn in it.
            GlView.Distance = cam.DistanceForScene(tile) * scene.Scale;
            GlView.Orthographic = cam.Orthographic;
            GlView.VerticalFieldOfViewDegrees = cam.FieldOfViewDegrees;
            GlView.SetOrientation(FieldCamera.YawDegrees, _vm.CameraPitchDegrees);
        }

        // ── sound ────────────────────────────────────────────────────────────────────────
        private SdatArchive _sdat;
        private bool _sdatTried;

        // A page turn plays the same short sound over and over, so it is rendered once.
        private readonly ConcurrentDictionary<(ScriptEffectKind, int), short[]> _rendered =
            new ConcurrentDictionary<(ScriptEffectKind, int), short[]>();

        private SdatArchive Sdat()
        {
            if (_sdatTried) return _sdat;
            _sdatTried = true;
            try { _sdat = SoundArchive.Load(); } catch { _sdat = null; }
            return _sdat;
        }

        // The music playing now, and how many fanfares are holding it.
        private object _music;
        private int _musicToken;
        private int _musicHolds;

        /// <summary>Long enough that a loop back to the start is rarely heard in a preview.</summary>
        private const double MusicSeconds = 150;

        /// <summary>Plays what a script asked for, and tells the preview how long it lasts.</summary>
        private void PlayFieldSound(ScriptEffectKind kind, int id)
        {
            if (kind == ScriptEffectKind.MusicStop) { StopMusic(); return; }
            if (kind == ScriptEffectKind.Music) { StartMusic(id); return; }
            if (!_vm.PlaySounds) { _vm.SoundLength(kind, 0); return; }

            var sdat = Sdat();
            if (sdat == null) { _vm.SoundLength(kind, 0); return; }

            // A fanfare holds the music (Snd_MePlay), and the music carries on once it is over.
            bool holdsMusic = kind == ScriptEffectKind.Fanfare;
            if (holdsMusic && _music != null && _musicHolds++ == 0) AudioOutput.Current.SetPaused(_music, true);

            System.Threading.Tasks.Task.Run(() =>
            {
                short[] pcm = null;
                try
                {
                    // A cry is not a sequence of its own: the games play the one shared sequence with the
                    // Pokemon's own instruments in place of its (snd_play.c:1091), so the species number
                    // goes in as the bank.
                    pcm = _rendered.GetOrAdd((kind, id), key => key.Item1 == ScriptEffectKind.Cry
                        ? SoundArchive.RenderCry(key.Item2)
                        : SseqPlayer.Render(sdat, key.Item2));
                    if (pcm != null && pcm.Length > 0) AudioOutput.Current.Play(pcm, 32000);
                }
                catch { /* a preview should never put an error dialog up mid-animation */ }

                // Interleaved stereo at 32 kHz, counted in 30 Hz field frames.
                double seconds = pcm == null ? 0 : pcm.Length / 2.0 / 32000.0;
                int frames = (int)Math.Ceiling(seconds * AnimatedPreviewViewModel.FramesPerSecond);
                Dispatcher.UIThread.Post(() => _vm.SoundLength(kind, frames));

                if (!holdsMusic) return;
                System.Threading.Thread.Sleep(TimeSpan.FromSeconds(seconds));
                Dispatcher.UIThread.Post(() =>
                {
                    if (_musicHolds > 0 && --_musicHolds == 0 && _music != null) AudioOutput.Current.SetPaused(_music, false);
                });
            });
        }

        /// <summary>Starts a piece of music looping, in place of whatever was playing.</summary>
        private void StartMusic(int id)
        {
            StopMusic();
            if (!_vm.PlaySounds) return;
            var sdat = Sdat();
            if (sdat == null) return;

            int token = ++_musicToken;
            System.Threading.Tasks.Task.Run(() =>
            {
                short[] pcm = null;
                try { pcm = SseqPlayer.Render(sdat, id, 32000, MusicSeconds); } catch { }
                if (pcm == null || pcm.Length == 0) return;
                Dispatcher.UIThread.Post(() =>
                {
                    // Something else was asked for while this one was being rendered.
                    if (token != _musicToken) return;
                    _music = AudioOutput.Current.StartLooping(pcm, 32000);
                    if (_musicHolds > 0) AudioOutput.Current.SetPaused(_music, true);
                });
            });
        }

        private void StopMusic()
        {
            _musicToken++;
            if (_music != null) AudioOutput.Current.Stop(_music);
            _music = null;
        }

        private void MapMusicChanged()
        {
            if (!_vm.PlaySounds || !_vm.PlayMapMusic) { StopMusic(); return; }
            StartMusic(_vm.MapMusicId);
        }

        /// <summary>
        /// Opens the walk already standing next to a tile and facing it, which is what the event editor
        /// does when somebody asks to step in at the event they have selected.
        /// </summary>
        public void StepInBeside(int tileX, int tileZ)
        {
            if (_vm == null) return;
            _vm.StandBeside(tileX, tileZ);
            if (_vm.CanStepInto) _vm.StepInto = true;
        }

        /// <summary>Opens the walk standing on a tile, which is where the dragged player was let go.</summary>
        public void StepInOn(int tileX, int tileZ)
        {
            if (_vm == null) return;
            _vm.StandOn(tileX, tileZ);
            if (_vm.CanStepInto) _vm.StepInto = true;
        }

        /// <summary>Puts the walk's starting point on whichever tile the pointer is over.</summary>
        private void PlaceStartAt(global::Avalonia.Point p)
        {
            if (_vm == null) return;
            var tile = _vm.TileAtScreen(p.X, p.Y, (x, y, z) =>
                GlView.WorldToScreen(x, y, z, out float sx, out float sy) ? (sx, sy) : ((float, float)?)null);
            if (tile != null) _vm.StandOn(tile.Value.x, tile.Value.z);
        }

        private void PlayPause_Click(object sender, RoutedEventArgs e)
        {
            _vm.Playing = !_vm.Playing;
            _lastTick = DateTime.UtcNow;
            _frames.Reset();
        }

        private void Restart_Click(object sender, RoutedEventArgs e)
        {
            _vm.Restart();
            _lastTick = DateTime.UtcNow;
            _frames.Reset();
        }

        // ── Stepping in ──────────────────────────────────────────────────────────────────
        private static bool IsA(Key k) => k == Key.Enter || k == Key.Space || k == Key.Z;
        private static bool IsB(Key k) => k == Key.X || k == Key.Back;

        private void OnKey(object sender, KeyEventArgs e)
        {
            if (!_vm.StepInto) return;

            // Typing an answer is typing, so leave the box alone while it has the focus.
            if (FocusManager?.GetFocusedElement() is TextBox)
            {
                if (e.Key == Key.Enter && _vm.HasStatePrompt && _vm.AcceptsTypedAnswer) { _vm.AnswerTyped(); e.Handled = true; }
                return;
            }

            // Esc gives up on whatever is running, the way the Stop button does.
            if (e.Key == Key.Escape)
            {
                if (_vm.ScriptRunning || _vm.HasQuestion) { _vm.StopScript(); e.Handled = true; }
                else if (_vm.MessageVisible) { _vm.Interact(); e.Handled = true; }
                return;
            }

            if (_vm.HasStatePrompt)
            {
                // Number keys pick the answers in order, and Y and N answer a two-way question.
                int pick = e.Key >= Key.D1 && e.Key <= Key.D9 ? e.Key - Key.D1
                         : e.Key >= Key.NumPad1 && e.Key <= Key.NumPad9 ? e.Key - Key.NumPad1
                         : e.Key == Key.Y ? 0 : e.Key == Key.N ? 1 : -1;
                if (pick >= 0 && pick < _vm.AnswerOptions.Count) { _vm.AnswerOption(pick); e.Handled = true; }
                return;
            }

            if (_vm.HasChoiceWindow || _vm.HasTouchChoice)
            {
                if (e.Key == Key.Up) _vm.MoveChoiceCursor(-1);
                else if (e.Key == Key.Down) _vm.MoveChoiceCursor(1);
                else if (e.Key == Key.Left) _vm.PageChoice(-1);
                else if (e.Key == Key.Right) _vm.PageChoice(1);
                else if (IsA(e.Key)) _vm.ConfirmChoice();
                else if (IsB(e.Key)) _vm.CancelChoice();
                else return;
                e.Handled = true;
                return;
            }

            if (e.KeyModifiers != KeyModifiers.None) return;
            switch (e.Key)
            {
                case Key.Up:    Say(_vm.Move(MoveFacing.Up)); break;
                case Key.Down:  Say(_vm.Move(MoveFacing.Down)); break;
                case Key.Left:  Say(_vm.Move(MoveFacing.Left)); break;
                case Key.Right: Say(_vm.Move(MoveFacing.Right)); break;
                default:
                    if (IsA(e.Key)) _vm.Interact();
                    // B only does anything while a script is listening for it.
                    else if (IsB(e.Key) && (_vm.ScriptRunning || _vm.MessageVisible)) _vm.PressA();
                    else return;
                    break;
            }
            e.Handled = true;
            Apply();
        }

        private void OnKeyUp(object sender, KeyEventArgs e)
        {
            if (IsA(e.Key) || IsB(e.Key)) _vm.ReleaseA();
        }

        private void Say(string message)
        {
            if (!string.IsNullOrEmpty(message)) _vm.ScriptLines.Add(message);
        }

        private void Answer_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.Content is string label)
                _vm.AnswerOption(_vm.AnswerOptions.IndexOf(label));
            Focus();
        }

        private void AnswerTyped_Click(object sender, RoutedEventArgs e)
        {
            _vm.AnswerTyped();
            Focus();
        }

        /// <summary>The touch menu's words out of the ROM, with the player's name and the like put in.</summary>
        private string TouchMenuText(int message)
        {
            if (RomInfo.fieldTouchMenuTextArchive < 0) return null;
            string text = _vm.ArchiveText?.Invoke(RomInfo.fieldTouchMenuTextArchive, message);
            return text == null ? null : _vm.ExpandVars(text);
        }

        private void StopScript_Click(object sender, RoutedEventArgs e)
        {
            _vm.StopScript();
            Focus();
        }

        private void ForgetState_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Control { DataContext: AnimatedPreviewViewModel.GameStateEntry entry }) entry.Forget();
            Focus();
        }

        private void ForgetAll_Click(object sender, RoutedEventArgs e)
        {
            _vm.ForgetGameState();
            Focus();
        }
    }
}
