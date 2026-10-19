using System;
using System.Collections.Generic;
using System.Linq;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// Drawings whose tiles a cell puts in place, as footprints are: the tiles are one run that the cell's pieces take
    /// in turn, so the picture is only right once each piece is set at its own spot. Reads the first cell of an NCER.
    /// </summary>
    internal static class CellPlacement
    {
        public readonly record struct Piece(int X, int Y, int Width, int Height, int FirstTile);

        /// <summary>The first cell's pieces, or null when the file isn't a cell layout this can read.</summary>
        public static List<Piece> Pieces(byte[] ncer, int bitsPerPixel)
        {
            NcerFile file = NcerFile.Read(ncer);
            // A mapping past 3 is 2D mapping, which these drawings never use.
            if (file == null || file.Cells.Count < 1 || file.Mapping > 3) return null;
            NcerFile.Cell first = file.Cells[0];
            List<Piece> pieces = new List<Piece>();
            foreach (NcerFile.Piece p in first.Pieces)
            {
                if (p.Shape > 2) return null;
                // An 8bpp tile is two characters.
                int tile = file.CharacterOf(first, p);
                if (bitsPerPixel == 8) tile /= 2;
                pieces.Add(new Piece(p.X, p.Y, p.Width, p.Height, tile));
            }
            return pieces.Count > 0 ? pieces : null;
        }

        /// <summary>The palette banks the first cell's pieces draw with, or null when the file isn't a cell layout.</summary>
        public static List<int> Banks(byte[] ncer)
        {
            NcerFile file = NcerFile.Read(ncer);
            if (file == null || file.Cells.Count < 1) return null;
            List<int> banks = new List<int>();
            foreach (NcerFile.Piece p in file.Cells[0].Pieces)
                if (!banks.Contains(p.Palette)) banks.Add(p.Palette);
            return banks;
        }

        /// <summary>Lays the run of tiles out as the cell shows it. False when a piece reaches past the run.</summary>
        public static bool Place(byte[] tiles, List<Piece> pieces, out byte[] picture, out int width, out int height)
        {
            int minX = pieces.Min(p => p.X), minY = pieces.Min(p => p.Y);
            width = pieces.Max(p => p.X + p.Width) - minX;
            height = pieces.Max(p => p.Y + p.Height) - minY;
            picture = new byte[width * height];
            foreach ((int tile, int px, int py) in Tiles(pieces, minX, minY))
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
            foreach ((int tile, int px, int py) in Tiles(pieces, minX, minY))
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
            foreach (Piece p in pieces)
                for (int ty = 0; ty < p.Height / 8; ty++)
                    for (int tx = 0; tx < p.Width / 8; tx++)
                        yield return (p.FirstTile + ty * (p.Width / 8) + tx, p.X - minX + tx * 8, p.Y - minY + ty * 8);
        }
    }
}
