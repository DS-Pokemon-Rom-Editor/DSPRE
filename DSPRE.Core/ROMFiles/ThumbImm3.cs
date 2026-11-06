namespace DSPRE.ROMFiles
{
    /// <summary>Thumb's adds rd, rn, #n with a three-bit immediate.</summary>
    public static class ThumbImm3
    {
        public static bool Is(ushort op) => (op & 0xFE00) == 0x1C00;
        public static int Value(ushort op) => op >> 6 & 7;
        public static ushort With(ushort op, int value) => (ushort)(op & ~0x1C0 | (value & 7) << 6);
    }

    /// <summary>Thumb's lsls rd, rm, #n.</summary>
    public static class ThumbLsls
    {
        public static bool Is(ushort op) => (op & 0xF800) == 0;
        public static int Amount(ushort op) => op >> 6 & 0x1F;
        public static ushort With(ushort op, int amount) => (ushort)(op & 0xF83F | (amount & 0x1F) << 6);
    }
}
