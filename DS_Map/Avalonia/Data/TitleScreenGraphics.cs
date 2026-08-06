using System;
using System.Collections.Generic;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// Reads and writes the HGSS main-menu title logo, background and copyright text strip in a/0/4/6.
    /// All three are real NCGR (tiles) + NCLR (palette) + NSCR (screen/tilemap) trios, confirmed against
    /// the leaked source's titledemo.naix and the real ROM's own bytes, not the flat "bitmap" shortcut an
    /// earlier pass here assumed. The logo and background share one palette (logo's colours first,
    /// background's appended right after); the copyright strip has its own separate, dedicated palette.
    ///
    /// The logo's real screen is 256x256 with visible content only in rows y=24-160; the top 192 rows
    /// (the actual DS screen height) are what this class exposes for preview/import/export, rows
    /// 192-255 are permanently off-screen and are always written blank on import.
    ///
    /// Importing any one image re-derives the *other* shared-palette image's current colours too (by
    /// re-decoding it) and rewrites both together, so the shared palette never drifts out of sync.
    /// </summary>
    public sealed class TitleScreenGraphics
    {
        public const int LogoWidth = 256, LogoHeight = 192;         // visible crop of the real 256x256 screen
        public const int BackgroundWidth = 256, BackgroundHeight = 192;
        public const int CopyrightWidth = 256, CopyrightHeight = 192;

        private const int LogoRealMapRows = 32;   // the real logo NSCR is 32x32 tiles (256x256)
        private const int LogoRealMapCols = 32;
        private const int LogoTileCapacity = 480;  // logo NCGR's real character-pool size
        private const int BackgroundTileCapacity = 768; // 32x24, no reuse in the real data, but budgeted the same
        private const int CopyrightTileCapacity = 96;   // copyright NCGR's real character-pool size

        private readonly ScriptNarc _narc = new(DirNames.titleScreenGraphics);
        public bool Available => _narc.Available;

        /// <summary>Which game's logo/background/palette this instance reads and writes. Both HeartGold's
        /// and SoulSilver's sets live in the same archive regardless of which one is actually loaded, so
        /// this can be switched freely, e.g. to edit both from one ROM project. Defaults to the loaded
        /// ROM's own version. Does not affect the copyright strip, which is shared between both.</summary>
        public GameVersions Version { get; set; } = gameVersion;

        // ── Session backup / revert ──────────────────────────────────────────
        // Keyed by raw NARC member index (the same index space regardless of which Version is selected,
        // since HeartGold and SoulSilver just occupy different member slots in the one archive). Captures
        // each member's on-disk bytes the first time this session is about to overwrite it, so RevertAll
        // can put back exactly what was there when the editor was opened, undoing every import made since.
        private readonly Dictionary<int, byte[]> _backup = new();

        public bool HasChanges => _backup.Count > 0;

        /// <summary>Reads a member's current on-disk bytes, snapshotting them first (once per session) so
        /// they can be restored later. Use this instead of a bare <c>_narc.Get</c> anywhere about to write.
        /// The snapshot is a defensive copy: when a member isn't LZ10-compressed, <see cref="NitroBgCodec.Inflate"/>
        /// hands back the very same array the caller goes on to mutate in place via WritePalette/WriteTileData/
        /// WriteMapData, so storing that reference directly would silently "revert" to the already-edited bytes.</summary>
        private byte[] GetAndSnapshot(int id)
        {
            byte[] raw = _narc.Get(id);
            if (raw != null && !_backup.ContainsKey(id)) _backup[id] = (byte[])raw.Clone();
            return raw;
        }

        /// <summary>Restores every archive member touched by an import this session back to its bytes from
        /// before the first edit, then forgets the backup (so importing again starts a fresh checkpoint).</summary>
        public void RevertAll()
        {
            foreach (var kv in _backup) _narc.Put(kv.Key, kv.Value);
            _backup.Clear();
        }

        // ── Decode ───────────────────────────────────────────────────────────
        public RawImage ComposeLogo()
        {
            var members = TitleScreenMembersFor(Version);
            var full = ComposeVia(members.logo, members.palette, members.logoNscr, transparentZero: true);
            return full == null ? null : Crop(full, LogoHeight);
        }

        public RawImage ComposeBackground()
        {
            var members = TitleScreenMembersFor(Version);
            return ComposeVia(members.background, members.palette, members.backgroundNscr, transparentZero: false);
        }

        public RawImage ComposeCopyright()
        {
            var m = TitleScreenCopyrightMembers;
            return ComposeVia(m.ncgr, m.nclr, m.nscr, transparentZero: true);
        }

        private RawImage ComposeVia(int chrIdx, int palIdx, int scrIdx, bool transparentZero)
        {
            if (!Available) return null;
            byte[] chr = NitroBgCodec.Inflate(_narc.Get(chrIdx));
            byte[] pal = NitroBgCodec.Inflate(_narc.Get(palIdx));
            byte[] scr = NitroBgCodec.Inflate(_narc.Get(scrIdx));
            if (chr == null || pal == null || scr == null) return null;
            try { return ToRawImage(NitroBgCodec.Composite(chr, pal, scr, transparentZero)); }
            catch (Exception ex)
            {
                AppLogger.Error($"TitleScreenGraphics.ComposeVia(chrIdx={chrIdx}): {ex}");
                return null;
            }
        }

        private static RawImage ToRawImage(NitroBgCodec.BgImage bg)
        {
            var raw = new RawImage(bg.Width, bg.Height);
            byte[] src = bg.Rgba, dst = raw.Bgra;
            for (int i = 0; i + 3 < src.Length; i += 4)
            { dst[i] = src[i + 2]; dst[i + 1] = src[i + 1]; dst[i + 2] = src[i]; dst[i + 3] = src[i + 3]; }
            return raw;
        }

        private static RawImage Crop(RawImage img, int height)
        {
            if (img.Height <= height) return img;
            var cropped = new RawImage(img.Width, height);
            Array.Copy(img.Bgra, 0, cropped.Bgra, 0, img.Width * height * 4);
            return cropped;
        }

        // ── Encode (logo + background, shared palette) ──────────────────────
        public string ImportLogo(RawImage png) => ImportLogoOrBackground(png, isLogo: true);
        public string ImportBackground(RawImage png) => ImportLogoOrBackground(png, isLogo: false);

        private string ImportLogoOrBackground(RawImage png, bool isLogo)
        {
            if (!Available) return "Title screen graphics archive is not available for this ROM.";
            if (png == null || png.IsEmpty) return "No image.";
            if (png.Width != LogoWidth || png.Height != LogoHeight)
                return $"Image must be exactly {LogoWidth}x{LogoHeight} (got {png.Width}x{png.Height}).";

            var members = TitleScreenMembersFor(Version);
            byte[] palRaw = NitroBgCodec.Inflate(GetAndSnapshot(members.palette));
            byte[] logoChrRaw = NitroBgCodec.Inflate(GetAndSnapshot(members.logo));
            byte[] logoScrRaw = NitroBgCodec.Inflate(GetAndSnapshot(members.logoNscr));
            byte[] bgChrRaw = NitroBgCodec.Inflate(GetAndSnapshot(members.background));
            byte[] bgScrRaw = NitroBgCodec.Inflate(GetAndSnapshot(members.backgroundNscr));
            if (palRaw == null || logoChrRaw == null || logoScrRaw == null || bgChrRaw == null || bgScrRaw == null)
                return "Could not read the current title screen graphics.";

            RawImage logoPng = isLogo ? png : ComposeLogo();
            RawImage bgPng = isLogo ? ComposeBackground() : png;
            if (logoPng == null || bgPng == null) return "Could not decode the current title screen graphics.";

            EncodedTiles logo, background;
            try
            {
                logo = QuantizeAndTile(logoPng, reserveZero: true, tileCols: LogoRealMapCols, tileRows: 24,
                    tileCapacity: LogoTileCapacity, bytesPerTile: 64, maxColors: 255);
                background = QuantizeAndTile(bgPng, reserveZero: false, tileCols: 32, tileRows: 24,
                    tileCapacity: BackgroundTileCapacity, bytesPerTile: 64, maxColors: 256);
            }
            catch (Exception ex) { return ex.Message; }

            int total = 1 + logo.Colors.Count + background.Colors.Count; // slot 0 is reserved for the logo's transparency
            if (total > 256)
                return $"Combined logo + background palette needs {total} colours, only 256 are available.";

            int bgBase = 1 + logo.Colors.Count;
            var palette = new (byte r, byte g, byte b)[256];
            for (int i = 0; i < logo.Colors.Count; i++) palette[1 + i] = logo.Colors[i];
            for (int i = 0; i < background.Colors.Count; i++) palette[bgBase + i] = background.Colors[i];

            // Logo tile pixel bytes are already final (0 = transparent, 1..N absolute); the background's
            // need shifting by bgBase, since QuantizeAndTile numbered them from 0 with no reservation.
            byte[] shiftedBgTileData = ShiftIndices(background.TileData, bgBase);

            // Logo's real NSCR is 32x32; only the top 24 rows are visible, pad the rest with blank (index 0).
            var logoMapEntries = new ushort[LogoRealMapCols * LogoRealMapRows];
            Array.Copy(logo.MapEntries, logoMapEntries, logo.MapEntries.Length);

            WritePalette(palRaw, palette);
            WriteTileData(logoChrRaw, logo.TileData);
            WriteMapData(logoScrRaw, logoMapEntries);
            WriteTileData(bgChrRaw, shiftedBgTileData);
            WriteMapData(bgScrRaw, background.MapEntries);

            _narc.Put(members.palette, palRaw);
            _narc.Put(members.logo, logoChrRaw);
            _narc.Put(members.logoNscr, logoScrRaw);
            _narc.Put(members.background, bgChrRaw);
            _narc.Put(members.backgroundNscr, bgScrRaw);
            return null;
        }

        // ── Encode (copyright, standalone palette) ───────────────────────────
        public string ImportCopyright(RawImage png)
        {
            if (!Available) return "Title screen graphics archive is not available for this ROM.";
            if (png == null || png.IsEmpty) return "No image.";
            if (png.Width != CopyrightWidth || png.Height != CopyrightHeight)
                return $"Image must be exactly {CopyrightWidth}x{CopyrightHeight} (got {png.Width}x{png.Height}).";

            var m = TitleScreenCopyrightMembers;
            byte[] palRaw = NitroBgCodec.Inflate(GetAndSnapshot(m.nclr));
            byte[] chrRaw = NitroBgCodec.Inflate(GetAndSnapshot(m.ncgr));
            byte[] scrRaw = NitroBgCodec.Inflate(GetAndSnapshot(m.nscr));
            if (palRaw == null || chrRaw == null || scrRaw == null)
                return "Could not read the current copyright graphics.";

            EncodedTiles encoded;
            try
            {
                // 4bpp, bank 0 only: 1 transparent + up to 15 opaque colours.
                encoded = QuantizeAndTile(png, reserveZero: true, tileCols: 32, tileRows: 24,
                    tileCapacity: CopyrightTileCapacity, bytesPerTile: 32, maxColors: 15);
            }
            catch (Exception ex) { return ex.Message; }

            var palette = new (byte r, byte g, byte b)[16];
            for (int i = 0; i < encoded.Colors.Count; i++) palette[1 + i] = encoded.Colors[i];

            WritePalette(palRaw, palette); // only the first 16-colour bank; the rest of the file is untouched
            WriteTileData(chrRaw, encoded.TileData);
            WriteMapData(scrRaw, encoded.MapEntries);

            _narc.Put(m.nclr, palRaw);
            _narc.Put(m.ncgr, chrRaw);
            _narc.Put(m.nscr, scrRaw);
            return null;
        }

        public string ImportPaletteRaw(byte[] nclrBytes)
        {
            if (!Available) return "Title screen graphics archive is not available for this ROM.";
            if (nclrBytes == null || nclrBytes.Length < 4 ||
                nclrBytes[0] != (byte)'R' || nclrBytes[1] != (byte)'L' || nclrBytes[2] != (byte)'C' || nclrBytes[3] != (byte)'N')
                return "Not a valid NCLR palette file.";
            int id = TitleScreenMembersFor(Version).palette;
            GetAndSnapshot(id);
            _narc.Put(id, nclrBytes);
            return null;
        }

        public byte[] ExportPaletteRaw() => Available ? NitroBgCodec.Inflate(_narc.Get(TitleScreenMembersFor(Version).palette)) : null;

        // ── Shared quantize + tile-dedup encoding ────────────────────────────
        private sealed class EncodedTiles
        {
            public List<(byte r, byte g, byte b)> Colors;
            public byte[] TileData;      // tileCapacity * bytesPerTile, zero-padded
            public ushort[] MapEntries;  // tileCols * tileRows, bank 0, no flip
        }

        /// <summary>Quantizes a PNG to palette indices and deduplicates its 8x8 tiles (first-seen order),
        /// throwing a descriptive error if it needs more distinct colours or unique tiles than available.
        /// With <paramref name="reserveZero"/>, transparent pixels map to index/colour 0 (not counted).</summary>
        private static EncodedTiles QuantizeAndTile(RawImage png, bool reserveZero, int tileCols, int tileRows,
            int tileCapacity, int bytesPerTile, int maxColors)
        {
            int w = tileCols * 8, h = tileRows * 8;
            var colorToIndex = new Dictionary<int, byte>();
            var colors = new List<(byte, byte, byte)>();
            var raster = new byte[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int si = (y * png.Width + x) * 4;
                    byte b = png.Bgra[si], g = png.Bgra[si + 1], r = png.Bgra[si + 2], a = png.Bgra[si + 3];
                    byte v;
                    if (reserveZero && a < 128)
                    {
                        v = 0;
                    }
                    else
                    {
                        int key = (r << 16) | (g << 8) | b;
                        if (!colorToIndex.TryGetValue(key, out v))
                        {
                            if (colors.Count >= maxColors)
                                throw new InvalidOperationException($"Image uses more than {maxColors} distinct colours.");
                            v = (byte)(colors.Count + (reserveZero ? 1 : 0));
                            colorToIndex[key] = v;
                            colors.Add((r, g, b));
                        }
                    }
                    raster[y * w + x] = v;
                }

            bool is8 = bytesPerTile == 64;
            var tileList = new List<byte[]>();
            var tileLookup = new Dictionary<string, int>();
            var mapEntries = new ushort[tileCols * tileRows];
            for (int ty = 0; ty < tileRows; ty++)
                for (int tx = 0; tx < tileCols; tx++)
                {
                    byte[] block = PackTile(raster, w, tx, ty, is8);
                    string key = Convert.ToBase64String(block);
                    if (!tileLookup.TryGetValue(key, out int tileIndex))
                    {
                        if (tileList.Count >= tileCapacity)
                            throw new InvalidOperationException(
                                $"Image needs more than {tileCapacity} unique 8x8 tiles once deduplicated. Simplify the image (flatter colours, more repeated blocks).");
                        tileIndex = tileList.Count;
                        tileLookup[key] = tileIndex;
                        tileList.Add(block);
                    }
                    mapEntries[ty * tileCols + tx] = (ushort)tileIndex;
                }

            var tileData = new byte[tileCapacity * bytesPerTile];
            for (int i = 0; i < tileList.Count; i++)
                Array.Copy(tileList[i], 0, tileData, i * bytesPerTile, bytesPerTile);

            return new EncodedTiles { Colors = colors, TileData = tileData, MapEntries = mapEntries };
        }

        private static byte[] PackTile(byte[] raster, int w, int tx, int ty, bool is8)
        {
            var block = new byte[is8 ? 64 : 32];
            for (int py = 0; py < 8; py++)
                for (int px = 0; px < 8; px++)
                {
                    byte v = raster[(ty * 8 + py) * w + (tx * 8 + px)];
                    if (is8)
                    {
                        block[py * 8 + px] = v;
                    }
                    else
                    {
                        int off = (py * 8 + px) / 2;
                        if ((px & 1) == 0) block[off] = (byte)((block[off] & 0xF0) | (v & 0x0F));
                        else block[off] = (byte)((block[off] & 0x0F) | ((v & 0x0F) << 4));
                    }
                }
            return block;
        }

        /// <summary>Shifts every index in a raw 8bpp tile-data byte array by <paramref name="shift"/>, used to
        /// move the background's (never-transparent, 0-based) tile pixel values into its slice of the merged
        /// palette. Safe from overflow because the caller already checked logo+background colours fit in 256.</summary>
        private static byte[] ShiftIndices(byte[] tileData, int shift)
        {
            var shifted = new byte[tileData.Length];
            for (int i = 0; i < tileData.Length; i++)
                shifted[i] = (byte)(tileData[i] + shift);
            return shifted;
        }

        private static void WritePalette(byte[] palRaw, (byte r, byte g, byte b)[] palette)
        {
            int pltt = NitroBgCodec.Find(palRaw, "TTLP", 0);
            int dataOffset = pltt + 0x18;
            for (int i = 0; i < palette.Length && dataOffset + i * 2 + 1 < palRaw.Length; i++)
            {
                var (r, g, b) = palette[i];
                ushort c = (ushort)(((r >> 3) & 0x1F) | (((g >> 3) & 0x1F) << 5) | (((b >> 3) & 0x1F) << 10));
                palRaw[dataOffset + i * 2] = (byte)(c & 0xFF);
                palRaw[dataOffset + i * 2 + 1] = (byte)(c >> 8);
            }
        }

        private static void WriteTileData(byte[] memberRaw, byte[] tiles)
        {
            int rahc = NitroBgCodec.Find(memberRaw, "RAHC", 0);
            int tileBytesOffset = rahc + 0x20;
            Array.Copy(tiles, 0, memberRaw, tileBytesOffset, tiles.Length);
        }

        private static void WriteMapData(byte[] scrRaw, ushort[] mapEntries)
        {
            int nrcs = NitroBgCodec.Find(scrRaw, "NRCS", 0);
            int mapDataOffset = nrcs + 0x14;
            for (int i = 0; i < mapEntries.Length; i++)
            {
                scrRaw[mapDataOffset + i * 2] = (byte)(mapEntries[i] & 0xFF);
                scrRaw[mapDataOffset + i * 2 + 1] = (byte)(mapEntries[i] >> 8);
            }
        }
    }
}
