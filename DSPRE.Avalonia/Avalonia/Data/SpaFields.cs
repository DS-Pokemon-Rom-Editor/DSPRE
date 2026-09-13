using System;
using System.Collections.Generic;

namespace DSPRE.Avalonia.Data
{
    /// <summary>The parts of an emitter record, in file order.</summary>
    public enum SpaBlock
    {
        Header, ScaleAnim, ColorAnim, AlphaAnim, TexAnim, Child,
        Gravity, Random, Magnet, Spin, Collision, Convergence,
    }

    public enum SpaFieldKind { Number, Flag, Color }

    /// <summary>One bit field inside an emitter record block, located by block, word and bit range.</summary>
    public sealed class SpaField
    {
        public string Name { get; }
        public SpaBlock Block { get; }
        /// <summary>Offset of the little-endian word holding the field, from the start of its block.</summary>
        public int Offset { get; }
        /// <summary>Size of that word in bytes: 1, 2 or 4.</summary>
        public int Size { get; }
        public int Shift { get; }
        public int Bits { get; }
        public bool Signed { get; }
        /// <summary>The value an editor shows is the raw value divided by this.</summary>
        public double Divisor { get; }
        public SpaFieldKind Kind { get; }
        /// <summary>A presence flag for an optional block; read-only, since changing it moves every byte after it.</summary>
        public bool DecidesLayout { get; }
        public long MinRaw { get; }
        public long MaxRaw { get; }

        internal SpaField(string name, SpaBlock block, int offset, int size, int shift, int bits, bool signed,
                          double divisor, SpaFieldKind kind, long? max, bool decidesLayout)
        {
            Name = name; Block = block; Offset = offset; Size = size; Shift = shift; Bits = bits;
            Signed = signed; Divisor = divisor; Kind = kind; DecidesLayout = decidesLayout;
            MinRaw = signed ? -(1L << (bits - 1)) : 0;
            MaxRaw = max ?? (signed ? (1L << (bits - 1)) - 1 : (1L << bits) - 1);
        }

        internal uint Mask => (uint)(((1UL << Bits) - 1) << Shift);

        internal long Extract(uint word)
        {
            long v = (word >> Shift) & (long)((1UL << Bits) - 1);
            if (Signed && (v & (1L << (Bits - 1))) != 0) v -= 1L << Bits;
            return v;
        }

        internal uint Insert(uint word, long raw)
            => (word & ~Mask) | (((uint)raw << Shift) & Mask);

        public override string ToString() => Block + "." + Name;
    }

    /// <summary>Every editable field of an SPA emitter record. Reserved and padding bits are left out so edits cannot reach them.</summary>
    public static class SpaFields
    {
        private static readonly List<SpaField> _all = new List<SpaField>();
        public static IReadOnlyList<SpaField> All => _all;

        private const double Px = 172.0;               // particle units per screen pixel, as SpaArchive reads them
        private const double Fx = 4096.0;              // fixed point 1.0
        private const double Deg = 65536.0 / 360.0;    // angle units per degree

        private static SpaField F(SpaBlock block, string name, int offset, int size, int shift = 0, int bits = -1,
                                  bool signed = false, double divisor = 1, SpaFieldKind kind = SpaFieldKind.Number,
                                  long? max = null, bool layout = false)
        {
            if (bits < 0) bits = size * 8;
            if (bits == 1 && kind == SpaFieldKind.Number) kind = SpaFieldKind.Flag;
            var f = new SpaField(name, block, offset, size, shift, bits, signed, divisor, kind, max, layout);
            _all.Add(f);
            return f;
        }

        private static SpaField H(string n, int o, int size, int shift = 0, int bits = -1, bool signed = false,
                                  double divisor = 1, SpaFieldKind kind = SpaFieldKind.Number, long? max = null, bool layout = false)
            => F(SpaBlock.Header, n, o, size, shift, bits, signed, divisor, kind, max, layout);

        private static SpaField Rgb(SpaBlock b, string n, int o) => F(b, n, o, 2, 0, 15, kind: SpaFieldKind.Color);

        // Resource flags (word @0).
        public static readonly SpaField EmissionType = H(nameof(EmissionType), 0, 4, 0, 4, max: 9);
        public static readonly SpaField DrawType = H(nameof(DrawType), 0, 4, 4, 2);
        public static readonly SpaField CircleAxis = H(nameof(CircleAxis), 0, 4, 6, 2);
        public static readonly SpaField HasScaleAnim = H(nameof(HasScaleAnim), 0, 4, 8, 1, layout: true);
        public static readonly SpaField HasColorAnim = H(nameof(HasColorAnim), 0, 4, 9, 1, layout: true);
        public static readonly SpaField HasAlphaAnim = H(nameof(HasAlphaAnim), 0, 4, 10, 1, layout: true);
        public static readonly SpaField HasTexAnim = H(nameof(HasTexAnim), 0, 4, 11, 1, layout: true);
        public static readonly SpaField HasRotation = H(nameof(HasRotation), 0, 4, 12, 1);
        public static readonly SpaField RandomInitAngle = H(nameof(RandomInitAngle), 0, 4, 13, 1);
        public static readonly SpaField SelfMaintaining = H(nameof(SelfMaintaining), 0, 4, 14, 1);
        public static readonly SpaField FollowEmitter = H(nameof(FollowEmitter), 0, 4, 15, 1);
        public static readonly SpaField HasChildResource = H(nameof(HasChildResource), 0, 4, 16, 1, layout: true);
        public static readonly SpaField PolygonRotAxis = H(nameof(PolygonRotAxis), 0, 4, 17, 2);
        public static readonly SpaField PolygonReferencePlane = H(nameof(PolygonReferencePlane), 0, 4, 19, 1);
        public static readonly SpaField RandomizeLoopedAnim = H(nameof(RandomizeLoopedAnim), 0, 4, 20, 1);
        public static readonly SpaField DrawChildrenFirst = H(nameof(DrawChildrenFirst), 0, 4, 21, 1);
        public static readonly SpaField HideParent = H(nameof(HideParent), 0, 4, 22, 1);
        public static readonly SpaField UseViewSpace = H(nameof(UseViewSpace), 0, 4, 23, 1);
        public static readonly SpaField HasGravity = H(nameof(HasGravity), 0, 4, 24, 1, layout: true);
        public static readonly SpaField HasRandom = H(nameof(HasRandom), 0, 4, 25, 1, layout: true);
        public static readonly SpaField HasMagnet = H(nameof(HasMagnet), 0, 4, 26, 1, layout: true);
        public static readonly SpaField HasSpin = H(nameof(HasSpin), 0, 4, 27, 1, layout: true);
        public static readonly SpaField HasCollisionPlane = H(nameof(HasCollisionPlane), 0, 4, 28, 1, layout: true);
        public static readonly SpaField HasConvergence = H(nameof(HasConvergence), 0, 4, 29, 1, layout: true);
        public static readonly SpaField HasFixedPolygonId = H(nameof(HasFixedPolygonId), 0, 4, 30, 1);
        public static readonly SpaField ChildHasFixedPolygonId = H(nameof(ChildHasFixedPolygonId), 0, 4, 31, 1);

        public static readonly SpaField EmitterPosX = H(nameof(EmitterPosX), 4, 4, signed: true, divisor: Px);
        public static readonly SpaField EmitterPosY = H(nameof(EmitterPosY), 8, 4, signed: true, divisor: Px);
        public static readonly SpaField EmitterPosZ = H(nameof(EmitterPosZ), 12, 4, signed: true, divisor: Px);
        public static readonly SpaField EmissionCount = H(nameof(EmissionCount), 16, 4, signed: true, divisor: Fx);
        public static readonly SpaField Radius = H(nameof(Radius), 20, 4, signed: true, divisor: Px);
        public static readonly SpaField Length = H(nameof(Length), 24, 4, signed: true, divisor: Px);
        public static readonly SpaField AxisX = H(nameof(AxisX), 28, 2, signed: true, divisor: Fx);
        public static readonly SpaField AxisY = H(nameof(AxisY), 30, 2, signed: true, divisor: Fx);
        public static readonly SpaField AxisZ = H(nameof(AxisZ), 32, 2, signed: true, divisor: Fx);
        public static readonly SpaField Color = H(nameof(Color), 34, 2, 0, 15, kind: SpaFieldKind.Color);
        public static readonly SpaField InitVelPos = H(nameof(InitVelPos), 36, 4, signed: true, divisor: Px);
        public static readonly SpaField InitVelAxis = H(nameof(InitVelAxis), 40, 4, signed: true, divisor: Px);
        public static readonly SpaField BaseScale = H(nameof(BaseScale), 44, 4, signed: true, divisor: Fx);
        public static readonly SpaField AspectRatio = H(nameof(AspectRatio), 48, 2, signed: true, divisor: Fx);
        public static readonly SpaField StartDelay = H(nameof(StartDelay), 50, 2);
        public static readonly SpaField MinRotation = H(nameof(MinRotation), 52, 2, signed: true, divisor: Deg);
        public static readonly SpaField MaxRotation = H(nameof(MaxRotation), 54, 2, signed: true, divisor: Deg);
        public static readonly SpaField InitAngle = H(nameof(InitAngle), 56, 2, divisor: Deg);
        public static readonly SpaField EmitterLifeTime = H(nameof(EmitterLifeTime), 60, 2);
        public static readonly SpaField ParticleLifeTime = H(nameof(ParticleLifeTime), 62, 2);
        public static readonly SpaField RandomScale = H(nameof(RandomScale), 64, 1);
        public static readonly SpaField RandomLifeTime = H(nameof(RandomLifeTime), 65, 1);
        public static readonly SpaField RandomInitVel = H(nameof(RandomInitVel), 66, 1);
        public static readonly SpaField EmissionInterval = H(nameof(EmissionInterval), 68, 4, 0, 8);
        public static readonly SpaField BaseAlpha = H(nameof(BaseAlpha), 68, 4, 8, 8, max: 31);
        public static readonly SpaField AirResistance = H(nameof(AirResistance), 68, 4, 16, 8);
        public static readonly SpaField TextureIndex = H(nameof(TextureIndex), 68, 4, 24, 8);
        public static readonly SpaField LoopFrames = H(nameof(LoopFrames), 72, 4, 0, 8);
        public static readonly SpaField DbbScale = H(nameof(DbbScale), 72, 4, 8, 16, divisor: Fx);
        public static readonly SpaField TextureTileCountS = H(nameof(TextureTileCountS), 72, 4, 24, 2);
        public static readonly SpaField TextureTileCountT = H(nameof(TextureTileCountT), 72, 4, 26, 2);
        public static readonly SpaField ScaleAnimDir = H(nameof(ScaleAnimDir), 72, 4, 28, 3, max: 2);
        public static readonly SpaField DpolFaceEmitter = H(nameof(DpolFaceEmitter), 72, 4, 31, 1);
        public static readonly SpaField FlipTextureS = H(nameof(FlipTextureS), 76, 4, 0, 1);
        public static readonly SpaField FlipTextureT = H(nameof(FlipTextureT), 76, 4, 1, 1);
        public static readonly SpaField PolygonX = H(nameof(PolygonX), 80, 2, signed: true, divisor: Fx);
        public static readonly SpaField PolygonY = H(nameof(PolygonY), 82, 2, signed: true, divisor: Fx);
        public static readonly SpaField UserFlags = H(nameof(UserFlags), 84, 4, 0, 8);

        public static readonly SpaField ScaleAnimStart = F(SpaBlock.ScaleAnim, nameof(ScaleAnimStart), 0, 2, signed: true, divisor: Fx);
        public static readonly SpaField ScaleAnimMid = F(SpaBlock.ScaleAnim, nameof(ScaleAnimMid), 2, 2, signed: true, divisor: Fx);
        public static readonly SpaField ScaleAnimEnd = F(SpaBlock.ScaleAnim, nameof(ScaleAnimEnd), 4, 2, signed: true, divisor: Fx);
        public static readonly SpaField ScaleAnimIn = F(SpaBlock.ScaleAnim, nameof(ScaleAnimIn), 6, 2, 0, 8);
        public static readonly SpaField ScaleAnimOut = F(SpaBlock.ScaleAnim, nameof(ScaleAnimOut), 6, 2, 8, 8);
        public static readonly SpaField ScaleAnimLoop = F(SpaBlock.ScaleAnim, nameof(ScaleAnimLoop), 8, 2, 0, 1);

        public static readonly SpaField ColorAnimStart = Rgb(SpaBlock.ColorAnim, nameof(ColorAnimStart), 0);
        public static readonly SpaField ColorAnimEnd = Rgb(SpaBlock.ColorAnim, nameof(ColorAnimEnd), 2);
        public static readonly SpaField ColorAnimIn = F(SpaBlock.ColorAnim, nameof(ColorAnimIn), 4, 4, 0, 8);
        public static readonly SpaField ColorAnimPeak = F(SpaBlock.ColorAnim, nameof(ColorAnimPeak), 4, 4, 8, 8);
        public static readonly SpaField ColorAnimOut = F(SpaBlock.ColorAnim, nameof(ColorAnimOut), 4, 4, 16, 8);
        public static readonly SpaField ColorAnimRandomStart = F(SpaBlock.ColorAnim, nameof(ColorAnimRandomStart), 8, 2, 0, 1);
        public static readonly SpaField ColorAnimLoop = F(SpaBlock.ColorAnim, nameof(ColorAnimLoop), 8, 2, 1, 1);
        public static readonly SpaField ColorAnimInterpolate = F(SpaBlock.ColorAnim, nameof(ColorAnimInterpolate), 8, 2, 2, 1);

        public static readonly SpaField AlphaAnimStart = F(SpaBlock.AlphaAnim, nameof(AlphaAnimStart), 0, 2, 0, 5);
        public static readonly SpaField AlphaAnimMid = F(SpaBlock.AlphaAnim, nameof(AlphaAnimMid), 0, 2, 5, 5);
        public static readonly SpaField AlphaAnimEnd = F(SpaBlock.AlphaAnim, nameof(AlphaAnimEnd), 0, 2, 10, 5);
        public static readonly SpaField AlphaAnimRandomRange = F(SpaBlock.AlphaAnim, nameof(AlphaAnimRandomRange), 2, 2, 0, 8);
        public static readonly SpaField AlphaAnimLoop = F(SpaBlock.AlphaAnim, nameof(AlphaAnimLoop), 2, 2, 8, 1);
        public static readonly SpaField AlphaAnimIn = F(SpaBlock.AlphaAnim, nameof(AlphaAnimIn), 4, 2, 0, 8);
        public static readonly SpaField AlphaAnimOut = F(SpaBlock.AlphaAnim, nameof(AlphaAnimOut), 4, 2, 8, 8);

        public static readonly SpaField TexAnimFrame0 = F(SpaBlock.TexAnim, nameof(TexAnimFrame0), 0, 1);
        public static readonly SpaField TexAnimFrame1 = F(SpaBlock.TexAnim, nameof(TexAnimFrame1), 1, 1);
        public static readonly SpaField TexAnimFrame2 = F(SpaBlock.TexAnim, nameof(TexAnimFrame2), 2, 1);
        public static readonly SpaField TexAnimFrame3 = F(SpaBlock.TexAnim, nameof(TexAnimFrame3), 3, 1);
        public static readonly SpaField TexAnimFrame4 = F(SpaBlock.TexAnim, nameof(TexAnimFrame4), 4, 1);
        public static readonly SpaField TexAnimFrame5 = F(SpaBlock.TexAnim, nameof(TexAnimFrame5), 5, 1);
        public static readonly SpaField TexAnimFrame6 = F(SpaBlock.TexAnim, nameof(TexAnimFrame6), 6, 1);
        public static readonly SpaField TexAnimFrame7 = F(SpaBlock.TexAnim, nameof(TexAnimFrame7), 7, 1);
        public static readonly SpaField TexAnimFrameCount = F(SpaBlock.TexAnim, nameof(TexAnimFrameCount), 8, 4, 0, 8, max: 8);
        public static readonly SpaField TexAnimStep = F(SpaBlock.TexAnim, nameof(TexAnimStep), 8, 4, 8, 8);
        public static readonly SpaField TexAnimRandomStart = F(SpaBlock.TexAnim, nameof(TexAnimRandomStart), 8, 4, 16, 1);
        public static readonly SpaField TexAnimLoop = F(SpaBlock.TexAnim, nameof(TexAnimLoop), 8, 4, 17, 1);

        public static readonly SpaField ChildUsesBehaviors = F(SpaBlock.Child, nameof(ChildUsesBehaviors), 0, 2, 0, 1);
        public static readonly SpaField ChildHasScaleAnim = F(SpaBlock.Child, nameof(ChildHasScaleAnim), 0, 2, 1, 1);
        public static readonly SpaField ChildHasAlphaAnim = F(SpaBlock.Child, nameof(ChildHasAlphaAnim), 0, 2, 2, 1);
        public static readonly SpaField ChildRotationType = F(SpaBlock.Child, nameof(ChildRotationType), 0, 2, 3, 2, max: 2);
        public static readonly SpaField ChildFollowEmitter = F(SpaBlock.Child, nameof(ChildFollowEmitter), 0, 2, 5, 1);
        public static readonly SpaField ChildUseColor = F(SpaBlock.Child, nameof(ChildUseColor), 0, 2, 6, 1);
        public static readonly SpaField ChildDrawType = F(SpaBlock.Child, nameof(ChildDrawType), 0, 2, 7, 2);
        public static readonly SpaField ChildPolygonRotAxis = F(SpaBlock.Child, nameof(ChildPolygonRotAxis), 0, 2, 9, 2);
        public static readonly SpaField ChildPolygonReferencePlane = F(SpaBlock.Child, nameof(ChildPolygonReferencePlane), 0, 2, 11, 1);
        public static readonly SpaField ChildRandomInitVel = F(SpaBlock.Child, nameof(ChildRandomInitVel), 2, 2, signed: true, divisor: Px);
        public static readonly SpaField ChildEndScale = F(SpaBlock.Child, nameof(ChildEndScale), 4, 2, signed: true, divisor: Fx);
        public static readonly SpaField ChildLifeTime = F(SpaBlock.Child, nameof(ChildLifeTime), 6, 2);
        public static readonly SpaField ChildVelocityRatio = F(SpaBlock.Child, nameof(ChildVelocityRatio), 8, 1);
        public static readonly SpaField ChildScaleRatio = F(SpaBlock.Child, nameof(ChildScaleRatio), 9, 1);
        public static readonly SpaField ChildColor = Rgb(SpaBlock.Child, nameof(ChildColor), 10);
        public static readonly SpaField ChildEmissionCount = F(SpaBlock.Child, nameof(ChildEmissionCount), 12, 4, 0, 8);
        public static readonly SpaField ChildEmissionDelay = F(SpaBlock.Child, nameof(ChildEmissionDelay), 12, 4, 8, 8);
        public static readonly SpaField ChildEmissionInterval = F(SpaBlock.Child, nameof(ChildEmissionInterval), 12, 4, 16, 8);
        public static readonly SpaField ChildTextureIndex = F(SpaBlock.Child, nameof(ChildTextureIndex), 12, 4, 24, 8);
        public static readonly SpaField ChildTextureTileCountS = F(SpaBlock.Child, nameof(ChildTextureTileCountS), 16, 4, 0, 2);
        public static readonly SpaField ChildTextureTileCountT = F(SpaBlock.Child, nameof(ChildTextureTileCountT), 16, 4, 2, 2);
        public static readonly SpaField ChildFlipTextureS = F(SpaBlock.Child, nameof(ChildFlipTextureS), 16, 4, 4, 1);
        public static readonly SpaField ChildFlipTextureT = F(SpaBlock.Child, nameof(ChildFlipTextureT), 16, 4, 5, 1);
        public static readonly SpaField ChildDpolFaceEmitter = F(SpaBlock.Child, nameof(ChildDpolFaceEmitter), 16, 4, 6, 1);

        public static readonly SpaField GravityX = F(SpaBlock.Gravity, nameof(GravityX), 0, 2, signed: true, divisor: Px);
        public static readonly SpaField GravityY = F(SpaBlock.Gravity, nameof(GravityY), 2, 2, signed: true, divisor: Px);
        public static readonly SpaField GravityZ = F(SpaBlock.Gravity, nameof(GravityZ), 4, 2, signed: true, divisor: Px);

        public static readonly SpaField RandomX = F(SpaBlock.Random, nameof(RandomX), 0, 2, signed: true, divisor: Px);
        public static readonly SpaField RandomY = F(SpaBlock.Random, nameof(RandomY), 2, 2, signed: true, divisor: Px);
        public static readonly SpaField RandomZ = F(SpaBlock.Random, nameof(RandomZ), 4, 2, signed: true, divisor: Px);
        public static readonly SpaField RandomInterval = F(SpaBlock.Random, nameof(RandomInterval), 6, 2);

        public static readonly SpaField MagnetTargetX = F(SpaBlock.Magnet, nameof(MagnetTargetX), 0, 4, signed: true, divisor: Px);
        public static readonly SpaField MagnetTargetY = F(SpaBlock.Magnet, nameof(MagnetTargetY), 4, 4, signed: true, divisor: Px);
        public static readonly SpaField MagnetTargetZ = F(SpaBlock.Magnet, nameof(MagnetTargetZ), 8, 4, signed: true, divisor: Px);
        public static readonly SpaField MagnetForce = F(SpaBlock.Magnet, nameof(MagnetForce), 12, 2, signed: true, divisor: Fx);

        // Signed: a negative angle spins the other way.
        public static readonly SpaField SpinAngle = F(SpaBlock.Spin, nameof(SpinAngle), 0, 2, signed: true, divisor: Deg);
        public static readonly SpaField SpinAxis = F(SpaBlock.Spin, nameof(SpinAxis), 2, 2, max: 2);

        public static readonly SpaField CollisionY = F(SpaBlock.Collision, nameof(CollisionY), 0, 4, signed: true, divisor: Px);
        public static readonly SpaField CollisionElasticity = F(SpaBlock.Collision, nameof(CollisionElasticity), 4, 2, signed: true, divisor: Fx);
        public static readonly SpaField CollisionType = F(SpaBlock.Collision, nameof(CollisionType), 6, 2, 0, 2);

        public static readonly SpaField ConvergenceTargetX = F(SpaBlock.Convergence, nameof(ConvergenceTargetX), 0, 4, signed: true, divisor: Px);
        public static readonly SpaField ConvergenceTargetY = F(SpaBlock.Convergence, nameof(ConvergenceTargetY), 4, 4, signed: true, divisor: Px);
        public static readonly SpaField ConvergenceTargetZ = F(SpaBlock.Convergence, nameof(ConvergenceTargetZ), 8, 4, signed: true, divisor: Px);
        public static readonly SpaField ConvergenceForce = F(SpaBlock.Convergence, nameof(ConvergenceForce), 12, 2, signed: true, divisor: Fx);

        /// <summary>RGB555 to 8-bit channels, as SpaArchive expands colours.</summary>
        public static (byte R, byte G, byte B) ToRgb888(long raw)
            => ((byte)(((raw & 0x1F) * 255) / 31), (byte)((((raw >> 5) & 0x1F) * 255) / 31), (byte)((((raw >> 10) & 0x1F) * 255) / 31));

        public static long FromRgb888(byte r, byte g, byte b)
            => (long)(Math.Round(r * 31 / 255.0)) | ((long)Math.Round(g * 31 / 255.0) << 5) | ((long)Math.Round(b * 31 / 255.0) << 10);
    }
}
