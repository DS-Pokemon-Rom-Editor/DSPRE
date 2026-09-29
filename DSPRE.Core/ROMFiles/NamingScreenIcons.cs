using System.Collections.Generic;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// The naming screen's archive (DirNames.nameInputGraphics). Its sprite drawing, cells and animations hold
    /// every icon the screen can show beside the name, one animation each; the layout is the same in all three
    /// games (NamingScreen_InitIconSprite in pokeplatinum and pokeheartgold naming_screen.c).
    /// </summary>
    public static class NamingScreenIcons
    {
        public const int BackgroundColoursFile = 0;
        public const int SpriteColoursFile = 1;
        public const int BackgroundTilesFile = 2;
        public const int TopScreenMapFile = 4;
        public const int SpriteTilesFile = 10;
        public const int SpriteCellsFile = 12;
        public const int SpriteAnimationsFile = 14;

        /// <summary>The underline under each letter of the name.</summary>
        public const int LetterSlotAnimation = 43;

        // Where the game puts the icon and the name on the top screen, in pixels.
        public const int IconX = 24, IconY = 8;
        public const int NameX = 80, NameY = 39, LetterStep = 12;

        // A Pokemon's gender goes after its ten letter slots: 80 + 10 * 13 (naming_screen.c).
        public const int GenderX = 210, GenderY = 27;
        public static bool IsGender(int animation) => animation == 45 || animation == 46;

        /// <summary>An icon and how many letters the screen that shows it takes (the underlines drawn).</summary>
        public sealed record Icon(int Animation, string Name, int Letters);

        public static readonly IReadOnlyList<Icon> All = new[]
        {
            new Icon(48, "Player, boy", 7),
            new Icon(49, "Player, girl", 7),
            new Icon(51, "Rival", 7),
            new Icon(47, "Box", 8),
            new Icon(54, "Group", 7),
            new Icon(53, "Pal Pad", 7),
            new Icon(55, "Route 224 rock", 10),
            new Icon(50, "Pokémon (the real icon is drawn over it)", 10),
            new Icon(45, "Pokémon gender, male", 10),
            new Icon(46, "Pokémon gender, female", 10),
        };
    }
}
