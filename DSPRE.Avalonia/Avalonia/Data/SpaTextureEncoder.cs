using System;
using System.Collections.Generic;
using System.Linq;

namespace DSPRE.Avalonia.Data
{
    public sealed class SpaTextureEncoding
    {
        /// <summary>Why the image cannot be stored in this texture, or null when it was encoded.</summary>
        public string Error;
        public byte[] Texels;
        public byte[] Palette;
        /// <summary>Distinct visible RGB555 colours in the image.</summary>
        public int SourceColors;
        public int PaletteColorsUsed;
        /// <summary>The image had more colours than the palette holds, so they were merged.</summary>
        public bool Quantized;
        /// <summary>Every visible colour was already in the palette, so the palette bytes are untouched.</summary>
        public bool PaletteKept;
    }

    /// <summary>Encodes RGBA8 into an existing SPA texture, the inverse of <see cref="DSPRE.Avalonia.Gl.NsbmdTextureDecoder"/>.</summary>
    public static class SpaTextureEncoder
    {
        public static string FormatName(int format) => format switch
        {
            1 => "A3I5", 2 => "4-colour palette", 3 => "16-colour palette", 4 => "256-colour palette",
            5 => "4x4 compressed", 6 => "A5I3", 7 => "direct colour", _ => "format " + format,
        };

        /// <summary>Returns new texel and palette arrays the same lengths as <paramref name="texels"/> and <paramref name="palette"/>.</summary>
        public static SpaTextureEncoding Encode(int format, int width, int height, bool color0Transparent,
                                                byte[] texels, byte[] palette, byte[] rgba)
        {
            int pixels = width * height;
            if (rgba == null || rgba.Length != pixels * 4)
                return Fail($"The image data does not hold {width}x{height} pixels.");

            int need = format switch { 1 or 4 or 6 => pixels, 2 => (pixels + 3) / 4, 3 => (pixels + 1) / 2, 7 => pixels * 2, _ => -1 };
            if (format == 5)
                return Fail("4x4 compressed textures cannot be re-encoded.");
            if (need < 0)
                return Fail($"Texture format {format} is not one the games draw.");
            if (texels == null || texels.Length < need)
                return Fail($"The texture's stored pixel data is shorter than a {width}x{height} {FormatName(format)} image.");

            var outTex = (byte[])texels.Clone();
            if (format == 7) return EncodeDirect(pixels, rgba, outTex, palette);

            int indexBits = format switch { 1 => 5, 2 => 2, 3 => 4, 4 => 8, _ => 3 };
            int entries = (palette?.Length ?? 0) / 2;
            int usable = Math.Min(entries, 1 << indexBits);
            bool alphaFormat = format == 1 || format == 6;
            bool index0Clear = !alphaFormat && color0Transparent;
            int first = index0Clear ? 1 : 0;
            int slots = usable - first;
            if (slots <= 0)
                return Fail("This texture's palette has no room for a colour.");

            // Alpha level per pixel, and whether the pixel shows its colour.
            var level = new int[pixels];
            var shows = new bool[pixels];
            for (int j = 0; j < pixels; j++)
            {
                int a = rgba[j * 4 + 3];
                if (format == 1) { level[j] = NearestLevel(a, 7, A3I5Alpha); shows[j] = level[j] > 0; }
                else if (format == 6) { level[j] = NearestLevel(a, 31, A5I3Alpha); shows[j] = level[j] > 0; }
                else if (index0Clear) shows[j] = a >= 128;
                else
                {
                    if (a < 128)
                        return Fail("This texture has no transparent colour, but the image has transparent pixels.");
                    shows[j] = true;
                }
            }

            var histogram = new Dictionary<int, int>();
            var order = new List<int>();
            for (int j = 0; j < pixels; j++)
            {
                if (!shows[j]) continue;
                int c = To555(rgba, j);
                if (histogram.TryGetValue(c, out int n)) histogram[c] = n + 1;
                else { histogram[c] = 1; order.Add(c); }
            }

            var result = new SpaTextureEncoding { SourceColors = order.Count, Palette = (byte[])palette.Clone(), Texels = outTex };
            int[] colors;   // palette colours at indices first..first+colors.Length-1
            var existing = new int[slots];
            for (int i = 0; i < slots; i++) existing[i] = (palette[(first + i) * 2] | (palette[(first + i) * 2 + 1] << 8)) & 0x7FFF;

            if (order.All(c => Array.IndexOf(existing, c) >= 0))
            {
                colors = existing;
                result.PaletteKept = true;
                result.PaletteColorsUsed = order.Count;
            }
            else
            {
                colors = order.Count <= slots ? order.ToArray() : MedianCut(histogram, slots);
                result.Quantized = order.Count > slots;
                result.PaletteColorsUsed = colors.Length;
                for (int i = 0; i < slots; i++)
                {
                    int c = i < colors.Length ? colors[i] : 0;
                    result.Palette[(first + i) * 2] = (byte)c;
                    result.Palette[(first + i) * 2 + 1] = (byte)(c >> 8);
                }
            }

            var nearest = new Dictionary<int, int>();
            int IndexOf(int c)
            {
                if (nearest.TryGetValue(c, out int idx)) return idx;
                int best = 0, bestD = int.MaxValue;
                for (int i = 0; i < colors.Length; i++)
                {
                    int d = Distance(c, colors[i]);
                    if (d < bestD) { bestD = d; best = i; if (d == 0) break; }
                }
                return nearest[c] = first + best;
            }

            if (format == 2 || format == 3) Array.Clear(outTex, 0, need);
            for (int j = 0; j < pixels; j++)
            {
                int idx = !shows[j] && index0Clear ? 0 : IndexOf(To555(rgba, j));
                switch (format)
                {
                    case 1: outTex[j] = (byte)((level[j] << 5) | idx); break;
                    case 6: outTex[j] = (byte)((level[j] << 3) | idx); break;
                    case 2: outTex[j / 4] |= (byte)(idx << ((j % 4) * 2)); break;
                    case 3: outTex[j / 2] |= (byte)(idx << ((j % 2) * 4)); break;
                    case 4: outTex[j] = (byte)idx; break;
                }
            }
            return result;
        }

        private static SpaTextureEncoding EncodeDirect(int pixels, byte[] rgba, byte[] outTex, byte[] palette)
        {
            var seen = new HashSet<int>();
            for (int j = 0; j < pixels; j++)
            {
                int c = To555(rgba, j);
                bool opaque = rgba[j * 4 + 3] >= 128;
                if (opaque) seen.Add(c);
                int v = c | (opaque ? 0x8000 : 0);
                outTex[j * 2] = (byte)v;
                outTex[j * 2 + 1] = (byte)(v >> 8);
            }
            return new SpaTextureEncoding
            {
                Texels = outTex, Palette = (byte[])(palette ?? Array.Empty<byte>()).Clone(),
                SourceColors = seen.Count, PaletteColorsUsed = 0, PaletteKept = true,
            };
        }

        private static readonly int[] A3I5Alpha = Enumerable.Range(0, 8).Select(a => ((a * 4) + (a / 2)) * 8).ToArray();
        private static readonly int[] A5I3Alpha = Enumerable.Range(0, 32).Select(a => a * 8).ToArray();

        private static int NearestLevel(int alpha, int max, int[] decoded)
        {
            int best = 0;
            for (int l = 1; l <= max; l++)
                if (Math.Abs(decoded[l] - alpha) < Math.Abs(decoded[best] - alpha)) best = l;
            return best;
        }

        private static int To555(byte[] rgba, int j)
            => (rgba[j * 4] >> 3) | ((rgba[j * 4 + 1] >> 3) << 5) | ((rgba[j * 4 + 2] >> 3) << 10);

        private static int Distance(int a, int b)
        {
            int dr = (a & 0x1F) - (b & 0x1F), dg = ((a >> 5) & 0x1F) - ((b >> 5) & 0x1F), db = ((a >> 10) & 0x1F) - ((b >> 10) & 0x1F);
            return dr * dr + dg * dg + db * db;
        }

        // Weighted median cut in RGB555 space.
        private static int[] MedianCut(Dictionary<int, int> histogram, int count)
        {
            var boxes = new List<List<KeyValuePair<int, int>>> { histogram.ToList() };
            while (boxes.Count < count)
            {
                int pick = -1, pickRange = 0, pickChannel = 0;
                for (int i = 0; i < boxes.Count; i++)
                {
                    if (boxes[i].Count < 2) continue;
                    for (int ch = 0; ch < 3; ch++)
                    {
                        int lo = 31, hi = 0;
                        foreach (var kv in boxes[i]) { int v = (kv.Key >> (ch * 5)) & 0x1F; lo = Math.Min(lo, v); hi = Math.Max(hi, v); }
                        if (hi - lo > pickRange) { pickRange = hi - lo; pick = i; pickChannel = ch; }
                    }
                }
                if (pick < 0) break;

                int shift = pickChannel * 5;
                var box = boxes[pick].OrderBy(kv => (kv.Key >> shift) & 0x1F).ToList();
                long total = box.Sum(kv => (long)kv.Value), run = 0;
                int split = 1;
                for (int i = 0; i < box.Count - 1; i++)
                {
                    run += box[i].Value;
                    split = i + 1;
                    if (run * 2 >= total) break;
                }
                boxes[pick] = box.GetRange(0, split);
                boxes.Add(box.GetRange(split, box.Count - split));
            }

            return boxes.Select(b =>
            {
                long w = 0, r = 0, g = 0, bl = 0;
                foreach (var kv in b)
                {
                    w += kv.Value;
                    r += (kv.Key & 0x1F) * kv.Value; g += ((kv.Key >> 5) & 0x1F) * kv.Value; bl += ((kv.Key >> 10) & 0x1F) * kv.Value;
                }
                return (int)((r + w / 2) / w) | ((int)((g + w / 2) / w) << 5) | ((int)((bl + w / 2) / w) << 10);
            }).ToArray();
        }

        private static SpaTextureEncoding Fail(string message) => new SpaTextureEncoding { Error = message };
    }
}
