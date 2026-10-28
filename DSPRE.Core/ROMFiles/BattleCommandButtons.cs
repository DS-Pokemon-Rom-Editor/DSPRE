using System;
using System.Collections.Generic;
using System.Linq;
using Table = DSPRE.RomLocator.BattleCommandTable;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// The battle touch screen's menus: each button's touch area, and for Command, Fight and Target the grid of cells the
    /// cursor walks. The cursor and the button's text and icons follow a moved area; the button art is a picture and doesn't.
    /// </summary>
    public sealed class BattleCommandButtons : IScreenButtons, IScreenButtonGrid
    {
        private sealed record Page(string Name, string[] Buttons, Table At);

        private const int Empty = 0xFF, Inset = 8;

        // pokeplatinum battle_subscreen.c sActionMenuTouchRects and the button layouts; pokeheartgold battle_input.c
        // sTwoOptionsMenuCursorInput and sCursorArrayPalParkMenu for the two-row grids.
        private static readonly (string Name, string[] Buttons)[] Menus =
        {
            ("Command", new[] { "Fight", "Bag", "Pokémon", "Run" }),
            ("Fight", new[] { "Cancel", "Move 1", "Move 2", "Move 3", "Move 4" }),
            ("Target", new[] { "Bottom left", "Top right", "Bottom right", "Top left", "Cancel" }),
            ("Yes or no", new[] { "Yes", "No" }),
            ("First battle", new[] { "Fight" }),
            ("Pal Park", new[] { "Park Ball", "Run" }),
            ("Recorded battle", new[] { "Stop" }),
        };

        private const int Command = 0, Fight = 0, Run = 3;

        private readonly Page[] _pages;
        private readonly ScreenTables _tables;
        private readonly (int Touch, int Grid)[] _starts;
        // Where each label and icon spot's x and y sit in _data.
        private readonly List<(RomLocator.BattleTextSpot Spot, int X, int Y)> _text = new List<(RomLocator.BattleTextSpot, int, int)>();
        private readonly byte[] _data;

        private BattleCommandButtons(Page[] pages, int overlay, int anchor, ScreenTables tables, IReadOnlyList<RomLocator.BattleTextSpot> text)
        {
            _pages = pages;
            _tables = tables;
            _starts = pages.Select(p => (tables.Take(overlay, anchor + p.At.Touch, p.Buttons.Length * TouchRects.Size),
                                         p.At.Grid is int grid ? tables.Take(overlay, anchor + grid, p.At.Width * p.At.Height) : tables.Count)).ToArray();
            foreach (RomLocator.BattleTextSpot spot in text)
            {
                int size = spot.Table ? 2 : 1;
                _text.Add((spot, tables.Take(overlay, anchor + spot.X, size), tables.Take(overlay, anchor + spot.Y, size)));
            }
            _data = tables.Finish();
        }

        // Spots are read only while every immediate is still a movs (high byte 0x20 to 0x27); otherwise the text stays put.
        private static IReadOnlyList<RomLocator.BattleTextSpot> TextSpots(byte[] file, int anchor)
        {
            IReadOnlyList<RomLocator.BattleTextSpot> spots = RomLocator.BattleTextSpots(RomInfo.GameFamily == RomInfo.GameFamilies.HGSS);
            bool Fits(int at, bool table) => anchor + at >= 0 && anchor + at + 2 <= file.Length && (table || (file[anchor + at + 1] & 0xF8) == 0x20);
            return spots.All(sp => Fits(sp.X, sp.Table) && Fits(sp.Y, sp.Table)) ? spots : Array.Empty<RomLocator.BattleTextSpot>();
        }

        /// <summary>A button's text and icon spots in pixels, in RomLocator.BattleTextSpots order.</summary>
        public IReadOnlyList<(int X, int Y)> TextOf(int page, int button) =>
            _text.Where(t => t.Spot.Page == page && t.Spot.Button == button)
                 .Select(t => t.Spot.Table ? ((int)BitConverter.ToUInt16(_data, t.X), (int)BitConverter.ToUInt16(_data, t.Y)) : (_data[t.X], _data[t.Y]))
                 .ToList();

        /// <summary>Null when this ROM's tables aren't known or don't look like the game's.</summary>
        public static BattleCommandButtons Read()
        {
            RomInfo.BattleCommandSites sites = RomLocator.BattleCommandSites;
            if (sites == null) return null;
            ScreenTables tables = new ScreenTables();
            byte[] file = tables.FileOf(sites.Overlay);
            if (file == null) return null;
            int a = (int)sites.TargetGrid;
            Table[] at = RomLocator.BattleCommandTables(RomInfo.GameFamily == RomInfo.GameFamilies.HGSS);
            Page[] pages = Menus.Select((m, i) => new Page(m.Name, m.Buttons, at[i])).ToArray();
            foreach (Page p in pages)
            {
                int end = a + p.At.Touch + p.Buttons.Length * TouchRects.Size;
                // Each touch list ends where the game's does, and every grid cell names one of the page's buttons.
                if (a + p.At.Touch < 0 || end >= file.Length || file[end] != 0xFF) return null;
                if (p.At.Grid is int grid && (a + grid < 0 || a + grid + p.At.Width * p.At.Height > file.Length
                                              || file.Skip(a + grid).Take(p.At.Width * p.At.Height).Any(c => c >= p.Buttons.Length))) return null;
            }
            BattleCommandButtons buttons = new BattleCommandButtons(pages, sites.Overlay, a, tables, TextSpots(file, a));
            return tables.Missing ? null : buttons;
        }

        public IReadOnlyList<string> PageNames => _pages.Select(p => p.Name).ToList();
        public string[] ButtonsOf(int page) => _pages[page].Buttons;
        public int StopsOf(int page) => _pages[page].At.Grid != null ? _pages[page].Buttons.Length : 0;

        private int TouchAt(int page, int button) => _starts[page].Touch + button * TouchRects.Size;

        public (int Top, int Bottom, int Left, int Right) AreaOf(int page, int button) => TouchRects.Read(_data, TouchAt(page, button));

        // battle_subscreen.c draws the cursor 8 pixels inside the touch area.
        public (double X, double Y) CursorCentre(int page, int stop)
        {
            (int top, int bottom, int left, int right) = AreaOf(page, stop);
            return ((left + Inset + right - Inset) / 2.0, (top + Inset + bottom - Inset) / 2.0);
        }

        public void Move(int page, int button, int dx, int dy)
        {
            (dx, dy) = TouchRects.Move(_data, TouchAt(page, button), dx, dy);
            foreach ((RomLocator.BattleTextSpot spot, int x, int y) in _text.Where(t => t.Spot.Page == page && t.Spot.Button == button))
            {
                int size = spot.Table ? 2 : 1;
                ScreenSpots.Shift(_data, x, size, dx, 255);
                ScreenSpots.Shift(_data, y, size, dy, 191);
            }
        }

        public void Resize(int page, int button, int width, int height) => TouchRects.Resize(_data, TouchAt(page, button), width, height);

        public (int Width, int Height) GridOf(int page) => (_pages[page].At.Width, _pages[page].At.Height);

        public int Cell(int page, int x, int y) => _data[_starts[page].Grid + y * _pages[page].At.Width + x];

        public void SetCell(int page, int x, int y, int button) => _data[_starts[page].Grid + y * _pages[page].At.Width + x] = (byte)button;

        /// <summary>Where the arrow takes the cursor from the button's first cell.</summary>
        public int Target(int page, int stop, CursorDirection d)
        {
            // battle_subscreen.c BattleSystem_MoveCursor steps one cell, skips empty ones and stops at the edge. On Command, Up
            // from Run does nothing, and Left or Right from Fight jump to the bottom row's corners.
            (int w, int h) = GridOf(page);
            int first = Enumerable.Range(0, w * h).FirstOrDefault(i => Cell(page, i % w, i / w) == stop, -1);
            if (first < 0) return stop;
            if (page == Command && stop == Run && d == CursorDirection.Up) return stop;
            (int dx, int dy) = d switch
            {
                CursorDirection.Up => (0, -1),
                CursorDirection.Down => (0, 1),
                CursorDirection.Left => (-1, 0),
                _ => (1, 0),
            };
            int x = first % w + dx, y = first / w + dy;
            while (x >= 0 && y >= 0 && x < w && y < h && Cell(page, x, y) == Empty) { x += dx; y += dy; }
            int target = x >= 0 && y >= 0 && x < w && y < h ? Cell(page, x, y) : stop;
            bool sideways = d == CursorDirection.Left || d == CursorDirection.Right;
            if (page == Command && stop == Fight && target == Fight && sideways) return Cell(page, d == CursorDirection.Left ? 0 : 2, 1);
            return target;
        }

        public void SetTarget(int page, int stop, CursorDirection d, int target) { }

        public IReadOnlyList<(int Value, string Name)> Choices(int page) =>
            _pages[page].Buttons.Select((name, i) => (i, name)).ToList();

        public int ValueOf(int page, int stop) => stop;
        public int StopOf(int page, int value) => value;

        public string Fixed(int page, int stop, CursorDirection d) => "Set by the grid";

        public byte[] ToBytes() => _tables.Copy();
        public void FromBytes(byte[] bytes) => _tables.Load(bytes);
        public byte[] FileBytes() => _tables.FileBytes();

        public string WhyNot() => _tables.NewProblem(Problems, _data);

        // The game starts the cursor on remembered cells, so an empty cell could leave it on no button.
        private IEnumerable<string> Problems(byte[] data)
        {
            for (int page = 0; page < _pages.Length; page++)
            {
                if (_pages[page].At.Grid == null) continue;
                (int w, int h) = GridOf(page);
                if (Enumerable.Range(0, w * h).Any(i => data[_starts[page].Grid + i] >= _pages[page].Buttons.Length))
                    yield return $"{_pages[page].Name}: every grid cell needs a button.";
            }
        }

        public string WhyNotWrite(byte[] bytes) =>
            bytes == null || bytes.Length != _data.Length ? "The battle menu tables don't match this ROM." : _tables.NewProblem(Problems, bytes);

        public void WriteFile(byte[] bytes)
        {
            if (WhyNotWrite(bytes) is string why) throw new InvalidOperationException(why);
            _tables.Write(bytes);
        }
    }
}
