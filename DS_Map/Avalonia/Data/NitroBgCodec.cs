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
        private static int U16(byte[] d, int o) => d[o] | (d[o + 1] << 8);
        private static int U32(byte[] d, int o) => d[o] | (d[o + 1] << 8) | (d[o + 2] << 16) | (d[o + 3] << 24);

        public static BgImage Composite(byte[] chr, byte[] pal, byte[] scr)
        {
            // NCLR PLTT: colours are RGB555 u16 starting 0x18 into the block.
            int pltt = Find(pal, "TTLP", 0);
            int palData = pltt >= 0 ? pltt + 0x18 : 0x28;
            int palCount = Math.Min(256, (pal.Length - palData) / 2);
            var colors = new (byte r, byte g, byte b)[Math.Max(256, palCount)];
            for (int i = 0; i < palCount; i++)
            {
                int c = U16(pal, palData + i * 2);
                colors[i] = ((byte)((c & 0x1F) << 3), (byte)(((c >> 5) & 0x1F) << 3), (byte)(((c >> 10) & 0x1F) << 3));
            }

            // NCGR RAHC: bitdepth at +0x0C (3 = 4bpp, 4 = 8bpp); tile bytes at +0x20.
            int rahc = Find(chr, "RAHC", 0);
            int depth = rahc >= 0 ? U32(chr, rahc + 0x0C) : 3;
            bool is8 = depth == 4;
            int tileBytes = rahc >= 0 ? rahc + 0x20 : 0x30;

            // NSCR NRCS: width/height in px at +0x08/+0x0A; map u16 entries at +0x14.
            int nrcs = Find(scr, "NRCS", 0);
            int w = nrcs >= 0 ? U16(scr, nrcs + 0x08) : 256;
            int h = nrcs >= 0 ? U16(scr, nrcs + 0x0A) : 256;
            if (w <= 0 || w > 1024) w = 256;
            if (h <= 0 || h > 1024) h = 256;
            int mapData = nrcs >= 0 ? nrcs + 0x14 : 0x24;

            var rgba = new byte[w * h * 4];
            int cols = w / 8, rows = h / 8, blocksX = (cols + 31) / 32;
            for (int ty = 0; ty < rows; ty++)
                for (int tx = 0; tx < cols; tx++)
                {
                    // NDS BG screen data is stored in 256×256 (32×32-tile) blocks, not a linear grid. A 512-wide
                    // map is [block(0,0)][block(1,0)]..., so index by block, then local (x,y) within the block.
                    int mapIdx = ((ty / 32) * blocksX + (tx / 32)) * 1024 + (ty % 32) * 32 + (tx % 32);
                    int mo = mapData + mapIdx * 2;
                    if (mo + 1 >= scr.Length) continue;
                    int e = U16(scr, mo);
                    int tile = e & 0x3FF, palNo = (e >> 12) & 0xF;
                    bool flipH = ((e >> 10) & 1) != 0, flipV = ((e >> 11) & 1) != 0;
                    int tbase = tileBytes + tile * (is8 ? 64 : 32);
                    for (int py = 0; py < 8; py++)
                        for (int px = 0; px < 8; px++)
                        {
                            int sx = flipH ? 7 - px : px, sy = flipV ? 7 - py : py;
                            int idx;
                            if (is8) { int off = tbase + sy * 8 + sx; idx = off < chr.Length ? chr[off] : 0; }
                            else { int off = tbase + (sy * 8 + sx) / 2; int bb = off < chr.Length ? chr[off] : 0; idx = ((sx & 1) == 0) ? (bb & 0xF) : (bb >> 4); }
                            if (idx == 0) continue;                       // colour 0 = transparent
                            int ci = is8 ? idx : palNo * 16 + idx;
                            if (ci >= palCount) ci = idx;
                            if (ci >= colors.Length) continue;
                            var (r, g, b) = colors[ci];
                            int dst = ((ty * 8 + py) * w + (tx * 8 + px)) * 4;
                            rgba[dst] = r; rgba[dst + 1] = g; rgba[dst + 2] = b; rgba[dst + 3] = 255;
                        }
                }
            return new BgImage { Rgba = rgba, Width = w, Height = h };
        }
    }
}
