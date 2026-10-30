using System;
using System.Collections.Generic;
using DSPRE.ROMFiles;
using global::Avalonia.Media.Imaging;
using DSPRE.Avalonia.BattleAnim;
using DSPRE.Avalonia.BattleAnim.Particles;
using DSPRE.Avalonia.BattleAnim.SpriteEffects;
using DSPRE.Avalonia.Data;
using Avalonia.Platform;

namespace DSPRE.Avalonia
{
    public sealed class BattleAnimPlayer : IBattleAnimSystem
    {
        private const int FnRotateMon = 4, FnFadeBg = 33, FnFadeBattlerSprite = 34,
                          FnScalePokemonSprite = 35, FnShake = 36, FnHideBattler = 40, FnScaleBattlerSprite = 42,
                          FnShakeBg = 68,
                          FnMoveEmitterA2BLinear = 65, FnMoveEmitterA2BParabolic = 66, FnRevolveEmitter = 72,
                          FnMoveEmitterViewportTop = 73;
        private static readonly HashSet<int> FnMoveBattlerFamily = new HashSet<int> { 51, 52, 57 };
        private const int FnBlinkAttacker = 50;
        private const int FnMoveBattlerOffScreen = 61, FnMoveBattlerToDefaultPos = 62, FnMoveBattlerOnOrOffScreen = 77, FnSetBgGrayscale = 74;
        private const int OffScreenLeft = -80, OffScreenRight = 256 + 80;
        private const int FnScrollCustomBg = 44, FnMuddyWater = 45;
        private const int FnRevolveBattler = 60;
        private const int AnimAttacker = 0x0002, AnimAttackerPartner = 0x0004, AnimDefender = 0x0008, AnimDefenderPartner = 0x0010,
                          AnimNotAttacker = 0x0020, AnimAllBattlers = 0x0040, AnimBackground = 0x0400;

        public readonly double[] MonDX = { 0, 0 };
        public readonly double[] MonDY = { 0, 0 };
        public readonly double[] MonRot = { 0, 0 };
        public readonly double[] MonScaleX = { 1, 1 };
        public readonly double[] MonScaleY = { 1, 1 };
        public readonly double[] MonTintA = { 0, 0 };
        public readonly bool[] MonVisible = { true, true };
        private readonly bool[] _monVanish = { false, false };
        public byte TintR { get; private set; } = 0;
        public byte TintG { get; private set; } = 0;
        public byte TintB { get; private set; } = 0;

        private sealed class MonFx { public int Mon, Frame, Frames, Kind; public byte R, G, B; public double Dx, Dy; public double[] Keys;
                                     public int UpF, WaitF, DownF, Cycles;
                                     public int Delay;
                                     public Shake Sh; public int NumMax, Rep; public bool ToScene;
                                     public double[][] Phases;
                                     public DroppedCap Cap;
                                     public bool Started;
                                     public int TraceCmd = -1;
                                     // Scale phases that take their steps plus a pass noticing the end, showing each step as it lands.
                                     public bool ExactSteps;
                                     public int[][] Fades;
                                     // A task that spends one more pass ending after its shake runs out.
                                     public bool EndPass, Ending;
                                     // Passes the task stays alive after its last frame, doing nothing.
                                     public int TailRuns;
                                     // A ported task: runs each pass, false once it has ended, in SysTask priority order.
                                     public Func<bool> Task; public int Priority;
                                     // A plain system task. The script's waits ignore it.
                                     public bool SysTask; }
        private readonly List<MonFx> _monFx = new List<MonFx>();

        private readonly List<WazaSeqCommand> _cmds;
        private readonly WazaSeqVersion _version;
        private readonly ScriptNarc _particleNarc;
        private double _atX, _atY, _dfX, _dfY;
        // Where each side's Pokemon sprite is drawn, which MON_SPRITE_X/Y_CENTER report with MonDX/MonDY added.
        private readonly int[] _monCX = new int[2], _monCY = new int[2];
        // Sprite attributes that effect tasks set on the Pokemon; they stay until set again.
        private readonly int[] _monAttrScaleX = { 0x100, 0x100 }, _monAttrScaleY = { 0x100, 0x100 };
        private readonly bool[] _monAttrHidden = new bool[2];
        private sealed class MonFade { public bool Active; public int Cur, Target, DelayLeft, Delay, Rgb; public int Shown = -1; }
        private readonly MonFade[] _monFades = { new MonFade(), new MonFade() };
        /// <summary>The blend weights of translucent sprites and the screen behind them; the animation starts at 8/8.</summary>
        public int BlendEva { get; private set; } = 8;
        public int BlendEvb { get; private set; } = 8;
        private uint _lcrng;
        /// <summary>Seed for LCRNG_Next, the random numbers some sprite effects use.</summary>
        public uint SpriteSeed { set => _lcrng = value; }

        private sealed class LoopFrame { public int Body, Total, Count; }
        private readonly List<LoopFrame> _loops = new List<LoopFrame>();
        private readonly List<int> _callStack = new List<int>();

        private int _pc, _wait;
        private bool _scriptDone;
        private int _guard;

        public readonly double[] MonShakeX = { 0, 0 };
        public readonly double[] MonShakeY = { 0, 0 };
        public readonly double[] MonMosaic = { 0, 0 };
        public readonly double[] MonClip = { 1, 1 };
        public readonly double[] MonAlpha = { 1, 1 };
        public int MonWarpMon { get; private set; } = -1;
        public double MonWarpAmp { get; private set; }
        public double MonWarpBaseDeg { get; private set; }
        public double MonWarpAddPerRow { get; private set; }
        public double MonWarpWidthA { get; private set; }
        public int MonWarpShimmer { get; private set; }
        public double ShakeX { get; private set; }
        public double ShakeY { get; private set; }
        public bool Grayscale { get; private set; }
        public double BgFlashAmount { get; private set; }
        public byte BgFlashR { get; private set; }
        public byte BgFlashG { get; private set; }
        public byte BgFlashB { get; private set; }
        public struct MonGhost { public int Mon; public double Dx, Dy, ScaleX, ScaleY, Alpha; public byte TintR, TintG, TintB; public double TintA; }
        private readonly List<MonGhost> _ghosts = new List<MonGhost>();
        public IReadOnlyList<MonGhost> Ghosts => _ghosts;

        public sealed class DroppedCap
        {
            public int SrcMon;
            public double Dx, Dy, ScaleX = 1, ScaleY = 1, Alpha = 1, RotDeg, Mosaic;
            public byte TintR, TintG, TintB; public double TintA;
            public bool Visible = true;
            public int Priority = 2;
            public double ClipOutX0, ClipOutY0, ClipOutX1 = -1, ClipOutY1 = -1;
        }
        private readonly Dictionary<int, DroppedCap> _caps = new Dictionary<int, DroppedCap>();
        public IReadOnlyCollection<DroppedCap> Caps => _caps.Values;
        private static int CapIdFromToolFlag(int flag)
        { for (int i = 0; i < 4; i++) if ((flag & (0x2 << i)) != 0) return i; return -1; }

        private sealed class Shake
        {
            public readonly int AmpX, AmpY, Sync, Num0; private int _cnt, _num, _step, _befX, _befY;
            public int X, Y;
            public Shake(int x, int y, int sync, int num)
            { AmpX = x; AmpY = y; Sync = sync; Num0 = num; _cnt = sync; _num = num; _befX = -x; _befY = -y; }
            private static void Tool(ref int now, ref int bef) { int t = bef; bef = now; now = (t == 0) ? 0 : -t; }
            public bool Calc()
            {
                if (_num == 0) return false;
                if (++_cnt >= Sync) { _cnt = 0; Tool(ref X, ref _befX); Tool(ref Y, ref _befY); if (++_step >= 4) { _step = 0; _num--; } }
                return true;
            }
        }

        private readonly BattleBgRenderer _bgRenderer = new BattleBgRenderer();
        private byte[] _bgRgba; private int _bgW, _bgH, _bgWrapW, _bgWrapH;
        private const int WET02_START_Y_OFS = 128;
        private const int WET02_STOP_Y_HI = 512, WET02_STOP_Y_LO = -412;
        private const int FxBgWrap = 512;
        private double _bgX, _bgY, _bgSpdX, _bgSpdY;

        private int _bgHoldLeft = -1;

        private void SetBackgroundParam(int which, int value)
        {
            switch (which)
            {
                case 0: _bgSpdX = value; break;
                case 1: _bgSpdY = value; break;
                case 2: _bgX = value; break;
                case 3: _bgY = value; break;
                default:
                    Note("background changes partway");
                    break;
            }
        }
        private double _bgOpacity, _bgPeak, _bgStopY; private int _bgFadeFrames; private bool _bgFadingOut, _bgOverlay, _bgUseStop;
        private readonly int[] _work = new int[16];

        private bool BackdropScrollReversedForSide()
        {
            int r = _work[6];
            if (r == 0) return false;
            bool dfMine = _dfVis == 0;
            if (r == 2 && _atVis == _dfVis) return _attackerIsEnemy;
            return dfMine;
        }
        private int _rasterLeft;
        public bool RasterActive => _rasterLeft > 0;
        public double RasterPhase { get; private set; }
        public double RasterAmp { get; private set; }
        public double RasterLineAdd { get; private set; } = Math.PI / 180.0;

        /// <summary>Rows from the top the wobble moves; the rest of the backdrop stays put.</summary>
        public int RasterRows { get; private set; } = 192;

        // An effect task with nothing to draw. It only holds the script's waits.
        private const int HoldOnly = 28;

        private const int BattlerFade = 29;
        private const int ScaleBattler = 31;
        private const int StrengthFx = 32, SpriteFades = 33, TeleportFx = 34, FlashFx = 35, SplashFx = 36, FlailFx = 37,
                          QuickAttackFx = 38, DrillPeckFx = 39, ConfusionFx = 40, NightShadeFx = 41, NightShadeHitFx = 42,
                          AcidArmorFx = 43, FaintAttackFx = 44, PlayfulHopsFx = 45, SpiteFx = 46, ReturnFx = 47, VitalThrowFx = 48,
                          MementoFx = 49, PosSeq = 50, FakeOutCurtainFx = 51, NightmareFx = 52, RotateFx = 53, ShakeScaleFx = 54,
                          ShadowPunchFx = 55;
        // ShakeContext with no interval: out, back, the other way, back, one move a pass.
        private static double ShakeStep(int update, double extent) => update % 4 == 1 ? extent : update % 4 == 3 ? -extent : 0;
        // A point on the game's oval loop (32 px wide, 8 px tall) at a fraction of a turn.
        private static (double x, double y) OvalLoop(double turn, double dir)
            => (Math.Sin(2 * Math.PI * turn) * 32 * dir, 8 * (1 - Math.Cos(2 * Math.PI * turn)));
        private const int SplashHopPasses = 21;
        private const int PartialDrawHeight = 80;
        /// <summary>Each side's sprite offset from the species' height data (0 for the player's side, 1 for the enemy's).</summary>
        public int[] SpriteYOffsets { get; } = new int[2];
        // How long the scrolling BG task runs: it fades the BG in 2 levels a pass, waits for the scroll to pass the
        // edge (the step drops towards 0 every slow-down + 1 passes), and fades back out the same way.
        private static int ScrollBgTaskRuns(bool muddy, double y, int stepY, int slowDown, int targetAlpha)
        {
            int bg = 0, other = muddy ? 16 : 31, state = 0, timer = 0, runs = 0;
            bool finished = false;
            while (state < 3 && runs < 2000)
            {
                runs++;
                int done = 0;
                if (state == 0)
                {
                    if (muddy) { if (bg < 16) bg += 2; else done++; if (other > 4) other -= 2; else done++; }
                    else { if (bg < targetAlpha - 2) bg += 2; else done++; if (other > 9) other -= 2; else done++; }
                    if (done == 2) { bg = muddy ? 16 : targetAlpha; other = muddy ? 4 : 7; state = 1; }
                }
                else if (state == 1) { if (finished) state = 2; }
                else
                {
                    if (bg > 2) bg -= 2; else done++;
                    if (other < (muddy ? 16 : 29)) other += 2; else done++;
                    if (done == 2) state = 3;
                }
                if ((stepY > 0 && y >= 512) || (stepY < 0 && y <= -412)) finished = true;
                y += stepY;
                if (slowDown < timer) { stepY = stepY < 0 ? stepY + 1 : 0; timer = 0; }
                else timer++;
            }
            return runs + 1;
        }

        private static int DoubleTeamSweepPasses()
        {
            int runs = 0;
            for (int i = 0; i < 9; i++) runs += 10 - (i + 1) / 2 + 1;
            return runs;
        }
        private static readonly int[] MinimizeDelays = { 2, 7, 13 };
        // Flail's swings: { degrees, steps }, each swung one way and back.
        private static readonly int[][] FlailSwings = { new[] { 20, 3 }, new[] { 15, 3 }, new[] { 10, 2 }, new[] { 5, 2 }, new[] { 0, 1 } };
        private static double FlailAngle(int f)
        {
            foreach (int[] sw in FlailSwings)
                for (int side = 0; side < 2; side++)
                {
                    int runs = sw[1] + 1;
                    if (f >= runs) { f -= runs; continue; }
                    // The angle holds on the pass that notices the swing is over.
                    double from = side == 0 ? -sw[0] : sw[0];
                    return from - 2 * from * Math.Min(f + 1, sw[1]) / sw[1];
                }
            return 0;
        }
        private static readonly double[][] SplashParts =
        {
            new double[] { 1, 1.2, 1, 0.8, 0, 16 }, new double[] { 1.2, 1, 0.8, 1.2, 16, 0 }, new double[] { 1, 1, 1.2, 1, 0, 0 },
        };
        // Strength blinks red 3 times: a pass starting the fade to 10, then 11 passes each way at one level a pass.
        private const int StrengthBlinks = 3, StrengthBlinkPasses = 23;
        private static readonly int[] EarthquakeExtents = { 12, 10, 8, 6, 4, 2, 1, 0 };
        // Earthquake's task waits for its BG fade until its 11th pass; each shake takes 20 passes and a pass to notice the end.
        private const int EarthquakeFirstShake = 10, EarthquakeShakePasses = 21;
        private static (double, double) ScaleStep(double sx, double ex, double sy, double ey, int steps, int k)
        {
            if (steps <= 0 || k <= 0) return (sx, sy);
            double dx = (ex - sx) / steps, dy = (ey - sy) / steps;
            return (sx + k * dx, sy + dx + (k - 1) * dy);
        }

        // Camouflage's stages, in passes.
        private const int CamouflageFade = 16, CamouflageScroll = 120, CamouflageFadeBack = CamouflageFade + 1 + CamouflageScroll,
                          CamouflagePasses = CamouflageFadeBack + CamouflageFade + 2;
        public bool HasBackground => _bgRgba != null && _bgOpacity > 0.001;
        public bool BackgroundIsOverlay => _bgOverlay;

        // The passes on which WaitForBgSwitch and WaitForPartialBgSwitch let the script go on.
        private int _bgSwitchDonePass, _bgSwitchPartialPass = int.MaxValue, _bgSwitchFade = 12;

        // Passes until the script sees the switch done. Blend: 2 levels a pass. Fade: two 9-pass palette fades.
        // A blend restore adds 3 passes.
        private void StartBgSwitch(int mode, bool restore)
        {
            int blend = _work.Length > 5 ? _work[5] : 0;
            int n = mode == 1 ? 20
                  : mode == 2 ? 2
                  : restore ? (blend == 1 ? 17 : blend == 2 ? 16 : 20)
                  : (blend == 1 ? 15 : blend == 2 ? 14 : 18);
            _bgSwitchDonePass = _pass + n;
            _bgSwitchPartialPass = mode == 1 && !restore ? _pass + 11 : int.MaxValue;
            _bgSwitchFade = Math.Max(1, n - 2);
        }
        public double BgCa { get; private set; }
        public double BgCb { get; private set; }
        public bool TrySampleBg(int x, int y, out byte r, out byte g, out byte b, out byte a)
        {
            r = g = b = a = 0;
            if (_bgRgba == null) return false;
            int sx = ((int)Math.Round(_bgX) % _bgWrapW + _bgWrapW) % _bgWrapW;
            int sy = ((int)Math.Round(_bgY) % _bgWrapH + _bgWrapH) % _bgWrapH;
            int tx = (sx + x) % _bgWrapW, ty = (sy + y) % _bgWrapH;
            if (tx >= _bgW || ty >= _bgH) return false;
            int i = (ty * _bgW + tx) * 4;
            r = _bgRgba[i]; g = _bgRgba[i + 1]; b = _bgRgba[i + 2]; a = _bgRgba[i + 3];
            return true;
        }

        private double _cellScaleX = 1, _cellScaleY = 1, _cellOpacity = 1;
        private int _cellPhase = -1, _cellFrame, _cellDefX, _cellDefY;
        private CellActor _surfActor;
        private const int FnSurf = 49;
        private const int SurfSpriteHeight = 16;

        public WeCellAnimRenderer Cells { get; set; }
        public int MovePower { get; set; } = -1;
        private CellSequence[] _cellSeqs;
        private CellSequence[] CellSeqs => _cellSeqs ??= (Cells != null && Cells.Loaded ? Cells.BuildSequences() : Array.Empty<CellSequence>());
        private readonly List<CellActor> _spriteActors = new List<CellActor>();
        public IReadOnlyList<CellActor> SpriteActors => _spriteActors;
        private static readonly (int x, int y) SurfHomePlayer = (76, 120), SurfHomeEnemy = (144, 64);
        private static double Lerp(double a, double b, double t) => a + (b - a) * Math.Clamp(t, 0, 1);

        private readonly List<string> _notes = new List<string>();
        public IReadOnlyList<string> Notes => _notes;

        private readonly List<int> _routinesRun = new List<int>();
        public IReadOnlyList<int> RoutinesRun => _routinesRun;

        private readonly List<int> _commandsRun = new List<int>();
        public IReadOnlyList<int> CommandsRun => _commandsRun;

        /// <summary>What ran and when, for the timeline.</summary>
        public BattleAnimTrace Trace { get; } = new BattleAnimTrace();
        private BattleAnimTrace.Item _scriptItem, _fadeItem;
        private int _lastCmd = -1;
        private readonly List<(MonFx Fx, BattleAnimTrace.Item Item)> _tracedFx = new List<(MonFx, BattleAnimTrace.Item)>();
        private readonly List<(SplEmitter Emitter, BattleAnimTrace.Item Item)> _tracedEmitters = new List<(SplEmitter, BattleAnimTrace.Item)>();

        private BattleAnimTrace.Item AddTrace(BattleAnimTrace.Lane lane, int cmd, int end = -1)
        {
            BattleAnimTrace.Item item = new BattleAnimTrace.Item { Lane = lane, Start = _pass, End = end < 0 ? _pass : end, Command = cmd };
            Trace.Items.Add(item);
            return item;
        }

        // Tags each effect task a command just started with that command and its start pass.
        private void TagNewWork(int cmd)
        {
            if (cmd < 0) return;
            foreach (MonFx fx in _monFx)
                if (fx.TraceCmd < 0) { fx.TraceCmd = cmd; _tracedFx.Add((fx, AddTrace(BattleAnimTrace.Lane.Effects, cmd))); }
            foreach (SoundTask t in _soundTasks) if (t.Cmd < 0) t.Cmd = cmd;
            if (_fadeFramesLeft > 0 && _fadeItem == null) _fadeItem = AddTrace(BattleAnimTrace.Lane.Background, cmd);
        }

        // Moves the end of everything still running to this pass, and lets go of what has finished.
        private void TraceEndOfPass()
        {
            for (int i = _tracedFx.Count - 1; i >= 0; i--)
            {
                _tracedFx[i].Item.End = _pass;
                if (!_monFx.Contains(_tracedFx[i].Fx)) _tracedFx.RemoveAt(i);
            }
            for (int i = _tracedEmitters.Count - 1; i >= 0; i--)
            {
                if (!EmitterLive(_tracedEmitters[i].Emitter)) { _tracedEmitters.RemoveAt(i); continue; }
                _tracedEmitters[i].Item.End = _pass;
            }
            if (_fadeItem != null) { _fadeItem.End = _pass; if (_fadeFramesLeft <= 0) _fadeItem = null; }
            if (_scriptItem != null) _scriptItem.End = _pass;
            Trace.TotalPasses = _pass;
        }
        private void Note(string what) { if (!_notes.Contains(what)) _notes.Add(what); }

        private double _fadeCur, _fadeStart, _fadeEnd; private int _fadeFrames, _fadeFramesLeft;
        public double FadeOpacity => Math.Clamp(_fadeCur, 0, 1);
        public byte FadeR { get; private set; }
        public byte FadeG { get; private set; }
        public byte FadeB { get; private set; }

        private readonly int _atVis, _dfVis;

        public BattleAnimPlayer(List<WazaSeqCommand> cmds, WazaSeqVersion version, ScriptNarc particleNarc,
                          double atX, double atY, double dfX, double dfY, int width = 256, int height = 192,
                          bool attackerIsEnemy = false, bool selfTarget = false)
        {
            _cmds = cmds ?? new List<WazaSeqCommand>();
            _version = version; _particleNarc = particleNarc;
            _attackerIsEnemy = attackerIsEnemy;
            _atVis = attackerIsEnemy ? 1 : 0;
            _dfVis = selfTarget ? _atVis : (attackerIsEnemy ? 0 : 1);
            // Effects anchor to the battle's fixed battler points (BattleAnimUtil_GetBattlerPos), not to where each
            // species is drawn.
            (double X, double Y) at = BattlerPoint(_atVis), df = BattlerPoint(_dfVis);
            _monCX[_atVis] = (int)Math.Round(atX); _monCY[_atVis] = (int)Math.Round(atY);
            _monCX[1 - _atVis] = (int)Math.Round(dfX); _monCY[1 - _atVis] = (int)Math.Round(dfY);
            _atX = at.X; _atY = at.Y;
            _dfX = df.X; _dfY = df.Y;
            _particles = new BattleParticles(particleNarc, _splRandom, attackerIsEnemy, selfTarget);
            int wp = 0;
            for (int i = 0; i < _cmds.Count; i++)
            {
                _cmds[i].WordPos = wp;
                _wordToIndex[wp] = i;
                wp += 1 + _cmds[i].Args.Length;
            }
        }

        public bool SecondTurnVariant { get; set; }

        /// <summary>0 no weather, 1 rain, 2 sandstorm, 3 sun, 4 hail.</summary>
        public int Weather { get; set; }

        /// <summary>A contest appeal: the contest branches, and the contestants' points, directions and particle
        /// positions. Set it before the first Step.</summary>
        public bool Contest
        {
            get => _contest;
            set
            {
                _contest = value;
                _particles.Contest = value;
                (_atX, _atY) = BattlerPoint(_atVis);
                (_dfX, _dfY) = BattlerPoint(_dfVis);
            }
        }
        private bool _contest;

        private readonly bool _attackerIsEnemy;
        // The game never seeds its particle randomness, so any start value is as faithful as another.
        private readonly SplRandom _splRandom = new SplRandom(DefaultSeed);
        public const uint DefaultSeed = 0x5EED;

        /// <summary>Where the particle randomness starts. Set it before the first Step.</summary>
        public uint Seed { get => _seed; set { _seed = value; _splRandom.Reset(value); } }
        private uint _seed = DefaultSeed;
        private readonly Dictionary<int, int> _wordToIndex = new Dictionary<int, int>();

        public Action<int> PlaySound;

        public Action PlayCry;

        public Action<int> StopSound;

        /// <summary>How many passes a sound effect plays for, so End and the sound waits hold as long as the game's.
        /// Null treats every sound as already finished.</summary>
        public Func<int, int> SoundPasses;

        /// <summary>How many passes the attacker's cry plays for.</summary>
        public Func<int> CryPasses;

        // The game's sound tasks: a repeated sound, a delayed one, or a pan that moves while a sound plays.
        private enum SoundTaskKind { Pan, Repeat, Delay }
        private sealed class SoundTask
        {
            public SoundTaskKind Kind;
            public int Sound, Interval, Tick, Count, StartPan, EndPan, PanStep, CurPan;
            public bool Started;
            public int Cmd = -1;
        }
        private readonly List<SoundTask> _soundTasks = new List<SoundTask>();
        private readonly List<(int Sound, int EndPass)> _playing = new List<(int, int)>();
        private int _cryEndPass;

        private void StartSound(int soundId, int cmd = -2)
        {
            PlaySound?.Invoke(soundId);
            int end = _pass + (SoundPasses?.Invoke(soundId) ?? 0);
            _playing.Add((soundId, end));
            AddTrace(BattleAnimTrace.Lane.Sound, cmd == -2 ? _lastCmd : cmd, end).A = soundId;
        }

        private bool IsSoundPlaying(int soundId)
        {
            foreach ((int Sound, int EndPass) p in _playing) if (p.Sound == soundId && p.EndPass > _pass) return true;
            return false;
        }

        private bool IsAnySoundPlaying()
        {
            foreach ((int Sound, int EndPass) p in _playing) if (p.EndPass > _pass) return true;
            return false;
        }

        // A pan stored as a signed byte, flipped so it points from the attacker's side.
        private int CorrectPan(int pan)
        {
            int p = (sbyte)pan;
            bool atEnemy = _atVis == 1, dfEnemy = _dfVis == 1;
            if (atEnemy && !dfEnemy) p = -p;
            else if (!atEnemy && !dfEnemy) { if (p > 0) p = -p; }
            else if (atEnemy && dfEnemy) { if (p < 0) p = -p; }
            return (sbyte)p;
        }

        private enum PanFix { None, Ends, EndsAndStepSign }

        // The pan starts from 0, not from the start value: the game clears the task and never copies it across.
        private void StartPanTask(int[] a, PanFix fix)
        {
            if (a.Length < 5) { if (a.Length >= 1) StartSound(a[0]); return; }
            int start = fix == PanFix.None ? (sbyte)a[1] : CorrectPan(a[1]);
            int end = fix == PanFix.None ? (sbyte)a[2] : CorrectPan(a[2]);
            int step = fix switch
            {
                PanFix.EndsAndStepSign => start < end ? Math.Abs((sbyte)a[3]) : start > end ? -Math.Abs((sbyte)a[3]) : 0,
                PanFix.Ends => CorrectPan(a[3]),
                _ => (sbyte)a[3],
            };
            StartSound(a[0]);
            _soundTasks.Add(new SoundTask { Kind = SoundTaskKind.Pan, Sound = a[0], StartPan = start, EndPan = end,
                                            PanStep = step, Interval = (byte)a[4] });
        }

        // Runs each sound task that was already running at the start of this pass, the way the game's task
        // queue does: a task added this pass is only switched on, and first runs on the next.
        private void RunSoundTasks()
        {
            for (int i = 0; i < _soundTasks.Count; i++)
            {
                SoundTask t = _soundTasks[i];
                if (!t.Started) continue;
                bool alive = true;
                switch (t.Kind)
                {
                    case SoundTaskKind.Repeat:
                        if (t.Tick++ < t.Interval) break;
                        t.Tick = 0;
                        t.Count = (byte)(t.Count - 1);
                        StartSound(t.Sound, t.Cmd);
                        if (t.Count == 0) alive = false;
                        break;
                    case SoundTaskKind.Delay:
                        if (t.Interval == 0) { StartSound(t.Sound, t.Cmd); alive = false; }
                        else t.Interval--;
                        break;
                    case SoundTaskKind.Pan:
                        if (t.Tick++ < t.Interval) break;
                        t.Tick = 0;
                        t.CurPan += t.PanStep;
                        if (t.PanStep == 0) alive = false;
                        else if (t.StartPan < t.EndPan) { if (t.CurPan >= t.EndPan) alive = false; }
                        else if (t.CurPan <= t.EndPan) alive = false;
                        if (!IsSoundPlaying(t.Sound)) alive = false;
                        break;
                }
                if (!alive) { _soundTasks.RemoveAt(i); i--; }
            }
        }

        private bool JumpRelative(int argWord, int offset)
        {
            if (_wordToIndex.TryGetValue(argWord + offset, out int idx)) { _pc = idx; return true; }
            return false;
        }

        /// <summary>The move is over: End has finished, as when the game stops the script.</summary>
        public bool Finished => _scriptDone;

        /// <summary>Script passes run so far; the game runs one every two screen frames.</summary>
        public int Pass => _pass;

        /// <summary>The particles as premultiplied BGRA rows, for drawing over the scene. The buffer is reused.</summary>
        public byte[] RenderParticlesBgra() => _particles.Render();

        public WriteableBitmap RenderFrame()
        {
            byte[] px = _particles.Render();
            WriteableBitmap wb = new WriteableBitmap(new global::Avalonia.PixelSize(SplRenderer.W, SplRenderer.H), new global::Avalonia.Vector(96, 96),
                PixelFormat.Bgra8888, AlphaFormat.Premul);
            using (ILockedFramebuffer fb = wb.Lock())
                for (int y = 0; y < SplRenderer.H; y++)
                    System.Runtime.InteropServices.Marshal.Copy(px, y * SplRenderer.W * 4, fb.Address + y * fb.RowBytes, SplRenderer.W * 4);
            return wb;
        }

        private int _pass, _bgStarts;
        private bool _delayActive, _endDelayDone, _particlesPending;
        private int _sfxWaitPasses;
        private readonly HashSet<CellActor> _actorsStarted = new HashSet<CellActor>();

        private bool EffectTasksActive() => _monFx.Exists(f => !f.SysTask) || _cellPhase >= 0 || _fadeFramesLeft > 0;

        /// <summary>
        /// One pass of the game's main loop: the script first, then every task that was already running, then the
        /// particles are drawn and moved on. A task the script starts in this pass first runs in the next one.
        /// </summary>
        public void Step()
        {
            _pass++;
            // The game draws the particles and only then moves them, so last pass's move happens here.
            if (_particlesPending) _particles.Update();
            _particlesPending = true;

            foreach (MonFx fx in _monFx) fx.Started = true;
            foreach (SoundTask t in _soundTasks) t.Started = true;
            _actorsStarted.Clear();
            foreach (CellActor a in _spriteActors) _actorsStarted.Add(a);
            // The game draws a sprite and then moves its animation on, so last pass's step is taken here.
            foreach (CellActor a in _spriteActors)
            {
                if (!a.TaskDriven) continue;
                if (a.PendingTicks > 0) a.TickBy(a.PendingTicks);
                a.PendingTicks = 0; a.Drawn = false;
            }
            bool cellRunning = _cellPhase >= 0;
            int bgStarts = _bgStarts, fadeLeft = _fadeFramesLeft, rasterLeft = _rasterLeft;

            RunScript();

            if (_fadeFramesLeft == fadeLeft) UpdateFade();
            UpdateMonFx();
            UpdateMonFades();
            UpdateBrightness();
            if (_bgStarts == bgStarts) UpdateBackground();
            if (_rasterLeft > 0 && _rasterLeft == rasterLeft) { _rasterLeft--; RasterPhase += 200.0 / 65536.0 * 2 * Math.PI; }
            if (cellRunning) UpdateCellFx();
            for (int i = 0; i < _spriteActors.Count; i++)
            {
                CellActor a = _spriteActors[i];
                if (a == _surfActor || a.TaskDriven || !_actorsStarted.Contains(a)) continue;
                a.Tick(); a.Age++;
            }
            RunSoundTasks();
            TraceEndOfPass();
        }

        // The game's delay waiter counts the delay down to zero, then spends one more pass switching back before
        // commands run again, so a delay of N holds the script for N + 2 passes.
        private void RunScript()
        {
            if (_scriptDone) return;
            if (_delayActive)
            {
                if (_wait == 0) _delayActive = false;
                else _wait--;
                return;
            }
            RunCommands();
            TagNewWork(_lastCmd);
        }

        private void Hold(int passes) { _wait = passes; _delayActive = true; }

        private void RunCommands()
        {
            while (_pc < _cmds.Count)
            {
                if (++_guard > 100000) { _scriptDone = true; return; }
                WazaSeqCommand c = _cmds[_pc];
                string name = BattleAnimCommands.Name(_version, c.OpId);
                // A wait that isn't over runs again next pass; list it once.
                TagNewWork(_lastCmd);
                if (_commandsRun.Count == 0 || _commandsRun[_commandsRun.Count - 1] != _pc)
                {
                    _commandsRun.Add(_pc);
                    if (_scriptItem != null) _scriptItem.End = _pass;
                    _scriptItem = AddTrace(BattleAnimTrace.Lane.Script, _pc);
                }
                _lastCmd = _pc;
                _pc++;

                switch (name)
                {
                    case "End":
                        // The game always spends one pass on End, then holds it until every emitter, effect and
                        // sound task is gone and the sounds have stopped, giving up on the sounds after 90 passes.
                        if (!_endDelayDone) { _endDelayDone = true; _pc--; return; }
                        if (_particles.ActiveEmitterCount != 0 || EffectTasksActive() || _soundTasks.Count > 0) { _sfxWaitPasses = 0; _pc--; return; }
                        if (IsAnySoundPlaying() && ++_sfxWaitPasses <= 90) { _pc--; return; }
                        _scriptDone = true;
                        return;

                    case "PlaySoundEffect":
                    case "PlayPannedSoundEffect":
                        if (c.Args.Length >= 1) StartSound(c.Args[0]);
                        break;

                    case "PlayMovingSoundEffectAtkDef":
                        StartPanTask(c.Args, PanFix.EndsAndStepSign);
                        break;
                    case "PlayMovingSoundEffectAtkDef2":
                        StartPanTask(c.Args, PanFix.Ends);
                        break;
                    case "PlayMovingSoundEffectNoCorrection":
                        StartPanTask(c.Args, PanFix.None);
                        break;

                    case "PlayDelayedSoundEffect":
                        if (c.Args.Length >= 3)
                            _soundTasks.Add(new SoundTask { Kind = SoundTaskKind.Delay, Sound = c.Args[0], Interval = (byte)c.Args[2] });
                        else if (c.Args.Length >= 1) StartSound(c.Args[0]);
                        break;

                    case "PlayLoopedSoundEffect":
                        if (c.Args.Length >= 4)
                            _soundTasks.Add(new SoundTask { Kind = SoundTaskKind.Repeat, Sound = c.Args[0], Interval = (byte)c.Args[2],
                                                            Tick = (byte)c.Args[2], Count = (byte)c.Args[3] });
                        else if (c.Args.Length >= 1) StartSound(c.Args[0]);
                        break;

                    case "WaitForSoundEffects":
                        if (_soundTasks.Count > 0) { _sfxWaitPasses = 0; _pc--; return; }
                        if (IsAnySoundPlaying() && ++_sfxWaitPasses <= 90) { _pc--; return; }
                        _sfxWaitPasses = 0;
                        break;

                    case "JumpByTurn":
                        if (SecondTurnVariant)
                        {
                            if (c.Args.Length >= 2 && JumpRelative(c.WordPos + 2, c.Args[1])) break;
                        }
                        if (c.Args.Length >= 1 && JumpRelative(c.WordPos + 1, c.Args[0])) break;
                        if (c.Args.Length >= 2) JumpRelative(c.WordPos + 2, c.Args[1]);
                        break;

                    case "JumpIfWeather":
                        // One offset per weather: none, rain, sandstorm, sun, hail.
                        {
                            int w = Weather >= 0 && Weather < c.Args.Length ? Weather : 0;
                            if (c.Args.Length >= 1) JumpRelative(c.WordPos + 1 + w, c.Args[w]);
                        }
                        break;

                    case "JumpIfContest":
                        if (Contest && c.Args.Length >= 1) JumpRelative(c.WordPos + 1, c.Args[0]);
                        break;
                    case "JumpIfFriendlyFire":
                        break;
                    case "Jump":
                        if (c.Args.Length >= 1) JumpRelative(c.WordPos + 1, c.Args[0]);
                        break;
                    case "JumpIfBattlerSide":
                        if (c.Args.Length >= 3)
                        {
                            bool checkedIsEnemy = c.Args[0] == 0 ? _attackerIsEnemy : !_attackerIsEnemy;
                            if (checkedIsEnemy) { if (JumpRelative(c.WordPos + 3, c.Args[2])) break; }
                            else { if (JumpRelative(c.WordPos + 2, c.Args[1])) break; }
                        }
                        break;
                    case "BeginLoop":
                        _loops.Add(new LoopFrame { Body = _pc, Total = c.Args.Length > 0 ? c.Args[0] : 1, Count = 0 });
                        break;
                    case "EndLoop":
                        if (_loops.Count > 0)
                        {
                            LoopFrame f = _loops[_loops.Count - 1];
                            if (++f.Count >= f.Total) _loops.RemoveAt(_loops.Count - 1);
                            else _pc = f.Body;
                        }
                        break;
                    case "Call":
                        if (c.Args.Length >= 1) { _callStack.Add(_pc); JumpRelative(c.WordPos + 1, c.Args[0]); }
                        break;
                    case "Return":
                        if (_callStack.Count > 0) { _pc = _callStack[_callStack.Count - 1]; _callStack.RemoveAt(_callStack.Count - 1); }
                        break;

                    case "Delay": Hold(c.Args.Length > 0 ? (byte)c.Args[0] : 0); return;
                    // A wait that isn't over stays on its command and checks again next pass; one that is over
                    // carries straight on in the same pass.
                    case "WaitForAnimTasks":
                        if (EffectTasksActive()) { _pc--; return; }
                        break;
                    case "WaitForAllEmitters":
                        if (_particles.ActiveEmitterCount != 0) { _pc--; return; }
                        break;

                    case "LoadParticleSystem":
                    case "LoadDebugParticleSystem":
                        if (c.Args.Length >= 2) _particles.Load(c.Args[0], c.Args[1]);
                        Hold(2);   // the game waits for the particle upload
                        return;
                    case "SetCameraProjection":
                        if (c.Args.Length >= 2) _particles.SetCameraProjection(c.Args[0], c.Args[1]);
                        break;
                    case "SetCameraFlip":
                        if (c.Args.Length >= 2) _particles.SetCameraFlip(c.Args[0], c.Args[1]);
                        break;
                    case "UnloadParticleSystem":
                        if (c.Args.Length >= 1) _particles.Unload(c.Args[0]);
                        break;

                    case "CreateEmitter":
                        if (c.Args.Length >= 2) CreateEmitter(c.Args[0], 0, c.Args[1], c.Args.Length >= 3 ? c.Args[2] : 0);
                        break;
                    case "CreateEmitterEx":
                        if (c.Args.Length >= 4) CreateEmitter(c.Args[0], c.Args[1], c.Args[2], c.Args[3]);
                        break;
                    case "CreateEmitterForMove":
                    case "CreateEmitterForFriendlyFire":
                        if (c.Args.Length >= 3)
                        {
                            int cb = c.Args[c.Args.Length - 1], count = c.Args.Length - 2;
                            int sep = _attackerIsEnemy ? 3 : 0;
                            if (sep >= count) sep = 0;
                            CreateEmitter(c.Args[0], 0, c.Args[1 + sep], cb);
                        }
                        break;

                    case "AddSprite":
                        AddSpriteActor(c.Args.Length > 0 ? c.Args[0] : 0, c.Args.Length > 1 ? c.Args[1] : 0, withCallback: false, null);
                        break;
                    case "AddSpriteWithFunc":
                        AddSpriteActor(c.Args.Length > 0 ? c.Args[0] : 0, c.Args.Length > 1 ? c.Args[1] : -1, withCallback: true,
                            c.Args.Length > 9 ? c.Args[9..] : null);
                        break;
                    case "FreeSpriteManager":
                        _spriteActors.Clear();
                        break;

                    case "AddPokemonSprite":
                    case "CreatePokemonCopy":
                        if (c.Args.Length >= 3)
                        {
                            int srcMon = (c.Args[0] == 1 || c.Args[0] == 3) ? _dfVis : _atVis;
                            int capId = c.Args[2];
                            _caps[capId] = new DroppedCap { SrcMon = srcMon };
                        }
                        break;
                    case "RemovePokemonSprite":
                    case "RemovePokemonCopy":
                        if (c.Args.Length >= 1) _caps.Remove(c.Args[0]); else _caps.Clear();
                        break;
                    case "FreePokemonSpriteManager":
                        _caps.Clear();
                        break;

                    case "SetVar":
                        if (c.Args.Length >= 2 && c.Args[0] >= 0 && c.Args[0] < _work.Length) _work[c.Args[0]] = c.Args[1];
                        break;
                    case "ResetVars":
                        Array.Clear(_work, 0, _work.Length);
                        break;
                    case "SwitchBg":
                        if (c.Args.Length >= 1)
                        {
                            int hRev = BackdropScrollReversedForSide() ? -1 : 1;
                            StartBgSwitch(c.Args.Length >= 2 ? c.Args[1] & 0xFFFF : 0, restore: false);
                            StartBackground(c.Args[0], overlay: false, posX: _work[2] * hRev, posY: _work[3] * hRev,
                                spdX: _work[0] * hRev, spdY: _work[1] * hRev, peak: 1.0, fadeFrames: _bgSwitchFade, stopY: 0, useStop: false);
                        }
                        break;
                    case "RestoreBg":
                        StartBgSwitch(c.Args.Length >= 2 ? c.Args[1] & 0xFFFF : 0, restore: true);
                        _bgFadingOut = true;
                        if (_bgRgba != null) _bgFadeFrames = _bgSwitchFade;
                        break;
                    case "WaitForBgSwitch":
                        if (_pass < _bgSwitchDonePass) { _pc--; return; }
                        break;
                    case "WaitForPartialBgSwitch":
                        // Only the fade switch has a halfway point; the game waits forever on the others.
                        if (_pass < (_bgSwitchPartialPass == int.MaxValue ? _bgSwitchDonePass : _bgSwitchPartialPass)) { _pc--; return; }
                        break;

                    case "FlashScreen":
                        _fadeStart = 1.0; _fadeEnd = 0; _fadeCur = 1.0;
                        _fadeFrames = _fadeFramesLeft = c.Args.Length >= 1 && c.Args[0] > 0 ? c.Args[0] : 8; _palFade = null;
                        FadeR = FadeG = FadeB = 255;
                        break;
                    case "SwitchBgAnimated":
                        if (c.Args.Length >= 1)
                        {
                            // A blend switch to the BG for whichever side is attacking, or the contest one.
                            int bgEx = Contest && c.Args.Length >= 3 ? c.Args[2] : _dfVis == 0 && c.Args.Length >= 2 ? c.Args[1] : c.Args[0];
                            int hRevEx = BackdropScrollReversedForSide() ? -1 : 1;
                            StartBgSwitch(0, restore: false);
                            StartBackground(bgEx, overlay: false, posX: _work[2] * hRevEx, posY: _work[3] * hRevEx,
                                spdX: _work[0] * hRevEx, spdY: _work[1] * hRevEx, peak: 1.0, fadeFrames: _bgSwitchFade, stopY: 0, useStop: false);
                        }
                        break;
                    case "JumpIfBatonPass":
                        break;

                    // Stops the sound only; a repeat task already running carries on and plays it again.
                    case "StopSoundEffect":
                        if (c.Args.Length >= 1)
                        {
                            int stopId = c.Args[0];
                            _playing.RemoveAll(x => x.Sound == stopId);
                            StopSound?.Invoke(stopId);
                        }
                        break;

                    case "PlayPokemonCry":
                        if (PlayCry != null) PlayCry();
                        else Note("cry");
                        _cryEndPass = _pass + (CryPasses?.Invoke() ?? 0);
                        break;

                    // Waits for the cry to finish; the value is how long the cry fades out after that.
                    case "WaitForPokemonCries":
                        if (_cryEndPass > _pass) { _pc--; return; }
                        break;

                    case "SetPokemonSpriteVisible":
                        if (c.Args.Length >= 2 && _caps.TryGetValue(c.Args[0], out DroppedCap oamCap))
                            oamCap.Visible = c.Args[1] != 0;
                        break;

                    case "StartTransform":
                    case "StartTransformRecolour":
                        Note("Transform's new sprite");
                        break;

                    case "LoadPokemonSpriteIntoBg":
                        Note("Pokémon copy in the background");
                        break;
                    case "RemovePokemonSpriteFromBg":
                        break;

                    case "SetBgSwitchVar":
                        if (c.Args.Length >= 2) SetBackgroundParam(c.Args[0], c.Args[1]);
                        break;

                    case "InitPokemonSpriteManager":
                    case "LoadPokemonSpriteDummyResources":
                    case "InitSpriteManager":
                    case "LoadCharResObj":
                    case "LoadPlttRes":
                    case "LoadCellResObj":
                    case "LoadAnimResObj":
                        break;

                    case "SetExtraParams":
                        break;

                    case "WaitForLRX":
                        break;

                    case "CallFunc":
                    case "Nop11":
                        DoFuncCall(c.Args);
                        break;
                }
            }
            _scriptDone = true;
        }

        private readonly BattleParticles _particles;

        // PokemonSpriteManager_UpdateCharAndPltt: each step blends the palette at the current level, then moves it on.
        // BrightnessController: the master brightness of the background and the Pokemon, -16 black to 16 white.
        private sealed class Brightness { public bool Active; public int Target, Level, Dir, StepDelta, Remainder, Acc, Steps; }
        private readonly Brightness _brightness = new Brightness();
        public int ScreenBrightness => _brightness.Level;

        // BrightnessController_StepTransition.
        private void UpdateBrightness()
        {
            Brightness b = _brightness;
            if (!b.Active) return;
            bool finished = false;
            if (b.Level + b.Dir * b.StepDelta != b.Target && b.Level != b.Target)
            {
                b.Level += b.Dir * b.StepDelta;
                b.Acc += b.Remainder;
                if (b.Acc >= b.Steps)
                {
                    b.Level += b.Dir;
                    if (b.Level != b.Target) b.Acc -= b.Steps;
                    else finished = true;
                }
            }
            else { b.Level = b.Target; finished = true; }
            if (finished) b.Active = false;
        }

        void IBattleAnimSystem.StartBrightness(int steps, int target, int start)
        {
            if (steps == 0) return;
            Brightness b = _brightness;
            b.Active = true; b.Steps = steps; b.Target = target; b.Level = start;
            int delta = start - target;
            if (delta > 0) b.Dir = -1; else { b.Dir = 1; delta = -delta; }
            b.StepDelta = delta / steps; b.Remainder = delta % steps; b.Acc = 0;
        }

        bool IBattleAnimSystem.BrightnessDone => !_brightness.Active;

        private void UpdateMonFades()
        {
            for (int m = 0; m < 2; m++)
            {
                MonFade f = _monFades[m];
                if (f.Active)
                {
                    if (f.DelayLeft == 0)
                    {
                        f.DelayLeft = f.Delay;
                        f.Shown = f.Cur;
                        if (f.Cur == f.Target) f.Active = false;
                        else if (f.Cur > f.Target) f.Cur--;
                        else f.Cur++;
                    }
                    else f.DelayLeft--;
                }
                if (f.Shown > 0)
                {
                    MonTintA[m] = Math.Min(16, f.Shown) / 16.0;
                    TintR = R5(f.Rgb); TintG = G5(f.Rgb); TintB = B5(f.Rgb);
                }
            }
        }

        int IBattleAnimSystem.Attacker => _atVis;
        int IBattleAnimSystem.Defender => _dfVis;
        // BattleAnimUtil_GetTransformDirectionX: in a contest the player's side faces left.
        int IBattleAnimSystem.TransformDirectionX(int battler) => _contest ? (battler == 0 ? -1 : 1) : battler == 1 ? -1 : 1;
        bool IBattleAnimSystem.Contest => _contest;
        int IBattleAnimSystem.TransformDirectionY(int battler) => battler == 1 ? -1 : 1;
        int IBattleAnimSystem.MonScaleX(int battler) => _monAttrScaleX[battler];
        int IBattleAnimSystem.MonScaleY(int battler) => _monAttrScaleY[battler];
        void IBattleAnimSystem.SetMonScale(int battler, int x, int y) { _monAttrScaleX[battler] = x; _monAttrScaleY[battler] = y; }
        void IBattleAnimSystem.SetMonHidden(int battler, bool hidden) => _monAttrHidden[battler] = hidden;
        int IBattleAnimSystem.MonYOffset(int battler) => SpriteYOffsets[battler];
        void IBattleAnimSystem.StartMonFade(int battler, int from, int to, int delay, int rgb15)
        {
            MonFade f = _monFades[battler];
            f.Active = true; f.Cur = from; f.Target = to; f.DelayLeft = 0; f.Delay = delay; f.Rgb = rgb15;
        }
        bool IBattleAnimSystem.MonFadeActive(int battler) => _monFades[battler].Active;
        void IBattleAnimSystem.SetBlendAlpha(int eva, int evb) { BlendEva = eva & 0x1F; BlendEvb = evb & 0x1F; }

        // Its own task at priority 0, so it first runs in the next pass, ahead of the effect tasks.
        AlphaFade IBattleAnimSystem.StartAlphaFade(int ev1From, int ev1To, int ev2From, int ev2To, int steps)
        {
            AlphaFade fade = new AlphaFade();
            PosLerp lerp = new PosLerp(ev1From, ev1To, ev2From, ev2To, steps);
            AddTask(() =>
            {
                if (!lerp.Update()) { fade.Done = true; return false; }
                BlendEva = Math.Max(0, lerp.X) & 0x1F; BlendEvb = Math.Max(0, lerp.Y) & 0x1F;
                return true;
            }, 0, sysTask: true);
            return fade;
        }

        CellActor IBattleAnimSystem.NewSprite(CellActor like)
        {
            CellActor sprite = new CellActor(CellSeqs, 0) { FuncId = like.FuncId, Gp = like.Gp, Manager = like.Manager, TaskDriven = true,
                X = _dfX, Y = _dfY, BaseX = _dfX, BaseY = _dfY, CapId = 0, Visible = CellSeqs.Length > 0 };
            _spriteActors.Add(sprite);
            return sprite;
        }

        void IBattleAnimSystem.StartTask(Func<bool> task, int priority) => AddTask(task, priority);
        void IBattleAnimSystem.StartSysTask(Func<bool> task, int priority) => AddTask(task, priority, sysTask: true);

        int IBattleAnimSystem.Random()
        {
            _lcrng = unchecked(_lcrng * 1103515245u + 24691u);
            return (int)(_lcrng >> 16);
        }
        (int X, int Y) IBattleAnimSystem.BattlerPos(int battler) { (double x, double y) = BattlerPoint(battler); return ((int)x, (int)y); }
        int IBattleAnimSystem.MonX(int battler) => _monCX[battler] + (int)Math.Round(MonDX[battler]);
        int IBattleAnimSystem.MonY(int battler) => _monCY[battler] + (int)Math.Round(MonDY[battler]);
        void IBattleAnimSystem.SetMonX(int battler, int x) => MonDX[battler] = (short)x - _monCX[battler];
        void IBattleAnimSystem.SetMonY(int battler, int y) => MonDY[battler] = (short)y - _monCY[battler];
        void IBattleAnimSystem.DeleteSprite(CellActor sprite) { sprite.Alive = false; _spriteActors.Remove(sprite); }

        // Sprite_UpdateAnim steps a ManagedSprite's animation by its speed, FX32_ONE * 2 unless set.
        void IBattleAnimSystem.DrawSprites(int manager)
        {
            foreach (CellActor a in _spriteActors)
            {
                if (!a.TaskDriven || a.Manager != manager) continue;
                if (a.Visible) a.Drawn = true;
                if (a.Animate) a.PendingTicks += 2;
            }
        }

        // sBattleAnimBattlerPositions: the single battle's points, or the contestants'.
        private (double X, double Y) BattlerPoint(int vis) =>
            _contest ? (vis == 0 ? (216.0, 112.0) : (80.0, 42.0)) : vis == 0 ? (64.0, 112.0) : (192.0, 48.0);

        // The callbacks read the SetExtraParams blocks after the command, and the script carries on past them.
        private int[] NextExtraParams()
        {
            if (_pc >= _cmds.Count || BattleAnimCommands.Name(_version, _cmds[_pc].OpId) != "SetExtraParams") return null;
            int[] args = _cmds[_pc].Args;
            _pc++;
            return args.Length > 1 ? args[1..] : Array.Empty<int>();
        }

        private void CreateEmitter(int slot, int index, int resource, int callback)
        {
            _particles.ExtraParams = NextExtraParams;
            SplEmitter e = _particles.CreateEmitter(slot, index, resource, callback);
            if (e == null) return;
            BattleAnimTrace.Item item = AddTrace(BattleAnimTrace.Lane.Particles, _lastCmd);
            item.A = slot; item.B = resource;
            _tracedEmitters.Add((e, item));
        }

        private bool EmitterLive(SplEmitter e)
        {
            foreach (SplManager m in _particles.Managers())
                for (SplEmitter x = m.Active.First; x != null; x = x.Next)
                    if (x == e) return true;
            return false;
        }

        private void AddTask(Func<bool> task, int priority = AnimTaskPriority, bool sysTask = false) =>
            _monFx.Add(new MonFx { Mon = _atVis, Kind = TaskFx, Task = task, Priority = priority, SysTask = sysTask });
        private const int AnimTaskPriority = 1100;

        // SysTaskManager_ExecuteTasks: lower priority numbers first, and tasks of one priority in the order they started.
        private void RunTasks()
        {
            List<MonFx> run = new List<MonFx>();
            foreach (MonFx fx in _monFx) if (fx.Kind == TaskFx && fx.Started) run.Add(fx);
            foreach (MonFx fx in System.Linq.Enumerable.OrderBy(run, f => f.Priority))
                if (!fx.Task()) _monFx.Remove(fx);
        }

        private const int WorkSlots = 8 + 2;

        private void DoFuncCall(int[] a)
        {
            if (a.Length < 1) return;

            if (a.Length < 2 + WorkSlots)
            {
                int[] padded = new int[2 + WorkSlots];
                Array.Copy(a, padded, a.Length);
                a = padded;
            }

            int fn = a[0];
            _routinesRun.Add(fn);
            if (FnMoveBattlerFamily.Contains(fn)) { MoveMon(a); return; }
            if (fn == FnSurf)
            {
                int castCap = _attackerIsEnemy ? 1 : 0;
                _surfActor = null;
                foreach (CellActor act in _spriteActors)
                {
                    if (act.CapId == castCap) { _surfActor = act; }
                    else if (act.CapId == 0 || act.CapId == 1) { act.Visible = false; act.Alive = false; }
                }
                (int x, int y) def = _attackerIsEnemy ? SurfHomeEnemy : SurfHomePlayer;
                _cellDefX = def.x; _cellDefY = def.y;
                _cellPhase = 0; _cellFrame = 0;
                if (_surfActor != null)
                {
                    if (_surfActor.SeqCount > castCap) _surfActor.SetSeq(castCap);
                    _surfActor.Visible = true; _surfActor.X = _cellDefX; _surfActor.Y = _cellDefY;
                    _surfActor.ScaleX = 1; _surfActor.ScaleY = 0.05; _surfActor.Alpha = 0;
                }
                return;
            }
            if ((fn == FnScrollCustomBg || fn == FnMuddyWater) && a.Length >= 10)
            {
                bool rev = a[7] != 0 && _attackerIsEnemy;
                double sgn = rev ? -1 : 1;
                int ofs = WET02_START_Y_OFS / 3 * 2;
                double spdY = a[6] * sgn;
                StartBackground(a[2], overlay: true, posX: a[3] * sgn, posY: a[4] * sgn + (rev ? -ofs : ofs),
                    spdX: a[5] * sgn, spdY: spdY, peak: Math.Min(a[8], 16) / 16.0, fadeFrames: 12,
                    stopY: spdY < 0 ? WET02_STOP_Y_LO : WET02_STOP_Y_HI, useStop: true);
                _monFx.Add(new MonFx { Mon = _atVis, Kind = HoldOnly,
                    Frames = ScrollBgTaskRuns(fn == FnMuddyWater, a[4] * sgn + (rev ? -ofs : ofs), (int)spdY, a[9], a[8]) });
                return;
            }
            switch (fn)
            {
                // The game's task spends its first pass setting up and one pass ending after the last shake.
                case FnShake when a.Length >= 6:
                {
                    int mode = a.Length > 6 ? a[6] : AnimDefender;
                    if ((mode & AnimBackground) != 0)
                    {
                        _monFx.Add(new MonFx { Kind = 5, Mon = 0, ToScene = true, Delay = 1, EndPass = true,
                            Sh = new Shake(a[2], a[3], Math.Max(1, a[4]), Math.Max(0, a[5])), NumMax = 0 });
                        break;
                    }
                    List<int> targets = TargetsFromFlags(mode);
                    foreach (int t in targets)
                        _monFx.Add(new MonFx { Kind = 5, Mon = t, ToScene = false, Delay = 1, EndPass = true,
                            Sh = new Shake(a[2], a[3], Math.Max(1, a[4]), Math.Max(0, a[5])), NumMax = 0 });
                    // With nothing to shake the task still runs its full length and holds the waits.
                    if (targets.Count == 0)
                    {
                        int amount = Math.Max(0, a[5]), interval = Math.Max(1, a[4]);
                        _monFx.Add(new MonFx { Kind = HoldOnly, Mon = _atVis, Frames = amount == 0 ? 3 : 4 + (4 * amount - 1) * interval });
                    }
                    break;
                }
                case FnShakeBg when a.Length >= 6:
                {
                    _monFx.Add(new MonFx { Kind = 5, Mon = 0, ToScene = true,
                        Sh = new Shake(a[2], a[3], Math.Max(1, a[4]), Math.Max(1, a[5])),
                        NumMax = a.Length > 6 ? Math.Max(0, a[6]) : 0, EndPass = true });
                    break;
                }

                case 53 when a.Length >= 7:
                {
                    // Shrinks while nudging right by 2 every other pair of passes, holds, grows, then snaps back.
                    int f1 = Math.Max(0, a[4]), f2 = Math.Max(0, a[5]), hold = Math.Max(0, a[6]);
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = ShakeScaleFx, Frames = f1 + 1 + hold + 1 + f2 + 3,
                        Keys = new double[] { a[2], a[3] }, UpF = f1, WaitF = hold, DownF = f2 });
                    break;
                }

                case 80:
                case 81:
                case 82:
                case 83:
                {
                    double speed = fn == 81 ? -3 : fn == 83 ? -6 : 3;
                    StartBackground(a[2], overlay: true, posX: 0, posY: 0, spdX: 0, spdY: speed,
                        peak: 12 / 16.0, fadeFrames: 12, stopY: 0, useStop: false);
                    _bgHoldLeft = 23;
                    // 2 passes of setup, 22 of scrolling, 12 fading out, and one ending.
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = HoldOnly, Frames = 37 });
                    break;
                }

                case FnFadeBg when a.Length >= 6:
                {
                    bool drawn = a[2] == 0;
                    if (!drawn) Note("effect layer colour fade");
                    StartBgPaletteFade(a[3], a[4], a[5], a.Length >= 7 ? a[6] : -1, drawn);
                    break;
                }

                case FnRotateMon when a.Length >= 5:
                {
                    // Turns from the start angle to the end in its steps and back, a pass noticing each end.
                    int steps = Math.Max(0, a[4]), mode = a.Length > 5 ? a[5] : 0;
                    int mon = mode == 0 || mode == 2 ? _dfVis : _atVis;
                    _monFx.Add(new MonFx { Mon = mon, Kind = RotateFx, Frames = 2 * (steps + 1) + 1, UpF = steps, Dx = mon == 0 ? 1 : -1,
                        Keys = new double[] { a[2] * 360.0 / 65536, a[3] * 360.0 / 65536 } });
                    break;
                }
                case FnScaleBattlerSprite when a.Length >= 10:
                {
                    int mon = MonFromFlag(a, 2);
                    if (mon < 0) break;
                    double rf = a[7] == 0 ? 100 : a[7];
                    int cycles = Math.Max(1, a[8] & 0xffff), hold = (a[8] >> 16) & 0xffff;
                    int scaleF = (a[9] >> 16) & 0xffff, restoreF = a[9] & 0xffff;
                    // A cycle is a pass starting the scale, its steps, the same back, and a pass for each stop; only the
                    // first cycle holds, since the task never resets its hold, and a last pass puts the scale back.
                    int per = Math.Max(1, scaleF) + Math.Max(1, restoreF) + 2;
                    _monFx.Add(new MonFx
                    {
                        // The routine runs the task's first pass itself.
                        Mon = mon, Kind = ScaleBattler, Frames = cycles * per + hold + 1, Started = true,
                        Keys = new[] { a[3] / rf, a[4] / rf, a[5] / rf, a[6] / rf }, UpF = scaleF, WaitF = hold, DownF = restoreF, Cycles = per,
                    });
                    break;
                }
                case FnScalePokemonSprite when a.Length >= 9:
                {
                    int sd = Math.Max(1, a[6]);
                    double s = a[4] / (double)sd, e = a[5] / (double)sd;
                    int num = Math.Max(1, a[7]);
                    int upF = Math.Max(0, (a[8] >> 16) & 0xffff), downF = Math.Max(0, a[8] & 0xffff);
                    int capId = a.Length > 9 ? a[9] : -1;
                    DroppedCap cap = (capId >= 0 && _caps.TryGetValue(capId, out DroppedCap dc)) ? dc : null;
                    // Each cycle: a pass to start, the steps and a pass noticing the end, the same back, then a last pass.
                    _monFx.Add(new MonFx
                    {
                        Mon = a[2] == 0 ? _atVis : _dfVis, Cap = cap, Kind = 1, Frames = num * (upF + downF + 4) + 1, ExactSteps = true,
                        Keys = new[] { s, e, s, e }, UpF = upF, WaitF = 0, DownF = downF, Cycles = num,
                    });
                    break;
                }
                case FnHideBattler:
                    {
                        int vm = MonFromFlag(a, 2);
                        if (vm >= 0) _monVanish[vm] = a.Length > 3 && a[3] != 0;
                    }
                    break;
                case FnMoveBattlerOffScreen:
                    {
                        int mon = MonFromFlag(a, 2), wait = Math.Max(1, a.Length > 3 ? a[3] : 1);
                        if (mon < 0) break;
                        double restX = mon == 0 ? _atX : _dfX;
                        _monFx.Add(new MonFx { Mon = mon, Kind = 4, Frames = wait, Dx = OffscreenX(mon) - restX, Dy = 0, TailRuns = 2 });
                    }
                    break;
                case FnMoveBattlerOnOrOffScreen:
                    {
                        int mode = a.Length > 2 ? a[2] : 0, mon = MonFromFlag(a, 3), wait = Math.Max(1, a.Length > 4 ? a[4] : 1);
                        if (mon < 0) break;
                        double restX = mon == 0 ? _atX : _dfX, off = OffscreenX(mon) - restX;
                        if (mode == 0) _monFx.Add(new MonFx { Mon = mon, Kind = 4, Frames = wait, Dx = off, Dy = 0 });
                        else { MonDX[mon] = off; _monFx.Add(new MonFx { Mon = mon, Kind = 4, Frames = wait, Dx = -off, Dy = 0 }); }
                    }
                    break;
                case FnMoveBattlerToDefaultPos:
                    {
                        int dm = MonFromFlag(a, 2);
                        if (dm >= 0) MonDX[dm] = MonDY[dm] = 0;
                    }
                    break;
                case FnSetBgGrayscale:
                    Grayscale = a.Length > 2 && a[2] != 0;
                    break;
                case 6:
                    AddScaleSteps(_atVis, new[] { new double[]{100,150,100,50,10}, new double[]{150,50,50,150,10},
                        new double[]{50,100,150,100,5}, new double[]{100,150,100,150,5}, new double[]{150,100,150,100,5} }, 1);
                    break;
                case 13:
                {
                    // Each grow or shrink takes a pass to start, then 7 passes for its 6 steps and the white fade.
                    double[] up = { 100, 115, 100, 115, 6, 1 }, down = { 115, 100, 115, 100, 6, 1 };
                    AddScaleSteps(_atVis, new[] { up, down, up, down }, 1);
                    AddSpriteFades(_atVis, 33, 255, 255, 255, new[] { new[] { 0, 0, 6, 0 }, new[] { 8, 6, 0, 0 }, new[] { 16, 0, 6, 0 }, new[] { 24, 6, 0, 0 } });
                    break;
                }
                case 14:
                    AddScaleSteps(_atVis, new[] { new double[]{100,150,100,50,8,1}, new double[]{150,50,50,150,8,1}, new double[]{50,100,150,100,8,1} }, 1);
                    break;
                case 15:
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = TeleportFx, Frames = 27 });
                    break;
                case 19:
                    // 3 hops of 3 parts, each a pass to start and 6 to run, with a spare pass between hops.
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = SplashFx, Frames = 3 * SplashHopPasses + 2 + 1 });
                    break;
                case 5:
                {
                    int shrinkF = Math.Max(0, a.Length > 4 ? a[4] : 10), growF = Math.Max(0, a.Length > 5 ? a[5] : 5);
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = StrengthFx, Frames = shrinkF + 1 + StrengthBlinks * StrengthBlinkPasses + growF + 3,
                        Keys = new double[] { a.Length > 2 ? a[2] : 70, a.Length > 3 ? a[3] : 120 }, UpF = shrinkF, DownF = growF });
                    break;
                }
                case 24:
                    _monFx.Add(new MonFx { Kind = 20, Frames = EarthquakeFirstShake + EarthquakeExtents.Length * EarthquakeShakePasses + 1 });
                    break;
                case 7:
                    // 9 sweeps of 4 copies, each a little quicker, then the copies fade out over 9 passes.
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = 9, Frames = DoubleTeamSweepPasses() + 9 + 1 });
                    break;
                case 8:
                    // One loop in 8 steps; 2 copies follow on their own 10-step loops from the 3rd and 5th pass, and the task
                    // ends when the last copy finishes.
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = QuickAttackFx, Frames = 15, Dx = _atVis == 0 ? 1 : -1 });
                    break;
                case 9:
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = DrillPeckFx, Frames = 58, Dx = _atVis == 0 ? 1 : -1 });
                    break;
                case 10:
                    if (a.Length >= 5)
                    {
                        int rn = Math.Max(1, a[2]), sync = Math.Max(1, a[3]);
                        double dir = (a[4] & AnimAttacker) != 0 ? -1 : 1;
                        int om = MonFromFlag(a, 4);
                        if (om >= 0) _monFx.Add(new MonFx { Mon = om, Kind = 22, Frames = sync * rn + 1, Cycles = rn, Dx = dir });
                    }
                    break;
                case 58:
                    AddScaleSteps(_dfVis, new[] { new double[] { 100, 20, 100, 20, 10 } }, 0);
                    break;
                case 31:
                    // Grow to 1.5 in 8 steps, hold 5 passes, shrink back in 8, then the task waits 17 passes.
                    AddScaleSteps(_atVis, new[] { new double[]{100,150,100,150,8}, new double[]{150,150,150,150,0,4}, new double[]{150,100,150,100,8} }, 19);
                    break;
                case 46:
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = 5, Sh = new Shake(4, 0, 1, 4), NumMax = 0 });
                    break;
                case 47:
                {
                    // A pass to start, shake 4 px for 16 passes, lunge in 4 steps, wait 9 passes, come back in 4.
                    double s = _atVis == 0 ? 1.0 : -1.0, x = 40 * s, y = -7 * s;
                    AddPosSteps(_atVis, new[] { new double[] { 0, 0, 0, 0, 0 }, new double[] { 0, 0, 0, 0, 16, 0, 4 },
                        new double[] { 0, x, 0, y, 4 }, new double[] { x, x, y, y, 0, 8 }, new double[] { x, 0, y, 0, 4 } }, 1);
                    break;
                }
                case 48:
                {
                    // A pass to start, pushed back in 4 steps, shaken 4 px for 16 passes, back in 4.
                    double s = _dfVis == 0 ? 1.0 : -1.0, x = -40 * s, y = 16 * s;
                    AddPosSteps(_dfVis, new[] { new double[] { 0, 0, 0, 0, 0 }, new double[] { 0, x, 0, y, 4 },
                        new double[] { x, x, y, y, 16, 0, 4 }, new double[] { x, 0, y, 0, 4 } }, 1);
                    break;
                }
                case 55:
                    // One pass of setup, a 16-step fade out, 120 passes of backdrop wobble, a 16-step fade back,
                    // then two passes to show the Pokemon and finish.
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = 27, Frames = CamouflagePasses });
                    break;
                case 26:
                {
                    // A see-through copy drifts 20 px back and down over 20 steps while fading out over 15.
                    int mon = (a.Length > 2 && a[2] == 0) ? _dfVis : _atVis;
                    _monFx.Add(new MonFx { Mon = mon, Kind = NightmareFx, Frames = 22, Dx = mon == 0 ? 1 : -1 });
                    break;
                }
                case 27:
                    if (a.Length > 2 && a[2] == 0)
                    {
                        // The user swings left and right 5 times, each a little less; each swing is its steps and a pass.
                        int runs = 1;
                        foreach (int[] sw in FlailSwings) runs += 2 * (sw[1] + 1);
                        _monFx.Add(new MonFx { Mon = _atVis, Kind = FlailFx, Frames = runs });
                    }
                    else if (a.Length >= 7)
                        _monFx.Add(new MonFx { Mon = _dfVis, Kind = 5, Delay = 1, EndPass = true,
                            Sh = new Shake(a[3], a[4], Math.Max(1, a[5]), Math.Max(1, a[6])), NumMax = 0 });
                    break;
                case 28:
                {
                    int pow = MovePower switch { 150 => 6, 110 => 5, 90 => 4, 70 => 3, 50 => 2, 30 => 1, _ => 0 };
                    _monFx.Add(new MonFx { Kind = 5, ToScene = true, Sh = new Shake(2 + pow, pow, 1, 10), NumMax = 0, Delay = 1, EndPass = true });
                    break;
                }
                case 11:
                    _monFx.Add(new MonFx { Mon = _dfVis, Kind = ConfusionFx, Frames = 14 });
                    break;
                case 16:
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = FlashFx, Frames = 43 });
                    break;
                case 17:
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = NightShadeFx, Frames = 52 });
                    break;
                case 18:
                    // The routine starts the fade to black itself, so it is 1 level further on each pass than the task.
                    _monFx.Add(new MonFx { Mon = _dfVis, Kind = NightShadeHitFx, Frames = 35 });
                    break;
                case 23:
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = FaintAttackFx, Frames = 84, Dx = _atVis == 0 ? 1 : -1 });
                    break;
                case 25:
                {
                    int mon = (a.Length > 2 && a[2] == 0) ? _atVis : _dfVis;
                    // The tilt alternates, starting away from the target.
                    _monFx.Add(new MonFx { Mon = mon, Kind = PlayfulHopsFx, Frames = 34, Dx = _atVis == 0 ? 1 : -1 });
                    break;
                }
                case 29:
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = ReturnFx, Frames = 33 });
                    break;
                case 30:
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = VitalThrowFx, Frames = 90, Dx = _atVis == 0 ? 1 : -1 });
                    break;
                case 32:
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = MementoFx, Frames = 51 });
                    break;
                case 37:
                    // 3 distortions, each a pass to set up and 17 to run.
                    _monFx.Add(new MonFx { Mon = _dfVis, Kind = 23, Frames = 3 * 18 + 1 });
                    break;
                case 12:
                    // The routine runs the task's first pass itself.
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = AcidArmorFx, Frames = 69, Started = true });
                    break;
                case 67:
                {
                    int mon = MonFromFlag(a, 2);
                        if (mon < 0) break;
                    int step = a.Length > 5 ? a[5] : 1, per = Math.Max(1, a.Length > 6 ? a[6] : 1);
                    if (step == 0) break;
                    // The sprite sinks into the ground or rises out of it by the step every interval, over the part of
                    // the 80 px frame above the species' own offset.
                    int height = Math.Clamp(PartialDrawHeight - SpriteYOffsets[mon], 1, PartialDrawHeight);
                    int steps = height / Math.Abs(step) + 1;
                    _monFx.Add(new MonFx { Mon = mon, Kind = 15, Frames = steps * per + 1, ExactSteps = true, UpF = per, DownF = step, Cycles = steps,
                        Keys = new double[] { height } });
                    break;
                }
                case 22:
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = 14, Frames = 71 });
                    break;
                case 20:
                    _monFx.Add(new MonFx { Mon = _dfVis, Kind = SpiteFx, Frames = 85 });
                    break;
                case 21:
                    // A white copy shows from the 11th pass to the 21st.
                    AddSpriteFades(_atVis, 22, 255, 255, 255, new[] { new[] { 10, 16, 16, 0 }, new[] { 20, 0, 0, 0 } });
                    break;
                case 59:
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = ShadowPunchFx, Frames = 22 });
                    break;
                case 38:
                {
                    int capBit = a.Length > 2 ? a[2] : 1, a1s = a.Length > 3 ? a[3] : 16, a1e = a.Length > 4 ? a[4] : 16, sync = Math.Max(1, a.Length > 7 ? a[7] : 8);
                    for (int b = 0; b < 2; b++) if ((capBit & (1 << b)) != 0)
                        _monFx.Add(new MonFx { Mon = b, Kind = 16, Frames = sync + 1, ExactSteps = true, Keys = new double[] { a1s, a1e } });
                    break;
                }
                case 41:
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = FakeOutCurtainFx, Frames = 29 });
                    break;
                case 43:
                    // The target squashes tall and thin in 4 steps and back in 3 while shaking 2 px.
                    AddScaleSteps(_dfVis, new[] { new double[] { 100, 70, 100, 300, 4 }, new double[] { 70, 100, 300, 100, 3 } }, 1);
                    AddPosSteps(_dfVis, new[] { new double[] { 0, 0, 0, 0, 8, 0, 2 } }, 1);
                    break;
                case 79:
                    // 39 rows of 2 px, 4 passes each, then a pass to stop the partial draw and one to end.
                    _monFx.Add(new MonFx { Mon = _dfVis, Kind = 16, Frames = 39 * 4 + 2, Keys = new double[] { 8, 8 } });
                    break;
                case 56:
                    if (_caps.TryGetValue(0, out DroppedCap t08cap))
                    { t08cap.ScaleX = t08cap.ScaleY = 1.2; t08cap.TintR = t08cap.TintG = t08cap.TintB = 255; t08cap.TintA = 0.4; }
                    // Its task only clears the window, on its first pass.
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = HoldOnly, Frames = 1 });
                    break;
                case 75:
                    if (a.Length > 2 && _caps.TryGetValue(a[2], out DroppedCap pvCap))
                    {
                        int para75 = a.Length > 6 ? a[6] : -1;
                        if (para75 == 2 || para75 == 3) { pvCap.Visible = false; break; }
                        if (a.Length > 5 && a[5] >= 0 && a[5] != 0xFF) pvCap.Priority = a[5];
                        if (a.Length > 7 && a[7] != 0)
                        {
                            bool tgt0 = a.Length > 8 && a[8] == 0;
                            pvCap.ClipOutX0 = tgt0 ? 0 : 128; pvCap.ClipOutY0 = tgt0 ? 160 : 86;
                            pvCap.ClipOutX1 = tgt0 ? 128 : 256; pvCap.ClipOutY1 = 192;
                            _monFx.Add(new MonFx { Cap = pvCap, Mon = 0, Kind = 26,
                                                   Frames = Math.Max(1, a.Length > 3 ? a[3] : 80) });
                        }
                    }
                    break;
                case 78:
                {
                    _caps[0] = new DroppedCap { SrcMon = _atVis };
                    _caps[1] = new DroppedCap { SrcMon = _dfVis };
                    // The copies are drawn by a task that counts its frames down (3 when given 0) and ends on the
                    // pass after. The routine runs the task's first pass itself.
                    int frames = a.Length > 2 && a[1] >= 1 && a[2] != 0 ? a[2] : 3;
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = HoldOnly, Frames = frames + 1, Started = true });
                    break;
                }
                case 70:
                    // The routine runs the task's first pass itself.
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = 17, Frames = 37, Started = true });
                    break;
                case 76:
                    _rasterLeft = Math.Max(1, a.Length > 2 ? a[2] : 60); RasterAmp = 32; RasterPhase = 0;
                    RasterLineAdd = Math.PI / 180.0; RasterRows = 192;
                    // Its task spends a pass starting the scroll, then runs for the given passes.
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = HoldOnly, Frames = Math.Max(1, a.Length > 2 ? a[2] : 0) + 1 });
                    break;
                case 71:
                {
                    int vec = _atVis == 0 ? 1 : -1;
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = 18, Frames = 48,
                        Dx = vec > 0 ? 255 + 80 : 0 - 80, Dy = vec > 0 ? 0 - 80 : 255 + 80 });
                    break;
                }
                case 63:
                {
                    int mode = a.Length > 2 ? a[2] : 0;
                    int start = a.Length > 5 ? a[5] : 0, end = a.Length > 6 ? a[6] : 16, col = a.Length > 7 ? a[7] : 0;
                    int capId = CapIdFromToolFlag(mode);
                    DroppedCap cap = (capId >= 0 && _caps.TryGetValue(capId, out DroppedCap dc)) ? dc : null;
                    // The palette fade blends once every max(step frames, 1) passes, by the step, and the task ends with
                    // the last blend.
                    int stepFrames = Math.Max(1, a.Length > 3 ? a[3] : 0), step = Math.Max(1, a.Length > 4 ? a[4] : 1);
                    int blends = (int)Math.Ceiling(Math.Abs(end - start) / (double)step);
                    _monFx.Add(new MonFx { Mon = capId >= 0 ? 0 : Math.Max(0, MonFromFlag(a, 2)), Cap = cap, Kind = 12, Frames = 1 + blends * stepFrames,
                        ExactSteps = true, UpF = stepFrames, DownF = step, Keys = new double[] { start, end }, R = R5(col), G = G5(col), B = B5(col) });
                    break;
                }
                case 69:
                {
                    int capId = a.Length > 2 ? a[2] : 0, add = a.Length > 3 ? a[3] : 1, hs = a.Length > 4 ? a[4] : 0;
                    double end = add < 0 ? 0 : 15;
                    int frames = Math.Max(1, (int)Math.Ceiling(Math.Abs(end - hs) / Math.Max(1, Math.Abs(add))));
                    DroppedCap cap = _caps.TryGetValue(capId, out DroppedCap dc) ? dc : null;
                    // The routine runs the task's first pass itself, which already takes the first step.
                    _monFx.Add(new MonFx { Mon = cap != null ? cap.SrcMon : (capId & 1), Cap = cap, Kind = 11, Frames = frames + 1, Started = true,
                        Keys = new double[] { hs + add, end, add } });
                    break;
                }
                case 39:
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = 5, Sh = new Shake(20, 0, 1, 10), NumMax = 0 });
                    _monFx.Add(new MonFx { Mon = _dfVis, Kind = 5, Sh = new Shake(2, 0, 1, 10), NumMax = 0 });
                    break;
                case FnRevolveBattler when a.Length >= 5:
                {
                    int mon = MonFromFlag(a, 2), rotaNum = Math.Max(1, a[3]), sync = Math.Max(1, a[4]);
                        if (mon < 0) break;
                    _monFx.Add(new MonFx { Mon = mon, Kind = 7, Frames = sync * rotaNum + 1, Keys = new double[] { 16, -4, sync, 8 } });
                    break;
                }
                case FnBlinkAttacker:
                    {
                        // Each toggle waits the interval and takes a pass, and so does the last check that shows it again.
                        int num = Math.Max(0, a.Length > 2 ? a[2] : 1) * 2, per = Math.Max(0, a.Length > 3 ? a[3] : 0) + 1;
                        _monFx.Add(new MonFx { Mon = _atVis, Kind = 2, UpF = per, Cycles = num, Frames = (num + 1) * per });
                    }
                    break;
                // The sprite's fade moves one step every (step frames + 1) passes, from 0 up to the strength and back,
                // holding in between; the task repeats that once per count, then spends a pass cleaning up.
                case FnFadeBattlerSprite when a.Length >= 6:
                {
                    int stepFrames = Math.Max(0, a[3]), count = Math.Max(1, a[4]), col = a[5];
                    int alpha = Math.Max(0, a.Length > 6 ? a[6] : 0), hold = Math.Max(0, a.Length > 7 ? a[7] : 0);
                    // The task never resets its hold, so only the first fade in and out holds.
                    int cycle = 2 * alpha * (stepFrames + 1) + 4;
                    // With no sprite there (a partner in a single battle) the routine starts no task at all.
                    int mon = MonFromFlag(a, 2);
                    if (mon < 0) break;
                    _monFx.Add(new MonFx { Mon = mon, Kind = BattlerFade, Frames = count * cycle + hold + 1,
                        UpF = stepFrames, WaitF = hold, Cycles = cycle, Keys = new[] { (double)alpha },
                        R = R5(col), G = G5(col), B = B5(col) });
                    break;
                }

                case FnRevolveEmitter:
                    AddTask(_particles.RevolveEmitter(a[2..]));
                    break;
                case FnMoveEmitterA2BLinear:
                case FnMoveEmitterA2BParabolic:
                    AddTask(_particles.MoveEmitterA2B(a[2..], fn == FnMoveEmitterA2BParabolic));
                    break;
                case FnMoveEmitterViewportTop:
                    AddTask(_particles.MoveEmitterViewportTop(a[2..]));
                    break;
            }
        }

        private const int TaskFx = 30;

        private void MoveMon(int[] a)
        {
            int cnt = a.Length >= 2 ? a[1] : 0;
            if (cnt < 2 || a.Length < 2 + cnt) return;
            int wait = Math.Max(1, a[2]);
            int ofsx = a[3];
            int ofsy = cnt >= 4 ? a[4] : 0;
            int type = a[1 + cnt];
            int mon = (type & AnimAttacker) != 0 ? _atVis : (type & AnimDefender) != 0 ? _dfVis : -1;
            if (mon < 0) return;
            double sign = mon == 0 ? 1.0 : -1.0;
            // Routines 57 and 52 run their task's first pass themselves, setting up and taking the first step; 51 spends
            // its first pass setting up. All three then spend a pass noticing the move is done and one ending.
            int fn = a[0];
            bool timed = fn == 57 || fn == 51 || fn == 52;
            _monFx.Add(new MonFx { Mon = mon, Frames = wait, Kind = 4, Dx = ofsx * sign, Dy = ofsy * sign,
                                   Delay = fn == 51 ? 1 : 0, TailRuns = timed ? 2 : 0, Started = fn != 51 });
        }

        private void AddGhost(int mon, double dx, double alpha, byte g)
            => _ghosts.Add(new MonGhost { Mon = mon, Dx = dx, Dy = 0, ScaleX = 1, ScaleY = 1, Alpha = alpha,
                                          TintR = g, TintG = g, TintB = g, TintA = 0.7 });

        // Scale phases as the game's tasks run them: { start x, end x, start y, end y, steps, passes at the start size }.
        // Tail is the passes after the last phase, which show the normal size.
        private void AddScaleSteps(int mon, double[][] phases, int tail)
        {
            if (mon < 0 || phases == null || phases.Length == 0) return;
            int total = tail; foreach (double[] p in phases) total += ScalePhaseRuns(p);
            _monFx.Add(new MonFx { Mon = mon, Kind = 8, Phases = phases, Frames = total, ExactSteps = true });
        }

        private static int ScalePhaseRuns(double[] p) => Math.Max(0, (int)p[4]) + 1 + (p.Length > 5 ? (int)p[5] : 0);

        // Offsets as the game's tasks move a sprite: { start x, end x, start y, end y, steps, passes at the start,
        // shake extent }. A shaking phase holds its place and shakes for its steps, then a pass notices the end.
        private void AddPosSteps(int mon, double[][] phases, int tail)
        {
            if (mon < 0 || phases == null || phases.Length == 0) return;
            int total = tail; foreach (double[] p in phases) total += ScalePhaseRuns(p);
            _monFx.Add(new MonFx { Mon = mon, Kind = PosSeq, Phases = phases, Frames = total });
        }

        // Sprite fades started at given passes of a task: { pass, from, to, delay }, a level every delay + 1 passes.
        private void AddSpriteFades(int mon, int frames, byte r, byte g, byte b, int[][] fades)
            => _monFx.Add(new MonFx { Mon = mon, Kind = SpriteFades, Frames = frames, R = r, G = g, B = b, Fades = fades });

        private void AddScaleSeq(int mon, double[][] phases, int repeat = 1)
        {
            if (mon < 0 || phases == null || phases.Length == 0) return;
            if (repeat < 1) repeat = 1;
            double[][] seq = new double[phases.Length * repeat][];
            for (int r = 0; r < repeat; r++) for (int i = 0; i < phases.Length; i++) seq[r * phases.Length + i] = phases[i];
            int total = 0; foreach (double[] p in seq) total += Math.Max(1, (int)p[4]);
            _monFx.Add(new MonFx { Mon = mon, Kind = 8, Phases = seq, Frames = total });
        }

        private static byte R5(int c) => (byte)((c & 0x1F) << 3);
        private static byte G5(int c) => (byte)(((c >> 5) & 0x1F) << 3);
        private static byte B5(int c) => (byte)(((c >> 10) & 0x1F) << 3);

        private List<int> TargetsFromFlags(int flag) => BattleAnimTargetFlags.Targets(flag, _atVis, _dfVis);

        private int MonFromFlag(int[] a, int idx)
        {
            int flag = idx < a.Length ? a[idx] : AnimDefender;
            List<int> t = TargetsFromFlags(flag);
            return t.Count > 0 ? t[0] : -1;
        }

        private void AddSpriteActor(int manager, int idOrCap, bool withCallback, int[] gp)
        {
            CellSequence[] seqs = CellSeqs;
            Func<IBattleAnimSystem, CellActor, Func<bool>> effect = withCallback ? SpriteEffectTable.Get(idOrCap) : null;
            if (effect != null)
            {
                CellActor sprite = new CellActor(seqs, 0) { FuncId = idOrCap, Gp = gp ?? Array.Empty<int>(), Manager = manager,
                    TaskDriven = true, X = _dfX, Y = _dfY, BaseX = _dfX, BaseY = _dfY, CapId = 0, Visible = seqs.Length > 0 };
                _spriteActors.Add(sprite);
                Func<bool> task = effect(this, sprite);
                if (task != null) AddTask(task);
                return;
            }
            if (withCallback)
            {
                // A function number past the game's table: the sprite just stands there, with no task.
                _spriteActors.Add(new CellActor(seqs, 0) { FuncId = idOrCap, Gp = gp ?? Array.Empty<int>(),
                    X = _dfX, Y = _dfY, BaseX = _dfX, BaseY = _dfY, CapId = 0, Visible = seqs.Length > 0 });
                return;
            }
            if (seqs.Length == 0) return;
            int seq = (idOrCap >= 0 && idOrCap < seqs.Length) ? idOrCap : 0;
            _spriteActors.Add(new CellActor(seqs, seq) { CapId = idOrCap, X = _dfX, Y = _dfY, BaseX = _dfX, BaseY = _dfY });
        }

        private double OffscreenX(int mon) => ((mon == 0 ? _atX : _dfX) < 128 ? OffScreenLeft : OffScreenRight);

        private void UpdateMonFx()
        {
            MonRot[0] = MonRot[1] = 0;
            for (int m = 0; m < 2; m++)
            {
                MonScaleX[m] = _monAttrScaleX[m] / 256.0; MonScaleY[m] = _monAttrScaleY[m] / 256.0;
                MonVisible[m] = !_monAttrHidden[m];
            }
            MonTintA[0] = MonTintA[1] = 0;
            MonShakeX[0] = MonShakeX[1] = 0; MonShakeY[0] = MonShakeY[1] = 0; ShakeX = ShakeY = 0;
            BgFlashAmount = 0;
            MonMosaic[0] = MonMosaic[1] = 0; MonClip[0] = MonClip[1] = 1; MonAlpha[0] = MonAlpha[1] = 1;
            MonWarpMon = -1;
            _ghosts.Clear();

            RunTasks();
            for (int m = 0; m < 2; m++)
            {
                MonScaleX[m] = _monAttrScaleX[m] / 256.0; MonScaleY[m] = _monAttrScaleY[m] / 256.0;
                MonVisible[m] = !_monAttrHidden[m];
            }

            for (int i = _monFx.Count - 1; i >= 0; i--)
            {
                MonFx fx = _monFx[i];
                if (!fx.Started) continue;
                if (fx.Delay > 0) { fx.Delay--; continue; }
                if (fx.Kind == 5)
                {
                    if (!fx.Sh.Calc())
                    {
                        if (fx.Rep < fx.NumMax) { fx.Rep++; fx.Sh = new Shake(fx.Sh.AmpX, fx.Sh.AmpY, fx.Sh.Sync, fx.Sh.Num0); }
                        else if (fx.EndPass && !fx.Ending) { fx.Ending = true; continue; }
                        else { _monFx.RemoveAt(i); continue; }
                    }
                    if (fx.ToScene) { ShakeX = fx.Sh.X; ShakeY = fx.Sh.Y; }
                    else { MonShakeX[fx.Mon] = fx.Sh.X; MonShakeY[fx.Mon] = fx.Sh.Y; }
                    continue;
                }
                if (fx.Kind == TaskFx) continue;
                if (fx.TailRuns > 0 && fx.Frame >= fx.Frames)
                {
                    if (++fx.Frame >= fx.Frames + fx.TailRuns) _monFx.RemoveAt(i);
                    continue;
                }
                double t = (double)fx.Frame / Math.Max(1, fx.Frames);
                switch (fx.Kind)
                {
                    case 0: MonRot[fx.Mon] = Math.Sin(t * Math.PI * 2 * 2) * 18.0; break;
                    case 1 when fx.ExactSteps:
                    {
                        int up = fx.UpF, down = fx.DownF, len = up + down + 4;
                        if (fx.Frame >= fx.Frames - 1) break;
                        int at = fx.Frame % len;
                        double from = fx.Keys[0], to = fx.Keys[1];
                        double v = at == 0 ? from
                                 : at <= up + 1 ? from + (to - from) * (up == 0 ? 1 : Math.Min(at, up) / (double)up)
                                 : at == up + 2 ? to
                                 : to + (from - to) * (down == 0 ? 1 : Math.Min(at - up - 2, down) / (double)down);
                        if (fx.Cap != null) { fx.Cap.ScaleX = v; fx.Cap.ScaleY = v; }
                        else { MonScaleX[fx.Mon] = v; MonScaleY[fx.Mon] = v; }
                        break;
                    }
                    case 1:
                        if (fx.Keys != null && fx.Keys.Length >= 4)
                        {
                            int per = Math.Max(1, fx.UpF + fx.WaitF + fx.DownF);
                            int fl = fx.Frame % per;
                            double sx = fx.Keys[0], ex = fx.Keys[1], sy = fx.Keys[2], ey = fx.Keys[3];
                            double cx, cy;
                            if (fl < fx.UpF) { double k = (double)fl / fx.UpF; cx = sx + (ex - sx) * k; cy = sy + (ey - sy) * k; }
                            else if (fl < fx.UpF + fx.WaitF) { cx = ex; cy = ey; }
                            else { double k = (double)(fl - fx.UpF - fx.WaitF) / fx.DownF; cx = ex + (sx - ex) * k; cy = ey + (sy - ey) * k; }
                            if (fx.Cap != null) { fx.Cap.ScaleX = cx; fx.Cap.ScaleY = cy; }
                            else { MonScaleX[fx.Mon] = cx; MonScaleY[fx.Mon] = cy; }
                        }
                        break;
                    case 2: MonVisible[fx.Mon] = Math.Min(fx.Cycles, (fx.Frame + 1) / Math.Max(1, fx.UpF)) % 2 == 0; break;
                    case 3: MonTintA[fx.Mon] = Math.Sin(t * Math.PI) * 0.85; TintR = fx.R; TintG = fx.G; TintB = fx.B; break;
                    case 6:
                    {
                        int fw = fx.UpF, w = fx.WaitF, cyc = Math.Max(1, 2 * fw + w), fl = fx.Frame % cyc;
                        double evyMax = fx.Keys != null && fx.Keys.Length > 0 ? fx.Keys[0] : 8;
                        double evy;
                        if (fw > 0 && fl < fw) evy = evyMax * (fl + 1) / fw;
                        else if (fl < fw + w) evy = evyMax;
                        else if (fw > 0) evy = evyMax * (1.0 - (double)(fl - fw - w + 1) / fw);
                        else evy = evyMax;
                        MonTintA[fx.Mon] = Math.Clamp(evy / 16.0, 0, 1); TintR = fx.R; TintG = fx.G; TintB = fx.B;
                        break;
                    }
                    case 4: MonDX[fx.Mon] += fx.Dx / fx.Frames; MonDY[fx.Mon] += fx.Dy / fx.Frames; break;
                    case 26:
                    {
                            DroppedCap cap = fx.Cap;
                        if (cap == null) break;
                        int f = fx.Frame;
                        if (f == 6 || f == 11 || f == 16 || f == 21) cap.Dy += 4;
                        else if (f == 23) cap.Dy += 8;
                        else if (f >= 37) cap.Dy += 4;
                        double capBaseY = cap.SrcMon == _atVis ? _atY : _dfY;
                        if (capBaseY + cap.Dy > 130 || fx.Frame >= fx.Frames - 1) cap.Visible = false;
                        break;
                    }
                    case 21:
                    {
                        double minA = (fx.Keys != null && fx.Keys.Length > 0) ? fx.Keys[0] : 0.1;
                        bool squash = fx.Keys != null && fx.Keys.Length > 1 && fx.Keys[1] > 0;
                        int outF = Math.Max(1, fx.Frames * 35 / 100), holdF = fx.Frames * 30 / 100;
                        int inF = Math.Max(1, fx.Frames - outF - holdF);
                        int f = fx.Frame; double alpha;
                        if (f < outF) alpha = 1.0 - (double)f / outF * (1 - minA);
                        else if (f < outF + holdF) alpha = minA;
                        else alpha = minA + (double)(f - outF - holdF) / inF * (1 - minA);
                        MonAlpha[fx.Mon] = Math.Clamp(alpha, 0, 1);
                        if (squash) MonScaleY[fx.Mon] = 0.7 + 0.3 * MonAlpha[fx.Mon];
                        break;
                    }
                    case 20:
                    {
                        // The BG darkens by 10 over the first passes; each shake then flashes the BG black or white for 2
                        // passes, which also undoes the darkening, and moves left, back, right, back 5 times.
                        int f = fx.Frame;
                        if (f < EarthquakeFirstShake)
                        {
                            BgFlashAmount = Math.Min(10, (f + 1) / 2 * 2) / 16.0;
                            BgFlashR = BgFlashG = BgFlashB = 0;
                            break;
                        }
                        int k = (f - EarthquakeFirstShake) / EarthquakeShakePasses, u = (f - EarthquakeFirstShake) % EarthquakeShakePasses;
                        if (k >= EarthquakeExtents.Length) break;
                        if (u < 2)
                        {
                            BgFlashAmount = 10.0 / 16.0;
                            BgFlashR = BgFlashG = BgFlashB = (byte)(k % 2 == 0 ? 0 : 255);
                        }
                        int e = EarthquakeExtents[k];
                        ShakeX = u % 4 == 0 && u < 20 ? e : u % 4 == 2 ? -e : 0;
                        break;
                    }
                    case 7:
                        // Each pass takes a step round the loop; the last pass notices the end and puts the sprite back.
                        if (fx.Keys != null && fx.Keys.Length >= 4 && fx.Frame < fx.Frames - 1)
                        {
                            double ang = (fx.Frame + 1) * 2.0 * Math.PI / Math.Max(1, fx.Keys[2]);
                            MonShakeX[fx.Mon] += fx.Keys[0] * Math.Sin(ang);
                            MonShakeY[fx.Mon] += fx.Keys[3] + fx.Keys[1] * Math.Cos(ang);
                        }
                        break;
                    case 9:
                    {
                        // Each sweep sends the near copies 32 px out (or back) in 7 steps and the far ones in 10, with one
                        // step fewer every other sweep. A sweep ends a pass after the slower pair arrives.
                        int f = fx.Frame, sweeps = DoubleTeamSweepPasses();
                        if (f >= fx.Frames - 1) break;
                        double alpha = 8 / 16.0;
                        double near = 32, far = 32;
                        if (f < sweeps)
                        {
                            int sweep = 0, at = f;
                            for (; sweep < 9; sweep++) { int runs = 10 - (sweep + 1) / 2 + 1; if (at < runs) break; at -= runs; }
                            int fr = (sweep + 1) / 2, nearSteps = 7 - fr, farSteps = 10 - fr;
                            double kn = Math.Min(at + 1, nearSteps) / (double)nearSteps, kf = Math.Min(at + 1, farSteps) / (double)farSteps;
                            bool outward = sweep % 2 == 0;
                            near = 32 * (outward ? kn : 1 - kn); far = 32 * (outward ? kf : 1 - kf);
                        }
                        else alpha = Math.Max(0, 8 - (f - sweeps + 1)) / 16.0;
                        AddGhost(fx.Mon, near, alpha, 128); AddGhost(fx.Mon, -near, alpha, 128);
                        AddGhost(fx.Mon, far, alpha, 196); AddGhost(fx.Mon, -far, alpha, 196);
                        break;
                    }
                    case 10:
                        if (fx.Keys != null && fx.Keys.Length > 0)
                            MonRot[fx.Mon] = fx.Keys.Length > 1 && fx.Keys[1] > 0 ? fx.Keys[0] * Math.Sin(t * Math.PI * fx.Keys[1]) : fx.Keys[0] * t;
                        break;
                    case 11:
                        if (fx.Keys != null && fx.Keys.Length >= 3)
                        {
                            double mv = Math.Clamp(fx.Keys[0] + fx.Keys[2] * fx.Frame, Math.Min(fx.Keys[0], fx.Keys[1]), Math.Max(fx.Keys[0], fx.Keys[1]));
                            if (fx.Cap != null) fx.Cap.Mosaic = mv; else MonMosaic[fx.Mon] = mv;
                        }
                        break;
                    case 12:
                        if (fx.Keys != null && fx.Keys.Length >= 2)
                        {
                            double span = Math.Abs(fx.Keys[1] - fx.Keys[0]);
                            double evy = fx.ExactSteps
                                ? fx.Keys[0] + Math.Sign(fx.Keys[1] - fx.Keys[0]) * Math.Min(span, fx.Frame / Math.Max(1, fx.UpF) * fx.DownF)
                                : fx.Keys[0] + (fx.Keys[1] - fx.Keys[0]) * t;
                            double ta = Math.Clamp(evy / 16.0, 0, 1);
                            if (fx.Cap != null) { fx.Cap.TintA = ta; fx.Cap.TintR = fx.R; fx.Cap.TintG = fx.G; fx.Cap.TintB = fx.B; }
                            else { MonTintA[fx.Mon] = ta; TintR = fx.R; TintG = fx.G; TintB = fx.B; }
                        }
                        break;
                    case 15 when fx.ExactSteps:
                    {
                        if (fx.Frame >= fx.Frames - 1) break;
                        int k = Math.Min(fx.Cycles, (fx.Frame + 1) / fx.UpF), step = fx.DownF;
                        double height = fx.Keys[0];
                        double drawn = step > 0 ? Math.Max(0, height - k * step) : Math.Min(height, -k * step);
                        MonClip[fx.Mon] = drawn / PartialDrawHeight;
                        MonShakeY[fx.Mon] += step > 0 ? k * step : height + k * step;
                        break;
                    }
                    case 15:
                        MonClip[fx.Mon] = (fx.Dx >= 0 ? 1 : -1) * Math.Clamp(t, 0.0, 1.0);
                        break;
                    case 16:
                        if (fx.Keys != null && fx.Keys.Length >= 2)
                        {
                            // The alpha task steps once a pass from the routine's own pass and ends a pass after its last step.
                            double k = fx.ExactSteps ? Math.Min(fx.Frame + 1, Math.Max(1, fx.Frames - 1)) / (double)Math.Max(1, fx.Frames - 1) : t;
                            MonAlpha[fx.Mon] = Math.Clamp((fx.Keys[0] + (fx.Keys[1] - fx.Keys[0]) * k) / 16.0, 0, 1);
                        }
                        break;
                    case SpriteFades:
                    {
                        int[] cur = null;
                        foreach (int[] fd in fx.Fades) if (fd[0] <= fx.Frame) cur = fd;
                        if (cur == null) break;
                        int moved = Math.Min(Math.Abs(cur[2] - cur[1]), (fx.Frame - cur[0]) / (cur[3] + 1));
                        int evy = cur[1] + Math.Sign(cur[2] - cur[1]) * moved;
                        MonTintA[fx.Mon] = evy / 16.0; TintR = fx.R; TintG = fx.G; TintB = fx.B;
                        break;
                    }
                    case ShadowPunchFx:
                    {
                        // The user and 2 grey copies slide 48 px right at different speeds and back; the game doesn't
                        // mirror it for the enemy side.
                        int r = fx.Frame + 1;
                        if (r >= fx.Frames) break;
                        double main, c1, c2;
                        if (r <= 10) { int j = r - 1; main = 48 * Math.Min(j, 3) / 3.0; c1 = 48 * Math.Min(j, 6) / 6.0; c2 = 48 * Math.Min(j, 8) / 8.0; }
                        else { int j = r - 11; main = 48 * (1 - Math.Min(j, 3) / 3.0); c1 = 48 * (1 - Math.Min(j, 6) / 6.0); c2 = 48 * (1 - Math.Min(j, 9) / 9.0); }
                        MonShakeX[fx.Mon] += main;
                        _ghosts.Add(new MonGhost { Mon = fx.Mon, Dx = c1, Dy = 0, ScaleX = 1, ScaleY = 1, Alpha = 0.5, TintR = 196, TintG = 196, TintB = 196, TintA = 0.5 });
                        _ghosts.Add(new MonGhost { Mon = fx.Mon, Dx = c2, Dy = 0, ScaleX = 1, ScaleY = 1, Alpha = 0.5, TintR = 196, TintG = 196, TintB = 196, TintA = 0.5 });
                        break;
                    }
                    case ShakeScaleFx:
                    {
                        int f = fx.Frame, f1 = fx.UpF, hold = fx.WaitF, f2 = fx.DownF;
                        double s1 = fx.Keys[0] / 100.0, s2 = fx.Keys[1] / 100.0, sc;
                        int growStart = f1 + 1 + hold + 1;
                        if (f < f1) { sc = 1 + (s1 - 1) * (f + 1) / f1; MonShakeX[fx.Mon] += (f % 4) < 2 ? 2 : 0; }
                        else if (f < growStart) sc = s1;
                        else if (f < growStart + f2) sc = s1 + (s2 - s1) * (f - growStart + 1) / f2;
                        else if (f == growStart + f2) sc = s2;
                        else sc = 1;
                        MonScaleX[fx.Mon] = MonScaleY[fx.Mon] = sc;
                        MonShakeY[fx.Mon] += (1 - sc) * 24;
                        break;
                    }
                    case RotateFx:
                    {
                        int f = fx.Frame, n = fx.UpF;
                        if (f >= fx.Frames - 1 || n == 0) break;
                        double from = fx.Keys[0], to = fx.Keys[1];
                        double k = f <= n ? Math.Min(f + 1, n) / (double)n : 1 - Math.Min(f - n, n) / (double)n;
                        // Angles wrap, so 360 to 375 is a 15 degree turn.
                        double deg = (from + (to - from) * k) % 360;
                        MonRot[fx.Mon] = (deg > 180 ? deg - 360 : deg) * fx.Dx;
                        break;
                    }
                    case NightmareFx:
                    {
                        int f = fx.Frame;
                        double k = Math.Min(f + 1, 20) / 20.0, alpha = 1 - Math.Min(f + 1, 15) / 15.0;
                        if (alpha > 0)
                            _ghosts.Add(new MonGhost { Mon = fx.Mon, Dx = -20 * fx.Dx * k, Dy = 20 * fx.Dx * k, ScaleX = 1, ScaleY = 1, Alpha = alpha });
                        break;
                    }
                    case PosSeq:
                    {
                        int acc = 0, pi = 0;
                        for (; pi < fx.Phases.Length; pi++) { int runs = ScalePhaseRuns(fx.Phases[pi]); if (fx.Frame < acc + runs) break; acc += runs; }
                        if (pi >= fx.Phases.Length) break;
                        double[] ph = fx.Phases[pi]; int steps = Math.Max(0, (int)ph[4]), lead = ph.Length > 5 ? (int)ph[5] : 0;
                        int done = Math.Min(fx.Frame - acc - lead + 1, steps);
                        double shake = ph.Length > 6 ? ph[6] : 0;
                        double k = shake > 0 || done <= 0 ? 0 : steps == 0 ? 1 : done / (double)steps;
                        MonShakeX[fx.Mon] += ph[0] + (ph[1] - ph[0]) * k + (shake > 0 && done > 0 && fx.Frame - acc - lead + 1 <= steps ? ShakeStep(done, shake) : 0);
                        MonShakeY[fx.Mon] += ph[2] + (ph[3] - ph[2]) * k;
                        break;
                    }
                    case FakeOutCurtainFx:
                    {
                        // The BG turns white 2 levels a pass from the 9th pass while the curtain closes, then fades back.
                        int r = fx.Frame + 1;
                        int evy = r < 9 ? 0 : r < 19 ? Math.Min(16, 2 * (r - 9)) : Math.Max(0, 16 - 2 * (r - 19));
                        BgFlashAmount = evy / 16.0; BgFlashR = BgFlashG = BgFlashB = 255;
                        break;
                    }
                    case MementoFx:
                    {
                        // Dark see-through copies: the user's stretches up and thins out, then the target's drops 64 px and
                        // unsquashes. The copies fade in over 8 passes first and out over 8 at the end.
                        int f = fx.Frame;
                        double alpha = 0.5 * (f < 41 ? Math.Min(f, 8) / 8.0 : 1 - Math.Min(f - 40, 8) / 8.0);
                        if (f >= 9 && f <= 20)
                        {
                            double sx = f <= 16 ? 1 : 1 - 0.9 * Math.Min(f - 16, 4) / 4.0, sy = f <= 16 ? 1 + (f - 9) / 8.0 : 2;
                            _ghosts.Add(new MonGhost { Mon = fx.Mon, Dx = 0, Dy = (1 - sy) * 24, ScaleX = sx, ScaleY = sy, Alpha = alpha,
                                                       TintA = 0.5, TintR = 0, TintG = 0, TintB = 0 });
                        }
                        if (f >= 24)
                        {
                            int target = fx.Mon == _atVis ? _dfVis : _atVis;
                            double dy = f <= 31 ? -64 * (1 - (f - 23) / 8.0) : 0;
                            int sc = Math.Clamp(f - 31, 0, 8);
                            _ghosts.Add(new MonGhost { Mon = target, Dx = 0, Dy = dy, ScaleX = 0.2 + 0.8 * sc / 8.0, ScaleY = 2 - sc / 8.0,
                                                       Alpha = alpha, TintA = 0.5, TintR = 0, TintG = 0, TintB = 0 });
                        }
                        break;
                    }
                    case ReturnFx:
                    {
                        // Down 32 px and back up twice, each way a pass to start and 6 steps.
                        int f = fx.Frame;
                        if (f >= 32) break;
                        int m = f / 8, j = f % 8;
                        double k = Math.Min(j, 6) / 6.0;
                        MonShakeY[fx.Mon] += m % 2 == 0 ? 32 * k : 32 * (1 - k);
                        break;
                    }
                    case VitalThrowFx:
                    {
                        // A 64-step loop, then the user and the target each lunge 32 px in 2 steps and come back in 8.
                        int f = fx.Frame;
                        if (f >= 1 && f <= 64)
                        {
                            (double lx, double ly) = OvalLoop(f / 64.0, fx.Dx);
                            MonShakeX[fx.Mon] += lx; MonShakeY[fx.Mon] += ly;
                        }
                        double at = f == 65 ? 16 : f >= 66 && f <= 70 ? 32 : f >= 71 && f <= 78 ? 32 * (1 - (f - 70) / 8.0) : 0;
                        double df = f == 68 ? 16 : f >= 69 && f <= 79 ? 32 : f >= 80 && f <= 87 ? 32 * (1 - (f - 79) / 8.0) : 0;
                        MonShakeX[fx.Mon] += at * fx.Dx;
                        MonShakeX[fx.Mon == _atVis ? _dfVis : _atVis] += df * fx.Dx;
                        break;
                    }
                    case FaintAttackFx:
                    {
                        // The user hides while a copy loops twice and fades out over 32 passes, then fades back in.
                        int f = fx.Frame;
                        if (f >= fx.Frames - 1) break;
                        MonVisible[fx.Mon] = false;
                        double alpha = f < 50 ? 1 - Math.Min(f + 1, 32) / 32.0 : Math.Min(f - 49, 32) / 32.0;
                        (double gx, double gy) = OvalLoop(Math.Min(f + 1, 32) / 16.0, fx.Dx);
                        if (alpha > 0) _ghosts.Add(new MonGhost { Mon = fx.Mon, Dx = gx, Dy = gy, ScaleX = 1, ScaleY = 1, Alpha = alpha });
                        break;
                    }
                    case PlayfulHopsFx:
                    {
                        // 4 hops of 8 passes: tilt 15 degrees in 3 steps while dipping 2 px, then back.
                        int r = fx.Frame + 1;
                        int hop = r < 9 ? 1 : 2 + (r - 9) / 8, call = r < 9 ? r : 1 + (r - 9) % 8;
                        if (hop > 4) break;
                        double[] tilt = { 5, 10, 15, 15, 10, 5, 0, 0 }, dip = { 0, 1, 2, 2, 1, 0, 0, 0 };
                        double sign = (hop % 2 == 1) == (fx.Dx > 0) ? 1 : -1;
                        MonRot[fx.Mon] = tilt[call - 1] * sign;
                        MonShakeY[fx.Mon] += dip[call - 1];
                        break;
                    }
                    case SpiteFx:
                    {
                        // A wobbling copy fades in to half over 24 passes, stays 33, and fades out over 24.
                        int f = fx.Frame;
                        if (f >= fx.Frames - 1) break;
                        double k = f < 58 ? Math.Min(f + 1, 24) / 24.0 : 1 - Math.Min(f - 57, 24) / 24.0;
                        // The target is hidden behind its copy, so the copy is drawn in its place.
                        MonWarpMon = fx.Mon; MonWarpAmp = 5; MonWarpBaseDeg = f * 5; MonWarpAddPerRow = 5; MonWarpWidthA = 0; MonWarpShimmer = 0;
                        MonAlpha[fx.Mon] = 0.5 * k;
                        break;
                    }
                    case AcidArmorFx:
                    {
                        // Melts over 25 passes, fades out over 17, stays gone 9, then fades back in over 17 unmelted.
                        int r = fx.Frame;
                        double alpha;
                        if (r < 25)
                        {
                            MonWarpMon = fx.Mon; MonWarpAmp = 8.0 * Math.Min(r + 1, 24) / 24; MonWarpBaseDeg = r * 5;
                            MonWarpAddPerRow = 5; MonWarpWidthA = 0; MonWarpShimmer = 0;
                            alpha = 1;
                        }
                        else if (r < 42)
                        {
                            MonWarpMon = fx.Mon; MonWarpAmp = 8; MonWarpBaseDeg = r * 5; MonWarpAddPerRow = 5; MonWarpWidthA = 0; MonWarpShimmer = 0;
                            alpha = 1 - Math.Min(r - 24, 16) / 16.0;
                        }
                        else if (r < 52) alpha = 0;
                        else alpha = Math.Min(r - 51, 16) / 16.0;
                        MonAlpha[fx.Mon] = r >= fx.Frames ? 1 : alpha;
                        break;
                    }
                    case NightShadeFx:
                    {
                        // A copy at 1.4 times the size fades in over 8 passes, waits 33, then shrinks to normal over 8 and goes.
                        int f = fx.Frame;
                        if (f >= 50) break;
                        double alpha = Math.Min(f + 1, 8) / 8.0, sc = f < 42 ? 1.4 : 1.4 - 0.4 * Math.Min(f - 41, 8) / 8.0;
                        _ghosts.Add(new MonGhost { Mon = fx.Mon, Dx = 0, Dy = (1 - sc) * 24, ScaleX = sc, ScaleY = sc, Alpha = alpha });
                        break;
                    }
                    case NightShadeHitFx:
                    {
                        int f = fx.Frame;
                        if (f >= fx.Frames - 1) break;
                        MonShakeX[fx.Mon] += f < 16 ? ShakeStep(f + 1, 4) : 0;
                        int evy = f < 16 ? f + 1 : Math.Max(0, 32 - f);
                        MonTintA[fx.Mon] = Math.Min(16, evy) / 16.0; TintR = TintG = TintB = 0;
                        break;
                    }
                    case ConfusionFx:
                    {
                        // The target shakes while a see-through copy stretches in 7 steps and back in 4, wobbling in width.
                        int f = fx.Frame;
                        if (f >= fx.Frames - 1) break;
                        MonShakeX[fx.Mon] += ShakeStep(f + 1, 2);
                        double k = f < 8 ? Math.Min(f + 1, 7) / 7.0 : 1 - Math.Min(f - 7, 4) / 4.0;
                        _ghosts.Add(new MonGhost { Mon = fx.Mon, Dx = ShakeStep(f + 1, 2), Dy = 0, ScaleX = 1 + 0.2 * k + (f % 2 == 0 ? 0.1 : -0.1),
                                                   ScaleY = 1 + 0.5 * k, Alpha = 0.5 });
                        break;
                    }
                    case DrillPeckFx:
                    {
                        // Back 32 px in 3 steps, tilt 20 degrees in 8, wait 3 passes, forward in 2, hold 33, tilt back in 4.
                        int f = fx.Frame;
                        double x = f < 4 ? -32 * Math.Min(f + 1, 3) / 3.0
                                 : f < 16 ? -32
                                 : f < 19 ? -32 * (1 - Math.Min(f - 15, 2) / 2.0) : 0;
                        double rot = f < 4 ? 0
                                   : f < 13 ? 20 * Math.Min(f - 3, 8) / 8.0
                                   : f < 52 ? 20
                                   : f < 57 ? 20 * (1 - Math.Min(f - 51, 4) / 4.0) : 0;
                        MonShakeX[fx.Mon] += x * fx.Dx;
                        MonRot[fx.Mon] = rot * fx.Dx;
                        break;
                    }
                    case QuickAttackFx:
                    {
                        (double x, double y) now = OvalLoop(Math.Min(fx.Frame + 1, 8) / 8.0, fx.Dx);
                        (double x, double y) prev = fx.Frame == 0 ? (0, 0) : OvalLoop(Math.Min(fx.Frame, 8) / 8.0, fx.Dx);
                        MonDX[fx.Mon] += now.x - prev.x;
                        MonDY[fx.Mon] += now.y - prev.y;
                        for (int gi = 0; gi < 2; gi++)
                        {
                            int step = fx.Frame + 1 - 2 * (gi + 1);
                            if (step < 1 || step > 10) continue;
                            (double gx, double gy) = OvalLoop(step / 10.0, fx.Dx);
                            _ghosts.Add(new MonGhost { Mon = fx.Mon, Dx = gx, Dy = gy, ScaleX = 1, ScaleY = 1, Alpha = 0.45 - 0.15 * gi });
                        }
                        break;
                    }
                    case FlailFx:
                        MonRot[fx.Mon] = FlailAngle(fx.Frame);
                        break;
                    case FlashFx:
                    {
                        // The BG fades to white 4 levels a pass while the user fades to black 1 a pass; 6 passes after both
                        // are done they fade back the same way.
                        int f = fx.Frame, back = 24;
                        int bg = f < back ? Math.Min(16, 4 * f) : Math.Max(0, 16 - 4 * (f - back));
                        int mon = f < back ? Math.Min(16, f) : Math.Max(0, 16 - (f - back));
                        BgFlashAmount = bg / 16.0; BgFlashR = BgFlashG = BgFlashB = 255;
                        MonTintA[fx.Mon] = mon / 16.0; TintR = TintG = TintB = 0;
                        break;
                    }
                    case SplashFx:
                    {
                        int f = fx.Frame, hop = Math.Min(2, f / (SplashHopPasses + 1)), at = f - hop * (SplashHopPasses + 1);
                        if (at >= SplashHopPasses) break;
                        int part = at / 7, run = at % 7;
                        double[] p = SplashParts[part];
                        double k = Math.Min(run, 5) / 5.0;
                        double scy = p[2] + (p[3] - p[2]) * k;
                        MonScaleX[fx.Mon] = p[0] + (p[1] - p[0]) * k;
                        MonScaleY[fx.Mon] = scy;
                        MonShakeY[fx.Mon] += p[4] + (p[5] - p[4]) * k + (1 - scy) * 24;
                        break;
                    }
                    case TeleportFx:
                    {
                        // A pass to start each part: squish tall over 10 steps, rise to the top of the screen over 5, squash
                        // away upwards over 5, then the sprite is hidden.
                        int f = fx.Frame;
                        double baseY = fx.Mon == _atVis ? _atY : _dfY;
                        const double squishY = 1.8;
                        double sx, sy, y;
                        if (f >= fx.Frames - 1) { _monVanish[fx.Mon] = true; break; }
                        if (f <= 12)
                        {
                            double k = Math.Min(f, 10) / 10.0;
                            sx = 1 - 0.9 * k; sy = 1 + 0.8 * k; y = (1 - sy) * 24;
                        }
                        else if (f <= 19)
                        {
                            double k = Math.Min(f - 12, 5) / 5.0, y0 = baseY + (1 - squishY) * 24;
                            sx = 0.1; sy = squishY; y = y0 * (1 - k) - baseY;
                        }
                        else
                        {
                            double k = Math.Min(f - 19, 5) / 5.0;
                            // Squashed from the top edge, with the sprite's centre now at y 0.
                            sx = 0.1; sy = squishY * (1 - k); y = 24 * (sy - squishY) - baseY;
                        }
                        MonScaleX[fx.Mon] = sx; MonScaleY[fx.Mon] = sy; MonShakeY[fx.Mon] += y;
                        break;
                    }
                    case StrengthFx:
                    {
                        // Shrinks while nudging right by 2 every other pair of passes, blinks red, grows, then snaps back.
                        int f = fx.Frame, shrinkF = fx.UpF, growF = fx.DownF;
                        double shrink = fx.Keys[0] / 100.0, grow = fx.Keys[1] / 100.0, sc;
                        int blinkStart = shrinkF + 1, growStart = blinkStart + StrengthBlinks * StrengthBlinkPasses;
                        if (f < shrinkF)
                        {
                            sc = 1 + (shrink - 1) * (f + 1) / shrinkF;
                            MonShakeX[fx.Mon] += (f % 4) < 2 ? 2 : 0;
                        }
                        else if (f < growStart)
                        {
                            sc = shrink;
                            if (f >= blinkStart)
                            {
                                int j = (f - blinkStart) % StrengthBlinkPasses;
                                int evy = j <= 10 ? j : Math.Max(0, 21 - j);
                                MonTintA[fx.Mon] = evy / 16.0; TintR = 255; TintG = 0; TintB = 0;
                            }
                        }
                        else if (f < growStart + growF) sc = shrink + (grow - shrink) * (f - growStart + 1) / growF;
                        else if (f == growStart + growF) sc = grow;
                        else sc = 1;
                        MonScaleX[fx.Mon] = MonScaleY[fx.Mon] = sc;
                        MonShakeY[fx.Mon] += (1 - sc) * 24;
                        break;
                    }
                    case ScaleBattler:
                    {
                        if (fx.Frame >= fx.Frames - 1) break;
                        double sx = fx.Keys[0], ex = fx.Keys[1], sy = fx.Keys[2], ey = fx.Keys[3];
                        int s = fx.UpF, r = fx.DownF, first = fx.Cycles + fx.WaitF;
                        bool firstCycle = fx.Frame < first;
                        int hold = firstCycle ? fx.WaitF : 0;
                        int at = firstCycle ? fx.Frame : (fx.Frame - first) % Math.Max(1, fx.Cycles);
                        int back = Math.Max(1, s) + hold + 1;
                        // Each way's first step adds the x step to both axes, so y stays off by the difference.
                        (double cx, double cy) = at < back
                            ? ScaleStep(sx, ex, sy, ey, s, Math.Min(at + 1, s))
                            : ScaleStep(ex, sx, ey, sy, r, Math.Min(at - back + 1, r));
                        MonScaleX[fx.Mon] = cx; MonScaleY[fx.Mon] = cy;
                        break;
                    }
                    case BattlerFade:
                    {
                        int alpha = (int)fx.Keys[0], per = fx.UpF + 1, first = fx.Cycles + fx.WaitF;
                        bool firstCycle = fx.Frame < first;
                        int at = firstCycle ? fx.Frame : (fx.Frame - first) % Math.Max(1, fx.Cycles);
                        int up = alpha * per, downStart = up + 2 + (firstCycle ? fx.WaitF : 0);
                        int evy = at <= up ? at / per
                                : at < downStart ? alpha
                                : Math.Max(0, alpha - (at - downStart) / per);
                        if (fx.Frame >= fx.Frames - 1) evy = 0;
                        MonTintA[fx.Mon] = Math.Clamp(evy / 16.0, 0, 1); TintR = fx.R; TintG = fx.G; TintB = fx.B;
                        break;
                    }
                    case 27:
                    {
                        // Passes counted from the first run: fades on 0-15, wobble from 16 for 120, fade back on 137-152.
                        int f = fx.Frame;
                        double ev = f < CamouflageFade ? 16 - 14.0 * (f + 1) / CamouflageFade
                                  : f < CamouflageFadeBack ? 2
                                  : f < CamouflageFadeBack + CamouflageFade ? 2 + 14.0 * (f - CamouflageFadeBack + 1) / CamouflageFade
                                  : 16;
                        MonAlpha[fx.Mon] = ev / 16.0;
                        if (f == CamouflageFade)
                        {
                            _rasterLeft = CamouflageScroll; RasterAmp = 12; RasterPhase = 0;
                            RasterLineAdd = 2 * Math.PI / 180.0; RasterRows = 160;
                        }
                        break;
                    }
                    case 18:
                    {
                        double home = fx.Mon == 0 ? _atX : _dfX;
                        double Where(int f)
                        {
                            if (f < 0) return 0;
                            int sg = Math.Min(2, f / 15); double kk = (f % 15) / 15.0;
                            double a2 = sg == 0 ? home : sg == 1 ? fx.Dx : fx.Dy;
                            double b2 = sg == 0 ? fx.Dx : sg == 1 ? fx.Dy : home;
                            return (a2 + (b2 - a2) * kk) - home;
                        }
                        double at = fx.Frame >= fx.Frames - 1 ? 0.0 : Where(fx.Frame);
                        MonDX[fx.Mon] += at - Where(fx.Frame - 1);
                        break;
                    }
                    case 17:
                    {
                        // A copy of the user fades in over the target in 15 passes and squashes away in 5, both flashing
                        // white; the target's flash fades back from the 22nd pass.
                        int p = fx.Frame + 1;
                        if (p >= fx.Frames) break;
                        int white = Math.Clamp(p - 2, 0, 15);
                        if (p < 20)
                        {
                            double sq = p <= 15 ? 0 : 0.2 * (p - 15);
                            _ghosts.Add(new MonGhost { Mon = _atVis, Dx = (_dfX - 32) - _atX, Dy = _dfY - _atY,
                                ScaleX = 1 + sq, ScaleY = 1 - sq, Alpha = Math.Min(p, 15) / 15.0, TintR = 255, TintG = 255, TintB = 255, TintA = white / 16.0 });
                        }
                        int target = p < 22 ? white : Math.Max(0, 15 - (p - 22));
                        MonTintA[_dfVis] = target / 16.0; TintR = TintG = TintB = 255;
                        break;
                    }
                    case 13:
                    {
                        int up = Math.Max(1, fx.UpF), hold = Math.Max(0, fx.WaitF), down = Math.Max(1, fx.DownF);
                        double At(int f)
                        {
                            if (f < 0) return 0;
                            if (f < up) return (double)f / up;
                            if (f < up + hold) return 1.0;
                            return Math.Max(0, 1 - (double)(f - up - hold) / down);
                        }
                        double now = fx.Frame >= fx.Frames - 1 ? 0.0 : At(fx.Frame);
                        double step = now - At(fx.Frame - 1);
                        MonDX[fx.Mon] += fx.Dx * step; MonDY[fx.Mon] += fx.Dy * step;
                        break;
                    }
                    case 22:
                    {
                        // Frames is the loop's steps plus the pass that notices the end; each pass takes one step.
                        double turns = fx.Cycles > 0 ? fx.Cycles : 1, dir = fx.Dx < 0 ? -1 : 1;
                        int steps = Math.Max(1, fx.Frames - 1);
                        (double x, double y) Orbit(int f)
                        {
                            if (f < 0) return (0, 0);
                            return OvalLoop(turns * Math.Min(f + 1, steps) / steps, dir);
                        }
                            (double, double) nowP = fx.Frame >= fx.Frames - 1 ? (0.0, 0.0) : Orbit(fx.Frame);
                            (double x, double y) prevP = Orbit(fx.Frame - 1);
                        MonDX[fx.Mon] += nowP.Item1 - prevP.Item1;
                        MonDY[fx.Mon] += nowP.Item2 - prevP.Item2;
                        double ang = 2 * Math.PI * turns * ((double)fx.Frame / Math.Max(1, fx.Frames));
                        for (int gi = 0; gi < fx.NumMax; gi++)
                        {
                            int pf = fx.Frame - 2 * (gi + 1);
                            if (pf < 0) continue;
                            double pa = 2 * Math.PI * turns * (pf / (double)Math.Max(1, fx.Frames));
                            _ghosts.Add(new MonGhost { Mon = fx.Mon, Dx = Math.Sin(pa) * 32 * dir, Dy = 8 * (1 - Math.Cos(pa)),
                                ScaleX = 1, ScaleY = 1, Alpha = 0.45 - 0.15 * gi });
                        }
                        break;
                    }
                    case 23:
                    {
                        if (fx.Frame >= fx.Frames - 1) break;
                        int phase = Math.Min(2, fx.Frame / 18);
                        double[] rw = { 16, -16, 20 };
                        double[] wa = { 5, -5, 10 };
                        MonWarpMon = fx.Mon;
                        MonWarpAmp = rw[phase]; MonWarpBaseDeg = 180; MonWarpAddPerRow = 180.0 / 80.0;
                        MonWarpWidthA = wa[phase];
                        MonWarpShimmer = (fx.Frame & 1) == 0 ? 1 : -1;
                        break;
                    }
                    case 24:
                        MonWarpMon = fx.Mon;
                        MonWarpAmp = 8; MonWarpBaseDeg = fx.Frame * 4.5; MonWarpAddPerRow = 5; MonWarpWidthA = 0; MonWarpShimmer = 0;
                        break;
                    case 14:
                    {
                        // 3 copies shrink to 5% one after another, 3 times over, then all grow back together.
                        int f = fx.Frame;
                        if (f >= fx.Frames - 1) break;
                        int c = f == 0 ? 0 : Math.Min(2, (f - 1) / 21), r = f - 21 * c;
                        for (int gi = 0; gi < 3; gi++)
                        {
                            int d = MinimizeDelays[gi];
                            double sc = f >= 63 ? 0.05 + 0.95 * Math.Min(Math.Max(0, f - 63), 5) / 5.0
                                      : f == 0 || (r >= 20 && c < 2) || r <= d ? 1
                                      : r <= d + 5 ? 1 - 0.95 * (r - d) / 5.0 : 0.05;
                            _ghosts.Add(new MonGhost { Mon = fx.Mon, Dx = 0, Dy = (1 - sc) * 24, ScaleX = sc, ScaleY = sc,
                                                       Alpha = 0.7, TintR = 128, TintG = 128, TintB = 128, TintA = 0.5 });
                        }
                        break;
                    }
                    case 8 when fx.ExactSteps:
                    {
                        int acc = 0, pi = 0;
                        for (; pi < fx.Phases.Length; pi++) { int runs = ScalePhaseRuns(fx.Phases[pi]); if (fx.Frame < acc + runs) break; acc += runs; }
                        if (pi >= fx.Phases.Length) break;
                        double[] ph = fx.Phases[pi]; int steps = Math.Max(0, (int)ph[4]), lead = ph.Length > 5 ? (int)ph[5] : 0;
                        int done = Math.Min(fx.Frame - acc - lead + 1, steps);
                        double k = done <= 0 ? 0 : steps == 0 ? 1 : done / (double)steps;
                        double scy = (ph[2] + (ph[3] - ph[2]) * k) / 100.0;
                        MonScaleX[fx.Mon] = (ph[0] + (ph[1] - ph[0]) * k) / 100.0;
                        MonScaleY[fx.Mon] = scy;
                        MonShakeY[fx.Mon] += (1 - scy) * 24;
                        break;
                    }
                    case 8:
                        if (fx.Phases != null && fx.Phases.Length > 0)
                        {
                            int acc = 0, pi = 0;
                            for (; pi < fx.Phases.Length; pi++) { int fr = Math.Max(1, (int)fx.Phases[pi][4]); if (fx.Frame < acc + fr) break; acc += fr; }
                            if (pi >= fx.Phases.Length) pi = fx.Phases.Length - 1;
                            double[] ph = fx.Phases[pi]; int dur = Math.Max(1, (int)ph[4]);
                            double k = Math.Clamp((double)(fx.Frame - acc) / dur, 0, 1);
                            double scy = (ph[2] + (ph[3] - ph[2]) * k) / 100.0;
                            MonScaleX[fx.Mon] = (ph[0] + (ph[1] - ph[0]) * k) / 100.0;
                            MonScaleY[fx.Mon] = scy;
                            MonShakeY[fx.Mon] += (1 - scy) * 24;
                        }
                        break;
                }
                if (++fx.Frame >= fx.Frames + fx.TailRuns) _monFx.RemoveAt(i);
            }
            if (_monVanish[0]) MonVisible[0] = false;
            if (_monVanish[1]) MonVisible[1] = false;
        }

        private void UpdateCellFx()
        {
            if (_cellPhase < 0) return;
            switch (_cellPhase)
            {
                case 0: _cellScaleX = 1.0; _cellScaleY = 0.05; _cellOpacity = 0; if (++_cellFrame >= 1) { _cellPhase = 1; _cellFrame = 0; } break;
                // Each scale is 12 steps and a pass that notices it is done; the task then spends a pass deleting the wave.
                case 1: { double t = Math.Min(_cellFrame + 1, 12) / 12.0; _cellScaleX = Lerp(1.0, 0.6, t); _cellScaleY = Lerp(0.05, 1.5, t); _cellOpacity = t; if (++_cellFrame >= 13) { _cellPhase = 2; _cellFrame = 0; } break; }
                case 2: _cellScaleX = 0.6; _cellScaleY = 1.5; _cellOpacity = 1; if (++_cellFrame >= 4) { _cellPhase = 3; _cellFrame = 0; } break;
                case 3: { double t = Math.Min(_cellFrame + 1, 12) / 12.0; _cellScaleX = Lerp(0.6, 1.5, t); _cellScaleY = Lerp(1.5, 0.1, t); _cellOpacity = 1 - t; if (++_cellFrame >= 13) { _cellPhase = 4; _cellFrame = 0; } break; }
                case 4: _cellOpacity = 0; _cellPhase = -1; break;
            }
            if (_surfActor != null)
            {
                _surfActor.ScaleX = _cellScaleX; _surfActor.ScaleY = _cellScaleY; _surfActor.Alpha = _cellOpacity;
                _surfActor.X = _cellDefX;
                _surfActor.Y = _cellDefY + (80 - SurfSpriteHeight * 2) / 2.0 * (1.0 - _cellScaleY);
                _surfActor.Visible = _cellOpacity > 0;
                if (_cellPhase < 0) { _surfActor.Visible = false; _surfActor.Alive = false; _surfActor = null; }
            }
        }

        private void StartBackground(int bgId, bool overlay, double posX, double posY, double spdX, double spdY,
                                     double peak, int fadeFrames, double stopY, bool useStop)
        {
            BattleBgRenderer.BgImage img = _bgRenderer.Build(bgId, reverse: _attackerIsEnemy);
            if (img == null) return;
            _bgStarts++;
            _bgRgba = img.Rgba; _bgW = img.Width; _bgH = img.Height;
            _bgWrapW = overlay ? FxBgWrap : _bgW;
            _bgWrapH = overlay ? FxBgWrap : _bgH;
            _bgX = posX; _bgY = posY; _bgSpdX = spdX; _bgSpdY = spdY;
            _bgHoldLeft = -1;
            _bgOpacity = 0; _bgPeak = peak; _bgFadeFrames = Math.Max(1, fadeFrames);
            _bgStopY = stopY; _bgUseStop = useStop; _bgFadingOut = false; _bgOverlay = overlay;
        }

        private void UpdateBackground()
        {
            if (_bgRgba == null) return;
            if (_bgHoldLeft > 0 && --_bgHoldLeft == 0) _bgFadingOut = true;
            _bgX += _bgSpdX; _bgY += _bgSpdY;
            if (_bgUseStop && !_bgFadingOut && ((_bgSpdY > 0 && _bgY >= _bgStopY) || (_bgSpdY < 0 && _bgY <= _bgStopY)))
                _bgFadingOut = true;
            double step = _bgPeak / _bgFadeFrames;
            if (_bgFadingOut) { _bgOpacity -= step; if (_bgOpacity <= 0) { _bgOpacity = 0; _bgRgba = null; } }
            else if (_bgOpacity < _bgPeak) _bgOpacity = Math.Min(_bgPeak, _bgOpacity + step);
            double prog = _bgPeak > 0 ? Math.Clamp(_bgOpacity / _bgPeak, 0, 1) : 0;
            BgCa = _bgOpacity;
            BgCb = _bgOverlay ? 1.0 - (1.0 - 7.0 / 16.0) * prog : 1.0 - prog;
        }

        private byte[] _bgBuf;
        public WriteableBitmap RenderBackground()
        {
            if (!HasBackground) return null;
            const int W = 256, H = 192;
            _bgBuf ??= new byte[W * H * 4];
            int sx = ((int)Math.Round(_bgX) % _bgW + _bgW) % _bgW;
            int sy = ((int)Math.Round(_bgY) % _bgH + _bgH) % _bgH;
            double op = _bgOpacity;
            Array.Clear(_bgBuf, 0, _bgBuf.Length);
            for (int y = 0; y < H; y++)
            {
                int ty = (sy + y) % _bgH;
                for (int x = 0; x < W; x++)
                {
                    int tx = (sx + x) % _bgW;
                    int si = (ty * _bgW + tx) * 4, di = (y * W + x) * 4;
                    double a = _bgRgba[si + 3] / 255.0 * op;
                    if (a <= 0) continue;
                    _bgBuf[di + 0] = (byte)(_bgRgba[si + 2] * a);
                    _bgBuf[di + 1] = (byte)(_bgRgba[si + 1] * a);
                    _bgBuf[di + 2] = (byte)(_bgRgba[si + 0] * a);
                    _bgBuf[di + 3] = (byte)(a * 255);
                }
            }
            WriteableBitmap wb = new WriteableBitmap(new global::Avalonia.PixelSize(W, H), new global::Avalonia.Vector(96, 96),
                global::Avalonia.Platform.PixelFormat.Bgra8888, global::Avalonia.Platform.AlphaFormat.Premul);
            using (ILockedFramebuffer fb = wb.Lock())
            {
                int rb = fb.RowBytes;
                if (rb == W * 4) System.Runtime.InteropServices.Marshal.Copy(_bgBuf, 0, fb.Address, _bgBuf.Length);
                else for (int y = 0; y < H; y++) System.Runtime.InteropServices.Marshal.Copy(_bgBuf, y * W * 4, fb.Address + y * rb, W * 4);
            }
            return wb;
        }

        // The palette fade task (SysTask_FadePalette) as FadeBg starts it.
        private sealed class PaletteFade
        {
            public int Step, Wait, WaitStep, End, Shown, Next;
            public bool Done, Drawn;
            public int Advance(int evy) => evy < End ? Math.Min(End, evy + Step) : Math.Max(End, evy - Step);
            public void Run()
            {
                if (Done) return;
                if (WaitStep < Wait) { WaitStep++; return; }
                WaitStep = 0;
                Shown = Next;
                if (Next == End) Done = true;
                else Next = Advance(Next);
            }
        }
        private PaletteFade _palFade;

        // PaletteData_StartFade on the BG palettes. The game ignores a fade while another is still running there.
        private void StartBgPaletteFade(int wait, int startEvy, int endEvy, int rgb15, bool drawn)
        {
            if (_palFade != null && !_palFade.Done) return;
            // It shows the start level now and steps by 2 (2 + |wait| for a negative wait); the fade task then shows the
            // next level every wait + 1 passes and stops a pass after reaching the end.
            PaletteFade fade = new PaletteFade { Step = wait < 0 ? 2 - wait : 2, Wait = Math.Max(0, wait), End = endEvy, Drawn = drawn };
            fade.WaitStep = fade.Wait;
            fade.Shown = startEvy;
            fade.Next = fade.Advance(startEvy);
            fade.Done = startEvy == endEvy;
            int advances = (int)Math.Ceiling(Math.Abs(endEvy - startEvy) / (double)fade.Step);
            if (drawn)
            {
                _fadeStart = Math.Clamp(startEvy / 16.0, 0, 1);
                _fadeEnd = Math.Clamp(endEvy / 16.0, 0, 1);
                _fadeCur = _fadeStart;
                if (rgb15 >= 0) { FadeR = R5(rgb15); FadeG = G5(rgb15); FadeB = B5(rgb15); }
            }
            _palFade = fade;
            // The fade task ends the pass after the fade's last step.
            _fadeFrames = _fadeFramesLeft = advances == 0 ? 1 : 2 + (advances - 1) * (fade.Wait + 1);
        }

        void IBattleAnimSystem.StartBgFade(int wait, int from, int to, int rgb15) => StartBgPaletteFade(wait, from, to, rgb15, true);
        bool IBattleAnimSystem.BgFadeActive => _palFade != null;

        private void UpdateFade()
        {
            if (_fadeFramesLeft <= 0) return;
            _fadeFramesLeft--;
            if (_palFade != null)
            {
                _palFade.Run();
                if (_palFade.Drawn) _fadeCur = Math.Clamp(_palFade.Shown / 16.0, 0, 1);
                if (_fadeFramesLeft == 0) _palFade = null;
                return;
            }
            double t = _fadeFrames <= 0 ? 1 : 1.0 - (double)_fadeFramesLeft / _fadeFrames;
            _fadeCur = _fadeStart + (_fadeEnd - _fadeStart) * t;
        }
    }
}
