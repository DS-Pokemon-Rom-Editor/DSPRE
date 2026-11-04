using System;
using System.IO;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// The game's shiny check, CalcShininessByOtIdAndPersonality: a Pokémon is shiny when its OT ID and PID halves,
    /// XORed together, come out below the compare value.
    /// </summary>
    public static class ShinyCheck
    {
        public const int Retail = 8;

        // What follows the cmp in every dump: bhs, movs r0 #1, b, movs r0 #0.
        private static readonly byte[] Tail = { 0x01, 0xD2, 0x01, 0x20, 0x00, 0xE0, 0x00, 0x20 };
        private const int LsrsImm = 0b00001;
        private const ushort ThumbNop = 0x46C0;   // mov r8, r8

        /// <summary>
        /// How many of the 65536 XOR results are shiny: 8 in the retail games, more or fewer after a shiny rate patch
        /// that changes the compare. Null when a patch changed the code in a way DSPRE can't read.
        /// </summary>
        public static int? Values()
        {
            if (RomLocator.WhyNot(RomLocator.Thing.ShinyCheck, 2 + Tail.Length) != null) return null;
            try
            {
                byte[] file = RomLocator.ReadFile(RomLocator.Thing.ShinyCheck);
                int at = RomLocator.Where(RomLocator.Thing.ShinyCheck).Offset;
                if (at < 4 || at + 2 + Tail.Length > file.Length) return null;
                ushort cmp = BitConverter.ToUInt16(file, at);
                if (!ThumbImm8.Is(cmp, ThumbImm8.Op.Cmp) || !file.AsSpan(at + 2, Tail.Length).SequenceEqual(Tail)) return null;
                // Mikelan98's rate change puts lsrs r0, r0, #n; nop before the compare, so its value is shifted left by n.
                ushort shift = BitConverter.ToUInt16(file, at - 4), nop = BitConverter.ToUInt16(file, at - 2);
                int bits = shift >> 11 == LsrsImm && (shift & 0x3F) == 0 && nop == ThumbNop ? shift >> 6 & 0x1F : 0;
                return ThumbImm8.Value(cmp) << bits;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is InvalidOperationException)
            {
                AppLogger.WarnOnce("ShinyCheck.Values", ex);
                return null;
            }
        }
    }
}
