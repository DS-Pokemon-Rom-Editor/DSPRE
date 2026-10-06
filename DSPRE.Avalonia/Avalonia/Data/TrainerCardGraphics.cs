using System;
using System.Collections.Generic;
using System.IO;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.Data
{
    /// <summary>Trainer card face design, 7 rank palettes, and male/female trainer-pose overlay.</summary>
    public sealed class TrainerCardGraphics
    {
        public const int CardWidth = 256, CardHeight = 192;
        public const int TrainerWidth = 256, TrainerHeight = 192;

        private const int CardTileCols = 32, CardTileRows = 24;

        // Limited by the drawing file's room and by the tile numbers each arrangement can store.
        private static int TileCapacity(byte[] chr, params byte[][] screens)
        {
            int capacity = NitroBgCodec.TileRoom(chr);
            foreach (byte[] scr in screens)
                capacity = Math.Min(capacity, NitroBgCodec.EntryBytes(scr) == 1 ? 256 : 1024);
            return capacity;
        }

        private readonly ScriptNarc _narc = new(DirNames.trainerCardGraphics);
        public bool Available => _narc.Available;

        public static string[] RankNames => TrainerCardRankNames;

        private readonly Dictionary<int, byte[]> _backup = new();
        public bool HasChanges
        {
            get
            {
                foreach (KeyValuePair<int, byte[]> kv in _backup)
                    if (!(_narc.Get(kv.Key) ?? Array.Empty<byte>()).AsSpan().SequenceEqual(kv.Value)) return true;
                return false;
            }
        }

        // Clones on first read: Inflate() can return the same array we later mutate in place.
        private byte[] GetAndSnapshot(int id)
        {
            History.Touching(id);
            return Backup(id);
        }

        private byte[] Backup(int id)
        {
            byte[] raw = _narc.Get(id);
            if (raw != null && !_backup.ContainsKey(id)) _backup[id] = (byte[])raw.Clone();
            return raw;
        }

        /// <summary>Each import as an undo step; the owner commits after an import attempt.</summary>
        public MemberEditUndo History { get; }

        public TrainerCardGraphics()
        {
            History = new MemberEditUndo(id => _narc.Get(id), (id, bytes) => { Backup(id); _narc.Put(id, bytes); });
        }

        /// <summary>After a save: the current files become what Discard goes back to.</summary>
        public void AcceptAll() => _backup.Clear();

        public void RevertAll()
        {
            foreach (KeyValuePair<int, byte[]> kv in _backup) _narc.Put(kv.Key, kv.Value);
            _backup.Clear();
            History.Clear();
        }

        // ── Decode ───────────────────────────────────────────────────────────
        public RawImage ComposeCardFront(int rankIndex) => ComposeCard(rankIndex, front: true);
        public RawImage ComposeCardBack(int rankIndex) => ComposeCard(rankIndex, front: false);

        private RawImage ComposeCard(int rankIndex, bool front)
        {
            (int ncgr, int facaNscr, int backNscr, int[] rankPalettes) m = TrainerCardMembers;
            int palId = m.rankPalettes[rankIndex];
            int scrId = front ? m.facaNscr : m.backNscr;
            return ComposeVia(m.ncgr, palId, scrId, transparentZero: false);
        }

        public RawImage ComposeTrainer(bool male)
        {
            (int ncgr, int maleNscr, int femaleNscr) t = TrainerCardTrainerMembers;
            (int ncgr, int facaNscr, int backNscr, int[] rankPalettes) m = TrainerCardMembers;
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

        // The screens are 32x32 tiles but the DS shows the top 24 rows, which is also what an import takes.
        private static RawImage ToRawImage(NitroBgCodec.BgImage bg)
        {
            int height = Math.Min(bg.Height, CardHeight);
            RawImage raw = new RawImage(bg.Width, height);
            byte[] src = bg.Rgba, dst = raw.Bgra;
            for (int i = 0; i + 3 < dst.Length && i + 3 < src.Length; i += 4)
            { dst[i] = src[i + 2]; dst[i + 1] = src[i + 1]; dst[i + 2] = src[i]; dst[i + 3] = src[i + 3]; }
            return raw;
        }

        // ── Card design (shared NCGR, rebuilds all 7 rank palettes) ──────────────────
        // Measured on retail DP, Platinum and HeartGold: the card draws with indices 0-63 (HeartGold also 96-111)
        // and the trainer pose with 65-95, in every rank's palette. Each import keeps to its own slots.
        private const int CardColourSlots = 64;
        private const int PoseFirstSlot = 65, PoseColourSlots = 31;

        public string ImportCardFront(RawImage png, IReadOnlyCollection<int> ranks) => ImportCardDesign(png, front: true, ranks);
        public string ImportCardBack(RawImage png, IReadOnlyCollection<int> ranks) => ImportCardDesign(png, front: false, ranks);

        private string ImportCardDesign(RawImage png, bool front, IReadOnlyCollection<int> ranks)
        {
            if (!Available) return "Trainer card graphics archive is not available for this ROM.";
            if (ranks == null || ranks.Count == 0) return "Pick at least one rank for the design's colours.";
            if (png == null || png.IsEmpty) return "No image.";
            if (png.Width != CardWidth || png.Height != CardHeight)
                return $"Image must be exactly {CardWidth}x{CardHeight} (got {png.Width}x{png.Height}).";

            (int ncgr, int facaNscr, int backNscr, int[] rankPalettes) m = TrainerCardMembers;
            byte[] chrRaw = NitroBgCodec.Inflate(GetAndSnapshot(m.ncgr));
            byte[] facaRaw = NitroBgCodec.Inflate(GetAndSnapshot(m.facaNscr));
            byte[] backRaw = NitroBgCodec.Inflate(GetAndSnapshot(m.backNscr));
            if (chrRaw == null || facaRaw == null || backRaw == null)
                return "Could not read the current card design.";

            RawImage frontPng = front ? png : ComposeCardFront(0);
            RawImage backPng = front ? ComposeCardBack(0) : png;
            if (frontPng == null || backPng == null) return "Could not decode the current card design.";

            int capacity = TileCapacity(chrRaw, facaRaw, backRaw);
            EncodedTiles frontTiles, backTiles;
            try
            {
                frontTiles = QuantizeAndTile(frontPng, tileCols: CardTileCols, tileRows: CardTileRows,
                    tileCapacity: capacity, maxColors: 256);
                backTiles = QuantizeAndTile(backPng, tileCols: CardTileCols, tileRows: CardTileRows,
                    tileCapacity: capacity, maxColors: 256);
            }
            catch (Exception ex) { return ex.Message; }

            int total = frontTiles.Colors.Count + backTiles.Colors.Count;
            if (total > CardColourSlots)
                return $"The front and back use {total} colours between them; the card has room for {CardColourSlots}.";

            int backBase = frontTiles.Colors.Count;
            (byte r, byte g, byte b)[] palette = new (byte r, byte g, byte b)[total];
            for (int i = 0; i < frontTiles.Colors.Count; i++) palette[i] = frontTiles.Colors[i];
            for (int i = 0; i < backTiles.Colors.Count; i++) palette[backBase + i] = backTiles.Colors[i];

            MergedTiles merged = MergeTilePools(frontTiles, backTiles, backBase, capacity, reserveZero: false);
            if (merged == null)
                return $"Front + back design needs more than {capacity} unique 8x8 tiles once deduplicated. Simplify the images.";

            if (!WriteMapData(facaRaw, merged.FrontMapEntries) || !WriteMapData(backRaw, merged.BackMapEntries))
                return "The card's arrangement files are too small for a whole card.";
            WriteTileData(chrRaw, merged.TileData);

            Dictionary<int, byte[]> palettes = new Dictionary<int, byte[]>();
            foreach (int rank in ranks)
                if (rank >= 0 && rank < m.rankPalettes.Length && NitroBgCodec.Inflate(GetAndSnapshot(m.rankPalettes[rank])) is byte[] raw)
                    palettes[m.rankPalettes[rank]] = raw;
            try { foreach (byte[] raw in palettes.Values) WritePalette(raw, palette, 0); }
            catch (InvalidDataException ex) { return ex.Message; }

            _narc.Put(m.ncgr, chrRaw);
            _narc.Put(m.facaNscr, facaRaw);
            _narc.Put(m.backNscr, backRaw);
            foreach (KeyValuePair<int, byte[]> kv in palettes) _narc.Put(kv.Key, kv.Value);
            return null;
        }

        // ── Trainer pose (shared NCGR, recolours the pose's slots in every rank's palette) ───────────
        public string ImportTrainerMale(RawImage png) => ImportTrainer(png, male: true);
        public string ImportTrainerFemale(RawImage png) => ImportTrainer(png, male: false);

        private string ImportTrainer(RawImage png, bool male)
        {
            if (!Available) return "Trainer card graphics archive is not available for this ROM.";
            if (png == null || png.IsEmpty) return "No image.";
            if (png.Width != TrainerWidth || png.Height != TrainerHeight)
                return $"Image must be exactly {TrainerWidth}x{TrainerHeight} (got {png.Width}x{png.Height}).";

            (int ncgr, int maleNscr, int femaleNscr) t = TrainerCardTrainerMembers;
            (int ncgr, int facaNscr, int backNscr, int[] rankPalettes) m = TrainerCardMembers;
            byte[] chrRaw = NitroBgCodec.Inflate(GetAndSnapshot(t.ncgr));
            byte[] maleRaw = NitroBgCodec.Inflate(GetAndSnapshot(t.maleNscr));
            byte[] femaleRaw = NitroBgCodec.Inflate(GetAndSnapshot(t.femaleNscr));
            byte[] palRaw = NitroBgCodec.Inflate(GetAndSnapshot(m.rankPalettes[0]));
            if (chrRaw == null || maleRaw == null || femaleRaw == null || palRaw == null)
                return "Could not read the current trainer pose.";

            RawImage malePng = male ? png : ComposeTrainer(true);
            RawImage femalePng = male ? ComposeTrainer(false) : png;
            if (malePng == null || femalePng == null) return "Could not decode the current trainer pose.";

            int capacity = TileCapacity(chrRaw, maleRaw, femaleRaw);
            EncodedTiles maleTiles, femaleTiles;
            try
            {
                maleTiles = QuantizeAndTile(malePng, tileCols: TrainerWidth / 8, tileRows: TrainerHeight / 8,
                    tileCapacity: capacity, maxColors: 255, reserveZero: true);
                femaleTiles = QuantizeAndTile(femalePng, tileCols: TrainerWidth / 8, tileRows: TrainerHeight / 8,
                    tileCapacity: capacity, maxColors: 255, reserveZero: true);
            }
            catch (Exception ex) { return ex.Message; }

            int total = maleTiles.Colors.Count + femaleTiles.Colors.Count;
            if (total > PoseColourSlots)
                return $"The two poses use {total} colours between them; the pose has room for {PoseColourSlots}.";

            // Each pose is numbered from index 1; both are shifted up into the pose's slots.
            (byte r, byte g, byte b)[] palette = new (byte r, byte g, byte b)[total];
            for (int i = 0; i < maleTiles.Colors.Count; i++) palette[i] = maleTiles.Colors[i];
            for (int i = 0; i < femaleTiles.Colors.Count; i++) palette[maleTiles.Colors.Count + i] = femaleTiles.Colors[i];

            MergedTiles merged = MergeTilePools(maleTiles, femaleTiles, PoseFirstSlot - 1 + maleTiles.Colors.Count, capacity,
                reserveZero: true, firstShift: PoseFirstSlot - 1);
            if (merged == null)
                return $"Male + female pose needs more than {capacity} unique 8x8 tiles once deduplicated. Simplify the images.";

            if (!WriteMapData(maleRaw, merged.FrontMapEntries) || !WriteMapData(femaleRaw, merged.BackMapEntries))
                return "The trainer's arrangement files are too small for a whole pose.";
            WriteTileData(chrRaw, merged.TileData);

            // Every rank's palette carries the pose's colours, so a card of any rank shows the new pose.
            Dictionary<int, byte[]> palettes = new Dictionary<int, byte[]> { [m.rankPalettes[0]] = palRaw };
            for (int rank = 1; rank < m.rankPalettes.Length; rank++)
                if (NitroBgCodec.Inflate(GetAndSnapshot(m.rankPalettes[rank])) is byte[] other) palettes[m.rankPalettes[rank]] = other;
            try { foreach (byte[] raw in palettes.Values) WritePalette(raw, palette, PoseFirstSlot); }
            catch (InvalidDataException ex) { return ex.Message; }

            _narc.Put(t.ncgr, chrRaw);
            _narc.Put(t.maleNscr, maleRaw);
            _narc.Put(t.femaleNscr, femaleRaw);
            foreach (KeyValuePair<int, byte[]> kv in palettes) _narc.Put(kv.Key, kv.Value);
            return null;
        }

        // ── Raw rank palette (advanced) ─────────
        public string ImportRankPaletteRaw(int rankIndex, byte[] nclrBytes)
        {
            if (!Available) return "Trainer card graphics archive is not available for this ROM.";
            if (nclrBytes == null || nclrBytes.Length < 4 ||
                nclrBytes[0] != (byte)'R' || nclrBytes[1] != (byte)'L' || nclrBytes[2] != (byte)'C' || nclrBytes[3] != (byte)'N')
                return "Not a valid colour file.";
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
                                throw new InvalidOperationException($"Image uses more than {maxColors} distinct colours.");
                            v = (byte)(colors.Count + (reserveZero ? 1 : 0));
                            colorToIndex[key] = v;
                            colors.Add((r, g, b));
                        }
                    }
                    raster[y * w + x] = v;
                }

            List<byte[]> tileList = new List<byte[]>();
            Dictionary<string, int> tileLookup = new Dictionary<string, int>();
            ushort[] mapEntries = new ushort[tileCols * tileRows];
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

            byte[] tileData = new byte[tileCapacity * 64];
            for (int i = 0; i < tileList.Count; i++)
                Array.Copy(tileList[i], 0, tileData, i * 64, 64);

            return new EncodedTiles { Colors = colors, TileData = tileData, MapEntries = mapEntries };
        }

        /// <summary>Merges two tile pools into one shared character bank, deduplicating identical
        /// tiles. Returns null if capacity is exceeded. If reserveZero, index 0 never shifts.</summary>
        private static MergedTiles MergeTilePools(EncodedTiles first, EncodedTiles second, int secondShift,
            int tileCapacity, bool reserveZero, int firstShift = 0)
        {
            List<byte[]> tileList = new List<byte[]>();
            Dictionary<string, int> tileLookup = new Dictionary<string, int>();

            ushort[] AddPool(EncodedTiles pool, int shift)
            {
                int count = pool.MapEntries.Length;
                ushort[] outEntries = new ushort[count];
                for (int i = 0; i < count; i++)
                {
                    int oldIndex = pool.MapEntries[i];
                    byte[] block = new byte[64];
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

            ushort[] frontEntries = AddPool(first, firstShift);
            if (frontEntries == null) return null;
            ushort[] backEntries = AddPool(second, secondShift);
            if (backEntries == null) return null;

            byte[] tileData = new byte[tileCapacity * 64];
            for (int i = 0; i < tileList.Count; i++)
                Array.Copy(tileList[i], 0, tileData, i * 64, 64);

            return new MergedTiles { TileData = tileData, FrontMapEntries = frontEntries, BackMapEntries = backEntries };
        }

        private static byte[] PackTile8bpp(byte[] raster, int w, int tx, int ty)
        {
            byte[] block = new byte[64];
            for (int py = 0; py < 8; py++)
                for (int px = 0; px < 8; px++)
                    block[py * 8 + px] = raster[(ty * 8 + py) * w + (tx * 8 + px)];
            return block;
        }

        private static void WritePalette(byte[] palRaw, (byte r, byte g, byte b)[] palette, int firstSlot)
        {
            int pltt = NitroBgCodec.Find(palRaw, "TTLP", 0);
            if (pltt < 0) throw new InvalidDataException("The colour file has no palette block.");
            int dataOffset = pltt + 0x18 + firstSlot * 2;
            for (int i = 0; i < palette.Length && dataOffset + i * 2 + 1 < palRaw.Length; i++)
            {
                (byte r, byte g, byte b) = palette[i];
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

        // Checks the file is long enough before writing any square.
        private static bool WriteMapData(byte[] scrRaw, ushort[] mapEntries)
        {
            int mapDataOffset = NitroBgCodec.ReadScreenHeader(scrRaw).MapAt;
            int entryBytes = NitroBgCodec.EntryBytes(scrRaw);
            if (mapDataOffset + mapEntries.Length * entryBytes > scrRaw.Length) return false;
            for (int i = 0; i < mapEntries.Length; i++)
                if (!NitroBgCodec.PutEntry(scrRaw, mapDataOffset, i, entryBytes, mapEntries[i])) return false;
            return true;
        }
    }
}
