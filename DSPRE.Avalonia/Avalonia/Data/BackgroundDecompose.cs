using System;
using System.Collections.Generic;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// Taking a painted background apart again, back into the tiles it is drawn from.
    ///
    /// A background is three files: a sheet of eight by eight tiles, an arrangement saying which tile goes
    /// in each square of the screen and whether it is flipped, and the colours. Painting the tile sheet
    /// means working on a jumble that looks nothing like the picture, so the browser shows the three put
    /// together and this is the way back from that.
    ///
    /// The rule followed is the exact inverse of NitroBgCodec.Composite, which is what draws them, down to
    /// how the arrangement is stored: the DS keeps it in blocks of thirty two squares by thirty two, not
    /// as one long grid, so a screen wider than that is several blocks side by side.
    ///
    /// One thing cannot be got around. Backgrounds reuse tiles: the same eight by eight square is drawn in
    /// many places, which is how they fit. Painting one place therefore changes every place that shares
    /// it. That is how the format works rather than a fault, so what happens here is that the sharing is
    /// counted and reported, and the caller can say so before anything is written.
    /// </summary>
    public static class BackgroundDecompose
    {
        public sealed class Result
        {
            /// <summary>The tile sheet with the painting in it.</summary>
            public byte[] Tiles;
            /// <summary>How many squares of the screen were actually changed.</summary>
            public int SquaresChanged;
            /// <summary>How many of those share their tile with a square somewhere else, which will have
            /// changed to match whether that was wanted or not.</summary>
            public int SquaresSharingATile;
            /// <summary>Pixels two squares sharing a tile asked to be different colours. The tile can
            /// only be one thing, so the last square painted wins and the other comes out wrong.</summary>
            public int PixelsPaintedTwoWays;

            /// <summary>Why it could not be done, when it could not.</summary>
            public string Whynot;
        }

        private static int U16(byte[] d, int o) => d[o] | (d[o + 1] << 8);

        /// <summary>
        /// Reads a painted picture back into the tile sheet the background is drawn from.
        ///
        /// The picture has to be the size the background is drawn at, and every colour in it has to be one
        /// the background's own colours already hold, because the tiles keep numbers pointing at those.
        /// </summary>
        public static Result PutBack(byte[] chr, byte[] pal, byte[] scr, byte[] painted,
                                     int width, int height)
        {
            var it = new Result();
            if (chr == null || pal == null || scr == null || painted == null)
            { it.Whynot = "This background is missing one of its three pieces."; return it; }

            var colours = NitroBgCodec.ReadPalette(pal, out int palCount);
            if (colours == null || colours.Length == 0)
            { it.Whynot = "This background's colours could not be read."; return it; }

            int rahc = NitroBgCodec.Find(chr, "RAHC", 0);
            bool is8 = rahc >= 0 && NitroBgCodec.U32(chr, rahc + 0x0C) == 4;
            int tileBytes = rahc >= 0 ? rahc + 0x20 : 0x30;

            int nrcs = NitroBgCodec.Find(scr, "NRCS", 0);
            int w = nrcs >= 0 ? U16(scr, nrcs + 0x08) : 256;
            int h = nrcs >= 0 ? U16(scr, nrcs + 0x0A) : 256;
            if (w <= 0 || w > 1024) w = 256;
            if (h <= 0 || h > 1024) h = 256;
            int mapData = nrcs >= 0 ? nrcs + 0x14 : 0x24;

            if (width != w || height != h)
            {
                it.Whynot = $"This background is {w} by {h} and that picture is {width} by {height}. "
                          + "Save this one first and paint over what comes out.";
                return it;
            }
            if (painted.Length < w * h * 4)
            { it.Whynot = "That picture is smaller than it says it is."; return it; }

            // How many squares of the screen use each tile, so sharing can be reported rather than
            // discovered afterwards.
            int cols = w / 8, rows = h / 8, blocksX = (cols + 31) / 32;
            var usedBy = new Dictionary<int, int>();
            for (int ty = 0; ty < rows; ty++)
                for (int tx = 0; tx < cols; tx++)
                {
                    int e = EntryAt(scr, mapData, blocksX, tx, ty);
                    if (e < 0) continue;
                    int tile = e & 0x3FF;
                    usedBy[tile] = usedBy.TryGetValue(tile, out int n) ? n + 1 : 1;
                }

            // Read from the sheet as it was and write into a copy.
            //
            // Backgrounds share tiles, so one tile is reached from several squares. Comparing against the
            // copy as it is being written means a square nobody painted sees the change another square
            // just made, decides it is wrong, and puts the original back: the paint would be undone by
            // its own neighbours. Comparing against the original means a square nobody painted writes
            // nothing at all.
            var outp = (byte[])chr.Clone();
            var was = chr;
            var writtenTo = new Dictionary<int, int>();   // pixel in the sheet -> what was put there
            int fought = 0;

            for (int ty = 0; ty < rows; ty++)
            {
                for (int tx = 0; tx < cols; tx++)
                {
                    int e = EntryAt(scr, mapData, blocksX, tx, ty);
                    if (e < 0) continue;
                    int tile = e & 0x3FF, palNo = (e >> 12) & 0xF;
                    bool flipH = ((e >> 10) & 1) != 0, flipV = ((e >> 11) & 1) != 0;

                    bool touched = false;
                    for (int py = 0; py < 8; py++)
                    {
                        for (int px = 0; px < 8; px++)
                        {
                            // Where this pixel of the tile ended up on screen, after the flip.
                            int sx = flipH ? 7 - px : px, sy = flipV ? 7 - py : py;
                            int at = (((ty * 8 + py) * w) + (tx * 8 + px)) * 4;
                            if (at + 3 >= painted.Length) continue;

                            int already = GetPixel(was, tileBytes, is8, tile, sx, sy);
                            if (already < 0) continue;

                            // Colour zero is the see-through one, and the drawing keeps it that way, so a
                            // pixel painted away goes back to zero and one left alone keeps its number.
                            if (painted[at + 3] == 0)
                            {
                                if (already == 0) continue;
                                if (Write(outp, writtenTo, ref fought, tileBytes, is8, tile, sx, sy, 0))
                                    touched = true;
                                continue;
                            }

                            int have = ColourOf(colours, palCount, is8, palNo, already);
                            if (have >= 0 && Same(colours[have], painted[at], painted[at + 1], painted[at + 2]))
                                continue;

                            int index = NearestIndex(colours, palCount, is8, palNo,
                                                     painted[at], painted[at + 1], painted[at + 2]);
                            if (index < 0) continue;
                            if (Write(outp, writtenTo, ref fought, tileBytes, is8, tile, sx, sy, index))
                                touched = true;
                        }
                    }

                    if (!touched) continue;
                    it.SquaresChanged++;
                    if (usedBy.TryGetValue(tile, out int shares) && shares > 1) it.SquaresSharingATile++;
                }
            }

            it.Tiles = outp;
            it.PixelsPaintedTwoWays = fought;
            return it;
        }

        /// <summary>
        /// The arrangement entry for one square of the screen.
        ///
        /// The DS stores this in blocks of thirty two squares by thirty two rather than as one grid, so a
        /// screen wider than 256 pixels is several blocks side by side and the square at (32,0) is the
        /// first square of the second block, not the thirty third of one long row.
        /// </summary>
        private static int EntryAt(byte[] scr, int mapData, int blocksX, int tx, int ty)
        {
            int mapIdx = ((ty / 32) * blocksX + (tx / 32)) * 1024 + (ty % 32) * 32 + (tx % 32);
            int mo = mapData + mapIdx * 2;
            return mo + 1 < scr.Length ? U16(scr, mo) : -1;
        }

        /// <summary>Which colour a tile's number points at, taking the square's own bank into account.</summary>
        private static int ColourOf((byte r, byte g, byte b)[] colours, int palCount, bool is8,
                                    int palNo, int index)
        {
            int ci = is8 ? index : palNo * 16 + index;
            if (ci >= palCount) ci = index;
            return ci < colours.Length ? ci : -1;
        }

        private static bool Same((byte r, byte g, byte b) c, byte r, byte g, byte b)
            => c.r == r && c.g == g && c.b == b;

        /// <summary>The number in this square's own bank that holds a colour, or -1 when none does.</summary>
        private static int NearestIndex((byte r, byte g, byte b)[] colours, int palCount, bool is8,
                                        int palNo, byte r, byte g, byte b)
        {
            int count = is8 ? Math.Min(256, colours.Length) : 16;
            int best = -1, bestOff = int.MaxValue;
            for (int index = 0; index < count; index++)
            {
                int ci = ColourOf(colours, palCount, is8, palNo, index);
                if (ci < 0) continue;
                var c = colours[ci];
                int dr = c.r - r, dg = c.g - g, db = c.b - b;
                int off = dr * dr + dg * dg + db * db;
                if (off >= bestOff) continue;
                bestOff = off; best = index;
                if (off == 0) break;
            }
            return best;
        }

        /// <summary>Writes one pixel, noticing when two squares sharing a tile disagree about it.</summary>
        private static bool Write(byte[] chr, Dictionary<int, int> writtenTo, ref int fought,
                                  int tileBytes, bool is8, int tile, int x, int y, int index)
        {
            int where = tile * 64 + y * 8 + x;
            if (writtenTo.TryGetValue(where, out int before) && before != index) fought++;
            if (!PutPixel(chr, tileBytes, is8, tile, x, y, index)) return false;
            writtenTo[where] = index;
            return true;
        }

        private static int GetPixel(byte[] chr, int tileBytes, bool is8, int tile, int x, int y)
        {
            int tbase = tileBytes + tile * (is8 ? 64 : 32);
            if (is8)
            {
                int off = tbase + y * 8 + x;
                return off >= 0 && off < chr.Length ? chr[off] : -1;
            }
            int at = tbase + (y * 8 + x) / 2;
            if (at < 0 || at >= chr.Length) return -1;
            return (x & 1) == 0 ? chr[at] & 0x0F : (chr[at] >> 4) & 0x0F;
        }

        private static bool PutPixel(byte[] chr, int tileBytes, bool is8, int tile, int x, int y, int index)
        {
            int tbase = tileBytes + tile * (is8 ? 64 : 32);
            if (is8)
            {
                int off = tbase + y * 8 + x;
                if (off < 0 || off >= chr.Length) return false;
                chr[off] = (byte)index;
                return true;
            }
            int at = tbase + (y * 8 + x) / 2;
            if (at < 0 || at >= chr.Length) return false;
            if ((x & 1) == 0) chr[at] = (byte)((chr[at] & 0xF0) | (index & 0x0F));
            else chr[at] = (byte)((chr[at] & 0x0F) | ((index & 0x0F) << 4));
            return true;
        }
    }
}
