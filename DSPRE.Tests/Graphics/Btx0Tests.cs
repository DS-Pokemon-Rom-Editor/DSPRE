using System;
using DSPRE;
using DSPRE.LibNDSFormats;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>
    /// Pins the GDI-free BTX0 decode/encode pair (<see cref="BTX0.ReadRaw"/> /
    /// <see cref="BTX0.Write(byte[], RawImage)"/>) on a synthetic 8×8 4bpp file, and that the
    /// transitional GDI <see cref="BTX0.Read"/> wrapper produces identical pixels.
    /// A wrong encode here silently corrupts overworld sprites in the ROM.
    /// </summary>
    public class Btx0Tests
    {
        // Offsets used by BTX0's parser, chosen for a minimal 224-byte file:
        //   [16]=20 → TEX0 at 20; dict at 20+80=100 (1 texture, params at 122: width 8, format 3);
        //   palette dict at 20+130=150 (1 palette); image data at 20+140=160 (32 bytes);
        //   palette data at 20+172=192 (16 × BGR555); palette size field [68]=4 → 4<<3=32 bytes.
        private static byte[] BuildFile()
        {
            var f = new byte[224];
            f[0] = (byte)'B'; f[1] = (byte)'T'; f[2] = (byte)'X'; f[3] = (byte)'0';
            BitConverter.GetBytes(20u).CopyTo(f, 16);
            f[20] = (byte)'T'; f[21] = (byte)'E'; f[22] = (byte)'X'; f[23] = (byte)'0';
            BitConverter.GetBytes((ushort)80).CopyTo(f, 34);    // tex dict at 100
            BitConverter.GetBytes(140u).CopyTo(f, 40);          // image data at 160
            BitConverter.GetBytes(4u).CopyTo(f, 68);            // palette size 4<<3 = 32
            BitConverter.GetBytes(130u).CopyTo(f, 72);          // palette dict at 150
            BitConverter.GetBytes(172u).CopyTo(f, 76);          // palette data at 192
            f[101] = 1;                                          // 1 texture
            BitConverter.GetBytes((ushort)(3 << 10)).CopyTo(f, 122); // width 8<<0, format 3 (4bpp)
            f[151] = 1;                                          // 1 palette

            for (int j = 0; j < 32; j++)                         // pixel i = i % 16, low nibble first
                f[160 + j] = (byte)(((2 * j) % 16) | (((2 * j + 1) % 16) << 4));
            for (int k = 0; k < 16; k++)                         // distinct BGR555 colors
                BitConverter.GetBytes((ushort)(R5(k) | (G5(k) << 5) | (B5(k) << 10))).CopyTo(f, 192 + k * 2);
            return f;
        }

        private static int R5(int k) => k;
        private static int G5(int k) => k + 8;
        private static int B5(int k) => 31 - k;

        [Fact]
        public void ReadRaw_DecodesSyntheticFile()
        {
            BTX0.PaletteIndex = 0;
            RawImage raw = BTX0.ReadRaw(BuildFile());

            Assert.NotNull(raw);
            Assert.Equal(8, raw.Width);
            Assert.Equal(8, raw.Height);
            Assert.Equal(16u, BTX0.ColorCount);

            for (int i = 0; i < 64; i++)
            {
                int k = i % 16;
                Assert.Equal(B5(k) * 8, raw.Bgra[i * 4 + 0]);   // B
                Assert.Equal(G5(k) * 8, raw.Bgra[i * 4 + 1]);   // G
                Assert.Equal(R5(k) * 8, raw.Bgra[i * 4 + 2]);   // R
                Assert.Equal(255, raw.Bgra[i * 4 + 3]);         // A
            }
        }

        [Fact]
        public void ReadRaw_RejectsAPaletteTheFileDoesNotStore()
        {
            BTX0.PaletteIndex = 1;
            Assert.Null(BTX0.ReadRaw(BuildFile()));
            Assert.Equal(1u, BTX0.PaletteCount);
        }

        [Fact]
        public void Write_RoundTripsThroughReadRaw()
        {
            BTX0.PaletteIndex = 0;
            byte[] file = BuildFile();
            Assert.NotNull(BTX0.ReadRaw(file));   // sets ImageOffset/PaletteOffset/ColorCount for Write

            // New image: same 16 colors but pixel i = (63 - i) % 16, different data, same color budget.
            var replacement = new RawImage(8, 8);
            for (int i = 0; i < 64; i++)
            {
                int k = (63 - i) % 16;
                replacement.SetPixel(i % 8, i / 8, (byte)(R5(k) * 8), (byte)(G5(k) * 8), (byte)(B5(k) * 8), 255);
            }

            byte[] written = BTX0.Write(file, replacement);
            RawImage roundTripped = BTX0.ReadRaw(written);

            Assert.NotNull(roundTripped);
            // Channels are multiples of 8, so the 555 quantization is lossless here.
            Assert.Equal(replacement.Bgra, roundTripped.Bgra);
        }

#if NET8_0_WINDOWS
        [Fact]
        public void GdiRead_MatchesReadRaw()
        {
            BTX0.PaletteIndex = 0;
            byte[] file = BuildFile();
            RawImage raw = BTX0.ReadRaw(file);
            using System.Drawing.Bitmap gdi = BTX0.Read(file);

            Assert.NotNull(gdi);
            RawImage viaGdi = GdiRawBridge.FromGdi(gdi);
            Assert.Equal(raw.Bgra, viaGdi.Bgra);
        }
#endif
    }
}
