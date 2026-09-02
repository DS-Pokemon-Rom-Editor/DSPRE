using System;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// Makes the three files a background is drawn from. Every field here was read off real files in
    /// HeartGold first: the colour lists all carry a depth, a size and sixteen colours a bank
    /// (checked over 429 of them), the tile sheets keep their size at the sixteenth and eighteenth
    /// bytes of RAHC, and the arrangement keeps its size in pixels at the eighth.
    /// </summary>
    public static class NitroBgWrite
    {
        private static void U16(byte[] d, int o, int v) { d[o] = (byte)v; d[o + 1] = (byte)(v >> 8); }
        private static void U32(byte[] d, int o, int v)
        { d[o] = (byte)v; d[o + 1] = (byte)(v >> 8); d[o + 2] = (byte)(v >> 16); d[o + 3] = (byte)(v >> 24); }
        private static void Tag(byte[] d, int o, string s)
        { for (int i = 0; i < 4; i++) d[o + i] = (byte)s[i]; }

        /// <summary>The outer wrapper every Nitro file starts with.</summary>
        private static void Envelope(byte[] d, string magic, int fileSize, int sections)
        {
            Tag(d, 0, magic);
            d[4] = 0xFF; d[5] = 0xFE;           // byte order
            d[6] = 0x00; d[7] = 0x01;           // version
            U32(d, 8, fileSize);
            U16(d, 0x0C, 0x10);                 // header size
            U16(d, 0x0E, sections);
        }

        /// <param name="colours">RGB555 already, one entry a colour, laid out bank after bank.</param>
        public static byte[] Palette(ushort[] colours, bool eightBit)
        {
            int data = colours.Length * 2;
            var d = new byte[0x28 + data];
            Envelope(d, "RLCN", d.Length, 1);
            Tag(d, 0x10, "TTLP");
            U32(d, 0x14, d.Length - 0x10);
            U32(d, 0x18, eightBit ? 4 : 3);
            U32(d, 0x1C, 0);
            U32(d, 0x20, data);
            U32(d, 0x24, 16);                   // real files say sixteen a bank whatever the depth
            for (int i = 0; i < colours.Length; i++) U16(d, 0x28 + i * 2, colours[i]);
            return d;
        }

        /// <param name="pixels">Colour numbers, one a pixel, tile after tile.</param>
        public static byte[] Tiles(byte[] pixels, int tileCount, bool eightBit)
        {
            int bytesPerTile = eightBit ? 64 : 32;
            var data = new byte[tileCount * bytesPerTile];
            if (eightBit) Array.Copy(pixels, data, Math.Min(pixels.Length, data.Length));
            else
                for (int i = 0; i + 1 < pixels.Length && i / 2 < data.Length; i += 2)
                    data[i / 2] = (byte)((pixels[i] & 0x0F) | ((pixels[i + 1] & 0x0F) << 4));

            // Real sheets say how many tiles across and down they are. A sheet built here is one long
            // strip of tiles, so it is that many across and one down.
            var d = new byte[0x30 + data.Length + 0x10];
            Envelope(d, "RGCN", d.Length, 2);
            d[6] = 0x01; d[7] = 0x01;           // the two-section sort the games write
            Tag(d, 0x10, "RAHC");
            U32(d, 0x14, 0x20 + data.Length);
            U16(d, 0x18, 1);                    // tiles down
            U16(d, 0x1A, tileCount);            // tiles across
            U32(d, 0x1C, eightBit ? 4 : 3);
            U32(d, 0x20, 0);
            U32(d, 0x24, 0);                    // kept in tiles, not straight across
            U32(d, 0x28, data.Length);
            U32(d, 0x2C, 0x18);
            Array.Copy(data, 0, d, 0x30, data.Length);

            int sopc = 0x30 + data.Length;
            Tag(d, sopc, "SOPC");
            U32(d, sopc + 4, 0x10);
            U32(d, sopc + 8, 0);
            U16(d, sopc + 0x0C, tileCount);
            U16(d, sopc + 0x0E, 1);
            return d;
        }

        /// <param name="squares">One entry a square of the screen, already carrying tile, turns and bank.</param>
        public static byte[] Arrangement(ushort[] squares, int widthPixels, int heightPixels)
        {
            int data = squares.Length * 2;
            var d = new byte[0x24 + data];
            Envelope(d, "RCSN", d.Length, 1);
            Tag(d, 0x10, "NRCS");
            U32(d, 0x14, d.Length - 0x10);
            U16(d, 0x18, widthPixels);
            U16(d, 0x1A, heightPixels);
            U32(d, 0x1C, 0);
            U32(d, 0x20, data);
            for (int i = 0; i < squares.Length; i++) U16(d, 0x24 + i * 2, squares[i]);
            return d;
        }
    }
}
