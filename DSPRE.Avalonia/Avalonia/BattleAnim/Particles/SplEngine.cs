using System;
using System.Collections.Generic;
using DSPRE.Avalonia.Data;

namespace DSPRE.Avalonia.BattleAnim.Particles
{
    /// <summary>
    /// Fixed-point helpers as the DS SDK computes them: 20.12 values, rounded multiplies, and the hardware square root and
    /// divider behind vector normalisation. Values are bit-exact, so particles land where the game puts them.
    /// </summary>
    public static class SplFx
    {
        public const int One = 4096;

        public static int Mul(int a, int b) => (int)(((long)a * b + 0x800) >> 12);

        // FX_Div through the hardware divider, rounded to 12 fraction bits.
        public static int Div(int n, int d) => d == 0 ? 0 : (int)((((long)n << 32) / d + (1L << 19)) >> 20);

        private static readonly short[] SinCos = BuildSinCos();

        // sin and cos of idx * 2pi / 4096, each rounded half away from zero as FX16_CONST does.
        private static short[] BuildSinCos()
        {
            short[] t = new short[4096 * 2];
            for (int i = 0; i < 4096; i++)
            {
                double a = i * 2.0 * Math.PI / 4096.0;
                t[i * 2] = Fx16Const(Math.Sin(a));
                t[i * 2 + 1] = Fx16Const(Math.Cos(a));
            }
            return t;
        }

        private static short Fx16Const(double x) => (short)(x > 0 ? x * 4096 + 0.5 : x * 4096 - 0.5);

        public static int Sin(int idx) => SinCos[((idx & 0xFFFF) >> 4) << 1];
        public static int Cos(int idx) => SinCos[(((idx & 0xFFFF) >> 4) << 1) + 1];

        private static long Isqrt(ulong v)
        {
            ulong r = (ulong)Math.Sqrt(v);
            while (r * r > v) r--;
            while ((r + 1) * (r + 1) <= v) r++;
            return (long)r;
        }

        // VEC_Normalize: 1/sqrt(l) as sqrt(4l) * (2^56 / l), applied with a 45-bit rounding shift.
        public static void Normalize(ref int x, ref int y, ref int z)
        {
            long l2 = (long)x * x + (long)y * y + (long)z * z;
            if (l2 == 0) return;
            long sqrt = (int)Isqrt((ulong)(l2 * 4));
            long inv = 0x0100000000000000L / l2;
            long k = unchecked(sqrt * inv);
            x = (int)(unchecked(k * x + (1L << 44)) >> 45);
            y = (int)(unchecked(k * y + (1L << 44)) >> 45);
            z = (int)(unchecked(k * z + (1L << 44)) >> 45);
        }

        public static void Normalize16(ref short x, ref short y, ref short z)
        {
            long l2 = x * x + y * y + z * z;
            if (l2 == 0) return;
            long sqrt = (int)Isqrt((ulong)(l2 * 4));
            long inv = 0x0100000000000000L / l2;
            long k = unchecked(sqrt * inv);
            x = (short)(unchecked(k * x + (1L << 44)) >> 45);
            y = (short)(unchecked(k * y + (1L << 44)) >> 45);
            z = (short)(unchecked(k * z + (1L << 44)) >> 45);
        }

        public static int Dot(int ax, int ay, int az, int bx, int by, int bz) =>
            (int)(((long)ax * bx + (long)ay * by + (long)az * bz + 0x800) >> 12);

        public static int Dot16(short ax, short ay, short az, short bx, short by, short bz) =>
            (int)(((long)(ax * bx + ay * by) + (az * bz + 0x800)) >> 12);

        public static (short X, short Y, short Z) Cross16(short ax, short ay, short az, short bx, short by, short bz) =>
            ((short)((ay * bz - az * by + 0x800) >> 12), (short)((az * bx - ax * bz + 0x800) >> 12), (short)((ax * by - ay * bx + 0x800) >> 12));

        public static int Distance(int ax, int ay, int az, int bx, int by, int bz)
        {
            long dx = ax - bx, dy = ay - by, dz = az - bz;
            long l2 = dx * dx + dy * dy + dz * dz;
            return (int)((Isqrt((ulong)(l2 * 4)) + 1) >> 1);
        }
    }

    /// <summary>One emitter definition of an SPA file, read in the particle library's own layout.</summary>
    public sealed class SplResource
    {
        public uint Flags;
        public int EmissionType => (int)(Flags & 0xF);
        public int DrawType => (int)((Flags >> 4) & 3);
        public int CircleAxis => (int)((Flags >> 6) & 3);
        public bool HasScaleAnim => (Flags & (1u << 8)) != 0;
        public bool HasColorAnim => (Flags & (1u << 9)) != 0;
        public bool HasAlphaAnim => (Flags & (1u << 10)) != 0;
        public bool HasTexAnim => (Flags & (1u << 11)) != 0;
        public bool HasRotation => (Flags & (1u << 12)) != 0;
        public bool RandomInitAngle => (Flags & (1u << 13)) != 0;
        public bool SelfMaintaining => (Flags & (1u << 14)) != 0;
        public bool FollowEmitter => (Flags & (1u << 15)) != 0;
        public bool HasChild => (Flags & (1u << 16)) != 0;
        public int PolygonRotAxis => (int)((Flags >> 17) & 3);
        public int PolygonReferencePlane => (int)((Flags >> 19) & 1);
        public bool RandomizeLoopedAnim => (Flags & (1u << 20)) != 0;
        public bool DrawChildrenFirst => (Flags & (1u << 21)) != 0;
        public bool HideParent => (Flags & (1u << 22)) != 0;
        public bool UseViewSpace => (Flags & (1u << 23)) != 0;
        public bool HasFixedPolygonId => (Flags & (1u << 30)) != 0;
        public bool ChildHasFixedPolygonId => (Flags & (1u << 31)) != 0;

        public int BaseX, BaseY, BaseZ;
        public int EmissionCount, Radius, Length;
        public short AxisX, AxisY, AxisZ;
        public ushort Color;
        public int InitVelPos, InitVelAxis, BaseScale;
        public short Aspect;
        public ushort StartDelay;
        public short MinRotation, MaxRotation;
        public ushort InitAngle;
        public ushort EmitterLifeTime, ParticleLifeTime;
        public int RndBaseScale, RndLifeTime, RndInitVel;
        public int EmissionInterval, BaseAlpha, AirResistance, TextureIndex, LoopFrames, DbbScale;
        public int TileS, TileT, ScaleAnimDir;
        public bool DpolFaceEmitter, FlipS, FlipT;
        public short PolygonX, PolygonY;

        public bool ScaleLoop; public short ScaleStart, ScaleMid, ScaleEnd; public int ScaleIn, ScaleOut;
        public ushort ColorStart, ColorEnd; public int ColorIn, ColorPeak, ColorOut; public bool ColorRandomStart, ColorLoop, ColorInterpolate;
        public int AlphaStart, AlphaMid, AlphaEnd, AlphaRandom, AlphaIn, AlphaOut; public bool AlphaLoop;
        public byte[] TexAnimTextures = new byte[8]; public int TexAnimFrames, TexAnimStep; public bool TexAnimRandomInit, TexAnimLoop;

        public SplChild Child;
        public List<SplBehavior> Behaviors = new List<SplBehavior>();
    }

    public sealed class SplChild
    {
        public ushort Flags;
        public bool UsesBehaviors => (Flags & 1) != 0;
        public bool HasScaleAnim => (Flags & 2) != 0;
        public bool HasAlphaAnim => (Flags & 4) != 0;
        public int RotationType => (Flags >> 3) & 3;
        public bool FollowEmitter => (Flags & 0x20) != 0;
        public bool UseChildColor => (Flags & 0x40) != 0;
        public int DrawType => (Flags >> 7) & 3;
        public int PolygonRotAxis => (Flags >> 9) & 3;
        public int PolygonReferencePlane => (Flags >> 11) & 1;
        public short RandomInitVelMag, EndScale;
        public ushort LifeTime;
        public int VelocityRatio, ScaleRatio;
        public ushort Color;
        public int EmissionCount, EmissionDelay, EmissionInterval, Texture, TileS, TileT;
        public bool FlipS, FlipT, DpolFaceEmitter;
    }

    public enum SplBehaviorKind { Gravity, Random, Magnet, Spin, Collision, Convergence }

    /// <summary>A behaviour block. The battle callbacks point magnets and convergences at a Pokemon by writing into
    /// these, so they are shared by every emitter of the resource, as in the game.</summary>
    public sealed class SplBehavior
    {
        public SplBehaviorKind Kind;
        public short MagX, MagY, MagZ;   // gravity and random magnitude
        public int Interval;             // random apply interval
        public int TargetX, TargetY, TargetZ;
        public short Force;              // magnet and convergence
        public int SpinAngle, SpinAxis;
        public int PlaneY; public short Elasticity; public int CollisionType;
    }

    public sealed class SplParticle
    {
        public SplParticle Next, Prev;
        public int PosX, PosY, PosZ, VelX, VelY, VelZ;
        public ushort Rotation; public short AngularVelocity;
        public ushort LifeTime, Age;
        public ushort LoopTimeFactor, LifeTimeFactor;
        public byte Texture, LifeRateOffset;
        public int BaseAlpha, AnimAlpha, PolygonId;
        public int BaseScale; public short AnimScale;
        public ushort Color;
        public int EmitterPosX, EmitterPosY, EmitterPosZ;
    }

    /// <summary>The library's intrusive list: new nodes go to the front, and iteration runs from the front.</summary>
    public sealed class SplList<T> where T : class
    {
        public T First, Last;
        public int Count;
        private readonly Func<T, T> _next; private readonly Func<T, T> _prev;
        private readonly Action<T, T> _setNext; private readonly Action<T, T> _setPrev;

        public SplList(Func<T, T> next, Func<T, T> prev, Action<T, T> setNext, Action<T, T> setPrev)
        { _next = next; _prev = prev; _setNext = setNext; _setPrev = setPrev; }

        public void PushFront(T node)
        {
            if (First == null) { First = node; Last = node; _setNext(node, null); _setPrev(node, null); }
            else { _setNext(node, First); _setPrev(node, null); _setPrev(First, node); First = node; }
            Count++;
        }

        public T PopFront()
        {
            T node = First;
            if (node == null) return null;
            First = _next(node);
            if (First != null) _setPrev(First, null); else Last = null;
            Count--;
            return node;
        }

        // SPLList_Erase, including its assumption that a node with no next is the last one.
        public T Erase(T node)
        {
            T next = _next(node);
            if (next == null)
            {
                if (First == node) { First = null; Last = null; }
                else { _setNext(_prev(node), null); Last = _prev(Last); }
            }
            else if (First == node) { First = next; _setPrev(First, null); }
            else { _setPrev(next, _prev(node)); _setNext(_prev(node), _next(node)); }
            Count--;
            return node;
        }

        public static SplList<SplParticle> Particles() =>
            new SplList<SplParticle>(p => p.Next, p => p.Prev, (p, n) => p.Next = n, (p, n) => p.Prev = n);
    }

    public sealed class SplEmitter
    {
        public SplEmitter Next, Prev;
        public SplList<SplParticle> Particles = SplList<SplParticle>.Particles();
        public SplList<SplParticle> Children = SplList<SplParticle>.Particles();
        public SplResource Resource;
        public bool Terminate, EmissionPaused, Paused, RenderingDisabled, Started;
        public int PosX, PosY, PosZ, VelX, VelY, VelZ, InitVelX, InitVelY, InitVelZ;
        public ushort Age;
        public short EmissionCountFractional;
        public short AxisX, AxisY, AxisZ;
        public ushort InitAngle;
        public int EmissionCount, Radius, Length, InitVelPosAmp, InitVelAxisAmp, BaseScale;
        public ushort ParticleLifeTime;
        public ushort Color;
        public int CollisionPlaneHeight;
        public short TextureS, TextureT, ChildTextureS, ChildTextureT;
        public int EmissionInterval, BaseAlpha, UpdateCycle;
        public short Cross1X, Cross1Y, Cross1Z, Cross2X, Cross2Y, Cross2Z;
        /// <summary>Called before (false) and after (true) each update, like the generic battle callback.</summary>
        public Action<SplEmitter, bool> UpdateCallback;
        public object UserData;

        public void SetPos(int x, int y, int z) { PosX = x + Resource.BaseX; PosY = y + Resource.BaseY; PosZ = z + Resource.BaseZ; }
    }

    /// <summary>
    /// One particle system's manager: its resources, an emitter pool of 20 and a particle pool of 200 shared by parents and
    /// children, updated and drawn newest emitter first (pokeplatinum lib/spl, src/particle_system.c).
    /// </summary>
    public sealed class SplManager
    {
        public const int MaxEmitters = 20, MaxParticles = 200;
        public const int FixedPolygonId = 5, MinPolygonId = 6, MaxPolygonId = 63;

        public readonly List<SplResource> Resources = new List<SplResource>();
        public readonly SplList<SplEmitter> Active =
            new SplList<SplEmitter>(e => e.Next, e => e.Prev, (e, n) => e.Next = n, (e, n) => e.Prev = n);
        private int _freeEmitters = MaxEmitters, _freeParticles = MaxParticles;
        private int _polygonId = MinPolygonId, _cycle;
        private readonly SplRandom _rng;

        public SplManager(byte[] spa, SplRandom rng)
        {
            _rng = rng;
            Load(spa);
        }

        public static uint U32(byte[] d, int o) => (uint)(d[o] | d[o + 1] << 8 | d[o + 2] << 16 | d[o + 3] << 24);
        public static int I32(byte[] d, int o) => (int)U32(d, o);
        public static ushort U16(byte[] d, int o) => (ushort)(d[o] | d[o + 1] << 8);
        public static short S16(byte[] d, int o) => (short)U16(d, o);

        // SPLManager_LoadResources: the records follow the 32-byte file header, each a header and the blocks its flags name.
        private void Load(byte[] d)
        {
            if (d == null || d.Length < 32) return;
            int count = U16(d, 8);
            int o = 32;
            for (int i = 0; i < count && o + 88 <= d.Length; i++)
            {
                SplResource r = new SplResource { Flags = U32(d, o) };
                r.BaseX = I32(d, o + 4); r.BaseY = I32(d, o + 8); r.BaseZ = I32(d, o + 12);
                r.EmissionCount = I32(d, o + 16); r.Radius = I32(d, o + 20); r.Length = I32(d, o + 24);
                r.AxisX = S16(d, o + 28); r.AxisY = S16(d, o + 30); r.AxisZ = S16(d, o + 32);
                r.Color = U16(d, o + 34);
                r.InitVelPos = I32(d, o + 36); r.InitVelAxis = I32(d, o + 40); r.BaseScale = I32(d, o + 44);
                r.Aspect = S16(d, o + 48); r.StartDelay = U16(d, o + 50);
                r.MinRotation = S16(d, o + 52); r.MaxRotation = S16(d, o + 54); r.InitAngle = U16(d, o + 56);
                r.EmitterLifeTime = U16(d, o + 60); r.ParticleLifeTime = U16(d, o + 62);
                r.RndBaseScale = d[o + 64]; r.RndLifeTime = d[o + 65]; r.RndInitVel = d[o + 66];
                uint m0 = U32(d, o + 68), m1 = U32(d, o + 72), m2 = U32(d, o + 76);
                r.EmissionInterval = (int)(m0 & 0xFF); r.BaseAlpha = (int)((m0 >> 8) & 0xFF) & 31;
                r.AirResistance = (int)((m0 >> 16) & 0xFF); r.TextureIndex = (int)(m0 >> 24);
                r.LoopFrames = (int)(m1 & 0xFF); r.DbbScale = (int)((m1 >> 8) & 0xFFFF);
                r.TileS = (int)((m1 >> 24) & 3); r.TileT = (int)((m1 >> 26) & 3); r.ScaleAnimDir = (int)((m1 >> 28) & 7);
                r.DpolFaceEmitter = (m1 >> 31) != 0;
                r.FlipS = (m2 & 1) != 0; r.FlipT = (m2 & 2) != 0;
                r.PolygonX = S16(d, o + 80); r.PolygonY = S16(d, o + 82);
                o += 88;
                if (r.HasScaleAnim && o + 12 <= d.Length)
                {
                    r.ScaleStart = S16(d, o); r.ScaleMid = S16(d, o + 2); r.ScaleEnd = S16(d, o + 4);
                    r.ScaleIn = d[o + 6]; r.ScaleOut = d[o + 7]; r.ScaleLoop = (U16(d, o + 8) & 1) != 0;
                    o += 12;
                }
                if (r.HasColorAnim && o + 12 <= d.Length)
                {
                    r.ColorStart = U16(d, o); r.ColorEnd = U16(d, o + 2);
                    r.ColorIn = d[o + 4]; r.ColorPeak = d[o + 5]; r.ColorOut = d[o + 6];
                    ushort f = U16(d, o + 8);
                    r.ColorRandomStart = (f & 1) != 0; r.ColorLoop = (f & 2) != 0; r.ColorInterpolate = (f & 4) != 0;
                    o += 12;
                }
                if (r.HasAlphaAnim && o + 8 <= d.Length)
                {
                    ushort a = U16(d, o);
                    r.AlphaStart = a & 31; r.AlphaMid = (a >> 5) & 31; r.AlphaEnd = (a >> 10) & 31;
                    ushort f = U16(d, o + 2);
                    r.AlphaRandom = f & 0xFF; r.AlphaLoop = (f & 0x100) != 0;
                    r.AlphaIn = d[o + 4]; r.AlphaOut = d[o + 5];
                    o += 8;
                }
                if (r.HasTexAnim && o + 12 <= d.Length)
                {
                    Array.Copy(d, o, r.TexAnimTextures, 0, 8);
                    uint p = U32(d, o + 8);
                    r.TexAnimFrames = (int)(p & 0xFF); r.TexAnimStep = (int)((p >> 8) & 0xFF);
                    r.TexAnimRandomInit = (p & (1u << 16)) != 0; r.TexAnimLoop = (p & (1u << 17)) != 0;
                    o += 12;
                }
                if (r.HasChild && o + 20 <= d.Length)
                {
                    SplChild c = new SplChild
                    {
                        Flags = U16(d, o), RandomInitVelMag = S16(d, o + 2), EndScale = S16(d, o + 4), LifeTime = U16(d, o + 6),
                        VelocityRatio = d[o + 8], ScaleRatio = d[o + 9], Color = U16(d, o + 10),
                    };
                    uint m = U32(d, o + 12), mm = U32(d, o + 16);
                    c.EmissionCount = (int)(m & 0xFF); c.EmissionDelay = (int)((m >> 8) & 0xFF);
                    c.EmissionInterval = (int)((m >> 16) & 0xFF); c.Texture = (int)(m >> 24);
                    c.TileS = (int)(mm & 3); c.TileT = (int)((mm >> 2) & 3);
                    c.FlipS = (mm & 0x10) != 0; c.FlipT = (mm & 0x20) != 0; c.DpolFaceEmitter = (mm & 0x40) != 0;
                    r.Child = c;
                    o += 20;
                }
                uint fl = r.Flags;
                if ((fl & (1u << 24)) != 0) { r.Behaviors.Add(new SplBehavior { Kind = SplBehaviorKind.Gravity, MagX = S16(d, o), MagY = S16(d, o + 2), MagZ = S16(d, o + 4) }); o += 8; }
                if ((fl & (1u << 25)) != 0) { r.Behaviors.Add(new SplBehavior { Kind = SplBehaviorKind.Random, MagX = S16(d, o), MagY = S16(d, o + 2), MagZ = S16(d, o + 4), Interval = U16(d, o + 6) }); o += 8; }
                if ((fl & (1u << 26)) != 0) { r.Behaviors.Add(new SplBehavior { Kind = SplBehaviorKind.Magnet, TargetX = I32(d, o), TargetY = I32(d, o + 4), TargetZ = I32(d, o + 8), Force = S16(d, o + 12) }); o += 16; }
                if ((fl & (1u << 27)) != 0) { r.Behaviors.Add(new SplBehavior { Kind = SplBehaviorKind.Spin, SpinAngle = U16(d, o), SpinAxis = U16(d, o + 2) }); o += 4; }
                if ((fl & (1u << 28)) != 0) { r.Behaviors.Add(new SplBehavior { Kind = SplBehaviorKind.Collision, PlaneY = I32(d, o), Elasticity = S16(d, o + 4), CollisionType = U16(d, o + 6) & 3 }); o += 8; }
                if ((fl & (1u << 29)) != 0) { r.Behaviors.Add(new SplBehavior { Kind = SplBehaviorKind.Convergence, TargetX = I32(d, o), TargetY = I32(d, o + 4), TargetZ = I32(d, o + 8), Force = S16(d, o + 12) }); o += 16; }
                Resources.Add(r);
            }
        }

        public int ParticleCount
        {
            get { int n = 0; for (SplEmitter e = Active.First; e != null; e = e.Next) n += e.Particles.Count + e.Children.Count; return n; }
        }

        // SPLEmitter_Init.
        private static void Init(SplEmitter e, SplResource r, int x, int y, int z)
        {
            e.Resource = r;
            e.Terminate = e.EmissionPaused = e.Paused = e.RenderingDisabled = e.Started = false;
            e.SetPos(x, y, z);
            e.InitVelX = e.InitVelY = e.InitVelZ = 0; e.VelX = e.VelY = e.VelZ = 0;
            e.Age = 0; e.EmissionCountFractional = 0;
            e.AxisX = r.AxisX; e.AxisY = r.AxisY; e.AxisZ = r.AxisZ;
            e.InitAngle = r.InitAngle; e.EmissionCount = r.EmissionCount; e.Radius = r.Radius; e.Length = r.Length;
            e.InitVelPosAmp = r.InitVelPos; e.InitVelAxisAmp = r.InitVelAxis; e.BaseScale = r.BaseScale;
            e.ParticleLifeTime = r.ParticleLifeTime;
            e.Color = 0x7FFF;
            e.EmissionInterval = r.EmissionInterval; e.BaseAlpha = r.BaseAlpha; e.UpdateCycle = 0;
            e.CollisionPlaneHeight = int.MinValue;
            e.TextureS = (short)(One << r.TileS); e.TextureT = (short)(One << r.TileT);
            if (r.FlipS) e.TextureS = (short)-e.TextureS;
            if (r.FlipT) e.TextureT = (short)-e.TextureT;
            if (r.HasChild && r.Child != null)
            {
                e.ChildTextureS = (short)(One << r.Child.TileS); e.ChildTextureT = (short)(One << r.Child.TileT);
                if (r.Child.FlipS) e.ChildTextureS = (short)-e.ChildTextureS;
                if (r.Child.FlipT) e.ChildTextureT = (short)-e.ChildTextureT;
            }
            e.Particles = SplList<SplParticle>.Particles(); e.Children = SplList<SplParticle>.Particles();
            e.UpdateCallback = null; e.UserData = null;
        }

        private const int One = SplFx.One;

        /// <summary>SPLManager_CreateEmitterWithCallback: null when the pool of 20 is used up. The callback sets the emitter
        /// up before it joins the list.</summary>
        public SplEmitter CreateEmitter(int resourceId, Action<SplEmitter> init)
        {
            if (_freeEmitters == 0 || resourceId < 0 || resourceId >= Resources.Count) return null;
            _freeEmitters--;
            SplEmitter e = new SplEmitter();
            Init(e, Resources[resourceId], 0, 0, 0);
            init?.Invoke(e);
            Active.PushFront(e);
            return e;
        }

        public void DeleteEmitter(SplEmitter e)
        {
            _freeParticles += e.Particles.Count + e.Children.Count;
            e.Particles = SplList<SplParticle>.Particles(); e.Children = SplList<SplParticle>.Particles();
            Active.Erase(e);
            _freeEmitters++;
        }

        public void DeleteAll()
        {
            SplEmitter e = Active.First;
            while (e != null) { SplEmitter next = e.Next; DeleteEmitter(e); e = next; }
        }

        public void StopAllEmission() { for (SplEmitter e = Active.First; e != null; e = e.Next) e.EmissionPaused = true; }

        /// <summary>SPLManager_Update.</summary>
        public void Update()
        {
            SplEmitter e = Active.First;
            while (e != null)
            {
                SplResource r = e.Resource;
                SplEmitter next = e.Next;
                if (!e.Started && e.Age >= r.StartDelay) { e.Started = true; e.Age = 0; }
                if (!e.Paused && (e.UpdateCycle == 0 || _cycle == e.UpdateCycle - 1)) UpdateEmitter(e);
                bool end = ((r.SelfMaintaining && r.EmitterLifeTime != 0 && e.Started && e.Age > r.EmitterLifeTime) || e.Terminate)
                           && e.Particles.Count == 0 && e.Children.Count == 0;
                if (end) { Active.Erase(e); _freeEmitters++; }
                e = next;
            }
            _cycle++;
            if (_cycle > 1) _cycle = 0;
        }

        private SplParticle Take()
        {
            if (_freeParticles == 0) return null;
            _freeParticles--;
            return new SplParticle();
        }

        // SPLEmitter_ComputeOrthogonalAxes.
        private static void OrthogonalAxes(SplEmitter e)
        {
            short vx = 0, vy = One, vz = 0;
            short ax, ay, az;
            switch (e.Resource.CircleAxis)
            {
                case 2: ax = One; ay = 0; az = 0; break;
                case 1: ax = 0; ay = One; az = 0; break;
                case 0: ax = 0; ay = 0; az = One; break;
                default: ax = e.AxisX; ay = e.AxisY; az = e.AxisZ; SplFx.Normalize16(ref ax, ref ay, ref az); break;
            }
            int dot = SplFx.Dot16(vx, vy, vz, ax, ay, az);
            if (dot == One || dot == -One) { vx = One; vy = 0; vz = 0; }
            e.Cross1X = (short)(SplFx.Mul(ay, vz) - SplFx.Mul(az, vy));
            e.Cross1Y = (short)(SplFx.Mul(az, vx) - SplFx.Mul(ax, vz));
            e.Cross1Z = (short)(SplFx.Mul(ax, vy) - SplFx.Mul(ay, vx));
            e.Cross2X = (short)(SplFx.Mul(ay, e.Cross1Z) - SplFx.Mul(az, e.Cross1Y));
            e.Cross2Y = (short)(SplFx.Mul(az, e.Cross1X) - SplFx.Mul(ax, e.Cross1Z));
            e.Cross2Z = (short)(SplFx.Mul(ax, e.Cross1Y) - SplFx.Mul(ay, e.Cross1X));
            SplFx.Normalize16(ref e.Cross1X, ref e.Cross1Y, ref e.Cross1Z);
            SplFx.Normalize16(ref e.Cross2X, ref e.Cross2Y, ref e.Cross2Z);
        }

        private static (short X, short Y, short Z) Up(SplEmitter e)
        {
            (short X, short Y, short Z) c = SplFx.Cross16(e.Cross1X, e.Cross1Y, e.Cross1Z, e.Cross2X, e.Cross2Y, e.Cross2Z);
            return c;
        }

        // SPLUtil_TiltCoordinates.
        private static void Tilt(SplParticle p, int x, int y, int z, SplEmitter e)
        {
            (short X, short Y, short Z) v = Up(e);
            short vx = v.X, vy = v.Y, vz = v.Z;
            SplFx.Normalize16(ref vx, ref vy, ref vz);
            p.PosX = SplFx.Mul(x, e.Cross1X) + SplFx.Mul(y, e.Cross2X) + SplFx.Mul(z, vx);
            p.PosY = SplFx.Mul(x, e.Cross1Y) + SplFx.Mul(y, e.Cross2Y) + SplFx.Mul(z, vy);
            p.PosZ = SplFx.Mul(x, e.Cross1Z) + SplFx.Mul(y, e.Cross2Z) + SplFx.Mul(z, vz);
        }

        private void RandomVec(out int x, out int y, out int z)
        {
            x = _rng.Fx32(24); y = _rng.Fx32(24); z = _rng.Fx32(24);
            SplFx.Normalize(ref x, ref y, ref z);
        }

        private void RandomVecXY(out int x, out int y)
        {
            x = _rng.Fx32(24); y = _rng.Fx32(24); int z = 0;
            SplFx.Normalize(ref x, ref y, ref z);
        }

        // SPLEmitter_EmitParticles.
        private void Emit(SplEmitter e)
        {
            SplResource r = e.Resource;
            int dec = e.EmissionCount + e.EmissionCountFractional;
            int total = dec >> 12;
            int emission = 0;
            e.EmissionCountFractional = (short)(dec & 0xFFF);
            int type = r.EmissionType;
            if (type == 2 || type == 3 || type == 5 || type == 6 || type == 7 || type == 8 || type == 9) OrthogonalAxes(e);
            for (int i = 0; i < total; i++)
            {
                SplParticle p = Take();
                if (p == null) return;
                e.Particles.PushFront(p);
                switch (type)
                {
                    case 0: p.PosX = p.PosY = p.PosZ = 0; break;
                    case 1:
                        RandomVec(out p.PosX, out p.PosY, out p.PosZ);
                        p.PosX = SplFx.Mul(p.PosX, e.Radius); p.PosY = SplFx.Mul(p.PosY, e.Radius); p.PosZ = SplFx.Mul(p.PosZ, e.Radius);
                        break;
                    case 2:
                    {
                        RandomVecXY(out int x, out int y);
                        Tilt(p, SplFx.Mul(x, e.Radius), SplFx.Mul(y, e.Radius), 0, e);
                        break;
                    }
                    case 3:
                    {
                        int idx = (emission * (16 * One)) / total;
                        emission++;
                        Tilt(p, SplFx.Mul(SplFx.Sin(idx), e.Radius), SplFx.Mul(SplFx.Cos(idx), e.Radius), 0, e);
                        break;
                    }
                    case 4:
                        RandomVec(out p.PosX, out p.PosY, out p.PosZ);
                        p.PosX = SplFx.Mul(SplFx.Mul(p.PosX, e.Radius), _rng.RangeFx(One));
                        p.PosY = SplFx.Mul(SplFx.Mul(p.PosY, e.Radius), _rng.RangeFx(One));
                        p.PosZ = SplFx.Mul(SplFx.Mul(p.PosZ, e.Radius), _rng.RangeFx(One));
                        break;
                    case 5:
                    {
                        RandomVecXY(out int x, out int y);
                        int px = SplFx.Mul(SplFx.Mul(x, e.Radius), _rng.RangeFx(One));
                        int py = SplFx.Mul(SplFx.Mul(y, e.Radius), _rng.RangeFx(One));
                        Tilt(p, px, py, 0, e);
                        break;
                    }
                    case 8:
                    case 9:
                    {
                        RandomVec(out p.PosX, out p.PosY, out p.PosZ);
                        (short X, short Y, short Z) up = Up(e);
                        int dot = SplFx.Dot(up.X, up.Y, up.Z, p.PosX, p.PosY, p.PosZ);
                        if (type == 8 ? dot <= 0 : dot < 0) { p.PosX = -p.PosX; p.PosY = -p.PosY; p.PosZ = -p.PosZ; }
                        if (type == 8)
                        {
                            p.PosX = SplFx.Mul(p.PosX, e.Radius); p.PosY = SplFx.Mul(p.PosY, e.Radius); p.PosZ = SplFx.Mul(p.PosZ, e.Radius);
                        }
                        else
                        {
                            p.PosX = SplFx.Mul(SplFx.Mul(p.PosX, e.Radius), (_rng.RangeFx(One) >> 1) + One / 2);
                            p.PosY = SplFx.Mul(SplFx.Mul(p.PosY, e.Radius), (_rng.RangeFx(One) >> 1) + One / 2);
                            p.PosZ = SplFx.Mul(SplFx.Mul(p.PosZ, e.Radius), (_rng.RangeFx(One) >> 1) + One / 2);
                        }
                        break;
                    }
                    case 6:
                    {
                        RandomVecXY(out p.VelX, out p.VelY);
                        Tilt(p, SplFx.Mul(p.VelX, e.Radius), SplFx.Mul(p.VelY, e.Radius), _rng.RangeFx(e.Length), e);
                        break;
                    }
                    case 7:
                    {
                        RandomVecXY(out p.VelX, out p.VelY);
                        int px = SplFx.Mul(SplFx.Mul(p.VelX, e.Radius), _rng.RangeFx(One));
                        int py = SplFx.Mul(SplFx.Mul(p.VelY, e.Radius), _rng.RangeFx(One));
                        Tilt(p, px, py, _rng.RangeFx(e.Length), e);
                        break;
                    }
                }

                int magPos = _rng.DoubleScaledRangeFx(e.InitVelPosAmp, r.RndInitVel);
                int magAxis = _rng.DoubleScaledRangeFx(e.InitVelAxisAmp, r.RndInitVel);
                int nx, ny, nz;
                if (type == 6)
                {
                    nx = SplFx.Mul(p.VelX, e.Cross1X) + SplFx.Mul(p.VelY, e.Cross2X);
                    ny = SplFx.Mul(p.VelX, e.Cross1Y) + SplFx.Mul(p.VelY, e.Cross2Y);
                    nz = SplFx.Mul(p.VelX, e.Cross1Z) + SplFx.Mul(p.VelY, e.Cross2Z);
                    SplFx.Normalize(ref nx, ref ny, ref nz);
                }
                else if (p.PosX == 0 && p.PosY == 0 && p.PosZ == 0) RandomVec(out nx, out ny, out nz);
                else { nx = p.PosX; ny = p.PosY; nz = p.PosZ; SplFx.Normalize(ref nx, ref ny, ref nz); }
                p.VelX = SplFx.Mul(nx, magPos) + SplFx.Mul(e.AxisX, magAxis) + e.InitVelX;
                p.VelY = SplFx.Mul(ny, magPos) + SplFx.Mul(e.AxisY, magAxis) + e.InitVelY;
                p.VelZ = SplFx.Mul(nz, magPos) + SplFx.Mul(e.AxisZ, magAxis) + e.InitVelZ;
                p.EmitterPosX = e.PosX; p.EmitterPosY = e.PosY; p.EmitterPosZ = e.PosZ;
                p.BaseScale = _rng.DoubleScaledRangeFx(e.BaseScale, r.RndBaseScale);
                p.AnimScale = One;
                if (r.HasColorAnim && r.ColorRandomStart)
                {
                    int index = (int)_rng.U32(12);
                    ushort[] clr = { r.ColorStart, r.Color, r.ColorEnd };
                    p.Color = clr[index % 3];
                }
                else p.Color = r.Color;
                p.BaseAlpha = e.BaseAlpha & 31;
                p.AnimAlpha = 31;
                p.Rotation = r.RandomInitAngle ? (ushort)_rng.Next() : e.InitAngle;
                p.AngularVelocity = r.HasRotation ? (short)((uint)_rng.BetweenFx(r.MinRotation, r.MaxRotation) >> 12) : (short)0;
                p.LifeTime = (ushort)(_rng.ScaledRange(e.ParticleLifeTime, r.RndLifeTime) + 1);
                p.Age = 0;
                if (r.HasTexAnim && r.TexAnimRandomInit)
                    p.Texture = r.TexAnimTextures[Math.Min(7, (int)(_rng.U32(12) % (uint)Math.Max(1, r.TexAnimFrames)))];
                else if (r.HasTexAnim) p.Texture = r.TexAnimTextures[0];
                else p.Texture = (byte)r.TextureIndex;
                p.LoopTimeFactor = (ushort)(0xFFFF / Math.Max(1, r.LoopFrames));
                p.LifeTimeFactor = (ushort)(0xFFFF / p.LifeTime);
                p.LifeRateOffset = 0;
                if (r.RandomizeLoopedAnim) p.LifeRateOffset = (byte)_rng.U32(8);
            }
        }

        // SPLEmitter_EmitChildren.
        private void EmitChildren(SplParticle p, SplEmitter e)
        {
            SplChild c = e.Resource.Child;
            int velRatio = SplFx.Mul(c.VelocityRatio * One, (int)(One / 256.0f + 0.5f));
            for (int i = 0; i < c.EmissionCount; i++)
            {
                SplParticle ch = Take();
                if (ch == null) return;
                e.Children.PushFront(ch);
                ch.PosX = p.PosX; ch.PosY = p.PosY; ch.PosZ = p.PosZ;
                ch.VelX = SplFx.Mul(p.VelX, velRatio) + _rng.RangeFx(c.RandomInitVelMag);
                ch.VelY = SplFx.Mul(p.VelY, velRatio) + _rng.RangeFx(c.RandomInitVelMag);
                ch.VelZ = SplFx.Mul(p.VelZ, velRatio) + _rng.RangeFx(c.RandomInitVelMag);
                ch.EmitterPosX = p.EmitterPosX; ch.EmitterPosY = p.EmitterPosY; ch.EmitterPosZ = p.EmitterPosZ;
                int parentScale = (p.BaseScale * p.AnimScale) >> 12;
                ch.BaseScale = (parentScale * (c.ScaleRatio + 1)) >> 6;
                ch.AnimScale = One;
                ch.Color = c.UseChildColor ? c.Color : p.Color;
                ch.BaseAlpha = ((p.BaseAlpha * (p.AnimAlpha + 1)) >> 5) & 31;
                ch.AnimAlpha = 31;
                switch (c.RotationType)
                {
                    case 1: ch.Rotation = p.Rotation; ch.AngularVelocity = 0; break;
                    case 2: ch.Rotation = p.Rotation; ch.AngularVelocity = p.AngularVelocity; break;
                    default: ch.Rotation = 0; ch.AngularVelocity = 0; break;
                }
                ch.LifeTime = c.LifeTime;
                ch.Age = 0;
                ch.Texture = (byte)c.Texture;
                ch.LoopTimeFactor = (ushort)(0xFFFF / Math.Max(1, p.LifeTime / 2));
                ch.LifeTimeFactor = (ushort)(0xFFFF / Math.Max(1, (int)p.LifeTime));
                ch.LifeRateOffset = 0;
            }
        }

        // SPLAnim_*.
        private static void AnimScale(SplParticle p, SplResource r, int rate)
        {
            if (rate < r.ScaleIn) p.AnimScale = (short)(r.ScaleStart + (rate * (r.ScaleMid - r.ScaleStart)) / r.ScaleIn);
            else if (rate < r.ScaleOut) p.AnimScale = r.ScaleMid;
            else p.AnimScale = (short)(r.ScaleEnd + ((rate - 255) * (r.ScaleEnd - r.ScaleMid)) / (255 - r.ScaleOut));
        }

        private static int R5(int c) => c & 31;
        private static int G5(int c) => (c >> 5) & 31;
        private static int B5(int c) => (c >> 10) & 31;
        private static ushort Rgb(int r, int g, int b) => (ushort)((r & 31) | (g & 31) << 5 | (b & 31) << 10);

        private static void AnimColor(SplParticle p, SplResource r, int rate)
        {
            if (rate < r.ColorIn) p.Color = r.ColorStart;
            else if (rate < r.ColorPeak)
            {
                if (r.ColorInterpolate)
                {
                    int a = rate - r.ColorIn, b = r.ColorPeak - r.ColorIn;
                    p.Color = Rgb(R5(r.ColorStart) + (a * (R5(r.Color) - R5(r.ColorStart))) / b,
                                  G5(r.ColorStart) + (a * (G5(r.Color) - G5(r.ColorStart))) / b,
                                  B5(r.ColorStart) + (a * (B5(r.Color) - B5(r.ColorStart))) / b);
                }
                else p.Color = Rgb(R5(r.Color), G5(r.Color), B5(r.Color));
            }
            else if (rate < r.ColorOut)
            {
                if (r.ColorInterpolate)
                {
                    int a = rate - r.ColorPeak, b = r.ColorOut - r.ColorPeak;
                    p.Color = Rgb(R5(r.Color) + (a * (R5(r.ColorEnd) - R5(r.Color))) / b,
                                  G5(r.Color) + (a * (G5(r.ColorEnd) - G5(r.Color))) / b,
                                  B5(r.Color) + (a * (B5(r.ColorEnd) - B5(r.Color))) / b);
                }
                else p.Color = Rgb(R5(r.ColorEnd), G5(r.ColorEnd), B5(r.ColorEnd));
            }
            else p.Color = r.ColorEnd;
        }

        private void AnimAlpha(SplParticle p, SplResource r, int rate)
        {
            uint value;
            if (rate < r.AlphaIn) value = (uint)((rate * (r.AlphaMid - r.AlphaStart)) / r.AlphaIn + r.AlphaStart);
            else if (rate < r.AlphaOut) value = (uint)r.AlphaMid;
            else value = (uint)(((rate - 255) * (r.AlphaEnd - r.AlphaMid)) / (255 - r.AlphaOut) + r.AlphaEnd);
            p.AnimAlpha = (int)(((value * (uint)(255 - ((r.AlphaRandom * (int)_rng.U32(8)) >> 8))) >> 8) & 31);
        }

        private static void AnimTexture(SplParticle p, SplResource r, int rate)
        {
            for (int i = 0; i < r.TexAnimFrames; i++)
                if (rate < r.TexAnimStep * (i + 1)) { p.Texture = r.TexAnimTextures[Math.Min(7, i)]; return; }
        }

        private void Behave(SplBehavior b, SplParticle p, ref int ax, ref int ay, ref int az, SplEmitter e)
        {
            switch (b.Kind)
            {
                case SplBehaviorKind.Gravity: ax += b.MagX; ay += b.MagY; az += b.MagZ; break;
                case SplBehaviorKind.Random:
                    if (b.Interval != 0 && p.Age % b.Interval == 0)
                    {
                        ax += _rng.RangeFx(b.MagX); ay += _rng.RangeFx(b.MagY); az += _rng.RangeFx(b.MagZ);
                    }
                    break;
                case SplBehaviorKind.Magnet:
                    ax += (b.Force * ((b.TargetX - p.PosX) - p.VelX)) >> 12;
                    ay += (b.Force * ((b.TargetY - p.PosY) - p.VelY)) >> 12;
                    az += (b.Force * ((b.TargetZ - p.PosZ) - p.VelZ)) >> 12;
                    break;
                case SplBehaviorKind.Spin:
                {
                    int s = SplFx.Sin(b.SpinAngle), c = SplFx.Cos(b.SpinAngle);
                    int x = p.PosX, y = p.PosY, z = p.PosZ;
                    // MTX_RotX33 / RotY33 / RotZ33, then MTX_MultVec33: each row summed in 64 bits and shifted once.
                    switch (b.SpinAxis)
                    {
                        case 0:
                            p.PosX = (int)(((long)x << 12) >> 12);
                            p.PosY = (int)(((long)y * c - (long)z * s) >> 12);
                            p.PosZ = (int)(((long)y * s + (long)z * c) >> 12);
                            break;
                        case 1:
                            p.PosX = (int)(((long)x * c + (long)z * s) >> 12);
                            p.PosZ = (int)((-(long)x * s + (long)z * c) >> 12);
                            break;
                        default:
                            p.PosX = (int)(((long)x * c - (long)y * s) >> 12);
                            p.PosY = (int)(((long)x * s + (long)y * c) >> 12);
                            break;
                    }
                    break;
                }
                case SplBehaviorKind.Collision:
                {
                    int y = e.CollisionPlaneHeight != int.MinValue ? e.CollisionPlaneHeight : b.PlaneY;
                    int ey = p.EmitterPosY;
                    bool crossed = (ey < y && ey + p.PosY > y) || (ey >= y && ey + p.PosY < y);
                    if (!crossed) break;
                    p.PosY = y - ey;
                    if (b.CollisionType == 0) p.Age = p.LifeTime;
                    else if (b.CollisionType == 1) p.VelY = -SplFx.Mul(p.VelY, b.Elasticity);
                    break;
                }
                case SplBehaviorKind.Convergence:
                    p.PosX += SplFx.Mul(b.Force, b.TargetX - p.PosX);
                    p.PosY += SplFx.Mul(b.Force, b.TargetY - p.PosY);
                    p.PosZ += SplFx.Mul(b.Force, b.TargetZ - p.PosZ);
                    break;
            }
        }

        private void NextPolygonId(SplParticle p, bool fixedId)
        {
            if (fixedId) { p.PolygonId = FixedPolygonId; return; }
            p.PolygonId = _polygonId;
            _polygonId++;
            if (_polygonId > MaxPolygonId) _polygonId = MinPolygonId;
        }

        /// <summary>SPLEmitter_Update.</summary>
        private void UpdateEmitter(SplEmitter e)
        {
            SplResource r = e.Resource;
            SplChild c = r.Child;
            int air = r.AirResistance + 384;
            e.UpdateCallback?.Invoke(e, false);
            if (r.EmitterLifeTime == 0 || e.Age < r.EmitterLifeTime)
                if (e.EmissionInterval != 0 && e.Age % e.EmissionInterval == 0)
                    if (!e.Terminate && !e.EmissionPaused && e.Started) Emit(e);

            bool scale = r.HasScaleAnim, color = r.HasColorAnim && !r.ColorRandomStart, alpha = r.HasAlphaAnim;
            bool tex = r.HasTexAnim && !r.TexAnimRandomInit;
            for (SplParticle p = e.Particles.First, next; p != null; p = next)
            {
                next = p.Next;
                int rateNoLoop = ((p.LifeTimeFactor * p.Age) >> 8) & 0xFF;
                int rateLoop = (p.LifeRateOffset + ((p.LoopTimeFactor * p.Age) >> 8)) & 0xFF;
                if (scale) AnimScale(p, r, r.ScaleLoop ? rateLoop : rateNoLoop);
                if (color) AnimColor(p, r, r.ColorLoop ? rateLoop : rateNoLoop);
                if (alpha) AnimAlpha(p, r, r.AlphaLoop ? rateLoop : rateNoLoop);
                if (tex) AnimTexture(p, r, r.TexAnimLoop ? rateLoop : rateNoLoop);
                int ax = 0, ay = 0, az = 0;
                if (r.FollowEmitter) { p.EmitterPosX = e.PosX; p.EmitterPosY = e.PosY; p.EmitterPosZ = e.PosZ; }
                foreach (SplBehavior b in r.Behaviors) Behave(b, p, ref ax, ref ay, ref az, e);
                p.Rotation = (ushort)(p.Rotation + p.AngularVelocity);
                p.VelX = (p.VelX * air) >> 9; p.VelY = (p.VelY * air) >> 9; p.VelZ = (p.VelZ * air) >> 9;
                p.VelX += ax; p.VelY += ay; p.VelZ += az;
                p.PosX += p.VelX + e.VelX; p.PosY += p.VelY + e.VelY; p.PosZ += p.VelZ + e.VelZ;
                if (r.HasChild && c != null)
                {
                    int delay = SplFx.Mul(p.LifeTime << 12, c.EmissionDelay << 12);
                    int diff = p.Age * One - (delay >> 8);
                    if (diff >= 0 && c.EmissionInterval != 0 && (diff >> 12) % c.EmissionInterval == 0) EmitChildren(p, e);
                }
                NextPolygonId(p, r.HasFixedPolygonId);
                p.Age++;
                if (p.Age > p.LifeTime) { e.Particles.Erase(p); _freeParticles++; }
            }

            if (r.HasChild && c != null)
            {
                bool childBehaviors = c.UsesBehaviors;
                for (SplParticle p = e.Children.First, next; p != null; p = next)
                {
                    next = p.Next;
                    int rate = ((p.Age << 8) / Math.Max(1, (int)p.LifeTime)) & 0xFF;
                    if (c.HasScaleAnim) p.AnimScale = (short)(c.EndScale + ((c.EndScale - One) * (rate - 255)) / 255);
                    if (c.HasAlphaAnim) p.AnimAlpha = (((255 - rate) * 31) / 255) & 31;
                    int ax = 0, ay = 0, az = 0;
                    if (c.FollowEmitter) { p.EmitterPosX = e.PosX; p.EmitterPosY = e.PosY; p.EmitterPosZ = e.PosZ; }
                    if (childBehaviors) foreach (SplBehavior b in r.Behaviors) Behave(b, p, ref ax, ref ay, ref az, e);
                    p.Rotation = (ushort)(p.Rotation + p.AngularVelocity);
                    p.VelX = (p.VelX * air) >> 9; p.VelY = (p.VelY * air) >> 9; p.VelZ = (p.VelZ * air) >> 9;
                    p.VelX += ax; p.VelY += ay; p.VelZ += az;
                    p.PosX += p.VelX + e.VelX; p.PosY += p.VelY + e.VelY; p.PosZ += p.VelZ + e.VelZ;
                    NextPolygonId(p, r.ChildHasFixedPolygonId);
                    p.Age++;
                    if (p.Age > p.LifeTime) { e.Children.Erase(p); _freeParticles++; }
                }
            }
            e.Age++;
            e.UpdateCallback?.Invoke(e, true);
        }
    }
}
