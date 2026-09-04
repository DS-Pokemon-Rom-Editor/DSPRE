using System;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// Shared low-level Nitro NCLR+NCGR+NSCR decoder: self-contained byte-offset parsing (no
    /// Images.NCLR/NCGR/NSCR dependency) for fast read-side compositing. Originally lived only in
    /// <see cref="BattleBgRenderer"/>; extracted so <see cref="DungeonCutinGraphics"/> can reuse the
    /// exact same, already-correct tile/palette/screen math instead of a second implementation.
    /// </summary>
    public static class NitroBgCodec
    {
        public sealed class BgImage { public byte[] Rgba; public int Width, Height; }

        public static byte[] Inflate(byte[] b)
        {
            if (b == null) return null;
            if (b.Length > 0 && b[0] == 0x10) { try { return NSMBe4.ROM.LZ77_Decompress(b); } catch { return b; } }
            return b;
        }

        public static int Find(byte[] d, string magic, int from)
        {
            for (int i = from; i + 4 <= d.Length; i += 4)
                if (d[i] == magic[0] && d[i + 1] == magic[1] && d[i + 2] == magic[2] && d[i + 3] == magic[3]) return i;
            return -1;
        }
        public static int U16(byte[] d, int o) => d[o] | (d[o + 1] << 8);
        public static int U32(byte[] d, int o) => d[o] | (d[o + 1] << 8) | (d[o + 2] << 16) | (d[o + 3] << 24);

        public static (byte r, byte g, byte b)[] ReadPalette(byte[] pal, out int palCount)
        {
            // NCLR PLTT: colours are RGB555 u16 starting 0x18 into the block.
            int pltt = Find(pal, "TTLP", 0);
            int palData = pltt >= 0 ? pltt + 0x18 : 0x28;
            palCount = Math.Min(256, (pal.Length - palData) / 2);
            var colors = new (byte r, byte g, byte b)[Math.Max(256, palCount)];
            for (int i = 0; i < palCount; i++)
            {
                int c = U16(pal, palData + i * 2);
                colors[i] = ((byte)((c & 0x1F) << 3), (byte)(((c >> 5) & 0x1F) << 3), (byte)(((c >> 10) & 0x1F) << 3));
            }
            return colors;
        }

        /// <summary>Where a drawing keeps its pixels and how many colours they point at.</summary>
        public static (bool EightBit, int TilesAt) ReadTileHeader(byte[] chr)
        {
            // NCGR RAHC: bitdepth at +0x0C, 3 for sixteen colours and 4 for two hundred and fifty six;
            // the pixels start at +0x20.
            int rahc = chr == null ? -1 : Find(chr, "RAHC", 0);
            int depth = rahc >= 0 ? U32(chr, rahc + 0x0C) : 3;
            return (depth == 4, rahc >= 0 ? rahc + 0x20 : 0x30);
        }

        /// <summary>How big a background is and where its arrangement starts.</summary>
        public static (int Width, int Height, int MapAt) ReadScreenHeader(byte[] scr)
        {
            // NSCR NRCS: width and height in pixels at +0x08 and +0x0A, the arrangement at +0x14.
            int nrcs = scr == null ? -1 : Find(scr, "NRCS", 0);
            int w = nrcs >= 0 ? U16(scr, nrcs + 0x08) : 256;
            int h = nrcs >= 0 ? U16(scr, nrcs + 0x0A) : 256;
            if (w <= 0 || w > 1024) w = 256;
            if (h <= 0 || h > 1024) h = 256;
            return (w, h, nrcs >= 0 ? nrcs + 0x14 : 0x24);
        }

        /// <summary>
        /// Where one square of the screen keeps its entry. A background wider than 32 squares is stored
        /// in blocks of 32 by 32, block after block; a narrower one is stored straight across at its own
        /// width. Read off the ROM's own files: complete narrow arrangements in HeartGold hold
        /// width-by-height entries, and the wide ones only make a solid picture read as blocks. Some
        /// screens deliberately carry fewer entries than their declared canvas; the compositor leaves
        /// the missing part empty through its bounds check.
        /// </summary>
        public static int SquareIndex(int cols, int tx, int ty)
        {
            if (cols <= 32) return ty * cols + tx;
            int blocksX = (cols + 31) / 32;
            return ((ty / 32) * blocksX + (tx / 32)) * 1024 + (ty % 32) * 32 + (tx % 32);
        }

        /// <summary>How many entries an arrangement of this size holds.</summary>
        public static int SquareCount(int cols, int rows)
            => cols <= 32 ? cols * rows : ((cols + 31) / 32) * ((rows + 31) / 32) * 1024;

        private static void ReadNcgrHeader(byte[] chr, out bool is8, out int tileBytes)
        {
            var head = ReadTileHeader(chr);
            is8 = head.EightBit;
            tileBytes = head.TilesAt;
        }

        private static void BlitTile(byte[] rgba, int w, byte[] chr, int tileBytes, bool is8,
            (byte r, byte g, byte b)[] colors, int palCount, int tile, int palNo, bool flipH, bool flipV,
            bool transparentZero, int dstTx, int dstTy)
        {
            int tbase = tileBytes + tile * (is8 ? 64 : 32);
            for (int py = 0; py < 8; py++)
                for (int px = 0; px < 8; px++)
                {
                    int sx = flipH ? 7 - px : px, sy = flipV ? 7 - py : py;
                    int idx;
                    if (is8) { int off = tbase + sy * 8 + sx; idx = off < chr.Length ? chr[off] : 0; }
                    else { int off = tbase + (sy * 8 + sx) / 2; int bb = off < chr.Length ? chr[off] : 0; idx = ((sx & 1) == 0) ? (bb & 0xF) : (bb >> 4); }
                    if (transparentZero && idx == 0) continue;
                    int ci = is8 ? idx : palNo * 16 + idx;
                    if (ci >= palCount) ci = idx;
                    if (ci >= colors.Length) continue;
                    var (r, g, b) = colors[ci];
                    int dst = ((dstTy * 8 + py) * w + (dstTx * 8 + px)) * 4;
                    rgba[dst] = r; rgba[dst + 1] = g; rgba[dst + 2] = b; rgba[dst + 3] = 255;
                }
        }

        public static BgImage Composite(byte[] chr, byte[] pal, byte[] scr, bool transparentZero = true)
        {
            var colors = ReadPalette(pal, out int palCount);
            return Composite(chr, colors, palCount, scr, transparentZero);
        }

        /// <summary>
        /// The same, with the colours already in hand. Some screens are drawn with more than one
        /// palette file loaded over each other, row by row, and only the caller knows which.
        /// </summary>
        public static BgImage Composite(byte[] chr, (byte r, byte g, byte b)[] colors, int palCount, byte[] scr,
                                        bool transparentZero = true)
        {
            ReadNcgrHeader(chr, out bool is8, out int tileBytes);

            var (w, h, mapData) = ReadScreenHeader(scr);

            var rgba = new byte[w * h * 4];
            int cols = w / 8, rows = h / 8;
            for (int ty = 0; ty < rows; ty++)
                for (int tx = 0; tx < cols; tx++)
                {
                    int mo = mapData + SquareIndex(cols, tx, ty) * 2;
                    if (mo + 1 >= scr.Length) continue;
                    int e = U16(scr, mo);
                    int tile = e & 0x3FF, palNo = (e >> 12) & 0xF;
                    bool flipH = ((e >> 10) & 1) != 0, flipV = ((e >> 11) & 1) != 0;
                    BlitTile(rgba, w, chr, tileBytes, is8, colors, palCount, tile, palNo, flipH, flipV, transparentZero, tx, ty);
                }
            return new BgImage { Rgba = rgba, Width = w, Height = h };
        }
    }
}
