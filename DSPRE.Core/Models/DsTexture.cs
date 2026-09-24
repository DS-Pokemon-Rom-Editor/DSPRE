using System;
using System.Collections.Generic;
using System.Linq;

namespace DSPRE.Models
{
    /// <summary>A texture in a DS texture format.</summary>
    public sealed class DsTexture
    {
        public enum Kind { SixteenColours = 3, TwoHundredFiftySix = 4, StraightColour = 7 }

        public string Name = "texture";

        public List<string> PaletteNames = new List<string>();
        public int Width, Height;
        public Kind Format;
        public byte[] Pixels;
        public ushort[] Colours = Array.Empty<ushort>();
        public bool FirstColourIsClear;

        public string Whynot;
        public List<string> Notes = new();

        public int ColoursSeen;

        public static DsTexture From(byte[] rgba, int width, int height, string name)
        {
            var t = new DsTexture { Name = Clean(name), Width = width, Height = height };
            if (rgba == null || width <= 0 || height <= 0 || rgba.Length < width * height * 4)
                return Fail(t, "Empty image.");
            if (!IsPowerOfTwo(width) || !IsPowerOfTwo(height) || width < 8 || height < 8
                || width > 1024 || height > 1024)
                return Fail(t, $"Texture size must be a power of two from 8 to 1024, got {width}x{height}, nearest {NearestPowerOfTwo(width)}x{NearestPowerOfTwo(height)}.");

            int n = width * height;
            var clear = new bool[n];
            var colour = new ushort[n];
            bool anyClear = false;
            var distinct = new HashSet<ushort>();
            var full = new HashSet<int>();
            for (int i = 0; i < n; i++)
            {
                if (rgba[i * 4 + 3] < 128) { clear[i] = true; anyClear = true; continue; }
                int r = rgba[i * 4], g = rgba[i * 4 + 1], b = rgba[i * 4 + 2];
                full.Add((r << 16) | (g << 8) | b);
                colour[i] = (ushort)((r >> 3) | ((g >> 3) << 5) | ((b >> 3) << 10));
                distinct.Add(colour[i]);
            }
            t.ColoursSeen = distinct.Count;
            t.FirstColourIsClear = anyClear;
            if (full.Count > distinct.Count)
                t.Notes.Add($"{t.Name}: {full.Count} colours reduced to {distinct.Count}.");

            int room = anyClear ? 1 : 0;
            if (distinct.Count + room <= 16) return t.AsPalette(Kind.SixteenColours, colour, clear, distinct);
            if (distinct.Count + room <= 256) return t.AsPalette(Kind.TwoHundredFiftySix, colour, clear, distinct);

            t.Format = Kind.StraightColour;
            t.Pixels = new byte[n * 2];
            for (int i = 0; i < n; i++)
            {
                ushort v = (ushort)(clear[i] ? 0 : colour[i] | 0x8000);
                t.Pixels[i * 2] = (byte)v;
                t.Pixels[i * 2 + 1] = (byte)(v >> 8);
            }
            t.Notes.Add($"{t.Name}: {distinct.Count} colours, stored as direct colour.");
            return t;
        }

        /// <summary>Texels in an existing texture's format and palette, since animation frames copy only texels.</summary>
        public static byte[] TexelsLike(byte[] rgba, int width, int height, int format, IReadOnlyList<ushort> palette,
                                        bool firstColourIsClear, out string whynot)
        {
            whynot = null;
            int n = width * height;
            if (rgba == null || rgba.Length < n * 4) { whynot = "The picture is empty."; return null; }
            int bits = format switch { 2 => 2, 3 => 4, 4 => 8, 7 => 16, _ => 0 };
            if (bits == 0) { whynot = $"Frames in texture format {format} can't be written."; return null; }
            if (bits < 16 && (palette == null || palette.Count == 0)) { whynot = "The texture has no palette."; return null; }

            int Nearest(int r, int g, int b)
            {
                int best = firstColourIsClear ? 1 : 0, bestD = int.MaxValue;
                int limit = Math.Min(palette.Count, 1 << bits);
                for (int i = firstColourIsClear ? 1 : 0; i < limit; i++)
                {
                    int c = palette[i];
                    int dr = ((c & 31) << 3) - r, dg = (((c >> 5) & 31) << 3) - g, db = (((c >> 10) & 31) << 3) - b;
                    int d = dr * dr + dg * dg + db * db;
                    if (d < bestD) { bestD = d; best = i; }
                }
                return best;
            }

            var texels = new byte[n * bits / 8];
            for (int i = 0; i < n; i++)
            {
                int r = rgba[i * 4], g = rgba[i * 4 + 1], b = rgba[i * 4 + 2];
                bool clear = rgba[i * 4 + 3] < 128;
                if (bits == 16)
                {
                    ushort v = clear ? (ushort)0 : (ushort)((r >> 3) | ((g >> 3) << 5) | ((b >> 3) << 10) | 0x8000);
                    texels[i * 2] = (byte)v; texels[i * 2 + 1] = (byte)(v >> 8);
                    continue;
                }
                int index = clear && firstColourIsClear ? 0 : Nearest(r, g, b);
                int perByte = 8 / bits, shift = (i % perByte) * bits;
                texels[i / perByte] |= (byte)(index << shift);
            }
            return texels;
        }

        private DsTexture AsPalette(Kind kind, ushort[] colour, bool[] clear, HashSet<ushort> distinct)
        {
            int room = FirstColourIsClear ? 1 : 0;
            var order = distinct.OrderBy(c => c).ToList();
            var number = new Dictionary<ushort, int>();
            for (int i = 0; i < order.Count; i++) number[order[i]] = i + room;

            int slots = kind == Kind.SixteenColours ? 16 : 256;
            Colours = new ushort[slots];
            for (int i = 0; i < order.Count; i++) Colours[i + room] = order[i];

            Format = kind;
            int n = Width * Height;
            if (kind == Kind.SixteenColours)
            {
                Pixels = new byte[n / 2];
                for (int i = 0; i + 1 < n; i += 2)
                {
                    int lo = clear[i] ? 0 : number[colour[i]];
                    int hi = clear[i + 1] ? 0 : number[colour[i + 1]];
                    Pixels[i / 2] = (byte)((lo & 0xF) | ((hi & 0xF) << 4));
                }
            }
            else
            {
                Pixels = new byte[n];
                for (int i = 0; i < n; i++) Pixels[i] = (byte)(clear[i] ? 0 : number[colour[i]]);
            }
            return this;
        }

        public uint ImageParam(int vramOffset) =>
            (uint)((vramOffset >> 3) & 0xFFFF)
            | (1u << 16) | (1u << 17)
            | ((uint)SizeCode(Width) << 20)
            | ((uint)SizeCode(Height) << 23)
            | ((uint)Format << 26)
            | (FirstColourIsClear ? 1u << 29 : 0u);

        public static int SizeCode(int v)
        {
            int code = 0, at = 8;
            while (at < v && code < 7) { at <<= 1; code++; }
            return code;
        }

        public int PaletteBytes => Colours.Length * 2;

        private static DsTexture Fail(DsTexture t, string why) { t.Whynot = why; return t; }
        private static bool IsPowerOfTwo(int v) => v > 0 && (v & (v - 1)) == 0;

        private static int NearestPowerOfTwo(int v)
        {
            int at = 8;
            while (at < v && at < 1024) at <<= 1;
            return Math.Clamp(at, 8, 1024);
        }

        private static string Clean(string name)
        {
            name = (name ?? "").Trim();
            var kept = new string(name.Where(c => c > 32 && c < 127).ToArray());
            if (kept.Length == 0) kept = "texture";
            return kept.Length > 15 ? kept.Substring(0, 15) : kept;
        }
    }
}
