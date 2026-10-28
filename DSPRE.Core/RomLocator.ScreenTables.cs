namespace DSPRE
{
    // Screen button tables at fixed distances from each screen's located anchor, measured on every dump: only the anchor moves
    // between languages.
    public static partial class RomLocator
    {
        /// <summary>A page's touch list, its arrow table (-1 for none) and its cursor spot table.</summary>
        public sealed record BagTable(int Touch, int Flow, int Cursor);

        /// <summary>
        /// The HGSS bag's pages from BagButtonSites.ContextFlow, in FieldBagButtons order: pockets and items, item menu, moving an
        /// item, toss amount, sell amount. pokeheartgold bag_app_main.c D-pad and sTouchscreenHitboxes tables, bag_app_sprite.c.
        /// </summary>
        public static readonly BagTable[] BagTables =
        {
            new BagTable(0x15C, 0x118, 0x590), new BagTable(0x28, 0x00, 0x590), new BagTable(0xC8, 0x5C, 0x50C),
            new BagTable(0x80, -1, 0), new BagTable(0x40, -1, 0),
        };

        /// <summary>The HGSS touch menu's other tables, from TouchMenuSites.MenuRects (pokeheartgold asm/overlay_27.s).</summary>
        public sealed record TouchMenuTables(int ExtraRects, int Sprites, int Names, int Flow);

        public static readonly TouchMenuTables TouchMenuDistances = new TouchMenuTables(-0x9C, 0xD0, 0x10C, 0x14C);

        /// <summary>
        /// The HGSS party menu's buttons: frames and window choices from PartyMenuSites.Anchor (sButtonRects, sButtonWindowIDs),
        /// touch lists and arrow tables from PartyMenuSites.Touch.
        /// </summary>
        public sealed record PartyMenuTables(int Frames, int Windows, int MenuTouch, int SubmenuTouch, int MenuArrows, int SubmenuArrows);

        public static readonly PartyMenuTables PartyMenuDistances = new PartyMenuTables(0x228, 0x2F0, 0x7C, 0x64, 0x140, 0xA0);

        /// <summary>
        /// The HGSS summary's touch lists from SummaryTouchSites.Cancel in SummaryTouchButtons order, and the party icons' sprite
        /// templates (s16 x and y, <see cref="SummaryIconStride"/> apart). pokeheartgold asm/unk_02088288.s and unk_0208DE40.s.
        /// </summary>
        public static readonly int[] SummaryTouchLists = { 0x1750, 0x28, 0x00, 0x0C, 0x13C };
        public const int SummaryIcons = 0xD30, SummaryIconStride = 0x28;

        /// <summary>A Pokégear page's touch list, its cursor grids with their entry counts, and its picture spots in tiles (-1 for none).</summary>
        public sealed record PokegearTable(int Overlay, int Touch, (int At, int Count)[] Grids, int Tiles = -1);

        // pokegear_main.c sTouchscreenButtonHitboxes and the four bar grids by registered cards, then the configure, map, phone and
        // radio tables from the map area rects. Japanese packs the later app tables 8 bytes closer.
        /// <summary>The Pokégear's pages in PokegearButtons order.</summary>
        public static PokegearTable[] PokegearTables(RomInfo.PokegearSites s)
        {
            int a = (int)s.MapAreas, jp = s.Japanese ? -8 : 0, app = s.AppOverlay;
            (int, int)[] none = System.Array.Empty<(int, int)>();
            return new[]
            {
                new PokegearTable(s.BarOverlay, (int)s.BarRects, new[] { (0x1CC0, 5), (0x1BDC, 3), (0x1C28, 4), (0x1C58, 4) }),
                new PokegearTable(app, a + 0x540 + jp, new[] { (a + 0x368, 6) }),
                new PokegearTable(app, a + 0x08, none),
                new PokegearTable(app, a + 0x790 + jp, none),
                new PokegearTable(app, a + 0x50, new[] { (a - 0x2A0, 9) }),
                new PokegearTable(app, a + 0x50, new[] { (a - 0x300, 8) }),
                new PokegearTable(app, a + 0x7B4 + jp, none),
                new PokegearTable(app, a + 0xAE0 + jp, none, (int)s.RadioButtonTiles),
                new PokegearTable(app, a + 0xB10 + jp, none),
                new PokegearTable(app, a + 0xAF4 + jp, none),
                new PokegearTable(app, a + 0xB34 + jp, none),
                new PokegearTable(app, a + 0xAD4 + jp, none),
                new PokegearTable(app, a + 0xAC8 + jp, none),
            };
        }

        /// <summary>
        /// The place-name banner's slide steps, hidden offsets and slide calls from PlaceNameBannerSites.Task, the hidden offset's
        /// repeat after Show, and the frame loop's second "% width" after Mod.
        /// </summary>
        public sealed record BannerTables(int SlideIn, int SlideOut, int[] Hidden, int[] SlideCalls, int ShowRepeat, int SecondMod);

        public static readonly BannerTables BannerDistances = new BannerTables(-0x2E, 0x10, new[] { 0x14, 0x18, 0x2A }, new[] { -0x1C, 0x24 }, 6, 10);
    }
}
