using System;
using System.Collections.Generic;
using System.Linq;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.Data
{
    /// <summary>Rebuilds a background arrangement from a picture. Tiles and colours other screens show stay as they are.</summary>
    public sealed class ScreenLayerFit
    {
        /// <summary>One background in the book: drawing, an arrangement pasted into it, and the rows its screen loads from this colours file.</summary>
        public sealed record Layer(int Drawing, int Arrangement, IReadOnlyCollection<int> SharedRows);

        public sealed class Plan
        {
            public int Need, Room, Merged, NewColours, Squares;
            public bool Fits;
            public List<int> NewSquares = new();
            /// <summary>Squares showing a colour that didn't fit.</summary>
            public List<int> ChangedSquares = new();
            public List<(ushort Colour, ushort Shown, int Pixels)> Swaps = new();
            public bool CanKeep;
            internal byte[] Sheet, Screen, Palette;
        }

        private const int Tile = 8;

        private readonly ScriptNarc _narc;
        private readonly int _drawing, _arrangement, _coloursFile;
        private readonly byte[] _storedSheet, _storedScreen, _storedColours;
        private readonly byte[] _sheet, _screen, _colourFile;
        private readonly int _tilesAt, _mapAt, _entryBytes, _cols, _rows, _room, _fileRoom, _rowSize;
        private readonly bool _eightBit;
        private readonly ushort[] _colours;
        private readonly bool[] _keptTiles;
        private readonly bool[][] _lockedNumbers;
        private readonly int[] _rowsUsed;
        private readonly bool _screenView;
        // The colours each row shows on screen, where other files overwrite rows this file doesn't load, or null when it loads all of them.
        private readonly ushort[] _shown;
        private readonly IReadOnlyCollection<int> _loadedRows;

        public int Width => _cols * Tile;
        public int Height => _rows * Tile;
        /// <summary>The rows on screen: a background 256 tall shows its first 192. A background pasted in slices shows the whole file.</summary>
        public int ShownHeight => _screenView ? Math.Min(Height, 192) : Height;

        /// <param name="extended">The layer reads extended palettes, so 256-colour tiles are fine.</param>
        public static string WhyNot(ScriptNarc narc, int drawing, int arrangement, int colours, bool extended = false)
        {
            if (drawing < 0 || arrangement < 0 || colours < 0) return "This background is missing its drawing, arrangement or colours.";
            byte[] sheet = GraphicAssets.Unsqueeze(narc.Get(drawing)), screen = GraphicAssets.Unsqueeze(narc.Get(arrangement));
            if (sheet == null || screen == null) return "This background couldn't be read.";
            if (NitroBgCodec.ReadTileHeader(sheet).EightBit && !extended) return "Pictures can't be imported onto 256-colour backgrounds yet.";
            if (NitroBgCodec.EntryBytes(screen) != 2) return "Pictures can't be imported onto rotating or scaling backgrounds yet.";
            return null;
        }

        /// <param name="layers">Every background of every page, this one included.</param>
        /// <param name="textRows">Colour rows text prints in, per colours file.</param>
        /// <param name="loadedRows">Rows of the colours file the game loads, each into the same row on screen.</param>
        /// <param name="screenView">The arrangement is pasted whole at the screen's corner.</param>
        /// <param name="tileLimit">Tiles the layer has room for in VRAM, past the end of the file. 0 keeps the file's size.</param>
        /// <param name="shown">The colours on screen for rows other files fill, so pictures show what the game shows.</param>
        /// <param name="gameRows">Extended palettes whose first 16 colours the game rewrites while the screen is open.</param>
        public ScreenLayerFit(DirNames dir, int drawing, int arrangement, int coloursFile, IEnumerable<Layer> layers, IEnumerable<(int Colours, int Row)> textRows,
                              IReadOnlyCollection<int> loadedRows, bool screenView = true, int tileLimit = 0, ushort[] shown = null, IEnumerable<int> gameRows = null)
        {
            _screenView = screenView;
            _shown = shown;
            _loadedRows = loadedRows;
            _narc = new ScriptNarc(dir);
            _drawing = drawing;
            _arrangement = arrangement;
            _coloursFile = coloursFile;
            _storedSheet = _narc.Get(drawing);
            _storedScreen = _narc.Get(arrangement);
            _storedColours = _narc.Get(coloursFile);
            _sheet = GraphicAssets.Unsqueeze(_storedSheet);
            _screen = GraphicAssets.Unsqueeze(_storedScreen);
            _colourFile = GraphicAssets.Unsqueeze(_storedColours);
            (_eightBit, _tilesAt) = NitroBgCodec.ReadTileHeader(_sheet);
            _rowSize = _eightBit ? 256 : TileFit.RowSize;
            (int w, int h, int mapAt) = NitroBgCodec.ReadScreenHeader(_screen);
            (_mapAt, _cols, _rows, _entryBytes) = (mapAt, w / Tile, h / Tile, NitroBgCodec.EntryBytes(_screen));
            _colours = NitroBgCodec.ReadColours(_colourFile);
            _fileRoom = NitroBgCodec.TileRoom(_sheet);
            _room = Math.Max(_fileRoom, tileLimit);

            int rowCount = Math.Max(1, _colours.Length / _rowSize);
            _keptTiles = new bool[_room];
            if (_room > 0) _keptTiles[0] = true;
            _lockedNumbers = Enumerable.Range(0, rowCount).Select(_ => new bool[_rowSize]).ToArray();
            foreach (bool[] row in _lockedNumbers) row[0] = true;
            foreach ((int file, int row) in textRows)
                if (file == coloursFile && row >= 0 && row < rowCount) Array.Fill(_lockedNumbers[row], true);
            foreach (int row in gameRows ?? Array.Empty<int>())
                if (_eightBit && row >= 0 && row < rowCount) Array.Fill(_lockedNumbers[row], true, 0, TileFit.RowSize);

            Dictionary<int, byte[]> sheets = new();
            foreach (Layer l in layers)
            {
                if (l.Arrangement == arrangement && l.Drawing == drawing) continue;
                if (l.Drawing != drawing && l.SharedRows.Count == 0) continue;
                byte[] screen = GraphicAssets.Unsqueeze(_narc.Get(l.Arrangement));
                if (screen == null) continue;
                if (!sheets.TryGetValue(l.Drawing, out byte[] sheet)) sheets[l.Drawing] = sheet = GraphicAssets.Unsqueeze(_narc.Get(l.Drawing));
                if (sheet == null) continue;
                (bool wide, int at) = NitroBgCodec.ReadTileHeader(sheet);
                foreach (ushort e in NitroBgCodec.ReadMap(screen).Entries)
                {
                    if (l.Drawing == drawing && (e & 0x3FF) < _room) _keptTiles[e & 0x3FF] = true;
                    // Every layer on a screen shares its palette rows, whatever colours file the layer names.
                    if (l.SharedRows.Contains(e >> 12 & 15)) Lock(_lockedNumbers, e, sheet, at, wide);
                }
            }
            int[] own = Enumerable.Range(0, _rows * _cols).Select(Entry).Where(e => e >= 0).Select(e => e >> 12 & 15).Where(r => r < rowCount).Distinct().ToArray();
            // Rows nothing else on the screens shows can take the picture's colours too.
            // A row the file doesn't load shows another file's colours, so the picture can't write there.
            _rowsUsed = own.Where(loadedRows.Contains).Concat(Enumerable.Range(0, rowCount).Where(r => loadedRows.Contains(r) && _lockedNumbers[r].Skip(1).All(n => !n))).Distinct().OrderBy(r => r).ToArray();
            if (_rowsUsed.Length == 0) _rowsUsed = new[] { 0 };
        }

        private static void Lock(bool[][] locked, int entry, byte[] sheet, int tilesAt, bool eightBit)
        {
            int row = entry >> 12 & 15;
            if (row >= locked.Length) return;
            foreach (byte n in TileFit.Numbers(sheet, tilesAt, entry & 0x3FF, eightBit))
                if (n < locked[row].Length) locked[row][n] = true;
        }

        private int Entry(int square)
        {
            int index = NitroBgCodec.SquareIndex(_cols, square % _cols, square / _cols);
            return NitroBgCodec.EntryAt(_screen, _mapAt, index, _entryBytes);
        }

        /// <summary>Why a picture of this size can't be used, or null. A background 256 tall also takes the 192 rows on screen.</summary>
        public string SizeProblem(int width, int height) =>
            width == Width && (height == Height || _screenView && Height > 192 && height == 192) ? null
            : $"That picture is {width} by {height}. This background needs {Width} by {Height}" + (_screenView && Height > 192 ? $" or {Width} by 192." : ".");

        /// <summary>Works out what the picture needs. Nothing is written.</summary>
        /// <param name="keep">Colours to give a place first when a row runs out.</param>
        public Plan Make(byte[] rgba, int width, int height, int merge, IReadOnlyCollection<ushort> keep = null)
        {
            Plan plan = new Plan();
            List<int> squares = new();
            for (int ty = 0; ty < Math.Min(_rows, height / Tile); ty++)
                for (int tx = 0; tx < _cols; tx++) squares.Add(ty * _cols + tx);
            plan.Squares = squares.Count;

            // Squares the picture doesn't reach keep their tiles and colours.
            bool[] kept = (bool[])_keptTiles.Clone();
            bool[][] locked = _lockedNumbers.Select(r => (bool[])r.Clone()).ToArray();
            HashSet<int> covered = new HashSet<int>(squares);
            for (int s = 0; s < _rows * _cols; s++)
            {
                int e = Entry(s);
                if (covered.Contains(s) || e < 0) continue;
                if ((e & 0x3FF) < kept.Length) kept[e & 0x3FF] = true;
                Lock(locked, e, _sheet, _tilesAt, _eightBit);
            }

            int[][] pixels = squares.Select(s => SquarePixels(rgba, width, s % _cols, s / _cols)).ToArray();
            // Each square takes the row that already has most of its colours and still has room for the rest, so rows fill
            // one after another.
            int[] rowOf = new int[squares.Count];
            Dictionary<int, HashSet<int>> taken = _rowsUsed.ToDictionary(r => r, _ => new HashSet<int>());
            Dictionary<int, int> room = _rowsUsed.ToDictionary(r => r, r => locked[r].Skip(1).Count(n => !n));
            // Colours already at locked numbers in the row cost no room.
            Dictionary<int, HashSet<int>> fixedColours = _rowsUsed.ToDictionary(r => r, r => Enumerable.Range(1, _rowSize - 1)
                .Where(n => locked[r][n] && r * _rowSize + n < _colours.Length).Select(n => _colours[r * _rowSize + n] & 0x7FFF).ToHashSet());
            Dictionary<int, HashSet<int>> present = _rowsUsed.ToDictionary(r => r, r => Enumerable.Range(1, _rowSize - 1)
                .Where(n => r * _rowSize + n < _colours.Length).Select(n => _colours[r * _rowSize + n] & 0x7FFF).ToHashSet());
            Dictionary<int, int> spent = _rowsUsed.ToDictionary(r => r, _ => 0);
            for (int i = 0; i < squares.Count; i++)
            {
                int now = Entry(squares[i]) >> 12 & 15;
                HashSet<int> want = pixels[i].Where(c => c >= 0).ToHashSet();
                // Colours the row already shows cost nothing to prefer. Any not on a locked number still takes a number.
                int Missing(int r) => want.Count(c => !taken[r].Contains(c) && !present[r].Contains(c));
                int Cost(int r) => want.Count(c => !taken[r].Contains(c) && !fixedColours[r].Contains(c));
                rowOf[i] = _rowsUsed.OrderBy(r => spent[r] + Cost(r) <= room[r] ? 0 : 1).ThenBy(Missing).ThenBy(r => r == now ? 0 : 1).First();
                spent[rowOf[i]] += Cost(rowOf[i]);
                taken[rowOf[i]].UnionWith(want);
            }

            Dictionary<int, TileFit.RowColours> sets = new();
            ushort[] colours = (ushort[])_colours.Clone();
            foreach (int row in _rowsUsed)
            {
                TileFit.RowColours set = new TileFit.RowColours(_colours, _colours, row, locked[row], keep);
                for (int i = 0; i < squares.Count; i++)
                    if (rowOf[i] == row) foreach (int c in pixels[i]) set.Want(c);
                set.Settle();
                sets[row] = set;
                plan.Swaps.AddRange(set.Swaps);
                plan.CanKeep |= set.Open > 0 && set.Refused > 0;
                foreach ((int number, ushort colour) in set.Added)
                {
                    if ((colours[row * _rowSize + number] & 0x7FFF) != colour) plan.NewColours++;
                    colours[row * _rowSize + number] = colour;
                }
            }

            int Number(int row, int c) => c < 0 ? 0 : sets[row].Number(c);
            List<TileFit.Square> fit = new();
            for (int i = 0; i < squares.Count; i++)
            {
                int row = rowOf[i], before = Entry(squares[i]);
                // A colour can sit at two numbers, so a pixel keeps its old number while that still shows its colour.
                byte[] old = before >= 0 && (before >> 12) == row ? TileFit.Flip(TileFit.Numbers(_sheet, _tilesAt, before & 0x3FF, _eightBit), before & 0xC00) : null;
                fit.Add(new TileFit.Square { Was = old != null ? before & 0xFFF : -1, Sheets = new[] { pixels[i].Select((c, k) => (byte)(old != null && c >= 0 && Shows(colours, row, old[k], c, _rowSize) ? old[k] : Number(row, c))).ToArray() } });
            }
            Dictionary<int, HashSet<int>> swapped = sets.ToDictionary(p => p.Key, p => p.Value.Swaps.Select(w => (int)w.Colour).ToHashSet());
            for (int i = 0; i < squares.Count; i++)
                if (pixels[i].Any(swapped[rowOf[i]].Contains)) plan.ChangedSquares.Add(squares[i]);

            List<int> free = Enumerable.Range(0, _room).Where(t => !kept[t]).ToList();
            IEnumerable<(int, byte[][])> keptTiles = Enumerable.Range(0, _room).Where(t => kept[t]).Select(t => (t, new[] { TileFit.Numbers(_sheet, _tilesAt, t) }));
            TileFit.Placement placed = TileFit.Place(fit, keptTiles, free, merge, free.Select(t => (t, new[] { TileFit.Numbers(_sheet, _tilesAt, t) })));
            (plan.Need, plan.Room, plan.Merged, plan.Fits) = (placed.Need, placed.Room, placed.Merged, placed.Fits);
            if (!plan.Fits) return plan;

            int tiles = placed.NewTiles.Select(t => t.Slot + 1).DefaultIfEmpty(0).Max();
            plan.Sheet = tiles > _fileRoom ? Grown(_sheet, tiles) : (byte[])_sheet.Clone();
            foreach ((int slot, byte[][] sheets) in placed.NewTiles) TileFit.Put(plan.Sheet, _tilesAt, slot, sheets[0], _eightBit);
            plan.Screen = (byte[])_screen.Clone();
            for (int i = 0; i < squares.Count; i++)
            {
                (int tile, int flip) = placed.Squares[i];
                int entry = rowOf[i] << 12 | flip | tile;
                if (!SameLook(Entry(squares[i]), _sheet, entry, plan.Sheet)) plan.NewSquares.Add(squares[i]);
                NitroBgCodec.PutEntry(plan.Screen, _mapAt, NitroBgCodec.SquareIndex(_cols, squares[i] % _cols, squares[i] / _cols), _entryBytes, entry);
            }
            plan.Palette = (byte[])_colourFile.Clone();
            foreach ((int row, TileFit.RowColours set) in sets)
                foreach ((int number, ushort colour) in set.Added) NitroBgCodec.PutColour(plan.Palette, row * _rowSize + number, colour);
            return plan;
        }

        private bool SameLook(int before, byte[] oldSheet, int after, byte[] newSheet)
        {
            if (before < 0 || (before >> 12) != (after >> 12)) return false;
            byte[] a = TileFit.Flip(TileFit.Numbers(oldSheet, _tilesAt, before & 0x3FF, _eightBit), before & 0xC00);
            byte[] b = TileFit.Flip(TileFit.Numbers(newSheet, _tilesAt, after & 0x3FF, _eightBit), after & 0xC00);
            return a.AsSpan().SequenceEqual(b);
        }

        private static bool Shows(ushort[] colours, int row, int number, int colour, int rowSize) =>
            row * rowSize + number < colours.Length && (colours[row * rowSize + number] & 0x7FFF) == colour;

        // The drawing with room for this many tiles. The headers count the new length.
        private byte[] Grown(byte[] sheet, int tiles)
        {
            NitroBgCodec.DrawingHeader head = NitroBgCodec.ReadDrawingHeader(sheet).Value;
            byte[] data = new byte[tiles * (_eightBit ? 64 : 32)];
            Array.Copy(sheet, head.DataAt, data, 0, Math.Min(data.Length, Math.Min(head.DataSize, sheet.Length - head.DataAt)));
            byte[] grown = NitroBgCodec.WithCharacterData(sheet, head, data);
            // A drawing that records its size in tiles keeps 32 across.
            if (head.TilesAcross > 0 && head.TilesDown > 0)
            {
                BitConverter.GetBytes((short)((tiles + 31) / 32)).CopyTo(grown, head.SectionAt + 0x08);
                BitConverter.GetBytes((short)32).CopyTo(grown, head.SectionAt + 0x0A);
            }
            return grown;
        }

        private static int[] SquarePixels(byte[] rgba, int width, int tx, int ty)
        {
            int[] p = new int[TileFit.Pixels];
            for (int i = 0; i < p.Length; i++)
            {
                int at = ((ty * Tile + i / Tile) * width + tx * Tile + i % Tile) * 4;
                p[i] = TileFit.Colour(rgba, at);
            }
            return p;
        }

        /// <summary>The background now, or with the plan applied.</summary>
        public byte[] Picture(Plan plan = null)
        {
            ushort[] colours = NitroBgCodec.ReadColours(plan?.Palette ?? _colourFile);
            if (_shown != null)
            {
                Array.Resize(ref colours, Math.Max(colours.Length, _shown.Length));
                for (int row = 0; row * _rowSize < _shown.Length; row++)
                    if (!_loadedRows.Contains(row)) Array.Copy(_shown, row * _rowSize, colours, row * _rowSize, Math.Min(_rowSize, _shown.Length - row * _rowSize));
            }
            (byte r, byte g, byte b)[] rgb = colours.Select(c => NitroBgCodec.Colour(c)).ToArray();
            byte[] rgba = NitroBgCodec.Composite(plan?.Sheet ?? _sheet, rgb, rgb.Length, plan?.Screen ?? _screen, transparentZero: false, extended: _eightBit).Rgba;
            return rgba == null ? null : rgba[..(Width * ShownHeight * 4)];
        }

        public int Drawing => _drawing;
        public int Arrangement => _arrangement;
        public int ColoursFile => _coloursFile;

        /// <summary>The files the plan writes, by member, for a preview that draws them before anything is saved.</summary>
        public Func<int, byte[]> Replacement(Plan plan) => member =>
            plan?.Sheet == null ? null : member == _drawing ? plan.Sheet : member == _arrangement ? plan.Screen : member == _coloursFile ? plan.Palette : null;

        /// <summary>Writes the plan through whatever staging is in use.</summary>
        public string Apply(Plan plan)
        {
            if (plan?.Fits != true || plan.Sheet == null) return "This picture doesn't fit the background.";
            Put(_drawing, plan.Sheet, _storedSheet);
            Put(_arrangement, plan.Screen, _storedScreen);
            Put(_coloursFile, plan.Palette, _storedColours);
            return null;
        }

        private void Put(int member, byte[] plain, byte[] stored)
        {
            byte marker = GraphicAssets.SqueezeMarker(stored);
            _narc.Put(member, marker != 0 ? GraphicAssets.Squeeze(plain, marker) : plain);
        }
    }
}
