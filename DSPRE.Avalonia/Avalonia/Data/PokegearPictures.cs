using System;
using System.Collections.Generic;
using System.Linq;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.Data
{
    /// <summary>The HGSS Pokégear's bottom screen with skin 0, for the touch and cursor tabs.</summary>
    public static class PokegearPictures
    {
        // pokeheartgold pokegear_main.c: the bar is rows 0 to 3 of screen 54, put at rows 20 to 23, coloured by rows 14 and 15 of file 30.
        private const int BarDrawing = 48, BarColours = 30, BarSheet = 54, BarRow = 20, BarRows = 4;
        private const int MarkingsPage = 4, IconPoolPage = 5, PhoneMovingPage = 6, RadioButtonsPage = 7;
        // Radio_ToggleChannelSelectionButtons copies a button's 4 by 4 tiles from row 24 of the radio screen.
        private const int ButtonSide = 4, ButtonRow = 24;

        /// <summary>The page's pixels in PokegearButtons page order, or null when the archives can't be read.</summary>
        /// <param name="radioButtons">The radio buttons' tile spots, from the page being edited.</param>
        /// <param name="dial">The radio dial's screen as edited, or null for the saved one.</param>
        /// <param name="hintRow">The phone hint band's first row, or -1 for none.</param>
        public static byte[] Draw(int page, IReadOnlyList<(int X, int Y)> radioButtons = null, byte[] dial = null, int hintRow = -1)
        {
            try
            {
                Func<int, byte[]> gear = MoveTextScreens.Members(RomInfo.DirNames.PokegearGraphics);
                if (gear == null) return null;
                DsBgScreen s = new DsBgScreen();
                byte[] under = null;
                if (page is MarkingsPage or IconPoolPage) return Markings(s);
                if (page >= RadioButtonsPage) Radio(s, radioButtons, dial);
                // The map frame and phone list are in separate archives. The Bar page shows the phone because the Pokégear opens on it.
                // In overlay_101_021EF1D8.c, skin 0's configure page is screen 16 on drawing 10 with palette file 4.
                (RomInfo.DirNames Dir, int Drawing, int Screen, int Colours)? app = page switch
                {
                    1 => (RomInfo.DirNames.PokegearSkinGraphics, 10, 16, 4),
                    2 => (RomInfo.DirNames.PokegearMapGraphics, 26, 32, 20),
                    0 or 3 or PhoneMovingPage => (RomInfo.DirNames.PokegearPhoneGraphics, 28, 34, 10),
                    _ => null,
                };
                if (app is { } a && MoveTextScreens.Members(a.Dir) is Func<int, byte[]> m)
                {
                    ushort[] colours = NitroBgCodec.ReadColours(m(a.Colours));
                    s.SetPalettes(colours, 0, 14);
                    s.LoadTiles(0x10000, m(a.Drawing));
                    s.InitLayer(1, 1, 0x10000);
                    (int w, ushort[] map) = NitroBgCodec.ReadMap(m(a.Screen));
                    s.LoadMap(1, map, w);
                    // PokegearPhone_PrintContextMenuTooltip: rows 24 to 27 of the phone's screen cover the bar.
                    if (hintRow >= 0) s.Copy(1, 0, hintRow, DsBgScreen.MapSide, BarRows, map, 0, 24, w);
                    // map overlay_101_021E9270.c draws the region map (8 bpp screen 11, drawing 10) under the frame, two rows down.
                    if (page == 2) under = RegionMap(m);
                }
                ushort[] bar = NitroBgCodec.ReadColours(gear(BarColours));
                s.SetPalette(14, bar, 14 * 16);
                s.SetPalette(15, bar, 15 * 16);
                s.LoadTiles(0, gear(BarDrawing));
                s.InitLayer(0, 0, 0);
                (int sheetWidth, ushort[] sheet) = NitroBgCodec.ReadMap(gear(BarSheet));
                s.Copy(0, 0, BarRow, DsBgScreen.MapSide, BarRows, sheet, 0, 0, sheetWidth);
                if (hintRow >= 0) s.Fill(0, 0, 0, hintRow, DsBgScreen.MapSide, BarRows, 0);
                byte[] rgba = s.Render(under);
                // overlay_101_021F49F8.c sprite 4: the dial cursor, pgradio_gra 3/2/1 at a new save's 128,128. Its palette 4 is file 0
                // row 0, loaded after the Pokégear's four.
                if (page >= RadioButtonsPage && MoveTextScreens.Members(RomInfo.DirNames.PokegearRadioGraphics) is Func<int, byte[]> radio
                    && NitroBgCodec.ReadColours(radio(0)) is ushort[] cursorColours)
                    GameTextScreens.Sprite(rgba, radio, 3, 2, 1, 128, 128, 0, _ => DsBgScreen.Row(cursorColours, 0));
                if (page is 0 or 3 && MoveTextScreens.Members(RomInfo.DirNames.PokegearPhoneGraphics) is Func<int, byte[]> phone)
                    PhoneRows(rgba, DsBgScreen.Row(NitroBgCodec.ReadColours(phone(10)), 2));
                return rgba;
            }
            catch (Exception ex) { AppLogger.WarnOnce("PokegearPictures.Draw", ex); return null; }
        }

        // The region map's top left corner, coloured by file 20 (the town map) or 63 (the fly map).
        internal static byte[] RegionMap(Func<int, byte[]> m, int colours = 20)
        {
            NitroBgCodec.BgImage map = Region(m, colours);
            if (map?.Rgba == null) return null;
            byte[] rgba = new byte[256 * 192 * 4];
            for (int y = 16; y < 192; y++)
                for (int x = 0; x < 256 && x < map.Width && y - 16 < map.Height; x++)
                    Buffer.BlockCopy(map.Rgba, ((y - 16) * map.Width + x) * 4, rgba, (y * 256 + x) * 4, 4);
            return rgba;
        }

        /// <summary>The whole region map the town map shows (file 11), in the town map's colours, or null when unreadable.</summary>
        internal static NitroBgCodec.BgImage WholeRegionMap()
        {
            try
            {
                Func<int, byte[]> m = MoveTextScreens.Members(RomInfo.DirNames.PokegearMapGraphics);
                NitroBgCodec.BgImage map = m == null ? null : Region(m, 20);
                if (map?.Rgba == null) return null;
                int width = Math.Min(map.Width, HgssFlySpots.MapColumns * 8), height = Math.Min(map.Height, HgssFlySpots.MapRows * 8);
                byte[] rgba = new byte[width * height * 4];
                for (int y = 0; y < height; y++) Buffer.BlockCopy(map.Rgba, y * map.Width * 4, rgba, y * width * 4, width * 4);
                return new NitroBgCodec.BgImage { Rgba = rgba, Width = width, Height = height };
            }
            catch (Exception ex) { AppLogger.WarnOnce("PokegearPictures.WholeRegionMap", ex); return null; }
        }

        private static NitroBgCodec.BgImage Region(Func<int, byte[]> m, int colours) =>
            NitroBgCodec.Composite(NitroBgCodec.Inflate(m(10)), NitroBgCodec.Inflate(m(colours)), Blocked(NitroBgCodec.Inflate(m(11))));

        // The region map is kept row by row, as the game copies it, but Composite reads a screen wider than 32 tiles as 32 by 32 blocks.
        private static byte[] Blocked(byte[] screen)
        {
            if (screen == null || screen.Length < 0x24) return screen;
            int columns = BitConverter.ToUInt16(screen, 0x18) / 8, rows = BitConverter.ToUInt16(screen, 0x1A) / 8;
            if (columns <= 32) return screen;
            byte[] blocked = (byte[])screen.Clone();
            int blocksAcross = columns / 32;
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < columns; x++)
                {
                    int block = y / 32 * blocksAcross + x / 32, to = 0x24 + (block * 1024 + y % 32 * 32 + x % 32) * 2, from = 0x24 + (y * columns + x) * 2;
                    if (from + 1 < screen.Length && to + 1 < blocked.Length) (blocked[to], blocked[to + 1]) = (screen[from], screen[from + 1]);
                }
            return blocked;
        }

        private static string[] _contacts;
        private static string _contactsRom;

        // The first six contacts in the phone book: name, then title.
        private static string[] Contacts()
        {
            if (_contacts != null && _contactsRom == RomInfo.WorkDir) return _contacts;
            _contactsRom = RomInfo.WorkDir;
            PokegearPhoneBook.Book book = PokegearPhoneBook.Load(out _);
            string[] names = PokegearContactArchives.Names(PokegearPhoneBook.GameContactCount);
            List<string> classes = new TextArchive(RomInfo.TrainerClassMessageNumber).Messages, phone = new TextArchive(RomInfo.TextBank(271)).Messages;
            _contacts = (book?.Entries ?? new List<PokegearPhoneBook.Entry>()).Take(6)
                .SelectMany((e, i) => new[] { names[i], PokegearPhoneBook.TitleText(e.Title, classes, phone) }).ToArray();
            return _contacts;
        }

        // PhoneContactListUI_DrawNameSlotBG draws 24-pixel rows from slot 1 in the Contacts window, its layer scrolled 32 up;
        // PrintNameAndClass puts the name in font 4 at x 16 and the title in font 0 at x 94.
        private static void PhoneRows(byte[] rgba, ushort[] row)
        {
            string[] contacts = Contacts();
            (int left, int top, int _, int _) = ScreenWindowLayouts.Window("pokegear", "Phone", "Contacts", (1, 2, 27, 24));
            int[][] sets = { new[] { 11, 9, 10, 1, 2, 5, 6, 10 }, new[] { 14, 12, 13, 1, 2, 5, 6, 13 } };
            void Fill(int x, int y, int w, int h, int colour)
            {
                (byte r, byte g, byte b) = NitroBgCodec.Colour(row[colour]);
                for (int yy = y; yy < y + h && yy < 192; yy++)
                    for (int xx = x; xx < x + w && xx < 256; xx++)
                    {
                        int at = (yy * 256 + xx) * 4;
                        (rgba[at], rgba[at + 1], rgba[at + 2], rgba[at + 3]) = (r, g, b, 255);
                    }
            }
            for (int slot = 0; slot * 2 < contacts.Length && slot < 6; slot++)
            {
                int[] c = sets[slot % 2];
                int x = left * 8, y = top * 8 - 8 + 24 * slot;
                Fill(x, y, 216, 24, c[0]);
                Fill(x + 8, y, 82, 20, c[1]);
                Fill(x + 90, y, 126, 20, c[2]);
                Fill(x + 1, y + 1, 2, 2, c[7]);
                Fill(x + 8, y, 2, 7, c[0]);
                foreach (int dot in new[] { 9, 13, 17 }) Fill(x + 9, y + dot, 2, 2, c[0]);
                DsBgScreen.DrawText(rgba, GameTextScreens.FontFor(4), contacts[slot * 2], x + 16, y + 2, row[c[3]], row[c[4]]);
                DsBgScreen.DrawText(rgba, GameTextScreens.FontFor(0), contacts[slot * 2 + 1], x + 94, y + 2, row[c[5]], row[c[6]]);
            }
        }

        // PokegearMap_SetBgParam_MarkingsMode hides the bar and shows screen 38's top 14 rows over screen 44, both on the frame's tiles.
        private static byte[] Markings(DsBgScreen s)
        {
            Func<int, byte[]> m = MoveTextScreens.Members(RomInfo.DirNames.PokegearMapGraphics);
            if (m == null) return null;
            ushort[] colours = NitroBgCodec.ReadColours(m(20));
            s.SetPalettes(colours, 0, 14);
            s.LoadTiles(0, m(26));
            s.InitLayer(2, 1, 0);
            s.InitLayer(3, 2, 0);
            (int panelWidth, ushort[] panel) = NitroBgCodec.ReadMap(m(38));
            s.Copy(2, 0, 0, 32, 14, panel, 0, 0, panelWidth);
            (int backWidth, ushort[] back) = NitroBgCodec.ReadMap(m(44));
            s.Copy(3, 0, 0, 32, 24, back, 0, 0, backWidth);
            return s.Render();
        }

        // overlay_101_021F49F8.c draws screen 22 over screen 28 on drawing 16 with palette file 10. Buttons go at their edited spots.
        private static void Radio(DsBgScreen s, IReadOnlyList<(int X, int Y)> buttons, byte[] dialScreen)
        {
            Func<int, byte[]> m = MoveTextScreens.Members(RomInfo.DirNames.PokegearRadioGraphics);
            if (m == null) return;
            ushort[] colours = NitroBgCodec.ReadColours(m(10));
            s.SetPalettes(colours, 0, 14);
            s.LoadTiles(0x10000, m(16));
            s.InitLayer(2, 2, 0x10000);
            s.InitLayer(3, 3, 0x10000);
            (int w, ushort[] dial) = NitroBgCodec.ReadMap(dialScreen ?? m(22));
            s.Copy(2, 0, 0, 32, 24, dial, 0, 0, w);
            (int backWidth, ushort[] back) = NitroBgCodec.ReadMap(m(28));
            s.Copy(3, 0, 0, 32, 24, back, 0, 0, backWidth);
            foreach ((int x, int y) in buttons ?? Array.Empty<(int, int)>()) s.Copy(2, x, y, ButtonSide, ButtonSide, dial, 0, ButtonRow, w);
        }
    }
}
