namespace DSPRE
{
    /// <summary>Sprite pixels XORed with a rolling key: backwards from the last word on Diamond and Pearl, forwards from the first later.</summary>
    public static class SpriteScrambling
    {
        public static void Unscramble(byte[] data, int off, int size) =>
            Unscramble(data, off, size, RomInfo.gameFamily == RomInfo.GameFamilies.DP);

        /// <summary>Direction given explicitly: HGSS trainer scans are keyed from the end like DP sprites.</summary>
        public static void Unscramble(byte[] data, int off, int size, bool fromEnd)
        {
            int words = size / 2;
            if (words <= 0 || off < 0 || off + words * 2 > data.Length) return;

            ushort At(int i) => (ushort)(data[off + i * 2] | (data[off + i * 2 + 1] << 8));
            void Put(int i, ushort v) { data[off + i * 2] = (byte)(v & 0xFF); data[off + i * 2 + 1] = (byte)(v >> 8); }

            unchecked
            {
                if (!fromEnd)
                {
                    uint key = At(0);
                    for (int i = 0; i < words; i++)
                    {
                        Put(i, (ushort)(At(i) ^ (ushort)(key & 0xFFFF)));
                        key = key * 1103515245 + 24691;
                    }
                }
                else
                {
                    uint key = At(words - 1);
                    for (int i = words - 1; i >= 0; i--)
                    {
                        Put(i, (ushort)(At(i) ^ (ushort)(key & 0xFFFF)));
                        key = key * 1103515245 + 24691;
                    }
                }
            }
        }

        public static void Scramble(byte[] data, int off, int size, ushort seed) =>
            Scramble(data, off, size, seed, RomInfo.gameFamily == RomInfo.GameFamilies.DP);

        public static void Scramble(byte[] data, int off, int size, ushort seed, bool fromEnd)
        {
            int words = size / 2;
            if (words <= 0 || off < 0 || off + words * 2 > data.Length) return;

            ushort At(int i) => (ushort)(data[off + i * 2] | (data[off + i * 2 + 1] << 8));
            void Put(int i, ushort v) { data[off + i * 2] = (byte)(v & 0xFF); data[off + i * 2 + 1] = (byte)(v >> 8); }

            Put(fromEnd ? words - 1 : 0, 0);

            unchecked
            {
                if (!fromEnd)
                {
                    uint key = seed;
                    for (int i = 0; i < words; i++)
                    {
                        Put(i, (ushort)(At(i) ^ (ushort)(key & 0xFFFF)));
                        key = key * 1103515245 + 24691;
                    }
                }
                else
                {
                    uint key = seed;
                    for (int i = words - 1; i >= 0; i--)
                    {
                        Put(i, (ushort)(At(i) ^ (ushort)(key & 0xFFFF)));
                        key = key * 1103515245 + 24691;
                    }
                }
            }
        }

        /// <summary>The seed word, needed to scramble the sprite again.</summary>
        public static ushort Seed(byte[] data, int off, int size) =>
            Seed(data, off, size, RomInfo.gameFamily == RomInfo.GameFamilies.DP);

        public static ushort Seed(byte[] data, int off, int size, bool fromEnd)
        {
            int words = size / 2;
            if (words <= 0) return 0;
            int at = fromEnd ? words - 1 : 0;
            return (ushort)(data[off + at * 2] | (data[off + at * 2 + 1] << 8));
        }
    }
}
