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
        public static RawImage ReadRaw(byte[] BTXFile)
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
                    ushort num12 = BitConverter.ToUInt16(BTXFile, (int)(num6 + PaletteIndex * (ColorCount * 2)) + i * 2);
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
