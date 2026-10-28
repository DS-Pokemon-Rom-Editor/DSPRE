using System.Collections.Generic;
using System.Linq;
using Spots = DSPRE.ROMFiles.TouchListButtons.Spots;
using static DSPRE.ROMFiles.ButtonNames;

namespace DSPRE.ROMFiles
{
    /// <summary>The HGSS options, main menu, trainer card and number pad touch areas.</summary>
    public static class HgssTouchLists
    {
        // For each options row, the index of its first choice in the touch list and its choice names.
        private static readonly (int First, int Count)[] OptionRows = { (0, 3), (3, 2), (5, 2), (7, 2), (9, 2) };

        public static TouchListButtons Options()
        {
            RomInfo.HgssOptionsSites s = RomLocator.HgssOptionsSites;
            if (s == null) return null;
            string[] buttons =
            {
                "Text speed 1", "Text speed 2", "Text speed 3", "Battle scene 1", "Battle scene 2", "Battle style 1", "Battle style 2",
                "Sound 1", "Sound 2", "Button mode 1", "Button mode 2", "Frame left", "Frame right", "Confirm", "Quit",
            };
            // Each row's highlight x is three s32s, and its label x is three u16s. The arrows, Quit and Confirm are sprite
            // templates 5 to 8, with x and y as s16 at 4.
            List<Spots> spots = OptionRows.Select((r, i) => new Spots((int)s.Highlights + i * 12, 4, 0, 4, 1, r.First, r.Count, XOnly: true)).ToList();
            if (s.Labels != 0)
                spots.AddRange(OptionRows.Select((r, i) => new Spots((int)s.Labels + i * 6, 2, 0, 2, 1, r.First, r.Count, XOnly: true)));
            const int template = 0x28;
            spots.Add(new Spots((int)s.Sprites + 5 * template, template, 4, 2, 1, 11, 2));
            spots.Add(new Spots((int)s.Sprites + 7 * template, template, 4, 2, 1, 14, 1));
            spots.Add(new Spots((int)s.Sprites + 8 * template, template, 4, 2, 1, 13, 1));
            // Quit's and Confirm's text windows sit where these movs immediates say, in tiles.
            spots.Add(new Spots((int)s.QuitX, 1, 0, 1, 8, 14, 1, XOnly: true));
            spots.Add(new Spots((int)s.QuitY, 1, 0, 1, 8, 14, 1, YOnly: true));
            spots.Add(new Spots((int)s.ConfirmX, 1, 0, 1, 8, 13, 1, XOnly: true));
            spots.Add(new Spots((int)s.ConfirmY, 1, 0, 1, 8, 13, 1, YOnly: true));
            // The choices' boxes are tiles in background screen 17; the plain band right of the labels starts at column 14.
            ScreenTiles boxes = new ScreenTiles(RomInfo.DirNames.OptionsGraphics, 17, Enumerable.Range(0, 11).ToArray(), 14);
            return TouchListButtons.Read(new[] { new TouchListButtons.Page("Options", buttons, s.Overlay, (int)s.Touch, Spots: spots.ToArray(), Tiles: boxes) });
        }

        /// <summary>
        /// Where the options screen puts its text: each row's choice label x (null where the language has no table), and the Quit and
        /// Confirm windows' tile spots.
        /// </summary>
        public static (int[][] Labels, (int X, int Y) Quit, (int X, int Y) Confirm) OptionsText(TouchListButtons options)
        {
            int rows = OptionRows.Length, labels = RomLocator.HgssOptionsSites?.Labels > 0 ? rows : 0;
            int[][] x = labels == 0 ? null : Enumerable.Range(0, rows).Select(r => options.SpotsOf(0, rows + r).Select(p => p.X).ToArray()).ToArray();
            int code = rows + labels + 3;
            return (x, (options.SpotsOf(0, code)[0].X, options.SpotsOf(0, code + 1)[0].Y), (options.SpotsOf(0, code + 2)[0].X, options.SpotsOf(0, code + 3)[0].Y));
        }

        public static TouchListButtons MainMenu()
        {
            RomInfo.HgssMainMenuSites s = RomLocator.HgssMainMenuSites;
            if (s == null) return null;
            // Message windows 5 and 6 hold the new game buttons: x, y, width, height and message as u32, in tiles.
            Spots windows = new Spots((int)s.Windows + 5 * 20, 20, 0, 4, 8);
            return TouchListButtons.Read(new[]
            {
                new TouchListButtons.Page("Scroll arrows", new[] { "Up", "Down" }, s.Overlay, (int)s.Arrows, Locked: FixedArt(0, 1)),
                new TouchListButtons.Page("New game warning", new[] { "Begin adventure", "Return" }, s.Overlay, (int)s.NewGame, Spots: new[] { windows }),
            });
        }

        public static TouchListButtons TrainerCard()
        {
            RomInfo.HgssTrainerCardSites s = RomLocator.HgssTrainerCardSites;
            if (s == null) return null;
            // The two buttons' sprites are u8 x and y; their labels are window templates 13 and 12, x and y in tiles at 1.
            Spots[] spots =
            {
                new Spots((int)s.ButtonSprites, 2, 0, 1, 1, 0, 2),
                new Spots((int)s.Windows + 13 * 8, 8, 1, 1, 8, 0, 1),
                new Spots((int)s.Windows + 12 * 8, 8, 1, 1, 8, 1, 1),
            };
            return TouchListButtons.Read(new[]
            {
                new TouchListButtons.Page("Card", new[] { "Cancel", "Hint", "Card" }, s.Overlay, (int)s.Touch, Spots: spots),
            });
        }

        public static GridMenuButtons NumberPad()
        {
            RomInfo.NumberPadSites s = RomLocator.NumberPadSites;
            if (s == null) return null;
            string[] keys = Enumerable.Range(0, 10).Select(i => i.ToString()).Concat(new[] { "Back", "Confirm", "Quit" }).ToArray();
            // Sprites 2 to 4 are Back, Confirm and Quit as u8 x and y; their labels are windows 0 to 2, x and y in tiles at 1.
            Spots[] spots = { new Spots((int)s.Sprites + 2 * 4, 4, 0, 1, 1, 10, 3), new Spots((int)s.Labels, 8, 1, 1, 8, 10, 3) };
            List<GridMenuButtons.Page> pages = new List<GridMenuButtons.Page>
            {
                new GridMenuButtons.Page("Keys", keys, (int)s.Keys, (int)s.Flow, FixedArt(Enumerable.Range(0, 10).ToArray()), Spots: spots),
            };
            pages.AddRange(s.Digits.Select((at, i) => new GridMenuButtons.Page(i == 0 ? "1 digit" : $"{i + 1} digits",
                Enumerable.Range(1, i + 1).Select(d => $"Digit {d}").ToArray(), (int)at, -1)));
            return GridMenuButtons.Read(s.Overlay, pages.ToArray());
        }
    }
}
