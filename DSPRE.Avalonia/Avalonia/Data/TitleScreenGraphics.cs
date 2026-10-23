using System;
using System.IO;
using System.Collections.Generic;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// Reads and writes the HGSS title logo, background and copyright strip in a/0/4/6. The logo and background share a
    /// palette, so importing either rewrites both. The logo's screen is 256 by 256; only the top 192 rows show, and the rest
    /// is written blank.
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

        private readonly ScriptNarc _narc = new(DirNames.TitleScreenGraphics);
        public bool Available => _narc.Available;

        /// <summary>Which game's logo set to read and write; both are in the archive. Defaults to the loaded game. The copyright
        /// strip is shared.</summary>
        public GameVersions Version { get; set; } = GameVersion;

        /// <summary>Imports wait here until the owner saves; drawing and exports read through them.</summary>
        public PendingArchiveWrites Pending { get; } = new PendingArchiveWrites();

        public bool HasChanges => Pending.IsDirty;

        // A copy: Inflate can hand back the very array an import then changes in place.
        private byte[] Current(int id) => (byte[])_narc.Get(id)?.Clone();

        public void AcceptAll() => Pending.Save();
        public void RevertAll() => Pending.Discard();

        // ── Decode ───────────────────────────────────────────────────────────
        public RawImage ComposeLogo()
        {
            (int logo, int palette, int background, int logoNscr, int backgroundNscr) members = TitleScreenMembersFor(Version);
            RawImage full = ComposeVia(members.logo, members.palette, members.logoNscr, transparentZero: true);
            return full == null ? null : Crop(full, LogoHeight);
        }

        public RawImage ComposeBackground()
        {
            (int logo, int palette, int background, int logoNscr, int backgroundNscr) members = TitleScreenMembersFor(Version);
            return ComposeVia(members.background, members.palette, members.backgroundNscr, transparentZero: false);
        }

        public RawImage ComposeCopyright()
        {
            (int ncgr, int nclr, int nscr) m = TitleScreenCopyrightMembers;
            return ComposeVia(m.ncgr, m.nclr, m.nscr, transparentZero: true);
        }

        private RawImage ComposeVia(int chrIdx, int palIdx, int scrIdx, bool transparentZero)
        {
            using IDisposable reading = Pending.Reading();
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
            RawImage raw = new RawImage(bg.Width, bg.Height);
            byte[] src = bg.Rgba, dst = raw.Bgra;
            for (int i = 0; i + 3 < src.Length; i += 4)
            { dst[i] = src[i + 2]; dst[i + 1] = src[i + 1]; dst[i + 2] = src[i]; dst[i + 3] = src[i + 3]; }
            return raw;
        }

        private static RawImage Crop(RawImage img, int height)
        {
            if (img.Height <= height) return img;
            RawImage cropped = new RawImage(img.Width, height);
            Array.Copy(img.Bgra, 0, cropped.Bgra, 0, img.Width * height * 4);
            return cropped;
        }

        // ── Encode (logo + background, shared palette) ──────────────────────
        public string ImportLogo(RawImage png) => ImportLogoOrBackground(png, isLogo: true);
        public string ImportBackground(RawImage png) => ImportLogoOrBackground(png, isLogo: false);

        private string ImportLogoOrBackground(RawImage png, bool isLogo)
        {
            if (!Available) return "This game doesn't have the title screen archive.";
            if (png == null || png.IsEmpty) return "No image.";
            if (png.Width != LogoWidth || png.Height != LogoHeight)
                return $"That picture is {png.Width} by {png.Height}. It has to be {LogoWidth} by {LogoHeight}.";

            (int logo, int palette, int background, int logoNscr, int backgroundNscr) members = TitleScreenMembersFor(Version);
            byte[] palRaw = NitroBgCodec.Inflate(Current(members.palette));
            byte[] logoChrRaw = NitroBgCodec.Inflate(Current(members.logo));
            byte[] logoScrRaw = NitroBgCodec.Inflate(Current(members.logoNscr));
            byte[] bgChrRaw = NitroBgCodec.Inflate(Current(members.background));
            byte[] bgScrRaw = NitroBgCodec.Inflate(Current(members.backgroundNscr));
            if (palRaw == null || logoChrRaw == null || logoScrRaw == null || bgChrRaw == null || bgScrRaw == null)
                return "The current title screen couldn't be read.";

            RawImage logoPng = isLogo ? png : ComposeLogo();
            RawImage bgPng = isLogo ? ComposeBackground() : png;
            if (logoPng == null || bgPng == null) return "The current title screen couldn't be read.";

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
                return $"The logo and background need {total} colours between them; there's room for 256.";

            int bgBase = 1 + logo.Colors.Count;
            (byte r, byte g, byte b)[] palette = new (byte r, byte g, byte b)[256];
            for (int i = 0; i < logo.Colors.Count; i++) palette[1 + i] = logo.Colors[i];
            for (int i = 0; i < background.Colors.Count; i++) palette[bgBase + i] = background.Colors[i];

            // The logo's pixel values are final (0 is see-through); the background's shift by bgBase, as they start from 0.
            byte[] shiftedBgTileData = ShiftIndices(background.TileData, bgBase);

            // The logo's NSCR is 32 by 32; only the top 24 rows show, so the rest is blank.
            ushort[] logoMapEntries = new ushort[LogoRealMapCols * LogoRealMapRows];
            Array.Copy(logo.MapEntries, logoMapEntries, logo.MapEntries.Length);

            try
            {
                WritePalette(palRaw, palette);
                WriteTileData(logoChrRaw, logo.TileData);
                WriteMapData(logoScrRaw, logoMapEntries);
                WriteTileData(bgChrRaw, shiftedBgTileData);
                WriteMapData(bgScrRaw, background.MapEntries);
            }
            catch (InvalidDataException ex) { return ex.Message; }

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
            if (!Available) return "This game doesn't have the title screen archive.";
            if (png == null || png.IsEmpty) return "No image.";
            if (png.Width != CopyrightWidth || png.Height != CopyrightHeight)
                return $"That picture is {png.Width} by {png.Height}. It has to be {CopyrightWidth} by {CopyrightHeight}.";

            (int ncgr, int nclr, int nscr) m = TitleScreenCopyrightMembers;
            byte[] palRaw = NitroBgCodec.Inflate(Current(m.nclr));
            byte[] chrRaw = NitroBgCodec.Inflate(Current(m.ncgr));
            byte[] scrRaw = NitroBgCodec.Inflate(Current(m.nscr));
            if (palRaw == null || chrRaw == null || scrRaw == null)
                return "The current copyright strip couldn't be read.";

            EncodedTiles encoded;
            try
            {
                // 4bpp, bank 0 only: 1 transparent + up to 15 opaque colours.
                encoded = QuantizeAndTile(png, reserveZero: true, tileCols: 32, tileRows: 24,
                    tileCapacity: CopyrightTileCapacity, bytesPerTile: 32, maxColors: 15);
            }
            catch (Exception ex) { return ex.Message; }

            (byte r, byte g, byte b)[] palette = new (byte r, byte g, byte b)[16];
            for (int i = 0; i < encoded.Colors.Count; i++) palette[1 + i] = encoded.Colors[i];

            try
            {
                WritePalette(palRaw, palette); // only the first 16-colour bank; the rest of the file is untouched
                WriteTileData(chrRaw, encoded.TileData);
                WriteMapData(scrRaw, encoded.MapEntries);
            }
            catch (InvalidDataException ex) { return ex.Message; }

            _narc.Put(m.nclr, palRaw);
            _narc.Put(m.ncgr, chrRaw);
            _narc.Put(m.nscr, scrRaw);
            return null;
        }

        public string ImportPaletteRaw(byte[] nclrBytes)
        {
            if (!Available) return "This game doesn't have the title screen archive.";
            if (nclrBytes == null || nclrBytes.Length < 4 ||
                nclrBytes[0] != (byte)'R' || nclrBytes[1] != (byte)'L' || nclrBytes[2] != (byte)'C' || nclrBytes[3] != (byte)'N')
                return "That isn't a colour file.";
            int id = TitleScreenMembersFor(Version).palette;
            _narc.Put(id, nclrBytes);
            return null;
        }

        public byte[] ExportPaletteRaw()
        {
            if (!Available) return null;
            using IDisposable reading = Pending.Reading();
            return NitroBgCodec.Inflate(_narc.Get(TitleScreenMembersFor(Version).palette));
        }

        // ── Shared quantize + tile-dedup encoding ────────────────────────────
        private sealed class EncodedTiles
        {
            public List<(byte r, byte g, byte b)> Colors;
            public byte[] TileData;      // tileCapacity * bytesPerTile, zero-padded
            public ushort[] MapEntries;  // tileCols * tileRows, bank 0, no flip
        }

        /// <summary>Turns a PNG into palette indices and unique 8 by 8 tiles, throwing when it needs too many of either. With
        /// <paramref name="reserveZero"/>, see-through pixels take colour 0.</summary>
        private static EncodedTiles QuantizeAndTile(RawImage png, bool reserveZero, int tileCols, int tileRows,
            int tileCapacity, int bytesPerTile, int maxColors)
        {
            int w = tileCols * 8, h = tileRows * 8;
            Dictionary<int, byte> colorToIndex = new Dictionary<int, byte>();
            List<(byte, byte, byte)> colors = new List<(byte, byte, byte)>();
            byte[] raster = new byte[w * h];
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
                                throw new InvalidOperationException($"That picture uses more than {maxColors} colours.");
                            v = (byte)(colors.Count + (reserveZero ? 1 : 0));
                            colorToIndex[key] = v;
                            colors.Add((r, g, b));
                        }
                    }
                    raster[y * w + x] = v;
                }

            bool is8 = bytesPerTile == 64;
            List<byte[]> tileList = new List<byte[]>();
            Dictionary<string, int> tileLookup = new Dictionary<string, int>();
            ushort[] mapEntries = new ushort[tileCols * tileRows];
            for (int ty = 0; ty < tileRows; ty++)
                for (int tx = 0; tx < tileCols; tx++)
                {
                    byte[] block = PackTile(raster, w, tx, ty, is8);
                    string key = Convert.ToBase64String(block);
                    if (!tileLookup.TryGetValue(key, out int tileIndex))
                    {
                        if (tileList.Count >= tileCapacity)
                            throw new InvalidOperationException(
                                $"That picture needs more than {tileCapacity} different 8 by 8 tiles. Use flatter colours or more repeated blocks.");
                        tileIndex = tileList.Count;
                        tileLookup[key] = tileIndex;
                        tileList.Add(block);
                    }
                    mapEntries[ty * tileCols + tx] = (ushort)tileIndex;
                }

            byte[] tileData = new byte[tileCapacity * bytesPerTile];
            for (int i = 0; i < tileList.Count; i++)
                Array.Copy(tileList[i], 0, tileData, i * bytesPerTile, bytesPerTile);

            return new EncodedTiles { Colors = colors, TileData = tileData, MapEntries = mapEntries };
        }

        private static byte[] PackTile(byte[] raster, int w, int tx, int ty, bool is8)
        {
            byte[] block = new byte[is8 ? 64 : 32];
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

        /// <summary>Shifts every pixel value by <paramref name="shift"/>, moving the background into its part of the shared
        /// palette; the caller has checked both fit in 256 colours.</summary>
        private static byte[] ShiftIndices(byte[] tileData, int shift)
        {
            byte[] shifted = new byte[tileData.Length];
            for (int i = 0; i < tileData.Length; i++)
                shifted[i] = (byte)(tileData[i] + shift);
            return shifted;
        }

        private static void WritePalette(byte[] palRaw, (byte r, byte g, byte b)[] palette)
        {
            if (NitroBgCodec.ColourCount(palRaw) < 0) throw new InvalidDataException("The colour file has no palette block.");
            for (int i = 0; i < palette.Length; i++)
            {
                (byte r, byte g, byte b) = palette[i];
                if (!NitroBgCodec.PutColour(palRaw, i, NitroBgCodec.ToBgr555(r, g, b))) break;
            }
        }

        private static void WriteTileData(byte[] memberRaw, byte[] tiles)
        {
            int tileBytesOffset = NitroBgCodec.ReadTileHeader(memberRaw).TilesAt;
            if (tileBytesOffset < 0 || tileBytesOffset + tiles.Length > memberRaw.Length)
                throw new InvalidDataException("The picture needs more tiles than the game's file holds.");
            Array.Copy(tiles, 0, memberRaw, tileBytesOffset, tiles.Length);
        }

        private static void WriteMapData(byte[] scrRaw, ushort[] mapEntries)
        {
            int mapDataOffset = NitroBgCodec.ReadScreenHeader(scrRaw).MapAt;
            int entryBytes = NitroBgCodec.EntryBytes(scrRaw);
            for (int i = 0; i < mapEntries.Length; i++)
                NitroBgCodec.PutEntry(scrRaw, mapDataOffset, i, entryBytes, mapEntries[i]);
        }
    }
}
