using System.Collections.Generic;
using System.Linq;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// What each Pokétch application is made of, and the rules an edit has to keep. Recipes from the
    /// pokeplatinum decomp's applications/poketch and its generated graphics index, checked against a real
    /// Platinum archive.
    ///
    /// The game does no bounds checking when it uploads these to VRAM: an oversized drawing runs off the end
    /// of the app's room and into the casing's tiles rather than being clipped. So the numbers here are what
    /// an import has to be refused against.
    /// </summary>
    public static class PoketchApps
    {
        /// <summary>Tiles of room an application gets. The same ceiling applies to drawings and to sprites.</summary>
        public const int TileCeiling = 512;

        /// <summary>
        /// The shared digit sheet. Five applications have this count compiled into where their own sprites
        /// start, so it cannot grow or shrink.
        /// </summary>
        public const int DigitSheetTiles = 80;

        /// <summary>Members no reachable application ever loads.</summary>
        public static readonly int[] NeverLoaded = { 1, 8, 9, 58, 59, 105 };

        /// <summary>
        /// The sheet of figures five screens share. It is loaded into sprite memory before a screen's own
        /// sheet, and their cells count tiles from where it ends, so anything drawing those cells needs it
        /// laid out first or every tile number is 80 out.
        /// </summary>
        public const int DigitSheetMember = 2;

        /// <summary>The sheet loaded ahead of this screen's own, or -1 when there is none.</summary>
        public static int SharedSheetFor(App app) => app != null && app.UsesDigitSheet ? DigitSheetMember : -1;

        /// <summary>
        /// The four colours of a theme row that the game addresses by number: text, icons and the Dot Artist
        /// canvas all pick from these, so changing them reaches far beyond one screen.
        /// </summary>
        public static readonly int[] ToneRamp = { 1, 4, 8, 15 };

        /// <summary>Members that carry more than a frame order: rotation, scale or a shift per frame.</summary>
        public static readonly int[] AnimationsWithTransforms = { 6, 28, 41, 73, 119 };

        public sealed class App
        {
            public int Id;
            public string Name;
            public int Tiles = -1;          // NCGR, the screen's drawing
            public int Arrangement = -1;    // NSCR, where those tiles go
            public int Sprites = -1;        // NCGR for the OBJ sheet
            public int Cells = -1;          // NCER, which carries the sprite positions
            public int Animation = -1;      // NANR, the frame order and how long each frame is held
            public int TilesUsed;           // of TileCeiling, drawing plus any text window
            public int SpriteTilesUsed;     // of TileCeiling, including the shared digits where used
            public bool UsesDigitSheet;     // members 2, 3, 4
            public bool UsesPokemonIcons;   // members 5, 6 plus icons from another archive
            public int FixedWidthTiles;     // 0 when the drawing may be any shape
            public int MaxTiles;            // 0 when only the ceiling applies
            public string ReadOnlyBecause;  // set when there is nothing here to edit
            public string Warning;          // a rule worth saying out loud before an edit

            /// <summary>
            /// Where the game puts this application's sprite, in screen pixels, for the ones whose own
            /// tables say so. Null means the position is buried in code and the preview has to guess.
            /// </summary>
            public (int X, int Y)[] SpriteSlots;

            /// <summary>
            /// Rectangles the game fills flat, in screen pixels, with the colour of the theme it names.
            /// These are the parts a running game fills in: a health bar, a blank note page.
            /// </summary>
            public (int X, int Y, int W, int H, int Colour)[] Fills;

            /// <summary>Room left for a bigger drawing, once the text windows are accounted for.</summary>
            public int TileRoom => MaxTiles > 0 ? MaxTiles : TileCeiling - (TilesUsed - Drawn);

            // How much of TilesUsed is the drawing itself rather than a window the app opens after it.
            private int Drawn => MaxTiles > 0 ? MaxTiles : TilesUsed;
        }

        /// <summary>
        /// The applications in the order the Pokétch cycles through them. Two of them draw nothing from this
        /// archive at all, and are listed so the editor can say why rather than leave them out.
        /// </summary>
        public static readonly App[] All =
        {
            new App { Id = 0,  Name = "Digital Watch", Tiles = 23, Arrangement = 24, TilesUsed = 48,
                      Warning = "The four digits come out of member 25, which is stored as 32 columns then a "
                              + "tail of 8 rather than one 40-wide strip. It has to keep that shape." },
            new App { Id = 1,  Name = "Calculator", Tiles = 16, Arrangement = 17, TilesUsed = 360,
                      FixedWidthTiles = 40,
                      Warning = "Every key is found by counting 40 tiles per row, and seventeen pressed-key "
                              + "tile lists are compiled in. The drawing has to stay 40 tiles wide, in order." },
            new App { Id = 2,  Name = "Memo Pad", Tiles = 30, Arrangement = 31, Sprites = 32, Cells = 33,
                      Animation = 34, TilesUsed = 392, SpriteTilesUsed = 160, MaxTiles = 12,
                      Warning = "The canvas starts at tile 12, so anything drawn past that is painted over.",
                      // The note page is a 20 by 19 tile window at tile 2,2, and an empty one is filled
                      // with the theme's fourth colour rather than left blank.
                      Fills = new[] { (16, 16, 160, 152, 4) } },
            new App { Id = 3,  Name = "Pedometer", Tiles = 49, Arrangement = 48, Sprites = 52, Cells = 50,
                      Animation = 51, TilesUsed = 8, SpriteTilesUsed = 208, UsesDigitSheet = true },
            new App { Id = 4,  Name = "Party Status", Tiles = 106, Sprites = 109, Cells = 107, Animation = 108,
                      TilesUsed = 58, SpriteTilesUsed = 104, UsesPokemonIcons = true,
                      Warning = "Tiles 1, 2, 5 and 6 of the drawing are the frame and fill the game places "
                              + "itself. Member 105 looks like this screen's arrangement but is never loaded.",
                      // Six party slots, and a health bar under each: eight tiles wide, one tall.
                      SpriteSlots = new[] { (64, 36), (160, 36), (64, 84), (160, 84), (64, 132), (160, 132) },
                      Fills = new[] { (32, 64, 64, 8, 4), (128, 64, 64, 8, 4),
                                      (32, 112, 64, 8, 4), (128, 112, 64, 8, 4),
                                      (32, 160, 64, 8, 4), (128, 160, 64, 8, 4) } },
            new App { Id = 5,  Name = "Friendship Checker", Tiles = 7, Sprites = 35, Cells = 36, Animation = 37,
                      TilesUsed = 4, SpriteTilesUsed = 224, UsesPokemonIcons = true,
                      Warning = "The background is four flat tiles; the whole screen is tile 0 repeated." },
            new App { Id = 6,  Name = "Dowsing Machine", Tiles = 39, Arrangement = 38, Sprites = 42,
                      Cells = 40, Animation = 41, TilesUsed = 64, SpriteTilesUsed = 101,
                      Warning = "Two of its cells ask for sprite colours 13 and 14, which the game builds as "
                              + "it runs. Those numbers cannot be moved." },
            new App { Id = 7,  Name = "Berry Searcher", Tiles = 117, Arrangement = 116, Sprites = 120,
                      Cells = 118, Animation = 119, TilesUsed = 124, SpriteTilesUsed = 60,
                      Warning = "Shares its drawing, sprites, cells and animation with Marking Map. Only the "
                              + "arrangement is its own." },
            new App { Id = 8,  Name = "Daycare Checker", Tiles = 81, Arrangement = 80, Sprites = 84,
                      Cells = 82, Animation = 83, TilesUsed = 16, SpriteTilesUsed = 172,
                      UsesPokemonIcons = true,
                      Warning = "Where the party icons land is worked out from this sprite sheet's size, so "
                              + "changing its tile count moves them.",
                      SpriteSlots = new[] { (56, 128), (168, 128), (112, 136),
                                            (48, 40), (64, 40), (80, 40), (96, 40),
                                            (152, 40), (168, 40), (184, 40), (200, 40) } },
            new App { Id = 9,  Name = "Pokémon History", TilesUsed = 49, SpriteTilesUsed = 192,
                      UsesPokemonIcons = true,
                      ReadOnlyBecause = "This screen has no drawing of its own. It fills with one blank tile "
                                      + "and writes its heading with the font." },
            new App { Id = 10, Name = "Counter", Tiles = 44, Arrangement = 43, Sprites = 47, Cells = 45,
                      Animation = 46, TilesUsed = 6, SpriteTilesUsed = 208, UsesDigitSheet = true,
                      SpriteSlots = new[] { (114, 128) } },
            new App { Id = 11, Name = "Analog Watch", Tiles = 23, Arrangement = 26, Sprites = 29, Cells = 27,
                      Animation = 28, TilesUsed = 48, SpriteTilesUsed = 48,
                      Warning = "Its animation holds 420 entries, the minute angles then the hour angles, "
                              + "found by counting. They cannot be reordered or trimmed.",
                      // Both hands pivot on the same point; which one you see is the cell bank.
                      SpriteSlots = new[] { (116, 100) } },
            new App { Id = 12, Name = "Marking Map", Tiles = 117, Arrangement = 115, Sprites = 120,
                      Cells = 118, Animation = 119, TilesUsed = 108, SpriteTilesUsed = 60,
                      Warning = "Its 19 animation entries are reached by counting from a fixed first one, so "
                              + "the order has to hold. Shared with Berry Searcher." },
            new App { Id = 13, Name = "Link Searcher", Tiles = 96, Arrangement = 95, Sprites = 99, Cells = 97,
                      Animation = 98, TilesUsed = 482, SpriteTilesUsed = 124, MaxTiles = 32,
                      Warning = "Its text window takes 480 of the 512 tiles, leaving room for 32.",
                      SpriteSlots = new[] { (112, 150), (112, 102) } },
            new App { Id = 14, Name = "Coin Toss", Tiles = 54, Arrangement = 53, Sprites = 57, Cells = 55,
                      Animation = 56, TilesUsed = 16, SpriteTilesUsed = 192,
                      // Where the coin sits at rest; it is thrown upward from here.
                      SpriteSlots = new[] { (112, 144) } },
            new App { Id = 15, Name = "Move Tester", Tiles = 61, Arrangement = 60, Sprites = 64, Cells = 62,
                      Animation = 63, TilesUsed = 131, SpriteTilesUsed = 32,
                      SpriteSlots = new[] { (28, 128), (116, 128), (108, 40), (196, 40),
                                            (108, 72), (196, 72), (44, 48) } },
            new App { Id = 16, Name = "Calendar", Tiles = 111, Arrangement = 110, Sprites = 114, Cells = 112,
                      Animation = 113, TilesUsed = 144, SpriteTilesUsed = 16, FixedWidthTiles = 12,
                      Warning = "Each figure's second row is found 12 tiles on, and the month and day tiles "
                              + "sit at fixed numbers. The drawing has to stay 12 tiles wide." },
            new App { Id = 17, Name = "Dot Artist", TilesUsed = 16,
                      ReadOnlyBecause = "The canvas is built while the game runs: sixteen flat tiles, one per "
                                      + "colour of the theme. There is no drawing to edit." },
            new App { Id = 18, Name = "Roulette", Tiles = 86, Arrangement = 85, Sprites = 89, Cells = 87,
                      Animation = 88, TilesUsed = 420, SpriteTilesUsed = 166, MaxTiles = 132,
                      Warning = "Its text window takes 380 tiles, leaving room for 132.",
                      SpriteSlots = new[] { (96, 96), (187, 50), (187, 96), (187, 142) } },
            new App { Id = 19, Name = "Trainer Counter", Tiles = 122, Arrangement = 121, Sprites = 125,
                      Cells = 123, Animation = 124, TilesUsed = 24, SpriteTilesUsed = 244,
                      UsesPokemonIcons = true,
                      Warning = "Its cells are the ten figures in order, reached by number.",
                      SpriteSlots = new[] { (96, 32), (112, 80), (176, 96), (48, 104),
                                            (144, 40), (152, 40), (160, 40),
                                            (100, 144), (108, 144), (116, 144),
                                            (164, 160), (172, 160), (180, 160),
                                            (36, 168), (44, 168), (52, 168) } },
            new App { Id = 20, Name = "Kitchen Timer", Tiles = 91, Arrangement = 90, Sprites = 94, Cells = 92,
                      Animation = 93, TilesUsed = 74, SpriteTilesUsed = 408, UsesDigitSheet = true,
                      Warning = "The hand poses are the same drawing at different cell positions, not "
                              + "separate art.",
                      SpriteSlots = new[] { (48, 56), (176, 56), (48, 160), (112, 160), (176, 160),
                                            (80, 88), (96, 88), (128, 88), (144, 88),
                                            (80, 136), (96, 136), (128, 136), (144, 136),
                                            (80, 112), (96, 112), (128, 112), (144, 112) } },
            new App { Id = 21, Name = "Color Changer", Tiles = 66, Arrangement = 65, Sprites = 69, Cells = 67,
                      Animation = 68, TilesUsed = 96, SpriteTilesUsed = 16,
                      Warning = "This is the screen that picks the theme every other application draws with.",
                      // The knob rests at the left end of the slider.
                      SpriteSlots = new[] { (56, 148) } },
            new App { Id = 22, Name = "Matchup Checker", Tiles = 71, Arrangement = 70, Sprites = 74,
                      Cells = 72, Animation = 73, TilesUsed = 16, SpriteTilesUsed = 294,
                      UsesPokemonIcons = true,
                      Warning = "Where the party icons land is worked out from this sprite sheet's size. Its "
                              + "animation is the only one in the archive that shifts a sprite as it plays.",
                      SpriteSlots = new[] { (112, 148), (112, 32), (48, 88), (176, 88),
                                            (48, 140), (176, 140) } },
            new App { Id = 23, Name = "Stopwatch", Tiles = 21, Arrangement = 20, Sprites = 22, Cells = 18,
                      Animation = 19, TilesUsed = 407, SpriteTilesUsed = 428, UsesDigitSheet = true,
                      FixedWidthTiles = 37,
                      Warning = "Its buttons are found by counting 37 tiles per row across eleven rows, seven "
                              + "states in order. The drawing has to keep that shape.",
                      // Four pairs of figures across the top, and the Voltorb below them.
                      SpriteSlots = new[] { (32, 40), (48, 40), (80, 40), (96, 40),
                                            (128, 40), (144, 40), (176, 40), (192, 40), (112, 96) } },
            new App { Id = 24, Name = "Alarm Clock", Tiles = 76, Arrangement = 75, Sprites = 79, Cells = 77,
                      Animation = 78, TilesUsed = 96, SpriteTilesUsed = 288, UsesDigitSheet = true,
                      SpriteSlots = new[] { (192, 104), (48, 48), (144, 48), (56, 80), (136, 80),
                                            (72, 120), (72, 164), (120, 120), (120, 164),
                                            (64, 144), (80, 144), (112, 144), (128, 144) } },
        };

        /// <summary>
        /// Which file holds the colours a drawing is painted with. The casing and the picture shown before
        /// the player has a Pokétch keep their own; everything else draws with the theme.
        /// </summary>
        public static int ColoursFor(int member)
        {
            if (member is 14 or 15) return 13;
            if (member is 10 or 11 or 12) return 12;
            return 0;
        }

        /// <summary>The file laying out a drawing's tiles, or -1 when that drawing has none.</summary>
        public static int ArrangementFor(int drawing)
        {
            if (drawing == 14) return 15;
            if (drawing == 10) return 11;
            if (drawing == 23) return 24;
            var app = All.FirstOrDefault(a => a.Tiles == drawing && a.Arrangement >= 0);
            return app?.Arrangement ?? -1;
        }

        /// <summary>Which applications draw from a member, for saying what else an edit reaches.</summary>
        public static IReadOnlyList<string> SharedBy(int member)
        {
            if (member < 0) return System.Array.Empty<string>();
            var names = All.Where(a => a.Tiles == member || a.Arrangement == member || a.Sprites == member
                                    || a.Cells == member || a.Animation == member)
                           .Select(a => a.Name).ToList();
            if (member is 2 or 3 or 4)
            {
                names.AddRange(All.Where(a => a.UsesDigitSheet && !names.Contains(a.Name)).Select(a => a.Name));
                names.Add("the application counter");
            }
            if (member is 5 or 6)
                names.AddRange(All.Where(a => a.UsesPokemonIcons && !names.Contains(a.Name)).Select(a => a.Name));
            if (member == 0) return new[] { "every application, the casing and the counter" };
            return names;
        }

    }
}
