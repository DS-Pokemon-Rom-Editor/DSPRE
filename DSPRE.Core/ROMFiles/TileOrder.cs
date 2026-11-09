using System;

namespace DSPRE.ROMFiles
{
    /// <summary>Nitro pictures stored as 8x8 tiles one after another, and the same picture row by row.</summary>
    public static class TileOrder
    {
        /// <param name="unit">Bytes in one row of a tile: 8 for one byte per pixel, 4 for packed 16 colour data.</param>
        /// <param name="length">Size of the result.</param>
        public static byte[] ToRows(byte[] tiled, int width, int height, int unit, int length)
        {
            byte[] rows = new byte[length];
            for (int i = 0; i < tiled.Length; i++)
            {
                int at = RowIndex(i, width, height, unit);
                if (at >= 0 && at < length) rows[at] = tiled[i];
            }
            return rows;
        }

        /// <summary>The inverse of <see cref="ToRows"/>.</summary>
        public static byte[] ToTiles(byte[] rows, int width, int height, int unit, int length)
        {
            byte[] tiled = new byte[length];
            for (int i = 0; i < length; i++)
            {
                int at = RowIndex(i, width, height, unit);
                if (at >= 0 && at < rows.Length) tiled[i] = rows[at];
            }
            return tiled;
        }

        // Where byte i of the tiled data sits in the row by row picture, or -1 past its edges.
        private static int RowIndex(int i, int width, int height, int unit)
        {
            int tileBytes = 8 * unit;
            int rowBytes = width * unit / 8;
            int across = Math.Max(1, width / 8);
            int tile = i / tileBytes, inTile = i % tileBytes;
            int column = tile % across * unit + inTile % unit;
            int row = tile / across * 8 + inTile / unit;
            return column < rowBytes && row < height ? row * rowBytes + column : -1;
        }
    }
}
