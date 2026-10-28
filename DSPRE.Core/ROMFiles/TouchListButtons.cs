using System;
using System.Collections.Generic;
using System.Linq;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// Touch lists with no cursor table, in an overlay or arm9, with any button spot tables that move along. Platinum's bag,
    /// trainer case, summary, PC and Pokétch use them.
    /// </summary>
    public sealed class TouchListButtons : IScreenButtons, IScreenArt
    {
        public const int InArm9 = ScreenTables.InArm9;

        /// <summary>A table of picture spots, one entry per button from <paramref name="FirstButton"/> on.</summary>
        /// <param name="Stride">Bytes per entry.</param>
        /// <param name="X">Where x sits in the entry; y follows it.</param>
        /// <param name="Size">1 for u8 spots, 2 for s16, 4 for s32.</param>
        /// <param name="Unit">Pixels per step: 8 for tiles.</param>
        /// <param name="XOnly">The entry holds only x.</param>
        /// <param name="YOnly">The entry holds only y.</param>
        public sealed record Spots(int At, int Stride, int X, int Size, int Unit, int FirstButton = 0, int Count = 0, bool XOnly = false,
                                   bool YOnly = false);

        /// <param name="File">The overlay, or <see cref="InArm9"/>.</param>
        /// <param name="Ended">Whether the list ends with the 0xFF entry; Pokétch lists are counted in code instead.</param>
        /// <param name="Locked">Buttons whose touch areas must stay, and why.</param>
        /// <param name="Pairs">Circles come in twos round one spot, and move together.</param>
        public sealed record Page(string Name, string[] Buttons, int File, int Touch, bool Ended = true, Spots[] Spots = null,
                                  IReadOnlyDictionary<int, string> Locked = null, bool Pairs = false, ScreenTiles Tiles = null);

        private const int Tile = 8;

        private readonly Page[] _pages;
        private readonly ScreenTables _tables;
        private readonly int[] _touchStart;
        private readonly List<int>[] _spotStart;
        private readonly ScreenTileArt[] _art;
        private readonly byte[] _data;

        private TouchListButtons(Page[] pages, ScreenTables tables)
        {
            _pages = pages;
            _tables = tables;
            _touchStart = new int[pages.Length];
            _spotStart = new List<int>[pages.Length];
            _art = new ScreenTileArt[pages.Length];
            for (int i = 0; i < pages.Length; i++)
            {
                Page p = pages[i];
                _touchStart[i] = tables.Take(p.File, p.Touch, p.Buttons.Length * TouchRects.Size);
                _spotStart[i] = (p.Spots ?? Array.Empty<Spots>()).Select(s => tables.Take(p.File, s.At, CountOf(p, s) * s.Stride)).ToList();
                _art[i] = ScreenTileArt.Take(p.Tiles, tables);
            }
            _data = tables.Finish();
            for (int i = 0; i < pages.Length; i++)
            {
                int page = i;
                _art[i]?.Remember(_data, b => AreaOf(page, b));
            }
        }

        private static int CountOf(Page p, Spots s) => s.Count > 0 ? s.Count : p.Buttons.Length - s.FirstButton;

        /// <summary>Null when a page's list doesn't look like the game's.</summary>
        public static TouchListButtons Read(Page[] pages)
        {
            if (pages == null) return null;
            ScreenTables tables = new ScreenTables();
            foreach (Page p in pages)
            {
                byte[] file = tables.FileOf(p.File);
                int end = p.Touch + p.Buttons.Length * TouchRects.Size;
                if (file == null || p.Touch < 0 || end + (p.Ended ? 1 : 0) > file.Length) return null;
                // An ended list stops where the game's does; one counted in code must at least read as areas on the screen.
                if (p.Ended ? file[end] != 0xFF : Enumerable.Range(0, p.Buttons.Length).Any(b => !TouchRects.OnScreen(file, p.Touch + b * TouchRects.Size)))
                    return null;
            }
            TouchListButtons buttons = new TouchListButtons(pages, tables);
            return tables.Missing ? null : buttons;
        }

        public IReadOnlyList<string> PageNames => _pages.Select(p => p.Name).ToList();
        public string[] ButtonsOf(int page) => _pages[page].Buttons;
        public int StopsOf(int page) => 0;

        private int TouchAt(int page, int button) => _touchStart[page] + button * TouchRects.Size;

        public (int Top, int Bottom, int Left, int Right) AreaOf(int page, int button) => TouchRects.Read(_data, TouchAt(page, button));

        public string MoveLocked(int page, int button) =>
            _pages[page].Locked != null && _pages[page].Locked.TryGetValue(button, out string why) ? why : null;

        /// <summary>The page's spots in the given table, in that table's units, or null.</summary>
        public IReadOnlyList<(int X, int Y)> SpotsOf(int page, int table = 0)
        {
            Spots[] all = _pages[page].Spots;
            if (all == null || table >= all.Length) return null;
            Spots s = all[table];
            int At(int i) => _spotStart[page][table] + i * s.Stride + s.X;
            return Enumerable.Range(0, CountOf(_pages[page], s))
                .Select(i => (s.YOnly ? 0 : Value(s, At(i)), s.XOnly ? 0 : Value(s, s.YOnly ? At(i) : At(i) + s.Size))).ToList();
        }

        /// <summary>One spot table entry's bytes, as the file holds them now.</summary>
        public byte[] SpotEntry(int page, int table, int entry)
        {
            Spots s = _pages[page].Spots[table];
            return _data.Skip(_spotStart[page][table] + entry * s.Stride).Take(s.Stride).ToArray();
        }

        private int Value(Spots s, int at) => ScreenSpots.Value(_data, at, s.Size);

        private bool Tiled(int page, int button) => _art[page]?.Moves(button) == true;

        /// <summary>Pixels per step: whole tiles for a button whose art or text sits on the tile grid.</summary>
        public int MoveStep(int page, int button) =>
            Tiled(page, button) || (_pages[page].Spots ?? Array.Empty<Spots>()).Any(s => s.Unit == Tile && button - s.FirstButton >= 0 &&
                                                                                       button - s.FirstButton < CountOf(_pages[page], s)) ? Tile : 1;

        /// <summary>The page's background screen as edited, or null when it has none.</summary>
        public byte[] ScreenOf(int page) => _art[page]?.Screen(_data);

        public bool HasArt(int page) => _art[page] != null;
        public ushort? SquareAt(int page, int x, int y) => _art[page]?.SquareAt(_data, x, y);
        public void SetSquare(int page, int x, int y, ushort square) => _art[page]?.SetSquare(_data, x, y, square);

        // A circle's partner, the button's picture spots and its background tiles move along.
        public void Move(int page, int button, int dx, int dy)
        {
            if (MoveLocked(page, button) != null) return;
            int step = MoveStep(page, button);
            (dx, dy) = ((int)Math.Round(dx / (double)step) * step, (int)Math.Round(dy / (double)step) * step);
            if (dx == 0 && dy == 0) return;
            (int top, int bottom, int left, int right) = AreaOf(page, button);
            (dx, dy) = TouchRects.Move(_data, TouchAt(page, button), dx, dy);
            // When an edge stops the area, it goes back to the last whole step.
            if (dx % step != 0 || dy % step != 0)
            {
                TouchRects.Move(_data, TouchAt(page, button), -(dx % step), -(dy % step));
                (dx, dy) = (dx - dx % step, dy - dy % step);
            }
            if (Tiled(page, button))
            {
                ScreenTileArt art = _art[page];
                art.Compose(_data, b => AreaOf(page, b));
            }
            if (_pages[page].Pairs) TouchRects.Move(_data, TouchAt(page, button ^ 1), dx, dy);
            Spots[] all = _pages[page].Spots ?? Array.Empty<Spots>();
            for (int t = 0; t < all.Length; t++)
            {
                Spots s = all[t];
                int entry = button - s.FirstButton;
                if (entry < 0 || entry >= CountOf(_pages[page], s)) continue;
                int at = _spotStart[page][t] + entry * s.Stride + s.X;
                // Spots are pixels or tiles without leaving the screen.
                if (!s.YOnly) ScreenSpots.Shift(_data, at, s.Size, (int)Math.Round(dx / (double)s.Unit), 255 / s.Unit);
                if (!s.XOnly) ScreenSpots.Shift(_data, s.YOnly ? at : at + s.Size, s.Size, (int)Math.Round(dy / (double)s.Unit), 191 / s.Unit);
            }
        }

        public void Resize(int page, int button, int width, int height)
        {
            if (MoveLocked(page, button) == null) TouchRects.Resize(_data, TouchAt(page, button), width, height);
        }

        public (double X, double Y) CursorCentre(int page, int stop) => (0, 0);
        public int Target(int page, int stop, CursorDirection d) => stop;
        public void SetTarget(int page, int stop, CursorDirection d, int target) { }
        public IReadOnlyList<(int Value, string Name)> Choices(int page) => Array.Empty<(int, string)>();
        public int ValueOf(int page, int stop) => stop;
        public int StopOf(int page, int value) => value;
        public string Fixed(int page, int stop, CursorDirection d) => null;

        public byte[] ToBytes() => _tables.Copy();
        public void FromBytes(byte[] bytes) => _tables.Load(bytes);
        public byte[] FileBytes() => _tables.FileBytes();

        public string WhyNot() => null;

        public string WhyNotWrite(byte[] bytes) => bytes == null || bytes.Length != _data.Length ? "The touch tables don't match this ROM." : null;

        public void WriteFile(byte[] bytes)
        {
            if (WhyNotWrite(bytes) is string why) throw new InvalidOperationException(why);
            _tables.Write(bytes);
            foreach (ScreenTileArt art in _art) art?.Write(bytes);
        }
    }
}
