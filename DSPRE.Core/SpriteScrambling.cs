namespace DSPRE
{
    /// <summary>Sprite pixels XORed with a rolling key: backwards from the last word on Diamond and Pearl, forwards from the first later.</summary>
    public static class SpriteScrambling
    {
        public static void Unscramble(byte[] data, int off, int size)
        {
            int words = size / 2;
            if (words <= 0 || off < 0 || off + words * 2 > data.Length) return;

            ushort At(int i) => (ushort)(data[off + i * 2] | (data[off + i * 2 + 1] << 8));
            void Put(int i, ushort v) { data[off + i * 2] = (byte)(v & 0xFF); data[off + i * 2 + 1] = (byte)(v >> 8); }

            unchecked
            {
                if (RomInfo.gameFamily != RomInfo.GameFamilies.DP)
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

        public static void Scramble(byte[] data, int off, int size, ushort seed)
        {
            int words = size / 2;
            if (words <= 0 || off < 0 || off + words * 2 > data.Length) return;

            ushort At(int i) => (ushort)(data[off + i * 2] | (data[off + i * 2 + 1] << 8));
            void Put(int i, ushort v) { data[off + i * 2] = (byte)(v & 0xFF); data[off + i * 2 + 1] = (byte)(v >> 8); }

            Put(RomInfo.gameFamily != RomInfo.GameFamilies.DP ? 0 : words - 1, 0);

            unchecked
            {
                if (RomInfo.gameFamily != RomInfo.GameFamilies.DP)
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
        public static ushort Seed(byte[] data, int off, int size)
        {
            int words = size / 2;
            if (words <= 0) return 0;
            int at = RomInfo.gameFamily != RomInfo.GameFamilies.DP ? 0 : words - 1;
            return (ushort)(data[off + at * 2] | (data[off + at * 2 + 1] << 8));
        }
    }
}
