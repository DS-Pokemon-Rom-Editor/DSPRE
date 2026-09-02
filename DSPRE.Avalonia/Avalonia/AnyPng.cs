using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace DSPRE.Avalonia
{
    /// <summary>
    /// Reads a PNG of any kind into plain pixels. <see cref="IndexedPng"/> only reads the numbered
    /// sort, which is what going back into an existing drawing needs; making a drawing from scratch
    /// has to take whatever picture somebody hands it.
    /// </summary>
    public static class AnyPng
    {
        /// <param name="rgba">Four bytes a pixel, left to right then top to bottom.</param>
        /// <param name="whynot">Why it could not be read, when it could not.</param>
        public static bool TryReadRgba(byte[] file, out byte[] rgba, out int width, out int height,
                                       out string whynot)
        {
            rgba = null; width = 0; height = 0; whynot = null;
            if (file == null || file.Length < 8 || !file.AsSpan(0, 8).SequenceEqual(IndexedPng.Signature))
            { whynot = "That file is not a PNG."; return false; }

            int pos = 8, w = 0, h = 0, depth = 0, colour = -1, interlace = 0;
            byte[] plte = null, trns = null;
            using var idat = new MemoryStream();

            while (pos + 8 <= file.Length)
            {
                int len = IndexedPng.ReadUInt32BE(file, pos);
                string type = Encoding.ASCII.GetString(file, pos + 4, 4);
                int at = pos + 8;
                if (len < 0 || at + len + 4 > file.Length) break;
                switch (type)
                {
                    case "IHDR":
                        w = IndexedPng.ReadUInt32BE(file, at);
                        h = IndexedPng.ReadUInt32BE(file, at + 4);
                        depth = file[at + 8];
                        colour = file[at + 9];
                        interlace = file[at + 12];
                        break;
                    case "PLTE": plte = file.AsSpan(at, len).ToArray(); break;
                    case "tRNS": trns = file.AsSpan(at, len).ToArray(); break;
                    case "IDAT": idat.Write(file, at, len); break;
                    case "IEND": pos = file.Length; continue;
                }
                pos = at + len + 4;
            }

            if (w <= 0 || h <= 0) { whynot = "That PNG does not say how big it is."; return false; }
            if (interlace != 0)
            {
                whynot = "That PNG is saved interlaced. Save it again with interlacing turned off.";
                return false;
            }
            int channels = colour switch { 0 => 1, 2 => 3, 3 => 1, 4 => 2, 6 => 4, _ => 0 };
            if (channels == 0) { whynot = "That PNG stores its colours in a way this cannot read."; return false; }
            if (colour == 3 && plte == null) { whynot = "That PNG says it has a colour list but does not carry one."; return false; }
            if (depth != 1 && depth != 2 && depth != 4 && depth != 8 && depth != 16)
            { whynot = $"That PNG keeps {depth} bits a channel, which this cannot read."; return false; }
            if (depth == 16 && colour == 3) { whynot = "That PNG is not a sort this can read."; return false; }

            byte[] raw;
            try
            {
                idat.Position = 0;
                using var zlib = new ZLibStream(idat, CompressionMode.Decompress);
                using var outMs = new MemoryStream();
                zlib.CopyTo(outMs);
                raw = outMs.ToArray();
            }
            catch (Exception ex) { whynot = "That PNG's pixels could not be unpacked: " + ex.Message; return false; }

            int bits = channels * depth;
            int stride = (w * bits + 7) / 8;
            int filterStep = Math.Max(1, bits / 8);
            if ((long)h * (stride + 1) > raw.Length)
            { whynot = "That PNG stops partway through its pixels."; return false; }

            var lines = new byte[h * stride];
            var prev = new byte[stride];
            int rp = 0;
            for (int y = 0; y < h; y++)
            {
                byte kind = raw[rp++];
                var cur = new byte[stride];
                Array.Copy(raw, rp, cur, 0, stride);
                rp += stride;
                IndexedPng.Unfilter(kind, cur, prev, filterStep);
                Array.Copy(cur, 0, lines, y * stride, stride);
                prev = cur;
            }

            rgba = new byte[w * h * 4];
            int maxIndex = (1 << depth) - 1;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    byte r, g, b, a = 255;
                    if (colour == 3)
                    {
                        int idx = Sample(lines, y * stride, x, depth, 1, 0);
                        int o = idx * 3;
                        r = o + 2 < plte.Length ? plte[o] : (byte)0;
                        g = o + 2 < plte.Length ? plte[o + 1] : (byte)0;
                        b = o + 2 < plte.Length ? plte[o + 2] : (byte)0;
                        if (trns != null && idx < trns.Length) a = trns[idx];
                    }
                    else if (colour == 0 || colour == 4)
                    {
                        int v = Sample(lines, y * stride, x, depth, channels, 0);
                        byte grey = depth == 8 || depth == 16 ? (byte)v : (byte)(v * 255 / maxIndex);
                        r = g = b = grey;
                        if (colour == 4) a = (byte)Sample(lines, y * stride, x, depth, channels, 1);
                        else if (trns != null && trns.Length >= 2 && v == ((trns[0] << 8) | trns[1])) a = 0;
                    }
                    else
                    {
                        r = (byte)Sample(lines, y * stride, x, depth, channels, 0);
                        g = (byte)Sample(lines, y * stride, x, depth, channels, 1);
                        b = (byte)Sample(lines, y * stride, x, depth, channels, 2);
                        if (colour == 6) a = (byte)Sample(lines, y * stride, x, depth, channels, 3);
                    }
                    int d = (y * w + x) * 4;
                    rgba[d] = r; rgba[d + 1] = g; rgba[d + 2] = b; rgba[d + 3] = a;
                }

            width = w; height = h;
            return true;
        }

        // One channel of one pixel. Sixteen-bit channels are cut down to their top byte, which is all
        // a five-bit screen could ever show of them anyway.
        private static int Sample(byte[] line, int rowStart, int x, int depth, int channels, int channel)
        {
            if (depth == 8) return line[rowStart + x * channels + channel];
            if (depth == 16) return line[rowStart + (x * channels + channel) * 2];
            int per = 8 / depth;
            int i = x * channels + channel;
            int shift = 8 - depth - (i % per) * depth;
            return (line[rowStart + i / per] >> shift) & ((1 << depth) - 1);
        }
    }
}
