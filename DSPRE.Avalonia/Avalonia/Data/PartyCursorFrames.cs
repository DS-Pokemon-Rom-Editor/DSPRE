using System;
using System.Collections.Generic;
using System.Linq;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// Rebuilds the party cursor frames at a new panel size: corners kept, edges and middle repeated, cut into hardware sprite sizes. Cell
    /// order stays the same, so the animation file doesn't change.
    /// </summary>
    public static class PartyCursorFrames
    {
        // The sprite sizes the hardware has, as (shape, size) attribute values.
        private static readonly Dictionary<(int W, int H), (int Shape, int Size)> Shapes = new()
        {
            [(8, 8)] = (0, 0), [(16, 16)] = (0, 1), [(32, 32)] = (0, 2), [(64, 64)] = (0, 3),
            [(16, 8)] = (1, 0), [(32, 8)] = (1, 1), [(32, 16)] = (1, 2), [(64, 32)] = (1, 3),
            [(8, 16)] = (2, 0), [(8, 32)] = (2, 1), [(16, 32)] = (2, 2), [(32, 64)] = (2, 3),
        };

        // Corners this many pixels across are kept as drawn.
        private const int Corner = 16;

        /// <summary>Null, with <paramref name="why"/>, when the files can't be rebuilt.</summary>
        public static (byte[] Cells, byte[] Tiles) Build(byte[] ncer, byte[] ncgr, int width, int height, out string why)
        {
            why = null;
            NcerFile cells = NcerFile.Read(ncer);
            byte[] chars = NitroBgCodec.ReadCharacters(ncgr);
            if (cells == null || chars == null || cells.Cells.Count == 0) { why = "The cursor's files couldn't be read."; return (null, null); }

            int w = width * 8, h = height * 8;
            List<NcerFile.NewCell> built = new List<NcerFile.NewCell>();
            List<byte> tiles = new List<byte>();
            foreach (NcerFile.Cell cell in cells.Cells)
            {
                (byte[] old, int ow, int oh) = Render(cells, cell, chars);
                if (ow < 2 * Corner || oh < 2 * Corner) { why = "The cursor's frames are too small to stretch."; return (null, null); }
                byte[] stretched = Stretch(old, ow, oh, w, h);
                NcerFile.Piece first = cell.Pieces.FirstOrDefault();
                int palette = first?.Palette ?? 0, priority = first == null ? 0 : (first.A2 >> 10) & 3;
                List<(ushort, ushort, ushort)> pieces = new List<(ushort, ushort, ushort)>();
                foreach ((int px, int py, int pw, int ph) in Cut(0, 0, w, h))
                {
                    int tile = tiles.Count / 32;
                    for (int ty = 0; ty < ph / 8; ty++)
                        for (int tx = 0; tx < pw / 8; tx++)
                            tiles.AddRange(Tile(stretched, w, px + tx * 8, py + ty * 8));
                    (int shape, int size) = Shapes[(pw, ph)];
                    int x = px - w / 2, y = py - h / 2;
                    pieces.Add(((ushort)((y & 0xFF) | shape << 14), (ushort)((x & 0x1FF) | size << 14),
                                (ushort)(tile | priority << 10 | palette << 12)));
                }
                // The low six bits are the frame's reach from its centre in 4-pixel steps.
                int reach = (int)(Math.Sqrt(w * w / 4.0 + h * h / 4.0) / 4);
                built.Add(new NcerFile.NewCell(pieces, (ushort)((cell.Attr & ~0x3F) | Math.Min(reach, 0x3F)),
                                               (short)(w / 2 - 1), (short)(h / 2 - 1), (short)(-w / 2), (short)(-h / 2)));
            }
            if (tiles.Count / 32 > 1024) { why = "Frames this big need more tiles than a sprite can number."; return (null, null); }
            byte[] newCells = cells.WithCells(built);
            byte[] newTiles = NitroBgWrite.ReplaceTiles(ncgr, tiles.ToArray());
            if (newCells == null || newTiles == null) { why = "DSPRE can't rebuild this kind of cursor file."; return (null, null); }
            return (newCells, newTiles);
        }

        /// <summary>The first frame's size in pixels, or null when the file can't be read.</summary>
        public static (int Width, int Height)? FrameSize(byte[] ncer)
        {
            NcerFile cells = NcerFile.Read(ncer);
            NcerFile.Cell cell = cells?.Cells.FirstOrDefault();
            if (cell == null || cell.Pieces.Count == 0) return null;
            return (cell.Pieces.Max(p => p.X + p.Width) - cell.Pieces.Min(p => p.X),
                    cell.Pieces.Max(p => p.Y + p.Height) - cell.Pieces.Min(p => p.Y));
        }

        private static (byte[] Pixels, int Width, int Height) Render(NcerFile file, NcerFile.Cell cell, byte[] chars)
        {
            int minX = cell.Pieces.Min(p => p.X), minY = cell.Pieces.Min(p => p.Y);
            int w = cell.Pieces.Max(p => p.X + p.Width) - minX, h = cell.Pieces.Max(p => p.Y + p.Height) - minY;
            byte[] pixels = new byte[w * h];
            foreach (NcerFile.Piece p in cell.Pieces)
            {
                int first = file.CharacterOf(cell, p);
                for (int y = 0; y < p.Height; y++)
                    for (int x = 0; x < p.Width; x++)
                    {
                        int sx = p.FlipH ? p.Width - 1 - x : x, sy = p.FlipV ? p.Height - 1 - y : y;
                        int at = (first + (sy / 8) * (p.Width / 8) + sx / 8) * 32 + ((sy % 8) * 8 + sx % 8) / 2;
                        if (at >= chars.Length) continue;
                        int value = (sx & 1) != 0 ? chars[at] >> 4 : chars[at] & 0xF;
                        pixels[(p.Y - minY + y) * w + p.X - minX + x] = (byte)value;
                    }
            }
            return (pixels, w, h);
        }

        private static byte[] Stretch(byte[] old, int ow, int oh, int w, int h)
        {
            byte[] result = new byte[w * h];
            int cx = Math.Min(Corner, w / 2), cy = Math.Min(Corner, h / 2);
            int From(int at, int size, int oldSize, int corner)
            {
                if (at < corner) return at;
                if (at >= size - corner) return oldSize - (size - at);
                return Corner + (at - corner) % (oldSize - 2 * Corner);
            }
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    result[y * w + x] = old[From(y, h, oh, cy) * ow + From(x, w, ow, cx)];
            return result;
        }

        // 4 bits per pixel, low nibble first.
        private static byte[] Tile(byte[] pixels, int w, int x0, int y0)
        {
            byte[] t = new byte[32];
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x += 2)
                    t[y * 4 + x / 2] = (byte)((pixels[(y0 + y) * w + x0 + x] & 0xF) | (pixels[(y0 + y) * w + x0 + x + 1] & 0xF) << 4);
            return t;
        }

        // Largest sizes first along each side; pairs the hardware doesn't have are split in half.
        private static IEnumerable<(int X, int Y, int W, int H)> Cut(int x, int y, int w, int h)
        {
            List<int> Chunks(int length)
            {
                List<int> chunks = new List<int>();
                foreach (int size in new[] { 64, 32, 16, 8 })
                    while (length >= size) { chunks.Add(size); length -= size; }
                return chunks;
            }
            int top = y;
            foreach (int ch in Chunks(h))
            {
                int left = x;
                foreach (int cw in Chunks(w))
                {
                    foreach ((int, int, int, int) piece in Legal(left, top, cw, ch)) yield return piece;
                    left += cw;
                }
                top += ch;
            }
        }

        private static IEnumerable<(int X, int Y, int W, int H)> Legal(int x, int y, int w, int h)
        {
            if (Shapes.ContainsKey((w, h))) { yield return (x, y, w, h); yield break; }
            if (w >= h)
            {
                foreach ((int, int, int, int) p in Legal(x, y, w / 2, h)) yield return p;
                foreach ((int, int, int, int) p in Legal(x + w / 2, y, w / 2, h)) yield return p;
            }
            else
            {
                foreach ((int, int, int, int) p in Legal(x, y, w, h / 2)) yield return p;
                foreach ((int, int, int, int) p in Legal(x, y + h / 2, w, h / 2)) yield return p;
            }
        }
    }
}
