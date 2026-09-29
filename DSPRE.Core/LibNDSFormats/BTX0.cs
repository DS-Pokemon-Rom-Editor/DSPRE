using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DSPRE.LibNDSFormats
{
    public class BTX0
    {
        public static uint PaletteIndex;

        public static uint PaletteCount;

        public static uint PaletteSize;

        public static uint ColorCount;

        public static uint ImageOffset;

        public static uint PaletteOffset;

        public static uint ImageWidth;

        public static uint ImageHeight;
        /// <summary>
        /// GDI-free twin of <see cref="Read"/>: decodes the BTX0 into a <see cref="RawImage"/>.
        /// Sets the same static header fields (<see cref="ImageOffset"/>, <see cref="PaletteOffset"/>,
        /// <see cref="ColorCount"/>, …) that <see cref="Write"/> relies on.
        /// </summary>
        public static RawImage ReadRaw(byte[] BTXFile) => ReadRaw(BTXFile, PaletteIndex);

        /// <summary>Decodes with the given palette rather than the static <see cref="PaletteIndex"/>.</summary>
        public static RawImage ReadRaw(byte[] BTXFile, uint paletteIndex)
        {
            if (BitConverter.ToUInt32(BTXFile, 0) != 811095106)
            {
                return null;
            }
            uint num = BitConverter.ToUInt32(BTXFile, 16);
            if (BitConverter.ToUInt32(BTXFile, (int)num) != 811091284)
            {
                return null;
            }
            uint num2 = num + BitConverter.ToUInt16(BTXFile, (int)(num + 14));
            uint num3 = (ImageOffset = num + BitConverter.ToUInt32(BTXFile, (int)(num + 20)));
            uint num4 = BitConverter.ToUInt32(BTXFile, (int)(num + 48)) << 3;
            uint num5 = num + BitConverter.ToUInt32(BTXFile, (int)(num + 52));
            uint num6 = (PaletteOffset = num + BitConverter.ToUInt32(BTXFile, (int)(num + 56)));
            uint num7 = BTXFile[num2 + 1];
            uint num8 = BitConverter.ToUInt16(BTXFile, (int)(num2 + 12 + num7 * 4 + 6));
            uint num9 = (uint)(8 << (((int)num8 >> 4) & 7));
            uint num10 = (num8 >> 10) & 7;
            uint num11 = (PaletteCount = BTXFile[num5 + 1]);
            PaletteSize = num4;
            if (num11 == 0 || paletteIndex >= num11)
            {
                return null;
            }
            if (num10 == 3)
            {
                int paletteLength = (int)(num4 / num11 / 2);
                if (num4 < 64 && num11 >= 2)
                {
                    paletteLength = (int)((BTXFile.Length - num6) / 2);
                }
                ColorCount = (uint)paletteLength;
                byte[] palR = new byte[paletteLength];
                byte[] palG = new byte[paletteLength];
                byte[] palB = new byte[paletteLength];
                for (int i = 0; i < paletteLength; i++)
                {
                    ushort num12 = BitConverter.ToUInt16(BTXFile, (int)(num6 + paletteIndex * (ColorCount * 2)) + i * 2);
                    palR[i] = (byte)((num12 & 0x1F) << 3);
                    palG[i] = (byte)((uint)(num12 & 0x3E0) >> 2);
                    palB[i] = (byte)((uint)(num12 & 0x7C00) >> 7);
                }
                ImageWidth = num9;
                ImageHeight = (num6 - num3) * 2 / num9;
                RawImage raw = new RawImage((int)ImageWidth, (int)ImageHeight);
                uint num13 = 0u;
                uint num14 = 0u;
                for (int j = (int)num3; j < num6; j++)
                {
                    uint num15 = BTXFile[j];
                    uint[] array2 = new uint[2]
                    {
                    num15 & 0xF,
                    num15 >> 4
                    };
                    for (int k = 0; k < array2.Length; k++)
                    {
                        uint idx = array2[k];
                        raw.SetPixel((int)num13, (int)num14, palR[idx], palG[idx], palB[idx], 255);
                        num13++;
                    }
                    if (num13 >= num9)
                    {
                        num13 = 0u;
                        num14++;
                    }
                }
                return raw;
            }
            return null;
        }

        public static Bitmap Read(byte[] BTXFile)
        {
            RawImage raw = ReadRaw(BTXFile);
            if (raw == null)
            {
                return null;
            }
            Bitmap bitmap = new Bitmap(raw.Width, raw.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            System.Drawing.Imaging.BitmapData data = bitmap.LockBits(
                new Rectangle(0, 0, raw.Width, raw.Height),
                System.Drawing.Imaging.ImageLockMode.WriteOnly,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            try
            {
                // Format32bppArgb memory layout is B,G,R,A little-endian, same as RawImage.Bgra.
                for (int y = 0; y < raw.Height; y++)
                {
                    System.Runtime.InteropServices.Marshal.Copy(
                        raw.Bgra, y * raw.Stride, data.Scan0 + y * data.Stride, raw.Stride);
                }
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
            return bitmap;
        }

        /// <summary>
        /// GDI-free twin of <see cref="Write(byte[], Bitmap)"/>: rebuilds the 4bpp image data and the
        /// selected palette from a <see cref="RawImage"/>. Relies on the statics set by the last
        /// <see cref="ReadRaw"/>/<see cref="Read"/> of the same file. Palette entries are assigned in
        /// first-seen scan order (the GDI overload's HashSet order was effectively the same).
        /// </summary>
        public static byte[] Write(byte[] BTXFile, RawImage bm)
        {
            byte[] px = bm.Bgra;
            List<uint> palette = new List<uint>();
            Dictionary<uint, uint> palIndex = new Dictionary<uint, uint>();
            for (int i = 0; i < bm.Width * bm.Height; i++)
            {
                uint c = BitConverter.ToUInt32(px, i * 4);
                if (palIndex.TryAdd(c, (uint)palette.Count))
                {
                    palette.Add(c);
                }
            }
            int p = 0;
            for (int j = (int)ImageOffset; j < PaletteOffset; j++)
            {
                uint lo = palIndex[BitConverter.ToUInt32(px, p * 4)];
                p++;
                uint hi = palIndex[BitConverter.ToUInt32(px, p * 4)];
                p++;
                BTXFile[j] = (byte)(lo | (hi << 4));
            }
            for (int m = 0; m < palette.Count; m++)
            {
                uint c = palette[m];
                byte blue = (byte)c;
                byte green = (byte)(c >> 8);
                byte red = (byte)(c >> 16);
                uint r5 = (uint)Math.Round(red / 8.0);
                uint g5 = (uint)Math.Round(green / 8.0);
                uint b5 = (uint)Math.Round(blue / 8.0);
                if (r5 > 31)
                {
                    r5 = 31u;
                }
                if (g5 > 31)
                {
                    g5 = 31u;
                }
                if (b5 > 31)
                {
                    b5 = 31u;
                }
                uint bgr555 = r5 + (g5 << 5) + (b5 << 10);
                BTXFile[PaletteOffset + PaletteIndex * (ColorCount * 2) + m * 2] = (byte)bgr555;
                BTXFile[PaletteOffset + PaletteIndex * (ColorCount * 2) + m * 2 + 1] = (byte)(bgr555 >> 8);
            }
            return BTXFile;
        }

        // Header math shared with ReadRaw, without touching the statics.
        private static bool TryGetLayout(byte[] f, uint paletteIndex, out int imageOffset, out int paletteBase, out int colorCount, out int width, out int height)
        {
            imageOffset = paletteBase = colorCount = width = height = 0;
            if (f == null || f.Length < 20 || BitConverter.ToUInt32(f, 0) != 811095106) return false;
            uint tex = BitConverter.ToUInt32(f, 16);
            if (tex + 60 > f.Length || BitConverter.ToUInt32(f, (int)tex) != 811091284) return false;
            uint dict = tex + BitConverter.ToUInt16(f, (int)(tex + 14));
            uint img = tex + BitConverter.ToUInt32(f, (int)(tex + 20));
            uint palSize = BitConverter.ToUInt32(f, (int)(tex + 48)) << 3;
            uint palDict = tex + BitConverter.ToUInt32(f, (int)(tex + 52));
            uint pal = tex + BitConverter.ToUInt32(f, (int)(tex + 56));
            if (dict + 1 >= f.Length || palDict + 1 >= f.Length) return false;
            uint entries = f[dict + 1];
            uint paramsAt = dict + 12 + entries * 4 + 6;
            if (paramsAt + 2 > f.Length) return false;
            uint param = BitConverter.ToUInt16(f, (int)paramsAt);
            if (((param >> 10) & 7) != 3) return false;
            uint count = f[palDict + 1];
            if (count == 0 || paletteIndex >= count) return false;
            int length = (int)(palSize / count / 2);
            if (palSize < 64 && count >= 2) length = (int)((f.Length - pal) / 2);
            width = 8 << (((int)param >> 4) & 7);
            if (pal <= img || pal > f.Length || length <= 0) return false;
            height = (int)((pal - img) * 2 / width);
            imageOffset = (int)img;
            colorCount = length;
            paletteBase = (int)(pal + paletteIndex * (uint)length * 2);
            return paletteBase + length * 2 <= f.Length;
        }

        private static ushort ToBgr555(uint bgra)
        {
            uint r5 = Math.Min(31u, (uint)Math.Round((byte)(bgra >> 16) / 8.0));
            uint g5 = Math.Min(31u, (uint)Math.Round((byte)(bgra >> 8) / 8.0));
            uint b5 = Math.Min(31u, (uint)Math.Round((byte)bgra / 8.0));
            return (ushort)(r5 | (g5 << 5) | (b5 << 10));
        }

        /// <summary>
        /// Writes the picture into a copy of <paramref name="BTXFile"/> without reordering the palette,
        /// so every other palette in the file stays aligned index for index. Each colour reuses the slot
        /// that already holds it, preferring the slot the pixel had before. Fully transparent pixels take
        /// slot 0. New colours take slots no pixel will use, those the old picture did not use first.
        /// Returns null with <paramref name="error"/> set when the picture does not fit.
        /// </summary>
        public static byte[] WriteKeepingPalette(byte[] BTXFile, RawImage bm, uint paletteIndex, out string error)
        {
            error = null;
            if (!TryGetLayout(BTXFile, paletteIndex, out int imageOffset, out int paletteBase, out int colorCount, out int width, out int height))
            {
                error = "This texture isn't a 16-color BTX0 image with that palette.";
                return null;
            }
            if (bm.Width != width || bm.Height != height)
            {
                error = $"Size mismatch. Existing texture: {width}×{height}, PNG: {bm.Width}×{bm.Height}";
                return null;
            }

            int slots = Math.Min(colorCount, 16);
            int pixels = width * height;
            var palette = new ushort[slots];
            for (int i = 0; i < slots; i++)
                palette[i] = (ushort)(BitConverter.ToUInt16(BTXFile, paletteBase + i * 2) & 0x7FFF);

            var oldIdx = new int[pixels];
            var usedByOld = new bool[16];
            for (int p = 0; p < pixels; p++)
            {
                int b = BTXFile[imageOffset + p / 2];
                oldIdx[p] = (p & 1) == 0 ? b & 0xF : b >> 4;
                usedByOld[oldIdx[p]] = true;
            }

            const int Transparent = -1;
            var colour = new int[pixels];
            var newIdx = new int[pixels];
            var claimed = new bool[16];
            var existing = new Dictionary<int, int>();
            for (int p = 0; p < pixels; p++)
            {
                uint c = BitConverter.ToUInt32(bm.Bgra, p * 4);
                colour[p] = (c >> 24) == 0 ? Transparent : ToBgr555(c);
                if (colour[p] == Transparent) newIdx[p] = 0;
                else if (oldIdx[p] < slots && palette[oldIdx[p]] == colour[p]) newIdx[p] = oldIdx[p];
                else
                {
                    newIdx[p] = -1;
                    continue;
                }
                claimed[newIdx[p]] = true;
            }

            for (int p = 0; p < pixels; p++)
            {
                if (newIdx[p] >= 0) continue;
                if (!existing.TryGetValue(colour[p], out int slot))
                {
                    slot = Array.IndexOf(palette, (ushort)colour[p]);
                    if (slot >= 0) claimed[slot] = true;
                    existing[colour[p]] = slot;
                }
                newIdx[p] = slot;
            }

            // Slot 0 is the see-through colour, so a new opaque colour never goes there.
            var added = new Dictionary<int, int>();
            for (int p = 0; p < pixels; p++)
            {
                if (newIdx[p] >= 0) continue;
                if (!added.TryGetValue(colour[p], out int slot))
                {
                    slot = -1;
                    for (int pass = 0; pass < 2 && slot < 0; pass++)
                        for (int s = 1; s < slots; s++)
                            if (!claimed[s] && (pass == 1 || !usedByOld[s])) { slot = s; break; }
                    if (slot < 0)
                    {
                        // Colours already given a slot can still sit on unassigned pixels, so count each once.
                        var needed = new HashSet<int>(added.Keys);
                        for (int q = 0; q < pixels; q++) if (newIdx[q] < 0) needed.Add(colour[q]);
                        string colours = needed.Count == 1 ? "1 colour that isn't" : $"{needed.Count} colours that aren't";
                        string free = added.Count == 0 ? "no slots are" : added.Count == 1 ? "only 1 slot is" : $"only {added.Count} slots are";
                        error = $"This picture needs {colours} in the palette, but {free} free. Use the palette's own colours.";
                        return null;
                    }
                    claimed[slot] = true;
                    added[colour[p]] = slot;
                    palette[slot] = (ushort)colour[p];
                }
                newIdx[p] = slot;
            }

            byte[] output = (byte[])BTXFile.Clone();
            for (int p = 0; p + 1 < pixels; p += 2)
                output[imageOffset + p / 2] = (byte)(newIdx[p] | (newIdx[p + 1] << 4));
            foreach (int slot in added.Values)
            {
                int at = paletteBase + slot * 2;
                ushort v = (ushort)((BitConverter.ToUInt16(BTXFile, at) & 0x8000) | palette[slot]);
                output[at] = (byte)v;
                output[at + 1] = (byte)(v >> 8);
            }
            return output;
        }

        /// <summary>
        /// Writes the picture into a copy of <paramref name="BTXFile"/> with a palette built from its own
        /// colours in first-seen order, for art that has no palette of its own yet. Only the given
        /// palette is written.
        /// </summary>
        public static byte[] WriteNewPalette(byte[] BTXFile, RawImage bm, uint paletteIndex, out string error)
        {
            error = null;
            if (!TryGetLayout(BTXFile, paletteIndex, out int imageOffset, out int paletteBase, out int colorCount, out int width, out int height))
            {
                error = "This texture isn't a 16-color BTX0 image with that palette.";
                return null;
            }
            if (bm.Width != width || bm.Height != height)
            {
                error = $"Size mismatch. Existing texture: {width}×{height}, PNG: {bm.Width}×{bm.Height}";
                return null;
            }

            int slots = Math.Min(colorCount, 16);
            int pixels = width * height;
            var index = new Dictionary<uint, int>();
            var colours = new List<uint>();
            var newIdx = new int[pixels];
            for (int p = 0; p < pixels; p++)
            {
                uint c = BitConverter.ToUInt32(bm.Bgra, p * 4);
                if ((c >> 24) == 0) c = 0;
                if (!index.TryGetValue(c, out int i))
                {
                    if (colours.Count >= slots)
                    {
                        error = $"Too many colors. Limit: {slots}.";
                        return null;
                    }
                    i = colours.Count;
                    index[c] = i;
                    colours.Add(c);
                }
                newIdx[p] = i;
            }

            byte[] output = (byte[])BTXFile.Clone();
            for (int p = 0; p + 1 < pixels; p += 2)
                output[imageOffset + p / 2] = (byte)(newIdx[p] | (newIdx[p + 1] << 4));
            for (int i = 0; i < colours.Count; i++)
            {
                ushort v = ToBgr555(colours[i]);
                output[paletteBase + i * 2] = (byte)v;
                output[paletteBase + i * 2 + 1] = (byte)(v >> 8);
            }
            return output;
        }

        /// <summary>Distinct colours, with every fully transparent pixel counted as one.</summary>
        public static int CountColors(RawImage img)
        {
            var seen = new HashSet<uint>();
            for (int i = 0; i + 3 < img.Bgra.Length; i += 4)
            {
                uint c = BitConverter.ToUInt32(img.Bgra, i);
                seen.Add((c >> 24) == 0 ? 0u : c);
            }
            return seen.Count;
        }

        public static byte[] Write(byte[] BTXFile, Bitmap bm)
        {
            HashSet<Color> hashSet = new HashSet<Color>();
            uint num = 0u;
            uint num2 = 0u;
            for (int i = 0; i < bm.Width * bm.Height; i++)
            {
                hashSet.Add(bm.GetPixel((int)num, (int)num2));
                num++;
                if (num >= bm.Width)
                {
                    num = 0u;
                    num2++;
                }
            }
            Color[] array = hashSet.ToArray();
            num = 0u;
            num2 = 0u;
            for (int j = (int)ImageOffset; j < PaletteOffset; j++)
            {
                Color pixel = bm.GetPixel((int)num, (int)num2);
                num++;
                uint num3 = 0u;
                for (int k = 0; k < array.Length; k++)
                {
                    if (array[k] == pixel)
                    {
                        num3 = (uint)k;
                        break;
                    }
                }
                pixel = bm.GetPixel((int)num, (int)num2);
                num++;
                for (int l = 0; l < array.Length; l++)
                {
                    if (array[l] == pixel)
                    {
                        num3 += (uint)(l << 4);
                        break;
                    }
                }
                BTXFile[j] = (byte)num3;
                if (num >= ImageWidth)
                {
                    num = 0u;
                    num2++;
                }
            }
            for (int m = 0; m < array.Length; m++)
            {
                uint num4 = (uint)Math.Round((double)(int)array[m].R / 8.0);
                uint num5 = (uint)Math.Round((double)(int)array[m].G / 8.0);
                uint num6 = (uint)Math.Round((double)(int)array[m].B / 8.0);
                if (num4 > 31)
                {
                    num4 = 31u;
                }
                if (num5 > 31)
                {
                    num5 = 31u;
                }
                if (num6 > 31)
                {
                    num6 = 31u;
                }
                uint num7 = num4 + (num5 << 5) + (num6 << 10);
                BTXFile[PaletteOffset + PaletteIndex * (ColorCount * 2) + m * 2] = (byte)num7;
                BTXFile[PaletteOffset + PaletteIndex * (ColorCount * 2) + m * 2 + 1] = (byte)(num7 >> 8);
            }
            return BTXFile;
        }
    }

}
