using System;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// The border the games draw round a message box, read out of the ROM so an edited one shows.
    /// </summary>
    public sealed class FieldWindowFrame
    {
        /// <summary>How many frames the player can pick between.</summary>
        public const int FrameCount = 20;

        /// <summary>The first talk frame's picture, NARC_winframe_talk_win00_ncgr.</summary>
        public const int FirstGraphicEntry = 2;

        public const int TileSize = 8;
        public const int TileCount = 18;

        private readonly byte[] _tiles;         // one index a pixel, tile by tile
        private readonly uint[] _colours;       // 0xAARRGGBB, entry 0 see-through

        /// <summary>The archive entry these colours were read from.</summary>
        public int PaletteEntry { get; }

        private FieldWindowFrame(byte[] tiles, uint[] colours, int paletteEntry)
        { _tiles = tiles; _colours = colours; PaletteEntry = paletteEntry; }

        /// <summary>
        /// Reads one of the twenty frames from the window frame archive's members, or null if it cannot be read.
        /// The caller supplies the members so pending and saved edits show.
        /// </summary>
        public static FieldWindowFrame Load(int frameIndex, Func<int, byte[]> member, int count)
        {
            try
            {
                if (frameIndex < 0 || frameIndex >= FrameCount) frameIndex = 0;
                int firstPalette = FirstPaletteIndex(member, count);
                if (firstPalette < 0) return null;
                int paletteEntry = firstPalette + 1 + frameIndex;
                byte[] gfx = member(FirstGraphicEntry + frameIndex);
                byte[] pal = member(paletteEntry);
                if (gfx == null || pal == null) return null;

                byte[] tiles = ReadTiles(gfx, TileCount);
                uint[] colours = ReadColours(pal);
                return tiles == null || colours == null ? null : new FieldWindowFrame(tiles, colours, paletteEntry);
            }
            catch { return null; }
        }

        /// <summary>
        /// The plain one-tile border menus and the yes/no box use: the archive's first picture with its first
        /// palette, standard_system in the Platinum decomp.
        /// </summary>
        public static FieldWindowFrame LoadStandard(Func<int, byte[]> member, int count)
        {
            try
            {
                int palette = FirstPaletteIndex(member, count);
                if (palette < 0) return null;
                byte[] gfx = member(0);
                byte[] pal = member(palette);
                if (gfx == null || pal == null) return null;

                byte[] tiles = ReadTiles(gfx, StandardTileCount);
                uint[] colours = ReadColours(pal);
                return tiles == null || colours == null ? null : new FieldWindowFrame(tiles, colours, palette);
            }
            catch { return null; }
        }

        /// <summary>The corners, edges and middle of the standard border.</summary>
        public const int StandardTileCount = 9;

        /// <summary>
        /// Paints the standard border round a writing area of the given size in tiles, one tile each side, and
        /// fills the middle with the paper. The window underneath takes its colours from the system font's
        /// palette, not the border's, whose colour 15 is black.
        /// </summary>
        public byte[] ComposeStandard(int tilesWide, int tilesHigh, out int width, out int height, uint paperArgb = 0xFFFFFFFFu)
        {
            int cols = tilesWide + 2, rows = tilesHigh + 2;
            int w = cols * TileSize, h = rows * TileSize;
            width = w; height = h;

            byte[] rgba = new byte[w * h * 4];
            void Put(int col, int row, int tile)
            {
                for (int y = 0; y < TileSize; y++)
                    for (int x = 0; x < TileSize; x++)
                    {
                        uint c = paperArgb;
                        if (tile >= 0)
                        {
                            byte index = _tiles[tile * TileSize * TileSize + y * TileSize + x];
                            c = index < _colours.Length ? _colours[index] : 0;
                        }
                        int at = ((row * TileSize + y) * w + col * TileSize + x) * 4;
                        rgba[at + 0] = (byte)(c >> 16);
                        rgba[at + 1] = (byte)(c >> 8);
                        rgba[at + 2] = (byte)c;
                        rgba[at + 3] = (byte)(c >> 24);
                    }
            }

            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                {
                    bool top = r == 0, bottom = r == rows - 1, left = c == 0, right = c == cols - 1;
                    int tile = top ? (left ? 0 : right ? 2 : 1)
                             : bottom ? (left ? 6 : right ? 8 : 7)
                             : left ? 3 : right ? 5 : -1;
                    Put(c, r, tile);
                }
            return rgba;
        }

        /// <summary>
        /// The system font's sixteen colours: menu windows are filled with 15 and written in 1 with a 2 shadow.
        /// Null when the font archive member cannot be read.
        /// </summary>
        public static uint[] LoadSystemFontColours(Func<int, byte[]> fontMember)
        {
            try
            {
                if (RomInfo.systemFontPaletteEntry < 0) return null;
                byte[] pal = fontMember(RomInfo.systemFontPaletteEntry);
                return pal == null ? null : ReadColours(pal);
            }
            catch { return null; }
        }

        /// <summary>The paper the writing sits on, as 0xAARRGGBB. </summary>
        public uint PaperArgb => _colours.Length > 15 ? _colours[15] : 0xFFFFFFFFu;

        /// <summary>
        /// Paints the whole border round a writing area of the given size in tiles, as straight RGBA.
        /// </summary>
        public byte[] Compose(int tilesWide, int tilesHigh, out int width, out int height)
        {
            int cols = 2 + tilesWide + 3;      // two tiles left of the writing, three right
            int rows = 1 + tilesHigh + 1;      // one above, one below
            int w = cols * TileSize, h = rows * TileSize;
            width = w; height = h;

            byte[] rgba = new byte[w * h * 4];
            void Put(int col, int row, int tile)
            {
                for (int y = 0; y < TileSize; y++)
                    for (int x = 0; x < TileSize; x++)
                    {
                        byte index = _tiles[tile * TileSize * TileSize + y * TileSize + x];
                        uint c = index < _colours.Length ? _colours[index] : 0;
                        int at = ((row * TileSize + y) * w + col * TileSize + x) * 4;
                        rgba[at + 0] = (byte)(c >> 16);
                        rgba[at + 1] = (byte)(c >> 8);
                        rgba[at + 2] = (byte)c;
                        rgba[at + 3] = (byte)(c >> 24);
                    }
            }

            int last = 2 + tilesWide;          // first column to the right of the writing

            Put(0, 0, 0); Put(1, 0, 1);
            for (int i = 0; i < tilesWide; i++) Put(2 + i, 0, 2);
            Put(last, 0, 3); Put(last + 1, 0, 4); Put(last + 2, 0, 5);

            for (int r = 0; r < tilesHigh; r++)
            {
                Put(0, 1 + r, 6); Put(1, 1 + r, 7);
                Put(last, 1 + r, 9); Put(last + 1, 1 + r, 10); Put(last + 2, 1 + r, 11);
            }

            Put(0, 1 + tilesHigh, 12); Put(1, 1 + tilesHigh, 13);
            for (int i = 0; i < tilesWide; i++) Put(2 + i, 1 + tilesHigh, 14);
            Put(last, 1 + tilesHigh, 15); Put(last + 1, 1 + tilesHigh, 16); Put(last + 2, 1 + tilesHigh, 17);

            return rgba;
        }

        // Tiles of 4-bit pixels, two a byte with the left one in the low half.
        private static byte[] ReadTiles(byte[] ncgr, int count)
        {
            byte[] packed = NitroBgCodec.ReadCharacters(ncgr);
            if (packed.Length < count * 32) return null;

            byte[] tiles = new byte[count * TileSize * TileSize];
            for (int t = 0; t < count; t++)
                for (int i = 0; i < 32; i++)
                {
                    byte b = packed[t * 32 + i];
                    int p = t * 64 + i * 2;
                    tiles[p] = (byte)(b & 0xF);
                    tiles[p + 1] = (byte)(b >> 4);
                }
            return tiles;
        }

        // The first sixteen colours, which is the half a palette window.c loads. Colour 0 is the one the
        // hardware leaves see-through on a background layer, which is what rounds the corners off.
        private static uint[] ReadColours(byte[] nclr)
        {
            ushort[] raw = NitroBgCodec.ReadColours(nclr);
            if (raw.Length < 16) return null;

            uint[] colours = new uint[16];
            for (int i = 0; i < 16; i++)
            {
                (byte r, byte g, byte b) = NitroBgCodec.Colour(raw[i]);
                colours[i] = i == 0 ? 0u : 0xFF000000u | ((uint)r << 16) | ((uint)g << 8) | b;
            }
            return colours;
        }

        // The frames' colours follow the system colours, whose position differs between the games.
        private static int FirstPaletteIndex(Func<int, byte[]> member, int count)
        {
            for (int i = 0; i < count; i++)
            {
                byte[] entry = member(i);
                if (entry != null && entry.Length >= 4 && entry[0] == 'R' && entry[1] == 'L' && entry[2] == 'C' && entry[3] == 'N')
                    return i;
            }
            return -1;
        }
    }
}
