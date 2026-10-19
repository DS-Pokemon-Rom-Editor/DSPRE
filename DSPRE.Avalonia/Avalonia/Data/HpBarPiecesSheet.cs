using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// The HP bar pieces from the battle code as a picture, laid out by what each piece is: the fills as rows from
    /// empty to full, the status words one under another, the gender and Lv blocks as they appear. Exported as a
    /// 16 colour PNG in that layout and read back by colour, so a repainted sheet goes back into the overlay with
    /// the same palette indices the bar uses.
    /// </summary>
    public static class HpBarPiecesSheet
    {
        private const int Tile = 8;

        public sealed record Group(string Name, int Top, int Width, int Height);

        private sealed record Layout(List<(int Tile, int X, int Y)> Cells, List<Group> Groups, int Width, int Height);

        private static Layout For(int tiles)
        {
            List<(int, int, int)> cells = new List<(int, int, int)>();
            List<Group> groups = new List<Group>();
            int top = 0, widest = 0;

            void Add(string name, IEnumerable<(int Tile, int X, int Y)> placed)
            {
                List<(int Tile, int X, int Y)> list = placed.ToList();
                int w = list.Max(c => c.X) + 1, h = list.Max(c => c.Y) + 1;
                cells.AddRange(list.Select(c => (c.Tile, c.X, top + c.Y)));
                groups.Add(new Group(name, top, w, h));
                top += h;
                widest = Math.Max(widest, w);
            }

            BattleGaugePieces.Family? family = BattleGaugePieces.Locate()?.Family;
            if (family == BattleGaugePieces.Family.DiamondPearl && tiles == 0x46)
            {
                IEnumerable<(int, int, int)> Row(int first, int count) => Enumerable.Range(0, count).Select(i => (first + i, i, 0));
                Add("HP", Row(0, 2));
                Add("HP in double battles", Row(BattleGaugePieces.DoublesHp, 2));
                Add("Green health", Row(BattleGaugePieces.HpGreen, 9));
                Add("Yellow health", Row(BattleGaugePieces.HpYellow, 9));
                Add("Red health", Row(BattleGaugePieces.HpRed, 9));
                Add("Experience", Row(BattleGaugePieces.ExpFill, 9));
                Add("Status: spare, paralysis, freeze, sleep, poison, burn, none",
                    Enumerable.Range(0, 21).Select(i => (0x26 + i, i % 3, i / 3)));
                Add("Gender and Lv", BattleGaugePieces.GenderPieces.SelectMany((block, k) => new[] { (block[0], k * 3, 0), (block[1], k * 3 + 1, 0) }));
                Add("Caught ball", Row(BattleGaugePieces.CaughtBall, 1));
                Add("Bar end", Row(BattleGaugePieces.BarEnd, 1));
                Add("Slash", Row(BattleGaugePieces.Slash, 1));
            }
            else if (family == BattleGaugePieces.Family.PlatinumAndJohto && tiles == 0x4E)
            {
                IEnumerable<(int, int, int)> Row(int first, int count) => Enumerable.Range(0, count).Select(i => (first + i, i, 0));
                Add("HP", Row(0, 2));
                Add("HP in double battles", Row(BattleGaugePieces.DoublesHp, 2));
                Add("Green health", Row(BattleGaugePieces.HpGreen, 9));
                Add("Yellow health", Row(BattleGaugePieces.HpYellow, 9));
                Add("Red health", Row(BattleGaugePieces.HpRed, 9));
                Add("Experience", Row(BattleGaugePieces.ExpFill, 9));
                Add("Status: none, paralysis, freeze, sleep, poison, burn, blank",
                    Enumerable.Range(0, 21).Select(i => (BattleGaugePieces.NoStatus + i, i % 3, i / 3)));
                Add("Gender and Lv", new[] { 0x3C, 0x3E, 0x40 }.SelectMany((topLeft, k) => new[]
                {
                    (topLeft, k * 3, 0), (topLeft + 1, k * 3 + 1, 0), (topLeft + 12, k * 3, 1), (topLeft + 13, k * 3 + 1, 1),
                }));
                Add("Caught ball", Row(BattleGaugePieces.CaughtBall, 1));
                Add("Bar end", Row(BattleGaugePieces.BarEnd, 1));
                Add("Slash", Row(BattleGaugePieces.Slash, 1));
                Add("Number ends in double battles", Row(BattleGaugePieces.NumbersLeft, 2));
            }
            else
            {
                const int across = 16;
                Add("Pieces", Enumerable.Range(0, tiles).Select(t => (t, t % across, t / across)));
            }
            return new Layout(cells, groups, widest, top);
        }

        public static IReadOnlyList<Group> Groups()
        {
            byte[] sheet = BattleGaugePieces.ReadSheet();
            return sheet == null ? Array.Empty<Group>() : For(sheet.Length / BattleGaugePieces.TileBytes).Groups;
        }

        /// <summary>The sheet as palette indices, one byte a pixel, or null.</summary>
        public static byte[] Indices(out int width, out int height)
        {
            width = height = 0;
            byte[] sheet = BattleGaugePieces.ReadSheet();
            if (sheet == null) return null;
            Layout layout = For(sheet.Length / BattleGaugePieces.TileBytes);
            width = layout.Width * Tile;
            height = layout.Height * Tile;

            byte[] indices = new byte[width * height];
            foreach ((int t, int cx, int cy) in layout.Cells)
                for (int i = 0; i < BattleGaugePieces.TileBytes; i++)
                {
                    byte b = sheet[t * BattleGaugePieces.TileBytes + i];
                    int x = cx * Tile + i % 4 * 2, y = cy * Tile + i / 4;
                    indices[y * width + x] = (byte)(b & 0xF);
                    indices[y * width + x + 1] = (byte)(b >> 4);
                }
            return indices;
        }

        /// <summary>One block of the sheet in colour, colour 0 left clear, or null.</summary>
        public static byte[] GroupRgba(Group group, out int width, out int height)
        {
            width = group.Width * Tile;
            height = group.Height * Tile;
            byte[] indices = Indices(out int sheetWidth, out _);
            System.Drawing.Color[] colours = BattleGaugeTextRenderer.Colours();
            if (indices == null || colours == null) return null;

            byte[] rgba = new byte[width * height * 4];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    byte index = indices[(group.Top * Tile + y) * sheetWidth + x];
                    if (index == 0) continue;
                    System.Drawing.Color c = colours[index];
                    int at = (y * width + x) * 4;
                    rgba[at] = c.R;
                    rgba[at + 1] = c.G;
                    rgba[at + 2] = c.B;
                    rgba[at + 3] = 255;
                }
            return rgba;
        }

        public static string Export(string path)
        {
            byte[] indices = Indices(out int width, out int height);
            System.Drawing.Color[] colours = BattleGaugeTextRenderer.Colours();
            if (indices == null) return BattleGaugePieces.WhyNot ?? "The HP bar pieces could not be read.";
            if (colours == null) return "The HP bar colours could not be read.";
            try { PlainFiles.WriteBytes(path, IndexedPngWriter.Encode4Bpp(indices, width, height, colours[..16])); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { return ex.Message; }
            return null;
        }

        /// <summary>
        /// Reads a PNG of the sheet, in the exported layout, back into the battle code. Every pixel has to be one
        /// of the HP bar colours, or see-through for colour 0.
        /// </summary>
        /// <param name="write">Where the new tiles go; the sheet in the battle overlay when null.</param>
        public static string Import(string path, Func<byte[], string> write = null)
        {
            byte[] old = BattleGaugePieces.ReadSheet();
            System.Drawing.Color[] colours = BattleGaugeTextRenderer.Colours();
            if (old == null) return BattleGaugePieces.WhyNot ?? "The HP bar pieces could not be read.";
            if (colours == null) return "The HP bar colours could not be read.";

            Layout layout = For(old.Length / BattleGaugePieces.TileBytes);
            int wide = layout.Width * Tile, tall = layout.Height * Tile;

            byte[] file;
            try { file = File.ReadAllBytes(path); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { return ex.Message; }
            if (!AnyPng.TryReadRgba(file, out byte[] rgba, out int width, out int height, out string whynot)) return whynot;
            if (width != wide || height != tall)
                return $"The pieces picture is {wide} by {tall}; this one is {width} by {height}. Export it first and paint over that.";

            // A PNG still in the bar's own palette order keeps its indices, so two slots holding the same
            // colour stay apart.
            bool keepIndices = IndexedPng.TryRead(file, out byte[] indexed, out uint[] palette, out _, out _)
                               && palette.Length <= 16 && indexed.Length == width * height
                               && SameOrder(palette, colours);

            byte[] tilesOut = (byte[])old.Clone();
            int strays = 0;
            foreach ((int t, int cx, int cy) in layout.Cells)
            {
                Array.Clear(tilesOut, t * BattleGaugePieces.TileBytes, BattleGaugePieces.TileBytes);
                for (int y = 0; y < Tile; y++)
                    for (int x = 0; x < Tile; x++)
                    {
                        int at = (cy * Tile + y) * width + cx * Tile + x;
                        int index = keepIndices ? indexed[at] : IndexOf(colours, rgba, at);
                        if (index < 0) { strays++; index = 0; }
                        int b = t * BattleGaugePieces.TileBytes + y * 4 + x / 2;
                        tilesOut[b] |= (byte)(x % 2 == 0 ? index & 0xF : (index & 0xF) << 4);
                    }
            }
            if (strays > 0)
                return $"{strays} pixels are not in the HP bar colours. Change the colours first, or paint with those.";

            return (write ?? BattleGaugePieces.WriteSheet)(tilesOut);
        }

        private static bool SameOrder(uint[] palette, System.Drawing.Color[] colours)
        {
            for (int i = 1; i < palette.Length; i++)
            {
                uint p = palette[i];
                if (i >= colours.Length || !Same(colours[i], (byte)(p >> 16), (byte)(p >> 8), (byte)p)) return false;
            }
            return true;
        }

        // The DS keeps five bits a channel, so colours are compared at that precision.
        private static bool Same(System.Drawing.Color c, byte r, byte g, byte b) =>
            c.R >> 3 == r >> 3 && c.G >> 3 == g >> 3 && c.B >> 3 == b >> 3;

        private static int IndexOf(System.Drawing.Color[] colours, byte[] rgba, int pixel)
        {
            if (rgba[pixel * 4 + 3] < 128) return 0;
            for (int i = 1; i < 16 && i < colours.Length; i++)
                if (Same(colours[i], rgba[pixel * 4], rgba[pixel * 4 + 1], rgba[pixel * 4 + 2])) return i;
            return -1;
        }
    }
}
