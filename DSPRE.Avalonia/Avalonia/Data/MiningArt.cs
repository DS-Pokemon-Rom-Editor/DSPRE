using System.Collections.Generic;
using System.IO;
using Avalonia.Media.Imaging;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// Draws an Underground wall row from ug_parts.narc as the game lays it out (Mining_DrawBuriedObject): Width by Height
    /// 4bpp tiles row by row, coloured by the palette's first 16 entries, with the cells outside the shape left out.
    /// </summary>
    internal static class MiningArt
    {
        private static string Member(int id) => Path.Combine(gameDirs[DirNames.undergroundParts].unpackedDir, id.ToString("D4"));

        public static void Unpack() => DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.undergroundParts });

        public static int MemberCount()
        {
            string dir = gameDirs[DirNames.undergroundParts].unpackedDir;
            return Directory.Exists(dir) ? Directory.GetFiles(dir).Length : 0;
        }

        /// <summary>What kind of file a member is: "tiles", "palette" or something else.</summary>
        public static string KindOf(int id)
        {
            string path = Member(id);
            if (!File.Exists(path)) return "";
            byte[] d = NitroBgCodec.Inflate(File.ReadAllBytes(path));
            if (NitroBgCodec.Find(d, "RAHC", 0) >= 0) return "tiles";
            if (NitroBgCodec.Find(d, "TTLP", 0) >= 0) return "palette";
            return "other";
        }

        /// <param name="masked">False draws every cell, for editing the shape over the whole picture.</param>
        public static Bitmap Render(MiningTable.Row row, bool masked = true)
        {
            if (row == null || row.Width <= 0 || row.Height <= 0) return null;
            string tilesPath = Member(row.Sprite), palettePath = Member(row.Palette);
            if (!File.Exists(tilesPath) || !File.Exists(palettePath)) return null;
            try
            {
                byte[] chr = NitroBgCodec.Inflate(File.ReadAllBytes(tilesPath));
                byte[] pal = NitroBgCodec.Inflate(File.ReadAllBytes(palettePath));
                (bool eightBit, int tilesAt) = NitroBgCodec.ReadTileHeader(chr);
                if (eightBit || tilesAt < 0) return null;
                (byte r, byte g, byte b)[] colours = NitroBgCodec.ReadPalette(pal, out _);
                int width = row.Width * 8, height = row.Height * 8;
                byte[] rgba = new byte[width * height * 4];
                for (int ty = 0; ty < row.Height; ty++)
                    for (int tx = 0; tx < row.Width; tx++)
                    {
                        if (masked && !row.Solid(tx / 2, ty / 2)) continue;
                        int tile = tilesAt + (ty * row.Width + tx) * 32;
                        if (tile + 32 > chr.Length) continue;
                        for (int y = 0; y < 8; y++)
                            for (int x = 0; x < 8; x++)
                            {
                                int v = chr[tile + y * 4 + x / 2];
                                v = (x & 1) != 0 ? v >> 4 : v & 15;
                                if (v == 0) continue;
                                int o = ((ty * 8 + y) * width + tx * 8 + x) * 4;
                                rgba[o] = colours[v].r; rgba[o + 1] = colours[v].g; rgba[o + 2] = colours[v].b; rgba[o + 3] = 255;
                            }
                    }
                return ImageConverter.FromRgba(rgba, width, height);
            }
            catch (IOException) { return null; }
        }
    }
}
