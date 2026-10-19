using System.Collections.Generic;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// Every Pokédex page of DP, Platinum and HGSS as the games put it together: which arrangements, drawings and
    /// colours go on each background, which sprites, text and pictures of the sample Pokémon, and the states a page
    /// can be in. Read from each game's own load code (pokediamond, pokeplatinum, pokeheartgold). Generated: change
    /// the page specs and regenerate rather than editing by hand.
    /// </summary>
    public static class DexPageRecipes
    {
        public static IReadOnlyList<ScreenPage> For(GameFamilies family) => family switch
        {
            GameFamilies.DP => DiamondPearl(),
            GameFamilies.Plat => Platinum(),
            GameFamilies.HGSS => HeartGoldSoulSilver(),
            _ => new List<ScreenPage>(),
        };

        private static ScreenPaste P(int screen, int tileX, int tileY, int fromX, int fromY, int width, int height, int palette,
                                  int fill, string when)
            => new ScreenPaste { Screen = screen, TileX = tileX, TileY = tileY, FromX = fromX, FromY = fromY, Width = width,
                              Height = height, Palette = palette, Fill = fill, When = when };

        private static ScreenBg B(int bg, int priority, int bpp, int drawing, int mapWidth, int mapHeight, int scrollX,
                               int scrollY, string when, List<ScreenPaste> pieces)
            => new ScreenBg { Bg = bg, Priority = priority, Bpp = bpp, Drawing = drawing, MapWidth = mapWidth,
                           MapHeight = mapHeight, ScrollX = scrollX, ScrollY = scrollY, When = when, Pieces = pieces };

        private static ScreenPaletteLoad L(int file, int toRow, int fromRow, int rows, string when)
            => new ScreenPaletteLoad { File = file, ToRow = toRow, FromRow = fromRow, Rows = rows, When = when };

        private static ScreenSprite S(string name, int cells, int drawing, int anim, int seq, int frame, int colours, int row,
                                   int x, int y, int priority, int typeSlot, int typeDrawing, int width, int height,
                                   DexSizeRole size, DexReadout readout, int step, int place, string when)
            => new ScreenSprite { Name = name, Cells = cells, Drawing = drawing, Anim = anim, Seq = seq, Frame = frame,
                               Colours = colours, Row = row, X = x, Y = y, Priority = priority, TypeSlot = typeSlot,
                               TypeDrawing = typeDrawing, Width = width, Height = height, Size = size,
                               Readout = readout, Step = step, Place = place, When = when };

        private static ScreenText T(ScreenTextKind kind, int line, int bg, int x, int y, ScreenTextAlign align, int boxHeight, int row,
                                 int ink, int shadow, int font, int sample, int offset, int colours, int priority,
                                 int above, int winX, int winY, int winW, int winH, bool keepUnder, string[] args,
                                 string when)
            => new ScreenText { Kind = kind, Line = line, Bg = bg, X = x, Y = y, Align = align, BoxHeight = boxHeight,
                             Row = row, Ink = ink, Shadow = shadow, Font = font, Sample = sample, Offset = offset,
                             Colours = colours, Priority = priority, Above = above, WinX = winX, WinY = winY,
                             WinW = winW, WinH = winH, KeepUnder = keepUnder, Args = args, When = when };

        private static ScreenRect R(int bg, int x, int y, int width, int height, int row, int colour, string when)
            => new ScreenRect { Bg = bg, X = x, Y = y, Width = width, Height = height, Row = row, Colour = colour, When = when };

        private static ScreenMon M(ScreenMonKind kind, int x, int y, int priority, int darken, int offset, DexSizeRole size,
                                 string when)
            => new ScreenMon { Kind = kind, X = x, Y = y, Priority = priority, Darken = darken, Offset = offset, Size = size,
                            When = when };


        private static List<ScreenPage> DiamondPearl() => new List<ScreenPage>
        {
            new ScreenPage
            {
                Id = "list", Name = "List", MainOnTop = true,
                Variants = new string[] { "Sinnoh", "National", "Search results" },
                Main = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(3, 3, 4, 28, 32, 32, 0, 0, null, new List<ScreenPaste> { P(38, 0, 0, 0, 0, -1, -1, -1, -1, null), P(39, 1, 4, 0, 0, -1, -1, 1, -1, null) }),
                        B(2, 1, 4, 28, 32, 32, 0, 0, null, new List<ScreenPaste> { P(40, 0, 0, 0, 29, -1, 3, -1, -1, "Sinnoh|Search results"), P(40, 0, 18, 0, 0, -1, 14, -1, -1, "Sinnoh|Search results"), P(42, 0, 0, 0, 29, -1, 3, -1, -1, "National"), P(42, 0, 18, 0, 0, -1, 14, -1, -1, "National") }),
                        B(1, 0, 4, -1, 32, 32, 0, 0, null, new List<ScreenPaste> {  }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(0, 0, 0, 16, null),
                        L(4, 0, 0, 1, "Search results"),
                    },
                    Sprites = new List<ScreenSprite>
                    {
                        S("Caught mark", 79, 81, 80, 1, 0, 3, 0, 116, 82, 2, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("List row 5", 79, 81, 80, 0, 0, 3, 0, 170, 82, 2, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("List row 4", 79, 81, 80, 0, 0, 3, 7, 175, 58, 2, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("List row 6", 79, 81, 80, 0, 0, 3, 7, 175, 106, 2, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("List row 3", 79, 81, 80, 0, 0, 3, 8, 177, 42, 2, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("List row 7", 79, 81, 80, 0, 0, 3, 8, 177, 122, 2, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("List row 2", 79, 81, 80, 0, 0, 3, 9, 181, 26, 2, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("List row 8", 79, 81, 80, 0, 0, 3, 9, 181, 138, 2, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("List row 1", 79, 81, 80, 0, 0, 3, 9, 185, 22, 2, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("List row 9", 79, 81, 80, 0, 0, 3, 9, 185, 142, 2, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Scroll marker", 79, 81, 80, 2, 0, 3, 0, 248, 85, 2, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 0, 1, 8, 152, (ScreenTextAlign)0, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "Sinnoh|National"),
                        T((ScreenTextKind)0, 108, 1, 8, 152, (ScreenTextAlign)0, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "Search results"),
                        T((ScreenTextKind)0, 1, 1, 128, 152, (ScreenTextAlign)0, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "Sinnoh|National"),
                        T((ScreenTextKind)7, -1, 1, 48, 170, (ScreenTextAlign)0, 0, 0, 2, 1, 0, 342, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)7, -1, 1, 180, 170, (ScreenTextAlign)0, 0, 0, 2, 1, 0, 210, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "Sinnoh|National"),
                        T((ScreenTextKind)2, -1, 1, 143, 14, (ScreenTextAlign)0, 0, 9, 3, 2, 2, 0, -4, 3, 2, 8, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)1, -1, 1, 170, 14, (ScreenTextAlign)0, 0, 9, 3, 2, 2, 0, -4, 3, 2, 8, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)2, -1, 1, 139, 18, (ScreenTextAlign)0, 0, 9, 3, 2, 2, 0, -3, 3, 2, 6, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)1, -1, 1, 166, 18, (ScreenTextAlign)0, 0, 9, 3, 2, 2, 0, -3, 3, 2, 6, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)2, -1, 1, 135, 34, (ScreenTextAlign)0, 0, 8, 3, 2, 2, 0, -2, 3, 2, 4, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)1, -1, 1, 162, 34, (ScreenTextAlign)0, 0, 8, 3, 2, 2, 0, -2, 3, 2, 4, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)2, -1, 1, 133, 50, (ScreenTextAlign)0, 0, 7, 3, 2, 2, 0, -1, 3, 2, 2, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)1, -1, 1, 160, 50, (ScreenTextAlign)0, 0, 7, 3, 2, 2, 0, -1, 3, 2, 2, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)2, -1, 1, 128, 74, (ScreenTextAlign)0, 0, 0, 3, 2, 2, 0, 0, 3, 2, 1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)1, -1, 1, 155, 74, (ScreenTextAlign)0, 0, 0, 3, 2, 2, 0, 0, 3, 2, 1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)2, -1, 1, 133, 98, (ScreenTextAlign)0, 0, 7, 3, 2, 2, 0, 1, 3, 2, 3, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)1, -1, 1, 160, 98, (ScreenTextAlign)0, 0, 7, 3, 2, 2, 0, 1, 3, 2, 3, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)2, -1, 1, 135, 114, (ScreenTextAlign)0, 0, 8, 3, 2, 2, 0, 2, 3, 2, 5, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)1, -1, 1, 162, 114, (ScreenTextAlign)0, 0, 8, 3, 2, 2, 0, 2, 3, 2, 5, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)2, -1, 1, 139, 130, (ScreenTextAlign)0, 0, 9, 3, 2, 2, 0, 3, 3, 2, 7, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)1, -1, 1, 166, 130, (ScreenTextAlign)0, 0, 9, 3, 2, 2, 0, 3, 3, 2, 7, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)2, -1, 1, 143, 134, (ScreenTextAlign)0, 0, 9, 3, 2, 2, 0, 4, 3, 2, 9, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)1, -1, 1, 170, 134, (ScreenTextAlign)0, 0, 9, 3, 2, 2, 0, 4, 3, 2, 9, 0, 0, 0, 0, false, null, null),
                    },
                    Mons = new List<ScreenMon>
                    {
                        M((ScreenMonKind)0, 56, 80, 2, 0, 0, (DexSizeRole)0, null),
                    },
                },
                Sub = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(2, 2, 4, 29, 32, 32, 0, 0, null, new List<ScreenPaste> { P(41, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                        B(3, 3, 8, 27, 32, 32, -120, 0, null, new List<ScreenPaste> { P(37, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(0, 0, 0, 16, null),
                        L(25, 3, 0, 1, "National"),
                        L(1, 3, 0, 1, "Search results"),
                    },
                    Sprites = new List<ScreenSprite>
                    {
                        S("Search", 82, 84, 83, 2, 0, 3, 0, 48, 40, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "Sinnoh|National"),
                        S("Switch", 82, 84, 83, 0, 0, 3, 0, 48, 88, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "Sinnoh|National"),
                        S("Check", 82, 84, 83, 1, 0, 3, 0, 48, 152, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Scroll up", 82, 84, 83, 3, 0, 3, 0, 124, 64, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Scroll down", 82, 84, 83, 4, 0, 3, 0, 124, 146, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Quit", 82, 84, 83, 5, 0, 3, 0, 124, 8, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 7, 1, 48, 34, (ScreenTextAlign)1, 0, 3, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, "Sinnoh|National"),
                        T((ScreenTextKind)0, 5, 1, 48, 82, (ScreenTextAlign)1, 0, 3, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, "Sinnoh|National"),
                        T((ScreenTextKind)0, 29, 1, 48, 146, (ScreenTextAlign)1, 0, 2, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 106, 1, 190, 0, (ScreenTextAlign)1, 0, 1, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, "Sinnoh|National"),
                        T((ScreenTextKind)0, 107, 1, 190, 0, (ScreenTextAlign)1, 0, 1, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, "Search results"),
                    },
                },
            },
            new ScreenPage
            {
                Id = "search_order", Name = "Search: order", MainOnTop = true,
                Variants = new string[] { "Normal", "None found" },
                Main = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(3, 3, 4, 28, 32, 32, 0, 0, null, new List<ScreenPaste> { P(43, 0, 0, 0, 0, -1, -1, -1, -1, null), P(44, 6, 6, 0, 0, -1, -1, -1, -1, null) }),
                        B(1, 1, 4, -1, 32, 32, 0, 0, null, new List<ScreenPaste> {  }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(0, 0, 0, 16, null),
                        L(5, 0, 0, 1, null),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 90, 1, 128, 8, (ScreenTextAlign)1, 32, 7, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "Normal"),
                        T((ScreenTextKind)0, 93, 1, 128, 8, (ScreenTextAlign)1, 32, 7, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "None found"),
                        T((ScreenTextKind)0, 81, 1, 128, 52, (ScreenTextAlign)1, 0, 7, 4, 3, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 115, 1, 128, 77, (ScreenTextAlign)1, 0, 7, 4, 3, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 53, 1, 128, 102, (ScreenTextAlign)1, 0, 7, 4, 3, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 53, 1, 128, 120, (ScreenTextAlign)1, 0, 7, 4, 3, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                    },
                },
                Sub = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(2, 2, 4, 29, 32, 32, 0, 0, null, new List<ScreenPaste> { P(75, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(0, 0, 0, 16, null),
                    },
                    Sprites = new List<ScreenSprite>
                    {
                        S("Back", 126, 125, 124, 3, 0, 3, 0, 212, 16, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Order", 126, 125, 124, 2, 3, 3, 0, 224, 48, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Name", 126, 125, 124, 2, 0, 3, 0, 224, 80, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Type", 126, 125, 124, 2, 0, 3, 0, 224, 112, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Form", 126, 125, 124, 2, 0, 3, 0, 224, 144, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("OK", 126, 125, 124, 1, 0, 3, 0, 212, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 1", 126, 125, 124, 0, 3, 3, 0, 48, 16, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 2", 126, 125, 124, 0, 0, 3, 0, 128, 16, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 3", 126, 125, 124, 0, 0, 3, 0, 48, 48, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 4", 126, 125, 124, 0, 0, 3, 0, 128, 48, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 5", 126, 125, 124, 0, 0, 3, 0, 48, 80, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 6", 126, 125, 124, 0, 0, 3, 0, 128, 80, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 50, 1, 224, 40, (ScreenTextAlign)1, 0, 3, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 47, 1, 224, 74, (ScreenTextAlign)1, 0, 3, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 48, 1, 224, 106, (ScreenTextAlign)1, 0, 3, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 49, 1, 224, 138, (ScreenTextAlign)1, 0, 3, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 51, 1, 212, 170, (ScreenTextAlign)1, 0, 1, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 81, 1, 48, 8, (ScreenTextAlign)1, 0, 11, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 82, 1, 128, 10, (ScreenTextAlign)1, 0, 11, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 83, 1, 48, 42, (ScreenTextAlign)1, 0, 11, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 84, 1, 128, 42, (ScreenTextAlign)1, 0, 11, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 85, 1, 48, 74, (ScreenTextAlign)1, 0, 11, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 86, 1, 128, 74, (ScreenTextAlign)1, 0, 11, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                    },
                },
            },
            new ScreenPage
            {
                Id = "search_name", Name = "Search: name", MainOnTop = true,
                Variants = new string[] { "Normal", "None found" },
                Main = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(3, 3, 4, 28, 32, 32, 0, 0, null, new List<ScreenPaste> { P(43, 0, 0, 0, 0, -1, -1, -1, -1, null), P(45, 6, 9, 0, 0, -1, -1, -1, -1, null) }),
                        B(1, 1, 4, -1, 32, 32, 0, 0, null, new List<ScreenPaste> {  }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(0, 0, 0, 16, null),
                        L(5, 0, 0, 1, null),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 87, 1, 128, 8, (ScreenTextAlign)1, 32, 7, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "Normal"),
                        T((ScreenTextKind)0, 93, 1, 128, 8, (ScreenTextAlign)1, 32, 7, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "None found"),
                        T((ScreenTextKind)0, 81, 1, 128, 52, (ScreenTextAlign)1, 0, 7, 4, 3, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 115, 1, 128, 77, (ScreenTextAlign)1, 0, 7, 4, 3, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 53, 1, 128, 102, (ScreenTextAlign)1, 0, 7, 4, 3, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 53, 1, 128, 120, (ScreenTextAlign)1, 0, 7, 4, 3, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                    },
                },
                Sub = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(2, 2, 4, 29, 32, 32, 0, 0, null, new List<ScreenPaste> { P(75, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(0, 0, 0, 16, null),
                    },
                    Sprites = new List<ScreenSprite>
                    {
                        S("Back", 126, 125, 124, 3, 0, 3, 0, 212, 16, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Order", 126, 125, 124, 2, 0, 3, 0, 224, 48, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Name", 126, 125, 124, 2, 3, 3, 0, 224, 80, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Type", 126, 125, 124, 2, 0, 3, 0, 224, 112, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Form", 126, 125, 124, 2, 0, 3, 0, 224, 144, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("OK", 126, 125, 124, 1, 0, 3, 0, 212, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 1", 126, 125, 124, 0, 0, 3, 0, 48, 16, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 2", 126, 125, 124, 0, 0, 3, 0, 128, 16, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 3", 126, 125, 124, 0, 0, 3, 0, 48, 48, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 4", 126, 125, 124, 0, 0, 3, 0, 128, 48, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 5", 126, 125, 124, 0, 0, 3, 0, 48, 80, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 6", 126, 125, 124, 0, 0, 3, 0, 128, 80, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 7", 126, 125, 124, 0, 0, 3, 0, 48, 112, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 8", 126, 125, 124, 0, 0, 3, 0, 128, 112, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 9", 126, 125, 124, 0, 0, 3, 0, 48, 144, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 10", 126, 125, 124, 0, 3, 3, 0, 128, 144, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 50, 1, 224, 42, (ScreenTextAlign)1, 0, 3, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 47, 1, 224, 72, (ScreenTextAlign)1, 0, 3, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 48, 1, 224, 106, (ScreenTextAlign)1, 0, 3, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 49, 1, 224, 138, (ScreenTextAlign)1, 0, 3, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 51, 1, 212, 170, (ScreenTextAlign)1, 0, 1, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 54, 1, 48, 10, (ScreenTextAlign)1, 0, 11, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 55, 1, 128, 10, (ScreenTextAlign)1, 0, 11, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 56, 1, 48, 42, (ScreenTextAlign)1, 0, 11, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 57, 1, 128, 42, (ScreenTextAlign)1, 0, 11, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 58, 1, 48, 74, (ScreenTextAlign)1, 0, 11, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 59, 1, 128, 74, (ScreenTextAlign)1, 0, 11, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 60, 1, 48, 106, (ScreenTextAlign)1, 0, 11, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 61, 1, 128, 106, (ScreenTextAlign)1, 0, 11, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 62, 1, 48, 138, (ScreenTextAlign)1, 0, 11, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 115, 1, 128, 136, (ScreenTextAlign)1, 0, 11, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                    },
                },
            },
            new ScreenPage
            {
                Id = "search_type", Name = "Search: type", MainOnTop = true,
                Variants = new string[] { "Normal", "None found" },
                Main = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(3, 3, 4, 28, 32, 32, 0, 0, null, new List<ScreenPaste> { P(43, 0, 0, 0, 0, -1, -1, -1, -1, null), P(46, 6, 12, 0, 0, -1, -1, -1, -1, null) }),
                        B(1, 1, 4, -1, 32, 32, 0, 0, null, new List<ScreenPaste> {  }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(0, 0, 0, 16, null),
                        L(5, 0, 0, 1, null),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 88, 1, 128, 8, (ScreenTextAlign)1, 32, 7, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "Normal"),
                        T((ScreenTextKind)0, 93, 1, 128, 8, (ScreenTextAlign)1, 32, 7, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "None found"),
                        T((ScreenTextKind)0, 81, 1, 128, 52, (ScreenTextAlign)1, 0, 7, 4, 3, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 115, 1, 128, 77, (ScreenTextAlign)1, 0, 7, 4, 3, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 53, 1, 128, 102, (ScreenTextAlign)1, 0, 7, 4, 3, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 53, 1, 128, 120, (ScreenTextAlign)1, 0, 7, 4, 3, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                    },
                },
                Sub = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(2, 2, 4, 29, 32, 32, 0, 0, null, new List<ScreenPaste> { P(75, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(0, 0, 0, 16, null),
                    },
                    Sprites = new List<ScreenSprite>
                    {
                        S("Back", 126, 125, 124, 3, 0, 3, 0, 212, 16, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Order", 126, 125, 124, 2, 0, 3, 0, 224, 48, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Name", 126, 125, 124, 2, 0, 3, 0, 224, 80, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Type", 126, 125, 124, 2, 3, 3, 0, 224, 112, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Form", 126, 125, 124, 2, 0, 3, 0, 224, 144, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("OK", 126, 125, 124, 1, 0, 3, 0, 212, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 1", 126, 125, 124, 0, 0, 3, 0, 48, 16, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 2", 126, 125, 124, 0, 0, 3, 0, 128, 16, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 3", 126, 125, 124, 0, 0, 3, 0, 48, 48, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 4", 126, 125, 124, 0, 0, 3, 0, 128, 48, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 5", 126, 125, 124, 0, 0, 3, 0, 48, 80, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 6", 126, 125, 124, 0, 0, 3, 0, 128, 80, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 7", 126, 125, 124, 0, 0, 3, 0, 48, 112, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 8", 126, 125, 124, 0, 0, 3, 0, 128, 112, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 9", 126, 125, 124, 0, 0, 3, 0, 48, 144, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 10", 126, 125, 124, 0, 3, 3, 0, 128, 144, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Next types", 126, 125, 124, 5, 0, 3, 0, 24, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 50, 1, 224, 42, (ScreenTextAlign)1, 0, 3, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 47, 1, 224, 74, (ScreenTextAlign)1, 0, 3, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 48, 1, 224, 104, (ScreenTextAlign)1, 0, 3, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 49, 1, 224, 138, (ScreenTextAlign)1, 0, 3, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 51, 1, 212, 170, (ScreenTextAlign)1, 0, 1, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 64, 1, 48, 10, (ScreenTextAlign)1, 0, 11, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 70, 1, 128, 10, (ScreenTextAlign)1, 0, 11, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 73, 1, 48, 42, (ScreenTextAlign)1, 0, 11, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 71, 1, 128, 42, (ScreenTextAlign)1, 0, 11, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 72, 1, 48, 74, (ScreenTextAlign)1, 0, 11, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 76, 1, 128, 74, (ScreenTextAlign)1, 0, 11, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 75, 1, 48, 106, (ScreenTextAlign)1, 0, 11, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 77, 1, 128, 106, (ScreenTextAlign)1, 0, 11, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 80, 1, 48, 138, (ScreenTextAlign)1, 0, 11, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 116, 1, 128, 136, (ScreenTextAlign)1, 0, 11, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                    },
                },
            },
            new ScreenPage
            {
                Id = "search_type_2", Name = "Search: type (second page)", MainOnTop = true,
                Variants = new string[] { "Normal", "None found" },
                Main = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(3, 3, 4, 28, 32, 32, 0, 0, null, new List<ScreenPaste> { P(43, 0, 0, 0, 0, -1, -1, -1, -1, null), P(46, 6, 12, 0, 0, -1, -1, -1, -1, null) }),
                        B(1, 1, 4, -1, 32, 32, 0, 0, null, new List<ScreenPaste> {  }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(0, 0, 0, 16, null),
                        L(5, 0, 0, 1, null),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 88, 1, 128, 8, (ScreenTextAlign)1, 32, 7, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "Normal"),
                        T((ScreenTextKind)0, 93, 1, 128, 8, (ScreenTextAlign)1, 32, 7, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "None found"),
                        T((ScreenTextKind)0, 81, 1, 128, 52, (ScreenTextAlign)1, 0, 7, 4, 3, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 115, 1, 128, 77, (ScreenTextAlign)1, 0, 7, 4, 3, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 53, 1, 128, 102, (ScreenTextAlign)1, 0, 7, 4, 3, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 53, 1, 128, 120, (ScreenTextAlign)1, 0, 7, 4, 3, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                    },
                },
                Sub = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(2, 2, 4, 29, 32, 32, 0, 0, null, new List<ScreenPaste> { P(75, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(0, 0, 0, 16, null),
                    },
                    Sprites = new List<ScreenSprite>
                    {
                        S("Back", 126, 125, 124, 3, 0, 3, 0, 212, 16, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Order", 126, 125, 124, 2, 0, 3, 0, 224, 48, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Name", 126, 125, 124, 2, 0, 3, 0, 224, 80, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Type", 126, 125, 124, 2, 3, 3, 0, 224, 112, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Form", 126, 125, 124, 2, 0, 3, 0, 224, 144, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("OK", 126, 125, 124, 1, 0, 3, 0, 212, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 1", 126, 125, 124, 0, 0, 3, 0, 48, 16, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 2", 126, 125, 124, 0, 0, 3, 0, 128, 16, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 3", 126, 125, 124, 0, 0, 3, 0, 48, 48, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 4", 126, 125, 124, 0, 0, 3, 0, 128, 48, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 5", 126, 125, 124, 0, 0, 3, 0, 48, 80, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 6", 126, 125, 124, 0, 0, 3, 0, 128, 80, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 7", 126, 125, 124, 0, 0, 3, 0, 48, 112, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 8", 126, 125, 124, 0, 0, 3, 0, 128, 112, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 9", 126, 125, 124, 0, 3, 3, 0, 128, 144, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Previous types", 126, 125, 124, 4, 0, 3, 0, 24, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 50, 1, 224, 42, (ScreenTextAlign)1, 0, 3, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 47, 1, 224, 74, (ScreenTextAlign)1, 0, 3, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 48, 1, 224, 104, (ScreenTextAlign)1, 0, 3, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 49, 1, 224, 138, (ScreenTextAlign)1, 0, 3, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 51, 1, 212, 170, (ScreenTextAlign)1, 0, 1, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 65, 1, 48, 10, (ScreenTextAlign)1, 0, 11, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 66, 1, 128, 10, (ScreenTextAlign)1, 0, 11, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 68, 1, 48, 42, (ScreenTextAlign)1, 0, 11, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 67, 1, 128, 42, (ScreenTextAlign)1, 0, 11, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 74, 1, 48, 74, (ScreenTextAlign)1, 0, 11, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 69, 1, 128, 74, (ScreenTextAlign)1, 0, 11, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 78, 1, 48, 106, (ScreenTextAlign)1, 0, 11, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 79, 1, 128, 106, (ScreenTextAlign)1, 0, 11, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 116, 1, 128, 136, (ScreenTextAlign)1, 0, 11, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                    },
                },
            },
            new ScreenPage
            {
                Id = "search_form", Name = "Search: form", MainOnTop = true,
                Variants = new string[] { "Normal", "None found" },
                Main = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(3, 3, 4, 28, 32, 32, 0, 0, null, new List<ScreenPaste> { P(43, 0, 0, 0, 0, -1, -1, -1, -1, null), P(47, 6, 17, 0, 0, -1, -1, -1, -1, null) }),
                        B(1, 1, 4, -1, 32, 32, 0, 0, null, new List<ScreenPaste> {  }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(0, 0, 0, 16, null),
                        L(5, 0, 0, 1, null),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 89, 1, 128, 8, (ScreenTextAlign)1, 32, 7, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "Normal"),
                        T((ScreenTextKind)0, 93, 1, 128, 8, (ScreenTextAlign)1, 32, 7, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "None found"),
                        T((ScreenTextKind)0, 81, 1, 128, 52, (ScreenTextAlign)1, 0, 7, 4, 3, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 115, 1, 128, 77, (ScreenTextAlign)1, 0, 7, 4, 3, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 53, 1, 128, 102, (ScreenTextAlign)1, 0, 7, 4, 3, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 53, 1, 128, 120, (ScreenTextAlign)1, 0, 7, 4, 3, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                    },
                },
                Sub = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(2, 2, 4, 29, 32, 32, 0, 0, null, new List<ScreenPaste> { P(75, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(0, 0, 0, 16, null),
                    },
                    Sprites = new List<ScreenSprite>
                    {
                        S("Back", 126, 125, 124, 3, 0, 3, 0, 212, 16, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Order", 126, 125, 124, 2, 0, 3, 0, 224, 48, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Name", 126, 125, 124, 2, 0, 3, 0, 224, 80, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Type", 126, 125, 124, 2, 0, 3, 0, 224, 112, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Form", 126, 125, 124, 2, 3, 3, 0, 224, 144, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("OK", 126, 125, 124, 1, 0, 3, 0, 212, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 1 icon", 129, 128, 127, 0, 0, 3, 0, 28, 16, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 1", 126, 125, 124, 6, 0, 3, 0, 28, 16, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 2 icon", 129, 128, 127, 5, 0, 3, 0, 84, 16, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 2", 126, 125, 124, 6, 0, 3, 0, 84, 16, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 3 icon", 129, 128, 127, 10, 0, 3, 0, 140, 16, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 3", 126, 125, 124, 6, 0, 3, 0, 140, 16, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 4 icon", 129, 128, 127, 1, 0, 3, 0, 28, 48, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 4", 126, 125, 124, 6, 0, 3, 0, 28, 48, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 5 icon", 129, 128, 127, 6, 0, 3, 0, 84, 48, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 5", 126, 125, 124, 6, 0, 3, 0, 84, 48, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 6 icon", 129, 128, 127, 11, 0, 3, 0, 140, 48, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 6", 126, 125, 124, 6, 0, 3, 0, 140, 48, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 7 icon", 129, 128, 127, 2, 0, 3, 0, 28, 80, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 7", 126, 125, 124, 6, 0, 3, 0, 28, 80, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 8 icon", 129, 128, 127, 9, 0, 3, 0, 84, 80, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 8", 126, 125, 124, 6, 0, 3, 0, 84, 80, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 9 icon", 129, 128, 127, 12, 0, 3, 0, 140, 80, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 9", 126, 125, 124, 6, 0, 3, 0, 140, 80, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 10 icon", 129, 128, 127, 3, 0, 3, 0, 28, 112, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 10", 126, 125, 124, 6, 0, 3, 0, 28, 112, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 11 icon", 129, 128, 127, 8, 0, 3, 0, 84, 112, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 11", 126, 125, 124, 6, 0, 3, 0, 84, 112, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 12 icon", 129, 128, 127, 13, 0, 3, 0, 140, 112, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 12", 126, 125, 124, 6, 0, 3, 0, 140, 112, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 13 icon", 129, 128, 127, 4, 0, 3, 0, 28, 144, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 13", 126, 125, 124, 6, 0, 3, 0, 28, 144, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 14 icon", 129, 128, 127, 7, 0, 3, 0, 84, 144, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 14", 126, 125, 124, 6, 0, 3, 0, 84, 144, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 15", 126, 125, 124, 6, 3, 3, 0, 140, 144, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 50, 1, 224, 42, (ScreenTextAlign)1, 0, 3, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 47, 1, 224, 74, (ScreenTextAlign)1, 0, 3, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 48, 1, 224, 106, (ScreenTextAlign)1, 0, 3, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 49, 1, 224, 136, (ScreenTextAlign)1, 0, 3, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 51, 1, 212, 170, (ScreenTextAlign)1, 0, 1, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                    },
                },
            },
            new ScreenPage
            {
                Id = "search_running", Name = "Searching", MainOnTop = true,
                Variants = new string[] { "Sinnoh", "National" },
                Main = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(2, 1, 4, 28, 32, 32, 0, 0, null, new List<ScreenPaste> { P(40, 0, 0, 0, 20, -1, 12, -1, -1, "Sinnoh"), P(40, 0, 12, 0, 0, -1, 12, -1, -1, "Sinnoh"), P(42, 0, 0, 0, 20, -1, 12, -1, -1, "National"), P(42, 0, 12, 0, 0, -1, 12, -1, -1, "National") }),
                        B(3, 3, 4, 28, 32, 32, 0, 0, null, new List<ScreenPaste> { P(43, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                        B(1, 0, 4, -1, 32, 32, 0, 0, null, new List<ScreenPaste> {  }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(0, 0, 0, 16, null),
                        L(5, 0, 0, 1, null),
                    },
                    Sprites = new List<ScreenSprite>
                    {
                        S("Spinning ball", 79, 81, 80, 17, 0, 3, 0, 128, 96, 2, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 94, 1, 128, 128, (ScreenTextAlign)1, 0, 7, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                    },
                },
                Sub = new ScreenLayout
                {
                    Darken = 16,
                    Bgs = new List<ScreenBg>
                    {
                        B(2, 2, 4, 29, 32, 32, 0, 0, null, new List<ScreenPaste> { P(75, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(0, 0, 0, 16, null),
                    },
                },
            },
            new ScreenPage
            {
                Id = "info", Name = "Info", MainOnTop = true,
                Variants = new string[] { "Sinnoh", "National" },
                Main = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(2, 1, 4, 33, 32, 32, 0, 0, null, new List<ScreenPaste> { P(57, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                        B(1, 0, 4, -1, 32, 32, 0, 0, null, new List<ScreenPaste> {  }),
                        B(3, 3, 4, 33, 32, 32, 0, 0, null, new List<ScreenPaste> { P(50, 0, 0, 0, 0, -1, -1, -1, -1, null), P(51, 0, 3, 0, 0, -1, -1, -1, -1, null), P(52, 12, 8, 0, 0, -1, -1, -1, -1, null), P(54, 0, 16, 0, 0, -1, -1, -1, -1, null) }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(6, 0, 0, 16, null),
                        L(24, 0, 0, 1, "National"),
                    },
                    Sprites = new List<ScreenSprite>
                    {
                        S("Name tag", 76, 78, 77, 0, 0, 3, 0, 172, 32, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Caught mark", 76, 78, 77, 1, 0, 3, 0, 118, 32, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Category box", 88, 90, 89, 17, 0, 13, 0, 192, 52, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("First type", 88, 90, 89, 0, 0, 13, 0, 170, 72, 0, 1, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Second type", 88, 90, 89, 0, 0, 13, 0, 220, 72, 0, 2, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 101, 1, 16, 0, (ScreenTextAlign)0, 0, 0, 3, 2, 2, 0, 0, 12, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)2, -1, 1, 130, 24, (ScreenTextAlign)0, 0, 0, 3, 2, 2, 0, 0, 3, 0, 0, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)1, -1, 1, 157, 24, (ScreenTextAlign)0, 0, 0, 3, 2, 2, 0, 0, 3, 0, 0, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)3, -1, 1, 182, 44, (ScreenTextAlign)1, 0, 0, 3, 2, 2, 0, 0, 3, 0, 2, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 9, 1, 152, 88, (ScreenTextAlign)0, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 10, 1, 152, 104, (ScreenTextAlign)0, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)4, -1, 1, 184, 88, (ScreenTextAlign)0, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)5, -1, 1, 184, 104, (ScreenTextAlign)0, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)6, -1, 1, 128, 136, (ScreenTextAlign)3, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                    },
                    Mons = new List<ScreenMon>
                    {
                        M((ScreenMonKind)0, 48, 72, 2, 0, 0, (DexSizeRole)0, null),
                        M((ScreenMonKind)1, 120, 88, 0, 0, 0, (DexSizeRole)0, null),
                    },
                },
                Sub = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(1, 0, 4, 34, 32, 32, 0, 0, null, new List<ScreenPaste> { P(59, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                        B(2, 2, 4, 34, 32, 32, 0, 0, null, new List<ScreenPaste> { P(62, 0, 0, 0, 0, -1, -1, -1, -1, null), P(60, 6, 14, 0, 0, -1, -1, -1, -1, null), P(63, 6, 7, 0, 0, -1, -1, -1, -1, null) }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(6, 0, 0, 16, null),
                        L(24, 0, 0, 1, "National"),
                        L(9, 4, 0, 1, null),
                        L(9, 5, 0, 1, null),
                    },
                    Sprites = new List<ScreenSprite>
                    {
                        S("Info", 94, 96, 95, 0, 3, 11, 0, 28, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Area", 94, 96, 95, 1, 0, 11, 0, 68, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Cry", 94, 96, 95, 2, 0, 11, 0, 108, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Size", 94, 96, 95, 3, 0, 11, 0, 148, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Forms", 94, 96, 95, 4, 0, 11, 0, 188, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Back", 94, 96, 95, 5, 0, 11, 0, 228, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Next", 100, 102, 101, 0, 0, 11, 0, 128, 132, 2, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Back", 100, 102, 101, 0, 0, 11, 0, 128, 76, 2, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Next arrow", 97, 99, 98, 13, 0, 11, 0, 160, 134, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Back arrow", 97, 99, 98, 12, 0, 11, 0, 96, 78, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 33, 1, 96, 126, (ScreenTextAlign)0, 0, 4, 3, 2, 2, 0, 0, 11, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 34, 1, 160, 70, (ScreenTextAlign)2, 0, 4, 3, 2, 2, 0, 0, 11, 0, -1, 0, 0, 0, 0, false, null, null),
                    },
                },
            },
            new ScreenPage
            {
                Id = "info_foreign", Name = "Info in another language", MainOnTop = true,
                Variants = new string[] { "French", "German", "Italian", "Spanish", "Japanese" },
                Main = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(2, 1, 4, 33, 32, 32, 0, 0, null, new List<ScreenPaste> { P(57, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                        B(1, 0, 4, -1, 32, 32, 0, 0, null, new List<ScreenPaste> {  }),
                        B(3, 3, 4, 33, 32, 32, 0, 0, null, new List<ScreenPaste> { P(50, 0, 0, 0, 0, -1, -1, -1, -1, null), P(51, 0, 3, 0, 0, -1, -1, -1, -1, null), P(55, 0, 16, 0, 0, -1, -1, -1, -1, null), P(56, 12, 8, 0, 0, -1, -1, -1, -1, null) }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(6, 0, 0, 16, null),
                        L(24, 0, 0, 1, "National"),
                    },
                    Sprites = new List<ScreenSprite>
                    {
                        S("Name tag", 76, 78, 77, 0, 0, 3, 0, 172, 32, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Caught mark", 76, 78, 77, 1, 0, 3, 0, 118, 32, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Category box", 88, 90, 89, 17, 0, 13, 0, 192, 52, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 101, 1, 16, 0, (ScreenTextAlign)0, 0, 0, 3, 2, 2, 0, 0, 12, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)2, -1, 1, 130, 24, (ScreenTextAlign)0, 0, 0, 3, 2, 2, 0, 0, 3, 0, 0, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)1, -1, 1, 157, 24, (ScreenTextAlign)0, 0, 0, 3, 2, 2, 0, 0, 3, 0, 0, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)3, -1, 1, 182, 44, (ScreenTextAlign)1, 0, 0, 3, 2, 2, 0, 0, 3, 0, 2, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 23, 1, 184, 72, (ScreenTextAlign)1, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "French"),
                        T((ScreenTextKind)9, 0, 1, 128, 96, (ScreenTextAlign)0, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "French"),
                        T((ScreenTextKind)10, 0, 1, 242, 112, (ScreenTextAlign)2, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "French"),
                        T((ScreenTextKind)8, 0, 1, 128, 136, (ScreenTextAlign)3, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "French"),
                        T((ScreenTextKind)0, 24, 1, 184, 72, (ScreenTextAlign)1, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "German"),
                        T((ScreenTextKind)9, 1, 1, 128, 96, (ScreenTextAlign)0, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "German"),
                        T((ScreenTextKind)10, 1, 1, 242, 112, (ScreenTextAlign)2, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "German"),
                        T((ScreenTextKind)8, 1, 1, 128, 136, (ScreenTextAlign)3, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "German"),
                        T((ScreenTextKind)0, 25, 1, 184, 72, (ScreenTextAlign)1, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "Italian"),
                        T((ScreenTextKind)9, 2, 1, 128, 96, (ScreenTextAlign)0, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "Italian"),
                        T((ScreenTextKind)10, 2, 1, 242, 112, (ScreenTextAlign)2, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "Italian"),
                        T((ScreenTextKind)8, 2, 1, 128, 136, (ScreenTextAlign)3, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "Italian"),
                        T((ScreenTextKind)0, 26, 1, 184, 72, (ScreenTextAlign)1, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "Spanish"),
                        T((ScreenTextKind)9, 3, 1, 128, 96, (ScreenTextAlign)0, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "Spanish"),
                        T((ScreenTextKind)10, 3, 1, 242, 112, (ScreenTextAlign)2, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "Spanish"),
                        T((ScreenTextKind)8, 3, 1, 128, 136, (ScreenTextAlign)3, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "Spanish"),
                        T((ScreenTextKind)0, 114, 1, 184, 72, (ScreenTextAlign)1, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "Japanese"),
                        T((ScreenTextKind)9, 4, 1, 128, 96, (ScreenTextAlign)0, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "Japanese"),
                        T((ScreenTextKind)10, 4, 1, 242, 112, (ScreenTextAlign)2, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "Japanese"),
                        T((ScreenTextKind)8, 4, 1, 128, 136, (ScreenTextAlign)3, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "Japanese"),
                    },
                    Mons = new List<ScreenMon>
                    {
                        M((ScreenMonKind)0, 48, 72, 2, 0, 0, (DexSizeRole)0, null),
                    },
                },
                Sub = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(1, 0, 4, 34, 32, 32, 0, 0, null, new List<ScreenPaste> { P(59, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                        B(2, 2, 4, 34, 32, 32, 0, 0, null, new List<ScreenPaste> { P(62, 0, 0, 0, 0, -1, -1, -1, -1, null), P(60, 6, 14, 0, 0, -1, -1, -1, -1, null), P(63, 6, 7, 0, 0, -1, -1, -1, -1, null) }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(6, 0, 0, 16, null),
                        L(24, 0, 0, 1, "National"),
                        L(9, 4, 0, 1, null),
                        L(9, 5, 0, 1, null),
                    },
                    Sprites = new List<ScreenSprite>
                    {
                        S("Info", 94, 96, 95, 0, 3, 11, 0, 28, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Area", 94, 96, 95, 1, 0, 11, 0, 68, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Cry", 94, 96, 95, 2, 0, 11, 0, 108, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Size", 94, 96, 95, 3, 0, 11, 0, 148, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Forms", 94, 96, 95, 4, 0, 11, 0, 188, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Back", 94, 96, 95, 5, 0, 11, 0, 228, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Next", 100, 102, 101, 0, 0, 11, 0, 128, 132, 2, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Back", 100, 102, 101, 0, 0, 11, 0, 128, 76, 2, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Next arrow", 97, 99, 98, 13, 0, 11, 0, 160, 134, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Back arrow", 97, 99, 98, 12, 0, 11, 0, 96, 78, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Language", 97, 99, 98, 2, 0, 11, 0, 28, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "French"),
                        S("Language", 97, 99, 98, 2, 0, 11, 0, 28, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "German"),
                        S("Language", 97, 99, 98, 2, 0, 11, 0, 28, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "Italian"),
                        S("Language", 97, 99, 98, 2, 0, 11, 0, 28, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "Spanish"),
                        S("Language", 97, 99, 98, 2, 0, 11, 0, 28, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "Japanese"),
                        S("Language", 97, 99, 98, 5, 0, 11, 0, 68, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "French"),
                        S("Language", 97, 99, 98, 4, 0, 11, 0, 68, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "German"),
                        S("Language", 97, 99, 98, 4, 0, 11, 0, 68, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "Italian"),
                        S("Language", 97, 99, 98, 4, 0, 11, 0, 68, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "Spanish"),
                        S("Language", 97, 99, 98, 4, 0, 11, 0, 68, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "Japanese"),
                        S("Language", 97, 99, 98, 6, 0, 11, 0, 108, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "French"),
                        S("Language", 97, 99, 98, 7, 0, 11, 0, 108, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "German"),
                        S("Language", 97, 99, 98, 6, 0, 11, 0, 108, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "Italian"),
                        S("Language", 97, 99, 98, 6, 0, 11, 0, 108, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "Spanish"),
                        S("Language", 97, 99, 98, 6, 0, 11, 0, 108, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "Japanese"),
                        S("Language", 97, 99, 98, 8, 0, 11, 0, 148, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "French"),
                        S("Language", 97, 99, 98, 8, 0, 11, 0, 148, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "German"),
                        S("Language", 97, 99, 98, 9, 0, 11, 0, 148, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "Italian"),
                        S("Language", 97, 99, 98, 8, 0, 11, 0, 148, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "Spanish"),
                        S("Language", 97, 99, 98, 8, 0, 11, 0, 148, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "Japanese"),
                        S("Language", 97, 99, 98, 10, 0, 11, 0, 188, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "French"),
                        S("Language", 97, 99, 98, 10, 0, 11, 0, 188, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "German"),
                        S("Language", 97, 99, 98, 10, 0, 11, 0, 188, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "Italian"),
                        S("Language", 97, 99, 98, 11, 0, 11, 0, 188, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "Spanish"),
                        S("Language", 97, 99, 98, 10, 0, 11, 0, 188, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "Japanese"),
                        S("Language", 97, 99, 98, 0, 0, 11, 0, 228, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "French"),
                        S("Language", 97, 99, 98, 0, 0, 11, 0, 228, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "German"),
                        S("Language", 97, 99, 98, 0, 0, 11, 0, 228, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "Italian"),
                        S("Language", 97, 99, 98, 0, 0, 11, 0, 228, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "Spanish"),
                        S("Language", 97, 99, 98, 1, 0, 11, 0, 228, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "Japanese"),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 33, 1, 96, 126, (ScreenTextAlign)0, 0, 4, 3, 2, 2, 0, 0, 11, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 34, 1, 160, 70, (ScreenTextAlign)2, 0, 4, 3, 2, 2, 0, 0, 11, 0, -1, 0, 0, 0, 0, false, null, null),
                    },
                },
            },
            new ScreenPage
            {
                Id = "area", Name = "Area", MainOnTop = true,
                Variants = new string[] { "Morning", "Day", "Night", "Area unknown" },
                Main = new ScreenLayout
                {
                    Darken = 0,
                    Habitat = new DexHabitat(),
                    Bgs = new List<ScreenBg>
                    {
                        B(2, 1, 4, 33, 32, 32, 0, 0, null, new List<ScreenPaste> { P(57, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                        B(1, 0, 4, -1, 32, 32, 0, 0, null, new List<ScreenPaste> {  }),
                        B(3, 3, 4, 33, 32, 32, 0, 0, null, new List<ScreenPaste> { P(64, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(6, 0, 0, 16, null),
                        L(24, 0, 0, 1, "National"),
                    },
                    Sprites = new List<ScreenSprite>
                    {
                        S("Area unknown", 91, 93, 92, 2, 0, 14, 0, 160, 96, 1, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "Area unknown"),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 102, 1, 16, 0, (ScreenTextAlign)0, 0, 0, 3, 2, 2, 0, 0, 12, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 36, 1, 44, 32, (ScreenTextAlign)1, 0, 2, 3, 2, 2, 0, 0, 14, 0, -1, 0, 0, 0, 0, false, null, "Morning"),
                        T((ScreenTextKind)0, 37, 1, 44, 32, (ScreenTextAlign)1, 0, 2, 3, 2, 2, 0, 0, 14, 0, -1, 0, 0, 0, 0, false, null, "Day"),
                        T((ScreenTextKind)0, 38, 1, 44, 32, (ScreenTextAlign)1, 0, 2, 3, 2, 2, 0, 0, 14, 0, -1, 0, 0, 0, 0, false, null, "Night"),
                        T((ScreenTextKind)0, 35, 1, 160, 88, (ScreenTextAlign)1, 0, 2, 3, 2, 2, 0, 0, 14, 0, -1, 0, 0, 0, 0, false, null, "Area unknown"),
                        T((ScreenTextKind)0, 37, 1, 44, 32, (ScreenTextAlign)1, 0, 2, 3, 2, 2, 0, 0, 14, 0, -1, 0, 0, 0, 0, false, null, "Area unknown"),
                    },
                    Mons = new List<ScreenMon>
                    {
                        M((ScreenMonKind)0, 40, 120, 2, 0, 0, (DexSizeRole)0, null),
                    },
                },
                Sub = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(1, 0, 4, 34, 32, 32, 0, 0, null, new List<ScreenPaste> { P(59, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                        B(2, 2, 4, 34, 32, 32, 0, 0, null, new List<ScreenPaste> { P(69, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(6, 0, 0, 16, null),
                        L(24, 0, 0, 1, "National"),
                        L(15, 2, 0, 1, "Morning"),
                        L(16, 2, 0, 1, "Day"),
                        L(17, 2, 0, 1, "Night"),
                        L(16, 2, 0, 1, "Area unknown"),
                    },
                    Sprites = new List<ScreenSprite>
                    {
                        S("Info", 94, 96, 95, 0, 0, 11, 0, 28, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Area", 94, 96, 95, 1, 3, 11, 0, 68, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Cry", 94, 96, 95, 2, 0, 11, 0, 108, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Size", 94, 96, 95, 3, 0, 11, 0, 148, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Forms", 94, 96, 95, 4, 0, 11, 0, 188, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Back", 94, 96, 95, 5, 0, 11, 0, 228, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Time of day", 103, 105, 104, 0, 0, 11, 0, 32, 128, 1, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "Morning"),
                        S("Time of day", 103, 105, 104, 1, 0, 11, 0, 128, 96, 1, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "Day"),
                        S("Time of day", 103, 105, 104, 2, 0, 11, 0, 224, 128, 1, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "Night"),
                        S("Time of day", 103, 105, 104, 1, 0, 11, 0, 128, 96, 1, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "Area unknown"),
                    },
                },
            },
            new ScreenPage
            {
                Id = "cry", Name = "Cry", MainOnTop = true,
                Variants = new string[] { "Sinnoh", "National" },
                Main = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(2, 1, 4, 33, 32, 32, 0, 0, null, new List<ScreenPaste> { P(57, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                        B(1, 0, 4, -1, 32, 32, 0, 0, null, new List<ScreenPaste> {  }),
                        B(3, 3, 4, 33, 32, 32, 0, 0, null, new List<ScreenPaste> { P(70, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(6, 0, 0, 16, null),
                        L(24, 0, 0, 1, "National"),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 103, 1, 16, 0, (ScreenTextAlign)0, 0, 0, 3, 2, 2, 0, 0, 12, 0, -1, 0, 0, 0, 0, false, null, null),
                    },
                    Mons = new List<ScreenMon>
                    {
                        M((ScreenMonKind)0, 48, 64, 2, 0, 0, (DexSizeRole)0, null),
                    },
                    Rects = new List<ScreenRect>
                    {
                        R(1, 0, 151, 256, 1, 9, 6, null),
                    },
                },
                Sub = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(1, 0, 4, 34, 32, 32, 0, 0, null, new List<ScreenPaste> { P(59, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                        B(2, 2, 4, 34, 32, 32, 0, 0, null, new List<ScreenPaste> { P(71, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                        B(3, 3, 8, 35, 32, 32, -48, -16, null, new List<ScreenPaste> { P(72, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(6, 0, 0, 16, null),
                        L(24, 0, 0, 1, "National"),
                        L(20, 7, 1, 1, null),
                    },
                    Sprites = new List<ScreenSprite>
                    {
                        S("Info", 94, 96, 95, 0, 0, 11, 0, 28, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Area", 94, 96, 95, 1, 0, 11, 0, 68, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Cry", 94, 96, 95, 2, 3, 11, 0, 108, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Size", 94, 96, 95, 3, 0, 11, 0, 148, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Forms", 94, 96, 95, 4, 0, 11, 0, 188, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Back", 94, 96, 95, 5, 0, 11, 0, 228, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Chorus and pan switch", 114, 113, 112, 4, 8, 18, 0, 64, 67, 2, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Dial", 114, 113, 112, 1, 0, 18, 0, 51, 157, 2, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Play", 114, 113, 112, 3, 5, 18, 0, 180, 131, 2, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Loop", 114, 113, 112, 6, 5, 18, 0, 230, 166, 2, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 41, 1, 64, 84, (ScreenTextAlign)1, 0, 0, 3, 2, 2, 0, 0, 18, 0, -1, 0, 0, 0, 0, false, null, null),
                    },
                },
            },
            new ScreenPage
            {
                Id = "size_height", Name = "Size: height", MainOnTop = true,
                Variants = new string[] { "Lucas", "Dawn" },
                Main = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(2, 1, 4, 33, 32, 32, 0, 0, null, new List<ScreenPaste> { P(57, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                        B(1, 0, 4, -1, 32, 32, 0, 0, null, new List<ScreenPaste> {  }),
                        B(3, 3, 4, 33, 32, 32, 0, 0, null, new List<ScreenPaste> { P(74, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(6, 0, 0, 16, null),
                        L(24, 0, 0, 1, "National"),
                    },
                    Sprites = new List<ScreenSprite>
                    {
                        S("Trainer", 91, 93, 92, 5, 0, 14, 0, 168, 88, 1, 0, -1, 0, 0, (DexSizeRole)2, (DexReadout)0, 0, 0, "Lucas"),
                        S("Trainer", 91, 93, 92, 6, 0, 14, 0, 168, 88, 1, 0, -1, 0, 0, (DexSizeRole)2, (DexReadout)0, 0, 0, "Dawn"),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 104, 1, 16, 0, (ScreenTextAlign)0, 0, 0, 3, 2, 2, 0, 0, 12, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 43, 1, 128, 24, (ScreenTextAlign)1, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 9, 1, 32, 168, (ScreenTextAlign)0, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 9, 1, 152, 168, (ScreenTextAlign)0, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)1, -1, 1, 26, 152, (ScreenTextAlign)0, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)4, -1, 1, 110, 168, (ScreenTextAlign)2, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 95, 1, 230, 168, (ScreenTextAlign)2, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "Lucas"),
                        T((ScreenTextKind)0, 96, 1, 230, 168, (ScreenTextAlign)2, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "Dawn"),
                    },
                    Mons = new List<ScreenMon>
                    {
                        M((ScreenMonKind)0, 88, 88, 2, 15, 0, (DexSizeRole)1, null),
                    },
                },
                Sub = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(1, 0, 4, 34, 32, 32, 0, 0, null, new List<ScreenPaste> { P(59, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                        B(2, 2, 4, 34, 32, 32, 0, 0, null, new List<ScreenPaste> { P(61, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(6, 0, 0, 16, null),
                        L(24, 0, 0, 1, "National"),
                        L(9, 4, 0, 1, null),
                        L(9, 5, 0, 1, null),
                    },
                    Sprites = new List<ScreenSprite>
                    {
                        S("Info", 94, 96, 95, 0, 0, 11, 0, 28, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Area", 94, 96, 95, 1, 0, 11, 0, 68, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Cry", 94, 96, 95, 2, 0, 11, 0, 108, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Size", 94, 96, 95, 3, 3, 11, 0, 148, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Forms", 94, 96, 95, 4, 0, 11, 0, 188, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Back", 94, 96, 95, 5, 0, 11, 0, 228, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 45, 1, 68, 112, (ScreenTextAlign)1, 0, 4, 3, 2, 2, 0, 0, 11, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 46, 1, 188, 112, (ScreenTextAlign)1, 0, 4, 3, 2, 2, 0, 0, 11, 0, -1, 0, 0, 0, 0, false, null, null),
                    },
                },
            },
            new ScreenPage
            {
                Id = "size_weight", Name = "Size: weight", MainOnTop = true,
                Variants = new string[] { "Lucas", "Dawn" },
                Main = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(2, 1, 4, 33, 32, 32, 0, 0, null, new List<ScreenPaste> { P(57, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                        B(1, 0, 4, -1, 32, 32, 0, 0, null, new List<ScreenPaste> {  }),
                        B(3, 3, 4, 33, 32, 32, 0, 0, null, new List<ScreenPaste> { P(73, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(6, 0, 0, 16, null),
                        L(24, 0, 0, 1, "National"),
                    },
                    Sprites = new List<ScreenSprite>
                    {
                        S("Trainer", 91, 93, 92, 3, 0, 14, 0, 184, 64, 3, 0, -1, 0, 0, (DexSizeRole)6, (DexReadout)0, 0, 0, "Lucas"),
                        S("Trainer", 91, 93, 92, 4, 0, 14, 0, 184, 64, 3, 0, -1, 0, 0, (DexSizeRole)6, (DexReadout)0, 0, 0, "Dawn"),
                        S("Stand", 91, 93, 92, 1, 0, 14, 0, 128, 106, 1, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Pan", 91, 93, 92, 0, 0, 14, 0, 184, 88, 3, 0, -1, 0, 0, (DexSizeRole)4, (DexReadout)0, 0, 0, null),
                        S("Pan", 91, 93, 92, 0, 0, 14, 0, 72, 88, 3, 0, -1, 0, 0, (DexSizeRole)3, (DexReadout)0, 0, 0, null),
                        S("Beam", -1, 36, -1, 0, 0, 6, 0, 64, 88, 2, 0, -1, 128, 16, (DexSizeRole)7, (DexReadout)0, 0, 0, null),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 104, 1, 16, 0, (ScreenTextAlign)0, 0, 0, 3, 2, 2, 0, 0, 12, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 44, 1, 128, 24, (ScreenTextAlign)1, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 10, 1, 32, 168, (ScreenTextAlign)0, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 10, 1, 152, 168, (ScreenTextAlign)0, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)1, -1, 1, 26, 152, (ScreenTextAlign)0, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)5, -1, 1, 110, 168, (ScreenTextAlign)2, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 97, 1, 230, 168, (ScreenTextAlign)2, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "Lucas"),
                        T((ScreenTextKind)0, 98, 1, 230, 168, (ScreenTextAlign)2, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "Dawn"),
                    },
                    Mons = new List<ScreenMon>
                    {
                        M((ScreenMonKind)3, 72, 64, 3, 0, 0, (DexSizeRole)5, null),
                    },
                },
                Sub = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(1, 0, 4, 34, 32, 32, 0, 0, null, new List<ScreenPaste> { P(59, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                        B(2, 2, 4, 34, 32, 32, 0, 0, null, new List<ScreenPaste> { P(61, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(6, 0, 0, 16, null),
                        L(24, 0, 0, 1, "National"),
                        L(9, 4, 0, 1, null),
                        L(9, 5, 0, 1, null),
                    },
                    Sprites = new List<ScreenSprite>
                    {
                        S("Info", 94, 96, 95, 0, 0, 11, 0, 28, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Area", 94, 96, 95, 1, 0, 11, 0, 68, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Cry", 94, 96, 95, 2, 0, 11, 0, 108, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Size", 94, 96, 95, 3, 3, 11, 0, 148, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Forms", 94, 96, 95, 4, 0, 11, 0, 188, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Back", 94, 96, 95, 5, 0, 11, 0, 228, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 45, 1, 68, 112, (ScreenTextAlign)1, 0, 4, 3, 2, 2, 0, 0, 11, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 46, 1, 188, 112, (ScreenTextAlign)1, 0, 4, 3, 2, 2, 0, 0, 11, 0, -1, 0, 0, 0, 0, false, null, null),
                    },
                },
            },
            new ScreenPage
            {
                Id = "forms", Name = "Forms", MainOnTop = true,
                Variants = new string[] { "Sinnoh", "National" },
                Main = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(2, 1, 4, 33, 32, 32, 0, 0, null, new List<ScreenPaste> { P(57, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                        B(1, 0, 4, -1, 32, 32, 0, 0, null, new List<ScreenPaste> {  }),
                        B(3, 3, 4, 33, 32, 32, 0, 0, null, new List<ScreenPaste> { P(50, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(6, 0, 0, 16, null),
                        L(24, 0, 0, 1, "National"),
                    },
                    Sprites = new List<ScreenSprite>
                    {
                        S("Front box", 123, 122, 121, 0, 0, 22, 0, 72, 96, 3, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Back box", 123, 122, 121, 2, 0, 22, 0, 184, 96, 3, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 105, 1, 16, 0, (ScreenTextAlign)0, 0, 0, 3, 2, 2, 0, 0, 12, 0, -1, 0, 0, 0, 0, false, null, null),
                    },
                    Mons = new List<ScreenMon>
                    {
                        M((ScreenMonKind)0, 72, 88, 2, 0, 0, (DexSizeRole)0, null),
                        M((ScreenMonKind)2, 184, 88, 2, 0, 0, (DexSizeRole)0, null),
                    },
                },
                Sub = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(1, 0, 4, 34, 32, 32, 0, 0, null, new List<ScreenPaste> { P(59, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                        B(2, 2, 4, 34, 32, 32, 0, 0, null, new List<ScreenPaste> { P(58, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(6, 0, 0, 16, null),
                        L(24, 0, 0, 1, "National"),
                        L(9, 4, 0, 1, null),
                    },
                    Sprites = new List<ScreenSprite>
                    {
                        S("Info", 94, 96, 95, 0, 0, 11, 0, 28, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Area", 94, 96, 95, 1, 0, 11, 0, 68, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Cry", 94, 96, 95, 2, 0, 11, 0, 108, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Size", 94, 96, 95, 3, 0, 11, 0, 148, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Forms", 94, 96, 95, 4, 3, 11, 0, 188, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Back", 94, 96, 95, 5, 0, 11, 0, 228, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 100, 1, 128, 136, (ScreenTextAlign)1, 0, 4, 3, 2, 2, 0, 0, 11, 0, -1, 0, 0, 0, 0, false, null, null),
                    },
                },
            },
            new ScreenPage
            {
                Id = "catch_registered", Name = "Registered after a catch", MainOnTop = true,
                Variants = new string[] { "Sinnoh", "National" },
                Main = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(3, 3, 4, 33, 32, 32, 0, 0, null, new List<ScreenPaste> { P(50, 0, 0, 0, 0, -1, -1, -1, -1, null), P(51, 0, 3, 0, 0, -1, -1, -1, -1, null), P(52, 12, 8, 0, 0, -1, -1, -1, -1, null), P(54, 0, 16, 0, 0, -1, -1, -1, -1, null) }),
                        B(2, 2, 4, 33, 32, 32, 0, 0, null, new List<ScreenPaste> { P(57, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                        B(1, 1, 4, -1, 32, 32, 0, 0, null, new List<ScreenPaste> {  }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(6, 0, 0, 16, null),
                        L(23, 0, 0, 1, "Sinnoh"),
                        L(24, 0, 0, 1, "National"),
                    },
                    Sprites = new List<ScreenSprite>
                    {
                        S("Name tag", 76, 78, 77, 0, 0, 3, 0, 172, 32, 2, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Caught mark", 76, 78, 77, 1, 0, 3, 0, 118, 32, 2, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Category box", 88, 90, 89, 17, 0, 13, 0, 192, 52, 2, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("First type", 88, 90, 89, 0, 0, 13, 0, 170, 72, 2, 1, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Second type", 88, 90, 89, 0, 0, 13, 0, 220, 72, 2, 2, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)2, -1, 1, 130, 24, (ScreenTextAlign)0, 0, 0, 3, 2, 2, 0, 0, 3, 2, 0, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)1, -1, 1, 157, 24, (ScreenTextAlign)0, 0, 0, 3, 2, 2, 0, 0, 3, 2, 0, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)3, -1, 1, 182, 44, (ScreenTextAlign)1, 0, 0, 3, 2, 2, 0, 0, 3, 2, 2, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 109, 1, 32, 0, (ScreenTextAlign)0, 0, 12, 3, 4, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 9, 1, 152, 88, (ScreenTextAlign)0, 0, 12, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 10, 1, 152, 104, (ScreenTextAlign)0, 0, 12, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)4, -1, 1, 184, 88, (ScreenTextAlign)0, 0, 12, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)5, -1, 1, 184, 104, (ScreenTextAlign)0, 0, 12, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)6, -1, 1, 128, 136, (ScreenTextAlign)3, 0, 12, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                    },
                    Mons = new List<ScreenMon>
                    {
                        M((ScreenMonKind)0, 48, 72, 0, 0, 0, (DexSizeRole)0, null),
                        M((ScreenMonKind)1, 120, 88, 2, 0, 0, (DexSizeRole)0, null),
                    },
                },
                Sub = new ScreenLayout
                {
                    Darken = 0,
                },
            },
        };

        private static List<ScreenPage> Platinum() => new List<ScreenPage>
        {
            new ScreenPage
            {
                Id = "list", Name = "List", MainOnTop = true,
                Variants = new string[] { "Sinnoh", "National", "Search results" },
                Main = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(3, 3, 4, 28, 32, 32, 0, 0, null, new List<ScreenPaste> { P(38, 0, 0, 0, 0, -1, -1, -1, -1, null), P(39, 1, 4, 0, 0, -1, -1, 1, -1, null) }),
                        B(2, 1, 4, 28, 32, 32, 0, 0, null, new List<ScreenPaste> { P(40, 0, 0, 0, 29, -1, 3, -1, -1, "Sinnoh|Search results"), P(40, 0, 18, 0, 0, -1, 14, -1, -1, "Sinnoh|Search results"), P(42, 0, 0, 0, 29, -1, 3, -1, -1, "National"), P(42, 0, 18, 0, 0, -1, 14, -1, -1, "National") }),
                        B(1, 0, 4, -1, 32, 32, 0, 0, null, new List<ScreenPaste> {  }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(0, 0, 0, 16, null),
                        L(4, 0, 0, 1, "Search results"),
                    },
                    Sprites = new List<ScreenSprite>
                    {
                        S("Caught mark", 79, 81, 80, 1, 0, 3, 0, 116, 82, 2, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("List row 5", 79, 81, 80, 0, 0, 3, 0, 170, 82, 2, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("List row 4", 79, 81, 80, 0, 0, 3, 7, 175, 58, 2, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("List row 6", 79, 81, 80, 0, 0, 3, 7, 175, 106, 2, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("List row 3", 79, 81, 80, 0, 0, 3, 8, 177, 42, 2, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("List row 7", 79, 81, 80, 0, 0, 3, 8, 177, 122, 2, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("List row 2", 79, 81, 80, 0, 0, 3, 9, 181, 26, 2, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("List row 8", 79, 81, 80, 0, 0, 3, 9, 181, 138, 2, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("List row 1", 79, 81, 80, 0, 0, 3, 9, 185, 22, 2, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("List row 9", 79, 81, 80, 0, 0, 3, 9, 185, 142, 2, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Scroll marker", 79, 81, 80, 2, 0, 3, 0, 248, 85, 2, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 0, 1, 8, 152, (ScreenTextAlign)0, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "Sinnoh|National"),
                        T((ScreenTextKind)0, 109, 1, 8, 152, (ScreenTextAlign)0, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "Search results"),
                        T((ScreenTextKind)0, 1, 1, 128, 152, (ScreenTextAlign)0, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "Sinnoh|National"),
                        T((ScreenTextKind)7, -1, 1, 48, 170, (ScreenTextAlign)0, 0, 0, 2, 1, 0, 342, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)7, -1, 1, 180, 170, (ScreenTextAlign)0, 0, 0, 2, 1, 0, 210, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "Sinnoh|National"),
                        T((ScreenTextKind)2, -1, 1, 143, 14, (ScreenTextAlign)0, 0, 9, 3, 2, 2, 0, -4, 3, 2, 8, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)1, -1, 1, 170, 14, (ScreenTextAlign)0, 0, 9, 3, 2, 2, 0, -4, 3, 2, 8, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)2, -1, 1, 139, 18, (ScreenTextAlign)0, 0, 9, 3, 2, 2, 0, -3, 3, 2, 6, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)1, -1, 1, 166, 18, (ScreenTextAlign)0, 0, 9, 3, 2, 2, 0, -3, 3, 2, 6, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)2, -1, 1, 135, 34, (ScreenTextAlign)0, 0, 8, 3, 2, 2, 0, -2, 3, 2, 4, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)1, -1, 1, 162, 34, (ScreenTextAlign)0, 0, 8, 3, 2, 2, 0, -2, 3, 2, 4, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)2, -1, 1, 133, 50, (ScreenTextAlign)0, 0, 7, 3, 2, 2, 0, -1, 3, 2, 2, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)1, -1, 1, 160, 50, (ScreenTextAlign)0, 0, 7, 3, 2, 2, 0, -1, 3, 2, 2, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)2, -1, 1, 128, 74, (ScreenTextAlign)0, 0, 0, 3, 2, 2, 0, 0, 3, 2, 1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)1, -1, 1, 155, 74, (ScreenTextAlign)0, 0, 0, 3, 2, 2, 0, 0, 3, 2, 1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)2, -1, 1, 133, 98, (ScreenTextAlign)0, 0, 7, 3, 2, 2, 0, 1, 3, 2, 3, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)1, -1, 1, 160, 98, (ScreenTextAlign)0, 0, 7, 3, 2, 2, 0, 1, 3, 2, 3, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)2, -1, 1, 135, 114, (ScreenTextAlign)0, 0, 8, 3, 2, 2, 0, 2, 3, 2, 5, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)1, -1, 1, 162, 114, (ScreenTextAlign)0, 0, 8, 3, 2, 2, 0, 2, 3, 2, 5, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)2, -1, 1, 139, 130, (ScreenTextAlign)0, 0, 9, 3, 2, 2, 0, 3, 3, 2, 7, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)1, -1, 1, 166, 130, (ScreenTextAlign)0, 0, 9, 3, 2, 2, 0, 3, 3, 2, 7, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)2, -1, 1, 143, 134, (ScreenTextAlign)0, 0, 9, 3, 2, 2, 0, 4, 3, 2, 9, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)1, -1, 1, 170, 134, (ScreenTextAlign)0, 0, 9, 3, 2, 2, 0, 4, 3, 2, 9, 0, 0, 0, 0, false, null, null),
                    },
                    Mons = new List<ScreenMon>
                    {
                        M((ScreenMonKind)0, 56, 80, 2, 0, 0, (DexSizeRole)0, null),
                    },
                },
                Sub = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(2, 2, 4, 29, 32, 32, 0, 0, null, new List<ScreenPaste> { P(41, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                        B(3, 3, 8, 27, 32, 32, -120, 0, null, new List<ScreenPaste> { P(37, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(0, 0, 0, 16, null),
                        L(25, 3, 0, 1, "National"),
                        L(1, 3, 0, 1, "Search results"),
                    },
                    Sprites = new List<ScreenSprite>
                    {
                        S("Search", 82, 84, 83, 2, 0, 3, 0, 48, 40, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "Sinnoh|National"),
                        S("Switch", 82, 84, 83, 0, 0, 3, 0, 48, 88, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "Sinnoh|National"),
                        S("Check", 82, 84, 83, 1, 0, 3, 0, 48, 152, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Scroll up", 82, 84, 83, 3, 0, 3, 0, 124, 64, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Scroll down", 82, 84, 83, 4, 0, 3, 0, 124, 146, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Quit", 82, 84, 83, 5, 0, 3, 0, 124, 8, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 7, 1, 48, 34, (ScreenTextAlign)1, 0, 3, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, "Sinnoh|National"),
                        T((ScreenTextKind)0, 5, 1, 48, 82, (ScreenTextAlign)1, 0, 3, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, "Sinnoh|National"),
                        T((ScreenTextKind)0, 29, 1, 48, 146, (ScreenTextAlign)1, 0, 2, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 107, 1, 190, 0, (ScreenTextAlign)1, 0, 1, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, "Sinnoh|National"),
                        T((ScreenTextKind)0, 108, 1, 190, 0, (ScreenTextAlign)1, 0, 1, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, "Search results"),
                    },
                },
            },
            new ScreenPage
            {
                Id = "search_order", Name = "Search: order", MainOnTop = true,
                Variants = new string[] { "Normal", "None found" },
                Main = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(3, 3, 4, 28, 32, 32, 0, 0, null, new List<ScreenPaste> { P(43, 0, 0, 0, 0, -1, -1, -1, -1, null), P(44, 6, 6, 0, 0, -1, -1, -1, -1, null) }),
                        B(1, 1, 4, -1, 32, 32, 0, 0, null, new List<ScreenPaste> {  }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(0, 0, 0, 16, null),
                        L(5, 0, 0, 1, null),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 90, 1, 128, 8, (ScreenTextAlign)1, 32, 7, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "Normal"),
                        T((ScreenTextKind)0, 93, 1, 128, 8, (ScreenTextAlign)1, 32, 7, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "None found"),
                        T((ScreenTextKind)0, 81, 1, 128, 52, (ScreenTextAlign)1, 0, 7, 4, 3, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 126, 1, 128, 77, (ScreenTextAlign)1, 0, 7, 4, 3, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 53, 1, 128, 102, (ScreenTextAlign)1, 0, 7, 4, 3, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 53, 1, 128, 120, (ScreenTextAlign)1, 0, 7, 4, 3, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                    },
                },
                Sub = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(2, 2, 4, 29, 32, 32, 0, 0, null, new List<ScreenPaste> { P(75, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(0, 0, 0, 16, null),
                    },
                    Sprites = new List<ScreenSprite>
                    {
                        S("Back", 126, 125, 124, 3, 0, 3, 0, 212, 16, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Order", 126, 125, 124, 2, 3, 3, 0, 224, 48, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Name", 126, 125, 124, 2, 0, 3, 0, 224, 80, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Type", 126, 125, 124, 2, 0, 3, 0, 224, 112, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Form", 126, 125, 124, 2, 0, 3, 0, 224, 144, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("OK", 126, 125, 124, 1, 0, 3, 0, 212, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 1", 126, 125, 124, 0, 3, 3, 0, 48, 16, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 2", 126, 125, 124, 0, 0, 3, 0, 128, 16, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 3", 126, 125, 124, 0, 0, 3, 0, 48, 48, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 4", 126, 125, 124, 0, 0, 3, 0, 128, 48, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 5", 126, 125, 124, 0, 0, 3, 0, 48, 80, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 6", 126, 125, 124, 0, 0, 3, 0, 128, 80, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 50, 1, 224, 40, (ScreenTextAlign)1, 0, 3, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 47, 1, 224, 74, (ScreenTextAlign)1, 0, 3, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 48, 1, 224, 106, (ScreenTextAlign)1, 0, 3, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 49, 1, 224, 138, (ScreenTextAlign)1, 0, 3, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 51, 1, 212, 170, (ScreenTextAlign)1, 0, 1, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 81, 1, 48, 8, (ScreenTextAlign)1, 0, 11, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 82, 1, 128, 10, (ScreenTextAlign)1, 0, 11, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 83, 1, 48, 42, (ScreenTextAlign)1, 0, 11, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 84, 1, 128, 42, (ScreenTextAlign)1, 0, 11, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 85, 1, 48, 74, (ScreenTextAlign)1, 0, 11, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 86, 1, 128, 74, (ScreenTextAlign)1, 0, 11, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                    },
                },
            },
            new ScreenPage
            {
                Id = "search_name", Name = "Search: name", MainOnTop = true,
                Variants = new string[] { "Normal", "None found" },
                Main = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(3, 3, 4, 28, 32, 32, 0, 0, null, new List<ScreenPaste> { P(43, 0, 0, 0, 0, -1, -1, -1, -1, null), P(45, 6, 9, 0, 0, -1, -1, -1, -1, null) }),
                        B(1, 1, 4, -1, 32, 32, 0, 0, null, new List<ScreenPaste> {  }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(0, 0, 0, 16, null),
                        L(5, 0, 0, 1, null),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 87, 1, 128, 8, (ScreenTextAlign)1, 32, 7, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "Normal"),
                        T((ScreenTextKind)0, 93, 1, 128, 8, (ScreenTextAlign)1, 32, 7, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "None found"),
                        T((ScreenTextKind)0, 81, 1, 128, 52, (ScreenTextAlign)1, 0, 7, 4, 3, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 126, 1, 128, 77, (ScreenTextAlign)1, 0, 7, 4, 3, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 53, 1, 128, 102, (ScreenTextAlign)1, 0, 7, 4, 3, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 53, 1, 128, 120, (ScreenTextAlign)1, 0, 7, 4, 3, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                    },
                },
                Sub = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(2, 2, 4, 29, 32, 32, 0, 0, null, new List<ScreenPaste> { P(75, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(0, 0, 0, 16, null),
                    },
                    Sprites = new List<ScreenSprite>
                    {
                        S("Back", 126, 125, 124, 3, 0, 3, 0, 212, 16, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Order", 126, 125, 124, 2, 0, 3, 0, 224, 48, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Name", 126, 125, 124, 2, 3, 3, 0, 224, 80, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Type", 126, 125, 124, 2, 0, 3, 0, 224, 112, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Form", 126, 125, 124, 2, 0, 3, 0, 224, 144, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("OK", 126, 125, 124, 1, 0, 3, 0, 212, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 1", 126, 125, 124, 0, 0, 3, 0, 48, 16, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 2", 126, 125, 124, 0, 0, 3, 0, 128, 16, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 3", 126, 125, 124, 0, 0, 3, 0, 48, 48, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 4", 126, 125, 124, 0, 0, 3, 0, 128, 48, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 5", 126, 125, 124, 0, 0, 3, 0, 48, 80, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 6", 126, 125, 124, 0, 0, 3, 0, 128, 80, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 7", 126, 125, 124, 0, 0, 3, 0, 48, 112, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 8", 126, 125, 124, 0, 0, 3, 0, 128, 112, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 9", 126, 125, 124, 0, 0, 3, 0, 48, 144, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 10", 126, 125, 124, 0, 3, 3, 0, 128, 144, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 50, 1, 224, 42, (ScreenTextAlign)1, 0, 3, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 47, 1, 224, 72, (ScreenTextAlign)1, 0, 3, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 48, 1, 224, 106, (ScreenTextAlign)1, 0, 3, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 49, 1, 224, 138, (ScreenTextAlign)1, 0, 3, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 51, 1, 212, 170, (ScreenTextAlign)1, 0, 1, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 54, 1, 48, 10, (ScreenTextAlign)1, 0, 11, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 55, 1, 128, 10, (ScreenTextAlign)1, 0, 11, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 56, 1, 48, 42, (ScreenTextAlign)1, 0, 11, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 57, 1, 128, 42, (ScreenTextAlign)1, 0, 11, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 58, 1, 48, 74, (ScreenTextAlign)1, 0, 11, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 59, 1, 128, 74, (ScreenTextAlign)1, 0, 11, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 60, 1, 48, 106, (ScreenTextAlign)1, 0, 11, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 61, 1, 128, 106, (ScreenTextAlign)1, 0, 11, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 62, 1, 48, 138, (ScreenTextAlign)1, 0, 11, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 126, 1, 128, 136, (ScreenTextAlign)1, 0, 11, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                    },
                },
            },
            new ScreenPage
            {
                Id = "search_type", Name = "Search: type", MainOnTop = true,
                Variants = new string[] { "Normal", "None found" },
                Main = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(3, 3, 4, 28, 32, 32, 0, 0, null, new List<ScreenPaste> { P(43, 0, 0, 0, 0, -1, -1, -1, -1, null), P(46, 6, 12, 0, 0, -1, -1, -1, -1, null) }),
                        B(1, 1, 4, -1, 32, 32, 0, 0, null, new List<ScreenPaste> {  }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(0, 0, 0, 16, null),
                        L(5, 0, 0, 1, null),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 88, 1, 128, 8, (ScreenTextAlign)1, 32, 7, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "Normal"),
                        T((ScreenTextKind)0, 93, 1, 128, 8, (ScreenTextAlign)1, 32, 7, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "None found"),
                        T((ScreenTextKind)0, 81, 1, 128, 52, (ScreenTextAlign)1, 0, 7, 4, 3, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 126, 1, 128, 77, (ScreenTextAlign)1, 0, 7, 4, 3, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 53, 1, 128, 102, (ScreenTextAlign)1, 0, 7, 4, 3, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 53, 1, 128, 120, (ScreenTextAlign)1, 0, 7, 4, 3, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                    },
                },
                Sub = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(2, 2, 4, 29, 32, 32, 0, 0, null, new List<ScreenPaste> { P(75, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(0, 0, 0, 16, null),
                    },
                    Sprites = new List<ScreenSprite>
                    {
                        S("Back", 126, 125, 124, 3, 0, 3, 0, 212, 16, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Order", 126, 125, 124, 2, 0, 3, 0, 224, 48, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Name", 126, 125, 124, 2, 0, 3, 0, 224, 80, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Type", 126, 125, 124, 2, 3, 3, 0, 224, 112, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Form", 126, 125, 124, 2, 0, 3, 0, 224, 144, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("OK", 126, 125, 124, 1, 0, 3, 0, 212, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 1", 126, 125, 124, 0, 0, 3, 0, 48, 16, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 2", 126, 125, 124, 0, 0, 3, 0, 128, 16, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 3", 126, 125, 124, 0, 0, 3, 0, 48, 48, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 4", 126, 125, 124, 0, 0, 3, 0, 128, 48, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 5", 126, 125, 124, 0, 0, 3, 0, 48, 80, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 6", 126, 125, 124, 0, 0, 3, 0, 128, 80, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 7", 126, 125, 124, 0, 0, 3, 0, 48, 112, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 8", 126, 125, 124, 0, 0, 3, 0, 128, 112, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 9", 126, 125, 124, 0, 0, 3, 0, 48, 144, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 10", 126, 125, 124, 0, 3, 3, 0, 128, 144, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Next types", 126, 125, 124, 5, 0, 3, 0, 24, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 50, 1, 224, 42, (ScreenTextAlign)1, 0, 3, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 47, 1, 224, 74, (ScreenTextAlign)1, 0, 3, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 48, 1, 224, 104, (ScreenTextAlign)1, 0, 3, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 49, 1, 224, 138, (ScreenTextAlign)1, 0, 3, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 51, 1, 212, 170, (ScreenTextAlign)1, 0, 1, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 64, 1, 48, 10, (ScreenTextAlign)1, 0, 11, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 70, 1, 128, 10, (ScreenTextAlign)1, 0, 11, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 73, 1, 48, 42, (ScreenTextAlign)1, 0, 11, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 71, 1, 128, 42, (ScreenTextAlign)1, 0, 11, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 72, 1, 48, 74, (ScreenTextAlign)1, 0, 11, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 76, 1, 128, 74, (ScreenTextAlign)1, 0, 11, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 75, 1, 48, 106, (ScreenTextAlign)1, 0, 11, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 77, 1, 128, 106, (ScreenTextAlign)1, 0, 11, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 80, 1, 48, 138, (ScreenTextAlign)1, 0, 11, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 127, 1, 128, 136, (ScreenTextAlign)1, 0, 11, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                    },
                },
            },
            new ScreenPage
            {
                Id = "search_type_2", Name = "Search: type (second page)", MainOnTop = true,
                Variants = new string[] { "Normal", "None found" },
                Main = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(3, 3, 4, 28, 32, 32, 0, 0, null, new List<ScreenPaste> { P(43, 0, 0, 0, 0, -1, -1, -1, -1, null), P(46, 6, 12, 0, 0, -1, -1, -1, -1, null) }),
                        B(1, 1, 4, -1, 32, 32, 0, 0, null, new List<ScreenPaste> {  }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(0, 0, 0, 16, null),
                        L(5, 0, 0, 1, null),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 88, 1, 128, 8, (ScreenTextAlign)1, 32, 7, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "Normal"),
                        T((ScreenTextKind)0, 93, 1, 128, 8, (ScreenTextAlign)1, 32, 7, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "None found"),
                        T((ScreenTextKind)0, 81, 1, 128, 52, (ScreenTextAlign)1, 0, 7, 4, 3, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 126, 1, 128, 77, (ScreenTextAlign)1, 0, 7, 4, 3, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 53, 1, 128, 102, (ScreenTextAlign)1, 0, 7, 4, 3, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 53, 1, 128, 120, (ScreenTextAlign)1, 0, 7, 4, 3, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                    },
                },
                Sub = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(2, 2, 4, 29, 32, 32, 0, 0, null, new List<ScreenPaste> { P(75, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(0, 0, 0, 16, null),
                    },
                    Sprites = new List<ScreenSprite>
                    {
                        S("Back", 126, 125, 124, 3, 0, 3, 0, 212, 16, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Order", 126, 125, 124, 2, 0, 3, 0, 224, 48, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Name", 126, 125, 124, 2, 0, 3, 0, 224, 80, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Type", 126, 125, 124, 2, 3, 3, 0, 224, 112, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Form", 126, 125, 124, 2, 0, 3, 0, 224, 144, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("OK", 126, 125, 124, 1, 0, 3, 0, 212, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 1", 126, 125, 124, 0, 0, 3, 0, 48, 16, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 2", 126, 125, 124, 0, 0, 3, 0, 128, 16, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 3", 126, 125, 124, 0, 0, 3, 0, 48, 48, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 4", 126, 125, 124, 0, 0, 3, 0, 128, 48, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 5", 126, 125, 124, 0, 0, 3, 0, 48, 80, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 6", 126, 125, 124, 0, 0, 3, 0, 128, 80, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 7", 126, 125, 124, 0, 0, 3, 0, 48, 112, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 8", 126, 125, 124, 0, 0, 3, 0, 128, 112, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 9", 126, 125, 124, 0, 3, 3, 0, 128, 144, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Previous types", 126, 125, 124, 4, 0, 3, 0, 24, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 50, 1, 224, 42, (ScreenTextAlign)1, 0, 3, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 47, 1, 224, 74, (ScreenTextAlign)1, 0, 3, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 48, 1, 224, 104, (ScreenTextAlign)1, 0, 3, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 49, 1, 224, 138, (ScreenTextAlign)1, 0, 3, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 51, 1, 212, 170, (ScreenTextAlign)1, 0, 1, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 65, 1, 48, 10, (ScreenTextAlign)1, 0, 11, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 66, 1, 128, 10, (ScreenTextAlign)1, 0, 11, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 68, 1, 48, 42, (ScreenTextAlign)1, 0, 11, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 67, 1, 128, 42, (ScreenTextAlign)1, 0, 11, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 74, 1, 48, 74, (ScreenTextAlign)1, 0, 11, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 69, 1, 128, 74, (ScreenTextAlign)1, 0, 11, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 78, 1, 48, 106, (ScreenTextAlign)1, 0, 11, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 79, 1, 128, 106, (ScreenTextAlign)1, 0, 11, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 127, 1, 128, 136, (ScreenTextAlign)1, 0, 11, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                    },
                },
            },
            new ScreenPage
            {
                Id = "search_form", Name = "Search: form", MainOnTop = true,
                Variants = new string[] { "Normal", "None found" },
                Main = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(3, 3, 4, 28, 32, 32, 0, 0, null, new List<ScreenPaste> { P(43, 0, 0, 0, 0, -1, -1, -1, -1, null), P(47, 6, 17, 0, 0, -1, -1, -1, -1, null) }),
                        B(1, 1, 4, -1, 32, 32, 0, 0, null, new List<ScreenPaste> {  }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(0, 0, 0, 16, null),
                        L(5, 0, 0, 1, null),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 89, 1, 128, 8, (ScreenTextAlign)1, 32, 7, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "Normal"),
                        T((ScreenTextKind)0, 93, 1, 128, 8, (ScreenTextAlign)1, 32, 7, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "None found"),
                        T((ScreenTextKind)0, 81, 1, 128, 52, (ScreenTextAlign)1, 0, 7, 4, 3, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 126, 1, 128, 77, (ScreenTextAlign)1, 0, 7, 4, 3, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 53, 1, 128, 102, (ScreenTextAlign)1, 0, 7, 4, 3, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 53, 1, 128, 120, (ScreenTextAlign)1, 0, 7, 4, 3, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                    },
                },
                Sub = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(2, 2, 4, 29, 32, 32, 0, 0, null, new List<ScreenPaste> { P(75, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(0, 0, 0, 16, null),
                    },
                    Sprites = new List<ScreenSprite>
                    {
                        S("Back", 126, 125, 124, 3, 0, 3, 0, 212, 16, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Order", 126, 125, 124, 2, 0, 3, 0, 224, 48, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Name", 126, 125, 124, 2, 0, 3, 0, 224, 80, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Type", 126, 125, 124, 2, 0, 3, 0, 224, 112, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Form", 126, 125, 124, 2, 3, 3, 0, 224, 144, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("OK", 126, 125, 124, 1, 0, 3, 0, 212, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 1 icon", 129, 128, 127, 0, 0, 3, 0, 28, 16, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 1", 126, 125, 124, 6, 0, 3, 0, 28, 16, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 2 icon", 129, 128, 127, 5, 0, 3, 0, 84, 16, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 2", 126, 125, 124, 6, 0, 3, 0, 84, 16, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 3 icon", 129, 128, 127, 10, 0, 3, 0, 140, 16, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 3", 126, 125, 124, 6, 0, 3, 0, 140, 16, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 4 icon", 129, 128, 127, 1, 0, 3, 0, 28, 48, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 4", 126, 125, 124, 6, 0, 3, 0, 28, 48, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 5 icon", 129, 128, 127, 6, 0, 3, 0, 84, 48, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 5", 126, 125, 124, 6, 0, 3, 0, 84, 48, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 6 icon", 129, 128, 127, 11, 0, 3, 0, 140, 48, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 6", 126, 125, 124, 6, 0, 3, 0, 140, 48, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 7 icon", 129, 128, 127, 2, 0, 3, 0, 28, 80, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 7", 126, 125, 124, 6, 0, 3, 0, 28, 80, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 8 icon", 129, 128, 127, 9, 0, 3, 0, 84, 80, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 8", 126, 125, 124, 6, 0, 3, 0, 84, 80, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 9 icon", 129, 128, 127, 12, 0, 3, 0, 140, 80, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 9", 126, 125, 124, 6, 0, 3, 0, 140, 80, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 10 icon", 129, 128, 127, 3, 0, 3, 0, 28, 112, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 10", 126, 125, 124, 6, 0, 3, 0, 28, 112, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 11 icon", 129, 128, 127, 8, 0, 3, 0, 84, 112, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 11", 126, 125, 124, 6, 0, 3, 0, 84, 112, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 12 icon", 129, 128, 127, 13, 0, 3, 0, 140, 112, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 12", 126, 125, 124, 6, 0, 3, 0, 140, 112, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 13 icon", 129, 128, 127, 4, 0, 3, 0, 28, 144, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 13", 126, 125, 124, 6, 0, 3, 0, 28, 144, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 14 icon", 129, 128, 127, 7, 0, 3, 0, 84, 144, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 14", 126, 125, 124, 6, 0, 3, 0, 84, 144, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Option 15", 126, 125, 124, 6, 3, 3, 0, 140, 144, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 50, 1, 224, 42, (ScreenTextAlign)1, 0, 3, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 47, 1, 224, 74, (ScreenTextAlign)1, 0, 3, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 48, 1, 224, 106, (ScreenTextAlign)1, 0, 3, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 49, 1, 224, 136, (ScreenTextAlign)1, 0, 3, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 51, 1, 212, 170, (ScreenTextAlign)1, 0, 1, 3, 2, 2, 0, 0, 3, 0, -1, 0, 0, 0, 0, false, null, null),
                    },
                },
            },
            new ScreenPage
            {
                Id = "search_running", Name = "Searching", MainOnTop = true,
                Variants = new string[] { "Sinnoh", "National" },
                Main = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(2, 1, 4, 28, 32, 32, 0, 0, null, new List<ScreenPaste> { P(40, 0, 0, 0, 20, -1, 12, -1, -1, "Sinnoh"), P(40, 0, 12, 0, 0, -1, 12, -1, -1, "Sinnoh"), P(42, 0, 0, 0, 20, -1, 12, -1, -1, "National"), P(42, 0, 12, 0, 0, -1, 12, -1, -1, "National") }),
                        B(3, 3, 4, 28, 32, 32, 0, 0, null, new List<ScreenPaste> { P(43, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                        B(1, 0, 4, -1, 32, 32, 0, 0, null, new List<ScreenPaste> {  }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(0, 0, 0, 16, null),
                        L(5, 0, 0, 1, null),
                    },
                    Sprites = new List<ScreenSprite>
                    {
                        S("Spinning ball", 79, 81, 80, 17, 0, 3, 0, 128, 96, 2, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 94, 1, 128, 128, (ScreenTextAlign)1, 0, 7, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                    },
                },
                Sub = new ScreenLayout
                {
                    Darken = 16,
                    Bgs = new List<ScreenBg>
                    {
                        B(2, 2, 4, 29, 32, 32, 0, 0, null, new List<ScreenPaste> { P(75, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(0, 0, 0, 16, null),
                    },
                },
            },
            new ScreenPage
            {
                Id = "info", Name = "Info", MainOnTop = true,
                Variants = new string[] { "Sinnoh", "National" },
                Main = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(2, 1, 4, 33, 32, 32, 0, 0, null, new List<ScreenPaste> { P(57, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                        B(1, 0, 4, -1, 32, 32, 0, 0, null, new List<ScreenPaste> {  }),
                        B(3, 3, 4, 33, 32, 32, 0, 0, null, new List<ScreenPaste> { P(50, 0, 0, 0, 0, -1, -1, -1, -1, null), P(51, 0, 3, 0, 0, -1, -1, -1, -1, null), P(52, 12, 8, 0, 0, -1, -1, -1, -1, null), P(54, 0, 16, 0, 0, -1, -1, -1, -1, null) }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(6, 0, 0, 16, null),
                        L(24, 0, 0, 1, "National"),
                    },
                    Sprites = new List<ScreenSprite>
                    {
                        S("Name tag", 76, 78, 77, 0, 0, 3, 0, 172, 32, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Caught mark", 76, 78, 77, 1, 0, 3, 0, 118, 32, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Category box", 88, 90, 89, 17, 0, 13, 0, 192, 52, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("First type", 88, 90, 89, 0, 0, 13, 0, 170, 72, 0, 1, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Second type", 88, 90, 89, 0, 0, 13, 0, 220, 72, 0, 2, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 102, 1, 16, 0, (ScreenTextAlign)0, 0, 0, 3, 2, 2, 0, 0, 12, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)2, -1, 1, 130, 24, (ScreenTextAlign)0, 0, 0, 3, 2, 2, 0, 0, 3, 0, 0, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)1, -1, 1, 157, 24, (ScreenTextAlign)0, 0, 0, 3, 2, 2, 0, 0, 3, 0, 0, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)3, -1, 1, 182, 44, (ScreenTextAlign)1, 0, 0, 3, 2, 2, 0, 0, 3, 0, 2, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 9, 1, 152, 88, (ScreenTextAlign)0, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 10, 1, 152, 104, (ScreenTextAlign)0, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)4, -1, 1, 184, 88, (ScreenTextAlign)0, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)5, -1, 1, 184, 104, (ScreenTextAlign)0, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)6, -1, 1, 128, 136, (ScreenTextAlign)3, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                    },
                    Mons = new List<ScreenMon>
                    {
                        M((ScreenMonKind)0, 48, 72, 2, 0, 0, (DexSizeRole)0, null),
                        M((ScreenMonKind)1, 120, 88, 0, 0, 0, (DexSizeRole)0, null),
                    },
                },
                Sub = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(1, 0, 4, 34, 32, 32, 0, 0, null, new List<ScreenPaste> { P(59, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                        B(2, 2, 4, 34, 32, 32, 0, 0, null, new List<ScreenPaste> { P(62, 0, 0, 0, 0, -1, -1, -1, -1, null), P(60, 6, 14, 0, 0, -1, -1, -1, -1, null), P(63, 6, 7, 0, 0, -1, -1, -1, -1, null) }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(6, 0, 0, 16, null),
                        L(24, 0, 0, 1, "National"),
                        L(9, 4, 0, 1, null),
                        L(9, 5, 0, 1, null),
                    },
                    Sprites = new List<ScreenSprite>
                    {
                        S("Info", 94, 96, 95, 0, 3, 11, 0, 28, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Area", 94, 96, 95, 1, 0, 11, 0, 68, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Cry", 94, 96, 95, 2, 0, 11, 0, 108, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Size", 94, 96, 95, 3, 0, 11, 0, 148, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Forms", 94, 96, 95, 4, 0, 11, 0, 188, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Back", 94, 96, 95, 5, 0, 11, 0, 228, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Next", 100, 102, 101, 0, 0, 11, 0, 128, 132, 2, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Back", 100, 102, 101, 0, 0, 11, 0, 128, 76, 2, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Next arrow", 97, 99, 98, 13, 0, 11, 0, 160, 134, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Back arrow", 97, 99, 98, 12, 0, 11, 0, 96, 78, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 33, 1, 96, 126, (ScreenTextAlign)0, 0, 4, 3, 2, 2, 0, 0, 11, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 34, 1, 160, 70, (ScreenTextAlign)2, 0, 4, 3, 2, 2, 0, 0, 11, 0, -1, 0, 0, 0, 0, false, null, null),
                    },
                },
            },
            new ScreenPage
            {
                Id = "info_foreign", Name = "Info in another language", MainOnTop = true,
                Variants = new string[] { "French", "German", "Italian", "Spanish", "Japanese" },
                Main = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(2, 1, 4, 33, 32, 32, 0, 0, null, new List<ScreenPaste> { P(57, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                        B(1, 0, 4, -1, 32, 32, 0, 0, null, new List<ScreenPaste> {  }),
                        B(3, 3, 4, 33, 32, 32, 0, 0, null, new List<ScreenPaste> { P(50, 0, 0, 0, 0, -1, -1, -1, -1, null), P(51, 0, 3, 0, 0, -1, -1, -1, -1, null), P(55, 0, 16, 0, 0, -1, -1, -1, -1, null), P(56, 12, 8, 0, 0, -1, -1, -1, -1, null) }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(6, 0, 0, 16, null),
                        L(24, 0, 0, 1, "National"),
                    },
                    Sprites = new List<ScreenSprite>
                    {
                        S("Name tag", 76, 78, 77, 0, 0, 3, 0, 172, 32, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Caught mark", 76, 78, 77, 1, 0, 3, 0, 118, 32, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Category box", 88, 90, 89, 17, 0, 13, 0, 192, 52, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 102, 1, 16, 0, (ScreenTextAlign)0, 0, 0, 3, 2, 2, 0, 0, 12, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)2, -1, 1, 130, 24, (ScreenTextAlign)0, 0, 0, 3, 2, 2, 0, 0, 3, 0, 0, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)1, -1, 1, 157, 24, (ScreenTextAlign)0, 0, 0, 3, 2, 2, 0, 0, 3, 0, 0, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)3, -1, 1, 182, 44, (ScreenTextAlign)1, 0, 0, 3, 2, 2, 0, 0, 3, 0, 2, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 23, 1, 176, 72, (ScreenTextAlign)1, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "French"),
                        T((ScreenTextKind)9, 0, 1, 120, 96, (ScreenTextAlign)0, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "French"),
                        T((ScreenTextKind)10, 0, 1, 240, 112, (ScreenTextAlign)2, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "French"),
                        T((ScreenTextKind)8, 0, 1, 128, 136, (ScreenTextAlign)3, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "French"),
                        T((ScreenTextKind)0, 24, 1, 176, 72, (ScreenTextAlign)1, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "German"),
                        T((ScreenTextKind)9, 1, 1, 120, 96, (ScreenTextAlign)0, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "German"),
                        T((ScreenTextKind)10, 1, 1, 240, 112, (ScreenTextAlign)2, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "German"),
                        T((ScreenTextKind)8, 1, 1, 128, 136, (ScreenTextAlign)3, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "German"),
                        T((ScreenTextKind)0, 25, 1, 176, 72, (ScreenTextAlign)1, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "Italian"),
                        T((ScreenTextKind)9, 2, 1, 120, 96, (ScreenTextAlign)0, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "Italian"),
                        T((ScreenTextKind)10, 2, 1, 240, 112, (ScreenTextAlign)2, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "Italian"),
                        T((ScreenTextKind)8, 2, 1, 128, 136, (ScreenTextAlign)3, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "Italian"),
                        T((ScreenTextKind)0, 26, 1, 176, 72, (ScreenTextAlign)1, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "Spanish"),
                        T((ScreenTextKind)9, 3, 1, 120, 96, (ScreenTextAlign)0, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "Spanish"),
                        T((ScreenTextKind)10, 3, 1, 240, 112, (ScreenTextAlign)2, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "Spanish"),
                        T((ScreenTextKind)8, 3, 1, 128, 136, (ScreenTextAlign)3, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "Spanish"),
                        T((ScreenTextKind)0, 125, 1, 176, 72, (ScreenTextAlign)1, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "Japanese"),
                        T((ScreenTextKind)9, 4, 1, 120, 96, (ScreenTextAlign)0, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "Japanese"),
                        T((ScreenTextKind)10, 4, 1, 240, 112, (ScreenTextAlign)2, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "Japanese"),
                        T((ScreenTextKind)8, 4, 1, 128, 136, (ScreenTextAlign)3, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "Japanese"),
                    },
                    Mons = new List<ScreenMon>
                    {
                        M((ScreenMonKind)0, 48, 72, 2, 0, 0, (DexSizeRole)0, null),
                    },
                },
                Sub = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(1, 0, 4, 34, 32, 32, 0, 0, null, new List<ScreenPaste> { P(59, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                        B(2, 2, 4, 34, 32, 32, 0, 0, null, new List<ScreenPaste> { P(62, 0, 0, 0, 0, -1, -1, -1, -1, null), P(60, 6, 14, 0, 0, -1, -1, -1, -1, null), P(63, 6, 7, 0, 0, -1, -1, -1, -1, null) }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(6, 0, 0, 16, null),
                        L(24, 0, 0, 1, "National"),
                        L(9, 4, 0, 1, null),
                        L(9, 5, 0, 1, null),
                    },
                    Sprites = new List<ScreenSprite>
                    {
                        S("Info", 94, 96, 95, 0, 3, 11, 0, 28, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Area", 94, 96, 95, 1, 0, 11, 0, 68, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Cry", 94, 96, 95, 2, 0, 11, 0, 108, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Size", 94, 96, 95, 3, 0, 11, 0, 148, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Forms", 94, 96, 95, 4, 0, 11, 0, 188, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Back", 94, 96, 95, 5, 0, 11, 0, 228, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Next", 100, 102, 101, 0, 0, 11, 0, 128, 132, 2, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Back", 100, 102, 101, 0, 0, 11, 0, 128, 76, 2, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Next arrow", 97, 99, 98, 13, 0, 11, 0, 160, 134, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Back arrow", 97, 99, 98, 12, 0, 11, 0, 96, 78, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Language", 97, 99, 98, 2, 0, 11, 0, 28, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "French"),
                        S("Language", 97, 99, 98, 2, 0, 11, 0, 28, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "German"),
                        S("Language", 97, 99, 98, 2, 0, 11, 0, 28, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "Italian"),
                        S("Language", 97, 99, 98, 2, 0, 11, 0, 28, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "Spanish"),
                        S("Language", 97, 99, 98, 2, 0, 11, 0, 28, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "Japanese"),
                        S("Language", 97, 99, 98, 5, 0, 11, 0, 68, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "French"),
                        S("Language", 97, 99, 98, 4, 0, 11, 0, 68, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "German"),
                        S("Language", 97, 99, 98, 4, 0, 11, 0, 68, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "Italian"),
                        S("Language", 97, 99, 98, 4, 0, 11, 0, 68, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "Spanish"),
                        S("Language", 97, 99, 98, 4, 0, 11, 0, 68, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "Japanese"),
                        S("Language", 97, 99, 98, 6, 0, 11, 0, 108, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "French"),
                        S("Language", 97, 99, 98, 7, 0, 11, 0, 108, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "German"),
                        S("Language", 97, 99, 98, 6, 0, 11, 0, 108, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "Italian"),
                        S("Language", 97, 99, 98, 6, 0, 11, 0, 108, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "Spanish"),
                        S("Language", 97, 99, 98, 6, 0, 11, 0, 108, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "Japanese"),
                        S("Language", 97, 99, 98, 8, 0, 11, 0, 148, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "French"),
                        S("Language", 97, 99, 98, 8, 0, 11, 0, 148, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "German"),
                        S("Language", 97, 99, 98, 9, 0, 11, 0, 148, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "Italian"),
                        S("Language", 97, 99, 98, 8, 0, 11, 0, 148, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "Spanish"),
                        S("Language", 97, 99, 98, 8, 0, 11, 0, 148, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "Japanese"),
                        S("Language", 97, 99, 98, 10, 0, 11, 0, 188, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "French"),
                        S("Language", 97, 99, 98, 10, 0, 11, 0, 188, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "German"),
                        S("Language", 97, 99, 98, 10, 0, 11, 0, 188, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "Italian"),
                        S("Language", 97, 99, 98, 11, 0, 11, 0, 188, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "Spanish"),
                        S("Language", 97, 99, 98, 10, 0, 11, 0, 188, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "Japanese"),
                        S("Language", 97, 99, 98, 0, 0, 11, 0, 228, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "French"),
                        S("Language", 97, 99, 98, 0, 0, 11, 0, 228, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "German"),
                        S("Language", 97, 99, 98, 0, 0, 11, 0, 228, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "Italian"),
                        S("Language", 97, 99, 98, 0, 0, 11, 0, 228, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "Spanish"),
                        S("Language", 97, 99, 98, 1, 0, 11, 0, 228, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "Japanese"),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 33, 1, 96, 126, (ScreenTextAlign)0, 0, 4, 3, 2, 2, 0, 0, 11, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 34, 1, 160, 70, (ScreenTextAlign)2, 0, 4, 3, 2, 2, 0, 0, 11, 0, -1, 0, 0, 0, 0, false, null, null),
                    },
                },
            },
            new ScreenPage
            {
                Id = "area", Name = "Area", MainOnTop = true,
                Variants = new string[] { "Morning", "Day", "Night", "Area unknown" },
                Main = new ScreenLayout
                {
                    Darken = 0,
                    Habitat = new DexHabitat(),
                    Bgs = new List<ScreenBg>
                    {
                        B(2, 1, 4, 33, 32, 32, 0, 0, null, new List<ScreenPaste> { P(57, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                        B(1, 0, 4, -1, 32, 32, 0, 0, null, new List<ScreenPaste> {  }),
                        B(3, 3, 4, 33, 32, 32, 0, 0, null, new List<ScreenPaste> { P(64, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(6, 0, 0, 16, null),
                        L(24, 0, 0, 1, "National"),
                    },
                    Sprites = new List<ScreenSprite>
                    {
                        S("Area unknown", 91, 93, 92, 2, 0, 14, 0, 160, 96, 1, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "Area unknown"),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 103, 1, 16, 0, (ScreenTextAlign)0, 0, 0, 3, 2, 2, 0, 0, 12, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 36, 1, 44, 32, (ScreenTextAlign)1, 0, 2, 3, 2, 2, 0, 0, 14, 0, -1, 0, 0, 0, 0, false, null, "Morning"),
                        T((ScreenTextKind)0, 37, 1, 44, 32, (ScreenTextAlign)1, 0, 2, 3, 2, 2, 0, 0, 14, 0, -1, 0, 0, 0, 0, false, null, "Day"),
                        T((ScreenTextKind)0, 38, 1, 44, 32, (ScreenTextAlign)1, 0, 2, 3, 2, 2, 0, 0, 14, 0, -1, 0, 0, 0, 0, false, null, "Night"),
                        T((ScreenTextKind)0, 35, 1, 160, 88, (ScreenTextAlign)1, 0, 2, 3, 2, 2, 0, 0, 14, 0, -1, 0, 0, 0, 0, false, null, "Area unknown"),
                        T((ScreenTextKind)0, 37, 1, 44, 32, (ScreenTextAlign)1, 0, 2, 3, 2, 2, 0, 0, 14, 0, -1, 0, 0, 0, 0, false, null, "Area unknown"),
                    },
                    Mons = new List<ScreenMon>
                    {
                        M((ScreenMonKind)0, 40, 120, 2, 0, 0, (DexSizeRole)0, null),
                    },
                },
                Sub = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(1, 0, 4, 34, 32, 32, 0, 0, null, new List<ScreenPaste> { P(59, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                        B(2, 2, 4, 34, 32, 32, 0, 0, null, new List<ScreenPaste> { P(69, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(6, 0, 0, 16, null),
                        L(24, 0, 0, 1, "National"),
                        L(15, 2, 0, 1, "Morning"),
                        L(16, 2, 0, 1, "Day"),
                        L(17, 2, 0, 1, "Night"),
                        L(16, 2, 0, 1, "Area unknown"),
                    },
                    Sprites = new List<ScreenSprite>
                    {
                        S("Info", 94, 96, 95, 0, 0, 11, 0, 28, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Area", 94, 96, 95, 1, 3, 11, 0, 68, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Cry", 94, 96, 95, 2, 0, 11, 0, 108, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Size", 94, 96, 95, 3, 0, 11, 0, 148, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Forms", 94, 96, 95, 4, 0, 11, 0, 188, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Back", 94, 96, 95, 5, 0, 11, 0, 228, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Time of day", 103, 105, 104, 0, 0, 11, 0, 32, 128, 1, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "Morning"),
                        S("Time of day", 103, 105, 104, 1, 0, 11, 0, 128, 96, 1, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "Day"),
                        S("Time of day", 103, 105, 104, 2, 0, 11, 0, 224, 128, 1, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "Night"),
                        S("Time of day", 103, 105, 104, 1, 0, 11, 0, 128, 96, 1, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "Area unknown"),
                    },
                },
            },
            new ScreenPage
            {
                Id = "cry", Name = "Cry", MainOnTop = true,
                Variants = new string[] { "Sinnoh", "National" },
                Main = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(2, 1, 4, 33, 32, 32, 0, 0, null, new List<ScreenPaste> { P(57, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                        B(1, 0, 4, -1, 32, 32, 0, 0, null, new List<ScreenPaste> {  }),
                        B(3, 3, 4, 33, 32, 32, 0, 0, null, new List<ScreenPaste> { P(70, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(6, 0, 0, 16, null),
                        L(24, 0, 0, 1, "National"),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 104, 1, 16, 0, (ScreenTextAlign)0, 0, 0, 3, 2, 2, 0, 0, 12, 0, -1, 0, 0, 0, 0, false, null, null),
                    },
                    Mons = new List<ScreenMon>
                    {
                        M((ScreenMonKind)0, 48, 64, 2, 0, 0, (DexSizeRole)0, null),
                    },
                    Rects = new List<ScreenRect>
                    {
                        R(1, 0, 151, 256, 1, 9, 6, null),
                    },
                },
                Sub = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(1, 0, 4, 34, 32, 32, 0, 0, null, new List<ScreenPaste> { P(59, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                        B(2, 2, 4, 34, 32, 32, 0, 0, null, new List<ScreenPaste> { P(71, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                        B(3, 3, 8, 35, 32, 32, -48, -16, null, new List<ScreenPaste> { P(72, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(6, 0, 0, 16, null),
                        L(24, 0, 0, 1, "National"),
                        L(20, 7, 1, 1, null),
                    },
                    Sprites = new List<ScreenSprite>
                    {
                        S("Info", 94, 96, 95, 0, 0, 11, 0, 28, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Area", 94, 96, 95, 1, 0, 11, 0, 68, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Cry", 94, 96, 95, 2, 3, 11, 0, 108, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Size", 94, 96, 95, 3, 0, 11, 0, 148, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Forms", 94, 96, 95, 4, 0, 11, 0, 188, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Back", 94, 96, 95, 5, 0, 11, 0, 228, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Chorus and pan switch", 114, 113, 112, 4, 8, 18, 0, 64, 67, 2, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Dial", 114, 113, 112, 1, 0, 18, 0, 51, 157, 2, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Play", 114, 113, 112, 3, 5, 18, 0, 180, 131, 2, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Loop", 114, 113, 112, 6, 5, 18, 0, 230, 166, 2, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 41, 1, 64, 84, (ScreenTextAlign)1, 0, 0, 3, 2, 2, 0, 0, 18, 0, -1, 0, 0, 0, 0, false, null, null),
                    },
                },
            },
            new ScreenPage
            {
                Id = "size_height", Name = "Size: height", MainOnTop = true,
                Variants = new string[] { "Lucas", "Dawn" },
                Main = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(2, 1, 4, 33, 32, 32, 0, 0, null, new List<ScreenPaste> { P(57, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                        B(1, 0, 4, -1, 32, 32, 0, 0, null, new List<ScreenPaste> {  }),
                        B(3, 3, 4, 33, 32, 32, 0, 0, null, new List<ScreenPaste> { P(74, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(6, 0, 0, 16, null),
                        L(24, 0, 0, 1, "National"),
                    },
                    Sprites = new List<ScreenSprite>
                    {
                        S("Trainer", 91, 93, 92, 5, 0, 14, 0, 168, 88, 1, 0, -1, 0, 0, (DexSizeRole)2, (DexReadout)0, 0, 0, "Lucas"),
                        S("Trainer", 91, 93, 92, 6, 0, 14, 0, 168, 88, 1, 0, -1, 0, 0, (DexSizeRole)2, (DexReadout)0, 0, 0, "Dawn"),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 105, 1, 16, 0, (ScreenTextAlign)0, 0, 0, 3, 2, 2, 0, 0, 12, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 43, 1, 128, 24, (ScreenTextAlign)1, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 9, 1, 32, 168, (ScreenTextAlign)0, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 9, 1, 152, 168, (ScreenTextAlign)0, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)1, -1, 1, 26, 152, (ScreenTextAlign)0, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)4, -1, 1, 110, 168, (ScreenTextAlign)2, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 95, 1, 230, 168, (ScreenTextAlign)2, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "Lucas"),
                        T((ScreenTextKind)0, 96, 1, 230, 168, (ScreenTextAlign)2, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "Dawn"),
                    },
                    Mons = new List<ScreenMon>
                    {
                        M((ScreenMonKind)0, 88, 88, 2, 15, 0, (DexSizeRole)1, null),
                    },
                },
                Sub = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(1, 0, 4, 34, 32, 32, 0, 0, null, new List<ScreenPaste> { P(59, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                        B(2, 2, 4, 34, 32, 32, 0, 0, null, new List<ScreenPaste> { P(61, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(6, 0, 0, 16, null),
                        L(24, 0, 0, 1, "National"),
                        L(9, 4, 0, 1, null),
                        L(9, 5, 0, 1, null),
                    },
                    Sprites = new List<ScreenSprite>
                    {
                        S("Info", 94, 96, 95, 0, 0, 11, 0, 28, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Area", 94, 96, 95, 1, 0, 11, 0, 68, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Cry", 94, 96, 95, 2, 0, 11, 0, 108, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Size", 94, 96, 95, 3, 3, 11, 0, 148, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Forms", 94, 96, 95, 4, 0, 11, 0, 188, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Back", 94, 96, 95, 5, 0, 11, 0, 228, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 45, 1, 68, 112, (ScreenTextAlign)1, 0, 4, 3, 2, 2, 0, 0, 11, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 46, 1, 188, 112, (ScreenTextAlign)1, 0, 4, 3, 2, 2, 0, 0, 11, 0, -1, 0, 0, 0, 0, false, null, null),
                    },
                },
            },
            new ScreenPage
            {
                Id = "size_weight", Name = "Size: weight", MainOnTop = true,
                Variants = new string[] { "Lucas", "Dawn" },
                Main = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(2, 1, 4, 33, 32, 32, 0, 0, null, new List<ScreenPaste> { P(57, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                        B(1, 0, 4, -1, 32, 32, 0, 0, null, new List<ScreenPaste> {  }),
                        B(3, 3, 4, 33, 32, 32, 0, 0, null, new List<ScreenPaste> { P(73, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(6, 0, 0, 16, null),
                        L(24, 0, 0, 1, "National"),
                    },
                    Sprites = new List<ScreenSprite>
                    {
                        S("Trainer", 91, 93, 92, 3, 0, 14, 0, 184, 64, 3, 0, -1, 0, 0, (DexSizeRole)6, (DexReadout)0, 0, 0, "Lucas"),
                        S("Trainer", 91, 93, 92, 4, 0, 14, 0, 184, 64, 3, 0, -1, 0, 0, (DexSizeRole)6, (DexReadout)0, 0, 0, "Dawn"),
                        S("Stand", 91, 93, 92, 1, 0, 14, 0, 128, 106, 1, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Pan", 91, 93, 92, 0, 0, 14, 0, 184, 88, 3, 0, -1, 0, 0, (DexSizeRole)4, (DexReadout)0, 0, 0, null),
                        S("Pan", 91, 93, 92, 0, 0, 14, 0, 72, 88, 3, 0, -1, 0, 0, (DexSizeRole)3, (DexReadout)0, 0, 0, null),
                        S("Beam", -1, 36, -1, 0, 0, 6, 0, 64, 88, 2, 0, -1, 128, 16, (DexSizeRole)7, (DexReadout)0, 0, 0, null),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 105, 1, 16, 0, (ScreenTextAlign)0, 0, 0, 3, 2, 2, 0, 0, 12, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 44, 1, 128, 24, (ScreenTextAlign)1, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 10, 1, 32, 168, (ScreenTextAlign)0, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 10, 1, 152, 168, (ScreenTextAlign)0, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)1, -1, 1, 26, 152, (ScreenTextAlign)0, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)5, -1, 1, 110, 168, (ScreenTextAlign)2, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 97, 1, 230, 168, (ScreenTextAlign)2, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "Lucas"),
                        T((ScreenTextKind)0, 98, 1, 230, 168, (ScreenTextAlign)2, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, "Dawn"),
                    },
                    Mons = new List<ScreenMon>
                    {
                        M((ScreenMonKind)3, 72, 64, 3, 0, 0, (DexSizeRole)5, null),
                    },
                },
                Sub = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(1, 0, 4, 34, 32, 32, 0, 0, null, new List<ScreenPaste> { P(59, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                        B(2, 2, 4, 34, 32, 32, 0, 0, null, new List<ScreenPaste> { P(61, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(6, 0, 0, 16, null),
                        L(24, 0, 0, 1, "National"),
                        L(9, 4, 0, 1, null),
                        L(9, 5, 0, 1, null),
                    },
                    Sprites = new List<ScreenSprite>
                    {
                        S("Info", 94, 96, 95, 0, 0, 11, 0, 28, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Area", 94, 96, 95, 1, 0, 11, 0, 68, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Cry", 94, 96, 95, 2, 0, 11, 0, 108, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Size", 94, 96, 95, 3, 3, 11, 0, 148, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Forms", 94, 96, 95, 4, 0, 11, 0, 188, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Back", 94, 96, 95, 5, 0, 11, 0, 228, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 45, 1, 68, 112, (ScreenTextAlign)1, 0, 4, 3, 2, 2, 0, 0, 11, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 46, 1, 188, 112, (ScreenTextAlign)1, 0, 4, 3, 2, 2, 0, 0, 11, 0, -1, 0, 0, 0, 0, false, null, null),
                    },
                },
            },
            new ScreenPage
            {
                Id = "forms", Name = "Forms", MainOnTop = true,
                Variants = new string[] { "Sinnoh", "National" },
                Main = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(2, 1, 4, 33, 32, 32, 0, 0, null, new List<ScreenPaste> { P(57, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                        B(1, 0, 4, -1, 32, 32, 0, 0, null, new List<ScreenPaste> {  }),
                        B(3, 3, 4, 33, 32, 32, 0, 0, null, new List<ScreenPaste> { P(50, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(6, 0, 0, 16, null),
                        L(24, 0, 0, 1, "National"),
                    },
                    Sprites = new List<ScreenSprite>
                    {
                        S("Front box", 123, 122, 121, 0, 0, 22, 0, 76, 96, 3, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Back box", 123, 122, 121, 2, 0, 22, 0, 180, 96, 3, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 106, 1, 16, 0, (ScreenTextAlign)0, 0, 0, 3, 2, 2, 0, 0, 12, 0, -1, 0, 0, 0, 0, false, null, null),
                    },
                    Mons = new List<ScreenMon>
                    {
                        M((ScreenMonKind)0, 76, 88, 2, 0, 0, (DexSizeRole)0, null),
                        M((ScreenMonKind)2, 180, 88, 2, 0, 0, (DexSizeRole)0, null),
                    },
                },
                Sub = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(1, 0, 4, 34, 32, 32, 0, 0, null, new List<ScreenPaste> { P(59, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                        B(2, 2, 4, 34, 32, 32, 0, 0, null, new List<ScreenPaste> { P(58, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(6, 0, 0, 16, null),
                        L(24, 0, 0, 1, "National"),
                        L(9, 4, 0, 1, null),
                    },
                    Sprites = new List<ScreenSprite>
                    {
                        S("Info", 94, 96, 95, 0, 0, 11, 0, 28, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Area", 94, 96, 95, 1, 0, 11, 0, 68, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Cry", 94, 96, 95, 2, 0, 11, 0, 108, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Size", 94, 96, 95, 3, 0, 11, 0, 148, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Forms", 94, 96, 95, 4, 3, 11, 0, 188, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Back", 94, 96, 95, 5, 0, 11, 0, 228, 24, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 101, 1, 128, 136, (ScreenTextAlign)1, 0, 4, 3, 2, 2, 0, 0, 11, 0, -1, 0, 0, 0, 0, false, null, null),
                    },
                },
            },
            new ScreenPage
            {
                Id = "catch_registered", Name = "Registered after a catch", MainOnTop = true,
                Variants = new string[] { "Sinnoh", "National" },
                Main = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(3, 3, 4, 33, 32, 32, 0, 0, null, new List<ScreenPaste> { P(50, 0, 0, 0, 0, -1, -1, -1, -1, null), P(51, 0, 3, 0, 0, -1, -1, -1, -1, null), P(52, 12, 8, 0, 0, -1, -1, -1, -1, null), P(54, 0, 16, 0, 0, -1, -1, -1, -1, null) }),
                        B(2, 2, 4, 33, 32, 32, 0, 0, null, new List<ScreenPaste> { P(57, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                        B(1, 1, 4, -1, 32, 32, 0, 0, null, new List<ScreenPaste> {  }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(6, 0, 0, 16, null),
                        L(23, 0, 0, 1, "Sinnoh"),
                        L(24, 0, 0, 1, "National"),
                    },
                    Sprites = new List<ScreenSprite>
                    {
                        S("Name tag", 76, 78, 77, 0, 0, 3, 0, 172, 32, 2, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Caught mark", 76, 78, 77, 1, 0, 3, 0, 118, 32, 2, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Category box", 88, 90, 89, 17, 0, 13, 0, 192, 52, 2, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("First type", 88, 90, 89, 0, 0, 13, 0, 170, 72, 2, 1, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Second type", 88, 90, 89, 0, 0, 13, 0, 220, 72, 2, 2, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)2, -1, 1, 130, 24, (ScreenTextAlign)0, 0, 0, 3, 2, 2, 0, 0, 3, 2, 0, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)1, -1, 1, 157, 24, (ScreenTextAlign)0, 0, 0, 3, 2, 2, 0, 0, 3, 2, 0, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)3, -1, 1, 182, 44, (ScreenTextAlign)1, 0, 0, 3, 2, 2, 0, 0, 3, 2, 2, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 110, 1, 32, 0, (ScreenTextAlign)0, 0, 12, 3, 4, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 9, 1, 152, 88, (ScreenTextAlign)0, 0, 12, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 10, 1, 152, 104, (ScreenTextAlign)0, 0, 12, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)4, -1, 1, 184, 88, (ScreenTextAlign)0, 0, 12, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)5, -1, 1, 184, 104, (ScreenTextAlign)0, 0, 12, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)6, -1, 1, 128, 136, (ScreenTextAlign)3, 0, 12, 2, 1, 0, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                    },
                    Mons = new List<ScreenMon>
                    {
                        M((ScreenMonKind)0, 48, 72, 0, 0, 0, (DexSizeRole)0, null),
                        M((ScreenMonKind)1, 120, 88, 2, 0, 0, (DexSizeRole)0, null),
                    },
                },
                Sub = new ScreenLayout
                {
                    Darken = 0,
                },
            },
        };

        private static List<ScreenPage> HeartGoldSoulSilver() => new List<ScreenPage>
        {
            new ScreenPage
            {
                Id = "cover", Name = "Cover", MainOnTop = false,
                Variants = new string[] { "Johto", "National", "No National Dex" },
                Main = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(0, 2, 4, 1, 64, 32, 0, 0, null, new List<ScreenPaste> { P(0, 0, 0, 0, 0, -1, -1, -1, -1, null), P(7, 5, 3, 0, 0, -1, -1, -1, -1, "Johto|No National Dex"), P(8, 5, 3, 0, 0, -1, -1, -1, -1, "National"), P(-1, 3, 15, 0, 0, 26, 2, -1, 22, "No National Dex") }),
                        B(2, 1, 4, 4, 32, 32, 0, 0, null, new List<ScreenPaste> { P(5, 0, 19, 0, 0, -1, -1, -1, -1, null) }),
                        B(3, 3, 4, 3, 32, 32, 0, 0, null, new List<ScreenPaste> { P(10, 0, 0, 0, 0, -1, -1, -1, -1, null), P(10, 0, 5, 0, 0, -1, -1, -1, -1, null), P(10, 0, 10, 0, 0, -1, -1, -1, -1, null), P(10, 0, 15, 0, 0, -1, -1, -1, -1, null), P(10, 0, 20, 0, 0, -1, -1, -1, -1, null) }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(2, 0, 0, 16, null),
                    },
                    Sprites = new List<ScreenSprite>
                    {
                        S("Dex switch", 13, 12, 14, 23, 0, 15, 1, 144, 128, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "Johto"),
                        S("Dex switch", 13, 12, 14, 23, 0, 15, 1, 112, 128, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "National"),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 0, 0, 112, 72, (ScreenTextAlign)2, 0, 9, 15, 8, 0, 0, 0, -1, 0, -1, 2, 9, 12, 2, true, null, null),
                        T((ScreenTextKind)0, 1, 0, 240, 72, (ScreenTextAlign)2, 0, 9, 15, 8, 0, 0, 0, -1, 0, -1, 18, 9, 12, 2, true, null, null),
                        T((ScreenTextKind)7, -1, 0, 88, 92, (ScreenTextAlign)0, 0, 9, 15, 8, 0, 342, 0, -1, 0, -1, 11, 11, 3, 2, true, null, null),
                        T((ScreenTextKind)7, -1, 0, 216, 92, (ScreenTextAlign)0, 0, 9, 15, 8, 0, 210, 0, -1, 0, -1, 27, 11, 3, 2, true, null, null),
                    },
                },
                Sub = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(0, 0, 4, 17, 64, 32, 0, 0, null, new List<ScreenPaste> { P(16, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                        B(3, 3, 4, 19, 32, 32, 0, 0, null, new List<ScreenPaste> { P(20, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(18, 0, 0, 16, null),
                    },
                },
            },
            new ScreenPage
            {
                Id = "list", Name = "List", MainOnTop = false,
                Variants = new string[] { "Entry", "Foreign entry", "No data" },
                Main = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(0, 2, 4, 1, 64, 32, 256, 0, null, new List<ScreenPaste> { P(0, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                        B(1, 0, 4, -1, 32, 32, 0, 0, null, new List<ScreenPaste> {  }),
                        B(2, 1, 4, 4, 32, 32, 0, 0, null, new List<ScreenPaste> { P(6, 0, 19, 0, 0, -1, -1, -1, -1, null), P(9, 0, 15, 0, 0, -1, -1, -1, -1, null), P(6, 8, 19, 8, 0, 16, -1, 11, -1, "No data") }),
                        B(3, 3, 4, 3, 32, 32, 0, 0, null, new List<ScreenPaste> { P(10, 0, 0, 0, 0, -1, -1, -1, -1, null), P(10, 0, 5, 0, 0, -1, -1, -1, -1, null), P(10, 0, 10, 0, 0, -1, -1, -1, -1, null), P(10, 0, 15, 0, 0, -1, -1, -1, -1, null), P(10, 0, 20, 0, 0, -1, -1, -1, -1, null), P(-1, 5, 1, 0, 0, 1, 1, -1, 4101, null), P(-1, 6, 1, 0, 0, 1, 1, -1, 4100, null), P(-1, 7, 1, 0, 0, 1, 1, -1, 4100, null), P(-1, 4, 1, 0, 0, 1, 1, -1, 4098, null), P(-1, 10, 1, 0, 0, 1, 1, -1, 4101, null), P(-1, 11, 1, 0, 0, 1, 1, -1, 4100, null), P(-1, 12, 1, 0, 0, 1, 1, -1, 4101, null), P(-1, 9, 1, 0, 0, 1, 1, -1, 4098, null), P(-1, 15, 1, 0, 0, 1, 1, -1, 4101, null), P(-1, 16, 1, 0, 0, 1, 1, -1, 4100, null), P(-1, 17, 1, 0, 0, 1, 1, -1, 4102, null), P(-1, 14, 1, 0, 0, 1, 1, -1, 4098, null), P(-1, 20, 1, 0, 0, 1, 1, -1, 4101, null), P(-1, 21, 1, 0, 0, 1, 1, -1, 4100, null), P(-1, 22, 1, 0, 0, 1, 1, -1, 4103, null), P(-1, 19, 1, 0, 0, 1, 1, -1, 4098, null), P(-1, 25, 1, 0, 0, 1, 1, -1, 4101, null), P(-1, 26, 1, 0, 0, 1, 1, -1, 4100, null), P(-1, 27, 1, 0, 0, 1, 1, -1, 4104, null), P(-1, 24, 1, 0, 0, 1, 1, -1, 4098, null), P(-1, 5, 6, 0, 0, 1, 1, -1, 4101, null), P(-1, 6, 6, 0, 0, 1, 1, -1, 4100, null), P(-1, 7, 6, 0, 0, 1, 1, -1, 4105, null), P(-1, 4, 6, 0, 0, 1, 1, -1, 4098, null), P(-1, 10, 6, 0, 0, 1, 1, -1, 4101, null), P(-1, 11, 6, 0, 0, 1, 1, -1, 4100, null), P(-1, 12, 6, 0, 0, 1, 1, -1, 4106, null), P(-1, 9, 6, 0, 0, 1, 1, -1, 4098, null), P(-1, 15, 6, 0, 0, 1, 1, -1, 4101, null), P(-1, 16, 6, 0, 0, 1, 1, -1, 4100, null), P(-1, 17, 6, 0, 0, 1, 1, -1, 4107, null), P(-1, 14, 6, 0, 0, 1, 1, -1, 4098, null), P(-1, 20, 6, 0, 0, 1, 1, -1, 4101, null), P(-1, 21, 6, 0, 0, 1, 1, -1, 4100, null), P(-1, 22, 6, 0, 0, 1, 1, -1, 4108, null), P(-1, 19, 6, 0, 0, 1, 1, -1, 4098, null), P(-1, 25, 6, 0, 0, 1, 1, -1, 4101, null), P(-1, 26, 6, 0, 0, 1, 1, -1, 4101, null), P(-1, 27, 6, 0, 0, 1, 1, -1, 4099, null), P(-1, 24, 6, 0, 0, 1, 1, -1, 4098, null), P(-1, 5, 11, 0, 0, 1, 1, -1, 4101, null), P(-1, 6, 11, 0, 0, 1, 1, -1, 4101, null), P(-1, 7, 11, 0, 0, 1, 1, -1, 4100, null), P(-1, 4, 11, 0, 0, 1, 1, -1, 4098, null), P(-1, 10, 11, 0, 0, 1, 1, -1, 4101, null), P(-1, 11, 11, 0, 0, 1, 1, -1, 4101, null), P(-1, 12, 11, 0, 0, 1, 1, -1, 4101, null), P(-1, 9, 11, 0, 0, 1, 1, -1, 4098, null), P(-1, 15, 11, 0, 0, 1, 1, -1, 4101, null), P(-1, 16, 11, 0, 0, 1, 1, -1, 4101, null), P(-1, 17, 11, 0, 0, 1, 1, -1, 4102, null), P(-1, 14, 11, 0, 0, 1, 1, -1, 4098, null), P(-1, 20, 11, 0, 0, 1, 1, -1, 4101, null), P(-1, 21, 11, 0, 0, 1, 1, -1, 4101, null), P(-1, 22, 11, 0, 0, 1, 1, -1, 4103, null), P(-1, 19, 11, 0, 0, 1, 1, -1, 4098, null), P(-1, 25, 11, 0, 0, 1, 1, -1, 4101, null), P(-1, 26, 11, 0, 0, 1, 1, -1, 4101, null), P(-1, 27, 11, 0, 0, 1, 1, -1, 4104, null), P(-1, 24, 11, 0, 0, 1, 1, -1, 4098, null), P(-1, 5, 16, 0, 0, 1, 1, -1, 4101, null), P(-1, 6, 16, 0, 0, 1, 1, -1, 4101, null), P(-1, 7, 16, 0, 0, 1, 1, -1, 4105, null), P(-1, 4, 16, 0, 0, 1, 1, -1, 4098, null), P(-1, 10, 16, 0, 0, 1, 1, -1, 4101, null), P(-1, 11, 16, 0, 0, 1, 1, -1, 4101, null), P(-1, 12, 16, 0, 0, 1, 1, -1, 4106, null), P(-1, 9, 16, 0, 0, 1, 1, -1, 4098, null), P(-1, 15, 16, 0, 0, 1, 1, -1, 4101, null), P(-1, 16, 16, 0, 0, 1, 1, -1, 4101, null), P(-1, 17, 16, 0, 0, 1, 1, -1, 4107, null), P(-1, 14, 16, 0, 0, 1, 1, -1, 4098, null), P(-1, 20, 16, 0, 0, 1, 1, -1, 4101, null), P(-1, 21, 16, 0, 0, 1, 1, -1, 4101, null), P(-1, 22, 16, 0, 0, 1, 1, -1, 4108, null), P(-1, 19, 16, 0, 0, 1, 1, -1, 4098, null), P(-1, 25, 16, 0, 0, 1, 1, -1, 4101, null), P(-1, 26, 16, 0, 0, 1, 1, -1, 4102, null), P(-1, 27, 16, 0, 0, 1, 1, -1, 4099, null), P(-1, 24, 16, 0, 0, 1, 1, -1, 4098, null), P(-1, 5, 21, 0, 0, 1, 1, -1, 4101, null), P(-1, 6, 21, 0, 0, 1, 1, -1, 4102, null), P(-1, 7, 21, 0, 0, 1, 1, -1, 4100, null), P(-1, 4, 21, 0, 0, 1, 1, -1, 4098, null), P(-1, 10, 21, 0, 0, 1, 1, -1, 4101, null), P(-1, 11, 21, 0, 0, 1, 1, -1, 4102, null), P(-1, 12, 21, 0, 0, 1, 1, -1, 4101, null), P(-1, 9, 21, 0, 0, 1, 1, -1, 4098, null), P(-1, 15, 21, 0, 0, 1, 1, -1, 4101, null), P(-1, 16, 21, 0, 0, 1, 1, -1, 4102, null), P(-1, 17, 21, 0, 0, 1, 1, -1, 4102, null), P(-1, 14, 21, 0, 0, 1, 1, -1, 4098, null), P(-1, 20, 21, 0, 0, 1, 1, -1, 4101, null), P(-1, 21, 21, 0, 0, 1, 1, -1, 4102, null), P(-1, 22, 21, 0, 0, 1, 1, -1, 4103, null), P(-1, 19, 21, 0, 0, 1, 1, -1, 4098, null), P(-1, 25, 21, 0, 0, 1, 1, -1, 4101, null), P(-1, 26, 21, 0, 0, 1, 1, -1, 4102, null), P(-1, 27, 21, 0, 0, 1, 1, -1, 4104, null), P(-1, 24, 21, 0, 0, 1, 1, -1, 4098, null) }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(2, 0, 0, 16, null),
                    },
                    Sprites = new List<ScreenSprite>
                    {
                        S("Grid cursor", 13, 12, 14, 1, 0, 15, 0, 128, 24, 3, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Scroll up", 13, 12, 14, 5, 0, 15, 1, 242, 12, 3, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Scroll down", 13, 12, 14, 8, 0, 15, 1, 242, 140, 3, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Scroll bar", 13, 12, 14, 4, 0, 15, 1, 242, 76, 3, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Scroll thumb", 13, 12, 14, 11, 0, 15, 1, 242, 40, 3, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)1, -1, 1, 124, 128, (ScreenTextAlign)1, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 10, 16, 11, 2, false, null, "Entry|Foreign entry"),
                    },
                    Mons = new List<ScreenMon>
                    {
                        M((ScreenMonKind)3, 48, 24, 3, 0, -2, (DexSizeRole)0, null),
                        M((ScreenMonKind)3, 88, 24, 3, 0, -1, (DexSizeRole)0, null),
                        M((ScreenMonKind)3, 128, 24, 3, 0, 0, (DexSizeRole)0, null),
                        M((ScreenMonKind)3, 168, 24, 3, 0, 1, (DexSizeRole)0, null),
                        M((ScreenMonKind)3, 208, 24, 3, 0, 2, (DexSizeRole)0, null),
                        M((ScreenMonKind)3, 48, 64, 3, 0, 3, (DexSizeRole)0, null),
                        M((ScreenMonKind)3, 88, 64, 3, 0, 4, (DexSizeRole)0, null),
                        M((ScreenMonKind)3, 128, 64, 3, 0, 5, (DexSizeRole)0, null),
                        M((ScreenMonKind)3, 168, 64, 3, 0, 6, (DexSizeRole)0, null),
                        M((ScreenMonKind)3, 208, 64, 3, 0, 7, (DexSizeRole)0, null),
                        M((ScreenMonKind)3, 48, 104, 3, 0, 8, (DexSizeRole)0, null),
                        M((ScreenMonKind)3, 88, 104, 3, 0, 9, (DexSizeRole)0, null),
                        M((ScreenMonKind)3, 128, 104, 3, 0, 10, (DexSizeRole)0, null),
                        M((ScreenMonKind)3, 168, 104, 3, 0, 11, (DexSizeRole)0, null),
                        M((ScreenMonKind)3, 208, 104, 3, 0, 12, (DexSizeRole)0, null),
                        M((ScreenMonKind)3, 48, 144, 3, 0, 13, (DexSizeRole)0, null),
                        M((ScreenMonKind)3, 88, 144, 3, 0, 14, (DexSizeRole)0, null),
                        M((ScreenMonKind)3, 128, 144, 3, 0, 15, (DexSizeRole)0, null),
                        M((ScreenMonKind)3, 168, 144, 3, 0, 16, (DexSizeRole)0, null),
                        M((ScreenMonKind)3, 208, 144, 3, 0, 17, (DexSizeRole)0, null),
                        M((ScreenMonKind)3, 48, 184, 3, 0, 18, (DexSizeRole)0, null),
                        M((ScreenMonKind)3, 88, 184, 3, 0, 19, (DexSizeRole)0, null),
                        M((ScreenMonKind)3, 128, 184, 3, 0, 20, (DexSizeRole)0, null),
                        M((ScreenMonKind)3, 168, 184, 3, 0, 21, (DexSizeRole)0, null),
                        M((ScreenMonKind)3, 208, 184, 3, 0, 22, (DexSizeRole)0, null),
                        M((ScreenMonKind)3, 48, 224, 3, 0, 23, (DexSizeRole)0, null),
                        M((ScreenMonKind)3, 88, 224, 3, 0, 24, (DexSizeRole)0, null),
                        M((ScreenMonKind)3, 128, 224, 3, 0, 25, (DexSizeRole)0, null),
                        M((ScreenMonKind)3, 168, 224, 3, 0, 26, (DexSizeRole)0, null),
                        M((ScreenMonKind)3, 208, 224, 3, 0, 27, (DexSizeRole)0, null),
                    },
                },
                Sub = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(0, 0, 4, 17, 64, 32, 256, 0, null, new List<ScreenPaste> { P(16, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                        B(1, 1, 4, -1, 32, 32, 0, 0, null, new List<ScreenPaste> {  }),
                        B(3, 3, 4, 19, 32, 32, 0, 0, null, new List<ScreenPaste> { P(20, 0, 0, 0, 0, -1, -1, -1, -1, "Entry"), P(21, 0, 0, 0, 0, -1, -1, -1, -1, "Foreign entry"), P(22, 0, 0, 0, 0, -1, -1, -1, -1, "No data") }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(18, 0, 0, 16, null),
                    },
                    Sprites = new List<ScreenSprite>
                    {
                        S("Caught mark", 30, 29, 31, 0, 0, 32, 0, 112, 32, 3, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "Entry|Foreign entry"),
                        S("First type", 33, 36, 34, 0, 0, 35, 0, 168, 72, 3, 1, 36, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "Entry"),
                        S("Second type", 33, 36, 34, 0, 0, 35, 0, 217, 72, 3, 2, 36, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "Entry"),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 8, 1, 16, 0, (ScreenTextAlign)0, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 2, 0, 8, 2, false, null, "Entry|Foreign entry"),
                        T((ScreenTextKind)2, -1, 1, 121, 24, (ScreenTextAlign)0, 0, 0, 2, 1, 4, 0, 0, -1, 0, -1, 15, 3, 4, 2, false, null, "Entry|Foreign entry"),
                        T((ScreenTextKind)1, -1, 1, 152, 24, (ScreenTextAlign)0, 0, 0, 2, 1, 4, 0, 0, -1, 0, -1, 19, 3, 9, 2, false, null, "Entry|Foreign entry"),
                        T((ScreenTextKind)3, -1, 1, 244, 40, (ScreenTextAlign)2, 0, 0, 2, 1, 4, 0, 0, -1, 0, -1, 13, 5, 18, 2, false, null, "Entry"),
                        T((ScreenTextKind)6, -1, 1, 128, 136, (ScreenTextAlign)3, 0, 1, 2, 1, 0, 0, 0, -1, 0, -1, 2, 17, 28, 6, false, null, "Entry"),
                        T((ScreenTextKind)0, 10, 1, 164, 88, (ScreenTextAlign)1, 0, 1, 2, 1, 0, 0, 0, -1, 0, -1, 18, 11, 5, 2, false, null, "Entry"),
                        T((ScreenTextKind)4, -1, 1, 188, 88, (ScreenTextAlign)0, 0, 1, 2, 1, 0, 0, 0, -1, 0, -1, 23, 11, 8, 2, false, null, "Entry"),
                        T((ScreenTextKind)0, 11, 1, 164, 104, (ScreenTextAlign)1, 0, 1, 2, 1, 0, 0, 0, -1, 0, -1, 18, 13, 5, 2, false, null, "Entry"),
                        T((ScreenTextKind)5, -1, 1, 188, 104, (ScreenTextAlign)0, 0, 1, 2, 1, 0, 0, 0, -1, 0, -1, 23, 13, 8, 2, false, null, "Entry"),
                        T((ScreenTextKind)0, 124, 1, 184, 64, (ScreenTextAlign)1, 0, 1, 2, 1, 0, 0, 0, -1, 0, -1, 16, 8, 14, 2, false, null, "Foreign entry"),
                        T((ScreenTextKind)0, 9, 1, 128, 88, (ScreenTextAlign)0, 0, 1, 2, 1, 0, 0, 0, -1, 0, -1, 16, 11, 3, 2, false, new[] { "213" }, "Foreign entry"),
                        T((ScreenTextKind)9, 0, 1, 160, 88, (ScreenTextAlign)0, 0, 1, 2, 1, 0, 0, 0, -1, 0, -1, 20, 11, 8, 2, false, null, "Foreign entry"),
                        T((ScreenTextKind)10, 0, 1, 244, 104, (ScreenTextAlign)2, 0, 1, 2, 1, 0, 0, 0, -1, 0, -1, 15, 13, 16, 2, false, null, "Foreign entry"),
                        T((ScreenTextKind)8, 0, 1, 128, 136, (ScreenTextAlign)3, 0, 1, 2, 1, 0, 0, 0, -1, 0, -1, 2, 17, 28, 6, false, null, "Foreign entry"),
                    },
                    Mons = new List<ScreenMon>
                    {
                        M((ScreenMonKind)0, 48, 72, 3, 0, 0, (DexSizeRole)0, "Entry|Foreign entry"),
                        M((ScreenMonKind)1, 120, 80, 3, 0, 0, (DexSizeRole)0, "Entry"),
                    },
                },
            },
            new ScreenPage
            {
                Id = "search_menu", Name = "Search", MainOnTop = false,
                Variants = new string[] {  },
                Main = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(0, 1, 4, -1, 32, 32, 0, 0, null, new List<ScreenPaste> {  }),
                        B(1, 0, 4, 4, 32, 32, 0, 0, null, new List<ScreenPaste> { P(69, 0, 20, 0, 0, -1, -1, -1, -1, null) }),
                        B(2, 2, 4, 58, 32, 32, 0, 0, null, new List<ScreenPaste> { P(57, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                        B(3, 3, 4, 3, 32, 32, 0, 0, null, new List<ScreenPaste> {  }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(2, 0, 0, 16, null),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 26, 0, 68, 8, (ScreenTextAlign)1, 0, 2, 2, 1, 0, 0, 0, -1, 0, -1, 4, 1, 9, 2, false, null, null),
                        T((ScreenTextKind)0, 27, 0, 28, 32, (ScreenTextAlign)1, 0, 2, 2, 1, 0, 0, 0, -1, 0, -1, 1, 4, 5, 2, false, null, null),
                        T((ScreenTextKind)0, 28, 0, 28, 56, (ScreenTextAlign)1, 0, 2, 2, 1, 0, 0, 0, -1, 0, -1, 1, 7, 5, 2, false, null, null),
                        T((ScreenTextKind)0, 29, 0, 28, 80, (ScreenTextAlign)1, 0, 2, 2, 1, 0, 0, 0, -1, 0, -1, 1, 10, 5, 2, false, null, null),
                        T((ScreenTextKind)0, 30, 0, 28, 104, (ScreenTextAlign)1, 0, 2, 2, 1, 0, 0, 0, -1, 0, -1, 1, 13, 5, 2, false, null, null),
                        T((ScreenTextKind)0, 31, 0, 28, 128, (ScreenTextAlign)1, 0, 2, 2, 1, 0, 0, 0, -1, 0, -1, 1, 16, 5, 2, false, null, null),
                        T((ScreenTextKind)0, 32, 0, 224, 32, (ScreenTextAlign)1, 0, 2, 2, 1, 0, 0, 0, -1, 0, -1, 25, 4, 6, 2, false, null, null),
                        T((ScreenTextKind)0, 35, 1, 40, 172, (ScreenTextAlign)1, 0, 4, 2, 1, 4, 0, 0, -1, 0, -1, 1, 21, 8, 2, true, null, null),
                        T((ScreenTextKind)0, 36, 1, 128, 172, (ScreenTextAlign)1, 0, 4, 2, 1, 4, 0, 0, -1, 0, -1, 12, 21, 8, 2, true, null, null),
                        T((ScreenTextKind)0, 37, 1, 216, 172, (ScreenTextAlign)1, 0, 4, 2, 1, 4, 0, 0, -1, 0, -1, 23, 21, 8, 2, true, null, null),
                        T((ScreenTextKind)0, 41, 0, 172, 8, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 16, 1, 11, 2, false, null, null),
                        T((ScreenTextKind)0, 113, 0, 88, 32, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 7, 4, 9, 2, false, null, null),
                        T((ScreenTextKind)0, 64, 0, 85, 56, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 7, 7, 8, 2, false, null, null),
                        T((ScreenTextKind)0, 64, 0, 149, 56, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 15, 7, 8, 2, false, null, null),
                        T((ScreenTextKind)0, 68, 0, 84, 128, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 7, 16, 7, 2, false, null, null),
                        T((ScreenTextKind)12, -1, 0, 80, 80, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 7, 10, 6, 2, false, null, null),
                        T((ScreenTextKind)12, -1, 0, 144, 80, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 152, 0, -1, 0, -1, 15, 10, 6, 2, false, null, null),
                        T((ScreenTextKind)13, -1, 0, 92, 104, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 7, 13, 9, 2, false, null, null),
                        T((ScreenTextKind)13, -1, 0, 180, 104, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 152, 0, -1, 0, -1, 18, 13, 9, 2, false, null, null),
                    },
                },
                Sub = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(1, 1, 4, -1, 32, 32, 0, 0, null, new List<ScreenPaste> {  }),
                        B(2, 2, 4, 58, 32, 32, 0, 0, null, new List<ScreenPaste> { P(60, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                        B(3, 3, 4, 58, 32, 32, 0, 0, null, new List<ScreenPaste> { P(59, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(18, 0, 0, 16, null),
                    },
                    Sprites = new List<ScreenSprite>
                    {
                        S("Search emblem", 24, 23, 25, 0, 0, 32, 0, 128, 80, 3, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 12, 1, 128, 158, (ScreenTextAlign)1, 0, 2, 2, 1, 0, 0, 0, -1, 0, -1, 2, 19, 28, 5, false, null, null),
                        T((ScreenTextKind)0, 13, 1, 128, 174, (ScreenTextAlign)1, 0, 2, 2, 1, 0, 0, 0, -1, 0, -1, 2, 19, 28, 5, false, null, null),
                    },
                },
            },
            new ScreenPage
            {
                Id = "search_order", Name = "Search: order", MainOnTop = false,
                Variants = new string[] {  },
                Main = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(0, 1, 4, -1, 32, 32, 0, 0, null, new List<ScreenPaste> {  }),
                        B(1, 0, 4, 4, 32, 32, 0, 0, null, new List<ScreenPaste> { P(70, 0, 20, 0, 0, -1, -1, -1, -1, null) }),
                        B(2, 2, 4, 58, 32, 32, 0, 0, null, new List<ScreenPaste> { P(62, 0, 0, 0, 0, -1, -1, -1, -1, null), P(62, 3, 6, 3, 6, 11, 2, 3, -1, null) }),
                        B(3, 3, 4, 3, 32, 32, 0, 0, null, new List<ScreenPaste> {  }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(2, 0, 0, 16, null),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 26, 0, 68, 8, (ScreenTextAlign)1, 0, 2, 2, 1, 0, 0, 0, -1, 0, -1, 3, 1, 11, 2, false, null, null),
                        T((ScreenTextKind)0, 41, 0, 188, 8, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 18, 1, 11, 2, false, null, null),
                        T((ScreenTextKind)0, 41, 0, 68, 48, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 3, 6, 11, 2, false, null, null),
                        T((ScreenTextKind)0, 42, 0, 188, 48, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 18, 6, 11, 2, false, null, null),
                        T((ScreenTextKind)0, 43, 0, 68, 80, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 3, 10, 11, 2, false, null, null),
                        T((ScreenTextKind)0, 44, 0, 188, 80, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 18, 10, 11, 2, false, null, null),
                        T((ScreenTextKind)0, 45, 0, 68, 112, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 3, 14, 11, 2, false, null, null),
                        T((ScreenTextKind)0, 46, 0, 188, 112, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 18, 14, 11, 2, false, null, null),
                        T((ScreenTextKind)0, 39, 1, 40, 172, (ScreenTextAlign)1, 0, 4, 2, 1, 4, 0, 0, -1, 0, -1, 1, 21, 8, 2, true, null, null),
                        T((ScreenTextKind)0, 40, 1, 216, 172, (ScreenTextAlign)1, 0, 4, 2, 1, 4, 0, 0, -1, 0, -1, 23, 21, 8, 2, true, null, null),
                    },
                },
                Sub = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(1, 1, 4, -1, 32, 32, 0, 0, null, new List<ScreenPaste> {  }),
                        B(2, 2, 4, 58, 32, 32, 0, 0, null, new List<ScreenPaste> { P(60, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                        B(3, 3, 4, 58, 32, 32, 0, 0, null, new List<ScreenPaste> { P(59, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(18, 0, 0, 16, null),
                    },
                    Sprites = new List<ScreenSprite>
                    {
                        S("Search emblem", 24, 23, 25, 0, 0, 32, 0, 128, 80, 3, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 12, 1, 128, 158, (ScreenTextAlign)1, 0, 2, 2, 1, 0, 0, 0, -1, 0, -1, 2, 19, 28, 5, false, null, null),
                        T((ScreenTextKind)0, 16, 1, 128, 174, (ScreenTextAlign)1, 0, 2, 2, 1, 0, 0, 0, -1, 0, -1, 2, 19, 28, 5, false, null, null),
                    },
                },
            },
            new ScreenPage
            {
                Id = "search_name", Name = "Search: name", MainOnTop = false,
                Variants = new string[] {  },
                Main = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(0, 1, 4, -1, 32, 32, 0, 0, null, new List<ScreenPaste> {  }),
                        B(1, 0, 4, 4, 32, 32, 0, 0, null, new List<ScreenPaste> { P(70, 0, 20, 0, 0, -1, -1, -1, -1, null) }),
                        B(2, 2, 4, 58, 32, 32, 0, 0, null, new List<ScreenPaste> { P(63, 0, 0, 0, 0, -1, -1, -1, -1, null), P(63, 27, 17, 27, 17, 2, 2, 3, -1, null) }),
                        B(3, 3, 4, 3, 32, 32, 0, 0, null, new List<ScreenPaste> {  }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(2, 0, 0, 16, null),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 27, 0, 80, 8, (ScreenTextAlign)1, 0, 2, 2, 1, 0, 0, 0, -1, 0, -1, 7, 1, 6, 2, false, null, null),
                        T((ScreenTextKind)0, 113, 0, 156, 8, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 15, 1, 9, 2, false, null, null),
                        T((ScreenTextKind)0, 69, 0, 32, 40, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 1, 5, 29, 14, false, null, null),
                        T((ScreenTextKind)0, 70, 0, 64, 40, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 71, 0, 96, 40, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 72, 0, 128, 40, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 73, 0, 160, 40, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 74, 0, 192, 40, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 75, 0, 224, 40, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 76, 0, 32, 72, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 77, 0, 64, 72, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 78, 0, 96, 72, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 79, 0, 128, 72, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 80, 0, 160, 72, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 81, 0, 192, 72, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 82, 0, 224, 72, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 83, 0, 32, 104, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 84, 0, 64, 104, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 85, 0, 96, 104, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 86, 0, 128, 104, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 87, 0, 160, 104, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 88, 0, 192, 104, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 89, 0, 224, 104, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 90, 0, 32, 136, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 91, 0, 64, 136, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 92, 0, 96, 136, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 93, 0, 128, 136, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 94, 0, 160, 136, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 113, 0, 224, 136, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 0, 0, 0, 0, false, null, null),
                        T((ScreenTextKind)0, 39, 1, 40, 172, (ScreenTextAlign)1, 0, 4, 2, 1, 4, 0, 0, -1, 0, -1, 1, 21, 8, 2, true, null, null),
                        T((ScreenTextKind)0, 40, 1, 216, 172, (ScreenTextAlign)1, 0, 4, 2, 1, 4, 0, 0, -1, 0, -1, 23, 21, 8, 2, true, null, null),
                    },
                },
                Sub = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(1, 1, 4, -1, 32, 32, 0, 0, null, new List<ScreenPaste> {  }),
                        B(2, 2, 4, 58, 32, 32, 0, 0, null, new List<ScreenPaste> { P(60, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                        B(3, 3, 4, 58, 32, 32, 0, 0, null, new List<ScreenPaste> { P(59, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(18, 0, 0, 16, null),
                    },
                    Sprites = new List<ScreenSprite>
                    {
                        S("Search emblem", 24, 23, 25, 0, 0, 32, 0, 128, 80, 3, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 12, 1, 128, 158, (ScreenTextAlign)1, 0, 2, 2, 1, 0, 0, 0, -1, 0, -1, 2, 19, 28, 5, false, null, null),
                        T((ScreenTextKind)0, 17, 1, 128, 174, (ScreenTextAlign)1, 0, 2, 2, 1, 0, 0, 0, -1, 0, -1, 2, 19, 28, 5, false, null, null),
                    },
                },
            },
            new ScreenPage
            {
                Id = "search_type", Name = "Search: type", MainOnTop = false,
                Variants = new string[] {  },
                Main = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(0, 1, 4, -1, 32, 32, 0, 0, null, new List<ScreenPaste> {  }),
                        B(1, 0, 4, 4, 32, 32, 0, 0, null, new List<ScreenPaste> { P(70, 0, 20, 0, 0, -1, -1, -1, -1, null) }),
                        B(2, 2, 4, 58, 32, 32, 0, 0, null, new List<ScreenPaste> { P(64, 0, 0, 0, 0, -1, -1, -1, -1, null), P(64, 8, 17, 8, 17, 8, 2, 3, -1, null) }),
                        B(3, 3, 4, 3, 32, 32, 0, 0, null, new List<ScreenPaste> {  }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(2, 0, 0, 16, null),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 28, 0, 52, 8, (ScreenTextAlign)1, 0, 2, 2, 1, 0, 0, 0, -1, 0, -1, 3, 1, 7, 2, false, null, null),
                        T((ScreenTextKind)0, 64, 0, 133, 8, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 13, 1, 8, 2, false, null, null),
                        T((ScreenTextKind)0, 64, 0, 197, 8, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 21, 1, 8, 2, false, null, null),
                        T((ScreenTextKind)0, 58, 0, 32, 40, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 0, 5, 8, 2, false, null, null),
                        T((ScreenTextKind)0, 50, 0, 96, 40, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 8, 5, 8, 2, false, null, null),
                        T((ScreenTextKind)0, 60, 0, 160, 40, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 16, 5, 8, 2, false, null, null),
                        T((ScreenTextKind)0, 56, 0, 224, 40, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 24, 5, 8, 2, false, null, null),
                        T((ScreenTextKind)0, 54, 0, 32, 64, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 0, 8, 8, 2, false, null, null),
                        T((ScreenTextKind)0, 48, 0, 96, 64, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 8, 8, 8, 2, false, null, null),
                        T((ScreenTextKind)0, 63, 0, 160, 64, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 16, 8, 8, 2, false, null, null),
                        T((ScreenTextKind)0, 53, 0, 224, 64, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 24, 8, 8, 2, false, null, null),
                        T((ScreenTextKind)0, 59, 0, 32, 88, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 0, 11, 8, 2, false, null, null),
                        T((ScreenTextKind)0, 61, 0, 96, 88, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 8, 11, 8, 2, false, null, null),
                        T((ScreenTextKind)0, 62, 0, 160, 88, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 16, 11, 8, 2, false, null, null),
                        T((ScreenTextKind)0, 51, 0, 224, 88, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 24, 11, 8, 2, false, null, null),
                        T((ScreenTextKind)0, 55, 0, 32, 112, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 0, 14, 8, 2, false, null, null),
                        T((ScreenTextKind)0, 49, 0, 96, 112, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 8, 14, 8, 2, false, null, null),
                        T((ScreenTextKind)0, 52, 0, 160, 112, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 16, 14, 8, 2, false, null, null),
                        T((ScreenTextKind)0, 57, 0, 224, 112, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 24, 14, 8, 2, false, null, null),
                        T((ScreenTextKind)0, 47, 0, 32, 136, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 0, 17, 8, 2, false, null, null),
                        T((ScreenTextKind)0, 64, 0, 96, 136, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 8, 17, 8, 2, false, null, null),
                        T((ScreenTextKind)0, 39, 1, 40, 172, (ScreenTextAlign)1, 0, 4, 2, 1, 4, 0, 0, -1, 0, -1, 1, 21, 8, 2, true, null, null),
                        T((ScreenTextKind)0, 40, 1, 216, 172, (ScreenTextAlign)1, 0, 4, 2, 1, 4, 0, 0, -1, 0, -1, 23, 21, 8, 2, true, null, null),
                    },
                },
                Sub = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(1, 1, 4, -1, 32, 32, 0, 0, null, new List<ScreenPaste> {  }),
                        B(2, 2, 4, 58, 32, 32, 0, 0, null, new List<ScreenPaste> { P(60, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                        B(3, 3, 4, 58, 32, 32, 0, 0, null, new List<ScreenPaste> { P(59, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(18, 0, 0, 16, null),
                    },
                    Sprites = new List<ScreenSprite>
                    {
                        S("Search emblem", 24, 23, 25, 0, 0, 32, 0, 128, 80, 3, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 12, 1, 128, 158, (ScreenTextAlign)1, 0, 2, 2, 1, 0, 0, 0, -1, 0, -1, 2, 19, 28, 5, false, null, null),
                        T((ScreenTextKind)0, 18, 1, 128, 174, (ScreenTextAlign)1, 0, 2, 2, 1, 0, 0, 0, -1, 0, -1, 2, 19, 28, 5, false, null, null),
                    },
                },
            },
            new ScreenPage
            {
                Id = "search_height", Name = "Search: height", MainOnTop = false,
                Variants = new string[] {  },
                Main = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(0, 1, 4, -1, 32, 32, 0, 0, null, new List<ScreenPaste> {  }),
                        B(1, 0, 4, 4, 32, 32, 0, 0, null, new List<ScreenPaste> { P(70, 0, 20, 0, 0, -1, -1, -1, -1, null) }),
                        B(2, 2, 4, 58, 32, 32, 0, 0, null, new List<ScreenPaste> { P(65, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                        B(3, 3, 4, 3, 32, 32, 0, 0, null, new List<ScreenPaste> {  }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(2, 0, 0, 16, null),
                    },
                    Sprites = new List<ScreenSprite>
                    {
                        S("Highest digit", 13, 12, 14, 0, 0, 15, 0, 204, 64, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)1, 152, 0, null),
                        S("Highest digit", 13, 12, 14, 0, 0, 15, 0, 204, 64, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)1, 152, 1, null),
                        S("Highest digit", 13, 12, 14, 0, 0, 15, 0, 204, 64, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)1, 152, 2, null),
                        S("Highest digit", 13, 12, 14, 0, 0, 15, 0, 204, 64, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)1, 152, 3, null),
                        S("Highest digit", 13, 12, 14, 0, 0, 15, 0, 204, 64, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)1, 152, 4, null),
                        S("Lowest digit", 13, 12, 14, 0, 0, 15, 0, 52, 136, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)1, 0, 0, null),
                        S("Lowest digit", 13, 12, 14, 0, 0, 15, 0, 52, 136, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)1, 0, 1, null),
                        S("Lowest digit", 13, 12, 14, 0, 0, 15, 0, 52, 136, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)1, 0, 2, null),
                        S("Lowest digit", 13, 12, 14, 0, 0, 15, 0, 52, 136, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)1, 0, 3, null),
                        S("Lowest digit", 13, 12, 14, 0, 0, 15, 0, 52, 136, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)1, 0, 4, null),
                        S("Arrow", 13, 12, 14, 56, 0, 15, 1, 18, 60, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Arrow", 13, 12, 14, 55, 0, 15, 1, 238, 60, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Arrow", 13, 12, 14, 58, 0, 15, 1, 18, 132, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Arrow", 13, 12, 14, 53, 0, 15, 1, 238, 132, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Highest", 13, 12, 14, 67, 0, 15, 1, 204, 68, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Lowest", 13, 12, 14, 68, 0, 15, 1, 52, 132, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 29, 0, 44, 8, (ScreenTextAlign)1, 0, 2, 2, 1, 0, 0, 0, -1, 0, -1, 3, 1, 5, 2, false, null, null),
                        T((ScreenTextKind)12, -1, 0, 108, 8, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 10, 1, 7, 2, false, null, null),
                        T((ScreenTextKind)12, -1, 0, 180, 8, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 152, 0, -1, 0, -1, 19, 1, 7, 2, false, null, null),
                        T((ScreenTextKind)0, 39, 1, 40, 172, (ScreenTextAlign)1, 0, 4, 2, 1, 4, 0, 0, -1, 0, -1, 1, 21, 8, 2, true, null, null),
                        T((ScreenTextKind)0, 40, 1, 216, 172, (ScreenTextAlign)1, 0, 4, 2, 1, 4, 0, 0, -1, 0, -1, 23, 21, 8, 2, true, null, null),
                    },
                },
                Sub = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(1, 1, 4, -1, 32, 32, 0, 0, null, new List<ScreenPaste> {  }),
                        B(2, 2, 4, 58, 32, 32, 0, 0, null, new List<ScreenPaste> { P(60, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                        B(3, 3, 4, 58, 32, 32, 0, 0, null, new List<ScreenPaste> { P(59, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(18, 0, 0, 16, null),
                    },
                    Sprites = new List<ScreenSprite>
                    {
                        S("Search emblem", 24, 23, 25, 0, 0, 32, 0, 128, 80, 3, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 12, 1, 128, 158, (ScreenTextAlign)1, 0, 2, 2, 1, 0, 0, 0, -1, 0, -1, 2, 19, 28, 5, false, null, null),
                        T((ScreenTextKind)0, 20, 1, 128, 174, (ScreenTextAlign)1, 0, 2, 2, 1, 0, 0, 0, -1, 0, -1, 2, 19, 28, 5, false, null, null),
                    },
                },
            },
            new ScreenPage
            {
                Id = "search_weight", Name = "Search: weight", MainOnTop = false,
                Variants = new string[] {  },
                Main = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(0, 1, 4, -1, 32, 32, 0, 0, null, new List<ScreenPaste> {  }),
                        B(1, 0, 4, 4, 32, 32, 0, 0, null, new List<ScreenPaste> { P(70, 0, 20, 0, 0, -1, -1, -1, -1, null) }),
                        B(2, 2, 4, 58, 32, 32, 0, 0, null, new List<ScreenPaste> { P(66, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                        B(3, 3, 4, 3, 32, 32, 0, 0, null, new List<ScreenPaste> {  }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(2, 0, 0, 16, null),
                    },
                    Sprites = new List<ScreenSprite>
                    {
                        S("Highest digit", 13, 12, 14, 0, 0, 15, 0, 204, 64, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)2, 152, 0, null),
                        S("Highest digit", 13, 12, 14, 0, 0, 15, 0, 204, 64, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)2, 152, 1, null),
                        S("Highest digit", 13, 12, 14, 0, 0, 15, 0, 204, 64, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)2, 152, 2, null),
                        S("Highest digit", 13, 12, 14, 0, 0, 15, 0, 204, 64, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)2, 152, 3, null),
                        S("Highest digit", 13, 12, 14, 0, 0, 15, 0, 204, 64, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)2, 152, 4, null),
                        S("Lowest digit", 13, 12, 14, 0, 0, 15, 0, 52, 136, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)2, 0, 0, null),
                        S("Lowest digit", 13, 12, 14, 0, 0, 15, 0, 52, 136, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)2, 0, 1, null),
                        S("Lowest digit", 13, 12, 14, 0, 0, 15, 0, 52, 136, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)2, 0, 2, null),
                        S("Lowest digit", 13, 12, 14, 0, 0, 15, 0, 52, 136, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)2, 0, 3, null),
                        S("Lowest digit", 13, 12, 14, 0, 0, 15, 0, 52, 136, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)2, 0, 4, null),
                        S("Arrow", 13, 12, 14, 56, 0, 15, 1, 18, 60, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Arrow", 13, 12, 14, 55, 0, 15, 1, 238, 60, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Arrow", 13, 12, 14, 58, 0, 15, 1, 18, 132, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Arrow", 13, 12, 14, 53, 0, 15, 1, 238, 132, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Highest", 13, 12, 14, 41, 0, 15, 1, 204, 68, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Lowest", 13, 12, 14, 42, 0, 15, 1, 52, 132, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 30, 0, 32, 8, (ScreenTextAlign)1, 0, 2, 2, 1, 0, 0, 0, -1, 0, -1, 1, 1, 6, 2, false, null, null),
                        T((ScreenTextKind)13, -1, 0, 100, 8, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 8, 1, 9, 2, false, null, null),
                        T((ScreenTextKind)13, -1, 0, 188, 8, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 152, 0, -1, 0, -1, 19, 1, 9, 2, false, null, null),
                        T((ScreenTextKind)0, 39, 1, 40, 172, (ScreenTextAlign)1, 0, 4, 2, 1, 4, 0, 0, -1, 0, -1, 1, 21, 8, 2, true, null, null),
                        T((ScreenTextKind)0, 40, 1, 216, 172, (ScreenTextAlign)1, 0, 4, 2, 1, 4, 0, 0, -1, 0, -1, 23, 21, 8, 2, true, null, null),
                    },
                },
                Sub = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(1, 1, 4, -1, 32, 32, 0, 0, null, new List<ScreenPaste> {  }),
                        B(2, 2, 4, 58, 32, 32, 0, 0, null, new List<ScreenPaste> { P(60, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                        B(3, 3, 4, 58, 32, 32, 0, 0, null, new List<ScreenPaste> { P(59, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(18, 0, 0, 16, null),
                    },
                    Sprites = new List<ScreenSprite>
                    {
                        S("Search emblem", 24, 23, 25, 0, 0, 32, 0, 128, 80, 3, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 12, 1, 128, 158, (ScreenTextAlign)1, 0, 2, 2, 1, 0, 0, 0, -1, 0, -1, 2, 19, 28, 5, false, null, null),
                        T((ScreenTextKind)0, 19, 1, 128, 174, (ScreenTextAlign)1, 0, 2, 2, 1, 0, 0, 0, -1, 0, -1, 2, 19, 28, 5, false, null, null),
                    },
                },
            },
            new ScreenPage
            {
                Id = "search_area", Name = "Search: area", MainOnTop = false,
                Variants = new string[] { "National Dex", "No National Dex" },
                Main = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(0, 1, 4, -1, 32, 32, 0, 0, null, new List<ScreenPaste> {  }),
                        B(1, 0, 4, 4, 32, 32, 0, 0, null, new List<ScreenPaste> { P(70, 0, 20, 0, 0, -1, -1, -1, -1, null) }),
                        B(2, 2, 4, 58, 32, 32, 0, 0, null, new List<ScreenPaste> { P(67, 0, 0, 0, 0, -1, -1, -1, -1, null), P(67, 19, 15, 19, 15, 7, 2, 3, -1, null) }),
                        B(3, 3, 4, 3, 32, 32, 0, 0, null, new List<ScreenPaste> {  }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(2, 0, 0, 16, null),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 31, 0, 80, 8, (ScreenTextAlign)1, 0, 2, 2, 1, 0, 0, 0, -1, 0, -1, 7, 1, 6, 2, false, null, null),
                        T((ScreenTextKind)0, 68, 0, 156, 8, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 16, 1, 7, 2, false, null, null),
                        T((ScreenTextKind)0, 65, 0, 76, 64, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 6, 8, 7, 2, false, null, null),
                        T((ScreenTextKind)0, 66, 0, 180, 64, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 19, 8, 7, 2, false, null, "National Dex"),
                        T((ScreenTextKind)0, 67, 0, 76, 120, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 6, 15, 7, 2, false, null, null),
                        T((ScreenTextKind)0, 68, 0, 180, 120, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 19, 15, 7, 2, false, null, null),
                        T((ScreenTextKind)0, 39, 1, 40, 172, (ScreenTextAlign)1, 0, 4, 2, 1, 4, 0, 0, -1, 0, -1, 1, 21, 8, 2, true, null, null),
                        T((ScreenTextKind)0, 40, 1, 216, 172, (ScreenTextAlign)1, 0, 4, 2, 1, 4, 0, 0, -1, 0, -1, 23, 21, 8, 2, true, null, null),
                    },
                },
                Sub = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(1, 1, 4, -1, 32, 32, 0, 0, null, new List<ScreenPaste> {  }),
                        B(2, 2, 4, 58, 32, 32, 0, 0, null, new List<ScreenPaste> { P(60, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                        B(3, 3, 4, 58, 32, 32, 0, 0, null, new List<ScreenPaste> { P(59, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(18, 0, 0, 16, null),
                    },
                    Sprites = new List<ScreenSprite>
                    {
                        S("Search emblem", 24, 23, 25, 0, 0, 32, 0, 128, 80, 3, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 12, 1, 128, 158, (ScreenTextAlign)1, 0, 2, 2, 1, 0, 0, 0, -1, 0, -1, 2, 19, 28, 5, false, null, null),
                        T((ScreenTextKind)0, 22, 1, 128, 174, (ScreenTextAlign)1, 0, 2, 2, 1, 0, 0, 0, -1, 0, -1, 2, 19, 28, 5, false, null, null),
                    },
                },
            },
            new ScreenPage
            {
                Id = "search_bodyshape", Name = "Search: form", MainOnTop = false,
                Variants = new string[] {  },
                Main = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(0, 1, 4, -1, 32, 32, 0, 0, null, new List<ScreenPaste> {  }),
                        B(1, 0, 4, 4, 32, 32, 0, 0, null, new List<ScreenPaste> { P(70, 0, 20, 0, 0, -1, -1, -1, -1, null) }),
                        B(2, 2, 4, 58, 32, 32, 0, 0, null, new List<ScreenPaste> { P(68, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                        B(3, 3, 4, 3, 32, 32, 0, 0, null, new List<ScreenPaste> {  }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(2, 0, 0, 16, null),
                    },
                    Sprites = new List<ScreenSprite>
                    {
                        S("Body shape", 73, 72, 74, 0, 0, 75, 0, 32, 56, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Body shape", 73, 72, 74, 1, 0, 75, 0, 80, 56, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Body shape", 73, 72, 74, 2, 0, 75, 0, 128, 56, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Body shape", 73, 72, 74, 3, 0, 75, 0, 176, 56, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Body shape", 73, 72, 74, 4, 0, 75, 0, 224, 56, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Body shape", 73, 72, 74, 5, 0, 75, 0, 32, 96, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Body shape", 73, 72, 74, 6, 0, 75, 0, 80, 96, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Body shape", 73, 72, 74, 7, 0, 75, 0, 128, 96, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Body shape", 73, 72, 74, 8, 0, 75, 0, 176, 96, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Body shape", 73, 72, 74, 9, 0, 75, 0, 224, 96, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Body shape", 73, 72, 74, 10, 0, 75, 0, 32, 136, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Body shape", 73, 72, 74, 11, 0, 75, 0, 80, 136, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Body shape", 73, 72, 74, 12, 0, 75, 0, 128, 136, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Body shape", 73, 72, 74, 13, 0, 75, 0, 176, 136, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 32, 0, 88, 8, (ScreenTextAlign)1, 0, 2, 2, 1, 0, 0, 0, -1, 0, -1, 8, 1, 6, 3, false, null, null),
                        T((ScreenTextKind)0, 39, 1, 40, 172, (ScreenTextAlign)1, 0, 4, 2, 1, 4, 0, 0, -1, 0, -1, 1, 21, 8, 2, true, null, null),
                        T((ScreenTextKind)0, 40, 1, 216, 172, (ScreenTextAlign)1, 0, 4, 2, 1, 4, 0, 0, -1, 0, -1, 23, 21, 8, 2, true, null, null),
                    },
                },
                Sub = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(1, 1, 4, -1, 32, 32, 0, 0, null, new List<ScreenPaste> {  }),
                        B(2, 2, 4, 58, 32, 32, 0, 0, null, new List<ScreenPaste> { P(60, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                        B(3, 3, 4, 58, 32, 32, 0, 0, null, new List<ScreenPaste> { P(59, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(18, 0, 0, 16, null),
                    },
                    Sprites = new List<ScreenSprite>
                    {
                        S("Search emblem", 24, 23, 25, 0, 0, 32, 0, 128, 80, 3, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 12, 1, 128, 158, (ScreenTextAlign)1, 0, 2, 2, 1, 0, 0, 0, -1, 0, -1, 2, 19, 28, 5, false, null, null),
                        T((ScreenTextKind)0, 21, 1, 128, 174, (ScreenTextAlign)1, 0, 2, 2, 1, 0, 0, 0, -1, 0, -1, 2, 19, 28, 5, false, null, null),
                    },
                },
            },
            new ScreenPage
            {
                Id = "search_running", Name = "Searching", MainOnTop = false,
                Variants = new string[] { "Searching", "Not found" },
                Main = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(0, 1, 4, -1, 32, 32, 0, 0, null, new List<ScreenPaste> {  }),
                        B(1, 0, 4, 4, 32, 32, 0, 0, null, new List<ScreenPaste> { P(69, 0, 20, 0, 0, -1, -1, -1, -1, null) }),
                        B(2, 2, 4, 58, 32, 32, 0, 0, null, new List<ScreenPaste> { P(57, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                        B(3, 3, 4, 3, 32, 32, 0, 0, null, new List<ScreenPaste> {  }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(2, 0, 0, 16, null),
                    },
                },
                Sub = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(1, 1, 4, -1, 32, 32, 0, 0, null, new List<ScreenPaste> {  }),
                        B(2, 2, 4, 58, 32, 32, 0, 0, null, new List<ScreenPaste> { P(60, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                        B(3, 3, 4, 58, 32, 32, 0, 0, null, new List<ScreenPaste> { P(59, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(18, 0, 0, 16, null),
                    },
                    Sprites = new List<ScreenSprite>
                    {
                        S("Search emblem", 24, 23, 25, 2, 0, 32, 0, 128, 80, 3, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 12, 1, 128, 158, (ScreenTextAlign)1, 0, 2, 2, 1, 0, 0, 0, -1, 0, -1, 2, 19, 28, 5, false, null, "Searching"),
                        T((ScreenTextKind)0, 25, 1, 128, 174, (ScreenTextAlign)1, 0, 2, 2, 1, 0, 0, 0, -1, 0, -1, 2, 19, 28, 5, false, null, "Searching"),
                        T((ScreenTextKind)0, 23, 1, 128, 158, (ScreenTextAlign)1, 0, 2, 2, 1, 0, 0, 0, -1, 0, -1, 2, 19, 28, 5, false, null, "Not found"),
                        T((ScreenTextKind)0, 24, 1, 128, 174, (ScreenTextAlign)1, 0, 2, 2, 1, 0, 0, 0, -1, 0, -1, 2, 19, 28, 5, false, null, "Not found"),
                    },
                },
            },
            new ScreenPage
            {
                Id = "search_results", Name = "Search results", MainOnTop = false,
                Variants = new string[] { "Criteria", "Entry" },
                Main = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(1, 0, 4, -1, 32, 32, 0, 0, null, new List<ScreenPaste> {  }),
                        B(2, 1, 4, 4, 32, 32, 0, 0, null, new List<ScreenPaste> { P(71, 0, 19, 0, 0, -1, -1, -1, -1, null), P(9, 0, 15, 0, 0, -1, -1, -1, -1, null) }),
                        B(3, 3, 4, 3, 32, 32, 0, 0, null, new List<ScreenPaste> { P(10, 0, 0, 0, 0, -1, -1, -1, -1, null), P(10, 0, 5, 0, 0, -1, -1, -1, -1, null), P(10, 0, 10, 0, 0, -1, -1, -1, -1, null), P(10, 0, 15, 0, 0, -1, -1, -1, -1, null), P(10, 0, 20, 0, 0, -1, -1, -1, -1, null), P(-1, 5, 1, 0, 0, 1, 1, -1, 4101, null), P(-1, 6, 1, 0, 0, 1, 1, -1, 4100, null), P(-1, 7, 1, 0, 0, 1, 1, -1, 4100, null), P(-1, 4, 1, 0, 0, 1, 1, -1, 4098, null), P(-1, 10, 1, 0, 0, 1, 1, -1, 4101, null), P(-1, 11, 1, 0, 0, 1, 1, -1, 4100, null), P(-1, 12, 1, 0, 0, 1, 1, -1, 4101, null), P(-1, 9, 1, 0, 0, 1, 1, -1, 4098, null), P(-1, 15, 1, 0, 0, 1, 1, -1, 4101, null), P(-1, 16, 1, 0, 0, 1, 1, -1, 4100, null), P(-1, 17, 1, 0, 0, 1, 1, -1, 4102, null), P(-1, 14, 1, 0, 0, 1, 1, -1, 4098, null), P(-1, 20, 1, 0, 0, 1, 1, -1, 4101, null), P(-1, 21, 1, 0, 0, 1, 1, -1, 4100, null), P(-1, 22, 1, 0, 0, 1, 1, -1, 4103, null), P(-1, 19, 1, 0, 0, 1, 1, -1, 4098, null), P(-1, 25, 1, 0, 0, 1, 1, -1, 4101, null), P(-1, 26, 1, 0, 0, 1, 1, -1, 4100, null), P(-1, 27, 1, 0, 0, 1, 1, -1, 4104, null), P(-1, 24, 1, 0, 0, 1, 1, -1, 4098, null), P(-1, 5, 6, 0, 0, 1, 1, -1, 4101, null), P(-1, 6, 6, 0, 0, 1, 1, -1, 4100, null), P(-1, 7, 6, 0, 0, 1, 1, -1, 4105, null), P(-1, 4, 6, 0, 0, 1, 1, -1, 4098, null), P(-1, 10, 6, 0, 0, 1, 1, -1, 4101, null), P(-1, 11, 6, 0, 0, 1, 1, -1, 4100, null), P(-1, 12, 6, 0, 0, 1, 1, -1, 4106, null), P(-1, 9, 6, 0, 0, 1, 1, -1, 4098, null), P(-1, 15, 6, 0, 0, 1, 1, -1, 4101, null), P(-1, 16, 6, 0, 0, 1, 1, -1, 4100, null), P(-1, 17, 6, 0, 0, 1, 1, -1, 4107, null), P(-1, 14, 6, 0, 0, 1, 1, -1, 4098, null), P(-1, 20, 6, 0, 0, 1, 1, -1, 4101, null), P(-1, 21, 6, 0, 0, 1, 1, -1, 4100, null), P(-1, 22, 6, 0, 0, 1, 1, -1, 4108, null), P(-1, 19, 6, 0, 0, 1, 1, -1, 4098, null), P(-1, 25, 6, 0, 0, 1, 1, -1, 4101, null), P(-1, 26, 6, 0, 0, 1, 1, -1, 4101, null), P(-1, 27, 6, 0, 0, 1, 1, -1, 4099, null), P(-1, 24, 6, 0, 0, 1, 1, -1, 4098, null), P(-1, 5, 11, 0, 0, 1, 1, -1, 4101, null), P(-1, 6, 11, 0, 0, 1, 1, -1, 4101, null), P(-1, 7, 11, 0, 0, 1, 1, -1, 4100, null), P(-1, 4, 11, 0, 0, 1, 1, -1, 4098, null), P(-1, 10, 11, 0, 0, 1, 1, -1, 4101, null), P(-1, 11, 11, 0, 0, 1, 1, -1, 4101, null), P(-1, 12, 11, 0, 0, 1, 1, -1, 4101, null), P(-1, 9, 11, 0, 0, 1, 1, -1, 4098, null), P(-1, 15, 11, 0, 0, 1, 1, -1, 4101, null), P(-1, 16, 11, 0, 0, 1, 1, -1, 4101, null), P(-1, 17, 11, 0, 0, 1, 1, -1, 4102, null), P(-1, 14, 11, 0, 0, 1, 1, -1, 4098, null), P(-1, 20, 11, 0, 0, 1, 1, -1, 4101, null), P(-1, 21, 11, 0, 0, 1, 1, -1, 4101, null), P(-1, 22, 11, 0, 0, 1, 1, -1, 4103, null), P(-1, 19, 11, 0, 0, 1, 1, -1, 4098, null), P(-1, 25, 11, 0, 0, 1, 1, -1, 4101, null), P(-1, 26, 11, 0, 0, 1, 1, -1, 4101, null), P(-1, 27, 11, 0, 0, 1, 1, -1, 4104, null), P(-1, 24, 11, 0, 0, 1, 1, -1, 4098, null), P(-1, 5, 16, 0, 0, 1, 1, -1, 4101, null), P(-1, 6, 16, 0, 0, 1, 1, -1, 4101, null), P(-1, 7, 16, 0, 0, 1, 1, -1, 4105, null), P(-1, 4, 16, 0, 0, 1, 1, -1, 4098, null), P(-1, 10, 16, 0, 0, 1, 1, -1, 4101, null), P(-1, 11, 16, 0, 0, 1, 1, -1, 4101, null), P(-1, 12, 16, 0, 0, 1, 1, -1, 4106, null), P(-1, 9, 16, 0, 0, 1, 1, -1, 4098, null), P(-1, 15, 16, 0, 0, 1, 1, -1, 4101, null), P(-1, 16, 16, 0, 0, 1, 1, -1, 4101, null), P(-1, 17, 16, 0, 0, 1, 1, -1, 4107, null), P(-1, 14, 16, 0, 0, 1, 1, -1, 4098, null), P(-1, 20, 16, 0, 0, 1, 1, -1, 4101, null), P(-1, 21, 16, 0, 0, 1, 1, -1, 4101, null), P(-1, 22, 16, 0, 0, 1, 1, -1, 4108, null), P(-1, 19, 16, 0, 0, 1, 1, -1, 4098, null), P(-1, 25, 16, 0, 0, 1, 1, -1, 4101, null), P(-1, 26, 16, 0, 0, 1, 1, -1, 4102, null), P(-1, 27, 16, 0, 0, 1, 1, -1, 4099, null), P(-1, 24, 16, 0, 0, 1, 1, -1, 4098, null), P(-1, 5, 21, 0, 0, 1, 1, -1, 4101, null), P(-1, 6, 21, 0, 0, 1, 1, -1, 4102, null), P(-1, 7, 21, 0, 0, 1, 1, -1, 4100, null), P(-1, 4, 21, 0, 0, 1, 1, -1, 4098, null), P(-1, 10, 21, 0, 0, 1, 1, -1, 4101, null), P(-1, 11, 21, 0, 0, 1, 1, -1, 4102, null), P(-1, 12, 21, 0, 0, 1, 1, -1, 4101, null), P(-1, 9, 21, 0, 0, 1, 1, -1, 4098, null), P(-1, 15, 21, 0, 0, 1, 1, -1, 4101, null), P(-1, 16, 21, 0, 0, 1, 1, -1, 4102, null), P(-1, 17, 21, 0, 0, 1, 1, -1, 4102, null), P(-1, 14, 21, 0, 0, 1, 1, -1, 4098, null), P(-1, 20, 21, 0, 0, 1, 1, -1, 4101, null), P(-1, 21, 21, 0, 0, 1, 1, -1, 4102, null), P(-1, 22, 21, 0, 0, 1, 1, -1, 4103, null), P(-1, 19, 21, 0, 0, 1, 1, -1, 4098, null), P(-1, 25, 21, 0, 0, 1, 1, -1, 4101, null), P(-1, 26, 21, 0, 0, 1, 1, -1, 4102, null), P(-1, 27, 21, 0, 0, 1, 1, -1, 4104, null), P(-1, 24, 21, 0, 0, 1, 1, -1, 4098, null) }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(2, 0, 0, 16, null),
                    },
                    Sprites = new List<ScreenSprite>
                    {
                        S("Grid cursor", 13, 12, 14, 1, 0, 15, 0, 128, 24, 3, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Scroll up", 13, 12, 14, 5, 0, 15, 1, 242, 12, 3, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Scroll down", 13, 12, 14, 8, 0, 15, 1, 242, 140, 3, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Scroll bar", 13, 12, 14, 4, 0, 15, 1, 242, 76, 3, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Scroll thumb", 13, 12, 14, 11, 0, 15, 1, 242, 40, 3, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)1, -1, 1, 124, 128, (ScreenTextAlign)1, 0, 0, 2, 1, 0, 0, 0, -1, 0, -1, 10, 16, 11, 2, false, null, "Entry"),
                    },
                    Mons = new List<ScreenMon>
                    {
                        M((ScreenMonKind)3, 48, 24, 3, 0, -2, (DexSizeRole)0, null),
                        M((ScreenMonKind)3, 88, 24, 3, 0, -1, (DexSizeRole)0, null),
                        M((ScreenMonKind)3, 128, 24, 3, 0, 0, (DexSizeRole)0, null),
                        M((ScreenMonKind)3, 168, 24, 3, 0, 1, (DexSizeRole)0, null),
                        M((ScreenMonKind)3, 208, 24, 3, 0, 2, (DexSizeRole)0, null),
                        M((ScreenMonKind)3, 48, 64, 3, 0, 3, (DexSizeRole)0, null),
                        M((ScreenMonKind)3, 88, 64, 3, 0, 4, (DexSizeRole)0, null),
                        M((ScreenMonKind)3, 128, 64, 3, 0, 5, (DexSizeRole)0, null),
                        M((ScreenMonKind)3, 168, 64, 3, 0, 6, (DexSizeRole)0, null),
                        M((ScreenMonKind)3, 208, 64, 3, 0, 7, (DexSizeRole)0, null),
                        M((ScreenMonKind)3, 48, 104, 3, 0, 8, (DexSizeRole)0, null),
                        M((ScreenMonKind)3, 88, 104, 3, 0, 9, (DexSizeRole)0, null),
                        M((ScreenMonKind)3, 128, 104, 3, 0, 10, (DexSizeRole)0, null),
                        M((ScreenMonKind)3, 168, 104, 3, 0, 11, (DexSizeRole)0, null),
                        M((ScreenMonKind)3, 208, 104, 3, 0, 12, (DexSizeRole)0, null),
                        M((ScreenMonKind)3, 48, 144, 3, 0, 13, (DexSizeRole)0, null),
                        M((ScreenMonKind)3, 88, 144, 3, 0, 14, (DexSizeRole)0, null),
                        M((ScreenMonKind)3, 128, 144, 3, 0, 15, (DexSizeRole)0, null),
                        M((ScreenMonKind)3, 168, 144, 3, 0, 16, (DexSizeRole)0, null),
                        M((ScreenMonKind)3, 208, 144, 3, 0, 17, (DexSizeRole)0, null),
                        M((ScreenMonKind)3, 48, 184, 3, 0, 18, (DexSizeRole)0, null),
                        M((ScreenMonKind)3, 88, 184, 3, 0, 19, (DexSizeRole)0, null),
                        M((ScreenMonKind)3, 128, 184, 3, 0, 20, (DexSizeRole)0, null),
                        M((ScreenMonKind)3, 168, 184, 3, 0, 21, (DexSizeRole)0, null),
                        M((ScreenMonKind)3, 208, 184, 3, 0, 22, (DexSizeRole)0, null),
                        M((ScreenMonKind)3, 48, 224, 3, 0, 23, (DexSizeRole)0, null),
                        M((ScreenMonKind)3, 88, 224, 3, 0, 24, (DexSizeRole)0, null),
                        M((ScreenMonKind)3, 128, 224, 3, 0, 25, (DexSizeRole)0, null),
                        M((ScreenMonKind)3, 168, 224, 3, 0, 26, (DexSizeRole)0, null),
                        M((ScreenMonKind)3, 208, 224, 3, 0, 27, (DexSizeRole)0, null),
                    },
                },
                Sub = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(1, 1, 4, -1, 32, 32, 0, 0, null, new List<ScreenPaste> {  }),
                        B(3, 3, 4, 19, 32, 32, 0, 0, "Entry", new List<ScreenPaste> { P(20, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                        B(2, 2, 4, 58, 32, 32, 0, 0, "Criteria", new List<ScreenPaste> { P(60, 0, 0, 0, 0, -1, -1, -1, -1, null), P(57, 0, 0, 0, 0, -1, 19, -1, -1, null) }),
                        B(3, 3, 4, 58, 32, 32, 0, 0, "Criteria", new List<ScreenPaste> { P(59, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(18, 0, 0, 16, null),
                    },
                    Sprites = new List<ScreenSprite>
                    {
                        S("Caught mark", 30, 29, 31, 0, 0, 32, 0, 112, 32, 3, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "Entry"),
                        S("First type", 33, 36, 34, 0, 0, 35, 0, 168, 72, 3, 1, 36, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "Entry"),
                        S("Second type", 33, 36, 34, 0, 0, 35, 0, 217, 72, 3, 2, 36, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, "Entry"),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 14, 1, 128, 158, (ScreenTextAlign)1, 0, 2, 2, 1, 0, 0, 0, -1, 0, -1, 2, 19, 28, 5, false, null, "Criteria"),
                        T((ScreenTextKind)0, 15, 1, 128, 174, (ScreenTextAlign)1, 0, 2, 2, 1, 0, 0, 0, -1, 0, -1, 2, 19, 28, 5, false, new[] { "25" }, "Criteria"),
                        T((ScreenTextKind)0, 26, 1, 68, 8, (ScreenTextAlign)1, 0, 2, 2, 1, 0, 0, 0, -1, 0, -1, 4, 1, 9, 2, false, null, "Criteria"),
                        T((ScreenTextKind)0, 27, 1, 28, 32, (ScreenTextAlign)1, 0, 2, 2, 1, 0, 0, 0, -1, 0, -1, 1, 4, 5, 2, false, null, "Criteria"),
                        T((ScreenTextKind)0, 28, 1, 28, 56, (ScreenTextAlign)1, 0, 2, 2, 1, 0, 0, 0, -1, 0, -1, 1, 7, 5, 2, false, null, "Criteria"),
                        T((ScreenTextKind)0, 29, 1, 28, 80, (ScreenTextAlign)1, 0, 2, 2, 1, 0, 0, 0, -1, 0, -1, 1, 10, 5, 2, false, null, "Criteria"),
                        T((ScreenTextKind)0, 30, 1, 28, 104, (ScreenTextAlign)1, 0, 2, 2, 1, 0, 0, 0, -1, 0, -1, 1, 13, 5, 2, false, null, "Criteria"),
                        T((ScreenTextKind)0, 31, 1, 28, 128, (ScreenTextAlign)1, 0, 2, 2, 1, 0, 0, 0, -1, 0, -1, 1, 16, 5, 2, false, null, "Criteria"),
                        T((ScreenTextKind)0, 32, 1, 224, 32, (ScreenTextAlign)1, 0, 2, 2, 1, 0, 0, 0, -1, 0, -1, 25, 4, 6, 2, false, null, "Criteria"),
                        T((ScreenTextKind)0, 41, 1, 172, 8, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 16, 1, 11, 2, false, null, "Criteria"),
                        T((ScreenTextKind)0, 113, 1, 88, 32, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 7, 4, 9, 2, false, null, "Criteria"),
                        T((ScreenTextKind)0, 64, 1, 85, 56, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 7, 7, 8, 2, false, null, "Criteria"),
                        T((ScreenTextKind)0, 64, 1, 149, 56, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 15, 7, 8, 2, false, null, "Criteria"),
                        T((ScreenTextKind)0, 68, 1, 84, 128, (ScreenTextAlign)1, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 7, 16, 7, 2, false, null, "Criteria"),
                        T((ScreenTextKind)0, 8, 1, 16, 0, (ScreenTextAlign)0, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 2, 0, 8, 2, false, null, "Entry"),
                        T((ScreenTextKind)2, -1, 1, 121, 24, (ScreenTextAlign)0, 0, 0, 2, 1, 4, 0, 0, -1, 0, -1, 15, 3, 4, 2, false, null, "Entry"),
                        T((ScreenTextKind)1, -1, 1, 152, 24, (ScreenTextAlign)0, 0, 0, 2, 1, 4, 0, 0, -1, 0, -1, 19, 3, 9, 2, false, null, "Entry"),
                        T((ScreenTextKind)3, -1, 1, 244, 40, (ScreenTextAlign)2, 0, 0, 2, 1, 4, 0, 0, -1, 0, -1, 13, 5, 18, 2, false, null, "Entry"),
                        T((ScreenTextKind)6, -1, 1, 128, 136, (ScreenTextAlign)3, 0, 1, 2, 1, 0, 0, 0, -1, 0, -1, 2, 17, 28, 6, false, null, "Entry"),
                        T((ScreenTextKind)0, 10, 1, 164, 88, (ScreenTextAlign)1, 0, 1, 2, 1, 0, 0, 0, -1, 0, -1, 18, 11, 5, 2, false, null, "Entry"),
                        T((ScreenTextKind)4, -1, 1, 188, 88, (ScreenTextAlign)0, 0, 1, 2, 1, 0, 0, 0, -1, 0, -1, 23, 11, 8, 2, false, null, "Entry"),
                        T((ScreenTextKind)0, 11, 1, 164, 104, (ScreenTextAlign)1, 0, 1, 2, 1, 0, 0, 0, -1, 0, -1, 18, 13, 5, 2, false, null, "Entry"),
                        T((ScreenTextKind)5, -1, 1, 188, 104, (ScreenTextAlign)0, 0, 1, 2, 1, 0, 0, 0, -1, 0, -1, 23, 13, 8, 2, false, null, "Entry"),
                    },
                    Mons = new List<ScreenMon>
                    {
                        M((ScreenMonKind)0, 48, 72, 3, 0, 0, (DexSizeRole)0, "Entry"),
                        M((ScreenMonKind)1, 120, 80, 3, 0, 0, (DexSizeRole)0, "Entry"),
                    },
                },
            },
            new ScreenPage
            {
                Id = "area", Name = "Area", MainOnTop = false,
                Variants = new string[] { "Johto", "Kanto", "No National Dex", "Area unknown" },
                Main = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(0, 1, 4, 83, 32, 32, 0, 0, null, new List<ScreenPaste> { P(81, 0, 0, 0, 0, -1, -1, -1, -1, "Johto|Kanto|Area unknown"), P(80, 0, 0, 0, 0, -1, -1, -1, -1, "No National Dex") }),
                        B(1, 0, 4, 4, 32, 32, 0, 0, null, new List<ScreenPaste> { P(11, 0, 20, 0, 0, -1, -1, -1, -1, null) }),
                        B(2, 2, 4, -1, 32, 32, 0, 0, null, new List<ScreenPaste> {  }),
                        B(3, 3, 4, 83, 32, 32, 0, 0, null, new List<ScreenPaste> { P(82, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(2, 0, 0, 16, null),
                    },
                    Sprites = new List<ScreenSprite>
                    {
                        S("Selected tab", 13, 12, 14, 3, 0, 15, 4, 32, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Time of day", 13, 12, 14, 26, 0, 15, 3, 32, 108, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Time cursor", 13, 12, 14, 27, 0, 15, 0, 32, 108, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Scroll up", 13, 12, 14, 5, 0, 15, 1, 242, 44, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Scroll down", 13, 12, 14, 8, 0, 15, 1, 242, 148, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Scroll bar", 13, 12, 14, 63, 0, 15, 1, 242, 96, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Scroll thumb", 13, 12, 14, 14, 0, 15, 1, 242, 96, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 132, 1, 32, 40, (ScreenTextAlign)1, 0, 1, 5, 9, 0, 0, 0, -1, 0, -1, 1, 5, 6, 2, false, null, null),
                        T((ScreenTextKind)0, 65, 1, 100, 8, (ScreenTextAlign)1, 0, 1, 15, 12, 4, 0, 0, -1, 0, -1, 9, 1, 7, 2, false, null, "Johto|Kanto"),
                        T((ScreenTextKind)0, 66, 1, 220, 8, (ScreenTextAlign)1, 0, 1, 15, 12, 4, 0, 0, -1, 0, -1, 24, 1, 7, 2, false, null, "Johto|Kanto"),
                        T((ScreenTextKind)0, 65, 1, 172, 8, (ScreenTextAlign)1, 0, 1, 5, 9, 0, 0, 0, -1, 0, -1, 18, 1, 7, 2, false, null, "No National Dex"),
                        T((ScreenTextKind)11, 0, 2, 152, 40, (ScreenTextAlign)1, 0, 1, 15, 12, 4, 0, 0, -1, 0, -1, 10, 5, 18, 2, false, null, "Johto|Kanto|No National Dex"),
                        T((ScreenTextKind)11, 1, 2, 152, 64, (ScreenTextAlign)1, 0, 1, 15, 12, 4, 0, 0, -1, 0, -1, 10, 8, 18, 2, false, null, "Johto|Kanto|No National Dex"),
                        T((ScreenTextKind)11, 2, 2, 152, 88, (ScreenTextAlign)1, 0, 1, 15, 12, 4, 0, 0, -1, 0, -1, 10, 11, 18, 2, false, null, "Johto|Kanto|No National Dex"),
                        T((ScreenTextKind)11, 3, 2, 152, 112, (ScreenTextAlign)1, 0, 1, 15, 12, 4, 0, 0, -1, 0, -1, 10, 14, 18, 2, false, null, "Johto|Kanto|No National Dex"),
                        T((ScreenTextKind)11, 4, 2, 152, 136, (ScreenTextAlign)1, 0, 1, 15, 12, 4, 0, 0, -1, 0, -1, 10, 17, 18, 2, false, null, "Johto|Kanto|No National Dex"),
                        T((ScreenTextKind)11, 5, 2, 152, 160, (ScreenTextAlign)1, 0, 1, 15, 12, 4, 0, 0, -1, 0, -1, 10, 20, 18, 2, false, null, "Johto|Kanto|No National Dex"),
                    },
                },
                Sub = new ScreenLayout
                {
                    Darken = 0,
                    Habitat = new DexHabitat { Bg = -1, Cells = 120, Drawing = 119, Anim = 121, Colours = 122, Absolute = true, Priority = 3, Blend = 15, BlendUnder = 16, Face = 116, FaceDrawing = 115, FaceAnim = 117, FaceColours = 118 },
                    Bgs = new List<ScreenBg>
                    {
                        B(0, 3, 4, 87, 64, 32, -72, 0, "Johto|No National Dex|Area unknown", new List<ScreenPaste> { P(86, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                        B(0, 3, 4, 87, 64, 32, 104, 0, "Kanto", new List<ScreenPaste> { P(86, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                        B(1, 0, 4, -1, 32, 32, 0, 0, null, new List<ScreenPaste> {  }),
                        B(3, 2, 4, 85, 32, 32, 0, 0, null, new List<ScreenPaste> { P(84, 0, 0, 0, 0, -1, -1, -1, -1, null), P(88, 10, 11, 0, 0, -1, -1, -1, -1, "Area unknown") }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(18, 0, 0, 16, null),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 142, 1, 16, 0, (ScreenTextAlign)0, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 2, 0, 9, 2, false, null, null),
                        T((ScreenTextKind)1, -1, 1, 36, 128, (ScreenTextAlign)1, 0, 1, 2, 1, 0, 0, 0, -1, 0, -1, 0, 16, 9, 2, false, null, null),
                        T((ScreenTextKind)0, 128, 1, 160, 96, (ScreenTextAlign)1, 0, 1, 2, 1, 0, 0, 0, -1, 0, -1, 13, 12, 14, 2, false, null, "Area unknown"),
                        T((ScreenTextKind)0, 130, 1, 36, 24, (ScreenTextAlign)1, 0, 1, 2, 1, 0, 0, 0, -1, 0, -1, 1, 3, 7, 2, false, null, null),
                    },
                    Mons = new List<ScreenMon>
                    {
                        M((ScreenMonKind)3, 36, 112, 0, 0, 0, (DexSizeRole)0, null),
                    },
                },
            },
            new ScreenPage
            {
                Id = "size", Name = "Size", MainOnTop = false,
                Variants = new string[] { "Male player", "Female player" },
                Main = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(1, 0, 4, 4, 32, 32, 0, 0, null, new List<ScreenPaste> { P(11, 0, 20, 0, 0, -1, -1, -1, -1, null) }),
                        B(3, 3, 4, 90, 32, 32, 0, 0, null, new List<ScreenPaste> { P(89, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(2, 0, 0, 16, null),
                    },
                    Sprites = new List<ScreenSprite>
                    {
                        S("Selected tab", 13, 12, 14, 3, 0, 15, 4, 96, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Dial centre", 13, 12, 14, 33, 0, 15, 1, 128, 104, 1, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Left knob", 13, 12, 14, 32, 0, 15, 1, 72, 104, 1, 0, -1, 0, 0, (DexSizeRole)3, (DexReadout)0, 0, 0, null),
                        S("Right knob", 13, 12, 14, 32, 0, 15, 1, 184, 104, 1, 0, -1, 0, 0, (DexSizeRole)4, (DexReadout)0, 0, 0, null),
                        S("Dial", 13, 12, 14, 31, 0, 15, 1, 128, 104, 1, 0, -1, 0, 0, (DexSizeRole)7, (DexReadout)0, 0, 0, null),
                        S("Player", 106, 105, 107, 0, 0, 108, 0, 184, 88, 0, 0, -1, 0, 0, (DexSizeRole)6, (DexReadout)0, 0, 0, "Male player"),
                        S("Player", 106, 109, 107, 0, 0, 110, 0, 184, 88, 0, 0, -1, 0, 0, (DexSizeRole)6, (DexReadout)0, 0, 0, "Female player"),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 137, 1, 128, 8, (ScreenTextAlign)1, 0, 1, 5, 9, 0, 0, 0, -1, 0, -1, 10, 1, 12, 2, false, null, null),
                        T((ScreenTextKind)0, 11, 1, 32, 136, (ScreenTextAlign)1, 0, 1, 15, 5, 0, 0, 0, -1, 0, -1, 2, 17, 4, 2, false, null, null),
                        T((ScreenTextKind)5, -1, 1, 88, 136, (ScreenTextAlign)1, 0, 1, 15, 5, 0, 0, 0, -1, 0, -1, 7, 17, 8, 2, false, null, null),
                        T((ScreenTextKind)0, 11, 1, 152, 136, (ScreenTextAlign)1, 0, 1, 15, 5, 0, 0, 0, -1, 0, -1, 17, 17, 4, 2, false, null, null),
                        T((ScreenTextKind)0, 140, 1, 208, 136, (ScreenTextAlign)1, 0, 1, 15, 5, 0, 0, 0, -1, 0, -1, 22, 17, 8, 2, false, null, "Male player"),
                        T((ScreenTextKind)0, 141, 1, 208, 136, (ScreenTextAlign)1, 0, 1, 15, 5, 0, 0, 0, -1, 0, -1, 22, 17, 8, 2, false, null, "Female player"),
                    },
                    Mons = new List<ScreenMon>
                    {
                        M((ScreenMonKind)3, 72, 88, 0, 0, 0, (DexSizeRole)5, null),
                    },
                },
                Sub = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(1, 1, 4, -1, 32, 32, 0, 0, null, new List<ScreenPaste> {  }),
                        B(2, 3, 4, 92, 32, 32, 0, 0, null, new List<ScreenPaste> { P(91, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(18, 0, 0, 16, null),
                    },
                    Sprites = new List<ScreenSprite>
                    {
                        S("Player silhouette", 112, 111, 113, 0, 0, 108, 0, 184, 112, 1, 0, -1, 0, 0, (DexSizeRole)2, (DexReadout)0, 0, 0, "Male player"),
                        S("Player silhouette", 112, 114, 113, 0, 0, 110, 0, 184, 112, 1, 0, -1, 0, 0, (DexSizeRole)2, (DexReadout)0, 0, 0, "Female player"),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 143, 1, 16, 0, (ScreenTextAlign)0, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 2, 0, 9, 2, false, null, null),
                        T((ScreenTextKind)1, -1, 1, 64, 24, (ScreenTextAlign)1, 0, 1, 2, 1, 0, 0, 0, -1, 0, -1, 4, 3, 8, 2, false, null, null),
                        T((ScreenTextKind)14, -1, 1, 192, 24, (ScreenTextAlign)1, 0, 1, 2, 1, 0, 0, 0, -1, 0, -1, 20, 3, 8, 2, false, null, null),
                        T((ScreenTextKind)0, 136, 1, 128, 48, (ScreenTextAlign)1, 0, 1, 2, 1, 0, 0, 0, -1, 0, -1, 10, 6, 12, 2, false, null, null),
                        T((ScreenTextKind)0, 10, 1, 32, 168, (ScreenTextAlign)1, 0, 2, 2, 1, 0, 0, 0, -1, 0, -1, 2, 21, 4, 2, false, null, null),
                        T((ScreenTextKind)4, -1, 1, 88, 168, (ScreenTextAlign)1, 0, 2, 2, 1, 0, 0, 0, -1, 0, -1, 7, 21, 8, 2, false, null, null),
                        T((ScreenTextKind)0, 10, 1, 152, 168, (ScreenTextAlign)1, 0, 2, 2, 1, 0, 0, 0, -1, 0, -1, 17, 21, 4, 2, false, null, null),
                        T((ScreenTextKind)0, 138, 1, 208, 168, (ScreenTextAlign)1, 0, 2, 2, 1, 0, 0, 0, -1, 0, -1, 22, 21, 8, 2, false, null, "Male player"),
                        T((ScreenTextKind)0, 139, 1, 208, 168, (ScreenTextAlign)1, 0, 2, 2, 1, 0, 0, 0, -1, 0, -1, 22, 21, 8, 2, false, null, "Female player"),
                    },
                    Mons = new List<ScreenMon>
                    {
                        M((ScreenMonKind)0, 72, 112, 2, 16, 0, (DexSizeRole)1, null),
                    },
                },
            },
            new ScreenPage
            {
                Id = "forms", Name = "Forms", MainOnTop = false,
                Variants = new string[] { "One form", "Several forms" },
                Main = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(0, 1, 4, 93, 32, 32, 0, 0, null, new List<ScreenPaste> { P(95, 0, 0, 0, 0, -1, -1, -1, -1, null), P(100, 16, 16, 0, 0, -1, -1, -1, -1, "One form") }),
                        B(1, 0, 4, 4, 32, 32, 0, 0, null, new List<ScreenPaste> { P(11, 0, 20, 0, 0, -1, -1, -1, -1, null) }),
                        B(2, 2, 4, -1, 32, 32, 0, 0, null, new List<ScreenPaste> {  }),
                        B(3, 3, 4, 93, 32, 32, 0, 0, null, new List<ScreenPaste> { P(96, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(2, 0, 0, 16, null),
                    },
                    Sprites = new List<ScreenSprite>
                    {
                        S("Selected tab", 13, 12, 14, 3, 0, 15, 4, 160, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Scroll up", 13, 12, 14, 7, 0, 15, 1, 242, 12, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Scroll down", 13, 12, 14, 10, 0, 15, 1, 242, 116, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Scroll bar", 13, 12, 14, 63, 0, 15, 1, 242, 64, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Scroll thumb", 13, 12, 14, 14, 0, 15, 1, 242, 64, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 168, 1, 168, 136, (ScreenTextAlign)0, 0, 1, 15, 12, 4, 0, 0, -1, 0, -1, 21, 17, 10, 2, false, null, "Several forms"),
                        T((ScreenTextKind)1, -1, 2, 64, 56, (ScreenTextAlign)0, 0, 1, 15, 12, 4, 0, 0, -1, 0, -1, 8, 7, 18, 2, false, null, null),
                    },
                    Mons = new List<ScreenMon>
                    {
                        M((ScreenMonKind)3, 48, 62, 0, 0, 0, (DexSizeRole)0, null),
                    },
                },
                Sub = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(1, 1, 4, -1, 32, 32, 0, 0, null, new List<ScreenPaste> {  }),
                        B(3, 3, 4, 94, 32, 32, 0, 0, null, new List<ScreenPaste> { P(97, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(18, 0, 0, 16, null),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 170, 1, 16, 0, (ScreenTextAlign)0, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 2, 0, 9, 2, false, null, null),
                        T((ScreenTextKind)1, -1, 1, 104, 40, (ScreenTextAlign)2, 0, 1, 2, 1, 0, 0, 0, -1, 0, -1, 4, 5, 9, 2, false, null, null),
                    },
                    Mons = new List<ScreenMon>
                    {
                        M((ScreenMonKind)0, 64, 120, 2, 0, 0, (DexSizeRole)0, null),
                    },
                },
            },
            new ScreenPage
            {
                Id = "forms_compare", Name = "Forms: compare", MainOnTop = false,
                Variants = new string[] {  },
                Main = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(1, 0, 4, -1, 32, 32, 0, 0, null, new List<ScreenPaste> {  }),
                        B(3, 3, 4, 93, 32, 32, 0, 0, null, new List<ScreenPaste> { P(98, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(2, 0, 0, 16, null),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 170, 1, 128, 16, (ScreenTextAlign)1, 0, 1, 5, 9, 0, 0, 0, -1, 0, -1, 13, 2, 6, 2, false, null, null),
                        T((ScreenTextKind)0, 171, 1, 72, 164, (ScreenTextAlign)1, 0, 1, 15, 12, 4, 0, 0, -1, 0, -1, 3, 20, 12, 3, false, null, null),
                        T((ScreenTextKind)0, 172, 1, 192, 164, (ScreenTextAlign)1, 0, 1, 15, 12, 4, 0, 0, -1, 0, -1, 18, 20, 12, 3, false, null, null),
                    },
                },
                Sub = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(1, 1, 4, -1, 32, 32, 0, 0, null, new List<ScreenPaste> {  }),
                        B(3, 3, 4, 94, 32, 32, 0, 0, null, new List<ScreenPaste> { P(99, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(18, 0, 0, 16, null),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 170, 1, 16, 0, (ScreenTextAlign)0, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 2, 0, 9, 2, false, null, null),
                    },
                },
            },
            new ScreenPage
            {
                Id = "back", Name = "Back to the list", MainOnTop = false,
                Variants = new string[] {  },
                Main = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(1, 0, 4, 4, 32, 32, 0, 0, null, new List<ScreenPaste> { P(11, 0, 20, 0, 0, -1, -1, -1, -1, null) }),
                        B(3, 3, 4, 102, 32, 32, 0, 0, null, new List<ScreenPaste> { P(101, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(2, 0, 0, 16, null),
                    },
                    Sprites = new List<ScreenSprite>
                    {
                        S("Selected tab", 13, 12, 14, 3, 0, 15, 4, 224, 176, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 174, 1, 140, 80, (ScreenTextAlign)1, 0, 1, 15, 12, 4, 0, 0, -1, 0, -1, 10, 10, 15, 2, false, null, null),
                    },
                },
                Sub = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(1, 1, 4, -1, 32, 32, 0, 0, null, new List<ScreenPaste> {  }),
                        B(2, 3, 4, 104, 32, 32, 0, 0, null, new List<ScreenPaste> { P(103, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(18, 0, 0, 16, null),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 173, 1, 16, 0, (ScreenTextAlign)0, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 2, 0, 9, 2, false, null, null),
                    },
                },
            },
            new ScreenPage
            {
                Id = "catch_registration", Name = "Registered after a catch", MainOnTop = true,
                Variants = new string[] {  },
                Main = new ScreenLayout
                {
                    Darken = 0,
                    Bgs = new List<ScreenBg>
                    {
                        B(1, 1, 4, -1, 32, 32, 0, 0, null, new List<ScreenPaste> {  }),
                        B(2, 2, 4, 19, 32, 32, 0, 0, null, new List<ScreenPaste> { P(20, 0, 0, 0, 0, -1, -1, -1, -1, null) }),
                    },
                    Palettes = new List<ScreenPaletteLoad>
                    {
                        L(18, 0, 0, 16, null),
                    },
                    Sprites = new List<ScreenSprite>
                    {
                        S("Caught mark", 30, 29, 31, 0, 0, 32, 0, 112, 32, 0, 0, -1, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("First type", 33, 36, 34, 0, 0, 35, 0, 168, 72, 0, 1, 36, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                        S("Second type", 33, 36, 34, 0, 0, 35, 0, 217, 72, 0, 2, 36, 0, 0, (DexSizeRole)0, (DexReadout)0, 0, 0, null),
                    },
                    Texts = new List<ScreenText>
                    {
                        T((ScreenTextKind)0, 144, 1, 16, 0, (ScreenTextAlign)0, 0, 2, 2, 1, 4, 0, 0, -1, 0, -1, 2, 0, 28, 2, false, null, null),
                        T((ScreenTextKind)2, -1, 1, 121, 24, (ScreenTextAlign)0, 0, 0, 2, 1, 4, 0, 0, -1, 0, -1, 15, 3, 4, 2, false, null, null),
                        T((ScreenTextKind)1, -1, 1, 152, 24, (ScreenTextAlign)0, 0, 0, 2, 1, 4, 0, 0, -1, 0, -1, 19, 3, 9, 2, false, null, null),
                        T((ScreenTextKind)3, -1, 1, 244, 40, (ScreenTextAlign)2, 0, 0, 2, 1, 4, 0, 0, -1, 0, -1, 13, 5, 18, 2, false, null, null),
                        T((ScreenTextKind)6, -1, 1, 128, 136, (ScreenTextAlign)3, 0, 1, 2, 1, 0, 0, 0, -1, 0, -1, 2, 17, 28, 6, false, null, null),
                        T((ScreenTextKind)0, 10, 1, 164, 88, (ScreenTextAlign)1, 0, 1, 2, 1, 0, 0, 0, -1, 0, -1, 18, 11, 5, 2, false, null, null),
                        T((ScreenTextKind)4, -1, 1, 188, 88, (ScreenTextAlign)0, 0, 1, 2, 1, 0, 0, 0, -1, 0, -1, 23, 11, 8, 2, false, null, null),
                        T((ScreenTextKind)0, 11, 1, 164, 104, (ScreenTextAlign)1, 0, 1, 2, 1, 0, 0, 0, -1, 0, -1, 18, 13, 5, 2, false, null, null),
                        T((ScreenTextKind)5, -1, 1, 188, 104, (ScreenTextAlign)0, 0, 1, 2, 1, 0, 0, 0, -1, 0, -1, 23, 13, 8, 2, false, null, null),
                    },
                    Mons = new List<ScreenMon>
                    {
                        M((ScreenMonKind)0, 48, 72, 2, 0, 0, (DexSizeRole)0, null),
                        M((ScreenMonKind)1, 120, 80, 2, 0, 0, (DexSizeRole)0, null),
                    },
                },
                Sub = new ScreenLayout
                {
                    Darken = 0,
                },
            },
        };

    }
}
