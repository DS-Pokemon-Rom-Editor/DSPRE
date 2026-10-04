using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// Drawings whose tiles a cell puts in place, as footprints are: the tiles are one run that the cell's pieces take
    /// in turn, so the picture is only right once each piece is set at its own spot. Reads the first cell of an NCER.
    /// </summary>
    internal static class CellPlacement
    {
        public readonly record struct Piece(int X, int Y, int Width, int Height, int FirstTile);

        // OAM shape (square, wide, tall) by size, in pixels.
        private static readonly (int W, int H)[,] Sizes =
        {
            { (8, 8), (16, 16), (32, 32), (64, 64) },
            { (16, 8), (32, 8), (32, 16), (64, 32) },
            { (8, 16), (8, 32), (16, 32), (32, 64) },
        };

        /// <summary>The first cell's pieces, or null when the file isn't a cell layout this can read.</summary>
        public static List<Piece> Pieces(byte[] ncer, int bitsPerPixel)
        {
            if (ncer == null || ncer.Length < 0x20 || Encoding.ASCII.GetString(ncer, 0, 4) != "RECN") return null;
            int kbec = BitConverter.ToUInt16(ncer, 0x0C);
            if (kbec + 0x20 > ncer.Length || Encoding.ASCII.GetString(ncer, kbec, 4) != "KBEC") return null;
            int cellCount = BitConverter.ToUInt16(ncer, kbec + 8);
            bool extended = (BitConverter.ToUInt16(ncer, kbec + 10) & 1) != 0;
            int cells = kbec + 8 + (int)BitConverter.ToUInt32(ncer, kbec + 12);
            int mapping = (int)BitConverter.ToUInt32(ncer, kbec + 16);
            if (cellCount < 1 || mapping > 3 || cells + 8 > ncer.Length) return null;   // 4 is 2D mapping

            int oams = cells + cellCount * (extended ? 16 : 8);
            int count = BitConverter.ToUInt16(ncer, cells);
            int at = oams + (int)BitConverter.ToUInt32(ncer, cells + 4);
            if (at + count * 6 > ncer.Length) return null;

            var pieces = new List<Piece>();
            for (int i = 0; i < count; i++, at += 6)
            {
                int a0 = BitConverter.ToUInt16(ncer, at), a1 = BitConverter.ToUInt16(ncer, at + 2), a2 = BitConverter.ToUInt16(ncer, at + 4);
                int shape = a0 >> 14, size = a1 >> 14;
                if (shape > 2) return null;
                int x = a1 & 0x1FF;
                if (x >= 256) x -= 512;
                // 1D mapping counts characters in 32-byte steps times 2^mapping; an 8bpp tile is two of them.
                int tile = (a2 & 0x3FF) << mapping;
                if (bitsPerPixel == 8) tile /= 2;
                var (w, h) = Sizes[shape, size];
                pieces.Add(new Piece(x, (sbyte)(a0 & 0xFF), w, h, tile));
            }
            return pieces.Count > 0 ? pieces : null;
        }

        /// <summary>Lays the run of tiles out as the cell shows it. False when a piece reaches past the run.</summary>
        public static bool Place(byte[] tiles, List<Piece> pieces, out byte[] picture, out int width, out int height)
        {
            int minX = pieces.Min(p => p.X), minY = pieces.Min(p => p.Y);
            width = pieces.Max(p => p.X + p.Width) - minX;
            height = pieces.Max(p => p.Y + p.Height) - minY;
            picture = new byte[width * height];
            foreach (var (tile, px, py) in Tiles(pieces, minX, minY))
            {
                if ((tile + 1) * 64 > tiles.Length) return false;
                for (int y = 0; y < 8; y++)
                    Array.Copy(tiles, tile * 64 + y * 8, picture, (py + y) * width + px, 8);
            }
            return true;
        }

        /// <summary>Puts a picture laid out by the cell back into the run, keeping tiles no piece uses.</summary>
        public static bool Unplace(byte[] picture, int width, List<Piece> pieces, byte[] tiles)
        {
            int minX = pieces.Min(p => p.X), minY = pieces.Min(p => p.Y);
            foreach (var (tile, px, py) in Tiles(pieces, minX, minY))
            {
                if ((tile + 1) * 64 > tiles.Length) return false;
                for (int y = 0; y < 8; y++)
                    Array.Copy(picture, (py + y) * width + px, tiles, tile * 64 + y * 8, 8);
            }
            return true;
        }

        // Each 8x8 tile of each piece, in 1D order across the piece, with where it lands in the picture.
        private static IEnumerable<(int Tile, int X, int Y)> Tiles(List<Piece> pieces, int minX, int minY)
        {
            foreach (var p in pieces)
                for (int ty = 0; ty < p.Height / 8; ty++)
                    for (int tx = 0; tx < p.Width / 8; tx++)
                        yield return (p.FirstTile + ty * (p.Width / 8) + tx, p.X - minX + tx * 8, p.Y - minY + ty * 8);
        }
    }
}
