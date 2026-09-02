using System;
using System.Collections.Generic;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.Data
{
    /// <summary>Trainer card face design, 7 rank palettes, and male/female trainer-pose overlay.</summary>
    public sealed class TrainerCardGraphics
    {
        public const int CardWidth = 256, CardHeight = 192;
        public const int TrainerWidth = 256, TrainerHeight = 192;

        private const int CardTileCols = 32, CardTileRows = 24;
        private const int CardTileCapacity = 512;
        private const int TrainerTileCapacity = 512;

        private readonly ScriptNarc _narc = new(DirNames.trainerCardGraphics);
        public bool Available => _narc.Available;

        public static string[] RankNames => TrainerCardRankNames;

        private readonly Dictionary<int, byte[]> _backup = new();
        public bool HasChanges => _backup.Count > 0;

        // Clones on first read: Inflate() can return the same array we later mutate in place.
        private byte[] GetAndSnapshot(int id)
        {
            byte[] raw = _narc.Get(id);
            if (raw != null && !_backup.ContainsKey(id)) _backup[id] = (byte[])raw.Clone();
            return raw;
        }

        public void RevertAll()
        {
            foreach (var kv in _backup) _narc.Put(kv.Key, kv.Value);
            _backup.Clear();
        }

        // ── Decode ───────────────────────────────────────────────────────────
        public RawImage ComposeCardFront(int rankIndex) => ComposeCard(rankIndex, front: true);
        public RawImage ComposeCardBack(int rankIndex) => ComposeCard(rankIndex, front: false);

        private RawImage ComposeCard(int rankIndex, bool front)
        {
            var m = TrainerCardMembers;
            int palId = m.rankPalettes[rankIndex];
            int scrId = front ? m.facaNscr : m.backNscr;
            return ComposeVia(m.ncgr, palId, scrId, transparentZero: false);
        }

        public RawImage ComposeTrainer(bool male)
        {
            var t = TrainerCardTrainerMembers;
            var m = TrainerCardMembers;
            int scrId = male ? t.maleNscr : t.femaleNscr;
            return ComposeVia(t.ncgr, m.rankPalettes[0], scrId, transparentZero: true);
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
                AppLogger.Error($"TrainerCardGraphics.ComposeVia(chrIdx={chrIdx}): {ex}");
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

        // ── Card design (shared NCGR, rebuilds all 7 rank palettes) ──────────────────
        public string ImportCardFront(RawImage png) => ImportCardDesign(png, front: true);
        public string ImportCardBack(RawImage png) => ImportCardDesign(png, front: false);

        private string ImportCardDesign(RawImage png, bool front)
        {
            if (!Available) return "Trainer card graphics archive is not available for this ROM.";
            if (png == null || png.IsEmpty) return "No image.";
            if (png.Width != CardWidth || png.Height != CardHeight)
                return $"Image must be exactly {CardWidth}x{CardHeight} (got {png.Width}x{png.Height}).";

            var m = TrainerCardMembers;
            byte[] chrRaw = NitroBgCodec.Inflate(GetAndSnapshot(m.ncgr));
            byte[] facaRaw = NitroBgCodec.Inflate(GetAndSnapshot(m.facaNscr));
            byte[] backRaw = NitroBgCodec.Inflate(GetAndSnapshot(m.backNscr));
            if (chrRaw == null || facaRaw == null || backRaw == null)
                return "Could not read the current card design.";

            RawImage frontPng = front ? png : ComposeCardFront(0);
            RawImage backPng = front ? ComposeCardBack(0) : png;
            if (frontPng == null || backPng == null) return "Could not decode the current card design.";

            EncodedTiles frontTiles, backTiles;
            try
            {
                frontTiles = QuantizeAndTile(frontPng, tileCols: CardTileCols, tileRows: CardTileRows,
                    tileCapacity: CardTileCapacity, maxColors: 256);
                backTiles = QuantizeAndTile(backPng, tileCols: CardTileCols, tileRows: CardTileRows,
                    tileCapacity: CardTileCapacity, maxColors: 256);
            }
            catch (Exception ex) { return ex.Message; }

            int total = frontTiles.Colors.Count + backTiles.Colors.Count;
            if (total > 256)
                return $"Combined front + back design needs {total} colours, only 256 are available.";

            int backBase = frontTiles.Colors.Count;
            var palette = new (byte r, byte g, byte b)[256];
            for (int i = 0; i < frontTiles.Colors.Count; i++) palette[i] = frontTiles.Colors[i];
            for (int i = 0; i < backTiles.Colors.Count; i++) palette[backBase + i] = backTiles.Colors[i];

            var merged = MergeTilePools(frontTiles, backTiles, backBase, CardTileCapacity, reserveZero: false);
            if (merged == null)
                return $"Front + back design needs more than {CardTileCapacity} unique 8x8 tiles once deduplicated. Simplify the images.";

            WriteTileData(chrRaw, merged.TileData);
            WriteMapData(facaRaw, merged.FrontMapEntries);
            WriteMapData(backRaw, merged.BackMapEntries);

            _narc.Put(m.ncgr, chrRaw);
            _narc.Put(m.facaNscr, facaRaw);
            _narc.Put(m.backNscr, backRaw);

            foreach (int palId in m.rankPalettes)
            {
                byte[] palRaw = NitroBgCodec.Inflate(GetAndSnapshot(palId));
                if (palRaw == null) continue;
                WritePalette(palRaw, palette);
                _narc.Put(palId, palRaw);
            }
            return null;
        }

        // ── Trainer pose (shared NCGR, recolors the Normal rank's palette) ───────────
        public string ImportTrainerMale(RawImage png) => ImportTrainer(png, male: true);
        public string ImportTrainerFemale(RawImage png) => ImportTrainer(png, male: false);

        private string ImportTrainer(RawImage png, bool male)
        {
            if (!Available) return "Trainer card graphics archive is not available for this ROM.";
            if (png == null || png.IsEmpty) return "No image.";
            if (png.Width != TrainerWidth || png.Height != TrainerHeight)
                return $"Image must be exactly {TrainerWidth}x{TrainerHeight} (got {png.Width}x{png.Height}).";

            var t = TrainerCardTrainerMembers;
            var m = TrainerCardMembers;
            byte[] chrRaw = NitroBgCodec.Inflate(GetAndSnapshot(t.ncgr));
            byte[] maleRaw = NitroBgCodec.Inflate(GetAndSnapshot(t.maleNscr));
            byte[] femaleRaw = NitroBgCodec.Inflate(GetAndSnapshot(t.femaleNscr));
            byte[] palRaw = NitroBgCodec.Inflate(GetAndSnapshot(m.rankPalettes[0]));
            if (chrRaw == null || maleRaw == null || femaleRaw == null || palRaw == null)
                return "Could not read the current trainer pose.";

            RawImage malePng = male ? png : ComposeTrainer(true);
            RawImage femalePng = male ? ComposeTrainer(false) : png;
            if (malePng == null || femalePng == null) return "Could not decode the current trainer pose.";

            EncodedTiles maleTiles, femaleTiles;
            try
            {
                maleTiles = QuantizeAndTile(malePng, tileCols: TrainerWidth / 8, tileRows: TrainerHeight / 8,
                    tileCapacity: TrainerTileCapacity, maxColors: 255, reserveZero: true);
                femaleTiles = QuantizeAndTile(femalePng, tileCols: TrainerWidth / 8, tileRows: TrainerHeight / 8,
                    tileCapacity: TrainerTileCapacity, maxColors: 255, reserveZero: true);
            }
            catch (Exception ex) { return ex.Message; }

            int total = 1 + maleTiles.Colors.Count + femaleTiles.Colors.Count;
            if (total > 256)
                return $"Combined male + female pose needs {total} colours, only 256 are available.";

            int femaleBase = 1 + maleTiles.Colors.Count;
            var palette = new (byte r, byte g, byte b)[256];
            for (int i = 0; i < maleTiles.Colors.Count; i++) palette[1 + i] = maleTiles.Colors[i];
            for (int i = 0; i < femaleTiles.Colors.Count; i++) palette[femaleBase + i] = femaleTiles.Colors[i];

            var merged = MergeTilePools(maleTiles, femaleTiles, femaleBase, TrainerTileCapacity, reserveZero: true);
            if (merged == null)
                return $"Male + female pose needs more than {TrainerTileCapacity} unique 8x8 tiles once deduplicated. Simplify the images.";

            WriteTileData(chrRaw, merged.TileData);
            WriteMapData(maleRaw, merged.FrontMapEntries);
            WriteMapData(femaleRaw, merged.BackMapEntries);
            WritePalette(palRaw, palette);

            _narc.Put(t.ncgr, chrRaw);
            _narc.Put(t.maleNscr, maleRaw);
            _narc.Put(t.femaleNscr, femaleRaw);
            _narc.Put(m.rankPalettes[0], palRaw);
            return null;
        }

        // ── Raw rank palette (advanced) ─────────
        public string ImportRankPaletteRaw(int rankIndex, byte[] nclrBytes)
        {
            if (!Available) return "Trainer card graphics archive is not available for this ROM.";
            if (nclrBytes == null || nclrBytes.Length < 4 ||
                nclrBytes[0] != (byte)'R' || nclrBytes[1] != (byte)'L' || nclrBytes[2] != (byte)'C' || nclrBytes[3] != (byte)'N')
                return "Not a valid NCLR palette file.";
            int id = TrainerCardMembers.rankPalettes[rankIndex];
            GetAndSnapshot(id);
            _narc.Put(id, nclrBytes);
            return null;
        }

        public byte[] ExportRankPaletteRaw(int rankIndex) =>
            Available ? NitroBgCodec.Inflate(_narc.Get(TrainerCardMembers.rankPalettes[rankIndex])) : null;

        // ── Shared quantize + tile-dedup encoding ─────────
        private sealed class EncodedTiles
        {
            public List<(byte r, byte g, byte b)> Colors;
            public byte[] TileData;
            public ushort[] MapEntries;
        }

        private sealed class MergedTiles
        {
            public byte[] TileData;
            public ushort[] FrontMapEntries;
            public ushort[] BackMapEntries;
        }

        private static EncodedTiles QuantizeAndTile(RawImage png, int tileCols, int tileRows,
            int tileCapacity, int maxColors, bool reserveZero = false)
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

            var tileList = new List<byte[]>();
            var tileLookup = new Dictionary<string, int>();
            var mapEntries = new ushort[tileCols * tileRows];
            for (int ty = 0; ty < tileRows; ty++)
                for (int tx = 0; tx < tileCols; tx++)
                {
                    byte[] block = PackTile8bpp(raster, w, tx, ty);
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

            var tileData = new byte[tileCapacity * 64];
            for (int i = 0; i < tileList.Count; i++)
                Array.Copy(tileList[i], 0, tileData, i * 64, 64);

            return new EncodedTiles { Colors = colors, TileData = tileData, MapEntries = mapEntries };
        }

        /// <summary>Merges two tile pools into one shared character bank, deduplicating identical
        /// tiles. Returns null if capacity is exceeded. If reserveZero, index 0 never shifts.</summary>
        private static MergedTiles MergeTilePools(EncodedTiles first, EncodedTiles second, int secondShift,
            int tileCapacity, bool reserveZero)
        {
            var tileList = new List<byte[]>();
            var tileLookup = new Dictionary<string, int>();

            ushort[] AddPool(EncodedTiles pool, int shift)
            {
                int count = pool.MapEntries.Length;
                var outEntries = new ushort[count];
                for (int i = 0; i < count; i++)
                {
                    int oldIndex = pool.MapEntries[i];
                    var block = new byte[64];
                    Array.Copy(pool.TileData, oldIndex * 64, block, 0, 64);
                    if (shift != 0)
                        for (int b = 0; b < 64; b++)
                            if (!reserveZero || block[b] != 0) block[b] = (byte)(block[b] + shift);

                    string key = Convert.ToBase64String(block);
                    if (!tileLookup.TryGetValue(key, out int tileIndex))
                    {
                        if (tileList.Count >= tileCapacity) return null;
                        tileIndex = tileList.Count;
                        tileLookup[key] = tileIndex;
                        tileList.Add(block);
                    }
                    outEntries[i] = (ushort)tileIndex;
                }
                return outEntries;
            }

            ushort[] frontEntries = AddPool(first, 0);
            if (frontEntries == null) return null;
            ushort[] backEntries = AddPool(second, secondShift);
            if (backEntries == null) return null;

            var tileData = new byte[tileCapacity * 64];
            for (int i = 0; i < tileList.Count; i++)
                Array.Copy(tileList[i], 0, tileData, i * 64, 64);

            return new MergedTiles { TileData = tileData, FrontMapEntries = frontEntries, BackMapEntries = backEntries };
        }

        private static byte[] PackTile8bpp(byte[] raster, int w, int tx, int ty)
        {
            var block = new byte[64];
            for (int py = 0; py < 8; py++)
                for (int px = 0; px < 8; px++)
                    block[py * 8 + px] = raster[(ty * 8 + py) * w + (tx * 8 + px)];
            return block;
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
            int tileBytesOffset = NitroBgCodec.ReadTileHeader(memberRaw).TilesAt;
            Array.Copy(tiles, 0, memberRaw, tileBytesOffset, tiles.Length);
        }

        private static void WriteMapData(byte[] scrRaw, ushort[] mapEntries)
        {
            int mapDataOffset = NitroBgCodec.ReadScreenHeader(scrRaw).MapAt;
            for (int i = 0; i < mapEntries.Length; i++)
            {
                scrRaw[mapDataOffset + i * 2] = (byte)(mapEntries[i] & 0xFF);
                scrRaw[mapDataOffset + i * 2 + 1] = (byte)(mapEntries[i] >> 8);
            }
        }
    }
}
