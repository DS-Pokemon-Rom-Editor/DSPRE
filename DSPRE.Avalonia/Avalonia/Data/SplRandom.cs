using System;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// The particle library's random generator and range helpers (pokeplatinum lib/spl/include/spl_random.h,
    /// lib/spl/src/spl_random.c). Every helper consumes exactly one draw, as the library's macros do, even when
    /// its range is zero, so per-emitter sequences line up with the game's call order.
    /// </summary>
    public sealed class SplRandom
    {
        public uint State;

        public SplRandom(uint seed = 0) { State = seed; }

        public void Reset(uint seed) => State = seed;

        public uint Next() => State = unchecked(State * 0x5eedf715u + 0x1b0cb173u);

        /// <summary>SPLRandom_U32 / SPLRandom_S32: the top <paramref name="bits"/> bits.</summary>
        public uint U32(int bits) => Next() >> (32 - bits);

        /// <summary>SPLRandom_FX32: an arithmetic shift, so the result is signed.</summary>
        public int Fx32(int bits) => (int)Next() >> (32 - bits);

        /// <summary>SPLRandom_ScaledRangeFX32 as a multiplier: num * (255 - ((range * U8) >> 8)) / 256.</summary>
        public double ScaledRange(int range) => (255 - ((range * (int)U32(8)) >> 8)) / 256.0;

        /// <summary>Integer form of SPLRandom_ScaledRangeFX32, truncating like the library.</summary>
        public int ScaledRange(int num, int range) => (num * (255 - ((range * (int)U32(8)) >> 8))) >> 8;

        /// <summary>SPLRandom_DoubleScaledRangeFX32 as a multiplier: (255 + range - ((range * U8) >> 7)) / 256.</summary>
        public double DoubleScaledRange(int range) => (255 + range - ((range * (int)U32(8)) >> 7)) / 256.0;

        /// <summary>SPLRandom_RangeFX32: num * (U9 - 256) / 256, uniform in [-num, num).</summary>
        public double Range(double num) => num * ((int)U32(9) - 256) / 256.0;

        /// <summary>SPLRandom_BetweenFX32: min + (max - min) * U12 / 4096.</summary>
        public double Between(double min, double max) => min + (max - min) * U32(12) / 4096.0;

        /// <summary>SPLRandom_VecFx32: three signed 24-bit components, normalised.</summary>
        public (double X, double Y, double Z) Vec()
        {
            double x = Fx32(24), y = Fx32(24), z = Fx32(24);
            double l = Math.Sqrt(x * x + y * y + z * z);
            return l == 0 ? (0.0, 0.0, 0.0) : (x / l, y / l, z / l);
        }

        /// <summary>SPLRandom_VecFx32_XY: two signed 24-bit components, z = 0, normalised.</summary>
        public (double X, double Y) VecXY()
        {
            double x = Fx32(24), y = Fx32(24);
            double l = Math.Sqrt(x * x + y * y);
            return l == 0 ? (0.0, 0.0) : (x / l, y / l);
        }
    }
}
