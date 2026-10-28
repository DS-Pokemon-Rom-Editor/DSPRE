using System;
using System.Collections.Generic;
using System.Linq;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// Screens run by the shared grid input handler (pokeheartgold unk_02019BA4.c): per page a touch list and a cursor table
    /// of 8-byte entries, x, y, width, height, then the stop Up, Down, Left and Right go to. The HGSS PC and Pokédex use it.
    /// </summary>
    public sealed class GridMenuButtons : IScreenButtons, IScreenArt
    {
        /// <param name="Touch">The touch list's offset in the overlay file.</param>
        /// <param name="Flow">The cursor table's offset, or -1 for a touch-only page.</param>
        /// <param name="Locked">Buttons whose touch areas must stay, and why.</param>
        /// <param name="Fixed">Arrows the game's code decides, and why.</param>
        /// <param name="Disabled">Stops the game switches off at times; the cursor runs past them.</param>
        /// <param name="Spots">Picture spots that move with their buttons, as in <see cref="TouchListButtons"/>.</param>
        /// <param name="Tiles">A background screen whose art moves with some buttons.</param>
        /// <param name="Code">Tile spots the code loads with Thumb instructions.</param>
        public sealed record Page(string Name, string[] Buttons, int Touch, int Flow,
                                  IReadOnlyDictionary<int, string> Locked = null,
                                  IReadOnlyDictionary<(int Stop, CursorDirection Direction), string> Fixed = null,
                                  int[][] Disabled = null, TouchListButtons.Spots[] Spots = null, ScreenTiles Tiles = null, CodeSpot[] Code = null);

        /// <summary>
        /// A button's x and y in tiles as the code loads them: each a <c>mov rd, #n</c>, or an <c>add rd, r1, #0</c> that copies x.
        /// <paramref name="Keep"/> is a second copy of x that stays at the old value when x moves.
        /// </summary>
        public sealed record CodeSpot(int Button, int X, int Y, int Keep = -1);

        private const int Entry = 8, Tile = 8;

        private readonly Page[] _pages;
        private readonly ScreenTables _tables;
        private readonly (int Touch, int Flow)[] _starts;
        private readonly List<int>[] _spotStarts;
        private readonly ScreenTileArt[] _art;
        private readonly Dictionary<int, int> _code = new Dictionary<int, int>();
        private readonly byte[] _data;

        private GridMenuButtons(int overlay, Page[] pages, ScreenTables tables)
        {
            _pages = pages;
            _tables = tables;
            _starts = new (int, int)[pages.Length];
            _spotStarts = new List<int>[pages.Length];
            _art = new ScreenTileArt[pages.Length];
            for (int i = 0; i < pages.Length; i++)
            {
                int touch = tables.Take(overlay, pages[i].Touch, pages[i].Buttons.Length * TouchRects.Size);
                int flow = pages[i].Flow >= 0 ? tables.Take(overlay, pages[i].Flow, pages[i].Buttons.Length * Entry) : tables.Count;
                _starts[i] = (touch, flow);
                _spotStarts[i] = (pages[i].Spots ?? Array.Empty<TouchListButtons.Spots>()).Select(s => tables.Take(overlay, s.At, s.Count * s.Stride)).ToList();
                foreach (int at in (pages[i].Code ?? Array.Empty<CodeSpot>()).SelectMany(c => new[] { c.X, c.Y, c.Keep }).Where(a => a >= 0))
                    if (!_code.ContainsKey(at)) _code[at] = tables.Take(overlay, at, 2);
                _art[i] = ScreenTileArt.Take(pages[i].Tiles, tables);
            }
            _data = tables.Finish();
            for (int i = 0; i < pages.Length; i++)
            {
                int page = i;
                _art[i]?.Remember(_data, b => AreaOf(page, b));
            }
        }

        /// <summary>Null when a page's tables don't look like the game's.</summary>
        public static GridMenuButtons Read(int overlay, Page[] pages)
        {
            ScreenTables tables = new ScreenTables();
            byte[] file = tables.FileOf(overlay);
            if (file == null) return null;
            foreach (Page p in pages)
            {
                int n = p.Buttons.Length, end = p.Touch + n * TouchRects.Size;
                // Each touch list ends where the game's does, and every neighbour is one of the page's stops.
                if (p.Touch < 0 || end >= file.Length || file[end] != 0xFF) return null;
                foreach (int at in (p.Code ?? Array.Empty<CodeSpot>()).SelectMany(c => new[] { c.X, c.Y, c.Keep }).Where(a => a >= 0))
                    if (at + 2 > file.Length || !LoadsTile(BitConverter.ToUInt16(file, at))) return null;
                if (p.Flow < 0) continue;
                if (p.Flow + n * Entry > file.Length) return null;
                for (int i = 0; i < n; i++)
                    for (int d = 0; d < 4; d++)
                        if ((file[p.Flow + i * Entry + 4 + d] & ~CursorDirections.Back) >= n) return null;
            }
            GridMenuButtons buttons = new GridMenuButtons(overlay, pages, tables);
            return tables.Missing ? null : buttons;
        }

        public IReadOnlyList<string> PageNames => _pages.Select(p => p.Name).ToList();
        public string[] ButtonsOf(int page) => _pages[page].Buttons;
        public int StopsOf(int page) => _pages[page].Flow >= 0 ? _pages[page].Buttons.Length : 0;

        private int TouchAt(int page, int button) => _starts[page].Touch + button * TouchRects.Size;
        private int StopAt(int page, int stop) => _starts[page].Flow + stop * Entry;

        public (int Top, int Bottom, int Left, int Right) AreaOf(int page, int button) => TouchRects.Read(_data, TouchAt(page, button));

        public string MoveLocked(int page, int button) =>
            _pages[page].Locked != null && _pages[page].Locked.TryGetValue(button, out string why) ? why : null;

        private IEnumerable<TouchListButtons.Spots> SpotsFor(int page, int button) =>
            (_pages[page].Spots ?? Array.Empty<TouchListButtons.Spots>()).Where(s => button - s.FirstButton >= 0 && button - s.FirstButton < s.Count);

        public int MoveStep(int page, int button) =>
            _art[page]?.Moves(button) == true ? Tile : SpotsFor(page, button).Select(s => s.Unit).DefaultIfEmpty(1).Max();

        /// <summary>The page's background screen as edited, or null when it has none.</summary>
        public byte[] ScreenOf(int page) => _art[page]?.Screen(_data);

        public bool HasArt(int page) => _art[page] != null;
        public ushort? SquareAt(int page, int x, int y) => _art[page]?.SquareAt(_data, x, y);
        public void SetSquare(int page, int x, int y, ushort square) => _art[page]?.SetSquare(_data, x, y, square);

        /// <summary>The page's tile sheet with the tiles its screen added, and the sheet's archive member. Null when it added none.</summary>
        public (byte[] Sheet, int Member)? SheetOf(int page) =>
            _art[page]?.SheetOf() is byte[] sheet ? (sheet, _art[page].Spec.Bands.Characters) : null;

        private static bool IsCopy(ushort op) => op >> 6 == 0b0001110000 && (op >> 3 & 7) == 1;
        private static bool LoadsTile(ushort op) => op >> 11 == 0b00100 || IsCopy(op);

        private ushort Op(int at) => BitConverter.ToUInt16(_data, _code[at]);

        // An add rd, r1, #0 reports the x it copies.
        private int Loaded(int at, int x) => IsCopy(Op(at)) ? x : Op(at) & 0xFF;

        private void Load(int at, int value)
        {
            ushort op = Op(at);
            int rd = IsCopy(op) ? op & 7 : op >> 8 & 7;
            BitConverter.TryWriteBytes(_data.AsSpan(_code[at]), (ushort)(0x2000 | rd << 8 | Math.Clamp(value, 0, 255)));
        }

        /// <summary>The page's byte spots from <paramref name="table"/>, in its units, or null.</summary>
        public IReadOnlyList<(int X, int Y)> SpotsOf(int page, int table)
        {
            TouchListButtons.Spots[] all = _pages[page].Spots;
            if (all == null || table >= all.Length) return null;
            TouchListButtons.Spots s = all[table];
            int At(int i) => _spotStarts[page][table] + i * s.Stride + s.X;
            return Enumerable.Range(0, s.Count).Select(i => ((int)_data[At(i)], s.XOnly ? 0 : (int)_data[At(i) + 1])).ToList();
        }

        // The cursor sits at the entry's x and y, so it moves as far as the area.
        public void Move(int page, int button, int dx, int dy)
        {
            if (MoveLocked(page, button) != null) return;
            int step = MoveStep(page, button);
            (int top, int bottom, int left, int right) = AreaOf(page, button);
            ScreenTileArt art = _art[page] is ScreenTileArt a && a.Moves(button) ? a : null;
            // A band button only slides along its band, between its label and other art.
            if (art != null && art.Slides(button))
            {
                (int least, int most) = art.Room(button);
                int from = (left - art.WasLeft(button)) / Tile;
                (dx, dy) = ((Math.Clamp(from + (int)Math.Round(dx / (double)Tile), least, most) - from) * Tile, 0);
            }
            (dx, dy) = TouchRects.Move(_data, TouchAt(page, button), (int)Math.Round(dx / (double)step) * step, (int)Math.Round(dy / (double)step) * step);
            // When an edge stops the area, it goes back to the last whole step.
            if (dx % step != 0 || dy % step != 0)
            {
                TouchRects.Move(_data, TouchAt(page, button), -(dx % step), -(dy % step));
                (dx, dy) = (dx - dx % step, dy - dy % step);
            }
            art?.Compose(_data, b => AreaOf(page, b));
            foreach (CodeSpot c in (_pages[page].Code ?? Array.Empty<CodeSpot>()).Where(c => c.Button == button))
            {
                int x = Op(c.X) & 0xFF, y = Loaded(c.Y, x);
                if (c.Keep >= 0) Load(c.Keep, Loaded(c.Keep, x));
                Load(c.X, x + dx / Tile);
                Load(c.Y, y + dy / Tile);
            }
            TouchListButtons.Spots[] spots = _pages[page].Spots ?? Array.Empty<TouchListButtons.Spots>();
            for (int t = 0; t < spots.Length; t++)
            {
                int entry = button - spots[t].FirstButton;
                if (entry < 0 || entry >= spots[t].Count) continue;
                // Grid pages only carry u8 spots.
                int at = _spotStarts[page][t] + entry * spots[t].Stride + spots[t].X;
                ScreenSpots.Shift(_data, at, 1, (int)Math.Round(dx / (double)spots[t].Unit), 255);
                if (!spots[t].XOnly) ScreenSpots.Shift(_data, at + 1, 1, (int)Math.Round(dy / (double)spots[t].Unit), 255);
            }
            if (button >= StopsOf(page)) return;
            int s = StopAt(page, button);
            ScreenSpots.Shift(_data, s, 1, dx, 255);
            ScreenSpots.Shift(_data, s + 1, 1, dy, 191);
        }

        public void Resize(int page, int button, int width, int height)
        {
            if (MoveLocked(page, button) == null) TouchRects.Resize(_data, TouchAt(page, button), width, height);
        }

        public (double X, double Y) CursorCentre(int page, int stop) => (_data[StopAt(page, stop)], _data[StopAt(page, stop) + 1]);

        public int Target(int page, int stop, CursorDirection d) => _data[StopAt(page, stop) + 4 + (int)d];

        public void SetTarget(int page, int stop, CursorDirection d, int target)
        {
            if (Fixed(page, stop, d) == null) _data[StopAt(page, stop) + 4 + (int)d] = (byte)target;
        }

        public IReadOnlyList<(int Value, string Name)> Choices(int page) => CursorDirections.WithBack(_pages[page].Buttons, StopsOf(page));

        public int ValueOf(int page, int stop) => stop;
        public int StopOf(int page, int value) => value & ~CursorDirections.Back;

        public string Fixed(int page, int stop, CursorDirection d) =>
            _pages[page].Fixed != null && _pages[page].Fixed.TryGetValue((stop, d), out string why) ? why : null;

        public bool HasCursor(int page, int stop) => CursorDirections.All.Any(d => Fixed(page, stop, d) == null);

        public byte[] ToBytes() => _tables.Copy();
        public void FromBytes(byte[] bytes) => _tables.Load(bytes);
        public byte[] FileBytes() => _tables.FileBytes();

        public string WhyNot() => _tables.NewProblem(Problems, _data);

        private IEnumerable<string> Problems(byte[] data)
        {
            for (int page = 0; page < _pages.Length; page++)
            {
                int n = StopsOf(page), flow = _starts[page].Flow;
                int To(int stop, CursorDirection d) => data[flow + stop * Entry + 4 + (int)d] & ~CursorDirections.Back;
                for (int stop = 0; stop < n; stop++)
                    foreach (CursorDirection d in CursorDirections.All)
                    {
                        if (To(stop, d) >= n)
                        {
                            yield return $"{_pages[page].Name}: {d} from {_pages[page].Buttons[stop]} goes to a button the cursor can't reach.";
                            continue;
                        }
                        // Past a switched-off stop the handler keeps going the same way until a stop that is on, the start, or a
                        // dead end, so a ring of switched-off stops freezes the game.
                        foreach (int[] off in _pages[page].Disabled ?? Array.Empty<int[]>())
                        {
                            HashSet<int> seen = new HashSet<int>();
                            int at = To(stop, d);
                            while (off.Contains(at) && at != stop && To(at, d) != at && seen.Add(at)) at = To(at, d);
                            if (off.Contains(at) && at != stop && To(at, d) != at)
                            {
                                yield return $"{_pages[page].Name}: {d} from {_pages[page].Buttons[stop]} can loop forever when some buttons are off.";
                                break;
                            }
                        }
                    }
            }
        }

        public string WhyNotWrite(byte[] bytes) =>
            bytes == null || bytes.Length != _data.Length ? "The screen's tables don't match this ROM." : _tables.NewProblem(Problems, bytes);

        public void WriteFile(byte[] bytes)
        {
            if (WhyNotWrite(bytes) is string why) throw new InvalidOperationException(why);
            _tables.Write(bytes);
            foreach (ScreenTileArt art in _art) art?.Write(bytes);
        }
    }
}
