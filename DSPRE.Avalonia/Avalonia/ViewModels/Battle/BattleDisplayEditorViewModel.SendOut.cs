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
        private bool _advancedOpen;
        public bool AdvancedOpen
        {
            get => _advancedOpen;
            set { if (Set(ref _advancedOpen, value) && value) { _ = LoadTrainersAsync(); RefreshMusic(); } }
        }

        /// <summary>The ball thrown: its graphic, burst and the colour the Pokémon appears in.</summary>
        public ObservableCollection<string> BallOptions { get; } = new ObservableCollection<string>();
        private readonly List<int> _ballIds = new List<int>();
        private int _ballIndex;
        public int BallIndex { get => _ballIndex; set { if (value >= 0 && Set(ref _ballIndex, value)) StopPlayback(); } }
        private int SelectedBall => _ballIndex >= 0 && _ballIndex < _ballIds.Count ? _ballIds[_ballIndex] : 4;
        /// <summary>A ball is thrown in a trainer battle, and by the player in a wild one.</summary>
        public bool BallMatters => IsTrainerBattle || Sides != PreviewSides.Opponent;

        private void LoadBallOptions()
        {
            _ballIds.Clear();
            List<string> names = new List<string>();
            foreach ((int ball, string name) in SendOutGraphics.Balls()) { _ballIds.Add(ball); names.Add(name); }
            ListSync.Apply(BallOptions, names);
            _ballIndex = Math.Max(0, _ballIds.IndexOf(4));   // Poké Ball
            OnPropertyChanged(nameof(BallIndex));
        }

        public ObservableCollection<string> TrainerOptions { get; } = new ObservableCollection<string>();
        private int _trainerIndex = -1;
        private bool _trainersLoading, _trainersLoaded;
        public int TrainerIndex
        {
            get => _trainerIndex;
            set
            {
                if (value < 0 || !Set(ref _trainerIndex, value)) return;
                StopPlayback();
                _trainer = SendOutGraphics.TrainerInfo(value);
                RefreshMusic();
            }
        }
        private (int Class, string ClassName, string Name) _trainer = (-1, "", "");

        private async System.Threading.Tasks.Task LoadTrainersAsync()
        {
            if (_trainersLoading || _trainersLoaded || !IsAvailable) return;
            _trainersLoading = true;
            List<string> list = null;
            int initial = 1;
            try
            {
                await System.Threading.Tasks.Task.Run(() =>
                {
                    DSPRE.DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.TrainerProperties, DirNames.TextArchives });
                    list = SendOutGraphics.TrainerList();
                    if (_trainerIndex < 0) initial = SendOutGraphics.DefaultTrainer();
                });
            }
            catch (Exception ex) { AppLogger.Error("Send-out preview couldn't list trainers: " + ex.Message); }
            _trainersLoading = false;
            if (list == null || list.Count == 0) return;
            _trainersLoaded = true;
            ListSync.Apply(TrainerOptions, list);
            if (_trainerIndex < 0 || _trainerIndex >= list.Count)
            {
                _trainerIndex = Math.Clamp(initial, 0, list.Count - 1);
                _trainer = SendOutGraphics.TrainerInfo(_trainerIndex);
            }
            OnPropertyChanged(nameof(TrainerIndex));
            RefreshMusic();
        }

        private bool _playTrainerIntro = true;
        /// <summary>Off plays the send-out a trainer makes mid-battle: no trainers, no slide.</summary>
        public bool PlayTrainerIntro { get => _playTrainerIntro; set { if (Set(ref _playTrainerIntro, value)) { StopPlayback(); RaiseAdvanced(); } } }

        private bool _trainerSlideIn;
        public bool TrainerSlideIn { get => _trainerSlideIn; set { if (Set(ref _trainerSlideIn, value)) StopPlayback(); } }

        private bool _showPartyBalls = true;
        public bool ShowPartyBalls { get => _showPartyBalls; set { if (Set(ref _showPartyBalls, value)) { StopPlayback(); RaiseAdvanced(); } } }

        private int _partyBallCount = 3;
        public int PartyBallCount { get => _partyBallCount; set { if (Set(ref _partyBallCount, Math.Clamp(value, 1, 6))) StopPlayback(); } }

        private bool _isShiny;
        /// <summary>Shows both sprites in their shiny colours, and a send-out plays the sparkle.</summary>
        public bool IsShiny { get => _isShiny; set { if (Set(ref _isShiny, value)) { StopPlayback(); RaiseSprites(); } } }

        // Diamond and Pearl pick one of three back animations by nature.
        private static readonly byte[] DpBackSlotForNature =
            { 0, 2, 0, 0, 0, 1, 1, 1, 0, 1, 2, 0, 1, 0, 0, 2, 2, 2, 2, 1, 1, 2, 1, 2, 1 };
        public bool CanPickNature => GameFamily == GameFamilies.DP && !HgEngineProject.IsActive;
        public IReadOnlyList<string> NatureOptions { get; } = DVCalculator.Natures.Select(n => n.Split(':')[0]).ToArray();
        private int _natureIndex;
        public int NatureIndex { get => _natureIndex; set { if (value >= 0 && Set(ref _natureIndex, value)) StopPlayback(); } }
        private int BackSlot => CanPickNature ? DpBackSlotForNature[Math.Clamp(_natureIndex, 0, DpBackSlotForNature.Length - 1)] : 0;

        public IReadOnlyList<string> TextSpeedOptions { get; } = new[] { "Slow text", "Mid text", "Fast text", "Instant text" };
        private int _textSpeedIndex = (int)TextSpeed.Mid;
        public int TextSpeedIndex { get => _textSpeedIndex; set { if (value >= 0 && Set(ref _textSpeedIndex, value)) StopPlayback(); } }

        private bool _playMusic = true;
        public bool PlayBattleMusic { get => _playMusic; set { if (Set(ref _playMusic, value)) { StopPlayback(); RefreshMusic(); } } }

        private bool _kantoMusic;
        /// <summary>HGSS plays the Kanto versions of its standard themes in Kanto.</summary>
        public bool KantoMusic { get => _kantoMusic; set { if (Set(ref _kantoMusic, value)) { StopPlayback(); RefreshMusic(); } } }
        public bool ShowKantoMusic => GameFamily == GameFamilies.HGSS;

        private string _battleMusicText = "";
        public string BattleMusicText { get => _battleMusicText; private set => Set(ref _battleMusicText, value); }

        public bool CanChooseTrainer => IsTrainerBattle;
        public bool CanChooseIntro => IsTrainerBattle;
        public bool CanSlideIn => IsTrainerBattle && _playTrainerIntro;
        public bool CanShowPartyBalls => IsTrainerBattle;
        public bool CanCountPartyBalls => IsTrainerBattle && _showPartyBalls;

        private void RaiseAdvanced()
        {
            foreach (string n in new[] { nameof(IsTrainerBattle), nameof(BallMatters), nameof(CanChooseTrainer), nameof(CanChooseIntro),
                                      nameof(CanSlideIn), nameof(CanShowPartyBalls), nameof(CanCountPartyBalls) })
                OnPropertyChanged(n);
        }

        private SendOutSequence _sendOut;
        private SendOutGraphics _gfx;
        private SplScene _enemyBurst, _playerBurst;
        private BattleAnimPlayer _enemySparkle, _playerSparkle;
        private CellActor _enemyBallActor, _playerBallActor;
        private bool _enemyBallRolling, _playerBallRolling;
        private readonly CellActor[] _enemyRowActors = new CellActor[6], _playerRowActors = new CellActor[6];
        private int _sendOutBall;
        private System.Threading.Tasks.Task<short[]> _cry, _ballOpenSound;
        private object _musicHandle;
        private int _musicVersion;

        public SceneSprite OpponentTrainerSprite { get; } = new SceneSprite();
        public SceneSprite PlayerTrainerSprite { get; } = new SceneSprite();
        public SceneSprite OpponentBallSprite { get; } = new SceneSprite();
        public SceneSprite PlayerBallSprite { get; } = new SceneSprite();

        private Bitmap _enemyBurstImage, _playerBurstImage, _enemySparkleImage, _playerSparkleImage, _partyRowsImage;
        public Bitmap OpponentBurstImage { get => _enemyBurstImage; private set => Set(ref _enemyBurstImage, value); }
        public Bitmap PlayerBurstImage { get => _playerBurstImage; private set => Set(ref _playerBurstImage, value); }
        public Bitmap OpponentSparkleImage { get => _enemySparkleImage; private set => Set(ref _enemySparkleImage, value); }
        public Bitmap PlayerSparkleImage { get => _playerSparkleImage; private set => Set(ref _playerSparkleImage, value); }
        public Bitmap PartyRowsImage { get => _partyRowsImage; private set => Set(ref _partyRowsImage, value); }

        private bool _enemyShown = true, _playerShown = true;
        public bool OpponentShown { get => _enemyShown; private set { if (Set(ref _enemyShown, value)) RaiseShadows(); } }
        public bool PlayerShown { get => _playerShown; private set => Set(ref _playerShown, value); }

        // The wild slide moves the sprite and its shadow; the grow-in scales both.
        private double _enemySlideX, _enemyGrow = 1, _playerGrow = 1;
        private double _enemyTint, _playerTint;
        private uint _enemyTintRgb, _playerTintRgb;

        private int _enemyPlatformX, _playerPlatformX, _backdropX, _enemyGaugeX, _playerGaugeX;
        private bool _enemyGaugeShown = true, _playerGaugeShown = true;
        public double ArenaGroundOpponentX => _arenaGroundOpponentLeft + _enemyPlatformX;
        public double ArenaGroundPlayerX => _arenaGroundPlayerLeft + _playerPlatformX;
        public double PlaceholderOpponentPlatformX => 129 + _enemyPlatformX;
        public double PlaceholderPlayerPlatformX => -42 + _playerPlatformX;
        public double BackdropX => _backdropX - _backdropWidth;
        private int _backdropWidth = 256;
        public double OpponentGaugeX => _enemyGaugeX;
        public double PlayerGaugeX => _playerGaugeX;
        public bool OpponentGaugeShown => _enemyGaugeShown;
        public bool PlayerGaugeShown => _playerGaugeShown;

        public double ShadowGrow => _enemyGrow;
        // The shadow sits under the sprite's scaled anchor, so it drops while the sprite is small.
        public double ShadowGrowY => (40 - _spriteY) * (1 - _enemyGrow);

        // Live front-sprite transform. The parts are kept because the shadow follows only X.
        private double _animOffsetX, _animOffsetY, _animScaleX = 1, _animScaleY = 1, _animRotation, _animFadeOpacity;
        public double AnimOffsetX
        {
            get => _animOffsetX;
            private set { if (Set(ref _animOffsetX, value)) { OnPropertyChanged(nameof(ShadowSmallLeft)); OnPropertyChanged(nameof(ShadowMediumLeft)); OnPropertyChanged(nameof(ShadowLargeLeft)); } }
        }
        public double AnimOffsetY { get => _animOffsetY; private set => Set(ref _animOffsetY, value); }
        public double AnimScaleX { get => _animScaleX; private set => Set(ref _animScaleX, value); }
        public double AnimScaleY { get => _animScaleY; private set => Set(ref _animScaleY, value); }
        public double AnimRotation { get => _animRotation; private set => Set(ref _animRotation, value); }
        private global::Avalonia.Matrix _animMatrix = global::Avalonia.Matrix.Identity;
        public global::Avalonia.Matrix AnimMatrix { get => _animMatrix; private set => Set(ref _animMatrix, value); }
        public double AnimFadeOpacity { get => _animFadeOpacity; private set => Set(ref _animFadeOpacity, value); }
        private IBrush _animFadeBrush = Brushes.Transparent;
        public IBrush AnimFadeBrush { get => _animFadeBrush; private set => Set(ref _animFadeBrush, value); }

        private double _animBackOffsetX, _animBackOffsetY, _animBackScaleX = 1, _animBackScaleY = 1, _animBackRotation, _animBackFadeOpacity;
        public double AnimBackOffsetX { get => _animBackOffsetX; private set => Set(ref _animBackOffsetX, value); }
        public double AnimBackOffsetY { get => _animBackOffsetY; private set => Set(ref _animBackOffsetY, value); }
        public double AnimBackScaleX { get => _animBackScaleX; private set => Set(ref _animBackScaleX, value); }
        public double AnimBackScaleY { get => _animBackScaleY; private set => Set(ref _animBackScaleY, value); }
        public double AnimBackRotation { get => _animBackRotation; private set => Set(ref _animBackRotation, value); }
        private global::Avalonia.Matrix _animBackMatrix = global::Avalonia.Matrix.Identity;
        public global::Avalonia.Matrix AnimBackMatrix { get => _animBackMatrix; private set => Set(ref _animBackMatrix, value); }
        public double AnimBackFadeOpacity { get => _animBackFadeOpacity; private set => Set(ref _animBackFadeOpacity, value); }
        private IBrush _animBackFadeBrush = Brushes.Transparent;
        public IBrush AnimBackFadeBrush { get => _animBackFadeBrush; private set => Set(ref _animBackFadeBrush, value); }

        private void EnsureAnimDefsNarc()
        {
            if (_animDefsTried) return;
            _animDefsTried = true;
            if (IsAvailable) _animDefsNarc = new OffsetNarc(DirNames.PokeAnimDefs, 1);
        }

        private void BeginSendOut()
        {
            _gfx ??= new SendOutGraphics();
            _sendOutBall = SelectedBall;
            if (_trainer.Class < 0) _trainer = SendOutGraphics.TrainerInfo(_trainerIndex >= 0 ? _trainerIndex : SendOutGraphics.DefaultTrainer());
            bool trainer = IsTrainerBattle;
            int trainerClass = Math.Max(0, _trainer.Class);
            SendOutOptions options = new SendOutOptions
            {
                Kind = (SendOutKind)_sendOutKindIndex,
                Sides = Sides,
                Ball = _sendOutBall,
                TrainerIntro = !trainer || _playTrainerIntro,
                SlideIn = trainer && _playTrainerIntro && _trainerSlideIn,
                ShowPartyBalls = trainer && _showPartyBalls,
                PartyBalls = _partyBallCount,
                Shiny = _isShiny,
                Speed = (TextSpeed)_textSpeedIndex,
                OpponentCryDelay = _animFrontCryDelay,
                PlayerCryDelay = _animBackCryDelay,
                CryZeroIsEight = GameFamily == GameFamilies.HGSS,
                TrainerSequences = trainer ? _gfx.OpponentTrainerSequenceCount(trainerClass) : 1,
                TrainerLandingTicks = trainer ? _gfx.OpponentTrainerSequenceTicks(trainerClass, 1) : 0,
                Text = MessageFor,
            };
            _sendOut = new SendOutSequence(options);
            _waitingSeals.Clear();
            _enemyBurst = new SplScene();
            _playerBurst = new SplScene();
            _enemySparkle = _playerSparkle = null;
            CellSequence[] balls = _gfx.BallSequences(_sendOutBall);
            _enemyBallActor = new CellActor(balls, 1);
            _playerBallActor = new CellActor(balls, 0);
            _enemyBallRolling = _playerBallRolling = false;
            CellSequence[] rowSeqs = _gfx.PartyRowSequences();
            for (int i = 0; i < 6; i++) { _enemyRowActors[i] = new CellActor(rowSeqs, 0); _playerRowActors[i] = new CellActor(rowSeqs, 3); }
            if (_sound)
            {
                _cry = SendOutGraphics.CryAsync(_currentId);
                _ballOpenSound = _gfx.BallOpenSoundAsync();
            }
            StartMusic();
            _mode = PreviewMode.SendOut;
            SendOutTick();
        }

        private void StopSounds()
        {
            try { AudioOutput.Current.Stop(); } catch (Exception ex) when (AppLogger.Noted("BattleDisplayEditorViewModel.StopSounds", ex)) { }
        }

        /// <summary>The Ball Capsule whose seals replace the ball burst. Null for the plain one.</summary>
        public BallCapsule Capsule { get; set; }

        private IReadOnlyList<BallSeal> _seals;
        private sealed class WaitingSeal { public int Ticks; public BallSeal Seal; public int X, Y; public bool Opponent; }
        private readonly List<WaitingSeal> _waitingSeals = new();

        private bool OpenWithCapsule(bool opponentSide)
        {
            if (Capsule == null || Capsule.IsEmpty) return false;
            _seals ??= BallSeals.Read();
            foreach (BallCapsule.Placed placed in Capsule.Seals)
            {
                if (placed.Seal <= 0 || placed.Seal >= _seals.Count || _seals[placed.Seal] == null) continue;
                BallSeal seal = _seals[placed.Seal];
                _waitingSeals.Add(new WaitingSeal
                {
                    Ticks = SealEffect.DelayTicks(seal, placed.X, placed.Y), Seal = seal, X = placed.X, Y = placed.Y, Opponent = opponentSide,
                });
            }
            return true;
        }

        private void LaunchDueSeals()
        {
            for (int i = _waitingSeals.Count - 1; i >= 0; i--)
            {
                WaitingSeal w = _waitingSeals[i];
                if (w.Ticks-- > 0) continue;
                _gfx.AddSeal(w.Opponent ? _enemyBurst : _playerBurst, w.Seal, w.X, w.Y, w.Opponent);
                _waitingSeals.RemoveAt(i);
            }
        }

        public void ForgetSealParticles(int entry) => _gfx?.ForgetParticles(entry);

        // Running players step first so the sequence sees what finished.
        private void SendOutTick()
        {
            _prog?.Step();
            _progBack?.Step();
            _frontFrames.Tick();
            _backFrames.Tick();
            bool opponentBursting = _enemyBurst.Active || _waitingSeals.Exists(w => w.Opponent);
            bool playerBursting = _playerBurst.Active || _waitingSeals.Exists(w => !w.Opponent);
            StepSparkle(ref _enemySparkle, img => OpponentSparkleImage = img);
            StepSparkle(ref _playerSparkle, img => PlayerSparkleImage = img);

            SendOutSequence s = _sendOut;
            s.OpponentBusy = FrontBusy;
            s.PlayerBusy = BackBusy;
            s.OpponentBurstBusy = opponentBursting;
            s.PlayerBurstBusy = playerBursting;
            s.OpponentSparkleBusy = _enemySparkle != null;
            s.PlayerSparkleBusy = _playerSparkle != null;
            s.Step();

            if (s.OpponentAnimStarts) StartFront(frames: true, movement: true);
            if (s.PlayerAnimStarts) StartBack(frames: true, movement: true);
            if (s.OpponentBallOpens) { if (!OpenWithCapsule(opponentSide: true)) _gfx.AddBurst(_enemyBurst, _sendOutBall, opponentSide: true); PlaySound(_ballOpenSound); }
            if (s.PlayerBallOpens) { if (!OpenWithCapsule(opponentSide: false)) _gfx.AddBurst(_playerBurst, _sendOutBall, opponentSide: false); PlaySound(_ballOpenSound); }
            LaunchDueSeals();
            if (s.OpponentCry) PlaySound(_cry);
            if (s.PlayerCry) PlaySound(_cry);
            if (s.OpponentSparkleStarts) _enemySparkle = StartSparkle(opponentSide: true);
            if (s.PlayerSparkleStarts) _playerSparkle = StartSparkle(opponentSide: false);
            foreach (string name in s.Sounds) PlaySound(_gfx.SoundAsync(name));

            ApplySendOut();
            PushFrames();
            PushTransforms();

            // Particles are drawn where they are, then moved, once a tick.
            OpponentBurstImage = _enemyBurst.Step();
            PlayerBurstImage = _playerBurst.Step();

            if (s.Done && !SpritesPlaying && _enemySparkle == null && _playerSparkle == null && _waitingSeals.Count == 0
                && !_enemyBurst.Active && !_playerBurst.Active)
                StopPlayback();
        }

        private BattleAnimPlayer StartSparkle(bool opponentSide)
        {
            // On the battler's centre, where the effect's emitters are placed.
            double x = opponentSide ? OpponentLeft + 40 : PlayerLeft + 40;
            double y = opponentSide ? OpponentTop + 40 : PlayerTop + 40;
            BattleAnimPlayer player = _gfx.Sparkle(opponentSide, x, y);
            if (player != null) player.PlaySound = id => PlaySound(_gfx.SoundAsync(id));
            return player;
        }

        private static void StepSparkle(ref BattleAnimPlayer sparkle, Action<Bitmap> show)
        {
            if (sparkle == null) return;
            sparkle.Step();
            if (sparkle.Finished) { sparkle = null; show(null); return; }
            show(sparkle.RenderFrame());
        }

        private void PlaySound(System.Threading.Tasks.Task<short[]> sound)
        {
            if (_sound) SendOutGraphics.Play(sound);
        }

        private void ApplySendOut()
        {
            SendOutSequence s = _sendOut;
            OpponentShown = s.Opponent.Visible;
            PlayerShown = s.Player.Visible;
            _enemySlideX = s.Opponent.OffsetX;
            _enemyGrow = s.Opponent.Scale; _playerGrow = s.Player.Scale;
            _enemyTint = s.Opponent.Tint; _enemyTintRgb = s.Opponent.TintRgb;
            _playerTint = s.Player.Tint; _playerTintRgb = s.Player.TintRgb;
            SetSceneOffsets(s.OpponentPlatformOffsetX, s.PlayerPlatformOffsetX, s.BackdropScrollX,
                s.OpponentGaugeOffsetX, s.PlayerGaugeOffsetX, s.OpponentGaugeVisible, s.PlayerGaugeVisible);

            SendOutSequence.TrainerState et = s.OpponentTrainer;
            ApplyTrainer(OpponentTrainerSprite, et, et.Visible ? _gfx.OpponentTrainer(Math.Max(0, _trainer.Class), et.Sequence, et.SequenceTicks) : null);
            ApplyTrainer(PlayerTrainerSprite, s.PlayerTrainer, s.PlayerTrainer.Visible ? _gfx.PlayerTrainer(s.PlayerTrainer.AnimTicks) : null);
            ApplyBall(OpponentBallSprite, s.OpponentBall, _enemyBallActor, ref _enemyBallRolling);
            ApplyBall(PlayerBallSprite, s.PlayerBall, _playerBallActor, ref _playerBallRolling);
            ApplyRow(s.OpponentRow, _enemyRowActors);
            ApplyRow(s.PlayerRow, _playerRowActors);
            PartyRowsImage = s.OpponentRow.Visible || s.PlayerRow.Visible
                ? _gfx.ComposeRows((s.OpponentRow, _enemyRowActors, false), (s.PlayerRow, _playerRowActors, true))
                : null;

            MessageBoxDim = 1 - s.TextBox;
            RaiseMessage();
            RaiseShadows();
        }

        // Rolling party-row balls advance two animation units a tick, as in battle.
        private static void ApplyRow(SendOutSequence.RowState row, CellActor[] actors)
        {
            for (int i = 0; i < 6; i++)
            {
                SendOutSequence.RowBall ball = row.Balls[i];
                if (actors[i].Seq != ball.Sequence) actors[i].SetSeq(ball.Sequence);
                else if (ball.Animating) { actors[i].Tick(); actors[i].Tick(); }
                else if (actors[i].FrameIndex != 0) actors[i].SetSeq(ball.Sequence);
            }
        }

        private static void ApplyTrainer(SceneSprite view, SendOutSequence.TrainerState t, Bitmap image)
        {
            view.Visible = t.Visible && image != null;
            if (!view.Visible) return;
            view.Image = image;
            view.Left = t.X - 80;
            view.Top = t.Y - 80;
        }

        // The ball's cells advance one animation unit a tick while it spins or opens.
        private void ApplyBall(SceneSprite view, SendOutSequence.BallState b, CellActor actor, ref bool rolling)
        {
            if (b.Animating)
            {
                if (!rolling || actor.Seq != b.Sequence) actor.SetSeq(b.Sequence);
                else actor.Tick();
            }
            rolling = b.Animating;

            view.Visible = b.Visible;
            if (!b.Visible) return;
            view.Image = _gfx.BallCell(_sendOutBall, actor.CellIndex);
            view.Left = b.X - 32;
            view.Top = b.Y - 32;
            view.Rotation = b.Rotation * 360.0 / 0x10000;
            view.Flash = b.Flash;
        }

        private void SetSceneOffsets(int opponentPlatform, int playerPlatform, int backdrop, int opponentGauge, int playerGauge, bool opponentGaugeShown, bool playerGaugeShown)
        {
            if (_enemyPlatformX != opponentPlatform)
            {
                _enemyPlatformX = opponentPlatform;
                OnPropertyChanged(nameof(ArenaGroundOpponentX)); OnPropertyChanged(nameof(PlaceholderOpponentPlatformX));
            }
            if (_playerPlatformX != playerPlatform)
            {
                _playerPlatformX = playerPlatform;
                OnPropertyChanged(nameof(ArenaGroundPlayerX)); OnPropertyChanged(nameof(PlaceholderPlayerPlatformX));
            }
            if (_backdropX != backdrop) { _backdropX = backdrop; OnPropertyChanged(nameof(BackdropX)); }
            if (_enemyGaugeX != opponentGauge) { _enemyGaugeX = opponentGauge; OnPropertyChanged(nameof(OpponentGaugeX)); }
            if (_playerGaugeX != playerGauge) { _playerGaugeX = playerGauge; OnPropertyChanged(nameof(PlayerGaugeX)); }
            if (_enemyGaugeShown != opponentGaugeShown) { _enemyGaugeShown = opponentGaugeShown; OnPropertyChanged(nameof(OpponentGaugeShown)); }
            if (_playerGaugeShown != playerGaugeShown) { _playerGaugeShown = playerGaugeShown; OnPropertyChanged(nameof(PlayerGaugeShown)); }
        }

        private void ResetScene()
        {
            _sendOut = null;
            _enemyBurst = _playerBurst = null;
            _enemySparkle = _playerSparkle = null;
            OpponentBurstImage = PlayerBurstImage = OpponentSparkleImage = PlayerSparkleImage = PartyRowsImage = null;
            OpponentShown = PlayerShown = true;
            _enemySlideX = 0; _enemyGrow = _playerGrow = 1; _enemyTint = _playerTint = 0;
            SetSceneOffsets(0, 0, 0, 0, 0, true, true);
            OpponentTrainerSprite.Visible = PlayerTrainerSprite.Visible = OpponentBallSprite.Visible = PlayerBallSprite.Visible = false;
            MessageBoxDim = 0;
            RaiseMessage();
            RaiseShadows();
        }

        private void RaiseShadows()
        {
            OnPropertyChanged(nameof(ShadowSmallVisible)); OnPropertyChanged(nameof(ShadowMediumVisible)); OnPropertyChanged(nameof(ShadowLargeVisible));
            OnPropertyChanged(nameof(ShadowSmallLeft)); OnPropertyChanged(nameof(ShadowMediumLeft)); OnPropertyChanged(nameof(ShadowLargeLeft));
            OnPropertyChanged(nameof(ShadowGrow)); OnPropertyChanged(nameof(ShadowGrowY));
        }
    }
}
