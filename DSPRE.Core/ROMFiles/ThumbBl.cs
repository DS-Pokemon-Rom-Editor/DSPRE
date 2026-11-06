using System;

namespace DSPRE.ROMFiles
{
    /// <summary>Thumb's two-halfword bl and blx calls.</summary>
    public static class ThumbBl
    {
        /// <summary>Where the call at <paramref name="at"/> in <paramref name="code"/> goes, or null when there is none there.</summary>
        /// <param name="address">The RAM address of <paramref name="at"/>.</param>
        /// <param name="blx">A blx to an ARM function instead of a bl. The target is word aligned.</param>
        public static uint? Target(byte[] code, int at, uint address, bool blx = false)
        {
            if (code == null || at < 0 || at + 4 > code.Length) return null;
            ushort hi = BitConverter.ToUInt16(code, at), lo = BitConverter.ToUInt16(code, at + 2);
            if ((hi & 0xF800) != 0xF000 || (lo & 0xF800) != (blx ? 0xE800 : 0xF800)) return null;
            int off = ((hi & 0x7FF) << 12) | ((lo & 0x7FF) << 1);
            if ((off & 0x400000) != 0) off -= 0x800000;
            uint to = unchecked((uint)(address + 4 + off));
            return blx ? to & ~3u : to;
        }

        /// <summary>A bl from <paramref name="from"/> to <paramref name="to"/>.</summary>
        public static byte[] Make(uint from, uint to)
        {
            int off = unchecked((int)(to - (from + 4)));
            if (off < -0x400000 || off >= 0x400000) throw new InvalidOperationException("The call is out of a bl's reach.");
            ushort hi = (ushort)(0xF000 | ((off >> 12) & 0x7FF)), lo = (ushort)(0xF800 | ((off >> 1) & 0x7FF));
            return new[] { (byte)hi, (byte)(hi >> 8), (byte)lo, (byte)(lo >> 8) };
        }
    }
}
