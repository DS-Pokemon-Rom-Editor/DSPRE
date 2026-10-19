using System;
using System.Collections.Generic;
using System.Linq;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// Draws one cell of a cell file from a drawing and a palette, the way the DS shows it: the cell's origin at the
    /// middle of the picture, each piece at its offset from there, colour 0 see-through, and pieces laid so that a
    /// lower priority value, then a lower piece number, ends up on top.
    /// </summary>
    public static class CellRenderer
    {
        /// <param name="only">The piece numbers to draw, or null for all of them.</param>
        public static RawImage Draw(NcerFile file, int cell, byte[] ncgr, ushort[] colours, int width, int height,
                                    bool transparent = true, ICollection<int> only = null)
        {
            RawImage image = new RawImage(width, height);
            if (file == null || cell < 0 || cell >= file.Cells.Count || ncgr == null || colours == null) return image;
            if (!(NitroBgCodec.ReadDrawingHeader(ncgr) is NitroBgCodec.DrawingHeader head)) return image;

            NcerFile.Cell c = file.Cells[cell];
            bool eightBit = head.EightBit;
            int rowSize = eightBit ? 256 : 16;
            int rows = Math.Max(1, colours.Length / rowSize);

            foreach (int i in DrawOrder(c))
            {
                if (only != null && !only.Contains(i)) continue;
                NcerFile.Piece p = c.Pieces[i];
                if (p.Width == 0 || p.Height == 0) continue;
                int row = p.Palette < rows ? p.Palette : 0;
                int start = head.DataAt + file.CharacterOf(c, p) * 32;
                int tileBytes = eightBit ? 64 : 32;
                int tilesWide = p.Width / 8;
                int left = width / 2 + p.X, top = height / 2 + p.Y;

                for (int y = 0; y < p.Height; y++)
                    for (int x = 0; x < p.Width; x++)
                    {
                        int sx = p.FlipH ? p.Width - 1 - x : x, sy = p.FlipV ? p.Height - 1 - y : y;
                        int tile = (sy / 8) * tilesWide + sx / 8, inTile = (sy % 8) * 8 + sx % 8;
                        int at = start + tile * tileBytes + (eightBit ? inTile : inTile / 2);
                        if (at < head.DataAt || at >= head.DataAt + head.DataSize) continue;
                        int index = eightBit ? ncgr[at] : (inTile & 1) != 0 ? ncgr[at] >> 4 : ncgr[at] & 0xF;
                        if (transparent && index == 0) continue;
                        int ci = row * rowSize + index;
                        if (ci >= colours.Length) continue;
                        int dx = left + x, dy = top + y;
                        if (dx < 0 || dy < 0 || dx >= width || dy >= height) continue;
                        (byte r, byte g, byte b) = NitroBgCodec.Colour(colours[ci]);
                        int o = (dy * width + dx) * 4;
                        image.Bgra[o] = b; image.Bgra[o + 1] = g; image.Bgra[o + 2] = r; image.Bgra[o + 3] = 255;
                    }
            }
            return image;
        }

        /// <summary>The piece numbers in the order they are laid down, the last one ending up on top.</summary>
        public static IEnumerable<int> DrawOrder(NcerFile.Cell cell)
            => Enumerable.Range(0, cell.Pieces.Count)
                .OrderByDescending(i => (cell.Pieces[i].A2 >> 10) & 3).ThenByDescending(i => i);
    }
}
