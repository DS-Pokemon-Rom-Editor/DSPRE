using static DSPRE.RomInfo;

namespace DSPRE
{
    // Touch lists outside the shared cursor handlers: Platinum's (pokeplatinum src/applications), measured on every Platinum dump,
    // then HGSS's options, main menu, trainer card and number pad.
    public static partial class RomLocator
    {
        // bag/main.c: the 8, 7, 4 and 1 pocket lists, the dial button and its two rings, and each pocket list's tile spots.
        public static PtBagTouchSites PtBagTouchSitesIn(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Platinum => language switch
            {
                GameLanguages.English => new PtBagTouchSites(84, 0x5A48, 0x5A28, 0x5930, 0x5900, 0x58F8, 0x5908, 0x5914, 0x5BA0, 0x5B80, 0x5B70, 0x58F4),
                GameLanguages.German => new PtBagTouchSites(84, 0x5A48, 0x5A28, 0x5930, 0x5900, 0x58F8, 0x5908, 0x5914, 0x5BA0, 0x5B80, 0x5B70, 0x58F4),
                GameLanguages.French => new PtBagTouchSites(84, 0x5A48, 0x5A28, 0x5930, 0x5900, 0x58F8, 0x5908, 0x5914, 0x5BA0, 0x5B80, 0x5B70, 0x58F4),
                GameLanguages.Italian => new PtBagTouchSites(84, 0x5A48, 0x5A28, 0x5930, 0x5900, 0x58F8, 0x5908, 0x5914, 0x5BA0, 0x5B80, 0x5B70, 0x58F4),
                GameLanguages.Spanish => new PtBagTouchSites(84, 0x5A48, 0x5A28, 0x5930, 0x5900, 0x58F8, 0x5908, 0x5914, 0x5BA0, 0x5B80, 0x5B70, 0x58F4),
                GameLanguages.Japanese => new PtBagTouchSites(84, 0x59B8, 0x5998, 0x58A0, 0x5870, 0x5868, 0x5878, 0x5884, 0x5B10, 0x5AF0, 0x5AE0, 0x5864),
                GameLanguages.Korean => new PtBagTouchSites(84, 0x5A48, 0x5A28, 0x5930, 0x5900, 0x58F8, 0x5908, 0x5914, 0x5BA0, 0x5B80, 0x5B70, 0x58F4),
                _ => null,
            },
            _ => null,
        };

        public static PtBagTouchSites PtBagTouchSites => Here(PtBagTouchSitesIn);

        // trainer_case/main.c: the badge case's touch lists and the badges' and sparkles' sprite spots.
        public static PtTrainerCaseSites PtTrainerCaseSitesIn(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Platinum => language switch
            {
                GameLanguages.English => new PtTrainerCaseSites(71, 0x2464, 0x2380, 0x25FC, 0x25BC),
                GameLanguages.German => new PtTrainerCaseSites(71, 0x2464, 0x2380, 0x25FC, 0x25BC),
                GameLanguages.French => new PtTrainerCaseSites(71, 0x2464, 0x2380, 0x25FC, 0x25BC),
                GameLanguages.Italian => new PtTrainerCaseSites(71, 0x2464, 0x2380, 0x25FC, 0x25BC),
                GameLanguages.Spanish => new PtTrainerCaseSites(71, 0x2464, 0x2380, 0x25FC, 0x25BC),
                GameLanguages.Japanese => new PtTrainerCaseSites(71, 0x235C, 0x2278, 0x24F4, 0x24B4),
                GameLanguages.Korean => new PtTrainerCaseSites(71, 0x247C, 0x2398, 0x2614, 0x25D4),
                _ => null,
            },
            _ => null,
        };

        public static PtTrainerCaseSites PtTrainerCaseSites => Here(PtTrainerCaseSitesIn);

        // pokemon_summary_screen/subscreen.c: the three button rect lists in arm9 and their tile spot tables.
        public static PtSummaryTouchSites PtSummaryTouchSitesIn(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Platinum => language switch
            {
                GameLanguages.English => new PtSummaryTouchSites(0xF5188, 0xF5134, 0xF511C, 0xF5164, 0xF514C, 0xF5128),
                GameLanguages.German => new PtSummaryTouchSites(0xF51E0, 0xF518C, 0xF5174, 0xF51BC, 0xF51A4, 0xF5180),
                GameLanguages.French => new PtSummaryTouchSites(0xF5210, 0xF51BC, 0xF51A4, 0xF51EC, 0xF51D4, 0xF51B0),
                GameLanguages.Italian => new PtSummaryTouchSites(0xF51A4, 0xF5150, 0xF5138, 0xF5180, 0xF5168, 0xF5144),
                GameLanguages.Spanish => new PtSummaryTouchSites(0xF521C, 0xF51C8, 0xF51B0, 0xF51F8, 0xF51E0, 0xF51BC),
                GameLanguages.Japanese => new PtSummaryTouchSites(0xF4830, 0xF47DC, 0xF47C4, 0xF480C, 0xF47F4, 0xF47D0),
                GameLanguages.Korean => new PtSummaryTouchSites(0xF5D04, 0xF5CB0, 0xF5C98, 0xF5CE0, 0xF5CC8, 0xF5CA4),
                _ => null,
            },
            _ => null,
        };

        public static PtSummaryTouchSites PtSummaryTouchSites => Here(PtSummaryTouchSitesIn);

        // pc_boxes box_app_manager.c: sMainPcButtons, sComparePokemonButtons and sPokemonMarkingsButtons.
        public static PtPcTouchSites PtPcTouchSitesIn(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Platinum => language switch
            {
                GameLanguages.English => new PtPcTouchSites(19, 0xF076, 0xF082, 0xF092),
                GameLanguages.German => new PtPcTouchSites(19, 0xF082, 0xF08E, 0xF09E),
                GameLanguages.French => new PtPcTouchSites(19, 0xF082, 0xF08E, 0xF09E),
                GameLanguages.Italian => new PtPcTouchSites(19, 0xF082, 0xF08E, 0xF09E),
                GameLanguages.Spanish => new PtPcTouchSites(19, 0xF082, 0xF08E, 0xF09E),
                GameLanguages.Japanese => new PtPcTouchSites(19, 0xF01A, 0xF026, 0xF036),
                GameLanguages.Korean => new PtPcTouchSites(19, 0xF082, 0xF08E, 0xF09E),
                _ => null,
            },
            _ => null,
        };

        public static PtPcTouchSites PtPcTouchSites => Here(PtPcTouchSitesIn);

        // poketch: the frame's list in overlay 25 and each app's list in that app's overlay, counted in code; Korean moves the move tester's
        // by 4 and Japanese the Pokémon history's by -4.
        private static PoketchTouchSites Poketch(uint moveTester, uint history) => new PoketchTouchSites(25, 0x2180, new (int, uint)[]
        {
            (26, 0x558), (27, 0xDD8), (28, 0x1498), (29, 0x97C), (30, 0x4B8), (35, 0x560), (36, 0x558), (37, 0x414), (41, 0xBA8),
            (43, moveTester), (44, 0xAF4), (45, 0xAEC), (46, 0xF70), (50, 0x7D8), (53, 0x720), (54, history),
        });

        public static PoketchTouchSites PoketchTouchSitesIn(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Platinum => language switch
            {
                GameLanguages.English => Poketch(0x8D8, 0x520),
                GameLanguages.German => Poketch(0x8D8, 0x520),
                GameLanguages.French => Poketch(0x8D8, 0x520),
                GameLanguages.Italian => Poketch(0x8D8, 0x520),
                GameLanguages.Spanish => Poketch(0x8D8, 0x520),
                GameLanguages.Japanese => Poketch(0x8D8, 0x51C),
                GameLanguages.Korean => Poketch(0x8DC, 0x520),
                _ => null,
            },
            _ => null,
        };

        public static PoketchTouchSites PoketchTouchSites => Here(PoketchTouchSitesIn);

        // pokeheartgold options_app.c hitboxes, x coordinate tables and OptionsApp_SetupWindows' Quit and Confirm movs immediates,
        // measured on every HGSS dump. Japanese has no label table.
        public static HgssOptionsSites HgssOptionsSitesIn(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.HeartGold or GameVersions.SoulSilver => language switch
            {
                GameLanguages.English => new HgssOptionsSites(54, 0x1468, 0x142C, 0x15AC, 0x1400, 0x76A, 0x74E, 0x7AE, 0x792),
                GameLanguages.German => new HgssOptionsSites(54, 0x1468, 0x142C, 0x15AC, 0x1400, 0x76A, 0x74E, 0x7AE, 0x792),
                GameLanguages.French => new HgssOptionsSites(54, 0x1468, 0x142C, 0x15AC, 0x1400, 0x76A, 0x74E, 0x7AE, 0x792),
                GameLanguages.Italian => new HgssOptionsSites(54, 0x1468, 0x142C, 0x15AC, 0x1400, 0x76A, 0x74E, 0x7AE, 0x792),
                GameLanguages.Spanish => new HgssOptionsSites(54, 0x1468, 0x142C, 0x15AC, 0x1400, 0x76A, 0x74E, 0x7AE, 0x792),
                GameLanguages.Japanese => new HgssOptionsSites(54, 0x13B0, 0x1374, 0x14F4, 0, 0x712, 0x6F6, 0x756, 0x73A),
                GameLanguages.Korean => new HgssOptionsSites(54, 0x14A8, 0x146C, 0x15EC, 0x1440, 0x76A, 0x74E, 0x7AE, 0x792),
                _ => null,
            },
            _ => null,
        };

        public static HgssOptionsSites HgssOptionsSites => Here(HgssOptionsSitesIn);

        // pokeheartgold main_menu.c: sArrowButtonHitboxes, sNewGameButtonHitboxes 12 bytes on, and ov74_0223BC30.
        public static HgssMainMenuSites HgssMainMenuSitesIn(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English => new HgssMainMenuSites(74, 0x14260, 0x1426C, 0x14BD0),
                GameLanguages.German => new HgssMainMenuSites(74, 0x14260, 0x1426C, 0x14BD0),
                GameLanguages.French => new HgssMainMenuSites(74, 0x14260, 0x1426C, 0x14BD0),
                GameLanguages.Italian => new HgssMainMenuSites(74, 0x14260, 0x1426C, 0x14BD0),
                GameLanguages.Spanish => new HgssMainMenuSites(74, 0x14260, 0x1426C, 0x14BD0),
                GameLanguages.Japanese => new HgssMainMenuSites(74, 0x100C4, 0x100D0, 0x112F0),
                GameLanguages.Korean => new HgssMainMenuSites(74, 0x14254, 0x14260, 0x14BD0),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English => new HgssMainMenuSites(74, 0x14264, 0x14270, 0x14BD0),
                GameLanguages.German => new HgssMainMenuSites(74, 0x14264, 0x14270, 0x14BD0),
                GameLanguages.French => new HgssMainMenuSites(74, 0x14264, 0x14270, 0x14BD0),
                GameLanguages.Italian => new HgssMainMenuSites(74, 0x14264, 0x14270, 0x14BD0),
                GameLanguages.Spanish => new HgssMainMenuSites(74, 0x14264, 0x14270, 0x14BD0),
                GameLanguages.Japanese => new HgssMainMenuSites(74, 0x100C8, 0x100D4, 0x112F0),
                GameLanguages.Korean => new HgssMainMenuSites(74, 0x14258, 0x14264, 0x14BD0),
                _ => null,
            },
            _ => null,
        };

        public static HgssMainMenuSites HgssMainMenuSites => Here(HgssMainMenuSitesIn);

        // pokeheartgold overlay 51: the card's three rects, the button sprites' x and y bytes (ov51_021E80A4) and ov51_021E7F48.
        public static HgssTrainerCardSites HgssTrainerCardSitesIn(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.HeartGold or GameVersions.SoulSilver => language switch
            {
                GameLanguages.English => new HgssTrainerCardSites(51, 0x22F8, 0x25E4, 0x2488),
                GameLanguages.German => new HgssTrainerCardSites(51, 0x22F8, 0x25E4, 0x2488),
                GameLanguages.French => new HgssTrainerCardSites(51, 0x22F8, 0x25E4, 0x2488),
                GameLanguages.Italian => new HgssTrainerCardSites(51, 0x22F8, 0x25E4, 0x2488),
                GameLanguages.Spanish => new HgssTrainerCardSites(51, 0x22F8, 0x25E4, 0x2488),
                GameLanguages.Japanese => new HgssTrainerCardSites(51, 0x21E0, 0x24E4, 0x2370),
                GameLanguages.Korean => new HgssTrainerCardSites(51, 0x2308, 0x2604, 0x2498),
                _ => null,
            },
            _ => null,
        };

        public static HgssTrainerCardSites HgssTrainerCardSites => Here(HgssTrainerCardSitesIn);

        // pokeheartgold overlay 29: ov29_0225E248, ov29_0225E280, the sprite and label tables, and ov29_0225E1AC's digit lists, the same
        // in every HGSS dump.
        private static readonly NumberPadSites NumberPad = new NumberPadSites(29, 0xD28, 0xD60, 0xCFC, 0xC58, new uint[] { 0xBF5, 0xBFD, 0xC09, 0xC2C, 0xC40, 0xC70 });

        public static NumberPadSites NumberPadSitesIn(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.HeartGold or GameVersions.SoulSilver => language switch
            {
                GameLanguages.English => NumberPad,
                GameLanguages.German => NumberPad,
                GameLanguages.French => NumberPad,
                GameLanguages.Italian => NumberPad,
                GameLanguages.Spanish => NumberPad,
                GameLanguages.Japanese => NumberPad,
                GameLanguages.Korean => NumberPad,
                _ => null,
            },
            _ => null,
        };

        public static NumberPadSites NumberPadSites => Here(NumberPadSitesIn);
    }
}
