using System;
using System.Collections.Generic;
using DSPRE.Avalonia.Data;
using DSPRE.Avalonia.Gl;
using DSPRE.Avalonia.ViewModels;
using DSPRE.ROMFiles;
using global::Avalonia.Controls;
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
            AddHandler(global::Avalonia.Input.InputElement.KeyDownEvent, OnKey,
                       global::Avalonia.Interactivity.RoutingStrategies.Tunnel);
            Closed += (_, _) => Stop();
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
            // The game's own letters, straight out of the ROM that is open, so an edited font shows.
            _vm.PlaySound = PlayFieldSound;
            FieldMessageBoxView.Font = FieldFont.LoadTalkFont();
            if (_vm.BorderNames.Count == 0)
            {
                for (int i = 0; i < FieldWindowFrame.FrameCount; i++) _vm.BorderNames.Add($"Frame {i}");
                _vm.BorderChanged += (_, _) =>
                {
                    FieldMessageBoxView.Frame = FieldWindowFrame.Load(_vm.BorderIndex);
                    MessageBox.InvalidateVisual();
                };
            }
            FieldMessageBoxView.Frame = FieldWindowFrame.Load(_vm.BorderIndex);
            _vm.MessageFontNote = FieldMessageBoxView.Font == null
                ? "Showing stand-in letters: the game's own font could not be read from this ROM."
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

        private SdatArchive Sdat()
        {
            if (_sdatTried) return _sdat;
            _sdatTried = true;
            try { _sdat = SoundArchive.Load(); } catch { _sdat = null; }
            return _sdat;
        }

        /// <summary>Plays what a script asked for. </summary>
        private void PlayFieldSound(ScriptEffectKind kind, int id)
        {
            if (kind == ScriptEffectKind.MusicStop) { AudioOutput.Current.Stop(); return; }

            var sdat = Sdat();
            if (sdat == null) return;

            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    // A cry is not a sequence of its own: the games play the one shared sequence with the
                    // Pokemon's own instruments in place of its (snd_play.c:1091), so the species number
                    // goes in as the bank.
                    var pcm = kind == ScriptEffectKind.Cry
                        ? SoundArchive.RenderCry(id)
                        : SseqPlayer.Render(sdat, id);
                    if (pcm != null && pcm.Length > 0) AudioOutput.Current.Play(pcm, 32000);
                }
                catch { /* a preview should never put an error dialog up mid-animation */ }
            });
        }

        private void MapMusicChanged()
        {
            if (!_vm.PlaySounds || !_vm.PlayMapMusic) { AudioOutput.Current.Stop(); return; }
            PlayFieldSound(ScriptEffectKind.Music, _vm.MapMusicId);
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

        private void PlayPause_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            _vm.Playing = !_vm.Playing;
            _lastTick = DateTime.UtcNow;
            _frames.Reset();
        }

        private void Restart_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            _vm.Restart();
            _lastTick = DateTime.UtcNow;
            _frames.Reset();
        }

        // ── Stepping in ──────────────────────────────────────────────────────────────────
        private void OnKey(object sender, global::Avalonia.Input.KeyEventArgs e)
        {
            if (!_vm.StepInto) return;

            // Typing an answer is typing, so leave the box alone while it has the focus.
            if (FocusManager?.GetFocusedElement() is TextBox) return;

            // You cannot walk off while somebody is still talking to you.
            if (_vm.MessageVisible
                && e.Key != global::Avalonia.Input.Key.Enter
                && e.Key != global::Avalonia.Input.Key.Space) { e.Handled = true; return; }

            switch (e.Key)
            {
                case global::Avalonia.Input.Key.Up:    Say(_vm.Move(MoveFacing.Up)); break;
                case global::Avalonia.Input.Key.Down:  Say(_vm.Move(MoveFacing.Down)); break;
                case global::Avalonia.Input.Key.Left:  Say(_vm.Move(MoveFacing.Left)); break;
                case global::Avalonia.Input.Key.Right: Say(_vm.Move(MoveFacing.Right)); break;
                case global::Avalonia.Input.Key.Enter:
                case global::Avalonia.Input.Key.Space: _vm.Interact(); break;
                default: return;
            }
            e.Handled = true;
            Apply();
        }

        private void Say(string message)
        {
            if (!string.IsNullOrEmpty(message)) _vm.ScriptLines.Add(message);
        }

        private void Answer_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (sender is Button b && b.Content is string label)
                _vm.AnswerOption(_vm.AnswerOptions.IndexOf(label));
        }

        private void AnswerTyped_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
            => _vm.AnswerTyped();
    }
}
