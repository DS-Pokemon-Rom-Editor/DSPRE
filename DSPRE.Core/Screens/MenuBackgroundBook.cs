using System;
using System.Collections.Generic;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Screens
{
    /// <summary>
    /// The backgrounds of a menu screen whose bottom background the bottom screen palettes patch can widen: Platinum's
    /// party screen, bag, PC boxes and naming screen, and the HGSS bag. The code draws everything else.
    /// </summary>
    public sealed class MenuBackgroundBook : ScreenBook
    {
        private readonly List<ScreenPage> _pages = new();
        private readonly string _title;
        private readonly DirNames _archive;

        private MenuBackgroundBook(string title, DirNames archive, params ScreenPage[] pages)
        {
            _title = title;
            _archive = archive;
            _pages.AddRange(pages);
        }

        /// <summary>A screen's bottom background, with its extended palettes once the patch is in.</summary>
        private static ScreenBg Bottom(BottomPalettesPatch.Screen screen, int bg, int priority, int arrangement, int load = 0, int mapWidth = 32, string when = null)
        {
            int extended = BottomPalettesPatch.PaletteMember(screen, load);
            return new ScreenBg
            {
                Bg = bg, Priority = priority, Drawing = screen.Loads[load].Drawing, ExtendedColours = extended, MapWidth = mapWidth, When = when,
                TileLimit = extended >= 0 ? screen.Loads[load].TileLimit : 0, Pieces = { new ScreenPaste { Screen = arrangement } },
            };
        }

        /// <summary>
        /// The PC boxes' bottom screen. The box list stacks three layers on one drawing. The side panels start slid to
        /// the left edge. Compare mode shows one of three layers, by what is compared.
        /// </summary>
        public static MenuBackgroundBook Pc()
        {
            BottomPalettesPatch.Screen pc = BottomPalettesPatch.Pc;
            ScreenPage box = new() { Id = "box", Name = "Box list" };
            box.Sub.Palettes.Add(new ScreenPaletteLoad { File = pc.Loads[0].Colours, Rows = 3 });
            box.Sub.Bgs.Add(Bottom(pc, 3, 3, 124));
            box.Sub.Bgs.Add(Bottom(pc, 2, 2, 125, mapWidth: 64));
            box.Sub.Bgs.Add(Bottom(pc, 1, 1, 126, mapWidth: 64));
            const string Battle = "Battle stats", Contest = "Contest stats", Moves = "Moves";
            ScreenPage compare = new() { Id = "compare", Name = "Compare", Variants = new[] { Battle, Contest, Moves } };
            compare.Sub.Palettes.Add(new ScreenPaletteLoad { File = pc.Loads[1].Colours, Rows = 4 });
            compare.Sub.Bgs.Add(Bottom(pc, 1, 1, 129, 1, when: Battle));
            compare.Sub.Bgs.Add(Bottom(pc, 2, 2, 130, 1, when: Contest));
            compare.Sub.Bgs.Add(Bottom(pc, 3, 3, 131, 1, when: Moves));
            return new MenuBackgroundBook("PC boxes", DirNames.BoxGraphics, box, compare);
        }

        /// <summary>The party screen: the menu on the top screen and the Poké Ball pattern under the touch buttons.</summary>
        public static MenuBackgroundBook Party()
        {
            BottomPalettesPatch.Screen party = BottomPalettesPatch.Party;
            ScreenPage page = new() { Id = "party", Name = "Party" };
            page.Main.Palettes.Add(new ScreenPaletteLoad { File = 16 });
            page.Main.Bgs.Add(new ScreenBg { Bg = 3, Priority = 3, Drawing = 15, Pieces = { new ScreenPaste { Screen = 17 } } });
            page.Sub.Palettes.Add(new ScreenPaletteLoad { File = party.Colours, Rows = 1 });
            page.Sub.Bgs.Add(Bottom(party, 1, 1, 14));
            return new MenuBackgroundBook("Party screen", DirNames.PartyMenuGraphics, page);
        }

        /// <summary>The naming screen's backdrop, on the top engine, which the game shows on the bottom screen.</summary>
        public static MenuBackgroundBook Naming()
        {
            BottomPalettesPatch.Screen naming = BottomPalettesPatch.Naming;
            ScreenPage page = new() { Id = "naming", Name = "Naming screen", MainOnTop = false };
            page.Main.Palettes.Add(new ScreenPaletteLoad { File = naming.Colours, Rows = 3 });
            // The name and title windows on this layer draw in colours 1, 14 and 15 of extended palette 0, so imports keep its first 16.
            page.Main.GameColours.Add(new ScreenPaletteLoad { File = naming.Colours, Rows = 1 });
            ScreenBg back = Bottom(naming, 2, 3, NamingScreenMembers.Backdrop);
            back.Drawing = BottomPalettesPatch.DrawingMember(naming);
            page.Main.Bgs.Add(back);
            return new MenuBackgroundBook("Naming screen", DirNames.NameInputGraphics, page);
        }

        /// <summary>
        /// The bag's bottom screen. Platinum's is the Poké Ball border over the dial, which reads the whole standard palette.
        /// HGSS's is the item buttons over the backdrop, recoloured per pocket.
        /// </summary>
        public static MenuBackgroundBook Bag() => GameFamily == GameFamilies.HGSS ? BagHgss() : BagPlatinum();

        private static readonly string[] Pockets = { "Items", "Medicine", "Poké Balls", "TMs and HMs", "Berries", "Mail", "Battle items", "Key items" };

        // bag_app_main.c: file 38 fills the rows, file 8 rows 8 to 11, and BagApp_LoadBgPaletteByPocket copies the pocket's rows
        // from file 41 into rows 0 to 3 (row 2 takes the next pocket's). BG1 holds the buttons and BG2 the backdrop of each layout.
        private static MenuBackgroundBook BagHgss()
        {
            BottomPalettesPatch.Screen bag = BottomPalettesPatch.Bag;
            ScreenPage pockets = new() { Id = "bag", Name = "Bag", Variants = Pockets };
            Colours(pockets.Sub, bag, Pockets);
            pockets.Sub.Bgs.Add(Bottom(bag, 1, 2, 43));
            pockets.Sub.Bgs.Add(Bottom(bag, 2, 3, 39));
            const string Move = "Move", Menu = "Item menu", Toss = "Toss", Sell = "Sell";
            ScreenPage menus = new() { Id = "menus", Name = "Menus", Variants = new[] { Move, Menu, Toss, Sell } };
            Colours(menus.Sub, bag, new[] { (string)null });
            ScreenBg buttons = Bottom(bag, 1, 2, 43);
            buttons.Pieces[0].When = $"{Menu}|{Toss}|{Sell}";
            buttons.Pieces.Add(new ScreenPaste { Screen = 44, When = Move });
            menus.Sub.Bgs.Add(buttons);
            ScreenBg back = Bottom(bag, 2, 3, 42);
            back.Pieces[0].When = Move;
            back.Pieces.Add(new ScreenPaste { Screen = 45, When = Menu });
            back.Pieces.Add(new ScreenPaste { Screen = 52, When = Toss });
            back.Pieces.Add(new ScreenPaste { Screen = 53, When = Sell });
            menus.Sub.Bgs.Add(back);
            return new MenuBackgroundBook("Bag", DirNames.BagGraphics, pockets, menus);
        }

        // The pocket's rows over rows 0 to 3, by variant. A null pocket shows the Items pocket.
        private static void Colours(ScreenLayout sub, BottomPalettesPatch.Screen bag, IReadOnlyList<string> pockets)
        {
            sub.Palettes.Add(new ScreenPaletteLoad { File = bag.Colours });
            sub.Palettes.Add(new ScreenPaletteLoad { File = 8, ToRow = 8, Rows = 4 });
            for (int p = 0; p < pockets.Count; p++)
                foreach ((int from, int to) in new[] { (p, 0), (p, 1), (p + 1, 2), (p, 3) })
                {
                    sub.Palettes.Add(new ScreenPaletteLoad { File = 41, FromRow = from, ToRow = to, Rows = 1, When = pockets[p] });
                    sub.GameColours.Add(new ScreenPaletteLoad { File = 41, FromRow = from, ToRow = to, Rows = 1, When = pockets[p] });
                }
        }

        private static MenuBackgroundBook BagPlatinum()
        {
            BottomPalettesPatch.Screen bag = BottomPalettesPatch.Bag;
            ScreenPage page = new() { Id = "bag", Name = "Bag" };
            page.Sub.Palettes.Add(new ScreenPaletteLoad { File = bag.Colours });
            page.Sub.Palettes.Add(new ScreenPaletteLoad { File = 18, ToRow = 2, Rows = 2 });
            page.Sub.Bgs.Add(new ScreenBg { Bg = 3, Priority = 2, Drawing = 32, Pieces = { new ScreenPaste { Screen = 33 } } });
            page.Sub.Bgs.Add(Bottom(bag, 1, 1, 17));
            return new MenuBackgroundBook("Bag", DirNames.BagGraphics, page);
        }

        public override string Title => _title;
        public override DirNames Archive => _archive;
        public override IReadOnlyList<ScreenPage> Pages => _pages;

        public override ScreenPageComposer MakeComposer(Func<int, FieldFont> font)
        {
            ScriptNarc archive = null;
            try
            {
                DSUtils.TryUnpackNarcs(new List<DirNames> { _archive });
                archive = new ScriptNarc(_archive);
            }
            catch (Exception ex) { AppLogger.Error(_title + " graphics: " + ex.Message); }
            ScriptNarc files = archive;
            return new ScreenPageComposer(i => files?.Get(i), font, (_, _) => "", _ => null, _ => -1, _ => 0);
        }
    }
}
