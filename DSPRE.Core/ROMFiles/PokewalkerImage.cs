using System;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// The Pokéwalker's own picture format, as HeartGold and SoulSilver keep it in a/2/5/6 (sprites, 64 by 96) and
    /// a/2/4/8 (icons, 32 by 48), each member LZ-packed. The picture runs in strips eight rows tall; each column of a
    /// strip is one word, high byte first, whose low byte holds one bit plane and high byte the other for rows 0-7.
    /// A pixel is 0 (white) to 3 (black). hg-engine builds them with tools/ENCODE_IMG from its PNGs.
    /// </summary>
    public static class PokewalkerImage
    {
        /// <summary>The shades hg-engine's PNGs give the four values.</summary>
        public static readonly uint[] Shades = { 0xFFFFFFFFu, 0xFFA8A8A8u, 0xFF505050u, 0xFF000000u };

        public const int SpriteWidth = 64, SpriteHeight = 96, IconWidth = 32, IconHeight = 48;

        /// <summary>The bytes a picture of this size takes, or -1 when its height isn't whole strips.</summary>
        public static int Size(int width, int height) => height % 8 != 0 ? -1 : width * height / 4;

        public static byte[] Decode(byte[] data, int width, int height)
        {
            if (data == null || Size(width, height) < 0 || data.Length < Size(width, height)) return null;
            byte[] pixels = new byte[width * height];
            int word = 0;
            for (int r = 0; r < height; r += 8)
                for (int c = 0; c < width; c++, word++)
                {
                    int value = (data[word * 2] << 8) | data[word * 2 + 1];
                    for (int r2 = 0; r2 < 8; r2++)
                        pixels[(r + r2) * width + c] = (byte)(((value >> r2) & 1) | (((value >> (8 + r2)) & 1) << 1));
                }
            return pixels;
        }

        /// <summary>Packs values 0-3 back into the format. Null when a value is out of range or the size is wrong.</summary>
        public static byte[] Encode(byte[] pixels, int width, int height)
        {
            int size = Size(width, height);
            if (pixels == null || size < 0 || pixels.Length != width * height) return null;
            byte[] data = new byte[size];
            int word = 0;
            for (int r = 0; r < height; r += 8)
                for (int c = 0; c < width; c++, word++)
                {
                    int value = 0;
                    for (int r2 = 0; r2 < 8; r2++)
                    {
                        byte p = pixels[(r + r2) * width + c];
                        if (p > 3) return null;
                        value |= (p & 1) << r2 | ((p >> 1) & 1) << (8 + r2);
                    }
                    data[word * 2] = (byte)(value >> 8);
                    data[word * 2 + 1] = (byte)value;
                }
            return data;
        }
    }
}
