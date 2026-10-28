using System;
using System.Collections.Generic;
using System.Linq;
using Table = DSPRE.RomLocator.PokegearTable;
using static DSPRE.ROMFiles.ButtonNames;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// The HGSS Pokégear's bottom screen: the bar, skins, map markings, phone and radio. Grid entries are 12 bytes: app, Left,
    /// Right, Up, Down, cursor x and y, then the cursor corners' offsets.
    /// </summary>
    public sealed class PokegearButtons : IScreenButtons
    {
        private const int Grid = 12, Stay = 0xFF, Tile = 8;

        /// <param name="At">Its tables. The editor changes the first grid, and the others copy its cursor spots.</param>
        /// <param name="Stops">The button each grid entry stands for, when it isn't the button at that index.</param>
        /// <param name="Locked">Why the touch areas stay, when the game draws their pictures at spots in code.</param>
        /// <param name="Pairs">Circles come in twos round one spot, and move together.</param>
        /// <param name="UpDown">Why Up and Down aren't read, when they aren't.</param>
        private sealed record Page(string Name, string[] Buttons, Table At, bool LeftRightOnly = false, int[] Stops = null, string Locked = null,
                                   bool Pairs = false, string UpDown = null);

        // The radio's dial lists: each station is a small circle for a clear signal round a larger one for a faint one.
        private static string[] Stations(int count) =>
            Enumerable.Range(1, count).SelectMany(i => new[] { $"Station {i} clear signal", $"Station {i} faint signal" }).ToArray();

        // The markings menu touch list: each of four markings has an icon and a word, then the icon choices, then Back.
        private static readonly string[] Markings =
            Enumerable.Range(1, 4).SelectMany(i => new[] { $"Icon {i}", $"Word {i}" }).Concat(Numbered("Icon choice", 8)).Append("Back").ToArray();

        private const string LeftRight = "Only Left and Right move it";

        private static Page[] PagesOf(Table[] t) => new[]
        {
            new Page("Bar", new[] { "Skins", "Radio", "Map", "Phone", "Back" }, t[0], LeftRightOnly: true),
            new Page("Skins", Numbered("Skin", 6), t[1]),
            new Page("Map", new[] { "Markings", "Zoom" }, t[2]),
            new Page("Phone", new[] { "Row 1", "Row 2", "Row 3", "Row 4", "Row 5", "Row 6", "Page up", "Page down" }, t[3]),
            // PokegearMap_InitUIButtons buttonSpecsMarkingsMenu and buttonSpecsSelectIcon over the markings touch list.
            new Page("Map markings", Markings, t[4], Stops: Enumerable.Range(0, 8).Append(16).ToArray(), Locked: TouchRects.FixedArt),
            new Page("Map marking icons", Markings, t[5], Stops: Enumerable.Range(8, 8).ToArray(), Locked: TouchRects.FixedArt, UpDown: LeftRight),
            new Page("Moving a contact", Numbered("Handle", 6).Concat(new[] { "Page up", "Page down" }).ToArray(), t[6]),
            // Radio_SnapCursorToChannelHitbox puts the cursor on Johto station n for button n.
            new Page("Radio buttons", Numbered("Station", 4), t[7]),
            new Page("Johto radio dial", Stations(4), t[8], Pairs: true),
            new Page("Kanto radio dial", Stations(3), t[9], Pairs: true),
            new Page("Kanto radio dial with the Expansion Card", Stations(5), t[10], Pairs: true),
            new Page("Ruins of Alph radio dial", Stations(1), t[11], Pairs: true),
            new Page("Rocket signal radio dial", Stations(1), t[12], Pairs: true),
        };

        private readonly Page[] _pages;
        private readonly ScreenTables _tables;
        private readonly int[] _touchStart, _tileStart;
        private readonly List<int>[] _gridStart;
        private readonly byte[] _data;

        private PokegearButtons(Page[] pages, ScreenTables tables)
        {
            _pages = pages;
            _tables = tables;
            _touchStart = new int[pages.Length];
            _tileStart = new int[pages.Length];
            _gridStart = new List<int>[pages.Length];
            for (int i = 0; i < pages.Length; i++)
            {
                Table t = pages[i].At;
                _touchStart[i] = tables.Take(t.Overlay, t.Touch, pages[i].Buttons.Length * TouchRects.Size);
                _gridStart[i] = t.Grids.Select(g => tables.Take(t.Overlay, g.At, g.Count * Grid)).ToList();
                _tileStart[i] = t.Tiles >= 0 ? tables.Take(t.Overlay, t.Tiles, pages[i].Buttons.Length * 2) : tables.Count;
            }
            _data = tables.Finish();
        }

        /// <summary>Null when this ROM's tables aren't known or don't look like the game's.</summary>
        public static PokegearButtons Read()
        {
            RomInfo.PokegearSites sites = RomLocator.PokegearSites;
            if (sites == null) return null;
            ScreenTables tables = new ScreenTables();
            Page[] pages = PagesOf(RomLocator.PokegearTables(sites));
            foreach (Page p in pages)
            {
                byte[] f = tables.FileOf(p.At.Overlay);
                if (f == null) return null;
                int end = p.At.Touch + p.Buttons.Length * TouchRects.Size;
                // Each touch list ends where the game's does; the edited grid has one entry per button, in button order.
                if (p.At.Touch < 0 || end >= f.Length || f[end] != 0xFF || p.At.Tiles + p.Buttons.Length * 2 > f.Length) return null;
                if (p.At.Grids.Any(g => g.At < 0 || g.At + g.Count * Grid > f.Length)) return null;
                for (int i = 0; p.LeftRightOnly && i < p.Buttons.Length; i++)
                    if (BitConverter.ToUInt16(f, p.At.Grids[0].At + i * Grid) != i) return null;
            }
            PokegearButtons buttons = new PokegearButtons(pages, tables);
            return tables.Missing ? null : buttons;
        }

        public IReadOnlyList<string> PageNames => _pages.Select(p => p.Name).ToList();
        public string[] ButtonsOf(int page) => _pages[page].Buttons;
        public int StopsOf(int page) => _pages[page].At.Grids.Length > 0 ? _pages[page].Stops?.Length ?? _pages[page].Buttons.Length : 0;

        /// <summary>The radio buttons' picture spots in tiles, or null on pages without them.</summary>
        public IReadOnlyList<(int X, int Y)> TileSpots(int page) =>
            _pages[page].At.Tiles < 0 ? null
                : Enumerable.Range(0, _pages[page].Buttons.Length).Select(i => ((int)_data[_tileStart[page] + i * 2], (int)_data[_tileStart[page] + i * 2 + 1])).ToList();

        /// <summary>The radio buttons page. Every radio picture shows its spots.</summary>
        public int RadioButtonsPage => Array.FindIndex(_pages, p => p.At.Tiles >= 0);

        public string StopName(int page, int stop) => _pages[page].Buttons[ButtonOf(page, stop)];

        private int ButtonOf(int page, int stop) => _pages[page].Stops?[stop] ?? stop;

        private int TouchAt(int page, int button) => _touchStart[page] + button * TouchRects.Size;
        private int EntryAt(int page, int stop) => _gridStart[page][0] + stop * Grid;

        public (int Top, int Bottom, int Left, int Right) AreaOf(int page, int button) => TouchRects.Read(_data, TouchAt(page, button));

        public string MoveLocked(int page, int button) => _pages[page].Locked;

        public int MoveStep(int page, int button) => _pages[page].At.Tiles >= 0 ? Tile : 1;

        // The cursor spot moves with the area, and on the bar it moves in every grid entry for that app. A circle's partner and any
        // picture placed in tiles move too.
        public void Move(int page, int button, int dx, int dy)
        {
            if (MoveLocked(page, button) != null) return;
            int step = MoveStep(page, button);
            (dx, dy) = TouchRects.Move(_data, TouchAt(page, button), (int)Math.Round(dx / (double)step) * step, (int)Math.Round(dy / (double)step) * step);
            if (_pages[page].Pairs) TouchRects.Move(_data, TouchAt(page, button ^ 1), dx, dy);
            if (_pages[page].At.Tiles >= 0)
            {
                int at = _tileStart[page] + button * 2;
                // The radio buttons are 4 by 4 tiles.
                ScreenSpots.Shift(_data, at, 1, (int)Math.Round(dx / (double)Tile), 32 - 4);
                ScreenSpots.Shift(_data, at + 1, 1, (int)Math.Round(dy / (double)Tile), 24 - 4);
            }
            if (StopsOf(page) == 0) return;
            for (int g = 0; g < _pages[page].At.Grids.Length; g++)
                for (int i = 0; i < _pages[page].At.Grids[g].Count; i++)
                {
                    int at = _gridStart[page][g] + i * Grid;
                    bool same = _pages[page].LeftRightOnly ? BitConverter.ToUInt16(_data, at) == button : g == 0 && ButtonOf(page, i) == button;
                    if (!same) continue;
                    ScreenSpots.Shift(_data, at + 6, 1, dx, 255);
                    ScreenSpots.Shift(_data, at + 7, 1, dy, 191);
                }
        }

        public void Resize(int page, int button, int width, int height)
        {
            if (MoveLocked(page, button) == null) TouchRects.Resize(_data, TouchAt(page, button), width, height);
        }

        public (double X, double Y) CursorCentre(int page, int stop) => (_data[EntryAt(page, stop) + 6], _data[EntryAt(page, stop) + 7]);

        // Grid bytes are Left, Right, Up, Down.
        private static int Link(CursorDirection d) => d switch
        {
            CursorDirection.Left => 2,
            CursorDirection.Right => 3,
            CursorDirection.Up => 4,
            _ => 5,
        };

        public int Target(int page, int stop, CursorDirection d) => _data[EntryAt(page, stop) + Link(d)];

        public void SetTarget(int page, int stop, CursorDirection d, int target)
        {
            if (Fixed(page, stop, d) == null) _data[EntryAt(page, stop) + Link(d)] = (byte)target;
        }

        public IReadOnlyList<(int Value, string Name)> Choices(int page) =>
            Enumerable.Range(0, StopsOf(page)).Select(i => (i, _pages[page].Buttons[ButtonOf(page, i)])).Append((Stay, "Stay")).ToList();

        public int ValueOf(int page, int stop) => stop;
        public int StopOf(int page, int value) => value < StopsOf(page) ? value : -1;

        // pokegear_main.c PokegearApp_HandleKeyInput_SwitchApps reads only Left and Right on the bar.
        public string Fixed(int page, int stop, CursorDirection d) =>
            d is CursorDirection.Up or CursorDirection.Down ? _pages[page].LeftRightOnly ? "The bar ignores Up and Down" : _pages[page].UpDown : null;

        public byte[] ToBytes() => _tables.Copy();
        public void FromBytes(byte[] bytes) => _tables.Load(bytes);
        public byte[] FileBytes() => _tables.FileBytes();

        // A link past the last entry does nothing in the game, but the editor keeps every link on a button or Stay.
        private IEnumerable<string> Problems(byte[] data)
        {
            for (int page = 0; page < _pages.Length; page++)
                for (int stop = 0; stop < StopsOf(page); stop++)
                    foreach (CursorDirection d in CursorDirections.All)
                    {
                        int v = data[EntryAt(page, stop) + Link(d)];
                        if (v != Stay && v >= StopsOf(page)) yield return $"{_pages[page].Name}: {d} from {_pages[page].Buttons[ButtonOf(page, stop)]} goes to a button that isn't there.";
                    }
        }

        public string WhyNot() => _tables.NewProblem(Problems, _data);

        public string WhyNotWrite(byte[] bytes) =>
            bytes == null || bytes.Length != _data.Length ? "The Pokégear tables don't match this ROM." : _tables.NewProblem(Problems, bytes);

        public void WriteFile(byte[] bytes)
        {
            if (WhyNotWrite(bytes) is string why) throw new InvalidOperationException(why);
            _tables.Write(bytes);
        }
    }
}
