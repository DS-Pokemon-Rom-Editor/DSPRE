using System;
using System.Collections.Generic;
using DSPRE.Avalonia.BattleAnim;
using DSPRE.Avalonia.Data;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.BattleAnim.Particles
{
    /// <summary>A move's particle systems as the battle runs them (src/particle_system.c, src/battle_anim emitter callbacks
    /// and script_funcs_3.c): global slot order, the world position table, emitter callbacks and moves. Singles only.</summary>
    public sealed class BattleParticles
    {
        public const int MaxSystems = 16;
        private const int PixelFactor = 172, ViewportTop = 16512;

        private sealed class PSystem
        {
            public SplManager Mgr;
            public SplCamera Cam = new SplCamera();
            public int CamAngleX, CamAngleY, CamAngleZ, CamDistance = 4 * SplFx.One;
            public IReadOnlyList<SpaTexture> Textures;
        }

        private readonly ScriptNarc _narc;
        private readonly SplRandom _rng;
        private readonly bool _attackerIsEnemy, _selfTarget;
        private readonly PSystem[] _global = new PSystem[MaxSystems];
        private readonly PSystem[] _bySlot = new PSystem[MaxSystems];
        private readonly int[] _scriptProjection = new int[MaxSystems];
        private readonly int[] _scriptFlip = new int[MaxSystems];
        private readonly Dictionary<int, SplEmitter> _emitters = new Dictionary<int, SplEmitter>();
        private readonly SplRenderer _renderer = new SplRenderer();
        private int _current;

        /// <summary>Reads the next SetExtraParams block after the running command and moves the script past it.</summary>
        public Func<int[]> ExtraParams;

        public BattleParticles(ScriptNarc narc, SplRandom rng, bool attackerIsEnemy, bool selfTarget)
        {
            _narc = narc; _rng = rng; _attackerIsEnemy = attackerIsEnemy; _selfTarget = selfTarget;
            for (int i = 0; i < MaxSystems; i++) _scriptProjection[i] = 1;
        }

        /// <summary>A contest appeal: the contestants' positions, axes and camera instead of the battle's.</summary>
        public bool Contest { get; set; }

        // Battler sides: the attacker, and the defender, who is the attacker for a move on its user.
        private bool AttackerIsPlayer => !_attackerIsEnemy;
        private bool DefenderIsPlayer => _selfTarget ? AttackerIsPlayer : !AttackerIsPlayer;
        private bool IsPlayer(bool attacker) => attacker ? AttackerIsPlayer : DefenderIsPlayer;

        public static int Const(double x) => (int)(x > 0 ? x * 4096 + 0.5 : x * 4096 - 0.5);

        public enum PosKind { Normal, Beam1, Beam2, Beam3, Hypnosis, TriAttack, HydroCannon, HyperVoice, GrassWhistle, DragonPulse, CameraLookAt, HydroPump }

        // BattleAnimUtil_GetBattlerTypeWorldPosInternal for the two single-battle battlers and the two contestants:
        // [player, enemy, contest player, contest enemy][kind] = (perspective x, y, z, orthographic x, y, z).
        private static readonly double[][][] WorldPos =
        {
            new[]
            {
                new[] { -2.3477, -1.334, 0.0156, -2.5, -1.5625, 0.0156 },
                new[] { -0.9688, -0.8125, 0.0156, -1.6035, -0.9766, 0.0156 },
                new[] { -0.9688, -0.8125, 0.0156, -1.6406, -1.414, 0.0156 },
                new[] { -2.1074, -1.6934, 0, -2.1074, -1.6934, 0 },
                new[] { -3.0625, -0.9375, 0, -3.0625, -0.9375, 0 },
                new[] { -2.3516, -1.4297, 0, -2.3516, -1.4297, 0 },
                new[] { -1.0117, -1.2695, 0, -1.0117, -1.2695, 0 },
                new[] { -3.0469, -1.0469, 0, -3.0469, -1.0469, 0 },
                new[] { -0.4375, -1.0312, 0, -0.4375, -1.0312, 0 },
                new[] { -2.0312, -1.0156, 0, -2.0312, -1.0156, 0 },
                new[] { 2.2168, 1.3516, 0, 2.2168, 1.3516, 0 },
                new[] { -1.5254, -0.7188, 0, -1.5254, -0.7188, 0 },
            },
            new[]
            {
                new[] { 2.6992, 1.0742, -1.2812, 2.5, 0.75, -1.2812 },
                new[] { 2.2812, 0.5312, -1.2812, 2.1484, 1.5781, -1.2812 },
                new[] { 2.2812, 0.5312, -1.2812, 3.5469, 1.961, -1.2812 },
                new[] { 3.1504, 1.247, 0, 3.1504, 1.247, 0 },
                new[] { 1.3262, 1.6309, 0, 1.3262, 1.6309, 0 },
                new[] { 3.209, 2.752, 0, 3.209, 2.752, 0 },
                new[] { 4.0508, 1.2617, 0, 4.0508, 1.2617, 0 },
                new[] { 3.2188, 1.8594, 0, 3.2188, 1.8594, 0 },
                new[] { 4.125, 0.8125, 0, 4.125, 0.8125, 0 },
                new[] { 2.6562, 1.0938, 0, 2.6562, 1.0938, 0 },
                new[] { -1.6934, -1.1797, 0, -1.6934, -1.1797, 0 },
                new[] { 2.0215, 1.3262, 0, 2.0215, 1.3262, 0 },
            },
            new[]
            {
                new[] { 1.7988, -1.2285, 0.0156, 3.3125, -1.5312, 0.0156 },
                new[] { 3.3125, -0.7812, 0.0156, 3.0898, -0.7188, 0.0156 },
                new[] { 3.3125, -0.7812, 0.0156, 3.3125, -0.7812, 0.0156 },
                new[] { 3.3125, -0.7812, 0, 3.3125, -0.7812, 0 },
                new[] { 1.8477, -0.7812, 0, 1.8477, -0.7812, 0 },
                new[] { 3.3125, -0.7812, 0, 3.3125, -0.7812, 0 },
                new[] { 3.3125, -0.7812, 0, 3.3125, -0.7812, 0 },
                new[] { 3.3125, -0.7812, 0, 3.3125, -0.7812, 0 },
                new[] { 3.3125, -0.7812, 0, 3.3125, -0.7812, 0 },
                new[] { 3.3125, -0.7812, 0, 3.3125, -0.7812, 0 },
                new[] { -2.3281, 1.8281, 0, -2.3281, 1.8281, 0 },
                new[] { 3.3125, -0.7812, 0, 3.3125, -0.7188, 0 },
            },
            new[]
            {
                new[] { -1.3096, 1.08, -1.2812, -1.8438, 1.0, -1.2812 },
                new[] { -0.5635, 1.0, -1.2812, -0.7285, 2.0176, -1.2812 },
                new[] { -0.5635, 1.0, -1.2812, -0.5635, 1.0, -1.2812 },
                new[] { -0.6055, 1.3672, 0, -0.6055, 1.3672, 0 },
                new[] { -2.002, 1.1465, 0, -2.002, 1.1465, 0 },
                new[] { -1.3672, 1.541, 0, -1.3672, 1.541, 0 },
                new[] { -0.1543, 1.502, 0, -0.1543, 1.502, 0 },
                new[] { -2.0625, 2.3125, 0, -2.0625, 2.3125, 0 },
                new[] { 0.125, 0.8496, 0, 0.125, 0.8496, 0 },
                new[] { -1.6719, 1.3281, 0, -1.6719, 1.3281, 0 },
                new[] { 1.2578, -1.4648, 0.666, 1.2578, -1.3203, 0.666 },
                new[] { -0.5635, 1.0, 0, -0.7285, 2.0176, 0 },
            },
        };

        private PSystem Current => _current >= 0 && _current < MaxSystems ? _bySlot[_current] : null;
        private bool CurrentOrtho => Current == null || Current.Cam.Orthographic;

        private (int X, int Y, int Z) TypePos(bool player, PosKind kind, bool ortho) => BattlerTypeWorldPos(player, Contest, ortho, kind);

        /// <summary>BattleAnimUtil_GetBattlerTypeWorldPos for a single battle's player or enemy, or a contestant.</summary>
        public static (int X, int Y, int Z) BattlerTypeWorldPos(bool player, bool contest, bool ortho, PosKind kind = PosKind.Normal)
        {
            double[] v = WorldPos[(player ? 0 : 1) + (contest ? 2 : 0)][(int)kind];
            int o = ortho ? 3 : 0;
            return (Const(v[o]), Const(v[o + 1]), Const(v[o + 2]));
        }

        // BattleAnimUtil_GetBattlerWorldPos_*: the current system's projection picks the column.
        private (int X, int Y, int Z) Pos(bool attacker, PosKind kind = PosKind.Normal) => TypePos(IsPlayer(attacker), kind, CurrentOrtho);

        // --- systems ---

        public void Load(int slot, int fileIndex)
        {
            if (slot < 0 || slot >= MaxSystems) return;
            if (_bySlot[slot] != null) Unload(slot);
            byte[] data = _narc.Get(fileIndex);
            if (data == null) return;
            PSystem ps = new PSystem { Mgr = new SplManager(data, _rng), Textures = SpaArchive.Parse(data).Textures };
            ps.Cam.Orthographic = false;
            for (int g = 0; g < MaxSystems; g++) if (_global[g] == null) { _global[g] = ps; break; }
            _bySlot[slot] = ps;
        }

        public void Unload(int slot)
        {
            if (slot < 0 || slot >= MaxSystems || _bySlot[slot] == null) return;
            PSystem ps = _bySlot[slot];
            for (int g = 0; g < MaxSystems; g++) if (_global[g] == ps) _global[g] = null;
            _bySlot[slot] = null;
        }

        public void SetCameraProjection(int slot, int projection) { if (slot >= 0 && slot < MaxSystems) _scriptProjection[slot] = projection; }
        public void SetCameraFlip(int slot, int flip) { if (slot >= 0 && slot < MaxSystems) _scriptFlip[slot] = flip; }

        public int ActiveEmitterCount
        {
            get { int n = 0; foreach (PSystem ps in _bySlot) if (ps != null) n += ps.Mgr.Active.Count; return n; }
        }

        public bool IsLoaded(int slot) => slot >= 0 && slot < MaxSystems && _bySlot[slot] != null;

        public SplEmitter Emitter(int index) => _emitters.TryGetValue(index, out SplEmitter e) ? e : null;

        /// <summary>ParticleSystem_UpdateAll.</summary>
        public void Update() { foreach (PSystem ps in _global) ps?.Mgr.Update(); }

        /// <summary>ParticleSystem_DrawAll into premultiplied BGRA.</summary>
        public byte[] Render()
        {
            foreach (PSystem ps in _global) if (ps != null) _renderer.Draw(ps.Mgr, ps.Cam, ps.Textures);
            return _renderer.Flush();
        }

        public IEnumerable<SplManager> Managers() { foreach (PSystem ps in _global) if (ps != null) yield return ps.Mgr; }

        // --- emitters ---

        /// <summary>BattleAnimScriptCmd_CreateEmitter(Ex): camera flip, projection, then the callback. A self-maintaining
        /// emitter is stored as null, as the game does.</summary>
        public SplEmitter CreateEmitter(int slot, int index, int resourceId, int callback)
        {
            if (slot < 0 || slot >= MaxSystems || _bySlot[slot] == null) return null;
            _current = slot;
            PSystem ps = _bySlot[slot];
            if (_scriptFlip[slot] != 0) ps.Cam.UpY = -ps.Cam.UpY;
            ps.Cam.Orthographic = _scriptProjection[slot] != 0;
            SplEmitter e = ps.Mgr.CreateEmitter(resourceId, em => RunCallback(callback, em));
            SplEmitter stored = e != null && e.Resource.SelfMaintaining ? null : e;
            _emitters[index] = stored;
            return e;
        }

        private int[] Extra(int count)
        {
            int[] got = ExtraParams?.Invoke();
            int[] p = new int[count];
            if (got != null) Array.Copy(got, p, Math.Min(count, got.Length));
            return p;
        }

        private void RunCallback(int id, SplEmitter e)
        {
            switch (id)
            {
                case 1:
                case 2:
                {
                    // SetPosToEnemy1 ends up on player 1 too, through its hardcoded battlers.
                    (int X, int Y, int Z) p = TypePos(true, PosKind.Normal, CurrentOrtho);
                    e.SetPos(p.X, p.Y, p.Z);
                    if (id == 2) { Current.Cam.UpX = 0; Current.Cam.UpY = DefenderIsPlayer ? SplFx.One : -SplFx.One; Current.Cam.UpZ = 0; }
                    break;
                }
                case 3: case 21: { (int X, int Y, int Z) p = Pos(true); e.SetPos(p.X, p.Y, p.Z); break; }
                case 4: { (int X, int Y, int Z) p = Pos(false); e.SetPos(p.X, p.Y, p.Z); break; }
                case 5: AxisPos(e, true, 0, 0); break;
                case 6: AxisPos(e, false, 0, 0); break;
                case 7: AxisPos(e, true, 1, 1); break;
                case 8: AxisPos(e, true, 1, 2); break;
                case 9: AxisPos(e, true, 1, 3); break;
                case 10: AxisPos(e, true, 1, 4); break;
                case 11: AxisPos(e, true, 1, 5); break;
                case 12: AxisPos(e, true, 2, 1); break;
                case 13: AxisPos(e, true, 2, 2); break;
                case 14: AxisPos(e, true, 2, 3); break;
                case 15: AxisPos(e, true, 2, 4); break;
                case 16: AxisPos(e, true, 2, 5); break;
                case 17: Generic(e); break;
                case 18: e.SetPos(0, 0, 0); break;
                case 19: { (int X, int Y, int Z) p = TypePos(AttackerIsPlayer, PosKind.Normal, CurrentOrtho); e.SetPos(p.X, p.Y, p.Z); break; }
                case 20: { (int X, int Y, int Z) p = TypePos(DefenderIsPlayer, PosKind.Normal, CurrentOrtho); e.SetPos(p.X, p.Y, p.Z); break; }
            }
        }

        private static SplBehavior Behavior(SplEmitter e, SplBehaviorKind kind)
        {
            foreach (SplBehavior b in e.Resource.Behaviors) if (b.Kind == kind) return b;
            return null;
        }

        private static int Sign(bool player) => player ? 1 : -1;
        private const int CenterY = 3200;   // BATTLE_3D_CENTER_Y, FX32_CONST(1 / 1.28)

        // BattleParticleUtil_SetEmitterAxisPosEx for single battles; forward is attacker to defender.
        private void AxisPos(SplEmitter e, bool forward, int behaviorMode, int targetMode)
        {
            bool startPlayer = forward ? AttackerIsPlayer : DefenderIsPlayer;
            int[] dirPlToEm = Extra(3);
            for (int i = 0; i < 6; i++) Extra(3);
            short dx = (short)(startPlayer ? dirPlToEm[0] : -dirPlToEm[0]);
            short dy = (short)(startPlayer ? dirPlToEm[1] : -dirPlToEm[1]);
            short dz = (short)(startPlayer ? dirPlToEm[2] : -dirPlToEm[2]);
            int sign = Sign(startPlayer);
            int[] ex = Extra(3);
            (int X, int Y, int Z) start = Pos(forward);
            int px, py, pz;
            if (ex[0] == 0 && ex[1] == 0 && ex[2] == 0) { px = start.X; py = start.Y; pz = start.Z; }
            else
            {
                if (ex[0] == 0 && ex[1] == CenterY) sign = 1;
                px = ex[0] * sign; py = ex[1] * sign; pz = start.Z * sign;
            }
            e.SetPos(px, py, pz);
            if (behaviorMode != 0)
            {
                SplBehavior b = Behavior(e, behaviorMode == 1 ? SplBehaviorKind.Convergence : SplBehaviorKind.Magnet);
                int tx = 0, ty = 0, tz = 0;
                switch (targetMode)
                {
                    case 1: if (b != null) { tx = b.TargetX * sign; ty = b.TargetY * sign; tz = b.TargetZ * sign; } break;
                    case 2: tx = 0; ty = CenterY * sign; tz = 0; break;
                    case 3: { (int X, int Y, int Z) d = Pos(!forward); tx = d.X; ty = d.Y; tz = d.Z; break; }
                    case 4: { (int X, int Y, int Z) a = Pos(forward); tx = a.X; ty = a.Y; tz = a.Z; break; }
                    case 5:
                    {
                        int[] p = Extra(4);
                        tx = p[0]; ty = p[1]; tz = p[2];
                        switch (p[3])
                        {
                            case 1: tx *= sign; break;
                            case 2: ty *= sign; break;
                            case 3: tz *= sign; break;
                            case 4: tx *= sign; ty *= sign; break;
                            case 5: tx *= sign; tz *= sign; break;
                            case 6: ty *= sign; tz *= sign; break;
                            case 7: tx *= sign; ty *= sign; tz *= sign; break;
                        }
                        break;
                    }
                }
                if (b != null) { b.TargetX = tx - px; b.TargetY = ty - py; b.TargetZ = tz - pz; }
            }
            e.AxisX = dx; e.AxisY = dy; e.AxisZ = dz;
        }

        // --- the generic callback ---

        private static readonly double[][] AxisConfig =
        {
            new[] { 0.9219, 0.5156, 0.748, -1.0322, -0.666, 0.748 },
            new[] { 0.588, 0.3047, 0.0, -0.377, -0.2285, 0.0 },
            new[] { 0.3438, 0.1797, 0.0, -0.295, -0.1914, 0.0 },
            new[] { 0.6172, 0.3877, 0.0, -1.041, -1.2344, 0.0 },
            new[] { 0.4766, 0.2676, 0.0, -0.4922, -0.2363, 0.0 },
            new[] { 0.7812, 0.42, 0.0, -0.8594, -0.4824, 0.0 },
            new[] { 0.3906, -0.0156, 0.0, -0.4531, -0.3926, 0.0 },
            new[] { 0.875, 0.5, 0.0, -0.8281, -0.4336, 0.0 },
        };

        private static (int X, int Y, int Z) Config(int table, bool startPlayer, bool endPlayer)
        {
            if (startPlayer == endPlayer) return (0, 0, 0);
            double[] c = AxisConfig[table];
            int o = startPlayer ? 0 : 3;
            return (Const(c[o]), Const(c[o + 1]), Const(c[o + 2]));
        }

        private sealed class GenericData
        {
            public int[] Params;
            public bool StartIsAttacker;
            public bool StartPlayer, EndPlayer;
            public int Dir;
            public int PosX, PosY, PosZ;
        }

        private void Generic(SplEmitter e)
        {
            GenericData d = new GenericData { Params = Extra(6) };
            int target = d.Params[1];
            d.StartIsAttacker = !(target == 1 || target == 3);
            d.StartPlayer = IsPlayer(d.StartIsAttacker);
            d.EndPlayer = IsPlayer(!d.StartIsAttacker);
            if (d.Params[2] != 0) GenericPosition(e, d);
            if (d.Params[3] != 0) GenericAxis(e, d);
            if (d.Params[4] != 0) GenericBehavior(e, d);
            GenericCamera(d);
            if (d.Params[0] == 1 || d.Params[0] == 2)
            {
                int z = d.Params[0] == 1 ? (d.EndPlayer ? Const(0.0625) : Const(-1.25)) : (d.EndPlayer ? 0 : Const(-1.3125));
                e.PosZ = z + e.Resource.BaseZ;
            }
        }

        private static int Dir(GenericData d) => d.StartPlayer ? 1 : -1;

        private void Offset(GenericData d, ref int x, ref int y, ref int z)
        {
            int[] p = Extra(4);
            d.Dir = Dir(d);
            if (p[0] == 1) d.Dir = 1;
            x += p[1] * d.Dir; y += p[2] * d.Dir; z += p[3] * d.Dir;
        }

        private void OffsetForBattler(GenericData d, bool player, ref int x, ref int y, ref int z)
        {
            int[] chosen = null;
            for (int type = 0; type < 6; type++)
            {
                int[] p = Extra(4);
                if ((type == 0 && player) || (type == 1 && !player)) chosen = p;
            }
            chosen ??= new int[4];
            d.Dir = Dir(d);
            if (chosen[0] == 1) d.Dir = 1;
            x += chosen[1] * d.Dir; y += chosen[2] * d.Dir; z += chosen[3] * d.Dir;
        }

        private void GenericPosition(SplEmitter e, GenericData d)
        {
            bool ortho = CurrentOrtho;
            (int X, int Y, int Z) Get(bool start, PosKind k) => TypePos(start ? d.StartPlayer : d.EndPlayer, k, ortho);
            int x = 0, y = 0, z = 0;
            void Set((int X, int Y, int Z) p) { x = p.X; y = p.Y; z = p.Z; }
            switch (d.Params[2])
            {
                case 1: Set(Get(true, PosKind.Normal)); break;
                case 2: Set(Get(false, PosKind.Normal)); break;
                case 3:
                {
                    int[] p = Extra(4);
                    d.Dir = Dir(d);
                    if (p[0] == 1) d.Dir = 1;
                    x = p[1] * d.Dir; y = p[2] * d.Dir; z = p[3];
                    break;
                }
                case 4: Set(Get(true, PosKind.Normal)); Offset(d, ref x, ref y, ref z); break;
                case 5: Set(Get(false, PosKind.Normal)); Offset(d, ref x, ref y, ref z); break;
                case 6: Set(Get(true, PosKind.Beam1)); break;
                case 7: Set(Get(false, PosKind.Beam1)); break;
                case 8: Set(Get(true, PosKind.HydroPump)); break;
                case 9: Set(Get(false, PosKind.HydroPump)); break;
                case 10: Set(Get(true, PosKind.Beam2)); break;
                case 11: Set(Get(false, PosKind.Beam2)); break;
                case 12: Set(TypePos(AttackerIsPlayer, PosKind.Normal, ortho)); Offset(d, ref x, ref y, ref z); break;
                case 13: Set(TypePos(DefenderIsPlayer, PosKind.Normal, ortho)); Offset(d, ref x, ref y, ref z); break;
                case 14: Set(Get(true, PosKind.Beam3)); break;
                case 15: Set(Get(false, PosKind.Beam3)); break;
                case 16: Set(Get(true, PosKind.Hypnosis)); break;
                case 17: Set(Get(false, PosKind.Hypnosis)); break;
                case 18: Set(Get(true, PosKind.TriAttack)); break;
                case 19: Set(Get(false, PosKind.TriAttack)); break;
                case 20: Set(Get(true, PosKind.HydroCannon)); break;
                case 21: Set(Get(false, PosKind.HydroCannon)); break;
                case 22: Set(Get(true, PosKind.HyperVoice)); break;
                case 23: Set(Get(false, PosKind.HyperVoice)); break;
                case 24: Set(Get(true, PosKind.GrassWhistle)); break;
                case 25: Set(Get(false, PosKind.GrassWhistle)); break;
                case 26: Set(Get(true, PosKind.DragonPulse)); break;
                case 27: Set(Get(false, PosKind.DragonPulse)); break;
                case 28: x = Const(2.8047); y = 0; z = 0; break;
                case 30:
                    x = Const(Contest ? 3.662 : d.StartPlayer ? -2.6904 : 2.6562);
                    y = Const(Contest ? -0.6719 : d.StartPlayer ? -0.8516 : 1.8691); z = 0;
                    break;
                case 31: x = Const(d.StartPlayer ? -1.4063 : 2.3164); y = Const(d.StartPlayer ? -1.0625 : -0.4844); z = 0; break;
                case 32:
                    x = Const(Contest ? 1.7266 : d.StartPlayer ? -1.125 : 1.8613);
                    y = Const(Contest ? -0.8906 : d.StartPlayer ? -1.0938 : 0.5488); z = 0;
                    break;
                case 33: x = Const(-1.2207); y = Const(-1.4648); z = 0; break;
                case 34: Set(Get(true, PosKind.Normal)); break;
                case 100: Set(Get(true, PosKind.Normal)); OffsetForBattler(d, d.StartPlayer, ref x, ref y, ref z); break;
                case 101: Set(Get(false, PosKind.Normal)); OffsetForBattler(d, d.EndPlayer, ref x, ref y, ref z); break;
            }
            d.PosX = x; d.PosY = y; d.PosZ = z;
            e.SetPos(x, y, z);
        }

        private void GenericAxis(SplEmitter e, GenericData d)
        {
            int x = 0, y = 0, z = 0;
            void From((int X, int Y, int Z) c, bool halfZ) { x = c.X; y = c.Y; z = halfZ ? c.Z / 2 : c.Z; }
            void Set(double ax, double ay, double az) { x = Const(ax); y = Const(ay); z = Const(az); }
            if (Contest && ContestAxis(d, Set, From))
            {
                e.AxisX = (short)x; e.AxisY = (short)y; e.AxisZ = (short)z;
                return;
            }
            switch (d.Params[3])
            {
                case 1: case 2: From(Config(0, d.StartPlayer, d.EndPlayer), true); break;
                case 4: case 5:
                    if (d.StartPlayer == d.EndPlayer) From(Config(0, d.StartPlayer, d.EndPlayer), true);
                    else From(Config(0, d.StartPlayer, !d.StartPlayer), true);
                    break;
                case 8: case 9: From(Config(1, d.StartPlayer, d.EndPlayer), true); break;
                case 10: case 11:
                {
                    // The x comes from config 3 and y and z from config 2.
                    (int X, int Y, int Z) c3 = Config(2, d.StartPlayer, d.EndPlayer), c2 = Config(1, d.StartPlayer, d.EndPlayer);
                    x = c3.X; y = c2.Y; z = c2.Z / 2;
                    break;
                }
                case 12: case 13: From(Config(3, d.StartPlayer, d.EndPlayer), false); break;
                case 14: case 15: From(Config(4, d.StartPlayer, d.EndPlayer), false); break;
                case 16: case 17: From(Config(5, d.StartPlayer, d.EndPlayer), false); break;
                case 18: case 19: From(Config(6, d.StartPlayer, d.EndPlayer), false); break;
                case 20: case 21: From(Config(7, d.StartPlayer, d.EndPlayer), false); break;
                case 6: case 7:
                {
                    int s = d.StartPlayer ? 1 : -1;
                    x = Const(0.7754) * s; y = Const(0.4785) * s; z = Const(-0.0723) * s;
                    break;
                }
                case 3: x = Const(-0.1953); y = Const(0.293); z = Const(0.122); break;
                case 22: x = Const(-0.8325); y = Const(-0.6455); z = 0; break;
                case 25: x = Const(-0.8398); y = Const(0.4766); z = 0; break;
                case 26:
                    if (d.StartPlayer) { x = Const(0.9219); y = Const(0.5156); } else { x = Const(-1.4648); y = Const(-0.537); }
                    z = 0;
                    break;
                case 24:
                    if (d.StartPlayer) { x = Const(0.6992); y = Const(0.916); } else { x = Const(-0.7188); y = Const(0.3555); }
                    z = 0;
                    break;
            }
            e.AxisX = (short)x; e.AxisY = (short)y; e.AxisZ = (short)z;
        }

        // The contest's own axes in GenericEmitterCallback_ApplyAxis; false for the modes a contest shares with battle.
        private static bool ContestAxis(GenericData d, Action<double, double, double> set, Action<(int X, int Y, int Z), bool> from)
        {
            bool towardDefender = d.Params[1] == 2;
            switch (d.Params[3])
            {
                case 1: case 2:
                    if (towardDefender) set(-0.8145, 0.4492, 0.1797); else set(0.5703, -0.3516, 0.1797);
                    return true;
                case 4: case 5:
                {
                    (int X, int Y, int Z) c = Config(0, d.StartPlayer, d.EndPlayer);
                    from((-c.X, c.Y, c.Z), true);
                    return true;
                }
                case 6: case 7:
                {
                    // The contest config flips for a start on the enemy side, as the battle one does.
                    double s = d.StartPlayer ? 1 : -1;
                    set(-0.5703 * s, 0.3516 * s, 0.1797 * s);
                    return true;
                }
                case 8: case 9:
                    if (towardDefender) set(-0.5703, 0.3516, 0.1797); else set(0.5703, -0.3516, 0.1797);
                    return true;
                case 12: case 13: set(-0.7168, 0.3516, 0.1797); return true;
                case 10: case 11: case 14: case 15: case 16: case 17: case 18: case 19: set(-0.5703, 0.3516, 0.1797); return true;
                case 20: case 21: set(-1.0586, 0.3516, 0.1797); return true;
                default: return false;
            }
        }

        private void GenericBehavior(SplEmitter e, GenericData d)
        {
            int mask = d.Params[4];
            for (int value = 1; value < 14; value++)
            {
                int flag = 1 << value;
                if ((mask & flag) != flag) continue;
                switch (value)
                {
                    case 1: GravityMag(e, d); break;
                    case 4: MagnetPos(e, d); break;
                    case 5: Force(e, d, SplBehaviorKind.Magnet); break;
                    case 6: SpinAngle(e, d); break;
                    case 7: SpinAxis(e, d); break;
                    case 12: ConvergencePos(e, d); break;
                    case 13: Force(e, d, SplBehaviorKind.Convergence); break;
                }
            }
        }

        private int[] BehaviorParams(GenericData d)
        {
            int[] p = Extra(5);
            d.Dir = Dir(d);
            if (p[1] == 1) d.Dir = 1;
            return p;
        }

        private void GravityMag(SplEmitter e, GenericData d)
        {
            int[] p = BehaviorParams(d);
            SplBehavior b = Behavior(e, SplBehaviorKind.Gravity);
            if (p[0] == 1)
            {
                if (b != null) { b.MagX = (short)((short)p[2] * d.Dir); b.MagY = (short)((short)p[3] * d.Dir); b.MagZ = (short)((short)p[4] * d.Dir); }
                return;
            }
            // Every other mode sets zero and then falls through to minus the emitter's position, as the game's code does.
            if (b != null) { b.MagX = (short)-d.PosX; b.MagY = (short)-d.PosY; b.MagZ = (short)-d.PosZ; }
        }

        private void MagnetPos(SplEmitter e, GenericData d)
        {
            int[] p = BehaviorParams(d);
            SplBehavior b = Behavior(e, SplBehaviorKind.Magnet);
            int x = 0, y = 0, z = 0;
            switch (p[0])
            {
                case 0: if (b != null) { x = b.TargetX * d.Dir; y = b.TargetY * d.Dir; z = b.TargetZ * d.Dir; } break;
                case 1: x = p[2] * d.Dir; y = p[3] * d.Dir; z = p[4] * d.Dir; break;
                case 2: { (int X, int Y, int Z) s = Pos(d.StartIsAttacker); x = s.X; y = s.Y; z = s.Z; break; }
                case 3: { (int X, int Y, int Z) s = Pos(!d.StartIsAttacker); x = s.X; y = s.Y; z = s.Z; break; }
            }
            if (b != null) { b.TargetX = x - d.PosX; b.TargetY = y - d.PosY; b.TargetZ = z; }
        }

        private void ConvergencePos(SplEmitter e, GenericData d)
        {
            int[] p = BehaviorParams(d);
            SplBehavior b = Behavior(e, SplBehaviorKind.Convergence);
            int x = 0, y = 0, z = 0;
            switch (p[0])
            {
                case 0: if (b != null) { x = b.TargetX * d.Dir; y = b.TargetY * d.Dir; z = b.TargetZ * d.Dir; } break;
                case 1: x = p[2] * d.Dir; y = p[3] * d.Dir; z = p[4] * d.Dir; break;
                case 4:
                {
                    (int X, int Y, int Z) t = Pos(!d.StartIsAttacker);
                    int den = p[3] == 0 ? 1 : p[3];
                    x = t.X * p[2] / den; y = t.Y * p[2] / den; z = t.Z * p[2] / den;
                    break;
                }
                case 2: { (int X, int Y, int Z) s = Pos(d.StartIsAttacker); x = s.X; y = s.Y; z = s.Z; break; }
                case 3: { (int X, int Y, int Z) s = Pos(!d.StartIsAttacker); x = s.X; y = s.Y; z = s.Z; break; }
            }
            if (b != null) { b.TargetX = x - d.PosX; b.TargetY = y - d.PosY; b.TargetZ = z - d.PosZ; }
        }

        private void Force(SplEmitter e, GenericData d, SplBehaviorKind kind)
        {
            int[] p = BehaviorParams(d);
            SplBehavior b = Behavior(e, kind);
            short force = 0;
            if (p[0] == 0 && b != null) force = (short)(b.Force * d.Dir);
            else if (p[0] == 1) force = (short)((short)p[2] * d.Dir);
            if (b != null) b.Force = force;
        }

        private void SpinAngle(SplEmitter e, GenericData d)
        {
            int[] p = BehaviorParams(d);
            SplBehavior b = Behavior(e, SplBehaviorKind.Spin);
            ushort angle = 0;
            if (p[0] == 0 && b != null) angle = (ushort)(b.SpinAngle * d.Dir);
            else if (p[0] == 1) angle = (ushort)((ushort)p[2] * d.Dir);
            if (b != null) b.SpinAngle = angle;
        }

        private void SpinAxis(SplEmitter e, GenericData d)
        {
            int[] p = BehaviorParams(d);
            SplBehavior b = Behavior(e, SplBehaviorKind.Spin);
            ushort axis = 0;
            if (p[0] == 0 && b != null) axis = (ushort)b.SpinAxis;
            else if (p[0] == 1) axis = (ushort)p[2];
            if (b != null) b.SpinAxis = axis;
        }

        private static ushort DegIdx(double degrees) => BattleAnimMath.DegToIdx(degrees);

        private void GenericCamera(GenericData d)
        {
            PSystem ps = Current;
            if (ps == null) return;
            switch (d.Params[5])
            {
                case 0: SetAngle(ps, 0, 0, 0); break;
                case 1: ps.Cam.Orthographic = true; SetAngle(ps, (ushort)(DegIdx(45) + 1), (ushort)(DegIdx(45) + 1), 0); break;
                case 2: { int[] p = Extra(4); SetAngle(ps, (ushort)p[1], (ushort)p[2], (ushort)p[3]); break; }
                case 3: ps.Cam.Orthographic = true; SetAngle(ps, (ushort)-DegIdx(67.502), (ushort)(DegIdx(45) + 1), (ushort)(DegIdx(22.5) + 1)); break;
                case 4:
                    ps.Cam.Orthographic = true;
                    if (Contest) SetAngle(ps, DegIdx(272.817), DegIdx(32.696), (ushort)(DegIdx(22.5) + 1));
                    else SetAngle(ps, (ushort)(DegIdx(50.8) + 1), DegIdx(20.567), 0);
                    break;
                case 5: ps.Cam.Orthographic = true; SetAngle(ps, DegIdx(272.817), DegIdx(32.696), (ushort)(DegIdx(22.5) + 1)); break;
                case 6: ps.Cam.Orthographic = true; SetAngle(ps, 0, 0, (ushort)-DegIdx(32.96)); break;
                case 7: { (int X, int Y, int Z) t = TypePos(d.StartPlayer, PosKind.CameraLookAt, ps.Cam.Orthographic); ps.Cam.TargetX = t.X; ps.Cam.TargetY = t.Y; ps.Cam.TargetZ = t.Z; break; }
                case 8: { (int X, int Y, int Z) t = TypePos(d.EndPlayer, PosKind.CameraLookAt, ps.Cam.Orthographic); ps.Cam.TargetX = t.X; ps.Cam.TargetY = t.Y; ps.Cam.TargetZ = t.Z; break; }
            }
        }

        // Camera_SetAngleAroundTarget.
        private static void SetAngle(PSystem ps, ushort ax, ushort ay, ushort az)
        {
            ps.CamAngleX = ax; ps.CamAngleY = ay; ps.CamAngleZ = az;
            int dist = ps.CamDistance;
            ps.Cam.PosX = SplFx.Mul(SplFx.Mul(SplFx.Sin(ay), dist), SplFx.Cos(ax)) + ps.Cam.TargetX;
            ps.Cam.PosZ = SplFx.Mul(SplFx.Mul(SplFx.Cos(ay), dist), SplFx.Cos(ax)) + ps.Cam.TargetZ;
            ps.Cam.PosY = SplFx.Mul(SplFx.Sin((ushort)-ax), dist) + ps.Cam.TargetY;
        }

        // --- the emitter-moving routines ---

        // IsEmitterActive: the oldest particle (the list's last) not on its last frame. With no particles the game reads
        // uninitialised values; this treats that as inactive.
        private static bool Active(SplEmitter e)
        {
            if (e == null || e.Particles.Last == null) return false;
            return e.Particles.Last.Age != e.Particles.Last.LifeTime;
        }

        private void Delete(SplEmitter e)
        {
            if (e == null) return;
            foreach (PSystem ps in _global)
                if (ps != null)
                    for (SplEmitter x = ps.Mgr.Active.First; x != null; x = x.Next)
                        if (x == e) { ps.Mgr.DeleteEmitter(e); return; }
        }

        private static int ToScreen(int world) => world / PixelFactor;

        /// <summary>MoveEmitterA2BLinear and Parabolic, vars: emitter, offset x and y, start delay, frames, radius, mode,
        /// params, curve. The task returns false once it has ended.</summary>
        public Func<bool> MoveEmitterA2B(int[] v, bool parabolic)
        {
            SplEmitter e = Emitter(v[0]);
            int offX = v[1], offY = v[2], startDelay = (short)v[3], frames = (short)v[4], radius = v[5], mode = v[6];
            int prm = v[7];
            bool curve = v[8] != 0;
            int skip = (int)(((uint)prm & 0xFFFF0000) >> 16), max = prm & 0xFFFF;
            if (max == 0) max = 0xFF;
            bool forward = mode == 0;
            int dir = AttackerIsPlayer ? 1 : -1;
            (int X, int Y, int Z) s = Pos(forward), en = Pos(!forward);
            int sx = ToScreen(s.X), ex = ToScreen(en.X) + offX * dir, sy = ToScreen(s.Y), ey = ToScreen(en.Y) + offY * dir;
            Parabolic arc = parabolic ? new Parabolic(sx, ex, sy, ey, frames, radius * -SplFx.One) : null;
            PosLerp lin = parabolic ? arc.Linear : new PosLerp(sx, ex, sy, ey, frames);
            bool Step() => parabolic ? arc.Update() : lin.Update();
            for (int i = 0; i < skip; i++) Step();
            int frame = max != 0xFF ? max + 1 : 0;
            int timer = 0, angle = 0;
            if (e != null) { e.PosX = lin.X * PixelFactor + e.Resource.BaseX; e.PosY = lin.Y * PixelFactor + e.Resource.BaseY; }
            return () =>
            {
                bool active = Active(e);
                if (!parabolic && timer < startDelay) { timer++; return true; }
                if (!Step() && !active) { Delete(e); return false; }
                if (!parabolic && (skip > frame || max < frame) && max != 0xFF) return true;
                if (e != null)
                {
                    e.PosX = lin.X * PixelFactor + e.Resource.BaseX;
                    e.PosY = lin.Y * PixelFactor + e.Resource.BaseY;
                    if (!parabolic && curve && frames != 0)
                    {
                        angle += 360 / frames;
                        int deg = (ushort)angle % 360;
                        // CalcSineDegrees: its SDK degree conversion isn't in the public decomps, so this one is unverified.
                        e.PosY = lin.Y * PixelFactor + SplFx.Sin(DegIdx(deg)) + e.Resource.BaseY;
                    }
                }
                return true;
            };
        }

        /// <summary>MoveEmitterViewportTop, vars: emitter, mode, type, frames, start delay, params.</summary>
        public Func<bool> MoveEmitterViewportTop(int[] v)
        {
            SplEmitter e = Emitter(v[0]);
            bool attacker = v[1] == 0;
            bool fromTop = v[2] == 0;
            int frames = (short)v[3], startDelay = (short)v[4], prm = v[5];
            int skip = (int)(((uint)prm & 0xFFFF0000) >> 16), max = prm & 0xFFFF;
            if (max == 0) max = 0xFF;
            (int X, int Y, int Z) b = Pos(attacker);
            int topY = ViewportTop + 60 * PixelFactor;
            int sx = b.X, sy = b.Y, ex = b.X, ey = b.Y;
            if (fromTop) sy = topY; else ey = topY;
            PosLerp lin = new PosLerp(ToScreen(sx), ToScreen(ex), ToScreen(sy), ToScreen(ey), frames);
            for (int i = 0; i < skip; i++) lin.Update();
            int frame = max != 0xFF ? max + 1 : 0;
            int timer = 0;
            if (e != null) { e.PosX = lin.X * PixelFactor + e.Resource.BaseX; e.PosY = lin.Y * PixelFactor + e.Resource.BaseY; }
            return () =>
            {
                bool active = Active(e);
                if (timer < startDelay) { timer++; return true; }
                if (!lin.Update() && !active) { Delete(e); return false; }
                if ((skip > frame || max < frame) && max != 0xFF) return true;
                if (e != null) { e.PosX = lin.X * PixelFactor + e.Resource.BaseX; e.PosY = lin.Y * PixelFactor + e.Resource.BaseY; }
                return true;
            };
        }

        /// <summary>RevolveEmitter, vars: emitter, start and end x, start and end y, radius x and y, frames, mode, particle
        /// system.</summary>
        public Func<bool> RevolveEmitter(int[] v)
        {
            SplEmitter e = Emitter(v[0]);
            bool attacker = v[8] == 0;
            (int X, int Y, int Z) bp = Pos(attacker);
            Revolution revs = new Revolution(DegIdx(v[1]), DegIdx(v[2]), DegIdx(v[3]), DegIdx(v[4]), v[5] * SplFx.One, v[6] * SplFx.One, v[7]);
            revs.Update();
            if (e != null) { e.PosX = bp.X + revs.X * PixelFactor + e.Resource.BaseX; e.PosY = bp.Y + revs.Y * PixelFactor + e.Resource.BaseY; }
            return () =>
            {
                bool active = Active(e);
                if (!revs.Update() && !active) { Delete(e); return false; }
                if (e != null) { e.PosX = bp.X + revs.X * PixelFactor + e.Resource.BaseX; e.PosY = bp.Y + revs.Y * PixelFactor + e.Resource.BaseY; }
                return true;
            };
        }
    }
}
