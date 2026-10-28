using System;
using System.Collections.Generic;
using System.Linq;
using Page = DSPRE.ROMFiles.GridMenuButtons.Page;
using static DSPRE.ROMFiles.ButtonNames;

namespace DSPRE.ROMFiles
{
    /// <summary>The HGSS PC and Pokédex search pages, as <see cref="GridMenuButtons"/>.</summary>
    public static class HgssMenuButtons
    {
        private static Dictionary<(int, CursorDirection), string> Fix(IEnumerable<int> stops, string why, params CursorDirection[] directions)
        {
            CursorDirection[] all = directions.Length > 0 ? directions : CursorDirections.All;
            return stops.SelectMany(s => all.Select(d => (s, d))).ToDictionary(k => k, _ => why);
        }

        private static Dictionary<(int, CursorDirection), string> Merge(params Dictionary<(int, CursorDirection), string>[] parts) =>
            parts.SelectMany(p => p).ToDictionary(kv => kv.Key, kv => kv.Value);

        private const string DragAndDrop = "Dragging Pokémon uses the same spots";
        private const string TouchOnly = "Touch only";
        private const string Scrolls = "Scrolls the boxes";

        // Box slots, the box name, then its two arrows: Left and Right on the name scroll the boxes; the arrows are touch only.
        private static Dictionary<(int, CursorDirection), string> BoxRow() =>
            Merge(Fix(new[] { 30 }, Scrolls, CursorDirection.Left, CursorDirection.Right), Fix(new[] { 31, 32 }, TouchOnly));

        // pokeheartgold overlay 14 ov14_021F8B10, in layout order; what each stop does is fixed by its number.
        private static Page[] PcPages(Func<int, (int Touch, int Flow)> at) => new[]
        {
            new Page("Deposit", Join(Numbered("Pokémon", 6), new[] { "Back", "Deposit", "Summary", "Mark", "Release", "Exit" }), at(0).Touch, at(0).Flow,
                     Locked(Enumerable.Range(0, 6), DragAndDrop)),
            new Page("Picking a box to deposit", Join(Numbered("Box", 6), new[] { "Left arrow", "Right arrow", "Deposit", "Cancel" }), at(1).Touch, at(1).Flow,
                     Fixed: Fix(new[] { 6, 7 }, TouchOnly)),
            new Page("Withdraw", Join(Numbered("Slot", 30), new[] { "Box name", "Left arrow", "Right arrow", "Back", "Withdraw", "Summary", "Mark", "Release", "Exit" }),
                     at(2).Touch, at(2).Flow, Locked(Enumerable.Range(0, 30), DragAndDrop), BoxRow()),
            new Page("Move", Join(Numbered("Slot", 30), new[] { "Box name", "Left arrow", "Right arrow", "Party", "Move Pokémon", "Back", "Move", "Summary", "Item",
                     "Mark", "Release", "Exit" }), at(3).Touch, at(3).Flow, Locked(Enumerable.Range(0, 30), DragAndDrop), BoxRow()),
            new Page("Carrying a Pokémon", Join(Numbered("Slot", 30), Numbered("Pokémon", 6), new[] { "Quit" }, Numbered("Box", 6), new[] { "Left arrow", "Right arrow", "Switch boxes" }),
                     at(4).Touch, at(4).Flow, Locked(Enumerable.Range(0, 36), DragAndDrop), Fix(new[] { 43, 44 }, TouchOnly),
                     // ov14_021F70C0, ov14_021F7340 and ov14_021F7A50 switch these off.
                     new[] { new[] { 45 }, new[] { 43, 44, 45 }, Enumerable.Range(37, 9).ToArray() }),
            new Page("Moving from the party", Join(Numbered("Pokémon", 6), new[] { "Swap", "Back", "Move", "Summary", "Item", "Mark", "Release", "Exit" }),
                     at(5).Touch, at(5).Flow, Locked(Enumerable.Range(0, 6), DragAndDrop)),
            new Page("Move items", Join(Numbered("Slot", 30), new[] { "Box name", "Left arrow", "Right arrow", "Party", "Move items", "Back", "Give", "Exit" }),
                     at(6).Touch, at(6).Flow, Locked(Enumerable.Range(0, 30), DragAndDrop), BoxRow()),
            new Page("Moving items in the party", Join(Numbered("Pokémon", 6), new[] { "Swap", "Back", "Give", "Exit" }), at(7).Touch, at(7).Flow,
                     Locked(Enumerable.Range(0, 6), DragAndDrop)),
            new Page("Marking", Join(Numbered("Mark", 6), new[] { "OK", "Cancel" }), at(8).Touch, at(8).Flow),
            new Page("Box theme", Join(Numbered("Box", 6), new[] { "Left arrow", "Right arrow", "Switch boxes", "Wallpaper", "Name", "Cancel" }),
                     at(9).Touch, at(9).Flow, Fixed: Fix(new[] { 6, 7 }, TouchOnly)),
            new Page("Wallpaper", Join(Numbered("Wallpaper", 4), new[] { "Left arrow", "Right arrow", "Change", "Cancel" }), at(10).Touch, at(10).Flow,
                     Fixed: Fix(new[] { 4, 5 }, TouchOnly)),
        };

        // The menu buttons, OK, Cancel and the box picker's buttons are windows ov14 places itself (column 21, three rows apart),
        // so a moved touch area would leave its button behind.
        private static readonly HashSet<string> CodePlaced = new HashSet<string>
        {
            "Back", "Deposit", "Withdraw", "Summary", "Mark", "Release", "Exit", "Move", "Item", "Give", "Cancel", "OK", "Change",
            "Wallpaper", "Name", "Switch boxes", "Quit", "Swap", "Party", "Move Pokémon", "Move items",
        };

        private static Page LockMenu(Page page)
        {
            Dictionary<int, string> locked = page.Locked?.ToDictionary(kv => kv.Key, kv => kv.Value) ?? new Dictionary<int, string>();
            for (int b = 0; b < page.Buttons.Length; b++)
                if (CodePlaced.Contains(page.Buttons[b])) locked.TryAdd(b, TouchRects.FixedArt);
            return page with { Locked = locked };
        }

        /// <summary>The PC's layouts, or null when this ROM's tables aren't known.</summary>
        public static GridMenuButtons Pc()
        {
            RomInfo.PcBoxSites sites = RomLocator.PcBoxSites;
            byte[] file = sites == null ? null : ScreenTables.ReadFile(sites.Overlay);
            if (file == null || sites.Layouts + 11 * 12 > file.Length) return null;
            long ram = OverlayUtils.OverlayTable.GetRAMAddress(sites.Overlay);
            // Every layout has both tables, so a pointer outside the overlay means the tables aren't the game's.
            int Pointer(int at)
            {
                long to = BitConverter.ToUInt32(file, at) - ram;
                return to >= 0 && to < file.Length ? (int)to : int.MinValue;
            }
            (int, int) At(int layout) => (Pointer((int)sites.Layouts + layout * 12), Pointer((int)sites.Layouts + layout * 12 + 4));
            if (Enumerable.Range(0, 11).Any(l => At(l).Item1 < 0 || At(l).Item2 < 0)) return null;
            return GridMenuButtons.Read(sites.Overlay, PcPages(At).Select(LockMenu).ToArray());
        }

        private const string Column = "Goes back to the column you came from";
        private const string ListMath = "The game works out the rows from this area";

        /// <summary>The Pokédex search pages, or null when this ROM's tables aren't known.</summary>
        public static GridMenuButtons Pokedex()
        {
            RomInfo.PokedexSearchSites sites = RomLocator.PokedexSearchSites;
            if (sites == null) return null;
            int T(int i) => (int)sites.Touch[i];
            int L(int i) => (int)sites.Lists[i];
            int F(int i) => (int)sites.Flow[i];
            // The name page has a stop per letter: 27 in the West, 45 kana in Japanese, 15 in Korean.
            int letters = RomInfo.GameLanguage switch { RomInfo.GameLanguages.Japanese => 45, RomInfo.GameLanguages.Korean => 15, _ => 27 };
            CursorDirection[] upDown = { CursorDirection.Up, CursorDirection.Down };
            // Text windows are 8-byte templates with x and y at byte 1, in tiles; highlights are x, y, width and height in tiles.
            TouchListButtons.Spots Window(int entry, int button, int count = 1) =>
                new TouchListButtons.Spots((int)sites.Art[0] + entry * 8, 8, 1, 1, 8, button, count);
            TouchListButtons.Spots Highlight(int table, int count) => new TouchListButtons.Spots((int)sites.Art[table], 4, 0, 1, 8, 0, count);
            ScreenTiles Art(int member, int count, int plainFromRow, int around, int below) =>
                new ScreenTiles(RomInfo.DirNames.PokedexGraphics, member, Enumerable.Range(0, count).ToArray(), Grid: 5, PlainFromRow: plainFromRow,
                                Left: around, Top: around, Right: around, Bottom: below, Rebuild: true);
            // The search menu's values are drawn twice, at templates 8 to 16 and again at 27 to 35.
            (int Entry, int Button)[] values = { (8, 0), (9, 1), (10, 2), (11, 2), (12, 3), (13, 3), (14, 4), (15, 4), (16, 5) };
            TouchListButtons.Spots[] searchText = values.SelectMany(v => new[] { Window(v.Entry, v.Button), Window(v.Entry + 19, v.Button) }).ToArray();
            // ov18_021E8BF4.c PokedexApp_MainSeq_22: the flash on a pressed value starts at Order's x. Type's y and Area's width copy x.
            int f = (int)sites.Flash;
            GridMenuButtons.CodeSpot[] flash =
            {
                new GridMenuButtons.CodeSpot(0, f, f + 0x2), new GridMenuButtons.CodeSpot(1, f + 0x26, f + 0x28),
                new GridMenuButtons.CodeSpot(2, f + 0x48, f + 0x4E), new GridMenuButtons.CodeSpot(3, f + 0x72, f + 0x74),
                new GridMenuButtons.CodeSpot(4, f + 0x98, f + 0x9A), new GridMenuButtons.CodeSpot(5, f + 0xBA, f + 0xC0, f + 0xC2),
            };
            // Reset, Start, Cancel and OK sit on a bar every page shares, and code places the shape picture. Search values slide along
            // bands that stretch behind them, and boxes moved on other pages leave the grid showing.
            Page[] pages =
            {
                new Page("Search", new[] { "Order", "Name", "Type", "Height", "Weight", "Area", "Shape", "Reset", "Start", "Cancel" }, T(0), F(0),
                         FixedArt(6, 7, 8, 9), Spots: searchText, Tiles: new ScreenTiles(RomInfo.DirNames.PokedexGraphics, 57, Enumerable.Range(0, 6).ToArray(), Grid: 5, PlainFromRow: 19,
                                                Bands: new ScreenBands(new[] { 15, 6, 6, 6, 6, 6 }, new[] { 8, 3, 3, 3, 3, 3 }, Lead: 1, Characters: 58, Room: 512)), Code: flash),
                new Page("Search: order", new[] { "Number", "A to Z", "Heaviest", "Lightest", "Tallest", "Shortest", "OK", "Cancel" }, T(1), F(1),
                         FixedArt(6, 7), Spots: new[] { Window(38, 0, 6), Highlight(1, 6) }, Tiles: Art(62, 6, 17, 1, 1)),
                // ov18_021F7060, ov18_021F71DC and ov18_021F7800 send Up and Down from OK and Cancel back to the column last visited.
                new Page("Search: name", Join(Numbered("Letter", letters), new[] { "OK", "Cancel" }), T(2), F(2), Fixed: Fix(new[] { letters, letters + 1 }, Column, upDown)),
                new Page("Search: type", Join(Numbered("Type", 18), new[] { "OK", "Cancel" }), T(3), F(3), FixedArt(18, 19), Fix(new[] { 18, 19 }, Column, upDown),
                         Spots: new[] { Window(47, 0, 18), Highlight(2, 18) }, Tiles: Art(64, 18, 20, 0, 1)),
                new Page("Search: height", new[] { "Maximum", "Minimum", "OK", "Cancel" }, T(4), F(4)),
                new Page("Search: weight", new[] { "Maximum", "Minimum", "OK", "Cancel" }, T(5), F(5)),
                new Page("Search: area", new[] { "Johto", "Kanto", "Unknown", "None", "OK", "Cancel" }, T(6), F(6), FixedArt(4, 5),
                         Spots: new[] { Window(76, 0, 4), Highlight(3, 4) }, Tiles: Art(67, 4, 18, 1, 1)),
                new Page("Search: shape", Join(Numbered("Shape", 15), new[] { "OK", "Cancel" }), T(7), F(7), Fixed: Fix(new[] { 15, 16 }, Column, upDown)),
                // The other pages are touch only. The list and its scroll bar turn a touch into a row with fixed sizes in code.
                new Page("Cover", new[] { "Pokédex switch", "Cover", "Search", "Open", "Quit" }, L(0), -1),
                new Page("List", new[] { "List", "Cover handle", "Up", "Down", "Scroll bar", "Language left", "Language right", "Search", "Cry", "Info",
                                         "Quit" }, L(1), -1, Locked(new[] { 0, 2, 3, 4 }, ListMath)),
                new Page("Height and weight arrows", new[] { "Maximum right", "Maximum left", "Minimum right", "Minimum left" }, L(2), -1),
                new Page("Detail tabs", new[] { "Area", "Size", "Forms", "Back" }, L(3), -1),
                new Page("Area map", Join(Numbered("Place", 4), new[] { "Up", "Down", "Scroll bar", "Johto", "Kanto", "Morning", "Day", "Night" }), L(4), -1,
                         Locked(new[] { 0, 1, 2, 3, 4, 5, 6 }, ListMath)),
                new Page("Forms", Join(Numbered("Form", 4), new[] { "Up", "Down", "Scroll bar", "Compare" }), L(5), -1, Locked(new[] { 0, 1, 2, 3, 4, 5, 6 }, ListMath)),
                new Page("Compare forms", new[] { "Left up", "Left down", "Right up", "Right down", "Switch", "Back", "Cursor left", "Cursor right" }, L(6), -1),
                new Page("Back from details", new[] { "Back" }, L(7), -1),
            };
            return GridMenuButtons.Read(sites.Overlay, pages);
        }
    }
}
