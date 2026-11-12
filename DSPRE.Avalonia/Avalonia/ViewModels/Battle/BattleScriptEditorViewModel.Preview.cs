using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using DSPRE.Avalonia.BattleAnim;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
using global::Avalonia.Media;
using global::Avalonia.Media.Imaging;
using global::Avalonia.Threading;
using DSPRE.Avalonia.Data;
using static DSPRE.RomInfo;
using Avalonia.Platform;
using System.Runtime.InteropServices;

namespace DSPRE.Avalonia.ViewModels.Battle
{
    public sealed partial class BattleScriptEditorViewModel
    {
        private void SetupCellPreview()
        {
            StopCell();
            PreviewFrame = -1;
            if (IsAnimation) { EnsureScene(); SceneComposite = _compositor.Render(null); }
            HasCellAnimation = false; HasParticleAnimation = false;
            CellPreview = null; CellAnimNote = "";
            RenderMessage();
            _animPlayer = null; ShakeX = ShakeY = 0; BackgroundDarken = 0;
            if (IsAnimation && _fileIndex >= 0)
            {
                List<WazaSeqCommand> cmds = BuildCommands();
                LoadCellResourcesForCommands(cmds, _fileIndex);
                RecordTimeline(cmds);
                // Once the sounds render, the timeline is recorded again, as their lengths set the waits.
                if (LoadSdat() != null)
                    System.Threading.Tasks.Task.Run(() => RenderScriptSounds(cmds))
                        .ContinueWith(_ => Dispatcher.UIThread.Post(() => RecordTimeline(BuildCommands())));

                int emitters = BattleAnimParticles.Extract(cmds, _version, _attackerIsOpponent).Count;
                HasParticleAnimation = emitters > 0 && _particleNarc.Available;

                CellAnimNote = "";
            }
            RaisePreviewProps();
        }

        private void LoadCellResourcesForCommands(List<WazaSeqCommand> cmds, int moveIdForLogging)
        {
            HasCellAnimation = false;
            BattleAnimSpriteResources res = BattleAnimSprites.Extract(cmds, _version);
            if (res.HasCellAnimation)
            {
                bool loaded = _cellRenderer.Load(res.Char, res.Pltt, res.Cell, res.CellAnm);
                if (loaded)
                {
                    // Surf picks the sequence by side; 1 is the opponent's flipped wave.
                    int bank = _attackerIsOpponent && _cellRenderer.AnimationCount > 1 ? 1 : 0;
                    _cellFrames = _cellRenderer.RenderAnimation(bank);
                    if (_cellFrames.Count > 0)
                    {
                        HasCellAnimation = true;   // shown only on Play, not as a static poster
                        CellOrigin = new global::Avalonia.RelativePoint(
                            _cellRenderer.ContentCx / 256.0, _cellRenderer.ContentCy / 192.0,
                            global::Avalonia.RelativeUnit.Relative);
                    }
                }
                AppLogger.Info($"Move animation {moveIdForLogging} sprites: char={res.Char} pltt={res.Pltt} cell={res.Cell} " +
                    $"anm={res.CellAnm} load={loaded} banks={_cellRenderer.AnimationCount} frames={_cellFrames.Count}");
            }
            else
            {
                // The renderer is shared between moves, so without this the previous move's sprite would draw.
                _cellRenderer.Unload();
                _cellFrames = Array.Empty<WeCellAnimRenderer.Frame>();
                AppLogger.Info($"Move animation {moveIdForLogging}: no sprites (char={res.Char} pltt={res.Pltt} " +
                    $"cell={res.Cell} anm={res.CellAnm})");
            }
        }

        // The game picks Metronome's move in battle code, so the preview picks one after the finger wag.
        private const int MetronomeMoveId = 118;
        private bool IsMetronomePreview => IsAnimation && _fileIndex == MetronomeMoveId;
        private static readonly Random MetronomeRandom = new Random();
        private int _metronomeCalledMoveId = -1;

        // Skips only Metronome, move 0 and scripts that don't parse, not the game's exclusion table.
        private int PickRandomMetronomeTarget()
        {
            int count = CurrentNarc.Count;
            if (count <= 1) return -1;
            for (int attempt = 0; attempt < 20; attempt++)
            {
                int id = MetronomeRandom.Next(1, count);
                if (id == MetronomeMoveId) continue;
                byte[] bytes = CurrentNarc.Get(id);
                if (bytes != null && bytes.Length >= 4) return id;
            }
            return -1;
        }

        public void ToggleCellPlay()
        {
            if (!HasPreview) return;
            if (IsCellPlaying) { Pause(); return; }
            if (_holding && _animPlayer != null && !_animPlayer.Finished) { StartClock(); return; }
            PlayFromStart();
        }

        public void StopPreview() { if (IsCellPlaying || _holding) StopCell(); }

        private void StartClock()
        {
            SetSoundsOn(_animPlayer, Speeds[_speedIndex] == 1);
            _holding = false;
            _previewTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(5) };
            _previewTimer.Tick -= PreviewTick; _previewTimer.Tick += PreviewTick;
            _passesRun = 0;
            _clock.Restart();
            _previewTimer.Start();
            RaisePreviewProps();
        }

        private void PlayFromStart(bool cutSounds = true)
        {
            _holding = false;
            _cellFrameIdx = 0; _cellTick = 0; _cellLoops = 0; _previewFrames = 0;
            List<WazaSeqCommand> cmds = BuildCommands();
            LoadCellResourcesForCommands(cmds, _fileIndex);   // a chained Metronome move may have left its sprites loaded
            if (HasCellAnimation && _cellFrames.Count > 0) CellPreview = _cellFrames[0].Bitmap;
            _notesShown = 0;
            if (cutSounds) StopPreviewSounds();
            _animPlayer = NewPlayer(cmds, _fileIndex);
            _metronomeCalledMoveId = IsMetronomePreview ? PickRandomMetronomeTarget() : -1;
            ShowPass();
            StartClock();
        }

        private BattleAnimPlayer NewPlayer(List<WazaSeqCommand> cmds, int moveId) => NewPlayer(cmds, moveId, live: true);

        // A live player plays its sounds. A recording one is silent and counts only rendered sounds.
        private BattleAnimPlayer NewPlayer(List<WazaSeqCommand> cmds, int moveId, bool live)
        {
            if (live) RenderScriptSounds(cmds);
            double aX = _attackerIsOpponent ? _dfX : _atX, aY = _attackerIsOpponent ? _dfY : _atY;
            double dX = _attackerIsOpponent ? _atX : _dfX, dY = _attackerIsOpponent ? _atY : _dfY;
            BattleAnimPlayer player = new BattleAnimPlayer(cmds, _version, _particleNarc, aX, aY, dX, dY,
                                   attackerIsOpponent: _attackerIsOpponent, selfTarget: IsSelfTargetMove(moveId))
            { SecondTurnVariant = _secondTurnVariant, Weather = _weatherIndex, Contest = _contest && HasContestCheck };
            player.Cells = _cellRenderer;
            if (live && _soundsOn)
            {
                player.PlaySound = PreviewSound;
                player.PlayCry = PreviewCry;
                player.StopSound = PreviewStopSound;
            }
            if (live)
            {
                player.SoundPasses = id => PassesOf(SoundPcm(id));
                player.CryPasses = () => PassesOf(CryPcm());
            }
            else
            {
                player.SoundPasses = id => PassesOf(RenderedSoundPcm(id));
                player.CryPasses = () => _cryPcmSpecies == _gaugeSpeciesId ? PassesOf(_cryPcm) : 0;
            }
            player.MovePower = GetMovePower(moveId);
            player.SpriteYOffsets[0] = _backYOffset;
            player.SpriteYOffsets[1] = _frontYOffset;
            int moveFlag = GetMoveFlagField(moveId);
            _hideGaugesThisMove = moveFlag >= 0 && (moveFlag & 0x40) == 0;
            _hideShadowThisMove = moveFlag >= 0 && (moveFlag & 0x80) != 0;
            return player;
        }

        private void StartChainedAnimation(int moveId)
        {
            byte[] bytes = CurrentNarc.Get(moveId);
            List<WazaSeqCommand> cmds = bytes != null ? BattleAnimScript.Parse(bytes, _version) : new List<WazaSeqCommand>();
            LoadCellResourcesForCommands(cmds, moveId);
            _animPlayer = NewPlayer(cmds, moveId);
            _cellFrameIdx = 0; _cellTick = 0; _cellLoops = 0;
            if (HasCellAnimation && _cellFrames.Count > 0) CellPreview = _cellFrames[0].Bitmap;

            string calledName = moveId >= 0 && moveId < _moveNames.Length ? _moveNames[moveId] : $"Move {moveId}";
            CellAnimNote = $"Metronome called {calledName}!";
            RaisePreviewProps();
        }

        // The move's base power when previewing the move-animation archive, else -1.
        private int GetMovePower(int moveId = -1)
        {
            if (moveId < 0) moveId = _fileIndex;
            if ((Archive)_archiveIndex != Archive.MoveAnimation || moveId < 0) return -1;
            try { return new MoveData(moveId).Damage; }
            catch (Exception ex) { AppLogger.WarnOnce("BattleScriptEditorViewModel.GetMovePower", ex); return -1; }
        }

        private bool IsSelfTargetMove(int moveId = -1)
        {
            if (moveId < 0) moveId = _fileIndex;
            if ((Archive)_archiveIndex != Archive.MoveAnimation || moveId < 0) return false;
            try
            {
                MoveData move = new MoveData(moveId);
                return BattleAnimTarget.DefenderIsUser(move.Target, move.Battleeffect);
            }
            catch (Exception ex) { AppLogger.WarnOnce("BattleScriptEditorViewModel.IsSelfTargetMove", ex); return false; }
        }

        private void PreviewTick(object sender, EventArgs e)
        {
            if (!IsCellPlaying) return;
            long due = (long)(_clock.Elapsed.TotalSeconds * PreviewFps * Speeds[_speedIndex]);
            if (due - _passesRun > 4) _passesRun = due - 1;   // after a stall, resume instead of fast-forwarding
            if (_passesRun >= due) return;
            while (IsCellPlaying && _passesRun < due)
            {
                _passesRun++;
                if (!RunPass()) return;
            }
            if (IsCellPlaying) ShowPass();
        }

        private void StepPass()
        {
            // Actors are drawn into SceneComposite, so the standalone cell overlay would draw them twice.
            bool cellsViaActors = _animPlayer != null && _animPlayer.SpriteActors.Count > 0;
            if (!cellsViaActors && HasCellAnimation && _cellFrames.Count > 0 && ++_cellTick >= _cellFrames[_cellFrameIdx].Duration)
            {
                _cellTick = 0;
                if (_cellFrameIdx + 1 >= _cellFrames.Count) _cellLoops++;
                _cellFrameIdx = (_cellFrameIdx + 1) % _cellFrames.Count;
                CellPreview = _cellFrames[_cellFrameIdx].Bitmap;
            }
            if (_animPlayer != null)
            {
                if (cellsViaActors && CellOpacity != 0) CellOpacity = 0;
                _animPlayer.Step();
            }
        }

        private void RaiseNotes()
        {
            if (_animPlayer == null || _animPlayer.Notes.Count == _notesShown) return;
            _notesShown = _animPlayer.Notes.Count;
            OnPropertyChanged(nameof(PreviewNotes));
            OnPropertyChanged(nameof(HasPreviewNotes));
        }

        private bool RunPass()
        {
            StepPass();
            RaiseNotes();
            bool done = _animPlayer != null ? _animPlayer.Finished : (_cellLoops >= 1);
            if (done && _metronomeCalledMoveId >= 0)
            {
                int calledMoveId = _metronomeCalledMoveId;
                _metronomeCalledMoveId = -1;   // only chain once; the called move doesn't itself call another
                _previewFrames = 0;
                StartChainedAnimation(calledMoveId);
                return false;   // keep the timer running for the chained move instead of stopping
            }
            if (done && _loop) { PlayFromStart(cutSounds: false); return false; }   // the last sounds play out, as in the game
            if (done || ++_previewFrames >= MaxPreviewFrames) { StopCell(stopSounds: false); return false; }
            return true;
        }

        private WriteableBitmap[] _frameBuffers;
        private int _frameBuffer;
        private static WriteableBitmap NewFrameBuffer() =>
            new WriteableBitmap(new global::Avalonia.PixelSize(256, 192), new global::Avalonia.Vector(96, 96),
                global::Avalonia.Platform.PixelFormat.Bgra8888, global::Avalonia.Platform.AlphaFormat.Premul);

        private void ShowPass()
        {
            if (_animPlayer != null)
            {
                // One picture a frame, alternating two bitmaps so the image gets a new source, no allocating.
                _frameBuffers ??= new[] { NewFrameBuffer(), NewFrameBuffer() };
                _frameBuffer ^= 1;
                _compositor.RenderInto(_frameBuffers[_frameBuffer], _animPlayer, _animPlayer.RenderParticlesBgra());
                SceneComposite = _frameBuffers[_frameBuffer];
                PreviewFrame = _animPlayer.Pass;
                ShakeX = _animPlayer.ShakeX; ShakeY = _animPlayer.ShakeY;
                BackgroundDarken = _animPlayer.FadeOpacity;
                if (_animPlayer.FadeOpacity > 0) FadeBrush = new SolidColorBrush(Color.FromRgb(_animPlayer.FadeR, _animPlayer.FadeG, _animPlayer.FadeB));
                PlayerOffsetX = _animPlayer.MonDX[0] + _animPlayer.MonShakeX[0]; PlayerOffsetY = _animPlayer.MonDY[0] + _animPlayer.MonShakeY[0];
                PlayerRotation = _animPlayer.MonRot[0]; PlayerScaleX = _animPlayer.MonScaleX[0]; PlayerScaleY = _animPlayer.MonScaleY[0];
                PlayerVisible = _animPlayer.MonVisible[0]; PlayerTintOpacity = _animPlayer.MonTintA[0];
                OpponentOffsetX = _animPlayer.MonDX[1] + _animPlayer.MonShakeX[1]; OpponentOffsetY = _animPlayer.MonDY[1] + _animPlayer.MonShakeY[1];
                OpponentRotation = _animPlayer.MonRot[1]; OpponentScaleX = _animPlayer.MonScaleX[1]; OpponentScaleY = _animPlayer.MonScaleY[1];
                OpponentVisible = _animPlayer.MonVisible[1]; OpponentTintOpacity = _animPlayer.MonTintA[1];
                if (_animPlayer.MonTintA[0] > 0 || _animPlayer.MonTintA[1] > 0)
                    TintBrush = new SolidColorBrush(Color.FromRgb(_animPlayer.TintR, _animPlayer.TintG, _animPlayer.TintB));
            }
        }

        private void StopCell(bool stopSounds = true)
        {
            _previewTimer?.Stop();
            _holding = false;
            if (stopSounds) StopPreviewSounds();
            _metronomeCalledMoveId = -1;   // manually stopped mid-Metronome; don't chain into it on a later, unrelated play
            BackgroundFrame = null;
            CellPreview = null;   // don't leave the last wave frame statically on screen
            // The last frame may have hidden or dragged mons (Dark Void), so the static scene is drawn again.
            if (IsAnimation && _sceneLoaded) SceneComposite = _compositor.Render(null);
            CellScaleX = CellScaleY = CellOpacity = 1;
            ShakeX = ShakeY = 0; BackgroundDarken = 0;
            PlayerOffsetX = PlayerOffsetY = OpponentOffsetX = OpponentOffsetY = 0;
            PlayerRotation = OpponentRotation = 0; PlayerScaleX = PlayerScaleY = OpponentScaleX = OpponentScaleY = 1;
            PlayerTintOpacity = OpponentTintOpacity = 0; PlayerVisible = OpponentVisible = true;
            OnPropertyChanged(nameof(IsCellPlaying));
            OnPropertyChanged(nameof(CellPlayButtonText)); OnPropertyChanged(nameof(CellPlayButtonIcon));
            OnPropertyChanged(nameof(CanStopPreview));
            OnPropertyChanged(nameof(GaugesVisible));
            OnPropertyChanged(nameof(ShadowHidden));
            OnPropertyChanged(nameof(RealGaugesVisible));
            OnPropertyChanged(nameof(PlaceholderGaugesVisible));
        }

        private void RaisePreviewProps()
        {
            OnPropertyChanged(nameof(HasCellAnimation));
            OnPropertyChanged(nameof(HasParticleAnimation));
            OnPropertyChanged(nameof(HasPreview));
            OnPropertyChanged(nameof(HasTurnCheck));
            OnPropertyChanged(nameof(HasWeatherCheck));
            OnPropertyChanged(nameof(HasContestCheck));
            OnPropertyChanged(nameof(CellAnimNote));
            OnPropertyChanged(nameof(PreviewNotes));
            OnPropertyChanged(nameof(HasPreviewNotes));
            OnPropertyChanged(nameof(IsCellPlaying));
            OnPropertyChanged(nameof(CellPlayButtonText)); OnPropertyChanged(nameof(CellPlayButtonIcon));
            OnPropertyChanged(nameof(CanStopPreview));
            OnPropertyChanged(nameof(GaugesVisible));
            OnPropertyChanged(nameof(ShadowHidden));
            OnPropertyChanged(nameof(RealGaugesVisible));
            OnPropertyChanged(nameof(PlaceholderGaugesVisible));
        }
    }
}
