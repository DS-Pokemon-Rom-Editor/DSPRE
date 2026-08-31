using System;
using System.Collections.Generic;
using global::Avalonia.Media.Imaging;
using DSPRE.Avalonia.Data;

namespace DSPRE.Avalonia
{
    /// <summary>
    /// Faithfully runs a WEST move-effect script on a frame timeline (mirroring the in-game interpreter): it walks
    /// the commands, advancing frames on WAIT / waiting on WAIT_PARTICLE, spawning emitters (LOAD/ADD_PARTICLE) at
    /// the right moment via <see cref="SpaSimulator"/>, and triggering the hardcoded routines we can reproduce:
    /// screen shake (WT_SHAKE), background palette-fade (HAIKEI_PAL_FADE) and Pokémon flash (SSP_POKE_PAL_FADE).
    /// Each <see cref="Step"/> = one 1/60 s frame; <see cref="RenderFrame"/> draws the live particle layer, and
    /// <see cref="ShakeX"/>/<see cref="ShakeY"/>/<see cref="FadeOpacity"/> drive the scene transform + overlay.
    /// </summary>
    public sealed class WestPlayer
    {
        // Pokémon particle-coordinate positions → screen px: x = plcd/172 + 120, y = 96 − plcd/172.
        public static (double x, double y) AttackerScreen => ToScreen(-15360, -6272);   // client A (player)
        public static (double x, double y) DefenderScreen => ToScreen(+13568, +2944);   // client B (enemy)
        private static (double x, double y) ToScreen(double px, double py) => (px / 172.0 + 120.0, 96.0 - py / 172.0);

        // WEST_SP_* function ids (order, verified)
        private const int FN_POKEROTA = 4, FN_HAIKEI_PAL_FADE = 33, FN_SSP_POKE_PAL_FADE = 34,
                          FN_CAP_POKE_SCALE = 35, FN_WT_SHAKE = 36, FN_POKE_VANISH = 40, FN_SSP_POKE_SCALE = 42,
                          FN_BG_SHAKE = 68,
                          FN_EMIT_STRAIGHT = 65, FN_EMIT_PARABOLIC = 66, FN_EMIT_ROTATION = 72;
        // The WE_T* linear-slide "move the Pokémon" primitives (the straight-line sync-move helper): T04(51) back-attack,
        // T05(52)/T06(53)/T07(54) return-to-pos, T10(57). NOTE: 50=T03 is a BLINK and 56=T08 is an AURA, NOT moves,
        // handled separately. 44/45 (WE_T02/T22) scroll the background, also separate.
        private static readonly HashSet<int> FN_WE_MOVE = new HashSet<int> { 51, 52, 53, 54, 57 };
        private const int FN_WE_T03 = 50;   // blink (toggles the attacker's visibility)
        // Position tools: slide the SSP mon off-screen / back / to default pos.
        // Function ids from the real enum (table index = id): DISP_OUT=61, DISP_DEF=62, PALCOL_CHANGE=74,
        // DISP_MOVE=77.
        private const int FN_DISP_OUT = 61, FN_DISP_DEF = 62, FN_DISP_MOVE = 77, FN_PALCOL_CHANGE = 74;
        private const int WIN_OSX = -80, WIN_OEX = 256 + 80;   // off-screen X (WIN_OSX/WIN_OEX)
        private const int FN_WE_T02 = 44, FN_WE_T22 = 45;   // background-scroll routines
        private const int FN_KAITEN = 60;   // mon traces an ellipse (dizzy/spin)
        // WE_TOOL flags: which Pokémon a routine targets.
        // Which Pokemon a routine acts on, from we_def.h:137-159. The names are relative, not sides:
        // WT_SSPointerGet (we_tool.c:1431) resolves M1 to the ATTACKER and E1 to the DEFENDER, M2 and E2
        // to their allies, and it only looks those up in a double battle. So in a single battle an M2 or
        // E2 target finds nobody and the routine does nothing at all.
        private const int WE_TOOL_M1 = 0x0002, WE_TOOL_M2 = 0x0004, WE_TOOL_E1 = 0x0008, WE_TOOL_E2 = 0x0010,
                          WE_TOOL_OTHER = 0x0020, WE_TOOL_STAGE = 0x0040, WE_TOOL_BG = 0x0400;
        // EMTFUNC_FIELD_OPERATOR, projectile/operator callback (uses the following EX_DATA).
        private const int EMTFUNC_FIELD_OPERATOR = 17;

        // Per-Pokémon sprite transforms (index 0 = attacker/player, 1 = defender/enemy) the routines animate; the
        // view binds these to the Shuckle RenderTransforms + tint overlays. (TCBs set ROT_Z / AFF / VANISH / palette.)
        public readonly double[] MonDX = { 0, 0 };   // persistent position offset (WE_T10 lunge), NOT reset per frame
        public readonly double[] MonDY = { 0, 0 };
        public readonly double[] MonRot = { 0, 0 };
        public readonly double[] MonScaleX = { 1, 1 };   // independent X/Y so squash/stretch (Stomp flatten) works
        public readonly double[] MonScaleY = { 1, 1 };
        public readonly double[] MonTintA = { 0, 0 };
        public readonly bool[] MonVisible = { true, true };
        private readonly bool[] _monVanish = { false, false };   // persistent hide until shown again
        public byte TintR { get; private set; } = 0;
        public byte TintG { get; private set; } = 0;
        public byte TintB { get; private set; } = 0;

        private sealed class MonFx { public int Mon, Frame, Frames, Kind; public byte R, G, B; public double Dx, Dy; public double[] Keys;
                                     public int UpF, WaitF, DownF, Cycles;     // scale-updown timing (Kind 1)
                                     public int Delay;                         // frames to wait before this effect starts (sequencing)
                                     public Shake Sh; public int NumMax, Rep; public bool ToScene;   // shake (Kind 5)
                                     public double[][] Phases;     // Kind 8: scale-keyframe sequence [sxStart,sxEnd,syStart,syEnd,frames] (/100)
                                     public DroppedCap Cap; }      // when set, the fx drives a POKEOAM_DROP cap, not the live mon
        private readonly List<MonFx> _monFx = new List<MonFx>();

        private readonly List<WazaSeqCommand> _cmds;
        private readonly WazaSeqVersion _version;
        private readonly ScriptNarc _particleNarc;
        private readonly double _atX, _atY, _dfX, _dfY;
        private readonly SpaParticlePreview _renderer;
        private readonly Dictionary<int, SpaArchive> _archives = new Dictionary<int, SpaArchive>();
        private readonly Dictionary<int, int> _slot = new Dictionary<int, int>();   // ptc_no → data_no
        private readonly Dictionary<int, SpaSimulator> _emitSlots = new Dictionary<int, SpaSimulator>();  // emit_no → sim
        private readonly Dictionary<int, List<SpaSimulator>> _ptcSims = new Dictionary<int, List<SpaSimulator>>();  // ptc_no → its sims (EXIT_PARTICLE)
        private SpaSimulator _lastSim;   // for EMIT_* that don't match a slot (fall back to the last emitter)

        // WEST_LOOP_LABEL/WEST_LOOP, a stack of active loop frames; WEST_SEQ_CALL/WEST_END_CALL, a
        // return-address stack. Both mirror the in-game fixed arrays but a stack suffices (and supports nesting).
        private sealed class LoopFrame { public int Body, Total, Count; }
        private readonly List<LoopFrame> _loops = new List<LoopFrame>();
        private readonly List<int> _callStack = new List<int>();

        private int _pc, _wait;
        private bool _scriptDone, _waitParticles, _waitFlag;
        private int _bgWait;   // HAIKEI_*_WAIT: 0 = none, 1 = block until the BG change fully settles, 2 = until half-faded
        private int _guard;

        // Shake. WT_SHAKE shakes the mode-selected MON sprite via MonShakeX/Y; BG_SHAKE
        // and WE_TOOL_BG shakes scroll the whole BG frame → ShakeX/Y. Both transient (reset/frame).
        public readonly double[] MonShakeX = { 0, 0 };
        public readonly double[] MonShakeY = { 0, 0 };
        public readonly double[] MonMosaic = { 0, 0 };   // the mosaic-level handler: OBJ mosaic level 0..15 (block = level+1) per mon
        // RECT_VIEW vertical wipe (SoftSpriteVisibleSet): visible fraction of the sprite; sign = reveal direction
        // (+ from the top, − from the bottom). 1.0 = fully shown (default each frame).
        public readonly double[] MonClip = { 1, 1 };
        // CAP_NormalAlphaFade / WE_252: per-mon translucency (blend alpha1/16). 1.0 = opaque (default each frame).
        public readonly double[] MonAlpha = { 1, 1 };
        // Per-scanline horizontal warp of a mon sprite (the *_Laster / DefLaster raster family). Each row y is shifted by
        //   ofs_x = sin(baseDeg + addPerRow·(y−top))·(amp ± shimmer) + (y−center)·widthA/10
        // Used by Extrasensory (WE_326DF: sine bulge over SIZE_Y 80 + shear, shimmering) and Acid Armor (WE_151: an 8-px
        // scrolling ripple as it melts). MonWarpMon = which mon (-1 = none); the compositor's BlitMon reads these. Transient.
        public int MonWarpMon { get; private set; } = -1;
        public double MonWarpAmp { get; private set; }         // sine amplitude (px)
        public double MonWarpBaseDeg { get; private set; }     // base angle (Acid Armor scrolls this)
        public double MonWarpAddPerRow { get; private set; }   // angle added per scanline (°)
        public double MonWarpWidthA { get; private set; }      // shear factor (0 = none)
        public int MonWarpShimmer { get; private set; }        // ±WIDTH_OFS(1) per-frame shiver (0 = none)
        public double ShakeX { get; private set; }
        public double ShakeY { get; private set; }
        public bool Grayscale { get; private set; }   // the grayscale-toggle handler: desaturate the whole scene palette (mode!=0)
        // Background colour flash (a palette color-change on FADE_MAIN_BG): tints the BACKDROP+platforms toward a colour
        // (NOT the mons). Earthquake (WE_089) pulses this black↔white each shake step. Transient (reset per frame).
        public double BgFlashAmount { get; private set; }
        public byte BgFlashR { get; private set; }
        public byte BgFlashG { get; private set; }
        public byte BgFlashB { get; private set; }
        // Afterimage ghosts (Double Team WE_104, Agility/After You blur, …): extra copies of a mon sprite at an offset,
        // scaled/faded and palette-recoloured (grayscale copies via the mon color-change call). Rebuilt every frame in UpdateMonFx,
        // drawn behind the real mons by the compositor.
        public struct MonGhost { public int Mon; public double Dx, Dy, ScaleX, ScaleY, Alpha; public byte TintR, TintG, TintB; public double TintA; }
        private readonly List<MonGhost> _ghosts = new List<MonGhost>();
        public IReadOnlyList<MonGhost> Ghosts => _ghosts;

        // POKEOAM_DROP: a Pokémon dropped into the OAM as a CAP, a persistent copy of a mon's sprite that the CAP_*
        // routines (CAP_POKE_SCALE / OAM_PAL_FADE / MOSAIC / POKE_OAM_VIEW) then scale/recolour/mosaic. Disable drops a
        // gray clone of the target; Substitute/Transform/Wish use it too. Keyed by cap_id; persists until DROP_RESET.
        public sealed class DroppedCap
        {
            public int SrcMon;                 // visual sprite to copy (0 = player/bottom, 1 = enemy/top)
            public double Dx, Dy, ScaleX = 1, ScaleY = 1, Alpha = 1, RotDeg, Mosaic;
            public byte TintR, TintG, TintB; public double TintA;
            public bool Visible = true;
            public int Priority = 2;           // OAM view priority (POKE_OAM_VIEW); lower value = drawn in front.
            // the sink-into-void step function's hardware window: OBJ pixels INSIDE this screen rect are hidden (the window's
            // inside-plane excludes OBJ), so the sinking copy is swallowed as it crosses into it. Empty
            // (X1 < X0) = no clip.
            public double ClipOutX0, ClipOutY0, ClipOutX1 = -1, ClipOutY1 = -1;
        }
        private readonly Dictionary<int, DroppedCap> _caps = new Dictionary<int, DroppedCap>();
        public IReadOnlyCollection<DroppedCap> Caps => _caps.Values;
        // WE_TOOL_C0..C3 (0x02/0x04/0x08/0x10) → cap-id 0..3, or −1 if the flag isn't a cap target.
        private static int CapIdFromToolFlag(int flag)
        { for (int i = 0; i < 4; i++) if ((flag & (0x2 << i)) != 0) return i; return -1; }

        // Exact port of the shake initializer / the shake-step calculator / the shake-tool state stepper. The offset cycles
        // +amp → 0 → −amp → 0 (one the shake-tool state stepper step every `sync` frames); after 4 steps `num` decrements. Done at num==0.
        private sealed class Shake
        {
            public readonly int AmpX, AmpY, Sync, Num0; private int _cnt, _num, _step, _befX, _befY;
            public int X, Y;
            public Shake(int x, int y, int sync, int num)
            { AmpX = x; AmpY = y; Sync = sync; Num0 = num; _cnt = sync; _num = num; _befX = -x; _befY = -y; }
            private static void Tool(ref int now, ref int bef) { int t = bef; bef = now; now = (t == 0) ? 0 : -t; }
            public bool Calc()   // returns FALSE once the shake has finished (matches the shake-step calculator)
            {
                if (_num == 0) return false;
                if (++_cnt >= Sync) { _cnt = 0; Tool(ref X, ref _befX); Tool(ref Y, ref _befY); if (++_step >= 4) { _step = 0; _num--; } }
                return true;
            }
        }

        // HAIKEI scrolling background (Surf water sweep / Fly sky / Cosmic Power cosmos, …). A decoded BG image is
        // scrolled (pos += spd) and alpha-faded over the scene. WE_T02 is an EFFECT overlay (drawn over the mons,
        // semi-transparent); HAIKEI_CHG replaces the battle backdrop (drawn behind the mons).
        private readonly BattleBgRenderer _bgRenderer = new BattleBgRenderer();
        private byte[] _bgRgba; private int _bgW, _bgH, _bgWrapW, _bgWrapH;
        // WeT02 constants (BG-scroll-register space; same for all moves):
        private const int WET02_START_Y_OFS = 128;                       // pos_y nudged by (OFS/3*2) at start
        private const int WET02_STOP_Y_HI = 512, WET02_STOP_Y_LO = -412; // WET02_STOP_Y_1 / _2 → fade-out trigger
        // BATTLE_FRAME_EFFECT (FRAME2_M) is a GF_BGL_SCRSIZ_512x512 BG (TextBgCntDat). The effect frame is
        // ScrClear'd then the NSCR is loaded into the top, so the layer WRAPS at 512×512 with the rows beyond the
        // loaded NSCR transparent. (This is why a 512×256 water sheet does NOT tile straight into 2 bands.)
        private const int FX_BG_WRAP = 512;
        private double _bgX, _bgY, _bgSpdX, _bgSpdY;

        /// <summary>Frames left before a background that runs for a set time starts washing off.
        /// Below zero means it has no set time and something else ends it.</summary>
        private int _bgHoldLeft = -1;

        /// <summary>
        /// Changes one setting of the background that is already scrolling (WEST_HAIKEI_PARA_CHG). The
        /// numbers are from we_def.h:385-392: 0 and 1 are the two speeds, 2 and 3 the two positions. The
        /// rest of them are fade and rotation settings this preview does not follow.
        /// </summary>
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
        private readonly int[] _work = new int[16];   // WORK_SET gp work (WEDEF_GP_INDEX_*: [0]=SPEED_X,
                                                      // [1]=SPEED_Y, [2]=BGPOS_X, [3]=BGPOS_Y, [6]=SPEED_R)

        // the per-script scroll-reversal rule: the work[SPEED_R] param lets a script flip its OWN backdrop
        // scroll/position per side, 0 = never; 1 = reverse when the DEFENDER is on the player's side;
        // 2 = the same, except a self-targeting move (at==df) reverses when the caster is the enemy.
        private bool BackdropScrollReversedForSide()
        {
            int r = _work[6];
            if (r == 0) return false;
            bool dfMine = _dfVis == 0;                 // defender on the player's (bottom) side
            if (r == 2 && _atVis == _dfVis) return _attackerIsEnemy;
            return dfMine;
        }
        // ラスター: a per-scanline horizontal sine wave on the battle background (heat-haze/ripple
        // for Nightmare/Whirlpool/Water Pulse). All exact: ROTA_ADD = 1°/scanline, SCR_SP = 200 angle-units/frame,
        // amplitude ROTA_WIDTH = 32·FX32_ONE → FX_MUL(sin, width)>>12 = ±32 px.
        private int _rasterLeft;
        public bool RasterActive => _rasterLeft > 0;
        public double RasterPhase { get; private set; }
        public double RasterAmp { get; private set; }
        public double RasterLineAdd => Math.PI / 180.0;   // ROTA_ADD = FX_GET_ROTA_NUM(1) = 1°
        public bool HasBackground => _bgRgba != null && _bgOpacity > 0.001;
        public bool BackgroundIsOverlay => _bgOverlay;
        // HAIKEI background transition state (haikei_chg_flag): NONE once a HAIKEI_CHG fade-in reaches peak or
        // a RECOVER fade-out reaches zero (rgba cleared). HAIKEI_CHG_WAIT (60 moves) blocks until this settles;
        // without it the move fired its effect before the backdrop appeared (Hyper Beam etc.).
        private bool BgSettled => _bgRgba == null || (!_bgFadingOut && _bgOpacity >= _bgPeak - 1e-6);
        // ENUM_HMODE_HALF: the fade has passed its midpoint. HAIKEI_HALF_WAIT (3 moves) releases here.
        private bool BgHalf => _bgRgba == null || _bgOpacity >= _bgPeak * 0.5 - 1e-6;
        // GX G2_SetBlendAlpha coefficients for the effect-BG plane: out = water·BgCa + sceneBelow·BgCb. For a WeT02
        // overlay this is the eva/evb blend (water added over a dimmed scene); for a HAIKEI backdrop it's a crossfade.
        public double BgCa { get; private set; }
        public double BgCb { get; private set; }
        /// <summary>Samples the scrolled+wrapped effect-BG at screen pixel (x,y), straight RGBA. False if no BG.</summary>
        public bool TrySampleBg(int x, int y, out byte r, out byte g, out byte b, out byte a)
        {
            r = g = b = a = 0;
            if (_bgRgba == null) return false;
            // Wrap at the BG-control size (FRAME2_M 512×512 for an effect overlay); the NSCR fills only part of it,
            // the rest is the cleared (transparent) BG.
            int sx = ((int)Math.Round(_bgX) % _bgWrapW + _bgWrapW) % _bgWrapW;
            int sy = ((int)Math.Round(_bgY) % _bgWrapH + _bgWrapH) % _bgWrapH;
            int tx = (sx + x) % _bgWrapW, ty = (sy + y) % _bgWrapH;
            if (tx >= _bgW || ty >= _bgH) return false;   // outside the loaded NSCR → cleared/transparent
            int i = (ty * _bgW + tx) * 4;
            r = _bgRgba[i]; g = _bgRgba[i + 1]; b = _bgRgba[i + 2]; a = _bgRgba[i + 3];
            return true;
        }

        // WE_057 (Surf wave): the wave actor scales thin→tall (rise) then wide→flat (wash) while
        // fading in/out. These three are internal scratch for the phase curve (scale = s/100, scale-rate helper),
        // applied to the wave CellActor each frame in UpdateCellFx, the actor renders through the normal cell-actor path.
        private double _cellScaleX = 1, _cellScaleY = 1, _cellOpacity = 1;
        private int _cellPhase = -1, _cellFrame, _cellDefX, _cellDefY;
        private CellActor _we057Actor;   // the casting wave actor WE_057 drives (one of the two ACT_ADD_EZ actors)
        private const int FN_WE_057 = 49;
        private const int WE057_OAM_HEIGHT = 16;          // WE057_OAM_HEIGHT (the Y-anchor poke_h)

        // ── General CATS cell-actor engine ──────────────────────────────────────────────────────────────────────
        // The VM sets Cells to the loaded effectclact resources before play. WEST_CATS_ACT_ADD[_EZ] create live
        // CellActors (the tested NANR playback model); they tick every frame and are rendered by the compositor at
        // their position + frame SRT + transform. WEST_CATS_RES_FREE clears them.
        public WeCellAnimRenderer Cells { get; set; }
        // The move's base power (MoveData.power), set by the VM before play. Drives WE_222's shake amplitude
        // (WazaEffParaGet(WE_PARA_POW)); −1 = unknown. Other power-scaled routines can read it too.
        public int MovePower { get; set; } = -1;
        private CellSequence[] _cellSeqs;
        private CellSequence[] CellSeqs => _cellSeqs ??= (Cells != null && Cells.Loaded ? Cells.BuildSequences() : Array.Empty<CellSequence>());
        private readonly List<CellActor> _catsActors = new List<CellActor>();
        public IReadOnlyList<CellActor> CatsActors => _catsActors;
        // WE_057 places the wave at the CASTER def: player (76,120) / enemy (144,64).
        private static readonly (int x, int y) WE057_DEF_PLAYER = (76, 120), WE057_DEF_ENEMY = (144, 64);
        private static double Lerp(double a, double b, double t) => a + (b - a) * Math.Clamp(t, 0, 1);

        // Things this move does that the preview does not show. Said out loud rather than left for
        // somebody to notice a difference and wonder whether it is a bug in their edit.
        private readonly List<string> _notes = new List<string>();
        public IReadOnlyList<string> Notes => _notes;

        private readonly List<int> _routinesRun = new List<int>();
        /// <summary>Which support routines this run actually reached, in order. Branches mean a script does not run all of its own.</summary>
        public IReadOnlyList<int> RoutinesRun => _routinesRun;

        private readonly List<int> _commandsRun = new List<int>();
        /// <summary>Which commands this run actually executed, by position in the script.</summary>
        public IReadOnlyList<int> CommandsRun => _commandsRun;
        private void Note(string what) { if (!_notes.Contains(what)) _notes.Add(what); }

        // background palette fade (coloured overlay, black for darken, green for Mega Drain, etc.)
        private double _fadeCur, _fadeStart, _fadeEnd; private int _fadeFrames, _fadeFramesLeft;
        public double FadeOpacity => Math.Clamp(_fadeCur, 0, 1);
        public byte FadeR { get; private set; }
        public byte FadeG { get; private set; }
        public byte FadeB { get; private set; }

        // Visual sprite index of the attacker / defender (0 = player/bottom, 1 = enemy/top). When the move is
        // previewed "performed by the enemy" these swap, so WE_TOOL_M1 (attacker) drives the top sprite and
        // SIDE_JP takes the enemy branch, matching what the game does when the AI is the attacker.
        private readonly int _atVis, _dfVis;

        public WestPlayer(List<WazaSeqCommand> cmds, WazaSeqVersion version, ScriptNarc particleNarc,
                          double atX, double atY, double dfX, double dfY, int width = 256, int height = 192,
                          bool attackerIsEnemy = false, bool selfTarget = false)
        {
            _cmds = cmds ?? new List<WazaSeqCommand>();
            _version = version; _particleNarc = particleNarc;
            _attackerIsEnemy = attackerIsEnemy;
            _atVis = attackerIsEnemy ? 1 : 0;
            // A self-targeting move (range = User/User-side) sets df_client == at_client in-game, so its TARGET_DF
            // emitters and defender-anchored effects play on the CASTER. Collapse the defender onto the attacker.
            _dfVis = selfTarget ? _atVis : (attackerIsEnemy ? 0 : 1);
            _atX = atX; _atY = atY;
            _dfX = selfTarget ? atX : dfX; _dfY = selfTarget ? atY : dfY;
            _renderer = new SpaParticlePreview(width, height);
            // Re-derive each command's word offset from the actual layout (opcode word + arg words) so branch/jump
            // targets resolve. The commands may come from the UI grid (which doesn't carry the parsed WordPos), so
            // we cannot trust c.WordPos here, recompute it, which reproduces the assembler's exact word layout.
            int wp = 0;
            for (int i = 0; i < _cmds.Count; i++)
            {
                _cmds[i].WordPos = wp;
                _wordToIndex[wp] = i;
                wp += 1 + _cmds[i].Args.Length;
            }
        }

        /// <summary>
        /// Which of the two animations a TURN_CHK move shows. The games pick by the parity of the battle's
        /// own turn counter (we_sys.c:3018), so a move can have a second animation that only ever plays on
        /// alternate turns. Set this to see that one.
        /// </summary>
        public bool SecondTurnVariant { get; set; }

        private readonly bool _attackerIsEnemy;
        private readonly Dictionary<int, int> _wordToIndex = new Dictionary<int, int>();

        /// <summary>Fired when a WEST_SE-family opcode plays a sound during preview, with the sound ID from the
        /// command's argument. The ViewModel wires this to actually render + play it; left null, playback just
        /// stays silent (the visual preview still runs normally).</summary>
        public Action<int> PlaySound;

        /// <summary>Play the attacker's own cry (WEST_VOICE_PLAY). The host knows which Pokemon that is.</summary>
        public Action PlayCry;

        /// <summary>Stop one sound that is playing, by the sequence number it was started with.</summary>
        public Action<int> StopSound;

        // Sounds scheduled by WEST_SE_WAITPLAY/WEST_SE_REPEAT to fire a real N-frame delay later instead of
        // immediately, ticked once per Step() alongside everything else on this same frame timeline.
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

        // Jump to the command at a word-relative target (offset measured from argWord, the word holding the offset),
        // as the assembler encodes it: (target − .)/4. Returns true if the jump landed on a known command.
        private bool JumpRelative(int argWord, int offset)
        {
            if (_wordToIndex.TryGetValue(argWord + offset, out int idx)) { _pc = idx; return true; }
            return false;
        }

        public bool Finished => _scriptDone && _renderer.AllFinished && _fadeFramesLeft <= 0 && _monFx.Count == 0 && !HasBackground && _cellPhase < 0 && _pendingSounds.Count == 0;
        public WriteableBitmap RenderFrame() => _renderer.RenderFrame();

        /// <summary>Every particle alive this frame, for checks that need to see them.</summary>
        public IEnumerable<SpaParticleState> LiveParticles() => _renderer.LiveParticles();

        public void Step()
        {
            TickPendingSounds();
            UpdateFade();
            UpdateMonFx();
            UpdateBackground();
            if (_rasterLeft > 0) { _rasterLeft--; RasterPhase += 200.0 / 65536.0 * 2 * Math.PI; }   // LASTER scroll (SCR_SP 200)
            UpdateCellFx();
            for (int i = 0; i < _catsActors.Count; i++)                           // advance live CATS cell actors
            {
                var a = _catsActors[i];
                if (a == _we057Actor) continue;   // WE_057 drives this one via scale/pos (UpdateCellFx); freeze its NANR pose
                a.Tick(); RunCatsDriver(a); a.Age++;                              // + run its ported CAT callback
            }

            if (_wait > 0) _wait--;
            // WAIT_FLAG blocks until every flagged WEEffect TCB finishes. HAIKEI_PAL_FADE registers such a
            // TCB (WeBGPalFade_TCB), so the fade must complete before the script continues; Flamethrower fades the BG
            // to red-brown and only THEN fires the beam (without this the beam shot before the fade was visible).
            else if (_waitFlag) { if (_monFx.Count == 0 && _cellPhase < 0 && _fadeFramesLeft <= 0) _waitFlag = false; }
            else if (_bgWait != 0) { if (_bgWait == 2 ? BgHalf : BgSettled) _bgWait = 0; }
            else if (_waitParticles) { if (_renderer.AllFinished) _waitParticles = false; }
            // A SEQEND ends the SCRIPT, not just the frame: without this guard the next Step resumed
            // execution right past it into the following variant block (PTAT / contest / other-side /
            // parity blocks), replaying the whole move (double Charge Beam / Lunar Dance).
            else if (!_scriptDone) RunCommands();

            _renderer.Step();   // advance all live emitters
        }

        private void RunCommands()
        {
            while (_pc < _cmds.Count)
            {
                if (++_guard > 100000) { _scriptDone = true; return; }
                var c = _cmds[_pc];
                string name = WestOpcodes.Name(_version, c.OpId);
                _commandsRun.Add(_pc);
                _pc++;

                switch (name)
                {
                    case "WEST_SEQEND":
                        _scriptDone = true; return;

                    // Sound-effect opcodes: the first argument is always the raw sound (sequence) number.
                    // WEST_SEPAN_FLOW is a sound whose PAN sweeps from a start to an end value over time
                    // (Water Gun/Hydro Pump's water-stream sound). It fires immediately like
                    // WEST_SE/WEST_SEPLAY_PAN; the pan sweep itself isn't modelled (the render is a fixed
                    // mono-to-stereo pan like every other WEST_SE variant).
                    case "WEST_SE":
                    case "WEST_SEPLAY_PAN":
                    case "WEST_SEPAN_FLOW":
                        if (c.Args.Length >= 1) PlaySound?.Invoke(c.Args[0]);
                        break;

                    // WEST_SE_WAITPLAY sound,pan,wait fires the sound `wait` FRAMES later, not immediately.
                    case "WEST_SE_WAITPLAY":
                        if (c.Args.Length >= 3) SchedulePlaySound(c.Args[0], c.Args[2]);
                        else if (c.Args.Length >= 1) PlaySound?.Invoke(c.Args[0]);
                        break;

                    // WEST_SE_REPEAT sound,pan,wait,repeat plays the sound `repeat` times, `wait` frames
                    // apart (e.g. Metronome's own tick, 8 frames/~133ms apart, a metronome's real ticking
                    // rhythm).
                    case "WEST_SE_REPEAT":
                        if (c.Args.Length >= 4)
                        {
                            int wait = Math.Max(0, c.Args[2]), repeat = Math.Max(1, c.Args[3]);
                            for (int r = 0; r < repeat; r++) SchedulePlaySound(c.Args[0], r * wait);
                        }
                        else if (c.Args.Length >= 1) PlaySound?.Invoke(c.Args[0]);
                        break;

                    // WEST_TURN_CHK offEven,offOdd: pick ONE branch by the parity of the global
                    // waza_eff_cnt, for genuine two-turn moves (Fly/Dig) that's charge vs attack turn, but
                    // plenty of moves use it for alternating VARIANTS (Lunar Dance). A fresh battle previews
                    // with count 0 (even), taking the first branch, exactly like the game's first use.
                    // TURN_CHK adrs_even, adrs_odd: the games alternate two animations by the battle's own
                    // turn counter (we_sys.c WEST_TURN_CHK jumps from the first offset on an even count and
                    // from the second on an odd one). A preview has no turn count, so it shows the even one,
                    // which is what a move looks like the first time it is used, unless the second variant
                    // is asked for.
                    case "WEST_TURN_CHK":
                        if (SecondTurnVariant)
                        {
                            if (c.Args.Length >= 2 && JumpRelative(c.WordPos + 2, c.Args[1])) break;
                        }
                        if (c.Args.Length >= 1 && JumpRelative(c.WordPos + 1, c.Args[0])) break;
                        if (c.Args.Length >= 2) JumpRelative(c.WordPos + 2, c.Args[1]);
                        break;

                    // TENKI_JP no_weather, rain, sandstorm, sun, hail: WEST_TENKI_JP always jumps, picking
                    // the offset for whichever weather is up and the first one when there is none. Letting
                    // this fall through ran the wrong branch of the one move that uses it, Weather Ball.
                    case "WEST_TENKI_JP":
                        if (c.Args.Length >= 1) JumpRelative(c.WordPos + 1, c.Args[0]);
                        break;

                    // CONTEST_JP jumps only in a Contest and PTAT_JP only when attacker and defender are on
                    // the same side, which is a double battle helping your own partner. Neither is true of
                    // this preview, so both carry on to the next command, which is what the games do too.
                    case "WEST_CONTEST_JP":
                    case "WEST_PTAT_JP":
                        break;
                    case "WEST_SEQ_JP":                                  // unconditional jump
                        if (c.Args.Length >= 1) JumpRelative(c.WordPos + 1, c.Args[0]);
                        break;
                    // SIDE_JP type,adrs1,adrs2 (WEST_SIDE_JP): checks a client's battle side, type 0 = the
                    // attacker, else the defender, and jumps adrs2 if that client is on the ENEMY side, otherwise
                    // adrs1. Mirrors how a move flips for player-vs-enemy casters; the preview's side toggle decides.
                    case "WEST_SIDE_JP":
                        if (c.Args.Length >= 3)
                        {
                            bool checkedIsEnemy = c.Args[0] == 0 ? _attackerIsEnemy : !_attackerIsEnemy;
                            if (checkedIsEnemy) { if (JumpRelative(c.WordPos + 3, c.Args[2])) break; }
                            else { if (JumpRelative(c.WordPos + 2, c.Args[1])) break; }
                        }
                        break;
                    // LOOP_LABEL cnt … LOOP repeats the block `cnt` times (WEST_LOOP_LABEL/WEST_LOOP). The
                    // body starts at the command right after LOOP_LABEL (= _pc, already advanced).
                    case "WEST_LOOP_LABEL":
                        _loops.Add(new LoopFrame { Body = _pc, Total = c.Args.Length > 0 ? c.Args[0] : 1, Count = 0 });
                        break;
                    case "WEST_LOOP":
                        if (_loops.Count > 0)
                        {
                            var f = _loops[_loops.Count - 1];
                            if (++f.Count >= f.Total) _loops.RemoveAt(_loops.Count - 1);   // done → fall out of the loop
                            else _pc = f.Body;                                              // else → back to the body
                        }
                        break;
                    // SEQ_CALL adrs … END_CALL is a subroutine call/return (WEST_SEQ_CALL/WEST_END_CALL):
                    // push the next command as the return point, jump to the (relative) target, run until END_CALL.
                    case "WEST_SEQ_CALL":
                        if (c.Args.Length >= 1) { _callStack.Add(_pc); JumpRelative(c.WordPos + 1, c.Args[0]); }
                        break;
                    case "WEST_END_CALL":
                        if (_callStack.Count > 0) { _pc = _callStack[_callStack.Count - 1]; _callStack.RemoveAt(_callStack.Count - 1); }
                        break;

                    case "WEST_WAIT": _wait = c.Args.Length > 0 ? Math.Max(0, c.Args[0]) : 0; return;
                    // WAIT_FLAG blocks on the last registered action TCB (a mon move / shake); this is what makes the
                    // forward lunge finish before the slide-back. WAIT_PARTICLE blocks on the particles.
                    case "WEST_WAIT_FLAG":
                        if (_monFx.Count > 0 || _cellPhase >= 0) { _waitFlag = true; return; }
                        break;
                    case "WEST_WAIT_PARTICLE":
                        if (!_renderer.AllFinished) { _waitParticles = true; return; }
                        break;

                    case "WEST_LOAD_PARTICLE":
                    case "WEST_LOAD_PARTICLE_EX":
                        if (c.Args.Length >= 2) _slot[c.Args[0]] = c.Args[1];
                        break;
                    // CAMERA_CHG no,mode / CAMERA_REVERCE no,flag: set the per-particle-slot camera
                    // mode/reverse flag; downstream anchor lookups use the turned-camera coordinate set, which
                    // we reproduce by mirroring that slot's layers (ViewReversed).
                    case "WEST_CAMERA_CHG":
                    case "WEST_CAMERA_REVERCE":
                        if (c.Args.Length >= 2) _cameraMode[c.Args[0]] = c.Args[1];
                        break;
                    // EXIT_PARTICLE no (the emitter-stop routine): stop the slot's emitters; quits emission so live particles
                    // die out, and lets an "emit forever" emitter actually finish (so WAIT_PARTICLE can release).
                    case "WEST_EXIT_PARTICLE":
                        if (c.Args.Length >= 1 && _ptcSims.TryGetValue(c.Args[0], out var exitSims))
                        {
                            foreach (var s in exitSims) s.Stop();
                            exitSims.Clear();
                        }
                        break;

                    case "WEST_ADD_PARTICLE":
                        if (c.Args.Length >= 2)
                        {
                            int cb = c.Args.Length >= 3 ? c.Args[2] : 0;
                            if (cb == EMTFUNC_FIELD_OPERATOR) SpawnOperator(c.Args[0], c.Args[1]);
                            else Spawn(c.Args[0], c.Args[1], cb, 0, 1);
                        }
                        break;
                    case "WEST_ADD_PARTICLE_EMIT_SET":   // ptc, emit_no, data_no, callback, registers slot emit_no
                        if (c.Args.Length >= 4)
                        {
                            var sim = Spawn(c.Args[0], c.Args[2], c.Args[3], 0, 1);
                            if (sim != null) _emitSlots[c.Args[1]] = sim;
                        }
                        break;
                    case "WEST_ADD_PARTICLE_SEP":
                    case "WEST_ADD_PARTICLE_PTAT":
                        // WEST_ADD_PARTICLE_SEP/PTAT list 6/4 pre-aimed emitter variants (one per battle
                        // formation aa/bb/a/b/c/d) but create EXACTLY ONE: index[ParticleSepIndexGet()]. In 1v1 that's
                        // 0 when the player attacks (beam aimed at the enemy) or 3 when the enemy attacks. Spawning all
                        // of them fired every direction at once (Hyper Beam shot forwards+sideways; Water Gun doubled).
                        if (c.Args.Length >= 3)
                        {
                            int cb = c.Args[c.Args.Length - 1], count = c.Args.Length - 2;
                            int sep = _attackerIsEnemy ? 3 : 0;
                            if (sep >= count) sep = 0;
                            Spawn(c.Args[0], c.Args[1 + sep], cb, 0, 1);
                        }
                        break;

                    // CATS cell actors. ACT_ADD_EZ [res_no, cap_id, char,pltt,cell,cellanm,mcell,mcellanm] → a
                    // display-only actor at the defender pos, kept in slot cap_id (a later FUNC_CALL drives it).
                    // ACT_ADD [res_no, func_id, …ids…, cnt, …gpwk] → an actor with a CATS callback (driven each frame).
                    case "WEST_CATS_ACT_ADD_EZ":
                        CatsActAdd(c.Args.Length > 1 ? c.Args[1] : 0, withCallback: false, null);
                        break;
                    case "WEST_CATS_ACT_ADD":   // [res_no, func_id, 6 ids, cnt, …gp_wk], gp_wk starts at arg 9
                        CatsActAdd(c.Args.Length > 1 ? c.Args[1] : -1, withCallback: true,
                            c.Args.Length > 9 ? c.Args[9..] : null);
                        break;
                    case "WEST_CATS_RES_FREE":
                        _catsActors.Clear();
                        break;

                    // POKEOAM_DROP flag,auto_move,cap_id,use_no, drop a copy of the flag-selected mon into OAM cap_id.
                    // PT_DROP/PT_DROP_RESET are the 2v2-partner equivalents. RES_FREE / DROP_RESET remove the cap(s).
                    case "WEST_POKEOAM_DROP":
                    case "WEST_PT_DROP":
                        if (c.Args.Length >= 3)
                        {
                            int srcMon = (c.Args[0] == 1 || c.Args[0] == 3) ? _dfVis : _atVis;   // E1/E2 → defender, M1/M2 → attacker
                            int capId = c.Args[2];
                            _caps[capId] = new DroppedCap { SrcMon = srcMon };
                        }
                        break;
                    case "WEST_POKEOAM_DROP_RESET":
                    case "WEST_PT_DROP_RESET":
                        if (c.Args.Length >= 1) _caps.Remove(c.Args[0]); else _caps.Clear();
                        break;
                    case "WEST_POKEOAM_RES_FREE":
                        _caps.Clear();
                        break;

                    // WORK_SET soeji,work / WORK_CLEAR: GP work registers. The HAIKEI background scroll
                    // reads its speed from work[SPEED_X=0] / work[SPEED_Y=1].
                    case "WEST_WORK_SET":
                        if (c.Args.Length >= 2 && c.Args[0] >= 0 && c.Args[0] < _work.Length) _work[c.Args[0]] = c.Args[1];
                        break;
                    case "WEST_WORK_CLEAR":
                        Array.Clear(_work, 0, _work.Length);
                        break;
                    // HAIKEI_CHG map_id, mode: replaces the full backdrop behind the mons, fading in, starting
                    // at work[BGPOS_X/Y] and scrolling at work[SPEED_X]/[SPEED_Y] if mode has the MOVE bit,
                    // until HAIKEI_RECOVER fades it out. The scroll updater feeds pos += speed straight into
                    // the BG scroll register, and hardware samples texel(screen + scroll), so positive
                    // speed_y scrolls the sky UP (Lunar Dance's rising moon). work[SPEED_R] can flip
                    // everything per side (ParamRev).
                    case "WEST_HAIKEI_CHG":
                        if (c.Args.Length >= 1)
                        {
                            int hRev = BackdropScrollReversedForSide() ? -1 : 1;
                            StartBackground(c.Args[0], overlay: false, posX: _work[2] * hRev, posY: _work[3] * hRev,
                                spdX: _work[0] * hRev, spdY: _work[1] * hRev, peak: 1.0, fadeFrames: 12, stopY: 0, useStop: false);
                        }
                        break;
                    case "WEST_HAIKEI_RECOVER":
                        _bgFadingOut = true;
                        break;
                    // HAIKEI_CHG_WAIT / HAIKEI_HALF_WAIT: block the script until the backdrop fade settles
                    // (flag→NONE) / passes its midpoint (flag→HALF). 60 / 3 moves rely on this for correct ordering.
                    case "WEST_HAIKEI_CHG_WAIT":
                        if (!BgSettled) { _bgWait = 1; return; }
                        break;
                    case "WEST_HAIKEI_HALF_WAIT":
                        if (!BgHalf) { _bgWait = 2; return; }
                        break;

                    // ── HGSS-only opcodes (appended after WEST_KEY_WAIT in HG's) ──────────────
                    // WEST_FLASH time, a full-screen WHITE flash that fades out over `time` frames.
                    case "WEST_FLASH":
                        _fadeStart = 1.0; _fadeEnd = 0; _fadeCur = 1.0;
                        _fadeFrames = _fadeFramesLeft = c.Args.Length >= 1 && c.Args[0] > 0 ? c.Args[0] : 8;
                        FadeR = FadeG = FadeB = 255;
                        break;
                    // WEST_HAIKEI_CHG_EX map_id, ch_mode, ex_bit, extended backdrop change driven by HG's
                    // animated batt_bg_planm data. We approximate it as a plain HAIKEI_CHG to map_id (the per-frame
                    // palette animation frames aren't loaded); ch_mode/ex_bit select the plane-anim variant.
                    case "WEST_HAIKEI_CHG_EX":
                        if (c.Args.Length >= 1)
                        {
                            int hRevEx = BackdropScrollReversedForSide() ? -1 : 1;
                            StartBackground(c.Args[0], overlay: false, posX: _work[2] * hRevEx, posY: _work[3] * hRevEx,
                                spdX: _work[0] * hRevEx, spdY: _work[1] * hRevEx, peak: 1.0, fadeFrames: 12, stopY: 0, useStop: false);
                        }
                        break;
                    // WEST_BATONTATTI_JP adrs, Baton Pass touch: jumps by `adrs` ONLY when the attacker has
                    // a client pair (double battle). In a single-target preview at_client_pair is false, so it just
                    // skips the offset, a no-op here (the arg is already consumed by the parser).
                    case "WEST_BATONTATTI_JP":
                        break;

                    // Stops one sound that is already playing, by its number (Snd_SeStopBySeqNo). A sound
                    // this script has queued but not started yet is simply dropped.
                    case "WEST_SE_STOP":
                        if (c.Args.Length >= 1)
                        {
                            int stopId = c.Args[0];
                            _pendingSounds.RemoveAll(x => x.soundId == stopId);
                            StopSound?.Invoke(stopId);
                        }
                        break;

                    // Plays the attacking Pokemon's own cry, with a pan and a volume this preview does not
                    // apply (WEST_VOICE_PLAY reads type, pan, volume; the cry itself is the attacker's).
                    case "WEST_VOICE_PLAY":
                        if (PlayCry != null) PlayCry();
                        else Note("This move plays the Pokémon's cry, which the preview cannot play here.");
                        break;

                    // Waits for that cry to finish before carrying on. Nothing here holds a cry open, so
                    // this waits the number of frames the script asks for and moves on.
                    case "WEST_VOICE_WAIT_STOP":
                        _wait = c.Args.Length > 0 ? Math.Max(0, c.Args[0]) : 0;
                        return;

                    // Shows or hides one of the dropped copies (CATS_ObjectEnableCap).
                    case "WEST_POKE_OAM_ENABLE":
                        if (c.Args.Length >= 2 && _caps.TryGetValue(c.Args[0], out var oamCap))
                            oamCap.Visible = c.Args[1] != 0;
                        break;

                    // Swaps the Pokemon's graphic for another one: the Substitute doll, the Snatch figure,
                    // or whatever Transform copied (WEST_HENSIN_ON reads which, then loads that graphic).
                    // The preview draws the Pokemon it was given and has no second graphic to swap in.
                    case "WEST_HENSIN_ON":
                    case "WEST_HENSIN_ON_RC":
                        Note("This move swaps the Pokémon's graphic for another one, which the preview keeps as it is.");
                        break;

                    // Copies the Pokemon into the background layer as tiles, and later clears it. The
                    // preview draws Pokemon as sprites and has no background copy of them.
                    case "WEST_POKEBG_DROP":
                        Note("This move draws a copy of the Pokémon into the background, which the preview does not do.");
                        break;
                    case "WEST_POKEBG_DROP_RESET":
                        break;   // nothing was drawn, so there is nothing to clear

                    // Changes a scrolling background's speed or position while it is running. Only the
                    // two speeds are followed here; the rest of the settings are left alone.
                    case "WEST_HAIKEI_PARA_CHG":
                        if (c.Args.Length >= 2) SetBackgroundParam(c.Args[0], c.Args[1]);
                        break;

                    // Set aside memory and load graphics into it. The preview draws from its own decoded
                    // copies of those graphics, so there is nothing here to set aside or load. Written out
                    // rather than left to fall through, so nothing is skipped without a reason.
                    case "WEST_POKEOAM_RES_INIT":
                    case "WEST_POKEOAM_RES_LOAD":
                    case "WEST_CATS_RES_INIT":
                    case "WEST_CATS_CAHR_RES_LOAD":
                    case "WEST_CATS_PLTT_RES_LOAD":
                    case "WEST_CATS_CELL_RES_LOAD":
                    case "WEST_CATS_CELLANM_RES_LOAD":
                        break;

                    // Never run as a command. Its own handler in the games is an assertion saying so
                    // (we_sys.c WEST_EX_DATA); it is read as data by the particle spawn before it, which
                    // is what the particle-adding cases here already do.
                    case "WEST_EX_DATA":
                        break;

                    // A pause the developers left in, waiting for L, R and X together (we_sys.c
                    // WEST_KEY_WAIT). Only one script has one, and stopping a preview dead is not useful.
                    case "WEST_KEY_WAIT":
                        Note("This move has a developer's pause left in it, which the preview runs straight past.");
                        break;

                    case "WEST_FUNC_CALL":
                    case "WEST_OLDACT_FUNC_CALL":
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
            // A particle whose picture could not be read is drawn as a plain round dot, which looks like
            // a real effect and is not one. Say so rather than let it pass for the move's own graphics.
            // No particle in either game fails today; this is for edited or added ones that might.
            if (tex == null)
                Note($"Particle {emitterNo} asks for picture {em.TexNo}, which is not in its file, so it "
                     + "is drawn as a plain dot.");
            else if (tex.Rgba == null)
                Note($"Particle {emitterNo}'s picture is stored in a way DSPRE cannot read (format "
                     + $"{tex.Format}), so it is drawn as a plain dot.");
            var (cx, cy, ax, ay, z) = Place(callback, sepIndex, sepCount);
            cx += em.PosX; cy -= em.PosY;   // emitter base offset (+Y up): hand-above (Karate Chop), L/R slap (Double Slap)
            // init_vel_axis needs an axis direction: the callback's (AXIS_ATTACK → toward defender) if it set one,
            // else the emitter's OWN base.axis (Flame Wheel's spinning flames stream along it, then spin → a wheel).
            double axX, axY;
            if (ax != 0 || ay != 0) { axX = ax; axY = -ay; }
            else { axX = em.AxisX; axY = em.AxisY; }
            var sim = new SpaSimulator(em, axX, axY) { AnchorX = cx, AnchorY = cy };   // spawn anchor (for EMIT_ROTATION re-centering)
            // EmitCall_CameraReverse* (cb 1/2) with an enemy attacker, or a WEST_CAMERA_CHG/REVERCE on this
            // slot: the game turns the particle camera 180°, mirroring the layer (and rotation chirality).
            bool reversed = ((callback == 1 || callback == 2) && _attackerIsEnemy)
                            || (_cameraMode.TryGetValue(ptc, out int camMode) && camMode != 0);
            // Mirror a quadrant texture into a full sprite only when the texture flips AND the emitter doubles the
            // texcoord span (tex_repeat ≥ 1), that's what builds a ring/flare from one stored quarter.
            _renderer.AddLayer(new SpaParticlePreview.Layer(sim, arc.Textures, tex, cx, cy, em.DrawType,
                em.RepeatS, em.RepeatT, em.Aspect, em.DbbScale, em.OffsetX, em.OffsetY,
                baseZ: z + em.PosZ, viewReversed: reversed, flipS: em.FlipS, flipT: em.FlipT, em: em));
            _lastSim = sim;
            TrackSim(ptc, sim);
            return sim;
        }

        // WEST_CAMERA_CHG/REVERCE per particle-slot camera state (we_sys camera_mode[]/camera_flag[]).
        private readonly Dictionary<int, int> _cameraMode = new Dictionary<int, int>();

        // Remember which emitters a particle slot spawned, so WEST_EXIT_PARTICLE can stop exactly those.
        private void TrackSim(int ptc, SpaSimulator sim)
        {
            if (!_ptcSims.TryGetValue(ptc, out var list)) _ptcSims[ptc] = list = new List<SpaSimulator>();
            list.Add(sim);
        }

        private SpaSimulator FindEmitter(int idx) => _emitSlots.TryGetValue(idx, out var s) ? s : _lastSim;

        // OPERATOR_POS_* values whose case reads s_client (the start/attacker side) → particles travel
        // to the target: SP(1) SP_OFS(4) LSP(6) RSP(8) L2SP(10) L3SP(14) L095SP(16) L161SP(18) L308SP(20) L304SP(22)
        // L320SP(24) L406SP(26), plus POS_194(34) which also just reads s_client. All other (EP) positions sit at the
        // target end. The FIXED-table positions (145/225/226) override the anchor entirely (handled below).
        private static readonly HashSet<int> _startPos = new HashSet<int> { 1, 4, 6, 8, 10, 14, 16, 18, 20, 22, 24, 26, 34 };
        private static bool IsStartPos(int pos) => _startPos.Contains(pos);

        // A projectile (EMTFUNC_FIELD_OPERATOR): the following EX_DATA gives source (AXIS_AT/DF) + destination
        // (TARGET_AT/DF). Spawns at the source and drifts the particles across to the target over their life, so
        // stings/beams/thrown objects actually travel instead of popping on the defender.
        private void SpawnOperator(int ptc, int emitterNo)
        {
            int target = 2, pos = 0, axis = 0, fldMode = 0;   // 1st EX_DATA: TARGET/POS/AXIS/FLD-mode
            int fldTgt = -1, fldFracN = 1, fldFracD = 1;      // 2nd EX_DATA: which mon the field converges to (+ %)
            double posOfsX = 0, posOfsY = 0;                  // POS_*_OFS extra offset from the 2nd EX_DATA
            if (_pc < _cmds.Count && WestOpcodes.Name(_version, _cmds[_pc].OpId) == "WEST_EX_DATA")
            {
                var ex = _cmds[_pc].Args;       // [count, pri, target, pos, axis, fld, camera]
                target = ex.Length > 2 ? ex[2] : 2;
                pos = ex.Length > 3 ? ex[3] : 0;
                axis = ex.Length > 4 ? ex[4] : 0;
                fldMode = ex.Length > 5 ? ex[5] : 0;   // FLD bitmask: MAGNET_POS=0x10, CONVERGENCE_POS=0x1000
                if (_pc + 1 < _cmds.Count && WestOpcodes.Name(_version, _cmds[_pc + 1].OpId) == "WEST_EX_DATA")
                {
                    var ex2 = _cmds[_pc + 1].Args;
                    if (pos == 4 || pos == 5 || pos == 12 || pos == 13)   // SP_OFS/EP_OFS + AT_SIDE_OFS/DF_SIDE_OFS:
                    {                           // 2nd EX_DATA = [count, reverce, offX, offY, offZ] (ECB_Tool_ExDataSet),
                                                // offsets in PT_LCD_PTP_CHG = px·172 (Leech Seed vines / Dark hit marks spread).
                        if (ex2.Length > 3) { posOfsX = ex2[2] / 172.0; posOfsY = ex2[3] / 172.0; }
                    }
                    else
                    {   // A field-config EX_DATA: [count, mode, reverce, x|num, y|den, z]. FLD_AT(2)/DF(3)/SET_DF(4).
                        int m = ex2.Length > 1 ? ex2[1] : -1;
                        if (m == 2 || m == 3) fldTgt = m;
                        else if (m == 4 && ex2.Length > 4 && ex2[4] != 0) { fldTgt = 3; fldFracN = ex2[3]; fldFracD = ex2[4]; }
                    }
                }
            }
            // WeSysExDataGet TARGET switch: TARGET_AT(1)/AT_SIDE(3) SWAP the clients:
            // s_client=DF (defender), e_client=AT (attacker); TARGET_NONE(0)/DF(2)/DF_SIDE(4) keep s_client=AT,
            // e_client=DF. Each POS reads s_client ("*SP"/start positions: SP, LSP, RSP, L2SP, L3SP, L095SP…L406SP,
            // SP_OFS) or e_client ("*EP"/end positions). So Flash Cannon (TARGET_AT + EP) → e_client=attacker; an
            // Absorb-style TARGET_AT + SP → s_client=defender. 0 = attacker side, 1 = defender side.
            bool swapClients = target == 1 || target == 3;   // TARGET_AT / TARGET_AT_SIDE
            int sClient = swapClients ? 1 : 0;
            int eClient = swapClients ? 0 : 1;
            int src = (IsStartPos(pos) || pos == 12) ? sClient : eClient;   // POS_AT_SIDE_OFS(12) anchors on the attacker side
            // Any AT/DF directional axis, AT(1)/DF(2), the _SIDE pair (4/5), the _OLD pair (6/7)
            // and every angled-laser variant AT_3…DF_406 (8…21), calls the emitter-axis setter with the attacker→defender
            // line (AxisPosTable[at][df]); particles then STREAM along it by their own init_vel_axis. AXIS_NONE leaves
            // the emitter on the archive's OWN axis. SET(3) and the contest axes fall back to the archive's axis. There is NO
            // emitter drift anywhere; the operator's position handling only calls the emitter-position setter, so a beam IS the
            // particle stream and a start-position sparkle (Absorb's KIRA at POS_SP) stays put on the attacker.
            bool axisOverride = axis >= 1 && axis <= 21 && axis != 3;

            if (!_slot.TryGetValue(ptc, out int data)) return;
            var arc = LoadArc(data);
            if (emitterNo < 0 || emitterNo >= arc.Emitters.Count) return;
            var em = arc.Emitters[emitterNo];
            var tex = (em.TexNo >= 0 && em.TexNo < arc.Textures.Count) ? arc.Textures[em.TexNo] : null;

            // The emitter anchor: normally the start/end mon, BUT OPERATOR_POS_145 (Bubble) is a FIXED formation
            // position per client type (pos145 table); fx32 world → particle px (/172), screen =
            // origin ± (Y up). + base offset + the POS_*_OFS offset (+Y up → screen subtract).
            double anchorX = src == 0 ? _atX : _dfX, anchorY = src == 0 ? _atY : _dfY;
            if (pos == 30 || pos == 31 || pos == 32)   // OPERATOR_POS_226 / 145 / 225: fixed formation position per type
            {
                int t = sClient == 0 ? 0 : 1;   // AA / BB
                var p = pos == 30 ? Pos226[t] : pos == 32 ? Pos225[t] : Pos145[t];
                anchorX = PARTICLE_ORIGIN_X + p.x / 172.0;
                anchorY = PARTICLE_ORIGIN_Y - p.y / 172.0;
            }
            double sx = anchorX + em.PosX + posOfsX, sy = anchorY - em.PosY - posOfsY;
            double driftX = 0, driftY = 0, magOX = double.NaN, magOY = double.NaN, convOX = double.NaN, convOY = double.NaN;
            double magOZ = double.NaN, convOZ = double.NaN;
            // The emit axis points from s_client to e_client (AxisPosTable[type(s)][type(e)]); for TARGET_AT that is
            // defender→attacker, the reverse of TARGET_DF. Unit direction in sim space (+Y up): screen Δ, Y flipped.
            double sCx = sClient == 0 ? _atX : _dfX, sCy = sClient == 0 ? _atY : _dfY;
            double eCx = eClient == 0 ? _atX : _dfX, eCy = eClient == 0 ? _atY : _dfY;
            double atdfX = eCx - sCx, atdfY = sCy - eCy;
            double atdfLen = Math.Sqrt(atdfX * atdfX + atdfY * atdfY);
            if (atdfLen > 1e-6) { atdfX /= atdfLen; atdfY /= atdfLen; } else { atdfX = 0; atdfY = 1; }
            if (fldTgt >= 0 && fldMode != 0)
            {   // The operator retargets the emitter's MAGNET (Mega Drain) or CONVERGENCE field (BubbleBeam/Aurora beam)
                // to a mon. FLD_SET_DF scales the target toward the particle origin (screen ≈120,96) by num/den. The
                // SPA stores this RELATIVE to the emitter (pos −= odp->pos), so subtract the anchor.
                double tgX = fldTgt == 2 ? _atX : _dfX, tgY = fldTgt == 2 ? _atY : _dfY;
                if (fldFracD != 1 || fldFracN != 1)   // FLD_SET_DF: target = origin + (num/den)·(mon − origin)
                {
                    double f = (double)fldFracN / fldFracD;
                    tgX = PARTICLE_ORIGIN_X + f * (tgX - PARTICLE_ORIGIN_X);
                    tgY = PARTICLE_ORIGIN_Y + f * (tgY - PARTICLE_ORIGIN_Y);
                }
                double rX = tgX - sx, rY = sy - tgY;   // sim space (+Y up), relative to the emitter anchor
                // Depth of the target mon's plane relative to the emitter's plane; the field targets are
                // 3D points in-game (a Mega Drain magnet on the far-side mon pulls in depth too).
                double rZ = ZOfVis(fldTgt == 2 ? _atVis : _dfVis) - (ZOfVis(src == 0 ? _atVis : _dfVis) + em.PosZ);
                if ((fldMode & 0x1000) != 0) { convOX = rX; convOY = rY; convOZ = rZ; }   // FLD_CONVERGENCE_POS
                else if ((fldMode & 0x10) != 0) { magOX = rX; magOY = rY; magOZ = rZ; }  // FLD_MAGNET_POS
            }
            // (driftX/driftY stay 0; there is no emitter drift; see axisOverride note above.)
            // AXIS override → spray along the at→df line (init_vel_axis carries it). The *_SIDE variants (4/5) only
            // mirror X in 2v2/contest; in a 1v1 they resolve to the SAME at→df direction (AxisPosTable[0][1], ECB_
            // Operator_Axiss), so DON'T mirror here (String Shot's web was firing up-left instead of at the target).
            // AXIS_NONE / SET / contest → keep the SPA emitter's own axis (Absorb's KIRA sparkle stays on the attacker).
            double opAxX, opAxY;
            if (axisOverride) { opAxX = atdfX; opAxY = atdfY; }
            else if (axis == 24)   // OPERATOR_AXIS_145 (Bubble): a fixed direction per client type (axis145 table, +Y up)
            {
                int t = sClient == 0 ? 0 : 1;
                double ax = Axis145[t].x, ay = Axis145[t].y, l = Math.Sqrt(ax * ax + ay * ay);
                if (l < 1e-6) l = 1; opAxX = ax / l; opAxY = ay / l;
            }
            else if (axis == 26)   // OPERATOR_AXIS_194 (Destiny Bond): fixed by the caster's side (SIDE_MINE vs enemy).
            {
                bool mine = (sClient == 0) != _attackerIsEnemy;   // s_client on the player's side?
                double ax = mine ? 3776 : -6000, ay = mine ? 2112 : -2200, l = Math.Sqrt(ax * ax + ay * ay);
                opAxX = ax / l; opAxY = ay / l;
            }
            else if (axis == 3)    // OPERATOR_AXIS_SET (Ancient Power, …): the fixed default (-800, 1200, 500) → up-left.
            {
                double ax = -800, ay = 1200, l = Math.Sqrt(ax * ax + ay * ay);
                opAxX = ax / l; opAxY = ay / l;
            }
            else { opAxX = em.AxisX; opAxY = em.AxisY; }
            var sim = new SpaSimulator(em, opAxX, opAxY, driftX, driftY, magOX, magOY, convOX, convOY,
                                       magOverrideZ: magOZ, convOverrideZ: convOZ);
            // Anchor-plane depth: the side the emitter actually sits on (the fixed-formation positions
            // 145/225/226 belong to the s_client side too).
            double opZ = ZOfVis(src == 0 ? _atVis : _dfVis) + em.PosZ;
            bool opReversed = _cameraMode.TryGetValue(ptc, out int opCam) && opCam != 0;
            _renderer.AddLayer(new SpaParticlePreview.Layer(sim, arc.Textures, tex, sx, sy, em.DrawType,
                em.RepeatS, em.RepeatT, em.Aspect, em.DbbScale, em.OffsetX, em.OffsetY,
                baseZ: opZ, viewReversed: opReversed, flipS: em.FlipS, flipT: em.FlipT, em: em));
            _lastSim = sim;
            TrackSim(ptc, sim);
        }

        // Particle-space origin in screen px: particle (0,0,0) renders at (120, 96) (mapping, see top of file).
        // FLD_SET_DF convergence scales the target toward this point.
        private const double PARTICLE_ORIGIN_X = 120, PARTICLE_ORIGIN_Y = 96;
        // OPERATOR_POS_145/225/226 + AXIS_145 fixed-formation tables, 1v1 rows AA/BB (fx32 world).
        // 145 = Bubble, 225 = Dragon Breath, 226 = Baton Pass. (The _CON contest rows are unused in a 1v1 preview.)
        private static readonly (int x, int y)[] Pos145 = { (-5760, -4352), (9488, -1984) };
        private static readonly (int x, int y)[] Pos225 = { (-4608, -4480), (7624, 2248) };
        private static readonly (int x, int y)[] Pos226 = { (-11020, -3488), (10880, 7656) };
        private static readonly (int x, int y)[] Axis145 = { (2864, 3752), (-2944, 1456) };

        /// <summary>How many work slots a routine can read, whatever the script passed it.</summary>
        private const int WorkSlots = 8 + 2;   // WE_GENE_WK_MAX, we_sys.h:92

        private void DoFuncCall(int[] a)
        {
            if (a.Length < 1) return;

            // The games copy the script's words into the work array and then zero the rest of it
            // (we_sys.c WEST_FUNC_CALL), so a routine handed fewer words than it reads sees zeros, and it
            // still runs. Padding here is what makes a short call behave the same way. Across both ROMs no
            // shipped call site is short enough to reach one of these zeros, so this changes nothing the
            // games do; it is for scripts somebody writes here.
            if (a.Length < 2 + WorkSlots)
            {
                var padded = new int[2 + WorkSlots];
                Array.Copy(a, padded, a.Length);
                a = padded;
            }

            int fn = a[0];
            _routinesRun.Add(fn);   // FUNC_CALL layout: [funcId, cnt, p0, p1, ...]; params start at index 2.
            if (FN_WE_MOVE.Contains(fn)) { MoveMon(a); return; }
            if (fn == FN_WE_057)
            {
                // drives one of the two wave actors created just before it (added via the simplified actor-add call).
                // It selects the CASTER's wave (player cap0/seq0 def (76,120); enemy cap1/seq1 def (144,64)), disables
                // the other, then animates SCALE + Y-pos + blend through the rise/hold/wash phases. Now rendered through
                // the general CATS actor path (BlitCellActors) like every other cell move, no legacy view overlay.
                int castCap = _attackerIsEnemy ? 1 : 0;
                _we057Actor = null;
                foreach (var act in _catsActors)
                {
                    if (act.CapId == castCap) { _we057Actor = act; }
                    else if (act.CapId == 0 || act.CapId == 1) { act.Visible = false; act.Alive = false; }   // the actor visibility-toggle call FALSE
                }
                var def = _attackerIsEnemy ? WE057_DEF_ENEMY : WE057_DEF_PLAYER;
                _cellDefX = def.x; _cellDefY = def.y;
                _cellPhase = 0; _cellFrame = 0;
                if (_we057Actor != null)
                {
                    if (_we057Actor.SeqCount > castCap) _we057Actor.SetSeq(castCap);   // the actor's set-animation-sequence call
                    _we057Actor.Visible = true; _we057Actor.X = _cellDefX; _we057Actor.Y = _cellDefY;
                    _we057Actor.ScaleX = 1; _we057Actor.ScaleY = 0.05; _we057Actor.Alpha = 0;
                }
                return;
            }
            // WE_T02/T22: scroll a background as an effect overlay. GPWork = a[2..]: [0]=BG_ID,
            // [1]=posX, [2]=posY, [3]=spdX, [4]=spdY, [5]=rev, [6]=bld_def, [7]=timer. Reverses for an enemy caster.
            if ((fn == FN_WE_T02 || fn == FN_WE_T22) && a.Length >= 10)
            {
                // GPWork a[2..]: [0]BG_ID [1]posX [2]posY [3]spdX [4]spdY [5]rev [6]bld [7]timer, all READ from the
                // effect. WeT02 negates pos/spd for an enemy caster (and uses the reverse tilemap) and nudges pos_y
                // by ±(START_Y_OFS/3*2)=±85; fades when pos_y crosses ±STOP_Y (±512/−412). bld → peak opacity (/31).
                bool rev = a[7] != 0 && _attackerIsEnemy;
                double sgn = rev ? -1 : 1;
                int ofs = WET02_START_Y_OFS / 3 * 2;
                double spdY = a[6] * sgn;
                StartBackground(a[2], overlay: true, posX: a[3] * sgn, posY: a[4] * sgn + (rev ? -ofs : ofs),
                    spdX: a[5] * sgn, spdY: spdY, peak: Math.Min(a[8], 16) / 16.0, fadeFrames: 12,
                    stopY: spdY < 0 ? WET02_STOP_Y_LO : WET02_STOP_Y_HI, useStop: true);   // peak = eva/16 (GX cap 16)
                return;
            }
            switch (fn)
            {
                case FN_WT_SHAKE when a.Length >= 6:
                {   // GPWork [0]powX [1]powY [2]sync [3]num [4]mode. Shakes the mode-selected MON sprite
                    // (WE_TOOL_M1=attacker, else target), or the BG frame if WE_TOOL_BG. pow is raw pixels (1:1).
                    int mode = a.Length > 6 ? a[6] : WE_TOOL_E1;
                    if ((mode & WE_TOOL_BG) != 0)
                    {
                        _monFx.Add(new MonFx { Kind = 5, Mon = 0, ToScene = true,
                            Sh = new Shake(a[2], a[3], Math.Max(1, a[4]), Math.Max(1, a[5])), NumMax = 0 });
                        break;
                    }
                    // The flag is a set, not a choice: STAGE shakes everybody. Nothing in either ROM asks
                    // for the background here, and only one site asks for STAGE, but the game does what
                    // the flag says rather than picking one.
                    foreach (int t in TargetsFromFlags(mode))
                        _monFx.Add(new MonFx { Kind = 5, Mon = t, ToScene = false,
                            Sh = new Shake(a[2], a[3], Math.Max(1, a[4]), Math.Max(1, a[5])), NumMax = 0 });
                    break;
                }
                case FN_BG_SHAKE when a.Length >= 6:
                {   // GPWork [0]powX [1]powY [2]sync [3]num [4]num_max [5]frame. Shakes the BG/effect
                    // frame scroll, re-running the whole shake num_max extra times (WeBgShake_TCB seq-- loop).
                    _monFx.Add(new MonFx { Kind = 5, Mon = 0, ToScene = true,
                        Sh = new Shake(a[2], a[3], Math.Max(1, a[4]), Math.Max(1, a[5])),
                        NumMax = a.Length > 6 ? Math.Max(0, a[6]) : 0 });
                    break;
                }

                // The two status overlays. Both scroll a background graphic behind the Pokemon and blend
                // it at 12 out of 16 (wsp_steff.c:296-298). Getting better scrolls it down at 3 a frame,
                // turning metallic scrolls it up at 6. GPWork[0] is which graphic, GPWork[1] is 0 for the
                // attacker and anything else for the defender (StatusEffect_Param_SetUp, wsp_steff.c).
                case 82:   // ST_EFF_RECOVER
                case 83:   // ST_EFF_METAL
                {
                    double speed = fn == 82 ? 3 : -6;
                    StartBackground(a[2], overlay: true, posX: 0, posY: 0, spdX: 0, spdY: speed,
                        peak: 12 / 16.0, fadeFrames: 12, stopY: 0, useStop: false);
                    // Its own task holds for STEFF_FADE_WAIT frames and then takes the blend down one
                    // step a frame from 12 to nothing (wsp_steff.c:58, :154 and the step after it), so
                    // about thirty-four frames all told. Without an end it would sit there for ever and
                    // the move would never finish.
                    _bgHoldLeft = 20;
                    break;
                }

                case FN_HAIKEI_PAL_FADE when a.Length >= 6:
                {   // Issues the palette-fade request (pfd, MAIN_BG, bit, WAIT=GPWork[1], START_EVY=GPWork[2],
                    // END_EVY=GPWork[3], COLOR=GPWork[4]). evy/16 = darkening toward COLOR; the fade ramps 1 evy step per
                    // `wait` frames so the duration = |end−start|·wait. (Was reading the args shifted, backgrounds didn't show.)
                    // GPWork[0] picks the palette set: 0 the backdrop, 1 the first effect layer, 2 the
                    // second. Only the backdrop is drawn here, so fading it for one of the others would
                    // colour the wrong thing. Two of HeartGold's 274 calls ask for the second layer.
                    if (a[2] != 0)
                    {
                        Note("This move fades an effect layer's colours, which the preview does not draw.");
                        break;
                    }
                    int wait = a[3];   // s8 in, may be NEGATIVE (Thunder uses -4)
                    int startEvy = a[4], endEvy = a.Length > 5 ? a[5] : 0;
                    _fadeStart = Math.Clamp(startEvy / 16.0, 0, 1);
                    _fadeEnd = Math.Clamp(endEvy / 16.0, 0, 1);
                    _fadeCur = _fadeStart;
                    // FadeReqSet: evy steps by DEF_FADE_VAL(=2) each update; a step happens every (wait+1)
                    // frames. wait<0 → fade_value = 2+|wait| and wait=0 (snappy: a big step EVERY frame). So Thunder's
                    // wait=-4 over evy 0→12 = step 6/frame = 2 frames (a sharp lightning flash), not 12/48.
                    int fadeVal = wait < 0 ? 2 + (-wait) : 2;
                    int effWait = wait < 0 ? 0 : wait;
                    int steps = Math.Max(1, (int)Math.Ceiling(Math.Abs(endEvy - startEvy) / (double)fadeVal));
                    _fadeFrames = _fadeFramesLeft = steps * (effWait + 1);
                    if (a.Length >= 7) { int c = a[6]; FadeR = R5(c); FadeG = G5(c); FadeB = B5(c); }
                    break;
                }

                case FN_POKEROTA:                                   // rotate the attacker (e.g. Peck)
                    _monFx.Add(new MonFx { Mon = _atVis, Frames = 16, Kind = 0 });
                    break;
                case FN_SSP_POKE_SCALE when a.Length >= 10:          // independent X/Y squash-stretch, N up-down cycles
                {   // GPWork: [0]flag [1]sclSX [2]sclEX [3]sclSY [4]sclEY [5]step [6](num&0xffff | wait<<16) [7](upF<<16|downF)
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
                case FN_CAP_POKE_SCALE when a.Length >= 9:           // Scales a dropped CAP up/down over time. GPWork
                {   // [0]at_df [1]alpha [2]scale_s [3]scale_e [4]scale_d(divisor) [5]num [6](upF<<16|downF) [7]capId.
                    int sd = Math.Max(1, a[6]);                     // scale = scale_s/scale_d → scale_e/scale_d (NOT /100)
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
                case FN_POKE_VANISH:                                // GPWork [0]target [1]flag
                    // (0=show, 1=hide). An INSTANT, PERSISTENT visibility set, not a blink.
                    {
                        int vm = MonFromFlag(a, 2);
                        if (vm >= 0) _monVanish[vm] = a.Length > 3 && a[3] != 0;
                    }
                    break;
                case FN_DISP_OUT:                                   // GPWork [0]target [1]wait. Slide
                    // the mon off-screen (X→−80 if on the bottom/own side, →336 if top) over `wait` frames; stays out.
                    {
                        int mon = MonFromFlag(a, 2), wait = Math.Max(1, a.Length > 3 ? a[3] : 1);
                        if (mon < 0) break;   // the flag picked nobody, so nothing happens
                        double restX = mon == 0 ? _atX : _dfX;
                        _monFx.Add(new MonFx { Mon = mon, Kind = 4, Frames = wait, Dx = OffscreenX(mon) - restX, Dy = 0 });
                    }
                    break;
                case FN_DISP_MOVE:                                   // GPWork [0]mode(0=out/else in)
                    // [1]target [2]wait. mode 0 slides off-screen; else snaps off-screen then slides back to default.
                    {
                        int mode = a.Length > 2 ? a[2] : 0, mon = MonFromFlag(a, 3), wait = Math.Max(1, a.Length > 4 ? a[4] : 1);
                        if (mon < 0) break;   // the flag picked nobody, so nothing happens
                        double restX = mon == 0 ? _atX : _dfX, off = OffscreenX(mon) - restX;
                        if (mode == 0) _monFx.Add(new MonFx { Mon = mon, Kind = 4, Frames = wait, Dx = off, Dy = 0 });
                        else { MonDX[mon] = off; _monFx.Add(new MonFx { Mon = mon, Kind = 4, Frames = wait, Dx = -off, Dy = 0 }); }
                    }
                    break;
                case FN_DISP_DEF:                                    // GPWork [0]target. Snap the mon
                    {
                        int dm = MonFromFlag(a, 2);
                        if (dm >= 0) MonDX[dm] = MonDY[dm] = 0;   // back to its default position, instantly.
                    }
                    break;
                case FN_PALCOL_CHANGE:                               // the grayscale-toggle handler: GPWork [0] !=0 → grayscale the
                    Grayscale = a.Length > 2 && a[2] != 0;           // scene palette, 0 → restore normal.
                    break;
                // ── Self-buff scale/shake routines, all on the ATTACKER (WeSysATNoGet). Ported from each
                //    routine's real the scale-rate keyframe helper phases / the shake initializer params. Kind 8 = scale-keyframe sequence.
                case 6:    // Bulk Up: flex, wide-short → narrow-tall → … → normal.
                    AddScaleSeq(_atVis, new[] { new double[]{100,150,100,50,10}, new double[]{150,50,50,150,10},
                        new double[]{50,100,150,100,5}, new double[]{100,150,100,150,5}, new double[]{150,100,150,100,5} });
                    break;
                case 13:   // Growth / Doom Desire's charge: pulse 100↔115 (6+6f ×4) AND flash
                           // the attacker WHITE in sync (SoftSpritePalFade evy 0→6→0, colour 0x7FFF), 2 blinks over the 4 steps.
                    AddScaleSeq(_atVis, new[] { new double[]{100,115,100,115,6}, new double[]{115,100,115,100,6} }, 4);
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = 6, Frames = 24, UpF = 6, WaitF = 0,
                        Keys = new double[] { 6 }, R = 255, G = 255, B = 255 });   // white flash synced with the pulse
                    break;
                case 14:   // Yoga pose: squash → stretch → normal (a scripted step sequence).
                    AddScaleSeq(_atVis, new[] { new double[]{100,150,100,50,8}, new double[]{150,50,50,150,8}, new double[]{50,100,150,100,8} });
                    break;
                case 15:   // stretch thin+tall (100→10 X, 100→180 Y) then collapse to nothing (a scripted step sequence).
                    AddScaleSeq(_atVis, new[] { new double[]{100,10,100,180,10}, new double[]{10,10,180,0,5} });
                    break;
                case 19:   // squash→stretch→normal jiggle, ×3 (a scripted step sequence, LOOP_CNT 3).
                    AddScaleSeq(_atVis, new[] { new double[]{100,120,100,80,5}, new double[]{120,100,80,120,5}, new double[]{100,100,120,100,5} }, 3);
                    break;
                case 5:    // Strength: the attacker SQUASHES 1.0→GPWork[0]/100 (+ a light shake),
                {          // pulses a colour WE070_FADE_CNT 3× (evy 10/16, col 0x1F = RED) while squashed, then STRETCHES to
                           // GPWork[1]/100 ("びよよーん" boing) and settles to 1.0. My old handler did ONLY the shake.
                    // What the routine itself reads is only two of these: WestSp_WE_070 (wsp_goto.c) passes
                    // GPWork[0] as the end scale and GPWork[2] as how many frames it takes, and never
                    // touches [1] or [3]. The stretch below comes from the task that runs afterwards and
                    // has not been re-checked against it.
                    int sq = a.Length > 2 ? a[2] : 70, st = a.Length > 3 ? a[3] : 120;   // GPWork[0] end scale (/100)
                    int sqSync = a.Length > 4 ? Math.Max(1, a[4]) : 10, stSync = a.Length > 5 ? Math.Max(1, a[5]) : 5;   // GPWork[2] frames
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = 5, Sh = new Shake(2, 0, 1, 4), NumMax = 0 });
                    AddScaleSeq(_atVis, new[] { new double[]{100,sq,100,sq,sqSync}, new double[]{sq,sq,sq,sq,18},
                        new double[]{sq,st,sq,st,stSync}, new double[]{st,100,st,100,5} });
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = 6, Frames = 18, UpF = 3, WaitF = 0, Cycles = 3,
                        Keys = new double[] { 10 }, R = 255, G = 0, B = 0, Delay = sqSync });   // 3 red effort-pulses while squashed
                    break;
                }
                case 24:   // Earthquake: the whole world shakes (mons + BG) at a decreasing amplitude
                           // WHILE the background flashes black↔white each step, not a plain mon shake. (Kind 20.)
                    _monFx.Add(new MonFx { Kind = 20, Frames = 40 });
                    break;
                case 7:    // Double Team: 4 gray afterimages of the attacker oscillate ±32px then fade.
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = 9, Frames = 80 });
                    break;
                // ── bespoke routines, ported from their real #define params ──
                case 8:    // Rolling Kick: attacker rolls, the rotation-motion builder ROTA_NUM 1 turn over SYNC 8, dir=vec_x,
                    // WITH WE098_OAM_MAX 2 zanzou after-image trails (DO_WAIT 2f apart), set via NumMax.
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = 22, Frames = 8, Cycles = 1, Dx = _atVis == 0 ? 1 : -1, NumMax = 2 });
                    break;
                case 9:    // Drill Peck: the attacker JABS −32·vec horizontally (out over MOVE_SYNC
                {          // 3, back over MOVE2_SYNC 2) via SetSspMatrix AND tilts SS_PARA_ROT_Z 0→20° then un-tilts, a drill.
                    double s = _atVis == 0 ? 1.0 : -1.0;   // MOVE_WIDTH −32 · vec_x
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = 10, Frames = 14, Keys = new double[] { 20, 1 } });   // tilt out & back
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = 13, Frames = 3 + 7 + 2, UpF = 3, WaitF = 7, DownF = 2, Dx = -32 * s, Dy = 0 });
                    break;
                }
                case 10:   // Submission: the rotation-motion builder, the GPWork[2]-flagged mon ROLLS GPWork[0](=7) turns over
                    // GPWork[1](=10) sync = 70f. Called once for the attacker (dir reversed, work[2]*=−1) and once for the
                    // defender. NOT a scale (my old handler scaled GPWork[0]→[1], which was flatly wrong).
                    if (a.Length >= 5)
                    {
                        int rn = Math.Max(1, a[2]), sync = Math.Max(1, a[3]);
                        double dir = (a[4] & WE_TOOL_M1) != 0 ? -1 : 1;
                        int om = MonFromFlag(a, 4);
                        if (om >= 0) _monFx.Add(new MonFx { Mon = om, Kind = 22, Frames = sync * rn, Cycles = rn, Dx = dir });
                    }
                    break;
                case 58:   // defender shrinks to 20% (the scale-rate keyframe helper 100→20) over 10 (a vanish/shrink).
                    AddScaleSeq(_dfVis, new[] { new double[] { 100, 20, 100, 20, 10 } });
                    break;
                case 31:   // Swagger: attacker scales UP 1.0→1.5 (S/D 10/10 → E/D 15/10) over SYNC 8,
                    // holds SCALE_POKE_WAIT 4, then DOWN 1.5→1.0 over 8 (WE207_AT_SCALE_UP/WAIT/SCALE_DOWN).
                    AddScaleSeq(_atVis, new[] { new double[]{100,150,100,150,8}, new double[]{150,150,150,150,4}, new double[]{150,100,150,100,8} });
                    break;
                case 46:   // base: attacker shake (WE224AT_SHAKE_X 4, NUM 4, SYNC 1).
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = 5, Sh = new Shake(4, 0, 1, 4), NumMax = 0 });
                    break;
                case 47:   // Megahorn attacker, We_224ATTCB: vibrate in place (shake 4,0,sync1,num4 ≈16f),
                {          // THEN charge toward the defender by (MOVE_X 40, MOVE_Y −7)·vec over SYNC 4, hold WAIT_NUM 8, return 4.
                    double s = _atVis == 0 ? 1.0 : -1.0;   // the X-vector flip helper/Y: player side +1, enemy side −1 (both axes)
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = 5, Sh = new Shake(4, 0, 1, 4), NumMax = 0 });
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = 13, Frames = 4 + 8 + 4, UpF = 4, WaitF = 8, DownF = 4,
                                           Dx = 40 * s, Dy = -7 * s, Delay = 16 });
                    break;
                }
                case 48:   // Megahorn defender, We_224DFTCB: knocked back by (MOVE_X −40, MOVE_Y +16)·vec over
                {          // SYNC 4, shakes (4,0,sync1,num4) at the displaced spot, then returns over 4 (MOVE1→SHAKE→MOVE2).
                    double s = _dfVis == 0 ? 1.0 : -1.0;
                    _monFx.Add(new MonFx { Mon = _dfVis, Kind = 13, Frames = 4 + 16 + 4, UpF = 4, WaitF = 16, DownF = 4,
                                           Dx = -40 * s, Dy = 16 * s });
                    _monFx.Add(new MonFx { Mon = _dfVis, Kind = 5, Sh = new Shake(4, 0, 1, 4), NumMax = 0, Delay = 4 });
                    break;
                }
                case 55:   // Camouflage: NOT a colour tint, a window-OBJ + translucency fade. The mon
                    // fades to ~EVA_E/16 = 2/16 opacity over WE293_EV_FRAME 16 (blends into the background = "camouflage").
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = 16, Frames = 16, Keys = new double[] { 16, 2 } });
                    break;
                case 26:   // slide the mon (GPWork[0]==0 ? defender : attacker) by OFS (−20,+20) over SYNC 20.
                    _monFx.Add(new MonFx { Mon = (a.Length > 2 && a[2] == 0) ? _dfVis : _atVis, Kind = 4, Frames = 20, Dx = -20, Dy = 20 });
                    break;
                case 27:   // shake the sep-selected mon with GPWork params (st.x,y,w,n = ampX,ampY,sync,num).
                    if (a.Length >= 7)
                        _monFx.Add(new MonFx { Mon = a[2] == 0 ? _atVis : _dfVis, Kind = 5,
                            Sh = new Shake(a[3], a[4], Math.Max(1, a[5]), Math.Max(1, a[6])), NumMax = 0 });
                    break;
                case 28:   // Magnitude: the shake initializer(2+pow, pow, sync→1, 10) shakes the mons AND
                {   // scrolls the background (GF_BGL_ScrollSet), i.e. the whole world shakes. pow maps the move's power
                    // exactly per source: 150→6, 110→5, 90→4, 70→3, 50→2, 30→1, else 0 (WazaEffParaGet(WE_PARA_POW)).
                    int pow = MovePower switch { 150 => 6, 110 => 5, 90 => 4, 70 => 3, 50 => 2, 30 => 1, _ => 0 };
                    _monFx.Add(new MonFx { Kind = 5, ToScene = true, Sh = new Shake(2 + pow, pow, 1, 10), NumMax = 0 });
                    break;
                }
                case 11:   // defender hit, shake (2,0,sync1,DFNUM6) + Z-scale 1.0→1.2x/1.5y then back.
                    _monFx.Add(new MonFx { Mon = _dfVis, Kind = 5, Sh = new Shake(2, 0, 1, 6), NumMax = 0 });
                    AddScaleSeq(_dfVis, new[] { new double[]{100,120,100,150,7}, new double[]{120,100,150,100,4} });
                    break;
                // WE_148: the background whitens and the attacker darkens together, both hold five frames,
                // then both come back (We148_TCB, wsp_goto.c). The background colour is 0x7FFF (white) and
                // the sprite's is 0x0000 (black); the background's wait of -2 means it steps fast rather
                // than slowly, the same reading the palette-fade routine uses.
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
                case 17:   // attacker scales 1.4→1.0 (START14/END10) over SYNC 8 (appears + settles).
                    AddScaleSeq(_atVis, new[] { new double[]{140,100,140,100,8} });
                    break;
                case 18:   // defender shake (WIDTH4,sync1,NUM4) + flash to black (BLACK_FADE 0→16).
                    _monFx.Add(new MonFx { Mon = _dfVis, Kind = 5, Sh = new Shake(4, 0, 1, 4), NumMax = 0 });
                    _monFx.Add(new MonFx { Mon = _dfVis, Kind = 3, Frames = 16, R = 0, G = 0, B = 0 });
                    break;
                case 23:   // Feint Attack / "DAMASIUTI": the attacker goes translucent and
                           // ORBITS an ellipse 2× over 32f (ROTA_NUM 2 × ROTA_SYNC 16) while FADING OUT (alpha 16→0),
                           // the defender takes damage, then it fades back in, a deceptive disappear-and-strike. (Was
                           // wrongly an in-place 720° sprite spin.)
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = 7, Frames = 32, Keys = new double[] { 24, 8, 16, 0 } });   // ellipse orbit ×2
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = 21, Frames = 64, Keys = new double[] { 0.0, 0 } });          // fade out → hold → back in
                    break;
                case 25:   // the GPWork[0]-mon rocks ±15° (ROTA_E) MOVE_NUM 4 times, SYNC 3 (≈12 frames),
                    // around an off-centre pivot (ROT_CY 50). GPWork[0]==0 → attacker, else defender.
                    _monFx.Add(new MonFx { Mon = (a.Length > 2 && a[2] == 0) ? _atVis : _dfVis, Kind = 10, Frames = 12, Keys = new double[] { 15, 4 } });
                    break;
                case 29:   // attacker vertical shake (SHAKE_Y 32, ONE_SYNC 6, NUM 4).
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = 5, Sh = new Shake(0, 32, 6, 4), NumMax = 0 });
                    break;
                case 30:   // Vital Throw: attacker ROLLS (the rotation-motion builder 1 turn over ROTA_SYNC 64),
                {          // THEN lunges toward the defender (STRAIGHT_MOVE_X 32·vec over SYNC 2), the defender is knocked the
                           // same 32·vec (2f), and the attacker returns over MOVE1_SYNC 8, the grab-and-throw.
                    double sAt = _atVis == 0 ? 1.0 : -1.0;   // both the attacker's lunge AND the defender's knock use the
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = 22, Frames = 64, Cycles = 1, Dx = sAt });   // attacker's vec_x
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = 13, Frames = 2 + 2 + 8, UpF = 2, WaitF = 2, DownF = 8,
                                           Dx = 32 * sAt, Dy = 0, Delay = 64 });
                    _monFx.Add(new MonFx { Mon = _dfVis, Kind = 13, Frames = 2 + 2 + 8, UpF = 2, WaitF = 2, DownF = 8,
                                           Dx = 32 * sAt, Dy = 0, Delay = 66 });
                    break;
                }
                case 32:   // attacker stretches thin+tall (X→0.1, Y→2.0) then settles back (SCALEOUT/IN).
                    AddScaleSeq(_atVis, new[] { new double[]{100,10,100,200,6}, new double[]{20,100,200,100,8} });
                    break;
                case 37:   // Extrasensory / じんつうりき: a DEFLASTER per-scanline warp of the
                    // defender, a horizontal sine bulge (angle 180°→360° over SIZE_Y) + a shear, shimmering, over
                    // WE326_CHANGE_NUM 3 phases × CHANGE_WAIT 16f. (Was a bitmap Z-spin; now the faithful raster twist.)
                    _monFx.Add(new MonFx { Mon = _dfVis, Kind = 23, Frames = 3 * 16 });
                    break;
                case 12:   // Acid Armor / "TOKERU" = melt: the attacker DISSOLVES, a ScrLaster per-scanline
                           // sine ripple (WE151_ROTA_WIDTH 8, ROTA_ADD 5°/row, scrolling) PLUS a blend-alpha fade out → hold
                           // → fade in (RASTER/FADE_OUT → WAIT → FADE_IN). Ripple = Kind 24, dissolve = Kind 21.
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = 21, Frames = 44, Keys = new double[] { 0.1, 1 } });
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = 24, Frames = 44 });   // the melt ripple runs the whole dissolve
                    break;
                case 67:   // the rectangular wipe-reveal handler: vertical wipe-reveal of the GPWork[0]-mon over `wait` (GPWork[4]); GPWork[3] sign = dir.
                {   // my>0 → reveal top-down; my≤0 → bottom-up. SoftSpriteVisibleSet grows the visible band.
                    int mon = MonFromFlag(a, 2), wait = Math.Max(1, a.Length > 6 ? a[6] : 8);
                        if (mon < 0) break;   // the flag picked nobody, so nothing happens
                    int my = a.Length > 5 ? a[5] : 1;
                    _monFx.Add(new MonFx { Mon = mon, Kind = 15, Frames = wait, Dx = my > 0 ? 1 : -1 });
                    break;
                }
                case 22:   // 4 gray copies of the attacker shrink away staggered then re-expand.
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = 14, Frames = 28 });
                    break;
                case 20:   // Spite: the DEFENDER is hidden and shown as a translucent ghost
                           // copy that fades in to ~50% (EVA 0→8) and back out (WE180 alpha fade), not an attacker orbit.
                    _monFx.Add(new MonFx { Mon = _dfVis, Kind = 21, Frames = 48, Keys = new double[] { 0.5, 0 } });
                    break;
                case 21:   // attacker flashes WHITE (the mon color-change call 256,256,256) + shrinks to 5% (window).
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = 3, Frames = 12, R = 255, G = 255, B = 255 });
                    AddScaleSeq(_atVis, new[] { new double[]{100,5,100,5,5}, new double[]{5,100,5,100,5} });
                    break;
                case 59:   // Shadow Punch: a translucent gray shadow copy of the attacker
                           // LUNGES forward 48px toward the target and back (the straight-line sync-move helper 0→+48→0), the punch.
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = 3, Frames = 20, R = 196, G = 196, B = 196 });   // gray/shadow tint
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = 13, Frames = 20, UpF = 10, DownF = 10,
                        Dx = 48 * (_atVis == 0 ? 1 : -1), Dy = 0 });   // forward-and-back punch lunge
                    break;
                case 38:   // fade the cap_bit-selected mons' alpha (the alpha-fade starter a1_s→a1_e/16).
                {   // GPWork [0]cap_bit [1]a1_s [2]a1_e [3]a2_s [4]a2_e [5]sync.
                    int capBit = a.Length > 2 ? a[2] : 1, a1s = a.Length > 3 ? a[3] : 16, a1e = a.Length > 4 ? a[4] : 16, sync = Math.Max(1, a.Length > 7 ? a[7] : 8);
                    for (int b = 0; b < 2; b++) if ((capBit & (1 << b)) != 0)
                        _monFx.Add(new MonFx { Mon = b, Kind = 16, Frames = sync, Keys = new double[] { a1s, a1e } });
                    break;
                }
                case 41:   // Wish: BG flashes WHITE, the palette-fade request to 0xffff is INSTANT (BRIN_SYNC 0),
                    _fadeStart = 1.0; _fadeEnd = 0; _fadeCur = 1.0; _fadeFrames = _fadeFramesLeft = 8;   // then fades out over BROUT_SYNC 8.
                    FadeR = FadeG = FadeB = 255;
                    break;
                case 43:   // fade the poke alpha (WE252_CAP_ALPHA 0→16 over CAP_SYNC 8).
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = 16, Frames = 8, Keys = new double[] { 0, 16 } });
                    break;
                case 79:   // handler 166: defender translucent (SS_PARA_ALPHA 8 = 0.5) for the W166_Tcb window-reveal loop:
                    // (YOFS_MAX 38 + 1) cycles × 4 frames ≈ 156, then alpha restored.
                    _monFx.Add(new MonFx { Mon = _dfVis, Kind = 16, Frames = 156, Keys = new double[] { 8, 8 } });
                    break;
                case 56:   // Superpower aura,: the actor scale-set call(cap0,1.2) in OBJWND window
                    // mode → an aura silhouette behind the mon. The TCB is vestigial (sets up then tears down), so it's a
                    // brief static aura, scale the dropped cap-0 copy 1.2× with a soft white glow.
                    if (_caps.TryGetValue(0, out var t08cap))
                    { t08cap.ScaleX = t08cap.ScaleY = 1.2; t08cap.TintR = t08cap.TintG = t08cap.TintB = 255; t08cap.TintA = 0.4; }
                    break;
                case 75:   // the OAM-view drag/sink handler: GPWork [0]cap [1]wait [2]bg_type [3]soft_pri [4]drop_para
                    // [5]callback [6]target. soft_pri = z-order (lower = in front). In 1v1 a view of the M2/E2
                    // (ally) copy is disabled outright, exactly like the source's 2vs2 guard.
                    if (a.Length > 2 && _caps.TryGetValue(a[2], out var pvCap))
                    {
                        int para75 = a.Length > 6 ? a[6] : -1;
                        if (para75 == 2 || para75 == 3) { pvCap.Visible = false; break; }   // WEDEF_DROP_M2/E2 in singles
                        if (a.Length > 5 && a[5] >= 0 && a[5] != 0xFF) pvCap.Priority = a[5];
                        // callback != 0 → the sink-into-void step function (Dark Void): the defender's copy is dragged down into
                        // the void in jittered +4/+8 steps, then sinks continuously and is swallowed past y≈130.
                        // The source also sets a hardware WINDOW whose inside-plane EXCLUDES OBJ, the copy is
                        // hidden wherever it overlaps the rect, so it visibly sinks below that line:
                        // target 0 → G2_SetWnd0Position(0,160,128,192); else (128,86,256,192).
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
                case 78:   // move 425: drop EVERY mon into an OAM cap so a following effect can move
                    // them all at once. In the 1v1 preview that's the attacker + defender.
                    _caps[0] = new DroppedCap { SrcMon = _atVis };
                    _caps[1] = new DroppedCap { SrcMon = _dfVis };
                    break;
                case 70:   // handler 272 (Role Play): attacker's white image appears at the defender (cap[0] at cap[1]−32px),
                    // fading in over the PaletteSoftFade (num 16 → 0→15 evy).
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = 17, Frames = 16 });
                    break;
                case 76:   // per-scanline raster sine wave on the BG for GPWork[0] frames (lst_wait_max).
                    _rasterLeft = Math.Max(1, a.Length > 2 ? a[2] : 60); RasterAmp = 32; RasterPhase = 0;
                    break;
                case 71:   // handler 289 (Snatch): the attacker dashes off one edge (±(255+80)) and back from the other (∓80).
                {   // vec = +1 for the player side (off right first), −1 for the enemy side.
                    int vec = _atVis == 0 ? 1 : -1;
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = 18, Frames = 45,   // 3 × the straight-line sync-move helper sync 15
                        Dx = vec > 0 ? 255 + 80 : 0 - 80, Dy = vec > 0 ? 0 - 80 : 255 + 80 });
                    break;
                }
                case 63:   // ramp the (dropped CAP's) OAM palette toward `col` over `wait`.
                {   // GPWork [0]mode(WE_TOOL_C* = cap) [1]wait [2]param [3]start [4]end [5]col.
                    int mode = a.Length > 2 ? a[2] : 0, wait = Math.Max(1, a.Length > 3 ? a[3] : 1);
                    int start = a.Length > 5 ? a[5] : 0, end = a.Length > 6 ? a[6] : 16, col = a.Length > 7 ? a[7] : 0;
                    int capId = CapIdFromToolFlag(mode);
                    DroppedCap cap = (capId >= 0 && _caps.TryGetValue(capId, out var dc)) ? dc : null;
                    _monFx.Add(new MonFx { Mon = capId >= 0 ? 0 : Math.Max(0, MonFromFlag(a, 2)), Cap = cap, Kind = 12, Frames = wait,
                        Keys = new double[] { start, end }, R = R5(col), G = G5(col), B = B5(col) });
                    break;
                }
                case 73:   // EMIT_SIMPLE_UD (wsp_tool.c:3881): move an emitter straight between the mon and a point
                {   // 60px above the top of the screen, once, over `time` frames after `wait` frames of delay. Mode
                    // picks the direction: 0 comes down onto the mon, anything else rises away from it. Both ends
                    // share the mon's x, so nothing moves sideways. The tick is the same one EMIT_STRAIGHT uses.
                    var sim = FindEmitter(a.Length > 2 ? a[2] : 0);
                    double monY73 = (a.Length > 3 && a[3] == 0) ? _atY : _dfY;
                    int mode73 = a.Length > 4 ? a[4] : 0;
                    int time73 = Math.Max(1, a.Length > 5 ? a[5] : 16);
                    int wait73 = a.Length > 6 ? Math.Max(0, a[6]) : 0;
                    double amp73 = monY73 + 60;   // +Y is up here: from the mon at screen monY to screen −60
                    sim?.SetEmitterMotion(f =>
                    {
                        double t = Math.Clamp((f - wait73) / (double)time73, 0, 1);
                        return (0, mode73 == 0 ? amp73 * (1 - t) : amp73 * t);
                    });
                    break;
                }
                case 69:   // the mosaic-level handler: pixelate the mon, ramp level GPWork[2]→(15 if add>0 else 0) by add/frame.
                {   // GPWork [0]cap_id [1]add [2]h_start [3]v_start. G2_SetOBJMosaicSize(h,v): block = level+1.
                    int capId = a.Length > 2 ? a[2] : 0, add = a.Length > 3 ? a[3] : 1, hs = a.Length > 4 ? a[4] : 0;
                    double end = add < 0 ? 0 : 15;
                    int frames = Math.Max(1, (int)Math.Ceiling(Math.Abs(end - hs) / Math.Max(1, Math.Abs(add))));
                    DroppedCap cap = _caps.TryGetValue(capId, out var dc) ? dc : null;   // WeSysPokeCapGet(cap_id)
                    _monFx.Add(new MonFx { Mon = cap != null ? cap.SrcMon : (capId & 1), Cap = cap, Kind = 11, Frames = frames + 1, Keys = new double[] { hs, end, add } });
                    break;
                }
                case 39:   // shake the attacker (the shake initializer 20,0,…,10) + the defender (2,0,…,10).
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = 5, Sh = new Shake(20, 0, 1, 10), NumMax = 0 });
                    _monFx.Add(new MonFx { Mon = _dfVis, Kind = 5, Sh = new Shake(2, 0, 1, 10), NumMax = 0 });
                    break;
                case FN_KAITEN when a.Length >= 5:                  // the mon traces an
                {   // ellipse, the rotation-motion builder(rota_num, sync) then halves the widths → the rotation-fx calculator gives
                    // x = 16·sin(θ), y = −4·cos(θ); base raised +8 (poke.p.y −= ROTA_W_Y=−8). θ advances 2π/sync per
                    // frame for rota_num revolutions (work[0]=sync·rota_num total). GPWork [0]target [1]rota_num [2]sync.
                    int mon = MonFromFlag(a, 2), rotaNum = Math.Max(1, a[3]), sync = Math.Max(1, a[4]);
                        if (mon < 0) break;   // the flag picked nobody, so nothing happens
                    _monFx.Add(new MonFx { Mon = mon, Kind = 7, Frames = sync * rotaNum, Keys = new double[] { 16, -4, sync, 8 } });
                    break;
                }
                case FN_WE_T03:                                     // Blink: GPWork [0]num [1]wait. Toggles
                    // the ATTACKER's visibility every `wait` frames, num·2 times, then restores visible.
                    {
                        int num = Math.Max(1, a.Length > 2 ? a[2] : 1) * 2, wait = Math.Max(1, a.Length > 3 ? a[3] : 1);
                        _monFx.Add(new MonFx { Mon = _atVis, Kind = 2, UpF = wait, Cycles = num, Frames = num * wait });
                    }
                    break;
                case FN_SSP_POKE_PAL_FADE when a.Length >= 6:
                {   // GPWork [0]target [1]fade_wait [2]count [3]rgb [4]evy_max(/16) [5]wait.
                    // Blink: ramp tint 0→evy_max over fade_wait frames, hold `wait`, ramp back to 0, repeated `count`×.
                    int fadeWait = Math.Max(0, a[3]), count = Math.Max(1, a[4]), col = a[5];
                    int evyMax = a.Length > 6 ? a[6] : 8, wait = a.Length > 7 ? a[7] : 0;
                    int cyc = Math.Max(1, 2 * fadeWait + wait);
                    _monFx.Add(new MonFx { Mon = Math.Max(0, MonFromFlag(a, 2)), Kind = 6, Frames = count * cyc,
                        UpF = fadeWait, WaitF = wait, Cycles = count, Keys = new[] { (double)evyMax },
                        R = R5(col), G = G5(col), B = B5(col) });
                    break;
                }

                // EMIT_ROTATION: the emitter orbits an ellipse (rx,ry) sweeping rad_s→rad_e over `wait` frames →
                // particles laid along a circular/spiral path. args=[idx, radSx, radEx, radSy, radEy, rx, ry, wait, …]
                case FN_EMIT_ROTATION when a.Length >= 10:
                {
                    var sim = FindEmitter(a[2]);
                    if (sim == null) break;
                    // GPWork[1..6] are angles in DEGREES (the C does FX_GET_ROTA_NUM = x·0xffff/360) and rx/ry are
                    // PIXEL radii (the emitter's rotation-field handler). The emitter traces an ellipse: offset.x =
                    // sin(angX)·rx, offset.y = cos(angY)·ry (the rotation-fx calculator) over `wait` frames.
                    double radSx = a[3], radEx = a[4], radSy = a[5], radEy = a[6];
                    double rx = a[7], ry = a[8];
                    int wait = Math.Max(1, a[9]);
                    // GPWork[8] (a[10]) = target mon (0 = attacker/self, else defender): the orbit is centred on THAT mon
                    // (WET_PokeParticlePosGet(s_client)), which may differ from where the emitter spawned, e.g. Fire
                    // Screw (we_463) emits at the attacker but orbits the DEFENDER. Re-centre via a constant shift.
                    int rotTgt = a.Length > 10 ? a[10] : 0;
                    // GPWork[9] picks which set of particles to swing. Nine of HeartGold's 105 calls ask
                    // for one other than the first, and only the first is simulated.
                    if (a.Length > 11 && a[11] != 0)
                        Note("This move swings a second set of particles, which the preview does not show.");
                    double rtX = rotTgt == 0 ? _atX : _dfX, rtY = rotTgt == 0 ? _atY : _dfY;
                    double shX = rtX - sim.AnchorX, shY = sim.AnchorY - rtY;   // sim space (+Y up)
                    sim.SetEmitterMotion(f =>
                    {
                        double t = Math.Min(1.0, (double)f / wait);
                        double angX = (radSx + (radEx - radSx) * t) * Math.PI / 180.0;
                        double angY = (radSy + (radEy - radSy) * t) * Math.PI / 180.0;
                        return (shX + rx * Math.Sin(angX), shY + ry * Math.Cos(angY));
                    });
                    break;
                }

                // EMIT_STRAIGHT / PARABOLIC (EmitMove_Init): the emitter travels from the start client to
                // the other over `time` frames, GPWork [0]=emit_id [3]=wait [4]=time [5]=height(px) [6]=target
                // (0=attacker start). STRAIGHT is a straight line; PARABOLIC arcs up to `height` px at mid-flight.
                case FN_EMIT_STRAIGHT when a.Length >= 7:
                case FN_EMIT_PARABOLIC when a.Length >= 7:
                {
                    var sim = FindEmitter(a[2]);
                    if (sim == null) break;
                    int time = Math.Max(1, a[6]);
                    double height = a[7];
                    int target = a.Length > 8 ? a[8] : 0;
                    double sX = target == 0 ? _atX : _dfX, sY = target == 0 ? _atY : _dfY;
                    double eX = target == 0 ? _dfX : _atX, eY = target == 0 ? _dfY : _atY;
                    double ddx = eX - sX, ddy = eY - sY;            // screen displacement start→end
                    bool arc = fn == FN_EMIT_PARABOLIC;
                    sim.SetEmitterMotion(f =>
                    {
                        double t = Math.Min(1.0, (double)f / time);
                        double screenY = ddy * t + (arc ? -height * 4 * t * (1 - t) : 0);   // arc peaks up at mid-flight
                        return (ddx * t, -screenY);   // sim is +Y-up; screen +Y is down → negate
                    });
                    break;
                }
            }
        }

        // Shared "move the Pokémon" routines (WE_T*). Layout: [funcId, cnt, wait, ofs_x, (ofs_y), type]; cnt=3 is an
        // X-only move (T05-like), cnt=4 adds Y (T10). type (last word) is the WE_TOOL flag picking the mon; ofs_x is
        // "forward" (toward the opponent), so it flips by side. The lunge persists (out then back via a second call).
        private void MoveMon(int[] a)
        {
            int cnt = a.Length >= 2 ? a[1] : 0;
            if (cnt < 2 || a.Length < 2 + cnt) return;
            int wait = Math.Max(1, a[2]);
            int ofsx = a[3];
            int ofsy = cnt >= 4 ? a[4] : 0;
            int type = a[1 + cnt];                       // last payload word = WE_TOOL flag
            int mon = (type & WE_TOOL_M1) != 0 ? _atVis : (type & WE_TOOL_E1) != 0 ? _dfVis : -1;
            if (mon < 0) return;
            // the X-vector flip helper: battle mode flips BOTH offsets for an enemy-side client
            // (visual 1 = top). WE_T10 and friends multiply x AND y by it, e.g. Dark Void's enemy-branch
            // defender drag (+4 steps then −80 return) plays unflipped on the player-side defender.
            double sign = mon == 0 ? 1.0 : -1.0;
            _monFx.Add(new MonFx { Mon = mon, Frames = wait, Kind = 4, Dx = ofsx * sign, Dy = ofsy * sign });
        }

        // Add an afterimage ghost of `mon` offset by `dx` (px), at `alpha`, recoloured toward a flat gray `g`
        // (the mon color-change call to 128 = mid-gray, 196 = light-gray copies). TintA fixed high so the copy reads as a shadow.
        private void AddGhost(int mon, double dx, double alpha, byte g)
            => _ghosts.Add(new MonGhost { Mon = mon, Dx = dx, Dy = 0, ScaleX = 1, ScaleY = 1, Alpha = alpha,
                                          TintR = g, TintG = g, TintB = g, TintA = 0.7 });

        // Add a scale-keyframe-sequence MonFx (the scale-rate keyframe helper phases). `phases` rows = [sxS,sxE,syS,syE,frames]
        // in /100 units; `repeat` re-runs the whole list (WE_074's CNT_MAX). Faithful to the source's squash/stretch flex.
        private void AddScaleSeq(int mon, double[][] phases, int repeat = 1)
        {
            if (mon < 0 || phases == null || phases.Length == 0) return;
            if (repeat < 1) repeat = 1;
            var seq = new double[phases.Length * repeat][];
            for (int r = 0; r < repeat; r++) for (int i = 0; i < phases.Length; i++) seq[r * phases.Length + i] = phases[i];
            int total = 0; foreach (var p in seq) total += Math.Max(1, (int)p[4]);
            _monFx.Add(new MonFx { Mon = mon, Kind = 8, Phases = seq, Frames = total });
        }

        // GX RGB555 → 8-bit channels (WE_PAL_GREEN/POISON/WHITE/… colour words).
        private static byte R5(int c) => (byte)((c & 0x1F) << 3);
        private static byte G5(int c) => (byte)(((c >> 5) & 0x1F) << 3);
        private static byte B5(int c) => (byte)(((c >> 10) & 0x1F) << 3);

        // Which sprite a routine targets, from its WE_TOOL flag arg (M1=attacker, E1=defender; defaults to defender).
        /// <summary>
        /// Every Pokemon a target flag picks out, in the order WT_SSPointerGet (we_tool.c:1431) picks them.
        ///
        /// STAGE means all of them and OTHER means everyone but the attacker, which in a single battle is
        /// just the defender. M2 and E2 are the allies and only exist in a double battle, so a flag asking
        /// only for one of those comes back empty and the caller does nothing, the same as the game.
        /// Across HeartGold's own scripts that is 37 call sites: 28 of WT_SHAKE, 4 of WE_T05, 5 of WE_T10.
        /// Only the WT_SHAKE ones were being got wrong; MoveMon already stopped on an ally-only flag.
        /// </summary>
        private List<int> TargetsFromFlags(int flag) => WestTargetFlags.Targets(flag, _atVis, _dfVis);

        /// <summary>The first Pokemon a target flag picks out, or -1 when it picks out none.</summary>
        private int MonFromFlag(int[] a, int idx)
        {
            int flag = idx < a.Length ? a[idx] : WE_TOOL_E1;
            var t = TargetsFromFlags(flag);
            return t.Count > 0 ? t[0] : -1;
        }

        // WEST_CATS_ACT_ADD[_EZ]: create a cell actor at the defender position. EZ keeps it in slot cap_id (a later
        // FUNC_CALL/SetSeq drives it); ACT_ADD's actor is driven by its CATS callback (ported separately). The actor
        // plays NANR sequence = cap_id when that bank exists (Surf: cap 0 = player wave, cap 1 = enemy wave), else 0.
        private void CatsActAdd(int idOrCap, bool withCallback, int[] gp)
        {
            var seqs = CellSeqs;
            if (seqs.Length == 0) return;
            if (withCallback)
            {
                // ACT_ADD: actor driven by CATS callback `idOrCap` (the opcode dispatch table). Default = visible, playing
                // seq 0 at the defender (the faithful base for the many marks/slashes that just play at the target);
                // a ported driver refines pos/scale/visibility. Un-ported callbacks keep the base behaviour.
                var actor = new CellActor(seqs, 0) { FuncId = idOrCap, Gp = gp ?? Array.Empty<int>(),
                    X = _dfX, Y = _dfY, BaseX = _dfX, BaseY = _dfY, CapId = 0 };
                SetupCats(actor);
                _catsActors.Add(actor);
            }
            else
            {
                int seq = (idOrCap >= 0 && idOrCap < seqs.Length) ? idOrCap : 0;
                _catsActors.Add(new CellActor(seqs, seq) { CapId = idOrCap, X = _dfX, Y = _dfY, BaseX = _dfX, BaseY = _dfY });
            }
        }

        // CATS callback ids (the opcode dispatch table order, CATS section).
        private const int FN_CSP_WE_081 = 1, FN_CSP_WE_134 = 2, FN_CSP_WE_271 = 3, FN_CSP_WE_118 = 4, FN_CSP_WE_132 = 5,
                          FN_CSP_WE_155 = 6, FN_CSP_WE_184 = 7, FN_CSP_WE_193 = 8, FN_CSP_WE_199 = 9, FN_CSP_WE_207_SUB = 10,
                          FN_CSP_WE_212 = 11, FN_CSP_WE_259 = 12, FN_CSP_WE_226 = 13, FN_CSP_WE_333 = 17, FN_CSP_WE_232 = 22,
                          FN_CSP_FREE = 25, FN_CSP_266 = 26, FN_CSP_090 = 27, FN_CSP_WE_269 = 19,
                          FN_CSP_WE_288 = 15, FN_CSP_WE_320 = 16, FN_CSP_WE_270 = 20, FN_CSP_WE_274 = 21, FN_CSP_WE_338 = 24,
                          FN_CSP_WE_275 = 23, FN_CSP_WE_286 = 14, FN_CSP_WE_252 = 18;
        private static readonly int[][] BindingBandWaitSteps = { new[] { 8, 2 }, new[] { 13, 1 }, new[] { 18, 3 } };

        // Per-callback spawn-time setup: initial visibility/position, and any SIBLING actors the callback creates.
        private void SetupCats(CellActor leader)
        {
            switch (leader.FuncId)
            {
                case FN_CSP_WE_207_SUB:
                    leader.Visible = false;   // the actor visibility-toggle call(FALSE) until the driver pops it in
                    break;
                case FN_CSP_WE_226:   // Baton Pass: the ball OPENS at the attacker, the mon SHRINKS into it
                    // (POKE_IN_BALL scale 1.0→0 over WE226_SCALE_SYNC 8) and VANISHES, the ball CLOSES (seq 1) then FLIES UP.
                    leader.X = leader.BaseX = _atX; leader.Y = leader.BaseY = _atY;
                    _monFx.Add(new MonFx { Mon = _atVis, Kind = 25, Frames = 48 });   // the attacker's shrink-into-ball + hide
                    break;
                case FN_CSP_FREE:     // just offset the cell by gp(0,1) from the defender and play its anim.
                    if (leader.Gp.Length >= 2) { leader.X += leader.Gp[0]; leader.Y += leader.Gp[1]; leader.BaseX = leader.X; leader.BaseY = leader.Y; }
                    break;
                case FN_CSP_WE_270:
                {   // 6 symbols in two columns (±32 x) × 3 rows (∓24 y) around centre (128,80),
                    // per-cell anim seq {0,0,1,1,2,3}; cap0/cap3 flipped.
                    int[] seqs = { 0, 0, 1, 1, 2, 3 };
                    double[] xs = { -32, 32, -32, 32, -32, 32 }, ys = { -24, -24, 24, 24, 0, 0 };
                    for (int i = 0; i < 6; i++)
                    {
                        var c = i == 0 ? leader : new CellActor(CellSeqs, 0) { FuncId = FN_CSP_WE_270 };
                        c.CapId = i; c.X = 128 + xs[i]; c.Y = 80 + ys[i]; c.BaseX = c.X; c.BaseY = c.Y;
                        c.FlipH = (i == 0 || i == 3); if (c.SeqCount > seqs[i]) c.SetSeq(seqs[i]);
                        if (i != 0) _catsActors.Add(c);
                    }
                    break;
                }
                case FN_CSP_WE_275:
                {   // Ingrain: 4 root cells at the attacker's base (y=140 player / 84 enemy),
                    // spread ±, inner pair flipped, appearing staggered.
                    double py = _attackerIsEnemy ? 84 : 140;
                    double[] xo = { -24, -8, 8, 24 }; bool[] fl = { false, true, true, false };
                    for (int i = 0; i < 4; i++)
                    {
                        var c = i == 0 ? leader : new CellActor(CellSeqs, 0) { FuncId = FN_CSP_WE_275 };
                        c.CapId = i; c.X = _atX + xo[i]; c.Y = py; c.BaseX = c.X; c.BaseY = py; c.FlipH = fl[i]; c.Visible = false;
                        if (i != 0) _catsActors.Add(c);
                    }
                    break;
                }
                case FN_CSP_WE_274:
                {   // 12 cells scattered across the field (rand_rect), staggered random lifetimes.
                    for (int i = 0; i < 12; i++)
                    {
                        var c = i == 0 ? leader : new CellActor(CellSeqs, 0) { FuncId = FN_CSP_WE_274 };
                        c.CapId = i; c.X = 40 + (i * 53 % 180); c.Y = 30 + (i * 37 % 120); c.BaseX = c.X; c.BaseY = c.Y; c.Visible = false;
                        if (i != 0) _catsActors.Add(c);
                    }
                    break;
                }
                case FN_CSP_WE_338:
                {   // Frenzy Plant: 8 cells chained along the attacker→defender line, alternating
                    // flip, revealed in sequence (the plant grows across).
                    for (int i = 0; i < 8; i++)
                    {
                        double f = i / 7.0, x = _atX + (_dfX - _atX) * f, y = _atY + (_dfY - _atY) * f;
                        var c = i == 0 ? leader : new CellActor(CellSeqs, 0) { FuncId = FN_CSP_WE_338 };
                        c.CapId = i; c.X = x; c.Y = y; c.BaseX = x; c.BaseY = y; c.FlipH = (i & 1) != 0; c.Visible = false;
                        if (i != 0) _catsActors.Add(c);
                    }
                    break;
                }
                case FN_CSP_WE_320:
                {   // music notes, Sing/Perish Song: 15 notes float up from the attacker, each a
                    // different note graphic (seq i%3), appearing staggered.
                    leader.BaseX = _atX; leader.BaseY = _atY; leader.X = _atX; leader.Y = _atY; leader.Visible = false;
                    for (int i = 1; i < 15; i++)
                    {
                        var c = new CellActor(CellSeqs, 0) { FuncId = FN_CSP_WE_320, CapId = i, X = _atX, Y = _atY, BaseX = _atX, BaseY = _atY, Visible = false };
                        if (c.SeqCount > 0) c.SetSeq(i % 3);
                        _catsActors.Add(c);
                    }
                    if (leader.SeqCount > 0) leader.SetSeq(0);
                    break;
                }
                case FN_CSP_WE_288:
                {   // Grudge: 6 spirits float out from the attacker, staggered.
                    leader.BaseX = _atX; leader.BaseY = _atY; leader.X = _atX; leader.Y = _atY; leader.Visible = false;
                    for (int i = 1; i < 6; i++)
                        _catsActors.Add(new CellActor(CellSeqs, 0) { FuncId = FN_CSP_WE_288, CapId = i, X = _atX, Y = _atY, BaseX = _atX, BaseY = _atY, Visible = false });
                    break;
                }
                case FN_CSP_WE_269:
                {   // Taunt: ONE translucent taunt cell at screen (128,80) (W269_OAM_MAX=1, NOT a
                    // stack of bind bands), playing its animation seq (0 normally / 1 when the enemy is the caster).
                    leader.X = leader.BaseX = 128; leader.Y = leader.BaseY = 80;
                    if (leader.SeqCount > 1 && _attackerIsEnemy) leader.SetSeq(1);
                    leader.Alpha = 0.5;
                    break;
                }
                case FN_CSP_090:      // Fissure: the crack sits at the defender's base, y=126 if the
                    // defender is on the player (bottom) side / 32 if enemy (top), with a per-side anim seq.
                    leader.X = leader.BaseX = _dfX;
                    leader.Y = leader.BaseY = _attackerIsEnemy ? 126 : 32;
                    if (leader.SeqCount > 1) leader.SetSeq(_attackerIsEnemy ? 1 : 0);
                    break;
                case FN_CSP_WE_259:
                {   // Torment: 6 anger marks fan around the attacker's head, 3 pairs at 0/30/60°,
                    // radius 48; even index = right (flipped), odd = left. Appear staggered.
                    for (int i = 0; i < 6; i++)
                    {
                        double ang = (i / 2) * 30.0 * Math.PI / 180.0, cxo = Math.Cos(ang) * 48, cyo = Math.Sin(ang) * 48;
                        bool right = (i % 2) == 0;
                        double x = _atX + (right ? cxo : -cxo), y = _atY - cyo;
                        if (i == 0) { leader.X = x; leader.Y = y; leader.FlipH = true; leader.CapId = 0; leader.Visible = false; }
                        else _catsActors.Add(new CellActor(CellSeqs, 0) { FuncId = FN_CSP_WE_259, CapId = i, X = x, Y = y, FlipH = right, Visible = false });
                    }
                    break;
                }
                case FN_CSP_WE_118:
                {   // Metronome finger-wag: hand at attacker+(40·vec,0), per-side anim seq.
                    double vec = _attackerIsEnemy ? -1 : 1;
                    leader.BaseX = _atX; leader.BaseY = _atY; leader.X = _atX + 40 * vec; leader.Y = _atY;
                    leader.ScaleX = leader.ScaleY = 0.1;
                    if (_attackerIsEnemy && leader.SeqCount > 1) leader.SetSeq(1);
                    break;
                }
                case FN_CSP_WE_132:
                {   // Constrict: 4 grass tendrils from the defender's base, stacked up 10px,
                    // alternating flip, appearing staggered (×4f).
                    leader.X = _dfX; leader.Y = _dfY + 16; leader.CapId = 0;
                    for (int i = 1; i < 4; i++)
                        _catsActors.Add(new CellActor(CellSeqs, 0) { FuncId = FN_CSP_WE_132, CapId = i,
                            X = _dfX, Y = _dfY + 16 - i * 10, FlipH = (i & 1) != 0, BaseX = _dfX, BaseY = _dfY });
                    break;
                }
                case FN_CSP_WE_155:
                    // Bonemerang: the bone boomerangs attacker→defender→attacker. Base = attacker.
                    leader.BaseX = _atX; leader.BaseY = _atY; leader.X = _atX; leader.Y = _atY;
                    break;
                case FN_CSP_WE_134:
                {   // Kinesis spoon-bend: spoon at the attacker, translucent; 2 afterimages trail
                    // by 8f each. Base = attacker. (The bend itself is the cell's NANR anim.)
                    leader.BaseX = _atX; leader.BaseY = _atY; leader.X = _atX; leader.Y = _atY; leader.Alpha = 0;
                    for (int i = 1; i <= 2; i++)   // WE134_ZANZOU_NUM afterimages
                        _catsActors.Add(new CellActor(CellSeqs, 0) { FuncId = FN_CSP_WE_134, CapId = i,
                            X = _atX, Y = _atY, BaseX = _atX, BaseY = _atY, Alpha = 0, Visible = false });
                    break;
                }
                case FN_CSP_WE_286:
                {   // Grudge, the 封 seal: 3 cells at the defender (raised by its shadow height).
                    // cap 0 = the seal symbol (NANR seq 1, opaque); caps 1-2 = translucent afterimage trails (WE286_ZANZOU_NUM).
                    leader.BaseX = _dfX; leader.BaseY = _dfY; leader.X = _dfX; leader.Y = _dfY; leader.CapId = 0; leader.Visible = false;
                    if (leader.SeqCount > 1) leader.SetSeq(1);   // "最初のアクターは違う絵", the seal uses a different anim seq
                    for (int i = 1; i <= 2; i++)
                        _catsActors.Add(new CellActor(CellSeqs, 0) { FuncId = FN_CSP_WE_286, CapId = i,
                            X = _dfX, Y = _dfY, BaseX = _dfX, BaseY = _dfY, Visible = false });
                    // After the seal descends (scale-in), the defender shakes (WE286_SHAKE 4,0,sync1,num6 ≈ 24f).
                    _monFx.Add(new MonFx { Mon = _dfVis, Kind = 5, Sh = new Shake(4, 0, 1, 6), NumMax = 0, Delay = 10 });
                    break;
                }
                case FN_CSP_WE_184:
                {   // Scary Face: a face cell appears at the attacker (+32·vec) and slides
                    // forward/up while scaling up, then fades. Base = attacker, not defender.
                    double vec = _attackerIsEnemy ? -1 : 1;
                    leader.BaseX = _atX; leader.BaseY = _atY;
                    leader.X = _atX + 32 * vec; leader.Y = _atY; leader.ScaleX = leader.ScaleY = 0.5;
                    break;
                }
                case FN_CSP_WE_271:
                    // shell-game shuffle: two cups at fixed screen spots fall, orbit, fade.
                    leader.X = leader.BaseX = 100; leader.Y = leader.BaseY = 54; leader.CapId = 0;
                    _catsActors.Add(new CellActor(CellSeqs, 0) { FuncId = FN_CSP_WE_271, CapId = 1, X = 180, Y = 39, BaseX = 180, BaseY = 39 });
                    break;
                case FN_CSP_WE_232:
                {   // Iron/Metal Claw: 4 claw marks, left pair (flipped H) at −32, right pair at
                    // +32, the lower of each at +32 y, all relative to the defender. Right pair appears 10f later.
                    leader.X = _dfX - 32; leader.Y = _dfY; leader.FlipH = true; leader.CapId = 0;
                    (int dx, int dy, bool flip)[] p = { (-32, 32, true), (32, 0, false), (32, 32, false) };
                    for (int i = 0; i < 3; i++)
                        _catsActors.Add(new CellActor(CellSeqs, 0) { FuncId = FN_CSP_WE_232, CapId = i + 1,
                            X = _dfX + p[i].dx, Y = _dfY + p[i].dy, FlipH = p[i].flip, BaseX = _dfX, BaseY = _dfY });
                    break;
                }
                case FN_CSP_WE_081:
                {   // Bind/Wrap/Clamp: GPWork[0] binding bands stacked on the defender's default
                    // pos (PosMove 0, DEF_POS−i·4 = 32,28,24…). Spawn the extra bands as siblings (band 0 = leader).
                    int n = leader.Gp.Length > 0 ? Math.Max(1, leader.Gp[0]) : 1;
                    leader.Y = leader.BaseY + 32;
                    for (int i = 1; i < n; i++)
                        _catsActors.Add(new CellActor(CellSeqs, 0) { FuncId = FN_CSP_WE_081, Gp = leader.Gp,
                            CapId = i, X = _dfX, Y = _dfY + (32 - i * 4), BaseX = _dfX, BaseY = _dfY });
                    break;
                }
            }
        }

        // Runs the ported the cell-actor callback* per-frame logic for an actor (dispatch by callback id). Unknown ids do nothing
        // → the actor just plays its cell animation at the defender (the correct base for most marks/hit-sprites).
        private void RunCatsDriver(CellActor a)
        {
            switch (a.FuncId)
            {
                case FN_CSP_WE_207_SUB: Drive207Sub(a); break;
                case FN_CSP_WE_081: Drive081(a); break;
                case FN_CSP_WE_333: Drive333(a); break;
                case FN_CSP_WE_232: Drive232(a); break;
                case FN_CSP_WE_271: Drive271(a); break;
                case FN_CSP_WE_184: Drive184(a); break;
                case FN_CSP_WE_134: Drive134(a); break;
                case FN_CSP_WE_286: Drive286(a); break;   // Grudge: the 封 seal scales in, holds, scales out
                case FN_CSP_WE_118: Drive118(a); break;
                case FN_CSP_WE_132: Drive132(a); break;
                case FN_CSP_WE_155: Drive155(a); break;
                case FN_CSP_WE_193: Drive193(a); break;
                case FN_CSP_WE_199: Drive199(a); break;
                case FN_CSP_WE_212: Drive212(a); break;
                case FN_CSP_WE_259: Drive259(a); break;
                case FN_CSP_266: Drive266(a); break;
                case FN_CSP_WE_269: Drive269(a); break;   // Taunt: a translucent taunt cell held at (128,80)
                case FN_CSP_WE_252: Drive252(a); break;   // Fake Out: translucent clap, fade in → play → fade out
                case FN_CSP_WE_226: Drive226(a); break;   // Baton Pass: ball opens → closes (seq 1) → flies up
                case FN_CSP_WE_320: DriveFloat(a, 3, 0.8, 1.2, 40); break;   // music notes rise
                case FN_CSP_WE_288: DriveFloat(a, 4, 1.0, 0.8, 44); break;   // grudge spirits drift
                case FN_CSP_WE_270: DriveAppearHoldFade(a, a.CapId * 5, 40, 12); break;   // staggered symbols
                case FN_CSP_WE_274: DriveAppearHoldFade(a, a.CapId * 2, 26 + a.CapId, 10); break;   // scatter field
                case FN_CSP_WE_338: DriveAppearHoldFade(a, a.CapId * 2, 48, 12); break;   // plant grows along the line
                case FN_CSP_WE_275: DriveAppearHoldFade(a, a.CapId * 5, 44, 12); break;   // roots sprout staggered
            }
        }

        // Baton Pass ball `a`: opens (seq 0) at the attacker while the mon shrinks into it (Kind 25);
        // at ~f24 it CLOSES (anim seq 1), then at ~f40 it FLIES UP off the top (WE226_MOVE_SYNC 8, y → 0) and is gone by f48.
        private void Drive226(CellActor a)
        {
            int t = a.Age;
            a.Visible = true;
            if (t == 24 && a.Seq != 1 && a.SeqCount > 1) a.SetSeq(1);                 // close the ball
            if (t >= 40 && t < 48) a.Y = a.BaseY * (1.0 - (t - 40) / 8.0);            // fly up to the top
            else if (t >= 48) { a.Visible = false; a.Alive = false; }
        }

        // Fake Out / NEKODAMASI clap: a TRANSLUCENT (GX_OAM_MODE_XLU) cell at the defender
        // that fades IN, auto-plays its clap NANR once, then fades OUT (We252 DrawCapTcb: ALPHAIN → ANM → ALPHAOUT).
        private void Drive252(CellActor a)
        {
            const int FadeIn = 6, FadeOut = 6, MaxHold = 40; const double Peak = 0.6;
            a.Visible = true;
            if (a.Age < FadeIn) { a.Alpha = Peak * (a.Age + 1) / FadeIn; return; }   // fade in
            if (!a.Finished && a.Age < MaxHold) { a.Alpha = Peak; return; }          // hold while the clap plays (capped if it loops)
            a.Alpha -= Peak / FadeOut;                                               // fade out
            if (a.Alpha <= 0) { a.Alpha = 0; a.Visible = false; a.Alive = false; }
        }

        // Generic "appear (after `delay`) → hold `hold`f → fade `fade`f → gone" driver for staggered multi-cell
        // callbacks (WE_270 symbols, WE_274 scatter, WE_338 chain). Position is fixed at spawn; the cell plays its anim.
        private void DriveAppearHoldFade(CellActor a, int delay, int hold, int fade)
        {
            int t = a.Age - delay;
            if (t < 0) { a.Visible = false; return; }
            a.Visible = true;
            if (t >= hold && t < hold + fade) a.Alpha = 1.0 - (t - hold) / (double)fade;
            else if (t >= hold + fade) a.Visible = false;
        }

        // Shared float-out driver for cell swarms (WE_320 notes, WE_288 spirits): each cell `a` appears staggered by
        // CapId·`stag`, drifts outward (·vec) + up with a sine wobble, then fades over the last 12f of `life`.
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

        // Follow Me: the finger sways side-to-side (±~40px, ~1.5 cycles over 42f) then
        // rotation-wags ±20° (±FX_GET_ROTA_NUM(20) over WAIT_266=4f each, ~6 swings), then gone.
        private void Drive266(CellActor a)
        {
            int t = a.Age; const int Sway = 42, Rot = 24;
            if (t < Sway) a.X = a.BaseX + 40 * Math.Sin(t / (double)Sway * Math.PI * 1.5);
            else if (t < Sway + Rot) { a.X = a.BaseX; a.ExtraRotDeg = 20 * Math.Sin((t - Sway) / 4.0 * Math.PI); }
            else a.Visible = false;
        }

        // Mean Look: the gaze cell plays at the defender, then scales 1.5→1.0 and alpha-fades out.
        private void Drive212(CellActor a)
        {
            int t = a.Age; const int Hold = 40, Fade = 24;
            if (t < Hold) { a.ScaleX = a.ScaleY = 1.5; }
            else if (t < Hold + Fade) { double k = (t - Hold) / (double)Fade; a.ScaleX = a.ScaleY = 1.5 - 0.5 * k; a.Alpha = 1 - k; }
            else a.Visible = false;
        }

        // Torment mark `a`: appears staggered by CapId (×4f), holds, then fades out.
        private void Drive259(CellActor a)
        {
            int t = a.Age, appear = a.CapId * 4; const int Hold = 40, Fade = 12;
            if (t < appear) { a.Visible = false; return; }
            a.Visible = true;
            int u = t - appear;
            if (u >= Hold && u < Hold + Fade) a.Alpha = 1 - (u - Hold) / (double)Fade;
            else if (u >= Hold + Fade) a.Visible = false;
        }

        // Lock-On/Mind Reader: the crosshair shows on the defender and auto-animates, then a white
        // flash, then it blinks SWITCH_COUNT=4 times (every 4f), then gone. (The HW screen flash is omitted.)
        private void Drive199(CellActor a)
        {
            int t = a.Age; const int Anim = 24, Flash = 8, Blink = 4 * 8;
            if (t < Anim + Flash) a.Visible = true;
            else if (t < Anim + Flash + Blink) a.Visible = ((t - Anim - Flash) / 4) % 2 == 0;
            else a.Visible = false;
        }

        // Foresight/Odor Sleuth: the magnifying glass traces a 6-segment zigzag over the defender
        // (W=H=80, 8f move + 4f wait each, returns to start), then fades out 16f while the defender flashes white.
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

        // Metronome: scale the hand in 0.1→1.0 (8f), wag ±20° about ∓20° for ~4 swings (rota
        // 359°↔320° forward / 0°↔40° reverse, one-sync 4f), then scale out. Stays at attacker+40·vec.
        private void Drive118(CellActor a)
        {
            int t = a.Age; const int In = 8, Wag = 32, Out = 8;
            double centre = _attackerIsEnemy ? 20 : -20;
            if (t < In) a.ScaleX = a.ScaleY = 0.1 + 0.9 * (t / (double)In);
            else if (t < In + Wag) { a.ScaleX = a.ScaleY = 1.0; a.ExtraRotDeg = centre + 20 * Math.Sin((t - In) / 4.0 * Math.PI); }
            else if (t < In + Wag + Out) { a.ExtraRotDeg = 0; a.ScaleX = a.ScaleY = 1.0 - 0.9 * ((t - In - Wag) / (double)Out); }
            else a.Visible = false;
        }

        // Constrict grass `a` (CapId 0-3): appear staggered (×4f), then squeeze scaleX 1.0↔0.8
        // three times (8f each half), then gone; the defender gets a brief shake while the grass binds.
        private void Drive132(CellActor a)
        {
            int t = a.Age; int appear = a.CapId * 4; const int AllIn = 16, Squeeze = 48;
            if (t < appear) { a.Visible = false; return; }
            a.Visible = true;
            if (t >= AllIn && t < AllIn + Squeeze)
            {
                a.ScaleX = 1.0 - 0.2 * Math.Abs(Math.Sin((t - AllIn) / 8.0 * Math.PI));   // 3 squeeze cycles over 48f
                if (a.CapId == 0 && t < AllIn + 8) { MonShakeX[_dfVis] = (t % 2 == 0 ? 4 : -4); }   // brief defender shake
            }
            else if (t >= AllIn + Squeeze) a.Visible = false;
        }

        // Bonemerang: the bone arcs attacker→defender (10f, up 32px) then back defender→attacker
        // (10f), spinning throughout, then gone.
        private void Drive155(CellActor a)
        {
            int t = a.Age; const int Leg = 10;
            a.ExtraRotDeg = t * 30;   // spin
            if (t < Leg) { double f = t / (double)Leg; a.X = a.BaseX + (_dfX - a.BaseX) * f; a.Y = a.BaseY + (_dfY - a.BaseY) * f - 32 * 4 * f * (1 - f); }
            else if (t < 2 * Leg) { double f = (t - Leg) / (double)Leg; a.X = _dfX + (a.BaseX - _dfX) * f; a.Y = _dfY + (a.BaseY - _dfY) * f - 32 * 4 * f * (1 - f); }
            else a.Visible = false;
        }

        // Kinesis spoon/afterimage `a` (CapId 0=main, 1-2=trails delayed 8f each): fade in 31f,
        // sweep a flat arc (sin·−32·vec X, cos·−8 Y; angle 90°→270° over 18f, the rotation-speed helper), hold while
        // the bend anime plays, fade out 8f. Trails are dimmer. (Afterimage trail is a faithful approximation.)
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

        // Grudge seal `a` (CapId 0 = the 封 seal, opaque; 1-2 = translucent trails, each delayed
        // ZANZOU_WAIT 9f): scale IN 2.5→1.0 (SCALE_S/D 25/10 → E/D 10/10) over SCALE_SYNC 10, hold while the defender
        // shakes, then scale OUT 1.0→2.5 over SCALEOUT_SYNC 6 and vanish. Positioned at the defender.
        private void Drive286(CellActor a)
        {
            int t = a.Age - 9 * a.CapId;   // WE286_ZANZOU_WAIT stagger between the seal and its trails
            if (t < 0) { a.Visible = false; return; }
            a.Visible = true;
            double peak = a.CapId == 0 ? 1.0 : 0.5;   // trails are XLU (~half)
            const int In = 10, Hold = 24, Out = 6;
            if (t < In) { double k = t / (double)In; a.ScaleX = a.ScaleY = 2.5 - 1.5 * k; a.Alpha = peak; }        // pop big → 1.0
            else if (t < In + Hold) { a.ScaleX = a.ScaleY = 1.0; a.Alpha = peak; }                                 // hold (defender shakes)
            else if (t < In + Hold + Out) { double k = (t - In - Hold) / (double)Out; a.ScaleX = a.ScaleY = 1.0 + 1.5 * k; a.Alpha = peak * (1 - k); }  // 1.0→2.5 + fade
            else a.Visible = false;
        }

        // Scary Face face cell: slide from the attacker (+32·vec) by (64·vec, −16) over 32f while
        // scaling 0.5→1.2 (face-scale-rate 5/10→12/10), then fade out over 8f.
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

        // Taunt: hold the single translucent taunt cell at screen (128,80), playing its NANR
        // animation, for WE269_EFF_TIME = 45 frames, then hide. (Was wrongly the WE_081 bind/squeeze.)
        private void Drive269(CellActor a)
        {
            a.X = 128; a.Y = 80; a.Alpha = 0.5;
            a.Visible = a.Age < 45;
        }

        // Cup `a` (CapId 0/1): fall ~25f (2px/frame), then orbit the shared midpoint ×5 (each loop a
        // 180° swap over 10f via a scale-rate curve → the two cups circle each other), then fade out.
        private void Drive271(CellActor a)
        {
            int t = a.Age; const int Fall = 25, Orbit = 50;
            double fallenY = a.BaseY + 50;
            if (t < Fall) { a.X = a.BaseX; a.Y = a.BaseY + 2 * t; }
            else if (t < Fall + Orbit)
            {
                double mx = 140, my = (54 + 50 + 39 + 50) / 2.0;      // midpoint of the two fallen positions (140, 96.5)
                double ox = a.BaseX - mx, oy = fallenY - my, r = Math.Sqrt(ox * ox + oy * oy), a0 = Math.Atan2(oy, ox);
                double ang = a0 + Math.PI * ((t - Fall) / 10.0);      // 180° per 10f loop
                a.X = mx + r * Math.Cos(ang); a.Y = my + r * Math.Sin(ang);
            }
            else { double k = Math.Min(1.0, (t - Fall - Orbit) / 8.0); a.Alpha = 1 - k; if (k >= 1) a.Visible = false; }
        }

        // Claw mark `a` (CapId 0-3): the two right-side claws (CapId≥2) appear after a 10f wait;
        // every claw plays its slash animation and is gone after 40 frames.
        private void Drive232(CellActor a)
        {
            if (a.CapId >= 2 && a.Age < 10) { a.Visible = false; return; }
            a.Visible = a.Age < 40;
        }

        // Icicle Spear / Spike Cannon: the cell flies a parabolic curve from the attacker to the
        // defender (+gp offset·vec) over `time` frames, arcing up `height` px, while spinning 20°→130° (forward) /
        // 90°→130° (reverse) over the first 10f (the curved Y-motion helper + InitMoveOneSync). Gone when the curve ends.
        private void Drive333(CellActor a)
        {
            int ofsX = a.Gp.Length > 0 ? a.Gp[0] : 0, ofsY = a.Gp.Length > 1 ? a.Gp[1] : 0;
            int time = a.Gp.Length > 2 ? Math.Max(1, a.Gp[2]) : 16, height = a.Gp.Length > 3 ? a.Gp[3] : 0;
            double vec = _attackerIsEnemy ? -1 : 1;
            double sxp = _atX, syp = _atY, exp = _dfX + ofsX * vec, eyp = _dfY + ofsY * vec;
            double frac = Math.Min(1.0, (double)a.Age / time);
            a.X = sxp + (exp - sxp) * frac;
            a.Y = syp + (eyp - syp) * frac - height * 4.0 * frac * (1 - frac);   // arc up (screen +Y is down)
            double k = Math.Min(1.0, a.Age / 10.0);
            a.ExtraRotDeg = vec > 0 ? (20 + (130 - 20) * k) : -(90 + (130 - 90) * k);   // spin (signed by facing)
            if (a.Age >= time) a.Visible = false;
        }

        // Anger "kiremark" (Taunt/Swagger): two marks pop in by the defender's head, first
        // upper-FORWARD (df+24·vec,−16), then upper-BACK (df−24·vec,−24), each scaling 1.0→1.4 (4f) then settling
        // →1.2 (2f) (scale-rate s10→e14 / ret14→e12), with a 4f gap; then it's gone.
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

        // Binding band `a` (a.CapId = band index): blink in (staggered until t=45),
        // squeeze scaleX 100→60% over 10f, hold 45f, then fade out, mirrors a similar 4-phase sequence.
        private void Drive081(CellActor a)
        {
            int t = a.Age, idx = Math.Min(a.CapId, BindingBandWaitSteps.Length - 1);
            int delay = BindingBandWaitSteps[idx][0], interval = Math.Max(1, BindingBandWaitSteps[idx][1]);
            const int Eff = 45;
            if (t < Eff) a.Visible = t >= delay && ((t - delay) / interval) % 2 == 0;   // staggered blink-in
            else if (t < Eff + 10) { a.Visible = true; a.ScaleX = 1.0 - 0.4 * ((t - Eff) / 10.0); a.ScaleY = 1.0; }  // squeeze
            else if (t < Eff + 10 + Eff) { a.Visible = true; a.ScaleX = 0.6; }            // hold tight
            else { double k = Math.Min(1.0, (t - (Eff + 10 + Eff)) / 15.0); a.Alpha = 1.0 - k; if (k >= 1) a.Visible = false; }  // fade
        }

        // Off-screen X for a mon: a sprite resting on the left/own half exits left (WIN_OSX), one on the right exits
        // right (WIN_OEX), mirrors WeDispOut's SIDE_MINE check on the client's screen side.
        private double OffscreenX(int mon) => ((mon == 0 ? _atX : _dfX) < 128 ? WIN_OSX : WIN_OEX);

        // Runs the active per-mon animations. Transient transforms (rot/scale/tint/visible) reset each frame and are
        // re-applied from the active effects; position (MonDX/DY) PERSISTS, WE_T10 moves accumulate (lunge out, back).
        private void UpdateMonFx()
        {
            MonRot[0] = MonRot[1] = 0; MonScaleX[0] = MonScaleX[1] = 1; MonScaleY[0] = MonScaleY[1] = 1;
            MonTintA[0] = MonTintA[1] = 0; MonVisible[0] = MonVisible[1] = true;
            MonShakeX[0] = MonShakeX[1] = 0; MonShakeY[0] = MonShakeY[1] = 0; ShakeX = ShakeY = 0;   // transient, re-applied below
            BgFlashAmount = 0;   // transient BG colour flash (Earthquake), re-applied below
            MonMosaic[0] = MonMosaic[1] = 0; MonClip[0] = MonClip[1] = 1; MonAlpha[0] = MonAlpha[1] = 1;
            MonWarpMon = -1;   // transient Extrasensory (WE_326DF) per-scanline warp, re-applied below
            _ghosts.Clear();   // afterimage ghosts are rebuilt each frame by the Kind 9/10 drivers below

            for (int i = _monFx.Count - 1; i >= 0; i--)
            {
                var fx = _monFx[i];
                if (fx.Delay > 0) { fx.Delay--; continue; }   // dormant until its scheduled start (e.g. Megahorn lunge after the shake)
                if (fx.Kind == 5)   // shake (exact the shake-step calculator); manages its own lifetime + BG_SHAKE outer repeats
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
                double t = (double)fx.Frame / Math.Max(1, fx.Frames);   // 0..1
                switch (fx.Kind)
                {
                    case 0: MonRot[fx.Mon] = Math.Sin(t * Math.PI * 2 * 2) * 18.0; break;     // rotate ±18°, 2 cycles
                    case 1:                                                                   // X/Y squash-stretch up-down
                        if (fx.Keys != null && fx.Keys.Length >= 4)
                        {
                            int per = Math.Max(1, fx.UpF + fx.WaitF + fx.DownF);
                            int fl = fx.Frame % per;                       // position within the current cycle
                            double sx = fx.Keys[0], ex = fx.Keys[1], sy = fx.Keys[2], ey = fx.Keys[3];
                            double cx, cy;
                            if (fl < fx.UpF) { double k = (double)fl / fx.UpF; cx = sx + (ex - sx) * k; cy = sy + (ey - sy) * k; }
                            else if (fl < fx.UpF + fx.WaitF) { cx = ex; cy = ey; }   // hold at the peak
                            else { double k = (double)(fl - fx.UpF - fx.WaitF) / fx.DownF; cx = ex + (sx - ex) * k; cy = ey + (sy - ey) * k; }
                            if (fx.Cap != null) { fx.Cap.ScaleX = cx; fx.Cap.ScaleY = cy; }
                            else { MonScaleX[fx.Mon] = cx; MonScaleY[fx.Mon] = cy; }
                        }
                        break;
                    case 2: MonVisible[fx.Mon] = (fx.Frame / Math.Max(1, fx.UpF)) % 2 == 0; break;   // blink: toggle every UpF frames
                    case 3: MonTintA[fx.Mon] = Math.Sin(t * Math.PI) * 0.85; TintR = fx.R; TintG = fx.G; TintB = fx.B; break;
                    case 6:   // SSP pal-fade blink: evy ramps 0→evyMax over UpF, holds WaitF, ramps back; ×Cycles
                    {
                        int fw = fx.UpF, w = fx.WaitF, cyc = Math.Max(1, 2 * fw + w), fl = fx.Frame % cyc;
                        double evyMax = fx.Keys != null && fx.Keys.Length > 0 ? fx.Keys[0] : 8;
                        double evy;
                        if (fw > 0 && fl < fw) evy = evyMax * (fl + 1) / fw;             // fade toward colour
                        else if (fl < fw + w) evy = evyMax;                              // hold at peak
                        else if (fw > 0) evy = evyMax * (1.0 - (double)(fl - fw - w + 1) / fw);   // fade back
                        else evy = evyMax;
                        MonTintA[fx.Mon] = Math.Clamp(evy / 16.0, 0, 1); TintR = fx.R; TintG = fx.G; TintB = fx.B;
                        break;
                    }
                    case 4: MonDX[fx.Mon] += fx.Dx / fx.Frames; MonDY[fx.Mon] += fx.Dy / fx.Frames; break;   // slide
                    case 26:   // the sink-into-void step function (Dark Void sink): deterministic mid-points of the source's
                    {          // rand() ladders, +4 at seq ~6/11/16/21, +8 at ~23, then from gene_cnt (~37)
                               // +4 every frame; the copy is swallowed (hidden) once it passes y ≈ 130.
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
                    case 21:  // dissolve / ghost-fade: alpha ramps 1 → minAlpha (Keys[0], default 0.1) → holds → back.
                    {         // Acid Armor (WE_151) melts to ~invisible (+ squash, Keys[1]>0); Spite (WE_180) fades the
                              // defender to ~0.5 (a translucent ghost). Phases = 35% out / 30% hold / 35% in.
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
                    case 20:  // Earthquake: 8 steps × 5f. Each step shakes the whole world
                    {         // (mons + BG) horizontally at a decreasing amplitude AND flashes the BACKGROUND black↔
                              // white (a palette color-change, held 3 of 5 frames), per a decreasing shake-amplitude table {12,10,8,6,4,2,1,0}.
                        int[] eqAmp = { 12, 10, 8, 6, 4, 2, 1, 0 };
                        int step = Math.Min(eqAmp.Length - 1, fx.Frame / 5), within = fx.Frame % 5;
                        ShakeX = eqAmp[step] * ((fx.Frame % 2) == 0 ? 1 : -1);   // sharp horizontal world shake
                        if (within < 3)   // BG flash for 3 of the 5 frames, alternating colour each step (evy 10/16)
                        {
                            BgFlashAmount = 10.0 / 16.0;
                            byte c = (byte)((step % 2) == 0 ? 0 : 255);   // even step → black, odd → white
                            BgFlashR = BgFlashG = BgFlashB = c;
                        }
                        break;
                    }
                    case 7:   // WE_KAITEN elliptical orbit (the rotation-fx calculator): x = ampX·sin θ, y = base + ampY·cos θ.
                        if (fx.Keys != null && fx.Keys.Length >= 4)
                        {
                            double ang = fx.Frame * 2.0 * Math.PI / Math.Max(1, fx.Keys[2]);
                            MonShakeX[fx.Mon] += fx.Keys[0] * Math.Sin(ang);
                            MonShakeY[fx.Mon] += fx.Keys[3] + fx.Keys[1] * Math.Cos(ang);
                        }
                        break;
                    case 9:   // WE_104 Double Team (かげぶんしん): 4 gray afterimages slide ±WE104_RANGE(32) px out and
                    {   // back, ~9 loops, then fade out (eva 8→0). Blend eva/16 ⇒ ghost alpha. Gray 128/196.
                        const int osc = 72, range = 32, loopLen = 8;
                        double eva = fx.Frame < osc ? 8 : Math.Max(0, 8 - (fx.Frame - osc));
                        double alpha = eva / 16.0;
                        if (alpha > 0.001)
                        {
                            double ph = (fx.Frame % loopLen) / (double)loopLen;
                            double f = ph < 0.5 ? ph * 2 : 2 - ph * 2;   // triangle 0→1→0
                            AddGhost(fx.Mon, range * f, alpha, 128); AddGhost(fx.Mon, -range * f, alpha, 128);
                            AddGhost(fx.Mon, range * (1 - f), alpha, 196); AddGhost(fx.Mon, -range * (1 - f), alpha, 196);
                        }
                        break;
                    }
                    case 10:  // spin: linear 0→Keys[0]° over life, OR (Keys[1]>0) a Keys[1]-cycle rock ±Keys[0]° (WE_204).
                        if (fx.Keys != null && fx.Keys.Length > 0)
                            MonRot[fx.Mon] = fx.Keys.Length > 1 && fx.Keys[1] > 0 ? fx.Keys[0] * Math.Sin(t * Math.PI * fx.Keys[1]) : fx.Keys[0] * t;
                        break;
                    case 11:  // the mosaic-level handler: ramp OBJ mosaic from Keys[0] toward Keys[1](0/15) by Keys[2]/frame (Mosaic_TCB).
                        if (fx.Keys != null && fx.Keys.Length >= 3)
                        {
                            double mv = Math.Clamp(fx.Keys[0] + fx.Keys[2] * fx.Frame, Math.Min(fx.Keys[0], fx.Keys[1]), Math.Max(fx.Keys[0], fx.Keys[1]));
                            if (fx.Cap != null) fx.Cap.Mosaic = mv; else MonMosaic[fx.Mon] = mv;
                        }
                        break;
                    case 12:  // OAM_PAL_FADE / palette soft-fade: evy ramps Keys[0]→Keys[1](/16) over life; tint = R/G/B.
                        if (fx.Keys != null && fx.Keys.Length >= 2)
                        {
                            double evy = fx.Keys[0] + (fx.Keys[1] - fx.Keys[0]) * t;
                            double ta = Math.Clamp(evy / 16.0, 0, 1);
                            if (fx.Cap != null) { fx.Cap.TintA = ta; fx.Cap.TintR = fx.R; fx.Cap.TintG = fx.G; fx.Cap.TintB = fx.B; }
                            else { MonTintA[fx.Mon] = ta; TintR = fx.R; TintG = fx.G; TintB = fx.B; }
                        }
                        break;
                    case 15:  // RECT_VIEW vertical wipe-reveal: clip fraction grows 0→1 over life; Dx sign = direction.
                        MonClip[fx.Mon] = (fx.Dx >= 0 ? 1 : -1) * Math.Clamp(t, 0.0, 1.0);
                        break;
                    case 16:  // CAP_NormalAlphaFade / WE_252: ramp mon translucency Keys[0]→Keys[1] (/16) over life.
                        if (fx.Keys != null && fx.Keys.Length >= 2)
                            MonAlpha[fx.Mon] = Math.Clamp((fx.Keys[0] + (fx.Keys[1] - fx.Keys[0]) * t) / 16.0, 0, 1);
                        break;
                    case 18:  // handler 289 (Snatch): 3 the straight-line sync-move helper segments of 15f each, home→Dx(off one edge),
                    {   // Dx→Dy (off the other, an invisible cross-screen), Dy→home. point_x[0,1,2] = Dx, Dy, home.
                        // The step since last frame, not the position: MonDX is a running offset that
                        // nothing clears, so adding a position every frame walks the sprite off screen.
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
                    case 17:  // handler 272 (Role Play): the attacker's white image appears at the defender (−32px), fading in
                        _ghosts.Add(new MonGhost { Mon = _atVis, Dx = (_dfX - 32) - _atX, Dy = _dfY - _atY,   // (eva 0→15).
                            ScaleX = 1, ScaleY = 1, Alpha = Math.Clamp(t * 2, 0, 1), TintR = 255, TintG = 255, TintB = 255, TintA = 0.5 });
                        break;
                    case 13:  // there-and-back move (jump): slide (Dx,Dy) out over UpF, hold WaitF at full, back over DownF.
                    {
                        // MonDX is a running offset that nothing resets between frames, so what goes in here
                        // is the STEP since last frame, not where the sprite should be. Adding the position
                        // every frame made Megahorn's charge pile up to 480px off screen and stay there.
                        int up = Math.Max(1, fx.UpF), hold = Math.Max(0, fx.WaitF), down = Math.Max(1, fx.DownF);
                        double At(int f)
                        {
                            if (f < 0) return 0;
                            if (f < up) return (double)f / up;
                            if (f < up + hold) return 1.0;
                            return Math.Max(0, 1 - (double)(f - up - hold) / down);
                        }
                        // On the last frame it goes all the way home rather than wherever the ramp had got
                        // to, otherwise the sprite is left a step short of where it started for ever.
                        double now = fx.Frame >= fx.Frames - 1 ? 0.0 : At(fx.Frame);
                        double step = now - At(fx.Frame - 1);
                        MonDX[fx.Mon] += fx.Dx * step; MonDY[fx.Mon] += fx.Dy * step;
                        break;
                    }
                    case 22:  // the rotation-motion builder orbit (Rolling Kick 098 / Submission 066 / Vital Throw 233): SetSspMatrix
                    {         // moves the sprite POSITION around a wide flat ellipse (DEF_ROTA_W_X 32, W_Y −8), ±32px across,
                              // bobbing DOWN 0→16, completing Cycles full turns. Dx sign = rotation direction (vec_x). Reads
                              // as a roll/spin. (Was a bitmap self-rotation /, Submission, a scale; both unfaithful.)
                        double turns = fx.Cycles > 0 ? fx.Cycles : 1, dir = fx.Dx < 0 ? -1 : 1;
                        // Steps again, for the same reason as the there-and-back move above.
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
                        for (int gi = 0; gi < fx.NumMax; gi++)   // WE_098 zanzou: NumMax trails at the orbit pos DO_WAIT(2)·(i+1) frames back
                        {
                            int pf = fx.Frame - 2 * (gi + 1);
                            if (pf < 0) continue;
                            double pa = 2 * Math.PI * turns * (pf / (double)Math.Max(1, fx.Frames));
                            _ghosts.Add(new MonGhost { Mon = fx.Mon, Dx = Math.Sin(pa) * 32 * dir, Dy = 8 * (1 - Math.Cos(pa)),
                                ScaleX = 1, ScaleY = 1, Alpha = 0.45 - 0.15 * gi });
                        }
                        break;
                    }
                    case 23:  // WE_326DF Extrasensory warp: 3 phases × WE326_CHANGE_WAIT 16f (We326DF_ParamSet). Phase params
                    {         // (rota_width, width_a) drive the per-scanline sine bulge + shear; the shimmer flg flips per frame.
                        int phase = Math.Min(2, fx.Frame / 16);
                        double[] rw = { 16, -16, 20 };    // WE326_ROTA0/1/2_WIDTH
                        double[] wa = { 5, -5, 10 };      // WE326_WIDTH0/1/2_NUM
                        MonWarpMon = fx.Mon;
                        MonWarpAmp = rw[phase]; MonWarpBaseDeg = 180; MonWarpAddPerRow = 180.0 / 80.0;   // WE326_ROTA_ADD_NUM
                        MonWarpWidthA = wa[phase];
                        MonWarpShimmer = (fx.Frame & 1) == 0 ? 1 : -1;   // WE326_WIDTH_OFS (rota_width_ofs_flg *= −1 each frame)
                        break;
                    }
                    case 24:  // WE_151 Acid Armor melt ripple: an 8-px per-scanline sine (WE151_ROTA_WIDTH 8, ROTA_ADD 5°/row)
                              // that SCROLLS (WE151_SCROLL_SP 80 ⇒ 0.8°/frame·... scaled) while the sprite alpha-dissolves (Kind 21).
                        MonWarpMon = fx.Mon;
                        MonWarpAmp = 8; MonWarpBaseDeg = fx.Frame * 4.5; MonWarpAddPerRow = 5; MonWarpWidthA = 0; MonWarpShimmer = 0;
                        break;
                    case 25:  // Baton Pass (WE_226): after the ball opens (~16f) the attacker shrinks 1.0→0 over 8f
                        if (fx.Frame < 16) { }                                                                   // (WE226_SCALE_SYNC) then stays
                        else if (fx.Frame < 24) { double k = (fx.Frame - 16) / 8.0; MonScaleX[fx.Mon] = MonScaleY[fx.Mon] = 1.0 - k; }  // hidden while the ball
                        else MonVisible[fx.Mon] = false;                                                         // closes + flies up (Drive226).
                        break;
                    case 14:  // WE_107: 4 gray afterimage copies at the mon shrink 1.0→0.05 over 5f (staggered by
                        for (int gi = 0; gi < 4; gi++)   // staggered wait steps {2,7,13,18}, then re-expand, bottom-anchored.
                        {
                            int[] delay = { 2, 7, 13, 18 };
                            int local = fx.Frame - delay[gi];
                            if (local < 0 || local >= 10) continue;
                            double sc = local < 5 ? 1.0 - 0.95 * (local / 5.0) : 0.05 + 0.95 * ((local - 5) / 5.0);
                            _ghosts.Add(new MonGhost { Mon = fx.Mon, Dx = 0, Dy = (1 - sc) * 24, ScaleX = sc, ScaleY = sc,
                                                       Alpha = 0.7, TintR = 128, TintG = 128, TintB = 128, TintA = 0.5 });
                        }
                        break;
                    case 8:   // scale-keyframe sequence (the scale-rate keyframe helper phases): squash/stretch flexes. Each
                        if (fx.Phases != null && fx.Phases.Length > 0)   // phase = [sxStart,sxEnd,syStart,syEnd,frames] /100.
                        {
                            int acc = 0, pi = 0;
                            for (; pi < fx.Phases.Length; pi++) { int fr = Math.Max(1, (int)fx.Phases[pi][4]); if (fx.Frame < acc + fr) break; acc += fr; }
                            if (pi >= fx.Phases.Length) pi = fx.Phases.Length - 1;
                            var ph = fx.Phases[pi]; int dur = Math.Max(1, (int)ph[4]);
                            double k = Math.Clamp((double)(fx.Frame - acc) / dur, 0, 1);
                            double scy = (ph[2] + (ph[3] - ph[2]) * k) / 100.0;
                            MonScaleX[fx.Mon] = (ph[0] + (ph[1] - ph[0]) * k) / 100.0;
                            MonScaleY[fx.Mon] = scy;
                            MonShakeY[fx.Mon] += (1 - scy) * 24;   // the Y-position scale-rate helper: keep the feet planted as it scales
                        }
                        break;
                }
                if (++fx.Frame >= fx.Frames) _monFx.RemoveAt(i);
            }
            if (_monVanish[0]) MonVisible[0] = false;   // persistent vanish overrides (until a show call clears it)
            if (_monVanish[1]) MonVisible[1] = false;
        }

        // WE_057 wave: rise (thin→tall, fade in) → hold → wash (wide→flat, fade out). The Y position re-anchors each
        // frame (the Y-position scale-rate helper, poke_h=16 → ofs = 24·(1−scaleY)) so the wave's BASE stays put and it
        // grows upward instead of scaling about its centre.
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
            // Apply the phase state to the casting wave actor (the actor scale-set call / PosSetCap / blend). The Y re-anchor
            // (the Y-position scale-rate helper, poke_h=16 → ofs = 24·(1−scaleY)) keeps the wave's BASE put so it grows up.
            if (_we057Actor != null)
            {
                _we057Actor.ScaleX = _cellScaleX; _we057Actor.ScaleY = _cellScaleY; _we057Actor.Alpha = _cellOpacity;
                _we057Actor.X = _cellDefX;
                _we057Actor.Y = _cellDefY + (80 - WE057_OAM_HEIGHT * 2) / 2.0 * (1.0 - _cellScaleY);
                _we057Actor.Visible = _cellOpacity > 0;
                if (_cellPhase < 0) { _we057Actor.Visible = false; _we057Actor.Alive = false; _we057Actor = null; }   // wash done → CATS delete
            }
        }

        // ── HAIKEI scrolling background ──────────────────────────────────────────────────────────────────────────
        // Mirrors WeT02_TCB / the backdrop-change routine: the move-effect BG is a full WRAPPING tiled layer (the
        // NSCR is a seamless 512×256 sheet) scrolled by (posX,posY) at the (spdX,spdY) read from the effect,
        // displayed in full, never cropped. It fades IN to peak; a WE_T02 overlay additionally fades OUT once
        // pos_y crosses the stop line (WET02_STOP_Y = +512 / −412), continuing to scroll meanwhile, then drops
        // the layer. HAIKEI_CHG backdrops (useStop=false) have no stop line and stay until HAIKEI_RECOVER flips
        // _bgFadingOut.
        private void StartBackground(int bgId, bool overlay, double posX, double posY, double spdX, double spdY,
                                     double peak, int fadeFrames, double stopY, bool useStop)
        {
            var img = _bgRenderer.Build(bgId, reverse: _attackerIsEnemy);
            if (img == null) return;
            _bgRgba = img.Rgba; _bgW = img.Width; _bgH = img.Height;
            // Effect overlay (WeT02) lives on the 512×512 FRAME2_M; a HAIKEI backdrop frame matches its NSCR size.
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
            if (_bgHoldLeft > 0 && --_bgHoldLeft == 0) _bgFadingOut = true;   // its time is up, wash it off
            _bgX += _bgSpdX; _bgY += _bgSpdY;                               // scroll every frame (even while fading out)
            if (_bgUseStop && !_bgFadingOut && ((_bgSpdY > 0 && _bgY >= _bgStopY) || (_bgSpdY < 0 && _bgY <= _bgStopY)))
                _bgFadingOut = true;                                        // reached the stop line → wash off
            double step = _bgPeak / _bgFadeFrames;
            if (_bgFadingOut) { _bgOpacity -= step; if (_bgOpacity <= 0) { _bgOpacity = 0; _bgRgba = null; } }
            else if (_bgOpacity < _bgPeak) _bgOpacity = Math.Min(_bgPeak, _bgOpacity + step);
            // GX blend coeffs (out = water·ca + sceneBelow·cb). ca = eva/16 (the water plane, ramps to peak); cb =
            // evb/16 (the scene below): WeT02 ramps ev2 31→7 (1.0→0.4375); a HAIKEI backdrop is a straight crossfade.
            double prog = _bgPeak > 0 ? Math.Clamp(_bgOpacity / _bgPeak, 0, 1) : 0;
            BgCa = _bgOpacity;
            BgCb = _bgOverlay ? 1.0 - (1.0 - 7.0 / 16.0) * prog : 1.0 - prog;
        }

        private byte[] _bgBuf;
        /// <summary>Renders the current background as a 256×192 scrolled, alpha-faded frame (or null if none).</summary>
        public WriteableBitmap RenderBackground()
        {
            if (!HasBackground) return null;
            const int W = 256, H = 192;
            _bgBuf ??= new byte[W * H * 4];
            int sx = ((int)Math.Round(_bgX) % _bgW + _bgW) % _bgW;   // the BG layer wraps in both axes (NDS hardware)
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
                    _bgBuf[di + 0] = (byte)(_bgRgba[si + 2] * a);   // premultiplied BGRA
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

        // Anchor-plane depth in px-units (+z toward the camera), from: player (visual 0, bottom)
        // WET_PARTICLE_Z_A = 0x40 → ≈0; enemy (visual 1, top) Z_BB = −5248 → −30.5 (farther from the camera:
        // the real reason enemy-side effects render smaller in-game).
        private static double ZOfVis(int vis) => vis == 1 ? -5248.0 / 172.0 : 64.0 / 172.0;

        // EMTFUNC_* → emitter screen centre + travel axis + anchor-plane depth.
        private (double cx, double cy, double ax, double ay, double z) Place(int callback, int sepIndex, int sepCount)
        {
            double dx = _dfX - _atX, dy = _dfY - _atY;
            double len = Math.Sqrt(dx * dx + dy * dy); if (len > 0) { dx /= len; dy /= len; }
            switch (callback)
            {
                case 18:  // SEP_POS (EmitCall_SepPos): sets the emitter position to a per-formation offset that is 0 in
                          // a 1v1, so emtr_pos = 0 + base.pos (the emitter X-position setter adds base.pos). The chosen
                          // variant BAKES its source + aim into base.pos/base.axis (Water Gun index0 base.pos = the
                          // player's mouth, index3 = the enemy's; Hyper Beam likewise), so anchor at the PARTICLE
                          // ORIGIN and let base.pos place it, NOT the attacker (that double-offset it off-screen).
                    return (PARTICLE_ORIGIN_X, PARTICLE_ORIGIN_Y, 0, 0, 0);
                case 0:   // DummyEmitCallback (wp_tbl.c:46 -> :111) does nothing at all: it never sets an
                          // emitter position, so the emitter stays where its own data puts it, exactly like
                          // SEP_POS below. Anchoring it to the defender moved every one of the 16 moves that
                          // use it, Blizzard among them.
                    return (PARTICLE_ORIGIN_X, PARTICLE_ORIGIN_Y, 0, 0, 0);
                case 1: case 3: case 19: case 21:                                   // attacker
                    return (_atX, _atY, 0, 0, ZOfVis(_atVis));
                case 5: case 7: case 8: case 9: case 10: case 11:                    // AXIS_ATTACK family → travel to defender
                case 12: case 13: case 14: case 15: case 16:
                    return (_atX, _atY, dx, dy, ZOfVis(_atVis));
                case 6:                                                              // AXIS_DEFENCE → travel to attacker
                    return (_dfX, _dfY, -dx, -dy, ZOfVis(_dfVis));
                default:                                                             // DEFENCE_POS / DUMMY / etc.
                    return (_dfX, _dfY, 0, 0, ZOfVis(_dfVis));
            }
        }
    }
}
