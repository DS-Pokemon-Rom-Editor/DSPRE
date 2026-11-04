using System;
using System.IO;

namespace DSPRE.ROMFiles
{
    /// <summary>The DP and Pt Pokédex animation for each type and for the category box, read from the code. Type expansions change both.</summary>
    public static class PokedexTypeAnimations
    {
        // PokedexGraphics_GetAnimIDfromType in retail: ??? shares Ghost's icon.
        private static readonly int[] Retail = { 0, 6, 14, 10, 8, 5, 11, 7, 9, 7, 1, 3, 2, 4, 15, 13, 16, 12 };
        public const int RetailBox = 0x11;

        /// <summary>The animation for a type, or -1 when the game has none for it.</summary>
        public static int ForType(int type)
        {
            if (type < 0) return -1;
            byte[] code = Code(RomLocator.Thing.PokedexTypeAnimation, 4);
            if (code != null)
            {
                ushort first = BitConverter.ToUInt16(code, 0), second = BitConverter.ToUInt16(code, 2);
                // adds r0, #n; bx lr
                if (ThumbImm8.Is(first, ThumbImm8.Op.Adds) && ThumbImm8.Register(first) == 0 && second == 0x4770)
                    return type + ThumbImm8.Value(first);
            }
            return type < Retail.Length ? Retail[type] : -1;
        }

        /// <summary>The category box's animation.</summary>
        public static int Box()
        {
            byte[] code = Code(RomLocator.Thing.PokedexTypeBoxAnimation, 2);
            if (code == null) return RetailBox;
            ushort op = BitConverter.ToUInt16(code, 0);
            return ThumbImm8.Is(op, ThumbImm8.Op.Movs) && ThumbImm8.Register(op) == 1 ? ThumbImm8.Value(op) : RetailBox;
        }

        // A preview must not rewrite the project, so a still-compressed overlay is read as retail.
        private static byte[] Code(RomLocator.Thing thing, int length)
        {
            try
            {
                RomLocator.Spot spot = RomLocator.Where(thing);
                if (spot == null || RomLocator.WhyNot(thing, length) != null) return null;
                if (spot.Overlay >= 0 && OverlayUtils.IsStillCompressed(spot.Overlay)) return null;
                return DSUtils.ReadFromFile(RomLocator.PathOf(spot), spot.Offset, length);
            }
            catch (Exception e) when (e is IOException || e is InvalidOperationException || e is UnauthorizedAccessException)
            {
                AppLogger.WarnOnce("PokedexTypeAnimations.Code", e);
                return null;
            }
        }
    }
}
