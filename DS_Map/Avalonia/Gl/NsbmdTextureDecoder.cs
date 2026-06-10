using System;
using LibNDSFormats.NSBMD;

namespace DSPRE.Avalonia.Gl
{
    /// <summary>Decoded texture ready for GL upload (RGBA8, plus wrap modes).</summary>
    public sealed class NsbmdTextureData
    {
        public byte[] Rgba;          // width*height*4, row-major top-to-bottom
        public int Width, Height;
        public int WrapS, WrapT;     // 0 = clamp-to-edge, 1 = repeat, 2 = mirrored-repeat
    }

    /// <summary>
    /// Decodes the 7 NDS texture formats (A3I5, palette 4/16/256, 4×4-compressed, A5I3,
    /// direct BGR555) from an <see cref="NSBMDMaterial"/> into RGBA8 — ported from the
    /// original renderer's MakeTexture/convert_4x4texel. Returns null when the material
    /// has no texture (format 0) or lacks the data to decode.
    /// </summary>
    public static class NsbmdTextureDecoder
    {
        public static NsbmdTextureData Decode(NSBMDMaterial m)
        {
            if (m == null || m.format == 0) return null;
            if (m.paldata == null && m.format != 7) return null;
            if (m.texdata == null || m.width <= 0 || m.height <= 0) return null;

            int pixels = m.width * m.height;
            var img = new RGBA[pixels];
            var pal = m.paldata;

            try
            {
                switch (m.format)
                {
                    case 1: // A3I5
                        for (int j = 0; j < pixels; j++)
                        {
                            int index = m.texdata[j] & 0x1f;
                            int a = m.texdata[j] >> 5;
                            a = ((a * 4) + (a / 2)) * 8;
                            img[j] = pal[index]; img[j].A = (byte)a;
                        }
                        break;
                    case 2: // 4-colour palette
                        if (m.color0 != 0) pal[0] = RGBA.Transparent;
                        for (int j = 0; j < pixels; j++)
                        {
                            uint idx = m.texdata[j / 4];
                            idx = (idx >> ((j % 4) << 1)) & 3;
                            img[j] = pal[idx];
                        }
                        break;
                    case 3: // 16-colour palette
                        if (m.color0 != 0) pal[0] = RGBA.Transparent;
                        for (int j = 0; j < pixels; j++)
                        {
                            int mi = j / 2;
                            if (mi >= m.texdata.Length) continue;
                            int idx = (m.texdata[mi] >> ((j % 2) << 2)) & 0x0f;
                            if (idx >= 0 && idx < pal.Length) img[j] = pal[idx];
                        }
                        break;
                    case 4: // 256-colour palette
                        if (m.color0 != 0) pal[0] = RGBA.Transparent;
                        for (int j = 0; j < pixels; j++)
                        {
                            int idx = m.texdata[j];
                            if (idx >= 0 && idx < pal.Length) img[j] = pal[idx];
                        }
                        break;
                    case 5: // 4x4-texel compressed
                        Convert4x4(m.texdata, m.width, m.height, m.spdata, pal, img);
                        break;
                    case 6: // A5I3
                        for (int j = 0; j < pixels; j++)
                        {
                            int index = m.texdata[j] & 0x7;
                            int a = (m.texdata[j] >> 3) * 8;
                            img[j] = pal[index]; img[j].A = (byte)a;
                        }
                        break;
                    case 7: // direct BGR555
                        for (int j = 0; j < pixels; j++)
                        {
                            int p = m.texdata[j * 2] + (m.texdata[j * 2 + 1] << 8);
                            img[j].R = (byte)((p & 0x1f) << 3);
                            img[j].G = (byte)(((p >> 5) & 0x1f) << 3);
                            img[j].B = (byte)(((p >> 10) & 0x1f) << 3);
                            img[j].A = (byte)((p & 0x8000) != 0 ? 0xff : 0);
                        }
                        break;
                    default: return null;
                }
            }
            catch { return null; }

            var rgba = new byte[pixels * 4];
            for (int k = 0; k < pixels; k++)
            {
                rgba[k * 4 + 0] = img[k].R;
                rgba[k * 4 + 1] = img[k].G;
                rgba[k * 4 + 2] = img[k].B;
                rgba[k * 4 + 3] = img[k].A;
            }

            return new NsbmdTextureData
            {
                Rgba = rgba, Width = m.width, Height = m.height,
                WrapS = WrapOf(m.repeatS, m.flipS),
                WrapT = WrapOf(m.repeatT, m.flipT),
            };
        }

        private static int WrapOf(int repeat, int flip)
            => (repeat == 1 && flip == 1) ? 2 : (repeat == 1 ? 1 : 0);

        private static void Convert4x4(byte[] tex, int width, int height, byte[] data, RGBA[] pal, RGBA[] outImg)
        {
            if (data == null) return;
            int w = width / 4, h = height / 4;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int blockIndex = y * w + x;
                    if (blockIndex * 4 + 3 >= tex.Length || blockIndex * 2 + 1 >= data.Length) continue;
                    uint t = BitConverter.ToUInt32(tex, blockIndex * 4);
                    ushort d = BitConverter.ToUInt16(data, blockIndex * 2);
                    int addr = d & 0x3fff;
                    int mode = (d >> 14) & 3;

                    for (int r = 0; r < 4; r++)
                        for (int c = 0; c < 4; c++)
                        {
                            int texel = (int)((t >> ((r * 4 + c) * 2)) & 3);
                            int outIdx = (y * 4 + r) * width + (x * 4 + c);
                            int p0 = addr << 1;
                            RGBA px = default;
                            switch (mode)
                            {
                                case 0:
                                    px = Pal(pal, p0 + texel);
                                    if (texel == 3) px = RGBA.Transparent;
                                    break;
                                case 2:
                                    px = Pal(pal, p0 + texel);
                                    break;
                                case 1:
                                    if (texel < 2) px = Pal(pal, p0 + texel);
                                    else if (texel == 2) px = Mix(Pal(pal, p0), Pal(pal, p0 + 1), 1, 1);
                                    else px = RGBA.Transparent;
                                    break;
                                case 3:
                                    if (texel < 2) px = Pal(pal, p0 + texel);
                                    else if (texel == 2) px = Mix(Pal(pal, p0), Pal(pal, p0 + 1), 5, 3);
                                    else px = Mix(Pal(pal, p0), Pal(pal, p0 + 1), 3, 5);
                                    break;
                            }
                            if (outIdx >= 0 && outIdx < outImg.Length) outImg[outIdx] = px;
                        }
                }
        }

        private static RGBA Pal(RGBA[] pal, int i) => (i >= 0 && i < pal.Length) ? pal[i] : default;

        private static RGBA Mix(RGBA a, RGBA b, int wa, int wb)
        {
            int t = wa + wb;
            return new RGBA
            {
                R = (byte)((a.R * wa + b.R * wb) / t),
                G = (byte)((a.G * wa + b.G * wb) / t),
                B = (byte)((a.B * wa + b.B * wb) / t),
                A = 0xff
            };
        }
    }
}
