using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using YamlDotNet.Serialization;

namespace DSPRE
{
    /// <summary>
    /// Reads (and, for ds-rom projects, edits) the DS ROM banner: the 32×32 game icon and the
    /// per-language titles shown in the DS system menu. Two on-disk shapes exist:
    /// legacy ndstool projects keep the raw <c>banner.bin</c> (4bpp tiled icon + BGR555 palette +
    /// UTF-16 titles), while ds-rom projects get an editable <c>banner/</c> folder
    /// (<c>banner.yaml</c> + <c>bitmap.png</c> + <c>palette.png</c>) that <c>dsrom build</c>
    /// re-encodes automatically. UI-toolkit-free: images go through <see cref="RawImage"/>.
    /// </summary>
    public static class GameBanner
    {
        // banner.bin layout (Nintendo standard)
        private const int IconBitmapOffset = 0x20;    // 4bpp, 4×4 tiles of 8×8 px = 512 bytes
        private const int IconPaletteOffset = 0x220;  // 16 × BGR555 = 32 bytes
        private const int TitleOffset = 0x240;        // 6 titles × 0x100 bytes UTF-16LE (JP,EN,FR,DE,IT,ES)
        private const int TitleBytes = 0x100;

        public const int IconSize = 32;
        /// <summary>Palette slot 0 is hardware-transparent, leaving 15 usable colors.</summary>
        public const int MaxOpaqueColors = 15;

        // ── Legacy banner.bin (display only) ─────────────────────────────────────────

        /// <summary>Decodes the 32×32 icon from a raw <c>banner.bin</c>. Palette index 0 comes out
        /// transparent (as the DS menu renders it). Returns null if the file is missing/short.</summary>
        public static RawImage ReadNdstoolIcon(string bannerBinPath)
        {
            if (!File.Exists(bannerBinPath)) return null;
            byte[] data = File.ReadAllBytes(bannerBinPath);
            if (data.Length < IconPaletteOffset + 32) return null;

            var palette = new (byte r, byte g, byte b)[16];
            for (int i = 0; i < 16; i++)
            {
                ushort v = (ushort)(data[IconPaletteOffset + i * 2] | (data[IconPaletteOffset + i * 2 + 1] << 8));
                palette[i] = ((byte)((v & 0x1F) * 8), (byte)(((v >> 5) & 0x1F) * 8), (byte)(((v >> 10) & 0x1F) * 8));
            }

            var img = new RawImage(IconSize, IconSize);
            int pos = IconBitmapOffset;
            for (int tileY = 0; tileY < 4; tileY++)
                for (int tileX = 0; tileX < 4; tileX++)
                    for (int y = 0; y < 8; y++)
                        for (int xByte = 0; xByte < 4; xByte++)
                        {
                            byte px = data[pos++];
                            int x = tileX * 8 + xByte * 2;
                            SetPaletted(img, x, tileY * 8 + y, px & 0x0F, palette);
                            SetPaletted(img, x + 1, tileY * 8 + y, px >> 4, palette);
                        }
            return img;
        }

        private static void SetPaletted(RawImage img, int x, int y, int palId, (byte r, byte g, byte b)[] palette)
        {
            var c = palette[palId];
            img.SetPixel(x, y, c.r, c.g, c.b, palId == 0 ? (byte)0 : (byte)255);
        }

        /// <summary>Reads the English title block (up to 3 lines) from a raw <c>banner.bin</c>.</summary>
        public static string ReadNdstoolTitle(string bannerBinPath)
        {
            try
            {
                byte[] data = File.ReadAllBytes(bannerBinPath);
                int off = TitleOffset + TitleBytes; // JP first, then EN
                if (data.Length < off + TitleBytes) return null;
                string s = Encoding.Unicode.GetString(data, off, TitleBytes);
                int nul = s.IndexOf('\0');
                return (nul >= 0 ? s.Substring(0, nul) : s).Trim();
            }
            catch { return null; }
        }

        // ── ds-rom banner folder ─────────────────────────────────────────────────────

        public static string DsRomBannerDir => Path.Combine(RomInfo.workDir, "banner");
        public static string DsRomBitmapPath => Path.Combine(DsRomBannerDir, "bitmap.png");
        public static string DsRomPalettePath => Path.Combine(DsRomBannerDir, "palette.png");
        public static string DsRomYamlPath => Path.Combine(DsRomBannerDir, "banner.yaml");

        /// <summary>Matches ds-rom's banner.yaml schema; <c>title</c> keys are language names
        /// ("japanese", "english", …) and values are the 2-3 line menu text.</summary>
        public class BannerYaml
        {
            public string version { get; set; }
            public Dictionary<string, string> title { get; set; }
            public BannerImages images { get; set; }
        }
        public class BannerImages
        {
            public string bitmap_path { get; set; }
            public string palette_path { get; set; }
        }

        public static BannerYaml ReadDsRomYaml()
        {
            try
            {
                if (!File.Exists(DsRomYamlPath)) return null;
                var deserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
                return deserializer.Deserialize<BannerYaml>(File.ReadAllText(DsRomYamlPath));
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Failed to parse banner.yaml: {ex.Message}");
                return null;
            }
        }

        public static void WriteDsRomYaml(BannerYaml banner)
        {
            var serializer = new SerializerBuilder().Build();
            File.WriteAllText(DsRomYamlPath, serializer.Serialize(banner));
        }

        /// <summary>
        /// Validates a candidate icon and writes it as the ds-rom project's <c>bitmap.png</c> +
        /// <c>palette.png</c> pair (palette slot 0 = transparent, then the opaque colors in
        /// first-seen order). Returns null on success, or a user-displayable error message.
        /// </summary>
        public static string ValidateAndWriteDsRomIcon(RawImage img)
        {
            if (img == null) return "The image could not be decoded.";
            if (img.Width != IconSize || img.Height != IconSize)
                return $"The icon must be exactly {IconSize}×{IconSize} pixels (got {img.Width}×{img.Height}).";

            // Normalize: alpha < 128 → the transparent slot; opaque pixels forced to alpha 255.
            var normalized = new RawImage(IconSize, IconSize);
            var opaqueColors = new List<(byte r, byte g, byte b)>();
            var colorIndex = new Dictionary<int, int>(); // packed rgb → palette slot (1-based)
            byte[] src = img.Bgra, dst = normalized.Bgra;
            for (int i = 0; i < src.Length; i += 4)
            {
                if (src[i + 3] < 128) continue; // stays (0,0,0,0)
                byte b = src[i], g = src[i + 1], r = src[i + 2];
                int key = (r << 16) | (g << 8) | b;
                if (!colorIndex.ContainsKey(key))
                {
                    if (opaqueColors.Count >= MaxOpaqueColors)
                        return $"Too many colors: DS icons allow at most {MaxOpaqueColors} opaque colors " +
                               "plus transparency. Reduce the color count and try again.";
                    colorIndex[key] = opaqueColors.Count + 1;
                    opaqueColors.Add((r, g, b));
                }
                dst[i] = b; dst[i + 1] = g; dst[i + 2] = r; dst[i + 3] = 255;
            }

            var palette = new RawImage(16, 1);
            for (int slot = 0; slot < opaqueColors.Count; slot++)
            {
                var c = opaqueColors[slot];
                palette.SetPixel(slot + 1, 0, c.r, c.g, c.b, 255);
            }

            Directory.CreateDirectory(DsRomBannerDir);
            WritePng(DsRomBitmapPath, normalized);
            WritePng(DsRomPalettePath, palette);
            return null;
        }

        // ── Minimal PNG writer (RGBA8, no interlace) ─────────────────────────────────
        // Kept dependency-free so headless code (tests, future CLI) can write banner images
        // without a UI toolkit's encoder.

        public static void WritePng(string path, RawImage img)
        {
            using var ms = new MemoryStream();

            // Raw scanlines, filter byte 0 per row, RGBA order.
            byte[] raw = new byte[img.Height * (1 + img.Width * 4)];
            int p = 0;
            for (int y = 0; y < img.Height; y++)
            {
                raw[p++] = 0;
                for (int x = 0; x < img.Width; x++)
                {
                    int i = (y * img.Width + x) * 4;
                    raw[p++] = img.Bgra[i + 2]; // R
                    raw[p++] = img.Bgra[i + 1]; // G
                    raw[p++] = img.Bgra[i];     // B
                    raw[p++] = img.Bgra[i + 3]; // A
                }
            }

            byte[] compressed;
            using (var cms = new MemoryStream())
            {
                using (var z = new ZLibStream(cms, CompressionLevel.Optimal, leaveOpen: true))
                    z.Write(raw, 0, raw.Length);
                compressed = cms.ToArray();
            }

            ms.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, 0, 8);

            byte[] ihdr = new byte[13];
            WriteBE(ihdr, 0, img.Width);
            WriteBE(ihdr, 4, img.Height);
            ihdr[8] = 8;   // bit depth
            ihdr[9] = 6;   // color type: RGBA
            WriteChunk(ms, "IHDR", ihdr);
            WriteChunk(ms, "IDAT", compressed);
            WriteChunk(ms, "IEND", Array.Empty<byte>());

            File.WriteAllBytes(path, ms.ToArray());
        }

        private static void WriteBE(byte[] buf, int off, int value)
        {
            buf[off] = (byte)(value >> 24); buf[off + 1] = (byte)(value >> 16);
            buf[off + 2] = (byte)(value >> 8); buf[off + 3] = (byte)value;
        }

        private static void WriteChunk(Stream s, string type, byte[] data)
        {
            byte[] len = new byte[4]; WriteBE(len, 0, data.Length);
            s.Write(len, 0, 4);
            byte[] typeBytes = Encoding.ASCII.GetBytes(type);
            s.Write(typeBytes, 0, 4);
            s.Write(data, 0, data.Length);
            uint crc = Crc32(typeBytes, data);
            byte[] crcBytes = new byte[4]; WriteBE(crcBytes, 0, (int)crc);
            s.Write(crcBytes, 0, 4);
        }

        private static uint[] _crcTable;
        private static uint Crc32(byte[] type, byte[] data)
        {
            if (_crcTable == null)
            {
                _crcTable = new uint[256];
                for (uint n = 0; n < 256; n++)
                {
                    uint c = n;
                    for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
                    _crcTable[n] = c;
                }
            }
            uint crc = 0xFFFFFFFF;
            foreach (byte b in type.Concat(data)) crc = _crcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
            return crc ^ 0xFFFFFFFF;
        }
    }
}
