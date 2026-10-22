using System.Collections.Generic;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// Text archives the games print in their own window instead of the message box. Line breaks there don't wait for a button.
    /// </summary>
    public static class FieldTextArchives
    {
        /// <summary>A screen that prints a whole message at once, in a window given in tiles.</summary>
        public sealed record Screen(string Name, int TileLeft, int TileTop, int TilesWide, int TilesHigh);

        // The error screens: one message in a 26 by 18 tile window at 3,3. pokeheartgold save_data_read_error.c, save_data_write_error.c,
        // error_message_reset.c; pokeplatinum unk_0209A74C.c, unk_0209AA74.c, error_message_reset.c. US archive numbers.
        private static readonly Dictionary<int, string> HgssErrorScreens = new()
        {
            [8] = "Save data read error",
            [9] = "Save data write error",
            [41] = "Communication error",
        };
        private static readonly Dictionary<int, string> PlatErrorScreens = new()
        {
            [5] = "Save data read error",
            [6] = "Save data write error",
            [214] = "Communication error",
        };

        /// <summary>Null for message box text, or when not known yet.</summary>
        public static Screen ScreenFor(int archive)
        {
            // Japanese numbers its archives differently, and these haven't been matched there yet.
            if (RomInfo.GameLanguage == RomInfo.GameLanguages.Japanese) return null;
            Dictionary<int, string> screens = RomInfo.GameFamily switch
            {
                RomInfo.GameFamilies.HGSS => HgssErrorScreens,
                RomInfo.GameFamilies.Plat => PlatErrorScreens,
                _ => null,
            };
            if (screens == null) return null;
            foreach (KeyValuePair<int, string> entry in screens)
                if (RomInfo.TextBank(entry.Key) == archive)
                    return new Screen(entry.Value, 3, 3, 26, 18);
            return null;
        }

        /// <summary>In pixels from the text area's corner; a right-aligned line ends at X.</summary>
        public sealed record Placed(int Line, int X, int Y, bool Right = false);

        /// <summary>
        /// A framed field window that prints several lines of one archive in the system font. <paramref name="CursorOn"/> is the entry the
        /// cursor starts on, or -1.
        /// </summary>
        public sealed record Window(string Name, int Archive, int TileLeft, int TileTop, int TilesWide, int TilesHigh,
                                    IReadOnlyList<Placed> Text, int CursorOn = -1);

        // pokeplatinum overlay005/save_info_window.c, pokeheartgold Field_SaveStatsPrinter_Print.
        private static Window SaveWindow(int archive, int place, int firstLabel, int firstValue) =>
            new Window("Save window", archive, 1, 1, 13, 10, new List<Placed>
            {
                new Placed(place, 0, 0),
                new Placed(firstLabel, 0, 16), new Placed(firstValue, 104, 16, true),
                new Placed(firstLabel + 1, 0, 32), new Placed(firstValue + 1, 104, 32, true),
                new Placed(firstLabel + 2, 0, 48), new Placed(firstValue + 2, 104, 48, true),
                new Placed(firstLabel + 3, 0, 64), new Placed(firstValue + 3, 104, 64, true),
            });

        // ShowMoney's window at 20,2. pokeplatinum FieldMenu_CreateMoneyWindow, pokeheartgold FieldSystem_ShowMoneyBox.
        private static Window MoneyWindow(int archive, int label, int amount) =>
            new Window("Money window", archive, 20, 2, 10, 4, new List<Placed> { new Placed(label, 0, 0), new Placed(amount, 80, 16, true) });

        private static readonly Window[] PlatWindows =
        {
            SaveWindow(534, 0, 1, 5),
            MoneyWindow(543, 18, 19),
            // pokeplatinum overlay007/shop_menu.c.
            new Window("Mart menu", 543, 1, 1, 13, 6, new List<Placed> { new Placed(15, 8, 0), new Placed(16, 8, 16), new Placed(17, 8, 32) }, 0),
            // The Battle Point exchange's yes/no.
            new Window("Yes/no box", 543, 23, 13, 7, 4, new List<Placed> { new Placed(29, 8, 0), new Placed(30, 8, 16) }, 0),
        };

        private static readonly Window[] HgssWindows =
        {
            SaveWindow(423, 0, 5, 9),
            SaveWindow(423, 0, 5, 13),
            MoneyWindow(435, 30, 31),
        };

        /// <summary>The field windows that print the line, with the US archive number mapped to this ROM's.</summary>
        public static List<Window> WindowsFor(int archive, int line)
        {
            List<Window> found = new List<Window>();
            if (RomInfo.GameLanguage == RomInfo.GameLanguages.Japanese) return found;
            Window[] windows = RomInfo.GameFamily switch
            {
                RomInfo.GameFamilies.HGSS => HgssWindows,
                RomInfo.GameFamilies.Plat => PlatWindows,
                _ => null,
            };
            if (windows == null) return found;
            foreach (Window w in windows)
            {
                if (RomInfo.TextBank(w.Archive) != archive) continue;
                foreach (Placed p in w.Text)
                    if (p.Line == line) { found.Add(w); break; }
            }
            return found;
        }
    }
}
