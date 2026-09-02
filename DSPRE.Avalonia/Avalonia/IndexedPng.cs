using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace DSPRE.Avalonia
{
    // Reads/writes real indexed (PNG color type 3) images, preserving the file's own literal index/PLTE order instead of re-quantizing by color like TryReadImageColors does.
    public static class IndexedPng
    {
        internal static readonly byte[] Signature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        public static bool TryRead(byte[] fileBytes, out byte[] indices, out uint[] palette, out int width, out int height)
        {
            indices = null; palette = null; width = 0; height = 0;
            if (fileBytes.Length < 8 || !fileBytes.AsSpan(0, 8).SequenceEqual(Signature)) return false;

            int pos = 8, w = 0, h = 0, bitDepth = 0, colorType = -1;
            byte[] plte = null, trns = null;
            using var idatStream = new MemoryStream();

            while (pos + 8 <= fileBytes.Length)
            {
                int len = ReadUInt32BE(fileBytes, pos);
                string type = Encoding.ASCII.GetString(fileBytes, pos + 4, 4);
                int dataStart = pos + 8;
                if (dataStart + len + 4 > fileBytes.Length) break;
                switch (type)
                {
                    case "IHDR":
                        w = ReadUInt32BE(fileBytes, dataStart);
                        h = ReadUInt32BE(fileBytes, dataStart + 4);
                        bitDepth = fileBytes[dataStart + 8];
                        colorType = fileBytes[dataStart + 9];
                        break;
                    case "PLTE":
                        plte = new byte[len];
                        Array.Copy(fileBytes, dataStart, plte, 0, len);
                        break;
                    case "tRNS":
                        trns = new byte[len];
                        Array.Copy(fileBytes, dataStart, trns, 0, len);
                        break;
                    case "IDAT":
                        idatStream.Write(fileBytes, dataStart, len);
                        break;
                    case "IEND":
                        pos = fileBytes.Length;
                        continue;
                }
                pos = dataStart + len + 4;
            }

            if (colorType != 3 || plte == null || w <= 0 || h <= 0) return false;
            if (bitDepth != 8 && bitDepth != 4 && bitDepth != 2 && bitDepth != 1) return false;

            idatStream.Position = 0;
            byte[] raw;
            using (var zlib = new ZLibStream(idatStream, CompressionMode.Decompress))
            using (var outMs = new MemoryStream())
            {
                zlib.CopyTo(outMs);
                raw = outMs.ToArray();
            }

            const int bpp = 1; // PNG spec: filtering byte-width is 1 for any sub-8-bit-depth single-channel image
            int stride = (w * bitDepth + 7) / 8;
            var prevLine = new byte[stride];
            var unfiltered = new byte[h * stride];
            int rawPos = 0;
            for (int y = 0; y < h; y++)
            {
                byte filterType = raw[rawPos++];
                var curLine = new byte[stride];
                Array.Copy(raw, rawPos, curLine, 0, stride);
                rawPos += stride;
                Unfilter(filterType, curLine, prevLine, bpp);
                Array.Copy(curLine, 0, unfiltered, y * stride, stride);
                prevLine = curLine;
            }

            indices = new byte[w * h];
            if (bitDepth == 8)
            {
                for (int y = 0; y < h; y++) Array.Copy(unfiltered, y * stride, indices, y * w, w);
            }
            else
            {
                int pxPerByte = 8 / bitDepth;
                byte mask = (byte)((1 << bitDepth) - 1);
                for (int y = 0; y < h; y++)
                {
                    int rowStart = y * stride;
                    for (int x = 0; x < w; x++)
                    {
                        int shift = 8 - bitDepth - (x % pxPerByte) * bitDepth;
                        indices[y * w + x] = (byte)((unfiltered[rowStart + x / pxPerByte] >> shift) & mask);
                    }
                }
            }

            int paletteCount = plte.Length / 3;
            palette = new uint[paletteCount];
            for (int i = 0; i < paletteCount; i++)
            {
                byte a = (trns != null && i < trns.Length) ? trns[i] : (byte)255;
                palette[i] = ((uint)a << 24) | ((uint)plte[i * 3] << 16) | ((uint)plte[i * 3 + 1] << 8) | plte[i * 3 + 2];
            }
            width = w; height = h;
            return true;
        }

        // Always writes 8-bit indexed: simplest, universally readable, and the palettes here never exceed 16 colors anyway.
        public static byte[] Write(byte[] indices, uint[] palette, int width, int height)
        {
            using var ms = new MemoryStream();
            ms.Write(Signature);

            var ihdr = new byte[13];
            WriteUInt32BEInto(ihdr, 0, width);
            WriteUInt32BEInto(ihdr, 4, height);
            ihdr[8] = 8; ihdr[9] = 3;
            WriteChunk(ms, "IHDR", ihdr);

            int n = palette.Length;
            var plte = new byte[n * 3];
            var trns = new byte[n];
            bool anyAlpha = false;
            for (int i = 0; i < n; i++)
            {
                uint c = palette[i];
                byte a = (byte)(c >> 24);
                plte[i * 3] = (byte)(c >> 16);
                plte[i * 3 + 1] = (byte)(c >> 8);
                plte[i * 3 + 2] = (byte)c;
                trns[i] = a;
                if (a != 255) anyAlpha = true;
            }
            WriteChunk(ms, "PLTE", plte);
            if (anyAlpha) WriteChunk(ms, "tRNS", trns);

            var raw = new byte[height * (width + 1)];
            for (int y = 0; y < height; y++)
                Array.Copy(indices, y * width, raw, y * (width + 1) + 1, width);

            byte[] compressed;
            using (var cms = new MemoryStream())
            {
                using (var zlib = new ZLibStream(cms, CompressionLevel.Optimal, leaveOpen: true))
                    zlib.Write(raw, 0, raw.Length);
                compressed = cms.ToArray();
            }
            WriteChunk(ms, "IDAT", compressed);
            WriteChunk(ms, "IEND", Array.Empty<byte>());
            return ms.ToArray();
        }

        internal static void Unfilter(byte filterType, byte[] cur, byte[] prev, int bpp)
        {
            switch (filterType)
            {
                case 1:
                    for (int i = 0; i < cur.Length; i++) cur[i] += (byte)(i >= bpp ? cur[i - bpp] : 0);
                    break;
                case 2:
                    for (int i = 0; i < cur.Length; i++) cur[i] += prev[i];
                    break;
                case 3:
                    for (int i = 0; i < cur.Length; i++) cur[i] += (byte)(((i >= bpp ? cur[i - bpp] : 0) + prev[i]) / 2);
                    break;
                case 4:
                    for (int i = 0; i < cur.Length; i++)
                    {
                        int a = i >= bpp ? cur[i - bpp] : 0, b = prev[i], c = i >= bpp ? prev[i - bpp] : 0;
                        cur[i] += (byte)PaethPredictor(a, b, c);
                    }
                    break;
            }
        }

        private static int PaethPredictor(int a, int b, int c)
        {
            int p = a + b - c;
            int pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
            if (pa <= pb && pa <= pc) return a;
            return pb <= pc ? b : c;
        }

        private static void WriteChunk(Stream s, string type, byte[] data)
        {
            byte[] typeBytes = Encoding.ASCII.GetBytes(type);
            WriteUInt32BE(s, data.Length);
            s.Write(typeBytes);
            s.Write(data);
            var crcBuf = new byte[typeBytes.Length + data.Length];
            Array.Copy(typeBytes, crcBuf, typeBytes.Length);
            Array.Copy(data, 0, crcBuf, typeBytes.Length, data.Length);
            WriteUInt32BE(s, unchecked((int)Crc32.Compute(crcBuf)));
        }

        internal static int ReadUInt32BE(byte[] data, int offset) =>
            (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];

        private static void WriteUInt32BE(Stream s, int value) =>
            s.Write(new[] { (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value });

        private static void WriteUInt32BEInto(byte[] buf, int offset, int value)
        {
            buf[offset] = (byte)(value >> 24); buf[offset + 1] = (byte)(value >> 16);
            buf[offset + 2] = (byte)(value >> 8); buf[offset + 3] = (byte)value;
        }

        private static class Crc32
        {
            private static readonly uint[] Table = BuildTable();
            private static uint[] BuildTable()
            {
                var table = new uint[256];
                for (uint n = 0; n < 256; n++)
                {
                    uint c = n;
                    for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
                    table[n] = c;
                }
                return table;
            }
            public static uint Compute(byte[] data)
            {
                uint crc = 0xFFFFFFFF;
                foreach (byte b in data) crc = Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
                return crc ^ 0xFFFFFFFF;
            }
        }
    }
}
