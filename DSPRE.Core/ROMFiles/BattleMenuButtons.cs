using System;
using System.Collections.Generic;
using System.Linq;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// The battle Pokémon and bag screens' buttons, page by page: each button's touch area and cursor stop. The stop's corners
    /// frame the button, and its neighbours are where the arrow keys go. The art, text windows and icons follow.
    /// </summary>
    public sealed class BattleMenuButtons : IScreenButtons
    {
        public enum Menu { Pokemon, Bag }

        /// <param name="Stops">How many of its buttons have a cursor stop; the rest are touch only.</param>
        /// <param name="Art">Each button's entry in the menu's art position table.</param>
        /// <param name="ArtX">Where the page sits in the art table's tile grid; the bag keeps its pages side by side.</param>
        public sealed record Page(string Name, string[] Buttons, int Stops, int[] Art, int ArtX = 0, int ArtY = 0);

        private const int Entry = 8, Tile = 8;

        private static int[] Count(int from, int count) => Enumerable.Range(from, count).ToArray();

        private static string[] Moves(params string[] then) => new[] { "Move 1", "Move 2", "Move 3", "Move 4" }.Concat(then).ToArray();

        // pokeplatinum battle_party.c / battle_party_sprites.c and battle_bag.c / battle_bag_sprites.c, in pointer table order;
        // HGSS overlay 8 keeps the same pages and button order.
        public static Page[] PagesOf(Menu menu) => menu == Menu.Pokemon
            ? new[]
            {
                new Page("Pokémon", new[] { "Pokémon 1", "Pokémon 2", "Pokémon 3", "Pokémon 4", "Pokémon 5", "Pokémon 6", "Cancel" }, 7, Count(0, 7)),
                new Page("Switch", new[] { "Switch", "Summary", "Moves", "Cancel" }, 4, new[] { 7, 8, 10, 6 }),
                new Page("Summary", new[] { "Previous", "Next", "Moves", "Cancel" }, 4, new[] { 12, 13, 11, 6 }),
                new Page("Moves", Moves("Previous", "Next", "Summary", "Cancel"), 8, new[] { 14, 15, 16, 17, 12, 13, 9, 6 }),
                new Page("Move info", Moves("Cancel"), 5, new[] { 30, 31, 32, 33, 6 }),
                new Page("Restore PP", Moves("Cancel"), 5, new[] { 19, 20, 21, 22, 6 }),
                new Page("Forget a move", Moves("New move", "Contest", "Cancel"), 7, new[] { 23, 24, 25, 26, 27, 18, 6 }),
                new Page("Forget the move", new[] { "Forget", "Contest", "Cancel" }, 3, new[] { 28, 18, 6 }),
            }
            : new[]
            {
                new Page("Pockets", new[] { "HP and PP", "Status", "Poké Balls", "Battle items", "Last used", "Cancel" }, 6, Count(0, 6)),
                RomInfo.GameFamily == RomInfo.GameFamilies.HGSS
                    ? new Page("Items", new[] { "Item 1", "Item 2", "Item 3", "Item 4", "Item 5", "Item 6", "Cancel", "Previous", "Next" }, 7,
                               new[] { 6, 7, 8, 9, 10, 11, 14, 12, 13 }, ArtX: 32)
                    : new Page("Items", new[] { "Item 1", "Item 2", "Item 3", "Item 4", "Item 5", "Item 6", "Previous", "Next", "Cancel" }, 9,
                               Count(6, 9), ArtX: 32),
                new Page("Use", new[] { "Use", "Cancel" }, 2, new[] { 15, 16 }, ArtY: 32),
            };

        public Page[] Pages { get; }

        private readonly ScreenTables _tables;
        private readonly (int Touch, int Stops)[] _starts;
        private readonly int _artStart;
        private readonly IReadOnlyList<RomLocator.BattleMenuSpot> _spots;
        // Where each spot entry sits in _data, and a window's size in tiles. Pages can share an entry.
        private readonly Dictionary<int, int> _spotData = new Dictionary<int, int>();
        private readonly Dictionary<int, (int W, int H)> _windowSizes = new Dictionary<int, (int W, int H)>();
        private readonly byte[] _data;

        private BattleMenuButtons(Page[] pages, int overlay, int anchor, RomLocator.BattleMenuTables at, int[] cursors, ScreenTables tables,
                                  IReadOnlyList<RomLocator.BattleMenuSpot> spots)
        {
            Pages = pages;
            _tables = tables;
            _starts = pages.Select((p, i) => (tables.Take(overlay, anchor + at.Touch[i], p.Buttons.Length * TouchRects.Size),
                                              tables.Take(overlay, cursors[i], p.Stops * Entry))).ToArray();
            _artStart = tables.Take(overlay, anchor + at.Art, at.ArtCount * TouchRects.Size);
            _spots = spots;
            byte[] file = tables.FileOf(overlay);
            foreach (RomLocator.BattleMenuSpot spot in spots)
            {
                if (_spotData.ContainsKey(spot.At)) continue;
                _spotData[spot.At] = tables.Take(overlay, anchor + spot.At + (spot.Window ? 1 : 0), spot.Window ? 2 : 8);
                if (spot.Window) _windowSizes[spot.At] = (file[anchor + spot.At + 3], file[anchor + spot.At + 4]);
            }
            _data = tables.Finish();
        }

        // Spots are read only while every window still looks like one on the page and every icon is on screen.
        private static IReadOnlyList<RomLocator.BattleMenuSpot> Spots(Menu menu, byte[] file, int anchor)
        {
            IReadOnlyList<RomLocator.BattleMenuSpot> spots = RomLocator.BattleMenuSpots(menu == Menu.Bag);
            bool Fits(RomLocator.BattleMenuSpot spot)
            {
                int at = anchor + spot.At;
                if (at < 0 || at + 8 > file.Length) return false;
                if (spot.Window) return (file[at] == 4 || file[at] == 5) && file[at + 1] + file[at + 3] <= 32 && file[at + 2] + file[at + 4] <= 24;
                int x = BitConverter.ToInt32(file, at), y = BitConverter.ToInt32(file, at + 4);
                return x >= 0 && x < 256 && y >= 0 && y < 192;
            }
            return spots.All(Fits) ? spots : Array.Empty<RomLocator.BattleMenuSpot>();
        }

        /// <summary>Null when this ROM's tables aren't known or don't look like the game's.</summary>
        public static BattleMenuButtons Read(Menu menu)
        {
            RomInfo.BattleMenuSites sites = RomLocator.BattleMenuSites;
            if (sites == null) return null;
            ScreenTables tables = new ScreenTables();
            byte[] file = tables.FileOf(sites.Overlay);
            if (file == null) return null;
            uint ram = OverlayUtils.OverlayTable.GetRAMAddress(sites.Overlay);
            int anchor = (int)sites.PartyCursors;
            RomLocator.BattleMenuTables at = RomLocator.BattleMenuTablesOf(menu == Menu.Bag, RomInfo.GameFamily == RomInfo.GameFamilies.HGSS);
            Page[] pages = PagesOf(menu);
            int[] cursors = new int[pages.Length];
            for (int i = 0; i < pages.Length; i++)
            {
                int pointer = anchor + at.Pointers + i * 4, touch = anchor + at.Touch[i];
                if (pointer < 0 || pointer + 4 > file.Length || touch < 0 || touch + pages[i].Buttons.Length * TouchRects.Size + 1 > file.Length) return null;
                long cursor = BitConverter.ToUInt32(file, pointer) - (long)ram;
                if (cursor < 0 || cursor + pages[i].Stops * Entry > file.Length) return null;
                cursors[i] = (int)cursor;
                // Each table ends where the game's does.
                if (file[touch + pages[i].Buttons.Length * TouchRects.Size] != 0xFF) return null;
            }
            BattleMenuButtons buttons = new BattleMenuButtons(pages, sites.Overlay, anchor, at, cursors, tables, Spots(menu, file, anchor));
            return tables.Missing ? null : buttons;
        }

        private int TouchAt(int page, int button) => _starts[page].Touch + button * TouchRects.Size;

        public (int Top, int Bottom, int Left, int Right) AreaOf(int page, int button) => TouchRects.Read(_data, TouchAt(page, button));

        private int Stop(int page, int button) => _starts[page].Stops + button * Entry;

        private bool HasStop(int page, int button) => button < Pages[page].Stops;

        public IReadOnlyList<string> PageNames => Pages.Select(p => p.Name).ToList();
        public string[] ButtonsOf(int page) => Pages[page].Buttons;
        public int StopsOf(int page) => Pages[page].Stops;

        public (double X, double Y) CursorCentre(int page, int stop)
        {
            (int x1, int y1, int x2, int y2) = Cursor(page, stop);
            return ((x1 + x2) / 2.0, (y1 + y2) / 2.0);
        }

        public IReadOnlyList<(int Value, string Name)> Choices(int page) => CursorDirections.WithBack(Pages[page].Buttons, Pages[page].Stops);

        public int ValueOf(int page, int stop) => stop;
        public int StopOf(int page, int value) => value & ~CursorDirections.Back;
        public string Fixed(int page, int stop, CursorDirection d) => null;

        private (int X1, int Y1, int X2, int Y2) Cursor(int page, int button)
        {
            int at = Stop(page, button);
            return (_data[at], _data[at + 1], _data[at + 2], _data[at + 3]);
        }

        /// <summary>A button. With <see cref="CursorDirections.Back"/> set, the cursor returns where it came from when it can, otherwise it goes to this button.</summary>
        public int Target(int page, int button, CursorDirection d) => _data[Stop(page, button) + 4 + (int)d];

        public void SetTarget(int page, int button, CursorDirection d, int target) => _data[Stop(page, button) + 4 + (int)d] = (byte)target;

        private int ArtEntry(int page, int button) => _artStart + Pages[page].Art[button] * TouchRects.Size;

        /// <summary>Where the game pastes the button's art: tile x, y, width and height on the page.</summary>
        public (int X, int Y, int W, int H) ArtOf(int page, int button)
        {
            int at = ArtEntry(page, button);
            return (_data[at] - Pages[page].ArtX, _data[at + 1] - Pages[page].ArtY, _data[at + 2], _data[at + 3]);
        }

        /// <summary>
        /// A button's text windows, by their top left tile, and icons, by their centre in pixels, in RomLocator.BattleMenuSpots order.
        /// </summary>
        public IReadOnlyList<(int X, int Y)> SpotsOf(int page, int button) =>
            _spots.Where(s => s.Page == page && s.Button == button).Select(s =>
            {
                int at = _spotData[s.At];
                return s.Window ? (_data[at], _data[at + 1]) : (BitConverter.ToInt32(_data, at), BitConverter.ToInt32(_data, at + 4));
            }).ToList();

        public int MoveStep(int page, int button) => Tile;

        /// <summary>Which art entry the button uses; Cancel and the arrows share one across the Pokémon pages.</summary>
        public int ArtIndex(int page, int button) => Pages[page].Art[button];

        /// <summary>Moves the touch area, cursor frame, art, text and icons together in whole tiles; buttons that share art or an icon move too.</summary>
        public void Move(int page, int button, int dx, int dy)
        {
            (int top, int bottom, int left, int right) = AreaOf(page, button);
            (int ax, int ay, int aw, int ah) = ArtOf(page, button);
            int tx = Math.Clamp((int)Math.Round(dx / (double)Tile), -ax, 32 - aw - ax), ty = Math.Clamp((int)Math.Round(dy / (double)Tile), -ay, 24 - ah - ay);
            dx = Math.Clamp(tx * Tile, -left, TouchRects.MaxRight - right);
            dy = Math.Clamp(ty * Tile, -top, TouchRects.MaxBottom - bottom);
            List<(int Page, int Button)> moved = Linked(page, button);
            foreach ((int p, int b) in moved.DistinctBy(m => Pages[m.Page].Art[m.Button]))
            {
                (int x, int y, int w, int h) = ArtOf(p, b);
                int at = ArtEntry(p, b);
                (_data[at], _data[at + 1]) = ((byte)(_data[at] + Math.Clamp(tx, -x, 32 - w - x)), (byte)(_data[at + 1] + Math.Clamp(ty, -y, 24 - h - y)));
            }
            foreach ((int p, int b) in moved) MoveArea(p, b, dx, dy);
            foreach (RomLocator.BattleMenuSpot spot in _spots.Where(s => moved.Contains((s.Page, s.Button))).DistinctBy(s => s.At))
            {
                int at = _spotData[spot.At];
                if (spot.Window)
                {
                    (int w, int h) = _windowSizes[spot.At];
                    ScreenSpots.Shift(_data, at, 1, tx, 32 - w);
                    ScreenSpots.Shift(_data, at + 1, 1, ty, 24 - h);
                }
                else
                {
                    ScreenSpots.Shift(_data, at, 4, tx * Tile, 255);
                    ScreenSpots.Shift(_data, at + 4, 4, ty * Tile, 191);
                }
            }
        }

        private List<(int Page, int Button)> Linked(int page, int button)
        {
            List<(int Page, int Button)> all = Enumerable.Range(0, Pages.Length)
                .SelectMany(p => Enumerable.Range(0, Pages[p].Buttons.Length).Select(b => (p, b))).ToList();
            HashSet<int> Shared((int Page, int Button) at) => _spots.Where(s => (s.Page, s.Button) == at).Select(s => s.At).ToHashSet();
            List<(int Page, int Button)> linked = new List<(int Page, int Button)> { (page, button) };
            for (int i = 0; i < linked.Count; i++)
            {
                (int p, int b) = linked[i];
                HashSet<int> spots = Shared((p, b));
                foreach ((int Page, int Button) other in all)
                    if (!linked.Contains(other) && (Pages[other.Page].Art[other.Button] == Pages[p].Art[b] || Shared(other).Overlaps(spots))) linked.Add(other);
            }
            return linked;
        }

        private void MoveArea(int page, int button, int dx, int dy)
        {
            (dx, dy) = TouchRects.Move(_data, TouchAt(page, button), dx, dy);
            if (!HasStop(page, button)) return;
            int s = Stop(page, button);
            ScreenSpots.Shift(_data, s, 1, dx, 255);
            ScreenSpots.Shift(_data, s + 1, 1, dy, 191);
            ScreenSpots.Shift(_data, s + 2, 1, dx, 255);
            ScreenSpots.Shift(_data, s + 3, 1, dy, 191);
        }

        /// <summary>Sets the touch area's size from its top left corner. The cursor frame keeps its margins.</summary>
        public void Resize(int page, int button, int width, int height)
        {
            (int _, int bottom, int _, int right) = AreaOf(page, button);
            TouchRects.Resize(_data, TouchAt(page, button), width, height);
            (int _, int newBottom, int _, int newRight) = AreaOf(page, button);
            if (!HasStop(page, button)) return;
            int s = Stop(page, button);
            ScreenSpots.Shift(_data, s + 2, 1, newRight - right, 255);
            ScreenSpots.Shift(_data, s + 3, 1, newBottom - bottom, 191);
        }

        public byte[] ToBytes() => _tables.Copy();
        public void FromBytes(byte[] bytes) => _tables.Load(bytes);
        public byte[] FileBytes() => _tables.FileBytes();

        public string WhyNot() => _tables.NewProblem(Problems, _data);

        private IEnumerable<string> Problems(byte[] data)
        {
            for (int page = 0; page < Pages.Length; page++)
                for (int button = 0; button < Pages[page].Stops; button++)
                    foreach (CursorDirection d in CursorDirections.All)
                    {
                        int target = data[Stop(page, button) + 4 + (int)d] & ~CursorDirections.Back;
                        if (target >= Pages[page].Stops)
                            yield return $"{Pages[page].Name}: {d} from {Pages[page].Buttons[button]} goes to a button the cursor can't reach.";
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
