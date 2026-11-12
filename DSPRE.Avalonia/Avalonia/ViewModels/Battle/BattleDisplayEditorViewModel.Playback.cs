using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using DSPRE.Avalonia.BattleAnim.Particles;
using DSPRE.Avalonia.Data;
using DSPRE.Avalonia.Gl;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using static DSPRE.RomInfo;
using static MKDS_Course_Editor.NSBTP.NSBTP.NSBTP_File;
using IEditorWithUnsavedChanges = global::DSPRE.Editors.IEditorWithUnsavedChanges;

namespace DSPRE.Avalonia.ViewModels.Battle
{
    public partial class BattleDisplayEditorViewModel
    {
        private enum PreviewMode { None, Frames, Animation, SendOut }
        private PreviewMode _mode;
        private OffsetNarc _animDefsNarc;
        private bool _animDefsTried;
        private PokeAnimPlayer _prog, _progBack;
        private readonly System.Diagnostics.Stopwatch _clock = new System.Diagnostics.Stopwatch();
        private long _ticksRun;

        // Only the live editor makes sound; a view model built for tests stays quiet.
        private readonly bool _sound;

        public bool IsPlaying => _mode != PreviewMode.None;
        public string FramesButtonText => ButtonText(PreviewMode.Frames, "Play frames");
        public string AnimationButtonText => ButtonText(PreviewMode.Animation, "Play animation");
        public string SendOutButtonText => _sendOutLoading ? "Loading…" : ButtonText(PreviewMode.SendOut, "Play send-out");
        private string ButtonText(PreviewMode mode, string idle) => _mode == mode ? "Stop" : idle;
        public string FramesButtonIcon => ButtonIcon(PreviewMode.Frames);
        public string AnimationButtonIcon => ButtonIcon(PreviewMode.Animation);
        public string SendOutButtonIcon => _sendOutLoading ? "wait" : ButtonIcon(PreviewMode.SendOut);
        private string ButtonIcon(PreviewMode mode) => _mode == mode ? "stop" : "play";
        public bool CanPlay => _hasAnimData;

        public IReadOnlyList<string> SideOptions { get; } = new[] { "Both sides", "Opponent (front)", "Player (back)" };
        private int _sideIndex;
        public int SideIndex { get => _sideIndex; set { if (value >= 0 && Set(ref _sideIndex, value)) { StopPlayback(); RaiseAdvanced(); } } }
        private PreviewSides Sides => (PreviewSides)_sideIndex;

        public IReadOnlyList<string> SendOutKindOptions { get; } = new[] { "Wild battle", "Trainer battle" };
        private int _sendOutKindIndex = 1;
        public int SendOutKindIndex
        {
            get => _sendOutKindIndex;
            set { if (value >= 0 && Set(ref _sendOutKindIndex, value)) { StopPlayback(); RaiseAdvanced(); RefreshMusic(); } }
        }
        public bool IsTrainerBattle => _sendOutKindIndex == (int)SendOutKind.Trainer;

        public void ToggleAnimationPlayback() => TogglePlayback(PreviewMode.Animation);

        public void ToggleFramePlayback() => TogglePlayback(PreviewMode.Frames);

        public void ToggleSendOutPlayback()
        {
            if (_mode == PreviewMode.SendOut || _animTimer == null) { TogglePlayback(PreviewMode.SendOut); return; }
            _ = PrepareThenSendOutAsync();
        }

        // Unpacking (first send-out) and theme rendering run in the background.
        private bool _sendOutReady, _sendOutLoading, _sendOutPreparing;

        private async System.Threading.Tasks.Task PrepareThenSendOutAsync()
        {
            if (_sendOutPreparing) return;
            _sendOutPreparing = true;
            try
            {
                if (!_sendOutReady)
                {
                    SetSendOutLoading(true);
                    try { await System.Threading.Tasks.Task.Run(SendOutGraphics.Unpack); }
                    catch (Exception ex) { AppLogger.Error("Send-out preview unpack failed: " + ex.Message); }
                    await LoadTrainersAsync();
                    _sendOutReady = true;
                }
                if (_sound && _playMusic)
                {
                    Task<short[]> music = await ChooseMusicAsync();
                    if (music != null && !music.IsCompleted) { SetSendOutLoading(true); await music; }
                }
            }
            finally
            {
                _sendOutPreparing = false;
                SetSendOutLoading(false);
            }
            TogglePlayback(PreviewMode.SendOut);
        }

        private void SetSendOutLoading(bool loading)
        {
            if (_sendOutLoading == loading) return;
            _sendOutLoading = loading;
            OnPropertyChanged(nameof(SendOutButtonText));
            OnPropertyChanged(nameof(SendOutButtonIcon));
        }

        // Finds the theme off the UI thread and starts rendering it. Null when there is none.
        private async System.Threading.Tasks.Task<System.Threading.Tasks.Task<short[]>> ChooseMusicAsync()
        {
            _gfx ??= new SendOutGraphics();
            SendOutGraphics gfx = _gfx;
            int version = ++_musicVersion;
            bool trainer = IsTrainerBattle, kanto = _kantoMusic;
            int trainerClass = Math.Max(0, _trainer.Class), species = _currentId;
            int seq = -1;
            string name = "";
            try
            {
                await System.Threading.Tasks.Task.Run(() =>
                {
                    seq = gfx.BattleMusic(trainer, trainerClass, species, kanto);
                    if (seq >= 0) name = SendOutGraphics.SequenceName(seq);
                });
            }
            catch (Exception ex) { AppLogger.Error("Battle music lookup failed: " + ex.Message); }
            if (version != _musicVersion) return null;
            BattleMusicText = name;
            return seq >= 0 && _playMusic ? gfx.MusicAsync(seq) : null;
        }

        // Renders the current theme ahead of the next play.
        private void RefreshMusic()
        {
            if (!_sound || !IsAvailable || !(_sendOutReady || _advancedOpen)) return;
            _ = ChooseMusicAsync();
        }

        private void StartMusic()
        {
            if (!_sound || !_playMusic || _gfx == null) return;
            int seq = _gfx.BattleMusic(IsTrainerBattle, Math.Max(0, _trainer.Class), _currentId, _kantoMusic);
            Task<short[]> music = seq >= 0 ? _gfx.MusicAsync(seq) : null;
            if (music != null && music.IsCompletedSuccessfully) _musicHandle = SendOutGraphics.StartMusic(music.Result);
        }

        private void TogglePlayback(PreviewMode mode)
        {
            bool stopping = _mode == mode;
            if (_mode == PreviewMode.SendOut && _sound) StopSounds();
            StopPlayback();
            if (stopping) return;

            if (mode == PreviewMode.SendOut) { BeginSendOut(); if (_mode != PreviewMode.SendOut) return; }
            else
            {
                bool frames = mode == PreviewMode.Frames, movement = mode == PreviewMode.Animation;
                if (Sides != PreviewSides.Player) StartFront(frames, movement);
                if (Sides != PreviewSides.Opponent) StartBack(frames, movement);
                PushFrames();
                PushTransforms();
                if (!SpritesPlaying) return;
            }

            _mode = mode;
            _ticksRun = 0;
            _clock.Restart();
            _animTimer?.Start();
            RaisePlaying();
        }

        private void StartFront(bool frames, bool movement)
        {
            EnsureAnimDefsNarc();
            if (frames) _frontFrames.Start(_frontSlots);
            if (movement) _prog = LoadProgram(_animFrontProg, _animFrontWait);
        }

        private void StartBack(bool frames, bool movement)
        {
            EnsureAnimDefsNarc();
            if (frames) _backFrames.Start(_backSlots);
            int slot = BackSlot;
            if (movement && slot < AnimBack.Count) _progBack = LoadProgram(AnimBack[slot].Number, AnimBack[slot].Wait);
        }

        private bool FrontBusy => _frontFrames.Active || (_prog?.Active ?? false);
        private bool BackBusy => _backFrames.Active || (_progBack?.Active ?? false);
        private bool SpritesPlaying => FrontBusy || BackBusy;

        private PokeAnimPlayer LoadProgram(int fileIndex, int startDelay)
        {
            byte[] bytes = _animDefsNarc?.GetRecord(fileIndex);
            List<PokemonAnimCommand> script = bytes != null ? PokeAnimScript.Parse(bytes) : null;
            return (script != null && script.Count > 0) ? new PokeAnimPlayer(script, startDelay) : null;
        }

        public void StopPlayback()
        {
            SendOutGraphics.StopMusic(_musicHandle);
            _musicHandle = null;
            _animTimer?.Stop();
            _clock.Reset();
            _prog = null; _progBack = null;
            _frontFrames.Stop();
            _backFrames.Stop();
            PreviewMode was = _mode;
            _mode = PreviewMode.None;
            ResetScene();
            PushFrames();
            PushTransforms();
            if (was == PreviewMode.None) return;
            RaisePlaying();
        }

        private void RaisePlaying()
        {
            OnPropertyChanged(nameof(IsPlaying));
            OnPropertyChanged(nameof(FramesButtonText));
            OnPropertyChanged(nameof(AnimationButtonText));
            OnPropertyChanged(nameof(SendOutButtonText));
            OnPropertyChanged(nameof(FramesButtonIcon));
            OnPropertyChanged(nameof(AnimationButtonIcon));
            OnPropertyChanged(nameof(SendOutButtonIcon));
            RaiseMessage();
        }

        // The UI timer fires irregularly, so ticks are counted from a stopwatch, not one per callback.
        private void OnAnimTimer()
        {
            if (!IsPlaying) { _animTimer?.Stop(); return; }
            long due = (long)(_clock.Elapsed.TotalSeconds * PreviewFps);
            if (due - _ticksRun > 4) _ticksRun = due - 1;   // after a stall, resume instead of fast-forwarding
            while (IsPlaying && _ticksRun < due)
            {
                _ticksRun++;
                GameTick();
            }
        }

        internal int FrontFrameShown => _frontFrame;

        internal void GameTick()
        {
            if (_mode == PreviewMode.SendOut) { SendOutTick(); return; }
            _prog?.Step();
            _progBack?.Step();
            _frontFrames.Tick();
            _backFrames.Tick();
            PushFrames();
            PushTransforms();
            if (!SpritesPlaying) StopPlayback();
        }

        private void PushFrames()
        {
            int front = ClampFrame(_frontFrames.SpriteFrame), back = ClampFrame(_backFrames.SpriteFrame);
            if (front != _frontFrame || back != _backFrame)
            {
                _frontFrame = front; _backFrame = back;
                RaiseSprites();
            }
            if (_frontShift != _frontFrames.HorizontalShift || _backShift != _backFrames.HorizontalShift)
            {
                _frontShift = _frontFrames.HorizontalShift; _backShift = _backFrames.HorizontalShift;
                RaiseLayout();
            }
        }

        private void PushTransforms()
        {
            PokeAnimPlayer f = _prog;
            AnimOffsetX = f?.OffsetX ?? 0; AnimOffsetY = f?.OffsetY ?? 0;
            AnimScaleX = f?.ScaleX ?? 1; AnimScaleY = f?.ScaleY ?? 1;
            AnimRotation = f?.RotationDegrees ?? 0;
            AnimMatrix = SpriteMatrix(f, _frontShift, _spriteY) * GrowMatrix(_enemyGrow, _spriteY)
                       * global::Avalonia.Matrix.CreateTranslation(_enemySlideX, 0);
            // A ball's colour or the wild intro's shade is a palette fade too, overriding the script's.
            if (_enemyTint > 0) { AnimFadeOpacity = _enemyTint; AnimFadeBrush = TintBrush(_enemyTintRgb); }
            else
            {
                AnimFadeOpacity = f?.FadeStrength ?? 0;
                if (f != null && f.FadeStrength > 0) AnimFadeBrush = new SolidColorBrush(Color.FromRgb(f.FadeR, f.FadeG, f.FadeB));
            }

            PokeAnimPlayer b = _progBack;
            AnimBackOffsetX = b?.OffsetX ?? 0; AnimBackOffsetY = b?.OffsetY ?? 0;
            AnimBackScaleX = b?.ScaleX ?? 1; AnimBackScaleY = b?.ScaleY ?? 1;
            AnimBackRotation = b?.RotationDegrees ?? 0;
            AnimBackMatrix = SpriteMatrix(b, _backShift, 0) * GrowMatrix(_playerGrow, _spriteY);
            if (_playerTint > 0) { AnimBackFadeOpacity = _playerTint; AnimBackFadeBrush = TintBrush(_playerTintRgb); }
            else
            {
                AnimBackFadeOpacity = b?.FadeStrength ?? 0;
                if (b != null && b.FadeStrength > 0) AnimBackFadeBrush = new SolidColorBrush(Color.FromRgb(b.FadeR, b.FadeG, b.FadeB));
            }
        }

        private static IBrush TintBrush(uint rgb) =>
            new SolidColorBrush(Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb));

        // Scales about (40, 80 − yOffset): a centre grow plus a (40 − yOffset) × (1 − scale) drop.
        private static global::Avalonia.Matrix GrowMatrix(double scale, int yOffset)
        {
            if (scale >= 1) return global::Avalonia.Matrix.Identity;
            double anchorY = 80 - yOffset;
            return global::Avalonia.Matrix.CreateTranslation(-40, -anchorY)
                 * global::Avalonia.Matrix.CreateScale(scale, scale)
                 * global::Avalonia.Matrix.CreateTranslation(40, anchorY);
        }

        // In the 80×80 box: scale about the centre, move, then rotate about the anchor.
        private static global::Avalonia.Matrix SpriteMatrix(PokeAnimPlayer p, int frameShift, int anchorBelowCentre)
        {
            if (p == null) return global::Avalonia.Matrix.Identity;
            const double centre = 40;
            double pivotX = centre - frameShift + p.OffsetX + p.PivotX;
            double pivotY = centre + anchorBelowCentre + p.OffsetY;
            return global::Avalonia.Matrix.CreateTranslation(-centre, -centre)
                 * global::Avalonia.Matrix.CreateScale(p.ScaleX, p.ScaleY)
                 * global::Avalonia.Matrix.CreateTranslation(centre + p.OffsetX - pivotX, centre + p.OffsetY - pivotY)
                 * global::Avalonia.Matrix.CreateRotation(p.RotationDegrees * Math.PI / 180)
                 * global::Avalonia.Matrix.CreateTranslation(pivotX, pivotY);
        }

        // Battles tick sprite animation at 30 Hz. 60 is kept only to preview a hack that runs it faster.
        public ObservableCollection<string> FrameRateOptions { get; } = new ObservableCollection<string> { "30 fps", "60 fps" };

        public int FrameRateIndex
        {
            get => DSPRE.SettingsManager.Settings?.BattlePreviewFps == 60 ? 1 : 0;
            set
            {
                int fps = value == 1 ? 60 : 30;
                if (DSPRE.SettingsManager.Settings == null || DSPRE.SettingsManager.Settings.BattlePreviewFps == fps) return;
                DSPRE.SettingsManager.Settings.BattlePreviewFps = fps;
                try { DSPRE.SettingsManager.Save(); } catch (Exception ex) { AppLogger.Warn("Saving the battle preview frame rate: " + ex.Message); }
                OnPropertyChanged(nameof(FrameRateIndex));
                if (IsPlaying) { _ticksRun = 0; _clock.Restart(); }
            }
        }

        private int PreviewFps => FrameRateIndex == 1 ? 60 : 30;

        // Shared by every Pokémon using this number, and saved with its own button.
        public ObservableCollection<ProgramCmdRow> ProgramRows { get; } = new ObservableCollection<ProgramCmdRow>();
        public bool HasProgramScript => ProgramRows.Count > 0;

        // Script target: 0 is the front (prg_anm_f), 1 to 3 the back slots 0 to 2 (prg_anm_b[n]).
        private int _scriptTarget;
        public string[] ScriptTargetOptions { get; } = { "Front", "Back slot 0", "Back slot 1", "Back slot 2" };
        public int ScriptTargetIndex
        {
            get => _scriptTarget;
            set { if (Set(ref _scriptTarget, Math.Clamp(value, 0, 3))) RefreshProgramScript(); }
        }
    }
}
