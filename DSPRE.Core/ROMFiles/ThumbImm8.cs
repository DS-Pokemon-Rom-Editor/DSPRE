namespace DSPRE.ROMFiles
{
    /// <summary>Thumb's one-byte immediate instructions: movs, cmp, adds and subs rd, #n.</summary>
    public static class ThumbImm8
    {
        public enum Op { Movs = 0b00100, Cmp = 0b00101, Adds = 0b00110, Subs = 0b00111 }

        public static bool Is(ushort op, Op kind) => op >> 11 == (int)kind;
        public static int Register(ushort op) => op >> 8 & 7;
        public static int Value(ushort op) => op & 0xFF;
        public static ushort Make(Op kind, int register, int value) => (ushort)((int)kind << 11 | (register & 7) << 8 | value & 0xFF);
    }
}
