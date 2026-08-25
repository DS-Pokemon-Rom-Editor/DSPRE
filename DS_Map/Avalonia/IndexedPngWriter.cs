using System;
using System.Drawing;
using System.IO;
using System.IO.Compression;

namespace DSPRE.Avalonia
{
    /// <summary>
    /// Writes a genuine indexed (palette) PNG — real PLTE/tRNS chunks, not a flattened RGBA render.
    /// Avalonia's own <c>Bitmap.Save</c> only writes 32bpp RGBA PNGs, and the cross-platform
    /// <see cref="RawImage"/> currency is always flattened BGRA (see its own doc comment), so neither
    /// can carry a real palette table through to the file — this is a from-scratch minimal encoder for
    /// the one shape DSPRE needs: up to 16 colors, 4 bits/pixel, index 0 transparent.
    /// </summary>
    internal static class IndexedPngWriter
    {
        private static readonly byte[] Signature = { 137, 80, 78, 71, 13, 10, 26, 10 };

        /// <summary>Encodes a 4bpp indexed PNG. <paramref name="palette"/> may have up to 16 entries;
        /// index 0 is written fully transparent (matches DSPRE's icon-graphic convention).</summary>
        public static byte[] Encode4Bpp(byte[] indices, int width, int height, Color[] palette)
        {
            using var ms = new MemoryStream();
            ms.Write(Signature, 0, Signature.Length);

            WriteChunk(ms, "IHDR", BuildIhdr(width, height));
            WriteChunk(ms, "PLTE", BuildPalette(palette));
            WriteChunk(ms, "tRNS", BuildTransparency(palette.Length));
            WriteChunk(ms, "IDAT", BuildIdat(indices, width, height));
            WriteChunk(ms, "IEND", Array.Empty<byte>());

            return ms.ToArray();
        }

        private static byte[] BuildIhdr(int width, int height)
        {
            var b = new byte[13];
            WriteUInt32BE(b, 0, (uint)width);
            WriteUInt32BE(b, 4, (uint)height);
            b[8] = 4;   // bit depth
            b[9] = 3;   // color type: palette
            b[10] = 0;  // compression: deflate
            b[11] = 0;  // filter: adaptive (per-scanline filter byte)
            b[12] = 0;  // interlace: none
            return b;
        }

        private static byte[] BuildPalette(Color[] palette)
        {
            int count = Math.Min(16, palette?.Length ?? 0);
            var b = new byte[count * 3];
            for (int i = 0; i < count; i++)
            {
                b[i * 3] = palette[i].R;
                b[i * 3 + 1] = palette[i].G;
                b[i * 3 + 2] = palette[i].B;
            }
            return b;
        }

        private static byte[] BuildTransparency(int paletteCount)
        {
            int count = Math.Min(16, paletteCount);
            var b = new byte[count];
            b[0] = 0; // index 0 fully transparent
            for (int i = 1; i < count; i++) b[i] = 255;
            return b;
        }

        private static byte[] BuildIdat(byte[] indices, int width, int height)
        {
            int rowBytes = (width + 1) / 2; // 2 pixels/byte, high nibble first
            var raw = new byte[(rowBytes + 1) * height]; // +1 per row for the filter-type byte

            int outPos = 0;
            for (int y = 0; y < height; y++)
            {
                raw[outPos++] = 0; // filter type: None
                for (int x = 0; x < width; x += 2)
                {
                    byte hi = (byte)(indices[y * width + x] & 0x0F);
                    byte lo = (x + 1 < width) ? (byte)(indices[y * width + x + 1] & 0x0F) : (byte)0;
                    raw[outPos++] = (byte)((hi << 4) | lo);
                }
            }

            using var compressed = new MemoryStream();
            using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
                zlib.Write(raw, 0, raw.Length);
            return compressed.ToArray();
        }

        private static void WriteChunk(Stream s, string type, byte[] data)
        {
            var lenBuf = new byte[4];
            WriteUInt32BE(lenBuf, 0, (uint)data.Length);
            s.Write(lenBuf, 0, 4);

            var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
            s.Write(typeBytes, 0, 4);
            s.Write(data, 0, data.Length);

            uint crc = Crc32(typeBytes, data);
            var crcBuf = new byte[4];
            WriteUInt32BE(crcBuf, 0, crc);
            s.Write(crcBuf, 0, 4);
        }

        private static void WriteUInt32BE(byte[] buf, int offset, uint value)
        {
            buf[offset] = (byte)(value >> 24);
            buf[offset + 1] = (byte)(value >> 16);
            buf[offset + 2] = (byte)(value >> 8);
            buf[offset + 3] = (byte)value;
        }

        private static readonly uint[] CrcTable = BuildCrcTable();

        private static uint[] BuildCrcTable()
        {
            var table = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                uint c = n;
                for (int k = 0; k < 8; k++)
                    c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
                table[n] = c;
            }
            return table;
        }

        private static uint Crc32(byte[] type, byte[] data)
        {
            uint c = 0xFFFFFFFF;
            foreach (byte b in type) c = CrcTable[(c ^ b) & 0xFF] ^ (c >> 8);
            foreach (byte b in data) c = CrcTable[(c ^ b) & 0xFF] ^ (c >> 8);
            return c ^ 0xFFFFFFFF;
        }
    }
}
