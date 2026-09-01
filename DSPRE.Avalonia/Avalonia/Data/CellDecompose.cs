using System;
using System.Collections.Generic;
using Ekona.Images;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// Taking a painted picture of an assembled sprite apart again, back into the tiles it is drawn from.
    /// </summary>
    public static class CellDecompose
    {
        /// <summary>Which piece is visible at each pixel, or -1 where nothing is.</summary>
        private static int[] Ownership(Bank bank, uint blockSize, ImageBase img, PaletteBase pal,
                                       int canvasW, int canvasH, int cropLeft, int cropTop,
                                       int width, int height)
        {
            var owner = new int[width * height];
            for (int i = 0; i < owner.Length; i++) owner[i] = -1;

            for (int i = 0; i < bank.oams.Length; i++)
            {
                var oam = bank.oams[i];
                if (oam.width == 0 || oam.height == 0) continue;

                // One piece on its own, drawn the same way the composer draws it.
                var only = new int[1] { i };
                var one = Actions.Get_RawImage(bank, blockSize, img, pal, canvasW, canvasH, true, -1, 1, only);
                if (one?.Bgra == null) continue;

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
                        int at = (cy * canvasW + cx) * 4;
                        if (at + 3 < one.Bgra.Length && one.Bgra[at + 3] != 0)
                            owner[y * width + x] = i;   // later pieces win, as on screen
                    }
                }
            }
            return owner;
        }

        /// <summary>Puts a painted picture back into the tile sheet it was drawn from.</summary>
        /// <param name="canvas">How wide and tall the pieces were laid out on, before the blank border
        /// around the picture was trimmed off. The pieces sit relative to the middle of that, not the
        /// middle of what is shown.</param>
        /// <param name="cropLeft">Where the shown picture starts inside that canvas.</param>
        public static string PutBack(Bank bank, uint blockSize, ImageBase img, PaletteBase pal,
                                     byte[] painted, int width, int height, byte[] tiles,
                                     out byte[] changed, int canvas = 0, int cropLeft = 0, int cropTop = 0)
        {
            changed = null;
            if (bank.oams == null || bank.oams.Length == 0) return "This entry has no pieces to put back.";
            if (painted == null || painted.Length < width * height * 4)
                return "That picture is not the size this sprite is drawn at.";
            if (canvas <= 0) canvas = Math.Max(width, height);

            var owner = Ownership(bank, blockSize, img, pal, canvas, canvas, cropLeft, cropTop,
                                  width, height);
            var outp = (byte[])tiles.Clone();
            bool fourBit = img.FormatColor == ColorFormat.colors16;
            int wrote = 0;   // kept for the log below

            for (int i = 0; i < bank.oams.Length; i++)
            {
                var oam = bank.oams[i];
                if (oam.width == 0 || oam.height == 0) continue;

                int num_pal = oam.obj2.index_palette;
                if (num_pal >= pal.NumberOfPalettes) num_pal = 0;
                var colours = pal.Palette[num_pal];

                uint tileOffset = (uint)(oam.obj2.tileOffset << (byte)blockSize);
                int startByte = (int)(tileOffset * 0x20 + bank.data_offset);

                // The pieces are placed against the middle of the canvas they were drawn on, then the
                // blank border was trimmed away, so both have to be taken off again.
                int left = canvas / 2 + oam.obj1.xOffset - cropLeft;
                int top = canvas / 2 + oam.obj0.yOffset - cropTop;
                bool flipX = oam.obj1.flipX == 1, flipY = oam.obj1.flipY == 1;

                for (int y = 0; y < oam.height; y++)
                {
                    for (int x = 0; x < oam.width; x++)
                    {
                        // Where this pixel of the piece landed on screen, after the flip.
                        int sx = flipX ? oam.width - 1 - x : x;
                        int sy = flipY ? oam.height - 1 - y : y;
                        int px = left + sx, py = top + sy;
                        if (px < 0 || px >= width || py < 0 || py >= height) continue;
                        if (owner[py * width + px] != i) continue;   // something else is on top here

                        int at = (py * width + px) * 4;

                        // Keep the number that is already there when it is already the right colour.
                        int already = GetPixel(outp, startByte, oam.width, x, y, fourBit);
                        if (already >= 0 && already < colours.Length
                            && Matches(colours[already], painted[at], painted[at + 1], painted[at + 2],
                                       painted[at + 3], colours))
                            continue;

                        int index = NearestColour(colours, painted[at], painted[at + 1], painted[at + 2],
                                                  painted[at + 3]);
                        if (index < 0) continue;

                        if (!PutPixel(outp, startByte, oam.width, x, y, index, fourBit)) continue;
                        wrote++;
                    }
                }
            }

            // Nothing written means nothing needed writing, which is what putting an unchanged picture
            // back should do, so that is not a failure.
            changed = outp;
            return null;
        }

        /// <summary>Where one pixel of a piece sits in the tile sheet.</summary>
        private static bool PutPixel(byte[] tiles, int startByte, int pieceWidth, int x, int y,
                                     int index, bool fourBit)
        {
            int across = pieceWidth / 8;
            int tile = (y / 8) * across + (x / 8);
            int inTile = (y % 8) * 8 + (x % 8);
            int pixel = tile * 64 + inTile;

            if (fourBit)
            {
                int at = startByte + pixel / 2;
                if (at < 0 || at >= tiles.Length) return false;
                if ((pixel & 1) == 0) tiles[at] = (byte)((tiles[at] & 0xF0) | (index & 0x0F));
                else tiles[at] = (byte)((tiles[at] & 0x0F) | ((index & 0x0F) << 4));
            }
            else
            {
                int at = startByte + pixel;
                if (at < 0 || at >= tiles.Length) return false;
                tiles[at] = (byte)index;
            }
            return true;
        }

        /// <summary>Whether a colour already in the drawing is the one that was painted. </summary>
        private static bool Matches(System.Drawing.Color have, byte r, byte g, byte b, byte a,
                                    System.Drawing.Color[] colours)
        {
            bool haveIsClear = colours.Length > 0 && have.R == colours[0].R && have.G == colours[0].G
                                                  && have.B == colours[0].B;
            if (a == 0) return haveIsClear;
            if (haveIsClear) return false;
            return have.R == r && have.G == g && have.B == b;
        }

        /// <summary>The number already at one pixel of a piece, or -1 when it is off the end.</summary>
        private static int GetPixel(byte[] tiles, int startByte, int pieceWidth, int x, int y, bool fourBit)
        {
            int across = pieceWidth / 8;
            int tile = (y / 8) * across + (x / 8);
            int inTile = (y % 8) * 8 + (x % 8);
            int pixel = tile * 64 + inTile;

            if (fourBit)
            {
                int at = startByte + pixel / 2;
                if (at < 0 || at >= tiles.Length) return -1;
                return (pixel & 1) == 0 ? tiles[at] & 0x0F : (tiles[at] >> 4) & 0x0F;
            }
            int flat = startByte + pixel;
            if (flat < 0 || flat >= tiles.Length) return -1;
            return tiles[flat];
        }

        /// <summary>Which of a sprite's own colours a painted pixel is. </summary>
        private static int NearestColour(System.Drawing.Color[] colours, byte r, byte g, byte b, byte a)
        {
            if (colours == null || colours.Length == 0) return -1;
            if (a == 0) return 0;

            int best = -1, bestOff = int.MaxValue;
            for (int i = 0; i < colours.Length; i++)
            {
                int dr = colours[i].R - r, dg = colours[i].G - g, db = colours[i].B - b;
                int off = dr * dr + dg * dg + db * db;
                if (off >= bestOff) continue;
                bestOff = off; best = i;
                if (off == 0) break;
            }
            return best;
        }

        /// <summary>How big a picture of this thing is, so a caller can check one before putting it back.</summary>
        public static (int Width, int Height) CanvasFor(int width, int height) => (width, height);
    }
}
