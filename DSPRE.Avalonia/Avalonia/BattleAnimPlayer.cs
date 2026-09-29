using System;
using System.Collections.Generic;
using global::Avalonia.Media.Imaging;
using DSPRE.Avalonia.Data;

namespace DSPRE.Avalonia
{
    public sealed class BattleAnimPlayer
    {
        public static (double x, double y) AttackerScreen => ToScreen(-15360, -6272);
        public static (double x, double y) DefenderScreen => ToScreen(+13568, +2944);
        private static (double x, double y) ToScreen(double px, double py) => (px / 172.0 + 120.0, 96.0 - py / 172.0);

        private const int FnRotateMon = 4, FnFadeBg = 33, FnFadeBattlerSprite = 34,
                          FnScalePokemonSprite = 35, FnShake = 36, FnHideBattler = 40, FnScaleBattlerSprite = 42,
                          FnShakeBg = 68,
                          FnMoveEmitterA2BLinear = 65, FnMoveEmitterA2BParabolic = 66, FnRevolveEmitter = 72;
        private static readonly HashSet<int> FnMoveBattlerFamily = new HashSet<int> { 51, 52, 53, 54, 57 };
        private const int FnBlinkAttacker = 50;
        private const int FnMoveBattlerOffScreen = 61, FnMoveBattlerToDefaultPos = 62, FnMoveBattlerOnOrOffScreen = 77, FnSetBgGrayscale = 74;
        private const int OffScreenLeft = -80, OffScreenRight = 256 + 80;
        private const int FnScrollCustomBg = 44, FnMuddyWater = 45;
        private const int FnRevolveBattler = 60;
        private const int AnimAttacker = 0x0002, AnimAttackerPartner = 0x0004, AnimDefender = 0x0008, AnimDefenderPartner = 0x0010,
                          AnimNotAttacker = 0x0020, AnimAllBattlers = 0x0040, AnimBackground = 0x0400;
        private const int EmitterCbGeneric = 17;

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
                                     public DroppedCap Cap; }
        private readonly List<MonFx> _monFx = new List<MonFx>();

        private readonly List<WazaSeqCommand> _cmds;
        private readonly WazaSeqVersion _version;
        private readonly ScriptNarc _particleNarc;
        private readonly double _atX, _atY, _dfX, _dfY;
        private readonly SpaParticlePreview _renderer;
        private readonly Dictionary<int, SpaArchive> _archives = new Dictionary<int, SpaArchive>();
        private readonly Dictionary<int, int> _slot = new Dictionary<int, int>();
        private readonly Dictionary<int, SpaSimulator> _emitSlots = new Dictionary<int, SpaSimulator>();
        private readonly Dictionary<int, List<SpaSimulator>> _ptcSims = new Dictionary<int, List<SpaSimulator>>();
        private SpaSimulator _lastSim;

        private sealed class LoopFrame { public int Body, Total, Count; }
        private readonly List<LoopFrame> _loops = new List<LoopFrame>();
        private readonly List<int> _callStack = new List<int>();

        private int _pc, _wait;
        private bool _scriptDone, _waitParticles, _waitFlag;
        private int _bgWait;
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
        private const int FX_BG_WRAP = 512;
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
                    Note("This move changes a background setting part way through that the preview does not follow.");
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
        public double RasterLineAdd => Math.PI / 180.0;
        public bool HasBackground => _bgRgba != null && _bgOpacity > 0.001;
        public bool BackgroundIsOverlay => _bgOverlay;
        private bool BgSettled => _bgRgba == null || (!_bgFadingOut && _bgOpacity >= _bgPeak - 1e-6);
        private bool BgHalf => _bgRgba == null || _bgOpacity >= _bgPeak * 0.5 - 1e-6;
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
            _atX = atX; _atY = atY;
            _dfX = selfTarget ? atX : dfX; _dfY = selfTarget ? atY : dfY;
            _renderer = new SpaParticlePreview(width, height);
            int wp = 0;
            for (int i = 0; i < _cmds.Count; i++)
            {
                _cmds[i].WordPos = wp;
                _wordToIndex[wp] = i;
                wp += 1 + _cmds[i].Args.Length;
            }
        }

        public bool SecondTurnVariant { get; set; }

        private readonly bool _attackerIsEnemy;
        private readonly SplRandom _splRandom = new SplRandom(0x5EED);
        private readonly Dictionary<int, int> _wordToIndex = new Dictionary<int, int>();

        public Action<int> PlaySound;

        public Action PlayCry;

        public Action<int> StopSound;

        private readonly List<(int framesLeft, int soundId)> _pendingSounds = new List<(int, int)>();
        private void SchedulePlaySound(int soundId, int delayFrames)
        {
            if (delayFrames <= 0) PlaySound?.Invoke(soundId);
            else _pendingSounds.Add((delayFrames, soundId));
        }
        private void TickPendingSounds()
        {
            for (int i = _pendingSounds.Count - 1; i >= 0; i--)
            {
                var (framesLeft, soundId) = _pendingSounds[i];
                if (framesLeft <= 0) { PlaySound?.Invoke(soundId); _pendingSounds.RemoveAt(i); }
                else _pendingSounds[i] = (framesLeft - 1, soundId);
            }
        }

        private bool JumpRelative(int argWord, int offset)
        {
            if (_wordToIndex.TryGetValue(argWord + offset, out int idx)) { _pc = idx; return true; }
            return false;
        }

        public bool Finished => _scriptDone && _renderer.AllFinished && _fadeFramesLeft <= 0 && _monFx.Count == 0 && !HasBackground && _cellPhase < 0 && _pendingSounds.Count == 0;
        public WriteableBitmap RenderFrame() => _renderer.RenderFrame();

        public IEnumerable<SpaParticleState> LiveParticles() => _renderer.LiveParticles();

        public void Step()
        {
            TickPendingSounds();
            UpdateFade();
            UpdateMonFx();
            UpdateBackground();
            if (_rasterLeft > 0) { _rasterLeft--; RasterPhase += 200.0 / 65536.0 * 2 * Math.PI; }
            UpdateCellFx();
            for (int i = 0; i < _spriteActors.Count; i++)
            {
                var a = _spriteActors[i];
                if (a == _surfActor) continue;
                a.Tick(); RunSpriteFunc(a); a.Age++;
            }

            if (_wait > 0) _wait--;
            else if (_waitFlag) { if (_monFx.Count == 0 && _cellPhase < 0 && _fadeFramesLeft <= 0) _waitFlag = false; }
            else if (_bgWait != 0) { if (_bgWait == 2 ? BgHalf : BgSettled) _bgWait = 0; }
            else if (_waitParticles) { if (_renderer.AllFinished) _waitParticles = false; }
            else if (!_scriptDone) RunCommands();

            _renderer.Step();
        }

        private void RunCommands()
        {
            while (_pc < _cmds.Count)
            {
                if (++_guard > 100000) { _scriptDone = true; return; }
                var c = _cmds[_pc];
                string name = BattleAnimCommands.Name(_version, c.OpId);
                _commandsRun.Add(_pc);
                _pc++;

                switch (name)
                {
                    case "End":
                        _scriptDone = true; return;

                    case "PlaySoundEffect":
                    case "PlayPannedSoundEffect":
                    case "PlayMovingSoundEffectAtkDef":
                        if (c.Args.Length >= 1) PlaySound?.Invoke(c.Args[0]);
                        break;

                    case "PlayDelayedSoundEffect":
                        if (c.Args.Length >= 3) SchedulePlaySound(c.Args[0], c.Args[2]);
                        else if (c.Args.Length >= 1) PlaySound?.Invoke(c.Args[0]);
                        break;

                    case "PlayLoopedSoundEffect":
                        if (c.Args.Length >= 4)
                        {
                            int wait = Math.Max(0, c.Args[2]), repeat = Math.Max(1, c.Args[3]);
                            for (int r = 0; r < repeat; r++) SchedulePlaySound(c.Args[0], r * wait);
                        }
                        else if (c.Args.Length >= 1) PlaySound?.Invoke(c.Args[0]);
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
                        if (c.Args.Length >= 1) JumpRelative(c.WordPos + 1, c.Args[0]);
                        break;

                    case "JumpIfContest":
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
                            var f = _loops[_loops.Count - 1];
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

                    case "Delay": _wait = c.Args.Length > 0 ? Math.Max(0, c.Args[0]) : 0; return;
                    case "WaitForAnimTasks":
                        if (_monFx.Count > 0 || _cellPhase >= 0) { _waitFlag = true; return; }
                        break;
                    case "WaitForAllEmitters":
                        if (!_renderer.AllFinished) { _waitParticles = true; return; }
                        break;

                    case "LoadParticleSystem":
                    case "LoadDebugParticleSystem":
                        if (c.Args.Length >= 2) _slot[c.Args[0]] = c.Args[1];
                        break;
                    case "SetCameraProjection":
                        if (c.Args.Length >= 2) _cameraProjection[c.Args[0]] = c.Args[1];
                        break;
                    case "SetCameraFlip":
                        if (c.Args.Length >= 2) _cameraMode[c.Args[0]] = c.Args[1];
                        break;
                    case "UnloadParticleSystem":
                        if (c.Args.Length >= 1 && _ptcSims.TryGetValue(c.Args[0], out var exitSims))
                        {
                            foreach (var s in exitSims) s.Stop();
                            exitSims.Clear();
                        }
                        break;

                    case "CreateEmitter":
                        if (c.Args.Length >= 2)
                        {
                            int cb = c.Args.Length >= 3 ? c.Args[2] : 0;
                            if (cb == EmitterCbGeneric) SpawnOperator(c.Args[0], c.Args[1]);
                            else Spawn(c.Args[0], c.Args[1], cb, 0, 1);
                        }
                        break;
                    case "CreateEmitterEx":
                        if (c.Args.Length >= 4)
                        {
                            var sim = Spawn(c.Args[0], c.Args[2], c.Args[3], 0, 1);
                            if (sim != null) _emitSlots[c.Args[1]] = sim;
                        }
                        break;
                    case "CreateEmitterForMove":
                    case "CreateEmitterForFriendlyFire":
                        if (c.Args.Length >= 3)
                        {
                            int cb = c.Args[c.Args.Length - 1], count = c.Args.Length - 2;
                            int sep = _attackerIsEnemy ? 3 : 0;
                            if (sep >= count) sep = 0;
                            Spawn(c.Args[0], c.Args[1 + sep], cb, 0, 1);
                        }
                        break;

                    case "AddSprite":
                        AddSpriteActor(c.Args.Length > 1 ? c.Args[1] : 0, withCallback: false, null);
                        break;
                    case "AddSpriteWithFunc":
                        AddSpriteActor(c.Args.Length > 1 ? c.Args[1] : -1, withCallback: true,
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
                            StartBackground(c.Args[0], overlay: false, posX: _work[2] * hRev, posY: _work[3] * hRev,
                                spdX: _work[0] * hRev, spdY: _work[1] * hRev, peak: 1.0, fadeFrames: 12, stopY: 0, useStop: false);
                        }
                        break;
                    case "RestoreBg":
                        _bgFadingOut = true;
                        break;
                    case "WaitForBgSwitch":
                        if (!BgSettled) { _bgWait = 1; return; }
                        break;
                    case "WaitForPartialBgSwitch":
                        if (!BgHalf) { _bgWait = 2; return; }
                        break;

                    case "FlashScreen":
                        _fadeStart = 1.0; _fadeEnd = 0; _fadeCur = 1.0;
                        _fadeFrames = _fadeFramesLeft = c.Args.Length >= 1 && c.Args[0] > 0 ? c.Args[0] : 8;
                        FadeR = FadeG = FadeB = 255;
                        break;
                    case "SwitchBgAnimated":
                        if (c.Args.Length >= 1)
                        {
                            int hRevEx = BackdropScrollReversedForSide() ? -1 : 1;
                            StartBackground(c.Args[0], overlay: false, posX: _work[2] * hRevEx, posY: _work[3] * hRevEx,
                                spdX: _work[0] * hRevEx, spdY: _work[1] * hRevEx, peak: 1.0, fadeFrames: 12, stopY: 0, useStop: false);
                        }
                        break;
                    case "JumpIfBatonPass":
                        break;

                    case "StopSoundEffect":
                        if (c.Args.Length >= 1)
                        {
                            int stopId = c.Args[0];
                            _pendingSounds.RemoveAll(x => x.soundId == stopId);
                            StopSound?.Invoke(stopId);
                        }
                        break;

                    case "PlayPokemonCry":
                        if (PlayCry != null) PlayCry();
                        else Note("This move plays the Pokémon's cry, which the preview cannot play here.");
                        break;

                    case "WaitForPokemonCries":
                        _wait = c.Args.Length > 0 ? Math.Max(0, c.Args[0]) : 0;
                        return;

                    case "SetPokemonSpriteVisible":
                        if (c.Args.Length >= 2 && _caps.TryGetValue(c.Args[0], out var oamCap))
                            oamCap.Visible = c.Args[1] != 0;
                        break;

                    case "StartTransform":
                    case "StartTransformRecolour":
                        Note("This move swaps the Pokémon's graphic for another one, which the preview keeps as it is.");
                        break;

                    case "LoadPokemonSpriteIntoBg":
                        Note("This move draws a copy of the Pokémon into the background, which the preview does not do.");
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
                        Note("This move has a developer's pause left in it, which the preview runs straight past.");
                        break;

                    case "CallFunc":
                    case "Nop11":
                        DoFuncCall(c.Args);
                        break;
                }
            }
            _scriptDone = true;
        }

        private SpaArchive LoadArc(int data)
        {
            if (!_archives.TryGetValue(data, out var arc))
            {
                var bytes = _particleNarc?.Get(data);
                arc = bytes != null ? SpaArchive.Parse(bytes) : new SpaArchive();
                _archives[data] = arc;
            }
            return arc;
        }

        private SpaSimulator Spawn(int ptc, int emitterNo, int callback, int sepIndex, int sepCount)
        {
            if (!_slot.TryGetValue(ptc, out int data)) return null;
            var arc = LoadArc(data);
            if (emitterNo < 0 || emitterNo >= arc.Emitters.Count) return null;
            var em = arc.Emitters[emitterNo];
            var tex = (em.TexNo >= 0 && em.TexNo < arc.Textures.Count) ? arc.Textures[em.TexNo] : null;
            if (tex == null)
                Note($"Particle {emitterNo} asks for picture {em.TexNo}, which is not in its file, so it "
                     + "is drawn as a plain dot.");
            else if (tex.Rgba == null)
                Note($"Particle {emitterNo}'s picture is stored in a way DSPRE cannot read (format "
                     + $"{tex.Format}), so it is drawn as a plain dot.");
            var (cx, cy, ax, ay, z) = Place(callback, sepIndex, sepCount);
            cx += em.PosX; cy -= em.PosY;
            double axX, axY;
            if (ax != 0 || ay != 0) { axX = ax; axY = -ay; }
            else { axX = em.AxisX; axY = em.AxisY; }
            var sim = new SpaSimulator(em, axX, axY, rng: _splRandom) { AnchorX = cx, AnchorY = cy };
            bool reversed = ((callback == 1 || callback == 2) && _attackerIsEnemy)
                            || (_cameraMode.TryGetValue(ptc, out int camMode) && camMode != 0);
            _renderer.AddLayer(new SpaParticlePreview.Layer(sim, arc.Textures, tex, cx, cy, em.DrawType,
                em.RepeatS, em.RepeatT, em.Aspect, em.DbbScale, em.OffsetX, em.OffsetY,
                baseZ: z + em.PosZ, viewReversed: reversed, flipS: em.FlipS, flipT: em.FlipT, em: em,
                orthographic: IsOrthographic(ptc)));
            _lastSim = sim;
            TrackSim(ptc, sim);
            return sim;
        }

        private readonly Dictionary<int, int> _cameraMode = new Dictionary<int, int>();
        private readonly Dictionary<int, int> _cameraProjection = new Dictionary<int, int>();

        private bool IsOrthographic(int ptc) => !_cameraProjection.TryGetValue(ptc, out int projection) || projection != 0;

        private void TrackSim(int ptc, SpaSimulator sim)
        {
            if (!_ptcSims.TryGetValue(ptc, out var list)) _ptcSims[ptc] = list = new List<SpaSimulator>();
            list.Add(sim);
        }

        private SpaSimulator FindEmitter(int idx) => _emitSlots.TryGetValue(idx, out var s) ? s : _lastSim;

        private static readonly HashSet<int> _startPos = new HashSet<int> { 1, 4, 6, 8, 10, 14, 16, 18, 20, 22, 24, 26, 34 };
        private static bool IsStartPos(int pos) => _startPos.Contains(pos);

        private void SpawnOperator(int ptc, int emitterNo)
        {
            int target = 2, pos = 0, axis = 0, fldMode = 0;
            int fldTgt = -1, fldFracN = 1, fldFracD = 1;
            double posOfsX = 0, posOfsY = 0;
            if (_pc < _cmds.Count && BattleAnimCommands.Name(_version, _cmds[_pc].OpId) == "SetExtraParams")
            {
                var ex = _cmds[_pc].Args;
                target = ex.Length > 2 ? ex[2] : 2;
                pos = ex.Length > 3 ? ex[3] : 0;
                axis = ex.Length > 4 ? ex[4] : 0;
                fldMode = ex.Length > 5 ? ex[5] : 0;
                if (_pc + 1 < _cmds.Count && BattleAnimCommands.Name(_version, _cmds[_pc + 1].OpId) == "SetExtraParams")
                {
                    var ex2 = _cmds[_pc + 1].Args;
                    if (pos == 4 || pos == 5 || pos == 12 || pos == 13)
                    {
                        if (ex2.Length > 3) { posOfsX = ex2[2] / 172.0; posOfsY = ex2[3] / 172.0; }
                    }
                    else
                    {
                        int m = ex2.Length > 1 ? ex2[1] : -1;
                        if (m == 2 || m == 3) fldTgt = m;
                        else if (m == 4 && ex2.Length > 4 && ex2[4] != 0) { fldTgt = 3; fldFracN = ex2[3]; fldFracD = ex2[4]; }
                    }
                }
            }
            bool swapClients = target == 1 || target == 3;
            int sClient = swapClients ? 1 : 0;
            int eClient = swapClients ? 0 : 1;
            int src = (IsStartPos(pos) || pos == 12) ? sClient : eClient;
            bool axisOverride = axis >= 1 && axis <= 21 && axis != 3;

            if (!_slot.TryGetValue(ptc, out int data)) return;
            var arc = LoadArc(data);
            if (emitterNo < 0 || emitterNo >= arc.Emitters.Count) return;
            var em = arc.Emitters[emitterNo];
            var tex = (em.TexNo >= 0 && em.TexNo < arc.Textures.Count) ? arc.Textures[em.TexNo] : null;

            double anchorX = src == 0 ? _atX : _dfX, anchorY = src == 0 ? _atY : _dfY;
            if (pos == 30 || pos == 31 || pos == 32)
            {
                int t = sClient == 0 ? 0 : 1;
                var p = pos == 30 ? Pos226[t] : pos == 32 ? Pos225[t] : Pos145[t];
                anchorX = PARTICLE_ORIGIN_X + p.x / 172.0;
                anchorY = PARTICLE_ORIGIN_Y - p.y / 172.0;
            }
            double sx = anchorX + em.PosX + posOfsX, sy = anchorY - em.PosY - posOfsY;
            double driftX = 0, driftY = 0, magOX = double.NaN, magOY = double.NaN, convOX = double.NaN, convOY = double.NaN;
            double magOZ = double.NaN, convOZ = double.NaN;
            double sCx = sClient == 0 ? _atX : _dfX, sCy = sClient == 0 ? _atY : _dfY;
            double eCx = eClient == 0 ? _atX : _dfX, eCy = eClient == 0 ? _atY : _dfY;
            double atdfX = eCx - sCx, atdfY = sCy - eCy;
            double atdfLen = Math.Sqrt(atdfX * atdfX + atdfY * atdfY);
            if (atdfLen > 1e-6) { atdfX /= atdfLen; atdfY /= atdfLen; } else { atdfX = 0; atdfY = 1; }
            if (fldTgt >= 0 && fldMode != 0)
            {
                double tgX = fldTgt == 2 ? _atX : _dfX, tgY = fldTgt == 2 ? _atY : _dfY;
                if (fldFracD != 1 || fldFracN != 1)
                {
                    double f = (double)fldFracN / fldFracD;
                    tgX = PARTICLE_ORIGIN_X + f * (tgX - PARTICLE_ORIGIN_X);
                    tgY = PARTICLE_ORIGIN_Y + f * (tgY - PARTICLE_ORIGIN_Y);
                }
                double rX = tgX - sx, rY = sy - tgY;
                double rZ = ZOfVis(fldTgt == 2 ? _atVis : _dfVis) - (ZOfVis(src == 0 ? _atVis : _dfVis) + em.PosZ);
                if ((fldMode & 0x1000) != 0) { convOX = rX; convOY = rY; convOZ = rZ; }
                else if ((fldMode & 0x10) != 0) { magOX = rX; magOY = rY; magOZ = rZ; }
            }
            double opAxX, opAxY;
            if (axisOverride) { opAxX = atdfX; opAxY = atdfY; }
            else if (axis == 24)
            {
                int t = sClient == 0 ? 0 : 1;
                double ax = Axis145[t].x, ay = Axis145[t].y, l = Math.Sqrt(ax * ax + ay * ay);
                if (l < 1e-6) l = 1; opAxX = ax / l; opAxY = ay / l;
            }
            else if (axis == 26)
            {
                bool mine = (sClient == 0) != _attackerIsEnemy;
                double ax = mine ? 3776 : -6000, ay = mine ? 2112 : -2200, l = Math.Sqrt(ax * ax + ay * ay);
                opAxX = ax / l; opAxY = ay / l;
            }
            else if (axis == 3)
            {
                double ax = -800, ay = 1200, l = Math.Sqrt(ax * ax + ay * ay);
                opAxX = ax / l; opAxY = ay / l;
            }
            else { opAxX = em.AxisX; opAxY = em.AxisY; }
            var sim = new SpaSimulator(em, opAxX, opAxY, driftX, driftY, magOX, magOY, convOX, convOY,
                                       magOverrideZ: magOZ, convOverrideZ: convOZ, rng: _splRandom);
            double opZ = ZOfVis(src == 0 ? _atVis : _dfVis) + em.PosZ;
            bool opReversed = _cameraMode.TryGetValue(ptc, out int opCam) && opCam != 0;
            _renderer.AddLayer(new SpaParticlePreview.Layer(sim, arc.Textures, tex, sx, sy, em.DrawType,
                em.RepeatS, em.RepeatT, em.Aspect, em.DbbScale, em.OffsetX, em.OffsetY,
                baseZ: opZ, viewReversed: opReversed, flipS: em.FlipS, flipT: em.FlipT, em: em,
                orthographic: IsOrthographic(ptc)));
            _lastSim = sim;
            TrackSim(ptc, sim);
        }

        private const double PARTICLE_ORIGIN_X = 120, PARTICLE_ORIGIN_Y = 96;
        private static readonly (int x, int y)[] Pos145 = { (-5760, -4352), (9488, -1984) };
        private static readonly (int x, int y)[] Pos225 = { (-4608, -4480), (7624, 2248) };
        private static readonly (int x, int y)[] Pos226 = { (-11020, -3488), (10880, 7656) };
        private static readonly (int x, int y)[] Axis145 = { (2864, 3752), (-2944, 1456) };

        private const int WorkSlots = 8 + 2;

        private void DoFuncCall(int[] a)
        {
            if (a.Length < 1) return;

            if (a.Length < 2 + WorkSlots)
            {
                var padded = new int[2 + WorkSlots];
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
                foreach (var act in _spriteActors)
                {
                    if (act.CapId == castCap) { _surfActor = act; }
                    else if (act.CapId == 0 || act.CapId == 1) { act.Visible = false; act.Alive = false; }
                }
                var def = _attackerIsEnemy ? SurfHomeEnemy : SurfHomePlayer;
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
                return;
            }
            switch (fn)
            {
                case FnShake when a.Length >= 6:
                {
                    int mode = a.Length > 6 ? a[6] : AnimDefender;
                    if ((mode & AnimBackground) != 0)
                    {
                        _monFx.Add(new MonFx { Kind = 5, Mon = 0, ToScene = true,
                            Sh = new Shake(a[2], a[3], Math.Max(1, a[4]), Math.Max(1, a[5])), NumMax = 0 });
                        break;
                    }
                    foreach (int t in TargetsFromFlags(mode))
                        _monFx.Add(new MonFx { Kind = 5, Mon = t, ToScene = false,
                            Sh = new Shake(a[2], a[3], Math.Max(1, a[4]), Math.Max(1, a[5])), NumMax = 0 });
                    break;
                }
                case FnShakeBg when a.Length >= 6:
                {
                    _monFx.Add(new MonFx { Kind = 5, Mon = 0, ToScene = true,
                        Sh = new Shake(a[2], a[3], Math.Max(1, a[4]), Math.Max(1, a[5])),
                        NumMax = a.Length > 6 ? Math.Max(0, a[6]) : 0 });
                    break;
                }

                case 82:
                case 83:
                {
                    double speed = fn == 82 ? 3 : -6;
                    StartBackground(a[2], overlay: true, posX: 0, posY: 0, spdX: 0, spdY: speed,
                        peak: 12 / 16.0, fadeFrames: 12, stopY: 0, useStop: false);
                    _bgHoldLeft = 20;
                    break;
                }

                case FnFadeBg when a.Length >= 6:
                {
                    if (a[2] != 0)
                    {
                        Note("This move fades an effect layer's colours, which the preview does not draw.");
                        break;
                    }
                    int wait = a[3];
                    int startEvy = a[4], endEvy = a.Length > 5 ? a[5] : 0;
                    _fadeStart = Math.Clamp(startEvy / 16.0, 0, 1);
                    _fadeEnd = Math.Clamp(endEvy / 16.0, 0, 1);
                    _fadeCur = _fadeStart;
                    int fadeVal = wait < 0 ? 2 + (-wait) : 2;
                    int effWait = wait < 0 ? 0 : wait;
                    int steps = Math.Max(1, (int)Math.Ceiling(Math.Abs(endEvy - startEvy) / (double)fadeVal));
                    _fadeFrames = _fadeFramesLeft = steps * (effWait + 1);
                    if (a.Length >= 7) { int c = a[6]; FadeR = R5(c); FadeG = G5(c); FadeB = B5(c); }
                    break;
                }

                case FnRotateMon:
                    _monFx.Add(new MonFx { Mon = _atVis, Frames = 16, Kind = 0 });
                    break;
                case FnScaleBattlerSprite when a.Length >= 10:
                {
                    double sx = a[3] / 100.0, ex = a[4] / 100.0, sy = a[5] / 100.0, ey = a[6] / 100.0;
                    int num = Math.Max(1, a[8] & 0xffff), wait = (a[8] >> 16) & 0xffff;
                    int upF = Math.Max(1, (a[9] >> 16) & 0xffff), downF = Math.Max(1, a[9] & 0xffff);
                    int per = upF + wait + downF;
                    _monFx.Add(new MonFx
                    {
                        Mon = Math.Max(0, MonFromFlag(a, 2)), Kind = 1, Frames = num * per,
                        Keys = new[] { sx, ex, sy, ey }, UpF = upF, WaitF = wait, DownF = downF, Cycles = num,
                    });
                    break;
                }
                case FnScalePokemonSprite when a.Length >= 9:
                {
                    int sd = Math.Max(1, a[6]);
                    double s = a[4] / (double)sd, e = a[5] / (double)sd;
                    int num = Math.Max(1, a[7]);
                    int upF = Math.Max(1, (a[8] >> 16) & 0xffff), downF = Math.Max(1, a[8] & 0xffff);
                    int capId = a.Length > 9 ? a[9] : -1;
                    DroppedCap cap = (capId >= 0 && _caps.TryGetValue(capId, out var dc)) ? dc : null;
                    _monFx.Add(new MonFx
                    {
                        Mon = a[2] == 0 ? _atVis : _dfVis, Cap = cap, Kind = 1, Frames = num * (upF + downF),
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
                        _monFx.Add(new MonFx { Mon = mon, Kind = 4, Frames = wait, Dx = OffscreenX(mon) - restX, Dy = 0 });
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
                    AddScaleSeq(_atVis, new[] { new double[]{100,150,100,50,10}, new double[]{150,50,50,150,10},
                        new double[]{50,100,150,100,5}, new double[]{100,150,100,150,5}, new double[]{150,100,150,100,5} });
                    break;
                case 13:
                    AddScaleSeq(_atVis, new[] { new double[]{100,115,100,115,6}, new double[]{115,100,115,100,6} }, 4);
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = 6, Frames = 24, UpF = 6, WaitF = 0,
                        Keys = new double[] { 6 }, R = 255, G = 255, B = 255 });
                    break;
                case 14:
                    AddScaleSeq(_atVis, new[] { new double[]{100,150,100,50,8}, new double[]{150,50,50,150,8}, new double[]{50,100,150,100,8} });
                    break;
                case 15:
                    AddScaleSeq(_atVis, new[] { new double[]{100,10,100,180,10}, new double[]{10,10,180,0,5} });
                    break;
                case 19:
                    AddScaleSeq(_atVis, new[] { new double[]{100,120,100,80,5}, new double[]{120,100,80,120,5}, new double[]{100,100,120,100,5} }, 3);
                    break;
                case 5:
                {
                    int sq = a.Length > 2 ? a[2] : 70, st = a.Length > 3 ? a[3] : 120;
                    int sqSync = a.Length > 4 ? Math.Max(1, a[4]) : 10, stSync = a.Length > 5 ? Math.Max(1, a[5]) : 5;
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = 5, Sh = new Shake(2, 0, 1, 4), NumMax = 0 });
                    AddScaleSeq(_atVis, new[] { new double[]{100,sq,100,sq,sqSync}, new double[]{sq,sq,sq,sq,18},
                        new double[]{sq,st,sq,st,stSync}, new double[]{st,100,st,100,5} });
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = 6, Frames = 18, UpF = 3, WaitF = 0, Cycles = 3,
                        Keys = new double[] { 10 }, R = 255, G = 0, B = 0, Delay = sqSync });
                    break;
                }
                case 24:
                    _monFx.Add(new MonFx { Kind = 20, Frames = 40 });
                    break;
                case 7:
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = 9, Frames = 80 });
                    break;
                case 8:
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = 22, Frames = 8, Cycles = 1, Dx = _atVis == 0 ? 1 : -1, NumMax = 2 });
                    break;
                case 9:
                {
                    double s = _atVis == 0 ? 1.0 : -1.0;
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = 10, Frames = 14, Keys = new double[] { 20, 1 } });
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = 13, Frames = 3 + 7 + 2, UpF = 3, WaitF = 7, DownF = 2, Dx = -32 * s, Dy = 0 });
                    break;
                }
                case 10:
                    if (a.Length >= 5)
                    {
                        int rn = Math.Max(1, a[2]), sync = Math.Max(1, a[3]);
                        double dir = (a[4] & AnimAttacker) != 0 ? -1 : 1;
                        int om = MonFromFlag(a, 4);
                        if (om >= 0) _monFx.Add(new MonFx { Mon = om, Kind = 22, Frames = sync * rn, Cycles = rn, Dx = dir });
                    }
                    break;
                case 58:
                    AddScaleSeq(_dfVis, new[] { new double[] { 100, 20, 100, 20, 10 } });
                    break;
                case 31:
                    AddScaleSeq(_atVis, new[] { new double[]{100,150,100,150,8}, new double[]{150,150,150,150,4}, new double[]{150,100,150,100,8} });
                    break;
                case 46:
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = 5, Sh = new Shake(4, 0, 1, 4), NumMax = 0 });
                    break;
                case 47:
                {
                    double s = _atVis == 0 ? 1.0 : -1.0;
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = 5, Sh = new Shake(4, 0, 1, 4), NumMax = 0 });
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = 13, Frames = 4 + 8 + 4, UpF = 4, WaitF = 8, DownF = 4,
                                           Dx = 40 * s, Dy = -7 * s, Delay = 16 });
                    break;
                }
                case 48:
                {
                    double s = _dfVis == 0 ? 1.0 : -1.0;
                    _monFx.Add(new MonFx { Mon = _dfVis, Kind = 13, Frames = 4 + 16 + 4, UpF = 4, WaitF = 16, DownF = 4,
                                           Dx = -40 * s, Dy = 16 * s });
                    _monFx.Add(new MonFx { Mon = _dfVis, Kind = 5, Sh = new Shake(4, 0, 1, 4), NumMax = 0, Delay = 4 });
                    break;
                }
                case 55:
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = 16, Frames = 16, Keys = new double[] { 16, 2 } });
                    break;
                case 26:
                    _monFx.Add(new MonFx { Mon = (a.Length > 2 && a[2] == 0) ? _dfVis : _atVis, Kind = 4, Frames = 20, Dx = -20, Dy = 20 });
                    break;
                case 27:
                    if (a.Length >= 7)
                        _monFx.Add(new MonFx { Mon = a[2] == 0 ? _atVis : _dfVis, Kind = 5,
                            Sh = new Shake(a[3], a[4], Math.Max(1, a[5]), Math.Max(1, a[6])), NumMax = 0 });
                    break;
                case 28:
                {
                    int pow = MovePower switch { 150 => 6, 110 => 5, 90 => 4, 70 => 3, 50 => 2, 30 => 1, _ => 0 };
                    _monFx.Add(new MonFx { Kind = 5, ToScene = true, Sh = new Shake(2 + pow, pow, 1, 10), NumMax = 0 });
                    break;
                }
                case 11:
                    _monFx.Add(new MonFx { Mon = _dfVis, Kind = 5, Sh = new Shake(2, 0, 1, 6), NumMax = 0 });
                    AddScaleSeq(_dfVis, new[] { new double[]{100,120,100,150,7}, new double[]{120,100,150,100,4} });
                    break;
                case 16:
                {
                    const int fadeIn = 8, hold = 5;
                    _fadeStart = 0; _fadeEnd = 1; _fadeCur = 0;
                    FadeR = FadeG = FadeB = 255;
                    _fadeFrames = _fadeFramesLeft = fadeIn * 2 + hold;
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = 6, Frames = fadeIn * 2 + hold,
                        UpF = fadeIn, WaitF = hold, Cycles = 1, Keys = new double[] { 16 },
                        R = 0, G = 0, B = 0 });
                    break;
                }
                case 17:
                    AddScaleSeq(_atVis, new[] { new double[]{140,100,140,100,8} });
                    break;
                case 18:
                    _monFx.Add(new MonFx { Mon = _dfVis, Kind = 5, Sh = new Shake(4, 0, 1, 4), NumMax = 0 });
                    _monFx.Add(new MonFx { Mon = _dfVis, Kind = 3, Frames = 16, R = 0, G = 0, B = 0 });
                    break;
                case 23:
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = 7, Frames = 32, Keys = new double[] { 24, 8, 16, 0 } });
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = 21, Frames = 64, Keys = new double[] { 0.0, 0 } });
                    break;
                case 25:
                    _monFx.Add(new MonFx { Mon = (a.Length > 2 && a[2] == 0) ? _atVis : _dfVis, Kind = 10, Frames = 12, Keys = new double[] { 15, 4 } });
                    break;
                case 29:
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = 5, Sh = new Shake(0, 32, 6, 4), NumMax = 0 });
                    break;
                case 30:
                {
                    double sAt = _atVis == 0 ? 1.0 : -1.0;
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = 22, Frames = 64, Cycles = 1, Dx = sAt });
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = 13, Frames = 2 + 2 + 8, UpF = 2, WaitF = 2, DownF = 8,
                                           Dx = 32 * sAt, Dy = 0, Delay = 64 });
                    _monFx.Add(new MonFx { Mon = _dfVis, Kind = 13, Frames = 2 + 2 + 8, UpF = 2, WaitF = 2, DownF = 8,
                                           Dx = 32 * sAt, Dy = 0, Delay = 66 });
                    break;
                }
                case 32:
                    AddScaleSeq(_atVis, new[] { new double[]{100,10,100,200,6}, new double[]{20,100,200,100,8} });
                    break;
                case 37:
                    _monFx.Add(new MonFx { Mon = _dfVis, Kind = 23, Frames = 3 * 16 });
                    break;
                case 12:
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = 21, Frames = 44, Keys = new double[] { 0.1, 1 } });
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = 24, Frames = 44 });
                    break;
                case 67:
                {
                    int mon = MonFromFlag(a, 2), wait = Math.Max(1, a.Length > 6 ? a[6] : 8);
                        if (mon < 0) break;
                    int my = a.Length > 5 ? a[5] : 1;
                    _monFx.Add(new MonFx { Mon = mon, Kind = 15, Frames = wait, Dx = my > 0 ? 1 : -1 });
                    break;
                }
                case 22:
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = 14, Frames = 28 });
                    break;
                case 20:
                    _monFx.Add(new MonFx { Mon = _dfVis, Kind = 21, Frames = 48, Keys = new double[] { 0.5, 0 } });
                    break;
                case 21:
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = 3, Frames = 12, R = 255, G = 255, B = 255 });
                    AddScaleSeq(_atVis, new[] { new double[]{100,5,100,5,5}, new double[]{5,100,5,100,5} });
                    break;
                case 59:
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = 3, Frames = 20, R = 196, G = 196, B = 196 });
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = 13, Frames = 20, UpF = 10, DownF = 10,
                        Dx = 48 * (_atVis == 0 ? 1 : -1), Dy = 0 });
                    break;
                case 38:
                {
                    int capBit = a.Length > 2 ? a[2] : 1, a1s = a.Length > 3 ? a[3] : 16, a1e = a.Length > 4 ? a[4] : 16, sync = Math.Max(1, a.Length > 7 ? a[7] : 8);
                    for (int b = 0; b < 2; b++) if ((capBit & (1 << b)) != 0)
                        _monFx.Add(new MonFx { Mon = b, Kind = 16, Frames = sync, Keys = new double[] { a1s, a1e } });
                    break;
                }
                case 41:
                    _fadeStart = 1.0; _fadeEnd = 0; _fadeCur = 1.0; _fadeFrames = _fadeFramesLeft = 8;
                    FadeR = FadeG = FadeB = 255;
                    break;
                case 43:
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = 16, Frames = 8, Keys = new double[] { 0, 16 } });
                    break;
                case 79:
                    _monFx.Add(new MonFx { Mon = _dfVis, Kind = 16, Frames = 156, Keys = new double[] { 8, 8 } });
                    break;
                case 56:
                    if (_caps.TryGetValue(0, out var t08cap))
                    { t08cap.ScaleX = t08cap.ScaleY = 1.2; t08cap.TintR = t08cap.TintG = t08cap.TintB = 255; t08cap.TintA = 0.4; }
                    break;
                case 75:
                    if (a.Length > 2 && _caps.TryGetValue(a[2], out var pvCap))
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
                    _caps[0] = new DroppedCap { SrcMon = _atVis };
                    _caps[1] = new DroppedCap { SrcMon = _dfVis };
                    break;
                case 70:
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = 17, Frames = 16 });
                    break;
                case 76:
                    _rasterLeft = Math.Max(1, a.Length > 2 ? a[2] : 60); RasterAmp = 32; RasterPhase = 0;
                    break;
                case 71:
                {
                    int vec = _atVis == 0 ? 1 : -1;
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = 18, Frames = 45,
                        Dx = vec > 0 ? 255 + 80 : 0 - 80, Dy = vec > 0 ? 0 - 80 : 255 + 80 });
                    break;
                }
                case 63:
                {
                    int mode = a.Length > 2 ? a[2] : 0, wait = Math.Max(1, a.Length > 3 ? a[3] : 1);
                    int start = a.Length > 5 ? a[5] : 0, end = a.Length > 6 ? a[6] : 16, col = a.Length > 7 ? a[7] : 0;
                    int capId = CapIdFromToolFlag(mode);
                    DroppedCap cap = (capId >= 0 && _caps.TryGetValue(capId, out var dc)) ? dc : null;
                    _monFx.Add(new MonFx { Mon = capId >= 0 ? 0 : Math.Max(0, MonFromFlag(a, 2)), Cap = cap, Kind = 12, Frames = wait,
                        Keys = new double[] { start, end }, R = R5(col), G = G5(col), B = B5(col) });
                    break;
                }
                case 73:
                {
                    var sim = FindEmitter(a.Length > 2 ? a[2] : 0);
                    double monY73 = (a.Length > 3 && a[3] == 0) ? _atY : _dfY;
                    int mode73 = a.Length > 4 ? a[4] : 0;
                    int time73 = Math.Max(1, a.Length > 5 ? a[5] : 16);
                    int wait73 = a.Length > 6 ? Math.Max(0, a[6]) : 0;
                    double amp73 = monY73 + 60;
                    sim?.SetEmitterMotion(f =>
                    {
                        double t = Math.Clamp((f - wait73) / (double)time73, 0, 1);
                        return (0, mode73 == 0 ? amp73 * (1 - t) : amp73 * t);
                    });
                    break;
                }
                case 69:
                {
                    int capId = a.Length > 2 ? a[2] : 0, add = a.Length > 3 ? a[3] : 1, hs = a.Length > 4 ? a[4] : 0;
                    double end = add < 0 ? 0 : 15;
                    int frames = Math.Max(1, (int)Math.Ceiling(Math.Abs(end - hs) / Math.Max(1, Math.Abs(add))));
                    DroppedCap cap = _caps.TryGetValue(capId, out var dc) ? dc : null;
                    _monFx.Add(new MonFx { Mon = cap != null ? cap.SrcMon : (capId & 1), Cap = cap, Kind = 11, Frames = frames + 1, Keys = new double[] { hs, end, add } });
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
                    _monFx.Add(new MonFx { Mon = mon, Kind = 7, Frames = sync * rotaNum, Keys = new double[] { 16, -4, sync, 8 } });
                    break;
                }
                case FnBlinkAttacker:
                    {
                        int num = Math.Max(1, a.Length > 2 ? a[2] : 1) * 2, wait = Math.Max(1, a.Length > 3 ? a[3] : 1);
                        _monFx.Add(new MonFx { Mon = _atVis, Kind = 2, UpF = wait, Cycles = num, Frames = num * wait });
                    }
                    break;
                case FnFadeBattlerSprite when a.Length >= 6:
                {
                    int fadeWait = Math.Max(0, a[3]), count = Math.Max(1, a[4]), col = a[5];
                    int evyMax = a.Length > 6 ? a[6] : 8, wait = a.Length > 7 ? a[7] : 0;
                    int cyc = Math.Max(1, 2 * fadeWait + wait);
                    _monFx.Add(new MonFx { Mon = Math.Max(0, MonFromFlag(a, 2)), Kind = 6, Frames = count * cyc,
                        UpF = fadeWait, WaitF = wait, Cycles = count, Keys = new[] { (double)evyMax },
                        R = R5(col), G = G5(col), B = B5(col) });
                    break;
                }

                case FnRevolveEmitter when a.Length >= 10:
                {
                    var sim = FindEmitter(a[2]);
                    if (sim == null) break;
                    double radSx = a[3], radEx = a[4], radSy = a[5], radEy = a[6];
                    double rx = a[7], ry = a[8];
                    int wait = Math.Max(1, a[9]);
                    int rotTgt = a.Length > 10 ? a[10] : 0;
                    if (a.Length > 11 && a[11] != 0)
                        Note("This move swings a second set of particles, which the preview does not show.");
                    double rtX = rotTgt == 0 ? _atX : _dfX, rtY = rotTgt == 0 ? _atY : _dfY;
                    double shX = rtX - sim.AnchorX, shY = sim.AnchorY - rtY;
                    sim.SetEmitterMotion(f =>
                    {
                        double t = Math.Min(1.0, (double)f / wait);
                        double angX = (radSx + (radEx - radSx) * t) * Math.PI / 180.0;
                        double angY = (radSy + (radEy - radSy) * t) * Math.PI / 180.0;
                        return (shX + rx * Math.Sin(angX), shY + ry * Math.Cos(angY));
                    });
                    break;
                }

                case FnMoveEmitterA2BLinear when a.Length >= 7:
                case FnMoveEmitterA2BParabolic when a.Length >= 7:
                {
                    var sim = FindEmitter(a[2]);
                    if (sim == null) break;
                    int time = Math.Max(1, a[6]);
                    double height = a[7];
                    int target = a.Length > 8 ? a[8] : 0;
                    double sX = target == 0 ? _atX : _dfX, sY = target == 0 ? _atY : _dfY;
                    double eX = target == 0 ? _dfX : _atX, eY = target == 0 ? _dfY : _atY;
                    double ddx = eX - sX, ddy = eY - sY;
                    bool arc = fn == FnMoveEmitterA2BParabolic;
                    sim.SetEmitterMotion(f =>
                    {
                        double t = Math.Min(1.0, (double)f / time);
                        double screenY = ddy * t + (arc ? -height * 4 * t * (1 - t) : 0);
                        return (ddx * t, -screenY);
                    });
                    break;
                }
            }
        }

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
            _monFx.Add(new MonFx { Mon = mon, Frames = wait, Kind = 4, Dx = ofsx * sign, Dy = ofsy * sign });
        }

        private void AddGhost(int mon, double dx, double alpha, byte g)
            => _ghosts.Add(new MonGhost { Mon = mon, Dx = dx, Dy = 0, ScaleX = 1, ScaleY = 1, Alpha = alpha,
                                          TintR = g, TintG = g, TintB = g, TintA = 0.7 });

        private void AddScaleSeq(int mon, double[][] phases, int repeat = 1)
        {
            if (mon < 0 || phases == null || phases.Length == 0) return;
            if (repeat < 1) repeat = 1;
            var seq = new double[phases.Length * repeat][];
            for (int r = 0; r < repeat; r++) for (int i = 0; i < phases.Length; i++) seq[r * phases.Length + i] = phases[i];
            int total = 0; foreach (var p in seq) total += Math.Max(1, (int)p[4]);
            _monFx.Add(new MonFx { Mon = mon, Kind = 8, Phases = seq, Frames = total });
        }

        private static byte R5(int c) => (byte)((c & 0x1F) << 3);
        private static byte G5(int c) => (byte)(((c >> 5) & 0x1F) << 3);
        private static byte B5(int c) => (byte)(((c >> 10) & 0x1F) << 3);

        private List<int> TargetsFromFlags(int flag) => BattleAnimTargetFlags.Targets(flag, _atVis, _dfVis);

        private int MonFromFlag(int[] a, int idx)
        {
            int flag = idx < a.Length ? a[idx] : AnimDefender;
            var t = TargetsFromFlags(flag);
            return t.Count > 0 ? t[0] : -1;
        }

        private void AddSpriteActor(int idOrCap, bool withCallback, int[] gp)
        {
            var seqs = CellSeqs;
            if (seqs.Length == 0) return;
            if (withCallback)
            {
                var actor = new CellActor(seqs, 0) { FuncId = idOrCap, Gp = gp ?? Array.Empty<int>(),
                    X = _dfX, Y = _dfY, BaseX = _dfX, BaseY = _dfY, CapId = 0 };
                SetupSpriteActors(actor);
                _spriteActors.Add(actor);
            }
            else
            {
                int seq = (idOrCap >= 0 && idOrCap < seqs.Length) ? idOrCap : 0;
                _spriteActors.Add(new CellActor(seqs, seq) { CapId = idOrCap, X = _dfX, Y = _dfY, BaseX = _dfX, BaseY = _dfY });
            }
        }

        private const int SpriteFuncStringShot = 1, SpriteFuncKinesis = 2, SpriteFuncTrick = 3, SpriteFuncMetronome = 4, SpriteFuncConstrict = 5,
                          SpriteFuncBonemerang = 6, SpriteFuncScaryFace = 7, SpriteFuncForesight = 8, SpriteFuncLockOn = 9, SpriteFuncSwaggerPart = 10,
                          SpriteFuncMeanLook = 11, SpriteFuncTorment = 12, SpriteFuncBatonPass = 13, SpriteFuncIcicleSpear = 17, SpriteFuncMetalClaw = 22,
                          SpriteFuncFree = 25, SpriteFuncFollowMe = 26, SpriteFuncFissure = 27, SpriteFuncTaunt = 19,
                          SpriteFuncGrudge = 15, SpriteFuncGrassWhistle = 16, SpriteFuncHelpingHand = 20, SpriteFuncAssist = 21, SpriteFuncFrenzyPlant = 24,
                          SpriteFuncIngrain = 23, SpriteFuncImprison = 14, SpriteFuncFakeOut = 18;
        private static readonly int[][] BindingBandWaitSteps = { new[] { 8, 2 }, new[] { 13, 1 }, new[] { 18, 3 } };

        private void SetupSpriteActors(CellActor leader)
        {
            switch (leader.FuncId)
            {
                case SpriteFuncSwaggerPart:
                    leader.Visible = false;
                    break;
                case SpriteFuncBatonPass:
                    leader.X = leader.BaseX = _atX; leader.Y = leader.BaseY = _atY;
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = 25, Frames = 48 });
                    break;
                case SpriteFuncFree:
                    if (leader.Gp.Length >= 2) { leader.X += leader.Gp[0]; leader.Y += leader.Gp[1]; leader.BaseX = leader.X; leader.BaseY = leader.Y; }
                    break;
                case SpriteFuncHelpingHand:
                {
                    int[] seqs = { 0, 0, 1, 1, 2, 3 };
                    double[] xs = { -32, 32, -32, 32, -32, 32 }, ys = { -24, -24, 24, 24, 0, 0 };
                    for (int i = 0; i < 6; i++)
                    {
                        var c = i == 0 ? leader : new CellActor(CellSeqs, 0) { FuncId = SpriteFuncHelpingHand };
                        c.CapId = i; c.X = 128 + xs[i]; c.Y = 80 + ys[i]; c.BaseX = c.X; c.BaseY = c.Y;
                        c.FlipH = (i == 0 || i == 3); if (c.SeqCount > seqs[i]) c.SetSeq(seqs[i]);
                        if (i != 0) _spriteActors.Add(c);
                    }
                    break;
                }
                case SpriteFuncIngrain:
                {
                    double py = _attackerIsEnemy ? 84 : 140;
                    double[] xo = { -24, -8, 8, 24 }; bool[] fl = { false, true, true, false };
                    for (int i = 0; i < 4; i++)
                    {
                        var c = i == 0 ? leader : new CellActor(CellSeqs, 0) { FuncId = SpriteFuncIngrain };
                        c.CapId = i; c.X = _atX + xo[i]; c.Y = py; c.BaseX = c.X; c.BaseY = py; c.FlipH = fl[i]; c.Visible = false;
                        if (i != 0) _spriteActors.Add(c);
                    }
                    break;
                }
                case SpriteFuncAssist:
                {
                    for (int i = 0; i < 12; i++)
                    {
                        var c = i == 0 ? leader : new CellActor(CellSeqs, 0) { FuncId = SpriteFuncAssist };
                        c.CapId = i; c.X = 40 + (i * 53 % 180); c.Y = 30 + (i * 37 % 120); c.BaseX = c.X; c.BaseY = c.Y; c.Visible = false;
                        if (i != 0) _spriteActors.Add(c);
                    }
                    break;
                }
                case SpriteFuncFrenzyPlant:
                {
                    for (int i = 0; i < 8; i++)
                    {
                        double f = i / 7.0, x = _atX + (_dfX - _atX) * f, y = _atY + (_dfY - _atY) * f;
                        var c = i == 0 ? leader : new CellActor(CellSeqs, 0) { FuncId = SpriteFuncFrenzyPlant };
                        c.CapId = i; c.X = x; c.Y = y; c.BaseX = x; c.BaseY = y; c.FlipH = (i & 1) != 0; c.Visible = false;
                        if (i != 0) _spriteActors.Add(c);
                    }
                    break;
                }
                case SpriteFuncGrassWhistle:
                {
                    leader.BaseX = _atX; leader.BaseY = _atY; leader.X = _atX; leader.Y = _atY; leader.Visible = false;
                    for (int i = 1; i < 15; i++)
                    {
                        var c = new CellActor(CellSeqs, 0) { FuncId = SpriteFuncGrassWhistle, CapId = i, X = _atX, Y = _atY, BaseX = _atX, BaseY = _atY, Visible = false };
                        if (c.SeqCount > 0) c.SetSeq(i % 3);
                        _spriteActors.Add(c);
                    }
                    if (leader.SeqCount > 0) leader.SetSeq(0);
                    break;
                }
                case SpriteFuncGrudge:
                {
                    leader.BaseX = _atX; leader.BaseY = _atY; leader.X = _atX; leader.Y = _atY; leader.Visible = false;
                    for (int i = 1; i < 6; i++)
                        _spriteActors.Add(new CellActor(CellSeqs, 0) { FuncId = SpriteFuncGrudge, CapId = i, X = _atX, Y = _atY, BaseX = _atX, BaseY = _atY, Visible = false });
                    break;
                }
                case SpriteFuncTaunt:
                {
                    leader.X = leader.BaseX = 128; leader.Y = leader.BaseY = 80;
                    if (leader.SeqCount > 1 && _attackerIsEnemy) leader.SetSeq(1);
                    leader.Alpha = 0.5;
                    break;
                }
                case SpriteFuncFissure:
                    leader.X = leader.BaseX = _dfX;
                    leader.Y = leader.BaseY = _attackerIsEnemy ? 126 : 32;
                    if (leader.SeqCount > 1) leader.SetSeq(_attackerIsEnemy ? 1 : 0);
                    break;
                case SpriteFuncTorment:
                {
                    for (int i = 0; i < 6; i++)
                    {
                        double ang = (i / 2) * 30.0 * Math.PI / 180.0, cxo = Math.Cos(ang) * 48, cyo = Math.Sin(ang) * 48;
                        bool right = (i % 2) == 0;
                        double x = _atX + (right ? cxo : -cxo), y = _atY - cyo;
                        if (i == 0) { leader.X = x; leader.Y = y; leader.FlipH = true; leader.CapId = 0; leader.Visible = false; }
                        else _spriteActors.Add(new CellActor(CellSeqs, 0) { FuncId = SpriteFuncTorment, CapId = i, X = x, Y = y, FlipH = right, Visible = false });
                    }
                    break;
                }
                case SpriteFuncMetronome:
                {
                    double vec = _attackerIsEnemy ? -1 : 1;
                    leader.BaseX = _atX; leader.BaseY = _atY; leader.X = _atX + 40 * vec; leader.Y = _atY;
                    leader.ScaleX = leader.ScaleY = 0.1;
                    if (_attackerIsEnemy && leader.SeqCount > 1) leader.SetSeq(1);
                    break;
                }
                case SpriteFuncConstrict:
                {
                    leader.X = _dfX; leader.Y = _dfY + 16; leader.CapId = 0;
                    for (int i = 1; i < 4; i++)
                        _spriteActors.Add(new CellActor(CellSeqs, 0) { FuncId = SpriteFuncConstrict, CapId = i,
                            X = _dfX, Y = _dfY + 16 - i * 10, FlipH = (i & 1) != 0, BaseX = _dfX, BaseY = _dfY });
                    break;
                }
                case SpriteFuncBonemerang:
                    leader.BaseX = _atX; leader.BaseY = _atY; leader.X = _atX; leader.Y = _atY;
                    break;
                case SpriteFuncKinesis:
                {
                    leader.BaseX = _atX; leader.BaseY = _atY; leader.X = _atX; leader.Y = _atY; leader.Alpha = 0;
                    for (int i = 1; i <= 2; i++)
                        _spriteActors.Add(new CellActor(CellSeqs, 0) { FuncId = SpriteFuncKinesis, CapId = i,
                            X = _atX, Y = _atY, BaseX = _atX, BaseY = _atY, Alpha = 0, Visible = false });
                    break;
                }
                case SpriteFuncImprison:
                {
                    leader.BaseX = _dfX; leader.BaseY = _dfY; leader.X = _dfX; leader.Y = _dfY; leader.CapId = 0; leader.Visible = false;
                    if (leader.SeqCount > 1) leader.SetSeq(1);
                    for (int i = 1; i <= 2; i++)
                        _spriteActors.Add(new CellActor(CellSeqs, 0) { FuncId = SpriteFuncImprison, CapId = i,
                            X = _dfX, Y = _dfY, BaseX = _dfX, BaseY = _dfY, Visible = false });
                    _monFx.Add(new MonFx { Mon = _dfVis, Kind = 5, Sh = new Shake(4, 0, 1, 6), NumMax = 0, Delay = 10 });
                    break;
                }
                case SpriteFuncScaryFace:
                {
                    double vec = _attackerIsEnemy ? -1 : 1;
                    leader.BaseX = _atX; leader.BaseY = _atY;
                    leader.X = _atX + 32 * vec; leader.Y = _atY; leader.ScaleX = leader.ScaleY = 0.5;
                    break;
                }
                case SpriteFuncTrick:
                    leader.X = leader.BaseX = 100; leader.Y = leader.BaseY = 54; leader.CapId = 0;
                    _spriteActors.Add(new CellActor(CellSeqs, 0) { FuncId = SpriteFuncTrick, CapId = 1, X = 180, Y = 39, BaseX = 180, BaseY = 39 });
                    break;
                case SpriteFuncMetalClaw:
                {
                    leader.X = _dfX - 32; leader.Y = _dfY; leader.FlipH = true; leader.CapId = 0;
                    (int dx, int dy, bool flip)[] p = { (-32, 32, true), (32, 0, false), (32, 32, false) };
                    for (int i = 0; i < 3; i++)
                        _spriteActors.Add(new CellActor(CellSeqs, 0) { FuncId = SpriteFuncMetalClaw, CapId = i + 1,
                            X = _dfX + p[i].dx, Y = _dfY + p[i].dy, FlipH = p[i].flip, BaseX = _dfX, BaseY = _dfY });
                    break;
                }
                case SpriteFuncStringShot:
                {
                    int n = leader.Gp.Length > 0 ? Math.Max(1, leader.Gp[0]) : 1;
                    leader.Y = leader.BaseY + 32;
                    for (int i = 1; i < n; i++)
                        _spriteActors.Add(new CellActor(CellSeqs, 0) { FuncId = SpriteFuncStringShot, Gp = leader.Gp,
                            CapId = i, X = _dfX, Y = _dfY + (32 - i * 4), BaseX = _dfX, BaseY = _dfY });
                    break;
                }
            }
        }

        private void RunSpriteFunc(CellActor a)
        {
            switch (a.FuncId)
            {
                case SpriteFuncSwaggerPart: Drive207Sub(a); break;
                case SpriteFuncStringShot: Drive081(a); break;
                case SpriteFuncIcicleSpear: Drive333(a); break;
                case SpriteFuncMetalClaw: Drive232(a); break;
                case SpriteFuncTrick: Drive271(a); break;
                case SpriteFuncScaryFace: Drive184(a); break;
                case SpriteFuncKinesis: Drive134(a); break;
                case SpriteFuncImprison: Drive286(a); break;
                case SpriteFuncMetronome: Drive118(a); break;
                case SpriteFuncConstrict: Drive132(a); break;
                case SpriteFuncBonemerang: Drive155(a); break;
                case SpriteFuncForesight: Drive193(a); break;
                case SpriteFuncLockOn: Drive199(a); break;
                case SpriteFuncMeanLook: Drive212(a); break;
                case SpriteFuncTorment: Drive259(a); break;
                case SpriteFuncFollowMe: Drive266(a); break;
                case SpriteFuncTaunt: Drive269(a); break;
                case SpriteFuncFakeOut: Drive252(a); break;
                case SpriteFuncBatonPass: Drive226(a); break;
                case SpriteFuncGrassWhistle: DriveFloat(a, 3, 0.8, 1.2, 40); break;
                case SpriteFuncGrudge: DriveFloat(a, 4, 1.0, 0.8, 44); break;
                case SpriteFuncHelpingHand: DriveAppearHoldFade(a, a.CapId * 5, 40, 12); break;
                case SpriteFuncAssist: DriveAppearHoldFade(a, a.CapId * 2, 26 + a.CapId, 10); break;
                case SpriteFuncFrenzyPlant: DriveAppearHoldFade(a, a.CapId * 2, 48, 12); break;
                case SpriteFuncIngrain: DriveAppearHoldFade(a, a.CapId * 5, 44, 12); break;
            }
        }

        private void Drive226(CellActor a)
        {
            int t = a.Age;
            a.Visible = true;
            if (t == 24 && a.Seq != 1 && a.SeqCount > 1) a.SetSeq(1);
            if (t >= 40 && t < 48) a.Y = a.BaseY * (1.0 - (t - 40) / 8.0);
            else if (t >= 48) { a.Visible = false; a.Alive = false; }
        }

        private void Drive252(CellActor a)
        {
            const int FadeIn = 6, FadeOut = 6, MaxHold = 40; const double Peak = 0.6;
            a.Visible = true;
            if (a.Age < FadeIn) { a.Alpha = Peak * (a.Age + 1) / FadeIn; return; }
            if (!a.Finished && a.Age < MaxHold) { a.Alpha = Peak; return; }
            a.Alpha -= Peak / FadeOut;
            if (a.Alpha <= 0) { a.Alpha = 0; a.Visible = false; a.Alive = false; }
        }

        private void DriveAppearHoldFade(CellActor a, int delay, int hold, int fade)
        {
            int t = a.Age - delay;
            if (t < 0) { a.Visible = false; return; }
            a.Visible = true;
            if (t >= hold && t < hold + fade) a.Alpha = 1.0 - (t - hold) / (double)fade;
            else if (t >= hold + fade) a.Visible = false;
        }

        private void DriveFloat(CellActor a, int stag, double xs, double ys, int life)
        {
            int t = a.Age - a.CapId * stag;
            if (t < 0) { a.Visible = false; return; }
            a.Visible = true;
            double vec = _attackerIsEnemy ? -1 : 1;
            a.X = a.BaseX + vec * t * xs + 8 * Math.Sin(t * 0.3 + a.CapId);
            a.Y = a.BaseY - t * ys;
            if (t >= life) a.Visible = false;
            else if (t > life - 12) a.Alpha = (life - t) / 12.0;
        }

        private void Drive266(CellActor a)
        {
            int t = a.Age; const int Sway = 42, Rot = 24;
            if (t < Sway) a.X = a.BaseX + 40 * Math.Sin(t / (double)Sway * Math.PI * 1.5);
            else if (t < Sway + Rot) { a.X = a.BaseX; a.ExtraRotDeg = 20 * Math.Sin((t - Sway) / 4.0 * Math.PI); }
            else a.Visible = false;
        }

        private void Drive212(CellActor a)
        {
            int t = a.Age; const int Hold = 40, Fade = 24;
            if (t < Hold) { a.ScaleX = a.ScaleY = 1.5; }
            else if (t < Hold + Fade) { double k = (t - Hold) / (double)Fade; a.ScaleX = a.ScaleY = 1.5 - 0.5 * k; a.Alpha = 1 - k; }
            else a.Visible = false;
        }

        private void Drive259(CellActor a)
        {
            int t = a.Age, appear = a.CapId * 4; const int Hold = 40, Fade = 12;
            if (t < appear) { a.Visible = false; return; }
            a.Visible = true;
            int u = t - appear;
            if (u >= Hold && u < Hold + Fade) a.Alpha = 1 - (u - Hold) / (double)Fade;
            else if (u >= Hold + Fade) a.Visible = false;
        }

        private void Drive199(CellActor a)
        {
            int t = a.Age; const int Anim = 24, Flash = 8, Blink = 4 * 8;
            if (t < Anim + Flash) a.Visible = true;
            else if (t < Anim + Flash + Blink) a.Visible = ((t - Anim - Flash) / 4) % 2 == 0;
            else a.Visible = false;
        }

        private static readonly (double x, double y)[] We193Pts =
            { (0, 0), (40, 40), (40, -40), (-40, 40), (-40, -40), (40, 40), (0, 0) };
        private void Drive193(CellActor a)
        {
            int t = a.Age; const int Seg = 12, Move = 8, Count = 6, Fade = 16;
            if (t < Seg * Count)
            {
                int s = t / Seg; double f = Math.Min(1.0, (t % Seg) / (double)Move);
                a.X = a.BaseX + We193Pts[s].x + (We193Pts[s + 1].x - We193Pts[s].x) * f;
                a.Y = a.BaseY + We193Pts[s].y + (We193Pts[s + 1].y - We193Pts[s].y) * f;
            }
            else
            {
                int ft = t - Seg * Count;
                a.X = a.BaseX; a.Y = a.BaseY; a.Alpha = Math.Max(0, 1 - ft / (double)Fade);
                MonTintA[_dfVis] = 10 / 16.0 * Math.Sin(Math.Min(1.0, ft / (double)Fade) * Math.PI);
                TintR = TintG = TintB = 255;
                if (ft >= Fade) a.Visible = false;
            }
        }

        private void Drive118(CellActor a)
        {
            int t = a.Age; const int In = 8, Wag = 32, Out = 8;
            double centre = _attackerIsEnemy ? 20 : -20;
            if (t < In) a.ScaleX = a.ScaleY = 0.1 + 0.9 * (t / (double)In);
            else if (t < In + Wag) { a.ScaleX = a.ScaleY = 1.0; a.ExtraRotDeg = centre + 20 * Math.Sin((t - In) / 4.0 * Math.PI); }
            else if (t < In + Wag + Out) { a.ExtraRotDeg = 0; a.ScaleX = a.ScaleY = 1.0 - 0.9 * ((t - In - Wag) / (double)Out); }
            else a.Visible = false;
        }

        private void Drive132(CellActor a)
        {
            int t = a.Age; int appear = a.CapId * 4; const int AllIn = 16, Squeeze = 48;
            if (t < appear) { a.Visible = false; return; }
            a.Visible = true;
            if (t >= AllIn && t < AllIn + Squeeze)
            {
                a.ScaleX = 1.0 - 0.2 * Math.Abs(Math.Sin((t - AllIn) / 8.0 * Math.PI));
                if (a.CapId == 0 && t < AllIn + 8) { MonShakeX[_dfVis] = (t % 2 == 0 ? 4 : -4); }
            }
            else if (t >= AllIn + Squeeze) a.Visible = false;
        }

        private void Drive155(CellActor a)
        {
            int t = a.Age; const int Leg = 10;
            a.ExtraRotDeg = t * 30;
            if (t < Leg) { double f = t / (double)Leg; a.X = a.BaseX + (_dfX - a.BaseX) * f; a.Y = a.BaseY + (_dfY - a.BaseY) * f - 32 * 4 * f * (1 - f); }
            else if (t < 2 * Leg) { double f = (t - Leg) / (double)Leg; a.X = _dfX + (a.BaseX - _dfX) * f; a.Y = _dfY + (a.BaseY - _dfY) * f - 32 * 4 * f * (1 - f); }
            else a.Visible = false;
        }

        private void Drive134(CellActor a)
        {
            int t = a.Age - 8 * a.CapId;
            if (t < 0) { a.Visible = false; return; }
            a.Visible = true;
            const int FadeIn = 31, Sweep = 18, Hold = 12, FadeOut = 8;
            double vec = _attackerIsEnemy ? -1 : 1, peak = a.CapId == 0 ? 1.0 : 0.5;
            double sweepT = Math.Min(Sweep, Math.Max(0, t - FadeIn));
            double ang = (90 + 180 * (sweepT / Sweep)) * Math.PI / 180.0;
            a.X = a.BaseX + Math.Sin(ang) * -32 * vec; a.Y = a.BaseY + Math.Cos(ang) * -8;
            if (t < FadeIn) a.Alpha = peak * (t / (double)FadeIn);
            else if (t < FadeIn + Sweep + Hold) a.Alpha = peak;
            else { double f = (t - (FadeIn + Sweep + Hold)) / (double)FadeOut; a.Alpha = peak * (1 - f); if (f >= 1) a.Visible = false; }
        }

        private void Drive286(CellActor a)
        {
            int t = a.Age - 9 * a.CapId;
            if (t < 0) { a.Visible = false; return; }
            a.Visible = true;
            double peak = a.CapId == 0 ? 1.0 : 0.5;
            const int In = 10, Hold = 24, Out = 6;
            if (t < In) { double k = t / (double)In; a.ScaleX = a.ScaleY = 2.5 - 1.5 * k; a.Alpha = peak; }
            else if (t < In + Hold) { a.ScaleX = a.ScaleY = 1.0; a.Alpha = peak; }
            else if (t < In + Hold + Out) { double k = (t - In - Hold) / (double)Out; a.ScaleX = a.ScaleY = 1.0 + 1.5 * k; a.Alpha = peak * (1 - k); }
            else a.Visible = false;
        }

        private void Drive184(CellActor a)
        {
            int t = a.Age; const int Move = 32, Fade = 8;
            double vec = _attackerIsEnemy ? -1 : 1, fx = a.BaseX + 32 * vec, fy = a.BaseY;
            if (t <= Move)
            {
                double k = (double)t / Move;
                a.X = fx + 64 * vec * k; a.Y = fy - 16 * k; a.ScaleX = a.ScaleY = 0.5 + 0.7 * k;
            }
            else if (t <= Move + Fade)
            {
                a.X = fx + 64 * vec; a.Y = fy - 16; a.ScaleX = a.ScaleY = 1.2;
                a.Alpha = 1.0 - (double)(t - Move) / Fade;
            }
            else a.Visible = false;
        }

        private void Drive269(CellActor a)
        {
            a.X = 128; a.Y = 80; a.Alpha = 0.5;
            a.Visible = a.Age < 45;
        }

        private void Drive271(CellActor a)
        {
            int t = a.Age; const int Fall = 25, Orbit = 50;
            double fallenY = a.BaseY + 50;
            if (t < Fall) { a.X = a.BaseX; a.Y = a.BaseY + 2 * t; }
            else if (t < Fall + Orbit)
            {
                double mx = 140, my = (54 + 50 + 39 + 50) / 2.0;
                double ox = a.BaseX - mx, oy = fallenY - my, r = Math.Sqrt(ox * ox + oy * oy), a0 = Math.Atan2(oy, ox);
                double ang = a0 + Math.PI * ((t - Fall) / 10.0);
                a.X = mx + r * Math.Cos(ang); a.Y = my + r * Math.Sin(ang);
            }
            else { double k = Math.Min(1.0, (t - Fall - Orbit) / 8.0); a.Alpha = 1 - k; if (k >= 1) a.Visible = false; }
        }

        private void Drive232(CellActor a)
        {
            if (a.CapId >= 2 && a.Age < 10) { a.Visible = false; return; }
            a.Visible = a.Age < 40;
        }

        private void Drive333(CellActor a)
        {
            int ofsX = a.Gp.Length > 0 ? a.Gp[0] : 0, ofsY = a.Gp.Length > 1 ? a.Gp[1] : 0;
            int time = a.Gp.Length > 2 ? Math.Max(1, a.Gp[2]) : 16, height = a.Gp.Length > 3 ? a.Gp[3] : 0;
            double vec = _attackerIsEnemy ? -1 : 1;
            double sxp = _atX, syp = _atY, exp = _dfX + ofsX * vec, eyp = _dfY + ofsY * vec;
            double frac = Math.Min(1.0, (double)a.Age / time);
            a.X = sxp + (exp - sxp) * frac;
            a.Y = syp + (eyp - syp) * frac - height * 4.0 * frac * (1 - frac);
            double k = Math.Min(1.0, a.Age / 10.0);
            a.ExtraRotDeg = vec > 0 ? (20 + (130 - 20) * k) : -(90 + (130 - 90) * k);
            if (a.Age >= time) a.Visible = false;
        }

        private void Drive207Sub(CellActor a)
        {
            double vec = _attackerIsEnemy ? -1 : 1; int t = a.Age;
            const int Pop = 6, Wait = 4;
            void Scale(int f) => a.ScaleX = a.ScaleY = f < 4 ? 1.0 + 0.4 * (f / 4.0) : 1.4 - 0.2 * ((f - 4) / 2.0);
            if (t < Pop) { a.Visible = true; a.X = a.BaseX + 24 * vec; a.Y = a.BaseY - 16; Scale(t); }
            else if (t < Pop + Wait) a.Visible = false;
            else if (t < Pop + Wait + Pop) { a.Visible = true; a.X = a.BaseX - 24 * vec; a.Y = a.BaseY - 24; Scale(t - Pop - Wait); }
            else a.Visible = false;
        }

        private void Drive081(CellActor a)
        {
            int t = a.Age, idx = Math.Min(a.CapId, BindingBandWaitSteps.Length - 1);
            int delay = BindingBandWaitSteps[idx][0], interval = Math.Max(1, BindingBandWaitSteps[idx][1]);
            const int Eff = 45;
            if (t < Eff) a.Visible = t >= delay && ((t - delay) / interval) % 2 == 0;
            else if (t < Eff + 10) { a.Visible = true; a.ScaleX = 1.0 - 0.4 * ((t - Eff) / 10.0); a.ScaleY = 1.0; }
            else if (t < Eff + 10 + Eff) { a.Visible = true; a.ScaleX = 0.6; }
            else { double k = Math.Min(1.0, (t - (Eff + 10 + Eff)) / 15.0); a.Alpha = 1.0 - k; if (k >= 1) a.Visible = false; }
        }

        private double OffscreenX(int mon) => ((mon == 0 ? _atX : _dfX) < 128 ? OffScreenLeft : OffScreenRight);

        private void UpdateMonFx()
        {
            MonRot[0] = MonRot[1] = 0; MonScaleX[0] = MonScaleX[1] = 1; MonScaleY[0] = MonScaleY[1] = 1;
            MonTintA[0] = MonTintA[1] = 0; MonVisible[0] = MonVisible[1] = true;
            MonShakeX[0] = MonShakeX[1] = 0; MonShakeY[0] = MonShakeY[1] = 0; ShakeX = ShakeY = 0;
            BgFlashAmount = 0;
            MonMosaic[0] = MonMosaic[1] = 0; MonClip[0] = MonClip[1] = 1; MonAlpha[0] = MonAlpha[1] = 1;
            MonWarpMon = -1;
            _ghosts.Clear();

            for (int i = _monFx.Count - 1; i >= 0; i--)
            {
                var fx = _monFx[i];
                if (fx.Delay > 0) { fx.Delay--; continue; }
                if (fx.Kind == 5)
                {
                    if (!fx.Sh.Calc())
                    {
                        if (fx.Rep < fx.NumMax) { fx.Rep++; fx.Sh = new Shake(fx.Sh.AmpX, fx.Sh.AmpY, fx.Sh.Sync, fx.Sh.Num0); }
                        else { _monFx.RemoveAt(i); continue; }
                    }
                    if (fx.ToScene) { ShakeX = fx.Sh.X; ShakeY = fx.Sh.Y; }
                    else { MonShakeX[fx.Mon] = fx.Sh.X; MonShakeY[fx.Mon] = fx.Sh.Y; }
                    continue;
                }
                double t = (double)fx.Frame / Math.Max(1, fx.Frames);
                switch (fx.Kind)
                {
                    case 0: MonRot[fx.Mon] = Math.Sin(t * Math.PI * 2 * 2) * 18.0; break;
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
                    case 2: MonVisible[fx.Mon] = (fx.Frame / Math.Max(1, fx.UpF)) % 2 == 0; break;
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
                        var cap = fx.Cap;
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
                        int[] eqAmp = { 12, 10, 8, 6, 4, 2, 1, 0 };
                        int step = Math.Min(eqAmp.Length - 1, fx.Frame / 5), within = fx.Frame % 5;
                        ShakeX = eqAmp[step] * ((fx.Frame % 2) == 0 ? 1 : -1);
                        if (within < 3)
                        {
                            BgFlashAmount = 10.0 / 16.0;
                            byte c = (byte)((step % 2) == 0 ? 0 : 255);
                            BgFlashR = BgFlashG = BgFlashB = c;
                        }
                        break;
                    }
                    case 7:
                        if (fx.Keys != null && fx.Keys.Length >= 4)
                        {
                            double ang = fx.Frame * 2.0 * Math.PI / Math.Max(1, fx.Keys[2]);
                            MonShakeX[fx.Mon] += fx.Keys[0] * Math.Sin(ang);
                            MonShakeY[fx.Mon] += fx.Keys[3] + fx.Keys[1] * Math.Cos(ang);
                        }
                        break;
                    case 9:
                    {
                        const int osc = 72, range = 32, loopLen = 8;
                        double eva = fx.Frame < osc ? 8 : Math.Max(0, 8 - (fx.Frame - osc));
                        double alpha = eva / 16.0;
                        if (alpha > 0.001)
                        {
                            double ph = (fx.Frame % loopLen) / (double)loopLen;
                            double f = ph < 0.5 ? ph * 2 : 2 - ph * 2;
                            AddGhost(fx.Mon, range * f, alpha, 128); AddGhost(fx.Mon, -range * f, alpha, 128);
                            AddGhost(fx.Mon, range * (1 - f), alpha, 196); AddGhost(fx.Mon, -range * (1 - f), alpha, 196);
                        }
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
                            double evy = fx.Keys[0] + (fx.Keys[1] - fx.Keys[0]) * t;
                            double ta = Math.Clamp(evy / 16.0, 0, 1);
                            if (fx.Cap != null) { fx.Cap.TintA = ta; fx.Cap.TintR = fx.R; fx.Cap.TintG = fx.G; fx.Cap.TintB = fx.B; }
                            else { MonTintA[fx.Mon] = ta; TintR = fx.R; TintG = fx.G; TintB = fx.B; }
                        }
                        break;
                    case 15:
                        MonClip[fx.Mon] = (fx.Dx >= 0 ? 1 : -1) * Math.Clamp(t, 0.0, 1.0);
                        break;
                    case 16:
                        if (fx.Keys != null && fx.Keys.Length >= 2)
                            MonAlpha[fx.Mon] = Math.Clamp((fx.Keys[0] + (fx.Keys[1] - fx.Keys[0]) * t) / 16.0, 0, 1);
                        break;
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
                        _ghosts.Add(new MonGhost { Mon = _atVis, Dx = (_dfX - 32) - _atX, Dy = _dfY - _atY,
                            ScaleX = 1, ScaleY = 1, Alpha = Math.Clamp(t * 2, 0, 1), TintR = 255, TintG = 255, TintB = 255, TintA = 0.5 });
                        break;
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
                        double turns = fx.Cycles > 0 ? fx.Cycles : 1, dir = fx.Dx < 0 ? -1 : 1;
                        (double x, double y) Orbit(int f)
                        {
                            if (f < 0) return (0, 0);
                            double a3 = 2 * Math.PI * turns * (f / (double)Math.Max(1, fx.Frames));
                            return (Math.Sin(a3) * 32 * dir, 8 * (1 - Math.Cos(a3)));
                        }
                        var nowP = fx.Frame >= fx.Frames - 1 ? (0.0, 0.0) : Orbit(fx.Frame);
                        var prevP = Orbit(fx.Frame - 1);
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
                        int phase = Math.Min(2, fx.Frame / 16);
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
                    case 25:
                        if (fx.Frame < 16) { }
                        else if (fx.Frame < 24) { double k = (fx.Frame - 16) / 8.0; MonScaleX[fx.Mon] = MonScaleY[fx.Mon] = 1.0 - k; }
                        else MonVisible[fx.Mon] = false;
                        break;
                    case 14:
                        for (int gi = 0; gi < 4; gi++)
                        {
                            int[] delay = { 2, 7, 13, 18 };
                            int local = fx.Frame - delay[gi];
                            if (local < 0 || local >= 10) continue;
                            double sc = local < 5 ? 1.0 - 0.95 * (local / 5.0) : 0.05 + 0.95 * ((local - 5) / 5.0);
                            _ghosts.Add(new MonGhost { Mon = fx.Mon, Dx = 0, Dy = (1 - sc) * 24, ScaleX = sc, ScaleY = sc,
                                                       Alpha = 0.7, TintR = 128, TintG = 128, TintB = 128, TintA = 0.5 });
                        }
                        break;
                    case 8:
                        if (fx.Phases != null && fx.Phases.Length > 0)
                        {
                            int acc = 0, pi = 0;
                            for (; pi < fx.Phases.Length; pi++) { int fr = Math.Max(1, (int)fx.Phases[pi][4]); if (fx.Frame < acc + fr) break; acc += fr; }
                            if (pi >= fx.Phases.Length) pi = fx.Phases.Length - 1;
                            var ph = fx.Phases[pi]; int dur = Math.Max(1, (int)ph[4]);
                            double k = Math.Clamp((double)(fx.Frame - acc) / dur, 0, 1);
                            double scy = (ph[2] + (ph[3] - ph[2]) * k) / 100.0;
                            MonScaleX[fx.Mon] = (ph[0] + (ph[1] - ph[0]) * k) / 100.0;
                            MonScaleY[fx.Mon] = scy;
                            MonShakeY[fx.Mon] += (1 - scy) * 24;
                        }
                        break;
                }
                if (++fx.Frame >= fx.Frames) _monFx.RemoveAt(i);
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
                case 1: { double t = _cellFrame / 12.0; _cellScaleX = Lerp(1.0, 0.6, t); _cellScaleY = Lerp(0.05, 1.5, t); _cellOpacity = t; if (++_cellFrame >= 12) { _cellPhase = 2; _cellFrame = 0; } break; }
                case 2: _cellScaleX = 0.6; _cellScaleY = 1.5; _cellOpacity = 1; if (++_cellFrame >= 4) { _cellPhase = 3; _cellFrame = 0; } break;
                case 3: { double t = _cellFrame / 12.0; _cellScaleX = Lerp(0.6, 1.5, t); _cellScaleY = Lerp(1.5, 0.1, t); _cellOpacity = 1 - t; if (++_cellFrame >= 12) { _cellPhase = -1; _cellOpacity = 0; } break; }
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
            var img = _bgRenderer.Build(bgId, reverse: _attackerIsEnemy);
            if (img == null) return;
            _bgRgba = img.Rgba; _bgW = img.Width; _bgH = img.Height;
            _bgWrapW = overlay ? FX_BG_WRAP : _bgW;
            _bgWrapH = overlay ? FX_BG_WRAP : _bgH;
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
            var wb = new WriteableBitmap(new global::Avalonia.PixelSize(W, H), new global::Avalonia.Vector(96, 96),
                global::Avalonia.Platform.PixelFormat.Bgra8888, global::Avalonia.Platform.AlphaFormat.Premul);
            using (var fb = wb.Lock())
            {
                int rb = fb.RowBytes;
                if (rb == W * 4) System.Runtime.InteropServices.Marshal.Copy(_bgBuf, 0, fb.Address, _bgBuf.Length);
                else for (int y = 0; y < H; y++) System.Runtime.InteropServices.Marshal.Copy(_bgBuf, y * W * 4, fb.Address + y * rb, W * 4);
            }
            return wb;
        }

        private void UpdateFade()
        {
            if (_fadeFramesLeft <= 0) return;
            _fadeFramesLeft--;
            double t = _fadeFrames <= 0 ? 1 : 1.0 - (double)_fadeFramesLeft / _fadeFrames;
            _fadeCur = _fadeStart + (_fadeEnd - _fadeStart) * t;
        }

        private static double ZOfVis(int vis) => vis == 1 ? -5248.0 / 172.0 : 64.0 / 172.0;

        private (double cx, double cy, double ax, double ay, double z) Place(int callback, int sepIndex, int sepCount)
        {
            double dx = _dfX - _atX, dy = _dfY - _atY;
            double len = Math.Sqrt(dx * dx + dy * dy); if (len > 0) { dx /= len; dy /= len; }
            switch (callback)
            {
                case 18:
                    return (PARTICLE_ORIGIN_X, PARTICLE_ORIGIN_Y, 0, 0, 0);
                case 0:
                    return (PARTICLE_ORIGIN_X, PARTICLE_ORIGIN_Y, 0, 0, 0);
                case 1: case 3: case 19: case 21:
                    return (_atX, _atY, 0, 0, ZOfVis(_atVis));
                case 5: case 7: case 8: case 9: case 10: case 11:
                case 12: case 13: case 14: case 15: case 16:
                    return (_atX, _atY, dx, dy, ZOfVis(_atVis));
                case 6:
                    return (_dfX, _dfY, -dx, -dy, ZOfVis(_dfVis));
                default:
                    return (_dfX, _dfY, 0, 0, ZOfVis(_dfVis));
            }
        }
    }
}
