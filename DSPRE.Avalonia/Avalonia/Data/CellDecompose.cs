using System;
using System.Collections.Generic;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// Taking a painted picture of an assembled sprite apart again, back into the tiles it is drawn from.
    /// </summary>
    public static class CellDecompose
    {
        /// <summary>Which piece is visible at each pixel, or -1 where nothing is.</summary>
        private static int[] Ownership(NcerFile file, int cell, byte[] ncgr, ushort[] colours,
                                       int canvasW, int canvasH, int cropLeft, int cropTop,
                                       int width, int height)
        {
            NcerFile.Cell c = file.Cells[cell];
            int[] owner = new int[width * height];
            for (int i = 0; i < owner.Length; i++) owner[i] = -1;
            bool[] covered = new bool[width * height];   // owned only by being under a piece, not seen in it

            List<int> order = new List<int>(CellRenderer.DrawOrder(c));
            foreach (int i in order)
            {
                NcerFile.Piece p = c.Pieces[i];
                if (p.Width == 0 || p.Height == 0) continue;

                // One piece on its own, drawn the same way the preview draws it.
                RawImage one = CellRenderer.Draw(file, cell, ncgr, colours, canvasW, canvasH, true, new[] { i });

                // Read it back in the picture's own coordinates, which is the canvas with the blank
                // border trimmed off.
                for (int y = 0; y < height; y++)
                {
                    int cy = cropTop + y;
                    if (cy < 0 || cy >= canvasH) continue;
                    for (int x = 0; x < width; x++)
                    {
                        int cx = cropLeft + x;
                        if (cx < 0 || cx >= canvasW) continue;
                        if (one.Bgra[(cy * canvasW + cx) * 4 + 3] != 0)
                            owner[y * width + x] = i;   // pieces laid down later win, as on screen
                    }
                }
            }

            // A see-through pixel still belongs to the piece covering it, or blank parts of a sprite could
            // never be painted in. The piece on top takes it, as for visible pixels.
            foreach (int i in order)
            {
                NcerFile.Piece p = c.Pieces[i];
                if (p.Width == 0 || p.Height == 0) continue;
                int left = canvasW / 2 + p.X - cropLeft;
                int top = canvasH / 2 + p.Y - cropTop;
                for (int y = Math.Max(0, top); y < Math.Min(height, top + p.Height); y++)
                    for (int x = Math.Max(0, left); x < Math.Min(width, left + p.Width); x++)
                    {
                        int at = y * width + x;
                        if (owner[at] >= 0 && !covered[at]) continue;
                        owner[at] = i;
                        covered[at] = true;
                    }
            }
            return owner;
        }

        /// <summary>Puts a painted picture back into the drawing it was assembled from.</summary>
        /// <param name="changed">The whole drawing file with the painted pixels written in.</param>
        /// <param name="canvas">How wide and tall the pieces were laid out on, before the blank border
        /// around the picture was trimmed off. The pieces sit relative to the middle of that, not the
        /// middle of what is shown.</param>
        /// <param name="cropLeft">Where the shown picture starts inside that canvas.</param>
        public static string PutBack(NcerFile file, int cell, byte[] ncgr, ushort[] colours,
                                     byte[] painted, int width, int height,
                                     out byte[] changed, int canvas = 0, int cropLeft = 0, int cropTop = 0)
        {
            changed = null;
            if (file == null || cell < 0 || cell >= file.Cells.Count || file.Cells[cell].Pieces.Count == 0)
                return "This entry has no pieces to put back.";
            if (painted == null || painted.Length < width * height * 4)
                return "That picture isn't the size this sprite is drawn at.";
            if (!(NitroBgCodec.ReadDrawingHeader(ncgr) is NitroBgCodec.DrawingHeader head))
                return "This drawing couldn't be taken apart, so nothing was changed.";
            if (canvas <= 0) canvas = Math.Max(width, height);

            NcerFile.Cell c = file.Cells[cell];
            int[] owner = Ownership(file, cell, ncgr, colours, canvas, canvas, cropLeft, cropTop, width, height);
            byte[] outp = (byte[])ncgr.Clone();
            bool fourBit = !head.EightBit;
            int rowSize = fourBit ? 16 : 256;
            int rows = Math.Max(1, colours.Length / rowSize);
            int dataEnd = head.DataAt + head.DataSize;

            for (int i = 0; i < c.Pieces.Count; i++)
            {
                NcerFile.Piece p = c.Pieces[i];
                if (p.Width == 0 || p.Height == 0) continue;

                // The same row of colours the preview drew this piece in.
                int row = p.Palette < rows ? p.Palette : 0;
                int from = Math.Min(colours.Length, row * rowSize);
                (byte r, byte g, byte b)[] own = new (byte, byte, byte)[Math.Min(rowSize, colours.Length - from)];
                for (int k = 0; k < own.Length; k++) own[k] = NitroBgCodec.Colour(colours[from + k]);

                int startByte = head.DataAt + file.CharacterOf(c, p) * 32;

                // The pieces are placed against the middle of the canvas they were drawn on, then the
                // blank border was trimmed away, so both have to be taken off again.
                int left = canvas / 2 + p.X - cropLeft;
                int top = canvas / 2 + p.Y - cropTop;

                for (int y = 0; y < p.Height; y++)
                {
                    for (int x = 0; x < p.Width; x++)
                    {
                        // Where this pixel of the piece landed on screen, after the flip.
                        int sx = p.FlipH ? p.Width - 1 - x : x;
                        int sy = p.FlipV ? p.Height - 1 - y : y;
                        int px = left + sx, py = top + sy;
                        if (px < 0 || px >= width || py < 0 || py >= height) continue;
                        if (owner[py * width + px] != i) continue;   // something else is on top here

                        int at = (py * width + px) * 4;

                        // Keep the number that is already there when it is already the right colour.
                        // A number past the colours is not drawn either, so a see-through pixel keeps it.
                        int already = GetPixel(outp, startByte, dataEnd, p.Width, x, y, fourBit);
                        if (already >= own.Length && painted[at + 3] == 0) continue;
                        if (already >= 0 && already < own.Length
                            && Matches(own, already, painted[at], painted[at + 1], painted[at + 2], painted[at + 3]))
                            continue;

                        int index = NearestColour(own, painted[at], painted[at + 1], painted[at + 2], painted[at + 3]);
                        if (index < 0) continue;
                        PutPixel(outp, startByte, dataEnd, p.Width, x, y, index, fourBit);
                    }
                }
            }

            // Writing nothing is right for an unchanged picture, so it isn't a failure.
            changed = outp;
            return null;
        }

        /// <summary>Where one pixel of a piece sits among the drawing's pixels.</summary>
        private static int PixelAt(int startByte, int pieceWidth, int x, int y, bool fourBit, out bool high)
        {
            int tile = (y / 8) * (pieceWidth / 8) + (x / 8);
            int pixel = tile * 64 + (y % 8) * 8 + (x % 8);
            high = fourBit && (pixel & 1) != 0;
            return startByte + (fourBit ? pixel / 2 : pixel);
        }

        private static void PutPixel(byte[] ncgr, int startByte, int dataEnd, int pieceWidth, int x, int y,
                                     int index, bool fourBit)
        {
            int at = PixelAt(startByte, pieceWidth, x, y, fourBit, out bool high);
            if (at < 0 || at >= dataEnd) return;
            if (!fourBit) ncgr[at] = (byte)index;
            else if (high) ncgr[at] = (byte)((ncgr[at] & 0x0F) | ((index & 0x0F) << 4));
            else ncgr[at] = (byte)((ncgr[at] & 0xF0) | (index & 0x0F));
        }

        /// <summary>The number already at one pixel of a piece, or -1 when it is off the end.</summary>
        private static int GetPixel(byte[] ncgr, int startByte, int dataEnd, int pieceWidth, int x, int y, bool fourBit)
        {
            int at = PixelAt(startByte, pieceWidth, x, y, fourBit, out bool high);
            if (at < 0 || at >= dataEnd) return -1;
            if (!fourBit) return ncgr[at];
            return high ? ncgr[at] >> 4 : ncgr[at] & 0x0F;
        }

        /// <summary>Whether the colour already in the drawing is the one that was painted.</summary>
        private static bool Matches((byte r, byte g, byte b)[] colours, int have, byte r, byte g, byte b, byte a)
        {
            // Number 0 is see-through whatever colour it holds.
            if (a == 0) return have == 0;
            if (have == 0) return false;
            return colours[have].r == r && colours[have].g == g && colours[have].b == b;
        }

        /// <summary>Which of a sprite's own colours a painted pixel is.</summary>
        private static int NearestColour((byte r, byte g, byte b)[] colours, byte r, byte g, byte b, byte a)
        {
            if (colours.Length == 0) return -1;
            if (a == 0) return 0;

            int best = -1, bestOff = int.MaxValue;
            for (int i = 1; i < colours.Length; i++)
            {
                int dr = colours[i].r - r, dg = colours[i].g - g, db = colours[i].b - b;
                int off = dr * dr + dg * dg + db * db;
                if (off >= bestOff) continue;
                bestOff = off; best = i;
                if (off == 0) break;
            }
            return best < 0 ? 0 : best;
        }
    }
}
