using System;
using System.IO;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// The border the games draw round a message box, read out of the ROM so an edited one shows.
    ///
    /// window.c:331 loads it from ARC_WINFRAME with TalkWinCgxArcGet for the picture and
    /// TalkWinPalArcGet for the colours, both of which are the archive's first talk frame plus the
    /// number the player chose in Options. winframe.naix puts the pictures at 2 to 21 and the colours at
    /// 26 to 45, so there are twenty to choose from.
    ///
    /// Each one is eighteen eight by eight tiles, six across and three down, and BmpTalkWinWriteMain in
    /// window.c:356 lays them out round the writing: two columns to its left, three to its right, and a
    /// row above and below, with the middle tiles repeated to whatever length is needed.
    /// </summary>
    public sealed class FieldWindowFrame
    {
        /// <summary>How many frames the player can pick between.</summary>
        public const int FrameCount = 20;

        /// <summary>The first talk frame's picture, NARC_winframe_talk_win00_ncgr.</summary>
        public const int FirstGraphicEntry = 2;
        /// <summary>The first talk frame's colours, NARC_winframe_talk_win00_nclr.</summary>
        public const int FirstPaletteEntry = 26;

        public const int TileSize = 8;
        public const int TileCount = 18;

        private readonly byte[] _tiles;         // one index a pixel, tile by tile
        private readonly uint[] _colours;       // 0xAARRGGBB, entry 0 see-through

        private FieldWindowFrame(byte[] tiles, uint[] colours) { _tiles = tiles; _colours = colours; }

        /// <summary>Reads one of the twenty frames out of the loaded ROM, or null if it cannot be read.</summary>
        public static FieldWindowFrame Load(int frameIndex = 0)
        {
            try
            {
                if (frameIndex < 0 || frameIndex >= FrameCount) frameIndex = 0;
                if (!RomInfo.gameDirs.TryGetValue(RomInfo.DirNames.windowFrames, out var dirs)) return null;
                string path = dirs.packedDir;
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;

                byte[] narc = File.ReadAllBytes(path);
                byte[] gfx = NarcEntry(narc, FirstGraphicEntry + frameIndex);
                byte[] pal = NarcEntry(narc, FirstPaletteEntry + frameIndex);
                if (gfx == null || pal == null) return null;

                byte[] tiles = ReadTiles(gfx);
                uint[] colours = ReadColours(pal);
                return tiles == null || colours == null ? null : new FieldWindowFrame(tiles, colours);
            }
            catch { return null; }
        }

        /// <summary>
        /// The paper the writing sits on, as 0xAARRGGBB. talk_msg.c:121 fills the box with colour 15
        /// before it writes anything, and the border itself never covers that middle part: the tile the
        /// layout would use for it, number 8, is the one BmpTalkWinWriteMain leaves out.
        /// </summary>
        public uint PaperArgb => _colours.Length > 15 ? _colours[15] : 0xFFFFFFFFu;

        /// <summary>
        /// Paints the whole border round a writing area of the given size in tiles, as straight RGBA.
        /// The middle of it is left see-through, because that is where the writing goes and the paper
        /// under it is painted separately.
        /// </summary>
        public byte[] Compose(int tilesWide, int tilesHigh, out int width, out int height)
        {
            int cols = 2 + tilesWide + 3;      // two tiles left of the writing, three right
            int rows = 1 + tilesHigh + 1;      // one above, one below
            int w = cols * TileSize, h = rows * TileSize;
            width = w; height = h;

            var rgba = new byte[w * h * 4];
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

        // Eighteen tiles, two pixels a byte with the left one in the low half.
        private static byte[] ReadTiles(byte[] ncgr)
        {
            if (ncgr.Length < 0x30 || ncgr[0] != 'R' || ncgr[1] != 'G' || ncgr[2] != 'C' || ncgr[3] != 'N') return null;
            int section = 0x10;
            int size = BitConverter.ToInt32(ncgr, section + 24);
            int at = section + 32;
            if (size < TileCount * 32 || at + size > ncgr.Length) return null;

            var tiles = new byte[TileCount * TileSize * TileSize];
            for (int t = 0; t < TileCount; t++)
                for (int i = 0; i < 32; i++)
                {
                    byte b = ncgr[at + t * 32 + i];
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
            if (nclr.Length < 0x30 || nclr[0] != 'R' || nclr[1] != 'L' || nclr[2] != 'C' || nclr[3] != 'N') return null;
            int at = 0x10 + 24;
            if (at + 32 > nclr.Length) return null;

            var colours = new uint[16];
            for (int i = 0; i < 16; i++)
            {
                ushort v = BitConverter.ToUInt16(nclr, at + i * 2);
                uint r = (uint)((v & 31) * 255 / 31);
                uint g = (uint)(((v >> 5) & 31) * 255 / 31);
                uint b = (uint)(((v >> 10) & 31) * 255 / 31);
                colours[i] = i == 0 ? 0u : 0xFF000000u | (r << 16) | (g << 8) | b;
            }
            return colours;
        }

        private static byte[] NarcEntry(byte[] narc, int index)
        {
            if (narc == null || narc.Length < 0x20) return null;
            if (narc[0] != 'N' || narc[1] != 'A' || narc[2] != 'R' || narc[3] != 'C') return null;

            int fatSize = BitConverter.ToInt32(narc, 0x14);
            int count = BitConverter.ToInt32(narc, 0x18);
            if (index < 0 || index >= count) return null;

            int fntStart = 0x10 + fatSize;
            int fntSize = BitConverter.ToInt32(narc, fntStart + 4);
            int images = fntStart + fntSize + 8;

            int at = 0x1C + index * 8;
            int from = BitConverter.ToInt32(narc, at);
            int to = BitConverter.ToInt32(narc, at + 4);
            if (to < from || images + to > narc.Length) return null;

            var blob = new byte[to - from];
            Array.Copy(narc, images + from, blob, 0, blob.Length);
            return blob;
        }
    }
}
