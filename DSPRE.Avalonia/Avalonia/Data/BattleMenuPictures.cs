using System;
using System.Collections.Generic;
using System.Linq;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.Data
{
    /// <summary>Draws the battle Pokémon and bag pages with each button's art where the game pastes it.</summary>
    public static class BattleMenuPictures
    {
        /// <summary>A button's art entry and where it goes on the page, in tiles.</summary>
        public sealed record Button(int Art, int X, int Y, int W, int H);

        // Front and back layer screens per BattleMenuButtons page (battle_party.c ScreenBackgroundDataMemberIndexes).
        private static readonly (int Front, int Back)[] PokemonScreens = { (1, 0), (19, 18), (5, 4), (3, 2), (7, 6), (9, 8), (11, 10), (15, 14) };

        private const int PocketIcon = 4, PocketIconColumn = 20, PocketIconRow = 47;

        // Which quarter of the 512 by 512 screen each page scrolls to (battle_bag.c SetupBackgroundScroll).
        private static readonly int[] BagQuarters = { 0, 1, 2 };

        // Where each art entry's unpressed block sits in the 32-tile-wide sheets, as (sheet member, tile x, tile y) (battle_party_buttons.c).
        private static (int Member, int X, int Y) PokemonArt(int art) => art switch
        {
            <= 5 => (20, 0, 0),
            6 => (20, 26, 24),
            7 => (21, 0, 0),
            <= 11 => (20, 0, 39),
            12 => (20, 0, 49),
            13 => (20, 0, 54),
            18 => (20, 20, 49),
            28 or 29 => (20, 0, 24),
            >= 30 => (20, 0, 59),
            _ => (21, 0, 51),
        };

        // The same for the bag, which has one sheet, member 1 (battle_bag_buttons.c).
        private static (int Member, int X, int Y) BagArt(int art) => art switch
        {
            <= 3 => (1, 0, 0),
            4 or 15 => (1, 0, 27),
            5 or 14 or 16 => (1, 0, 57),
            12 => (1, 0, 47),
            13 => (1, 0, 52),
            _ => (1, 16, 0),
        };

        // Palette row for each unpressed button (GetButtonColor); item buttons use the first pocket's.
        private static int BagPalette(int art) => art switch
        {
            <= 3 => 0,
            4 => 3,
            5 or 12 or 13 or 14 or 16 => 2,
            _ => 8,
        };

        /// <summary>Each button on the page with its art and current position.</summary>
        public static List<Button> Placed(BattleMenuButtons buttons, int page) =>
            Enumerable.Range(0, buttons.Pages[page].Buttons.Length).Select(b =>
            {
                (int x, int y, int w, int h) = buttons.ArtOf(page, b);
                return new Button(buttons.ArtIndex(page, b), x, y, w, h);
            }).ToList();

        /// <summary>Current text window and icon spots for each button on the page, in BattleMenuButtons.SpotsOf order.</summary>
        public static IReadOnlyList<(int X, int Y)>[] Spots(BattleMenuButtons buttons, int page) =>
            Enumerable.Range(0, buttons.Pages[page].Buttons.Length).Select(b => buttons.SpotsOf(page, b)).ToArray();

        /// <summary>The bottom screen's pixels, or null when the archive can't be read.</summary>
        public static byte[] Draw(BattleMenuButtons.Menu menu, int page, IReadOnlyList<Button> buttons, IReadOnlyList<(int X, int Y)>[] spots = null)
        {
            try { return menu == BattleMenuButtons.Menu.Pokemon ? Pokemon(page, buttons, spots) : Bag(page, buttons, spots); }
            catch (Exception ex) { AppLogger.WarnOnce("BattleMenuPictures.Draw", ex); return null; }
        }

        private static void Palettes(DsBgScreen s, ushort[] colours, int rows)
        {
            s.SetPalettes(colours, 0, rows);
        }

        // A w by h block of a sheet's entries from tile x, y; the sheets are 32 tiles wide.
        private static ushort[] Block(ushort[] sheet, int x, int y, int w, int h)
        {
            ushort[] block = new ushort[w * h];
            for (int row = 0; row < h; row++)
                for (int column = 0; column < w; column++)
                {
                    int at = (y + row) * DsBgScreen.MapSide + x + column;
                    block[row * w + column] = at < sheet.Length ? sheet[at] : (ushort)0;
                }
            return block;
        }

        private static void PasteButtons(DsBgScreen s, int layer, Func<int, byte[]> m, IReadOnlyList<Button> buttons,
                                         Func<int, (int Member, int X, int Y)> art, Func<int, int> palette = null)
        {
            Dictionary<int, ushort[]> sheets = new Dictionary<int, ushort[]>();
            foreach (Button b in buttons)
            {
                (int member, int sx, int sy) = art(b.Art);
                if (!sheets.TryGetValue(member, out ushort[] sheet)) sheets[member] = sheet = NitroBgCodec.ReadMap(m(member)).Entries;
                ushort[] block = Block(sheet, sx, sy, b.W, b.H);
                if (palette != null) for (int i = 0; i < block.Length; i++) block[i] = (ushort)((block[i] & 0x0FFF) | palette(b.Art) << 12);
                s.Put(layer, block, b.X, b.Y, b.W, b.H);
            }
        }

        // battle_party.c gives both layers tiles 22 at 0x8000 and palette 23, and pastes the buttons on the front layer.
        private static byte[] Pokemon(int page, IReadOnlyList<Button> buttons, IReadOnlyList<(int X, int Y)>[] spots)
        {
            Func<int, byte[]> m = MoveTextScreens.Members(RomInfo.DirNames.BattlePartyGraphics);
            if (m == null || page < 0 || page >= PokemonScreens.Length) return null;
            DsBgScreen s = new DsBgScreen();
            ushort[] colours = NitroBgCodec.ReadColours(m(23));
            Palettes(s, colours, 16);
            s.LoadTiles(0x8000, m(22));
            s.InitLayer(3, 3, 0x8000);
            s.LoadMap(3, NitroBgCodec.ReadMap(m(PokemonScreens[page].Back)).Entries, DsBgScreen.MapSide);
            s.InitLayer(2, 2, 0x8000);
            s.LoadMap(2, NitroBgCodec.ReadMap(m(PokemonScreens[page].Front)).Entries, DsBgScreen.MapSide);
            PasteButtons(s, 2, m, buttons, PokemonArt);
            byte[] rgba = s.Render();
            // The "Choose a Pokémon." and "Restore which move?" boxes cover the buttons under them (sMessageBoxWindowTemplates).
            if (page == 0 || page == 5) GameTextScreens.MessageWindow(rgba, 2, 21, 22, 2, GameTextScreens.TalkColours[15]);
            GameTextScreens.BattlePartyText(rgba, page, colours, spots);
            return rgba;
        }

        // battle_bag.c draws tiles 2, screen 0 and twelve palette rows from file 3 on one layer and pastes the buttons into it.
        private static byte[] Bag(int page, IReadOnlyList<Button> buttons, IReadOnlyList<(int X, int Y)>[] spots)
        {
            Func<int, byte[]> m = MoveTextScreens.Members(RomInfo.DirNames.BattleBagGraphics);
            if (m == null || page < 0 || page >= BagQuarters.Length) return null;
            DsBgScreen s = new DsBgScreen();
            ushort[] colours = NitroBgCodec.ReadColours(m(3));
            Palettes(s, colours, 12);
            s.LoadTiles(0, m(2));
            s.InitLayer(2, 3, 0);
            const int quarter = DsBgScreen.MapSide * DsBgScreen.MapSide;
            s.LoadMap(2, NitroBgCodec.ReadMap(m(0)).Entries.Skip(BagQuarters[page] * quarter).Take(quarter).ToArray(), DsBgScreen.MapSide);
            PasteButtons(s, 2, m, buttons, BagArt, BagPalette);
            // AddIconDataToButtonData centres each pocket's 4 by 4 icon in the top rows of its button.
            ushort[] sheet = NitroBgCodec.ReadMap(m(1)).Entries;
            foreach (Button b in buttons.Where(b => b.Art <= 3))
                s.Put(2, Block(sheet, PocketIconColumn, PocketIconRow + b.Art * PocketIcon, PocketIcon, PocketIcon), b.X + (b.W - PocketIcon) / 2, b.Y,
                      PocketIcon, PocketIcon);
            byte[] rgba = s.Render();
            GameTextScreens.BattleBagText(rgba, page, colours, spots);
            return rgba;
        }
    }
}
