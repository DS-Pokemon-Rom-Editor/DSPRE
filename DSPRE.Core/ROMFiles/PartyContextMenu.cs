using System;
using System.Collections.Generic;
using System.Linq;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// The HGSS party menu's buttons (pokeheartgold party_context_menu.c): each button's frame and touch area, and where the arrow
    /// keys go in the Pokémon menu and the item menu for each number of options.
    /// </summary>
    public sealed class PartyContextMenu
    {
        public enum Menu { Pokemon, Item }

        /// <summary>Button names by window, matching the label names in the Layout tab.</summary>
        public const int Buttons = 12, Quit = 7, FirstSubmenu = 8;

        public static string ButtonName(int window) => window switch
        {
            < Quit => $"Menu button {window + 1}",
            Quit => "Quit button",
            _ => $"Submenu button {window - FirstSubmenu + 1}",
        };

        private const int MenuTouchCount = 8, SubmenuTouchCount = 5, Tile = 8;

        /// <summary>Options counted with Quit: the Pokémon menu has 2 to 8, the item menu 2 to 5.</summary>
        public static int MostOptions(Menu menu) => menu == Menu.Pokemon ? 8 : 5;

        // Up, Down, then one entry Left and Right share; the arrow tables are ints.
        private static int Directions(Menu menu) => menu == Menu.Pokemon ? 3 : 2;

        private readonly ScreenTables _tables;
        private readonly int _frames, _windows, _menuTouch, _submenuTouch, _menuArrows, _submenuArrows;
        private readonly byte[] _data;

        private PartyContextMenu(RomInfo.PartyMenuSites sites, ScreenTables tables)
        {
            _tables = tables;
            RomLocator.PartyMenuTables t = RomLocator.PartyMenuDistances;
            int a = (int)sites.Anchor, touch = (int)sites.Touch, arm9 = ScreenTables.InArm9;
            _frames = tables.Take(arm9, a + t.Frames, Buttons * 4);
            _windows = tables.Take(arm9, a + t.Windows, 8 * 2 * 8);
            _menuTouch = tables.Take(arm9, touch + t.MenuTouch, MenuTouchCount * TouchRects.Size);
            _submenuTouch = tables.Take(arm9, touch + t.SubmenuTouch, SubmenuTouchCount * TouchRects.Size);
            _menuArrows = tables.Take(arm9, touch + t.MenuArrows, 7 * 8 * 3 * 4);
            _submenuArrows = tables.Take(arm9, touch + t.SubmenuArrows, 4 * 5 * 2 * 4);
            _data = tables.Finish();
        }

        /// <summary>Null when this isn't HGSS or the tables don't look like the game's.</summary>
        public static PartyContextMenu Read()
        {
            if (RomLocator.PartyMenuSites is not RomInfo.PartyMenuSites sites) return null;
            ScreenTables tables = new ScreenTables();
            byte[] arm9 = tables.FileOf(ScreenTables.InArm9);
            if (arm9 == null) return null;
            RomLocator.PartyMenuTables t = RomLocator.PartyMenuDistances;
            bool Ends(long at) => at >= 0 && at < arm9.Length && arm9[at] == 0xFF;
            // Both touch lists end where the game's do, and Quit's frame is where both lists put its touch area.
            if (!Ends(sites.Touch + t.MenuTouch + MenuTouchCount * TouchRects.Size) || !Ends(sites.Touch + t.SubmenuTouch + SubmenuTouchCount * TouchRects.Size))
                return null;
            PartyContextMenu menu = new PartyContextMenu(sites, tables);
            return !tables.Missing && menu.FrameMatchesTouch(Quit) ? menu : null;
        }

        /// <summary>The button's frame in tiles.</summary>
        public (int X, int Y, int W, int H) Frame(int window)
        {
            int at = _frames + window * 4;
            return (_data[at], _data[at + 1], _data[at + 2], _data[at + 3]);
        }

        // Each touch list covers its frames exactly. The Pokémon menu's list covers windows 0 to 7, and the item menu's covers 8 to 11, then Quit.
        private IEnumerable<int> TouchOf(int window)
        {
            if (window < MenuTouchCount) yield return _menuTouch + window * TouchRects.Size;
            if (window >= FirstSubmenu) yield return _submenuTouch + (window - FirstSubmenu) * TouchRects.Size;
            if (window == Quit) yield return _submenuTouch + (SubmenuTouchCount - 1) * TouchRects.Size;
        }

        private bool FrameMatchesTouch(int window)
        {
            (int x, int y, int w, int h) = Frame(window);
            return TouchOf(window).All(at => _data[at] == y * Tile && _data[at + 1] == (y + h) * Tile && _data[at + 2] == x * Tile
                                             && _data[at + 3] == (byte)((x + w) * Tile));
        }

        /// <summary>Moves the button's frame and touch area by whole tiles; the Layout moves its label.</summary>
        public void Move(int window, int dx, int dy)
        {
            (int x, int y, int w, int h) = Frame(window);
            Place(window, Math.Clamp(x + dx, 0, 32 - w), Math.Clamp(y + dy, 0, 24 - h), w, h);
        }

        /// <summary>Sets the frame's size in tiles from its top left corner.</summary>
        public void Resize(int window, int width, int height)
        {
            (int x, int y, int _, int _) = Frame(window);
            Place(window, x, y, Math.Clamp(width, 1, 32 - x), Math.Clamp(height, 1, 24 - y));
        }

        // A right edge of 256 is stored as 0, like in the game's lists.
        private void Place(int window, int x, int y, int w, int h)
        {
            int f = _frames + window * 4;
            (_data[f], _data[f + 1], _data[f + 2], _data[f + 3]) = ((byte)x, (byte)y, (byte)w, (byte)h);
            foreach (int at in TouchOf(window))
                (_data[at], _data[at + 1], _data[at + 2], _data[at + 3]) = ((byte)(y * Tile), (byte)((y + h) * Tile), (byte)(x * Tile), (byte)((x + w) * Tile));
        }

        /// <summary>The window each option shows in, Quit last.</summary>
        public int[] OptionWindows(Menu menu, int options) =>
            Enumerable.Range(0, options).Select(i => (int)(sbyte)_data[_windows + ((options - 1) * 2 + (int)menu) * 8 + i]).ToArray();

        private int ArrowAt(Menu menu, int options, int option, CursorDirection d)
        {
            int slot = d switch { CursorDirection.Up => 0, CursorDirection.Down => 1, _ => 2 };
            int rows = menu == Menu.Pokemon ? 8 : 5;
            return (menu == Menu.Pokemon ? _menuArrows : _submenuArrows) + (((options - 2) * rows + option) * Directions(menu) + slot) * 4;
        }

        /// <summary>The option the arrow goes to, or -1 for none; Left and Right share one entry, and the item menu has neither.</summary>
        public int Target(Menu menu, int options, int option, CursorDirection d) => Target(_data, menu, options, option, d);

        private int Target(byte[] data, Menu menu, int options, int option, CursorDirection d) =>
            menu == Menu.Item && d is CursorDirection.Left or CursorDirection.Right ? -1 : BitConverter.ToInt32(data, ArrowAt(menu, options, option, d));

        public void SetTarget(Menu menu, int options, int option, CursorDirection d, int target)
        {
            if (menu == Menu.Item && d is CursorDirection.Left or CursorDirection.Right) return;
            BitConverter.TryWriteBytes(_data.AsSpan(ArrowAt(menu, options, option, d)), target);
        }

        public byte[] ToBytes() => _tables.Copy();
        public void FromBytes(byte[] bytes) => _tables.Load(bytes);
        public byte[] FileBytes() => _tables.FileBytes();

        // ListMenu selections past the menu's options read the wrong window, so every arrow names an option or -1.
        private IEnumerable<string> Problems(byte[] data)
        {
            foreach (Menu menu in new[] { Menu.Pokemon, Menu.Item })
                for (int n = 2; n <= MostOptions(menu); n++)
                    for (int o = 0; o < n; o++)
                        foreach (CursorDirection d in CursorDirections.All)
                        {
                            int t = Target(data, menu, n, o, d);
                            if (t < -1 || t >= n) yield return $"{(menu == Menu.Pokemon ? "Pokémon" : "Item")} menu with {n} options: {d} goes to an option that isn't there.";
                        }
        }

        public string WhyNot() => _tables.NewProblem(Problems, _data);

        public string WhyNotWrite(byte[] bytes) =>
            bytes == null || bytes.Length != _data.Length ? "The party menu tables don't match this ROM." : _tables.NewProblem(Problems, bytes);

        public void WriteFile(byte[] bytes)
        {
            if (WhyNotWrite(bytes) is string why) throw new InvalidOperationException(why);
            _tables.Write(bytes);
        }
    }
}
