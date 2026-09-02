using System;
using System.Collections.Generic;
using System.Linq;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// A picture in one of the shapes the DS's texture unit reads. Which shape is chosen by what the
    /// picture actually needs: sixteen colours if it fits, then 256, then straight colour when it
    /// holds more than a list can.
    ///
    /// The numbers packed into TexImageParam are the ones the NitroSystem headers name
    /// (NNSG3dTexImageParam): the size fields at bits 20 and 23, the
    /// format at 26, and the see-through flag at 29.
    /// </summary>
    public sealed class DsTexture
    {
        /// <summary>The shapes this writes. The numbers are the hardware's own.</summary>
        public enum Kind { SixteenColours = 3, TwoHundredFiftySix = 4, StraightColour = 7 }

        public string Name = "texture";
        public int Width, Height;
        public Kind Format;
        /// <summary>The picture itself, in whatever shape Format says.</summary>
        public byte[] Pixels;
        /// <summary>Its colours, five bits a channel, or empty for straight colour.</summary>
        public ushort[] Colours = Array.Empty<ushort>();
        /// <summary>Whether the first colour means see-through rather than a colour.</summary>
        public bool FirstColourIsClear;

        public string Whynot;
        public List<string> Notes = new();

        /// <summary>How many colours a picture came in with, before anything was done to it.</summary>
        public int ColoursSeen;

        /// <param name="rgba">Four bytes a pixel.</param>
        public static DsTexture From(byte[] rgba, int width, int height, string name)
        {
            var t = new DsTexture { Name = Clean(name), Width = width, Height = height };
            if (rgba == null || width <= 0 || height <= 0 || rgba.Length < width * height * 4)
                return Fail(t, "That picture has no pixels in it.");
            if (!IsPowerOfTwo(width) || !IsPowerOfTwo(height) || width < 8 || height < 8
                || width > 1024 || height > 1024)
                return Fail(t, $"A picture painted on a model has to be a power of two across and down, "
                             + $"from 8 to 1024. That one is {width} by {height}. The nearest that would "
                             + $"fit is {NearestPowerOfTwo(width)} by {NearestPowerOfTwo(height)}.");

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
                t.Notes.Add($"The screen keeps five bits of red, green and blue, so the {full.Count} "
                          + $"colours in {t.Name} came out as {distinct.Count}.");

            int room = anyClear ? 1 : 0;                    // the clear slot, when one is needed
            if (distinct.Count + room <= 16) return t.AsPalette(Kind.SixteenColours, colour, clear, distinct);
            if (distinct.Count + room <= 256) return t.AsPalette(Kind.TwoHundredFiftySix, colour, clear, distinct);

            // Too many for any list, so each pixel carries its own colour. It costs twice the room and
            // the DS can only turn the whole picture see-through, not part of it.
            t.Format = Kind.StraightColour;
            t.Pixels = new byte[n * 2];
            for (int i = 0; i < n; i++)
            {
                ushort v = (ushort)(clear[i] ? 0 : colour[i] | 0x8000);
                t.Pixels[i * 2] = (byte)v;
                t.Pixels[i * 2 + 1] = (byte)(v >> 8);
            }
            t.Notes.Add($"{t.Name} uses {distinct.Count} colours, more than a list holds, so every pixel "
                      + "carries its own. It takes twice the room of a listed picture.");
            return t;
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

        /// <summary>
        /// The word the hardware is handed for this picture, with the place it sits in memory filled
        /// in by whoever lays the textures out.
        /// </summary>
        public uint ImageParam(int vramOffset) =>
            (uint)((vramOffset >> 3) & 0xFFFF)
            | (1u << 16) | (1u << 17)                       // repeat both ways, as the games do
            | ((uint)SizeCode(Width) << 20)
            | ((uint)SizeCode(Height) << 23)
            | ((uint)Format << 26)
            | (FirstColourIsClear ? 1u << 29 : 0u);

        /// <summary>The hardware keeps a size as how many times it doubles up from eight.</summary>
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

        /// <summary>Names inside a model are sixteen bytes of plain letters.</summary>
        private static string Clean(string name)
        {
            name = (name ?? "").Trim();
            var kept = new string(name.Where(c => c > 32 && c < 127).ToArray());
            if (kept.Length == 0) kept = "texture";
            return kept.Length > 15 ? kept.Substring(0, 15) : kept;
        }
    }
}
