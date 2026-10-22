using System.Collections.Generic;
using System.Linq;
using static DSPRE.RomInfo;

namespace DSPRE
{
    public static partial class RomLocator
    {
        // The window templates the party and PC box screens keep in plain tables. A read checks each table's first window's
        // background and first tile, which a move never changes, so a dump these are wrong for gets no layout rather than a
        // bad one.

        private static ScreenWindowName[] PartyPokemon(params int[] slots) => slots.SelectMany(n =>
            new[] { "name", "level", "HP", "HP bar", "comment" }
                .Select(part => new ScreenWindowName(ROMFiles.ScreenWindowLayout.PokemonWindow(n, part), n, part))).ToArray();

        private static ScreenWindowName[] Named(IEnumerable<string> names) => names.Select(n => new ScreenWindowName(n)).ToArray();

        private static IEnumerable<string> Numbered(string what, int from, int count) =>
            Enumerable.Range(from, count).Select(i => $"{what} {i}");

        private static readonly string[] PartyText =
            { "Cancel button", "Confirm button", "Short message", "Medium message", "Long message", "Item menu", "Move list" };

        // The party menu's tables in arm9 sit at fixed distances from a constant table beside them: Platinum's slot order for
        // leaving the buttons (pokeplatinum main.c Unk_020F1BD4), HGSS's field move list (pokeheartgold party_menu.c
        // sFieldMoves). Measured on every Platinum, HeartGold and SoulSilver dump.
        private static uint? PartyAnchor(GameVersions game, GameLanguages language) => game switch
        {
            GameVersions.Platinum => language switch
            {
                GameLanguages.English => 0xF1BD4,
                GameLanguages.German => 0xF1C2C,
                GameLanguages.French => 0xF1C5C,
                GameLanguages.Italian => 0xF1BF0,
                GameLanguages.Spanish => 0xF1C68,
                GameLanguages.Japanese => 0xF1264,
                GameLanguages.Korean => 0xF265C,
                _ => null,
            },
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English => 0x1013C4,
                GameLanguages.German => 0x101378,
                GameLanguages.French => 0x1013A8,
                GameLanguages.Italian => 0x10133C,
                GameLanguages.Spanish => 0x1013AC,
                GameLanguages.Japanese => 0x100B44,
                GameLanguages.Korean => 0x101A70,
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English => 0x1013C4,
                GameLanguages.German => 0x101378,
                GameLanguages.French => 0x1013A8,
                GameLanguages.Italian => 0x10133C,
                GameLanguages.Spanish => 0x1013B4,
                GameLanguages.Japanese => 0x100B44,
                GameLanguages.Korean => 0x101A68,
                _ => null,
            },
            _ => null,
        };

        // HGSS's party touch areas are in .data, away from the anchor: the Frontier list (pokeheartgold party_menu.c
        // _02110104), then 0x24 bytes on the single and multi ones (_02110128).
        private static uint? HgssPartyTouch(GameVersions game, GameLanguages language) => game switch
        {
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English => 0x110104,
                GameLanguages.German => 0x1100E4,
                GameLanguages.French => 0x110124,
                GameLanguages.Italian => 0x1100A4,
                GameLanguages.Spanish => 0x110124,
                GameLanguages.Japanese => 0x10F638,
                GameLanguages.Korean => 0x110AE4,
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English => 0x110104,
                GameLanguages.German => 0x1100E4,
                GameLanguages.French => 0x110124,
                GameLanguages.Italian => 0x1100A4,
                GameLanguages.Spanish => 0x110144,
                GameLanguages.Japanese => 0x10F638,
                GameLanguages.Korean => 0x110AE4,
                _ => null,
            },
            _ => null,
        };

        public static ScreenWindowTableSite[] PartyWindowSitesIn(GameVersions game, GameLanguages language, int revision)
        {
            if (PartyAnchor(game, language) is not uint a) return null;
            // pokeplatinum src/applications/party_menu/windows.c: sTextWindows, sMemberWindowTemplates and the multi battle ones.
            if (game == GameVersions.Platinum)
                return new ScreenWindowTableSite[]
                {
                    new("Messages and buttons", -1, a + 0x2FC, Named(PartyText), 1, 0x160),
                    new("Pokémon", -1, a + 0x334, PartyPokemon(1, 2, 3, 4, 5, 6), 1, 0x28),
                    new("Pokémon in a multi battle", -1, a + 0x424, PartyPokemon(2, 4, 6), 1, 0x5C),
                };
            // pokeheartgold src/party_context_menu.c; the touch buttons replace Platinum's list menu.
            return new ScreenWindowTableSite[]
            {
                new("Messages and buttons", -1, a + 0x258, Named(PartyText.Concat(Numbered("Menu button", 1, 7)).Append("Quit button")
                    .Concat(Numbered("Submenu button", 1, 4))), 1, 0x180),
                new("Pokémon", -1, a + 0x370, PartyPokemon(1, 2, 3, 4, 5, 6), 1, 0x48),
                new("Pokémon in a multi battle", -1, a + 0x460, PartyPokemon(2, 4, 6), 1, 0x7C),
                new("Top screen", -1, a + 0x4D8, Named(new[] { "Name", "Item heading", "Held item" }), 6, 0x1),
            };
        }

        /// <summary>
        /// The party menu's tables that place each Pokémon in the single layout: panels, the cursor tables that stop on them, the status
        /// icons' sprite templates and, in HGSS, the touch areas.
        /// </summary>
        public static PartyPanelSites PartyPanelSitesIn(GameVersions game, GameLanguages language, int revision)
        {
            if (PartyAnchor(game, language) is not uint a) return null;
            // pokeplatinum main.c sMemberPanelTemplates; sCursorPosTable_Basic, _SelectOrder, _SelectEgg; sprites.c sSpriteTemplates.
            if (game == GameVersions.Platinum)
                return new PartyPanelSites(a + 0x224, new[] { a + 0x1E4, a + 0x164, a + 0x1A4 }, a + 0x4EC, System.Array.Empty<uint>());
            if (HgssPartyTouch(game, language) is not uint t) return null;
            // pokeheartgold party_menu.c _0210150C; cursor tables _0210140C, _0210148C, _021014CC; party_menu_sprites.c sSpriteTemplates.
            return new PartyPanelSites(a + 0x148, new[] { a + 0x48, a + 0xC8, a + 0x108 }, a + 0x534, new[] { t + 0x24, t });
        }

        public static PartyPanelSites PartyPanelSites => Here(PartyPanelSitesIn);

        // pokeheartgold party_menu.c sub_0207A16C (with sub_0207A174 after it), sub_0207A780, sub_0207CAAC; party_menu_list_items.c
        // sub_0207FC1C, sub_0207FD6C, PartyMenu_SwapMonsPanelsStep, PartyMenu_RedrawMonHpBarAfterSwap. Measured on every
        // HeartGold and SoulSilver dump; the code is the same in all of them, only its place moves.
        public static PartyPanelCodeSites PartyPanelCodeSitesIn(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English or GameLanguages.German or GameLanguages.French or GameLanguages.Italian => PartyCodeWest,
                GameLanguages.Spanish => new PartyPanelCodeSites(0x207A164, 0x207A778, 0x207CAA4, 0x207FC14, 0x207FD64, 0x207FEE0, 0x2080474),
                GameLanguages.Japanese => PartyCodeJapanese,
                GameLanguages.Korean => new PartyPanelCodeSites(0x207A270, 0x207A884, 0x207CBB0, 0x207FD20, 0x207FE70, 0x207FFEC, 0x2080580),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English or GameLanguages.German or GameLanguages.French or GameLanguages.Italian => PartyCodeWest,
                GameLanguages.Spanish => PartyCodeWest,
                GameLanguages.Japanese => PartyCodeJapanese,
                GameLanguages.Korean => new PartyPanelCodeSites(0x207A268, 0x207A87C, 0x207CBA8, 0x207FD18, 0x207FE68, 0x207FFE4, 0x2080578),
                _ => null,
            },
            _ => null,
        };

        private static readonly PartyPanelCodeSites PartyCodeWest =
            new PartyPanelCodeSites(0x207A16C, 0x207A780, 0x207CAAC, 0x207FC1C, 0x207FD6C, 0x207FEE8, 0x208047C);
        private static readonly PartyPanelCodeSites PartyCodeJapanese =
            new PartyPanelCodeSites(0x2079C00, 0x207A214, 0x207C530, 0x207F674, 0x207F7C4, 0x207F940, 0x207FED4);

        public static PartyPanelCodeSites PartyPanelCodeSites => Here(PartyPanelCodeSitesIn);

        // pokeheartgold party_context_menu.c PartyMenu_DrawMonHpBarOnWindow: the movs r2, #48 before CalculateHpBarPixelsLength, at
        // +0x9C. Measured on every HeartGold and SoulSilver dump.
        public static PartyHpBarSites PartyHpBarSitesIn(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English or GameLanguages.German or GameLanguages.French or GameLanguages.Italian => HpBarAt(0x207D4EC),
                GameLanguages.Spanish => HpBarAt(0x207D4E4),
                GameLanguages.Japanese => HpBarAt(0x207CF70),
                GameLanguages.Korean => HpBarAt(0x207D5F0),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English or GameLanguages.German or GameLanguages.French or GameLanguages.Italian => HpBarAt(0x207D4EC),
                GameLanguages.Spanish => HpBarAt(0x207D4EC),
                GameLanguages.Japanese => HpBarAt(0x207CF70),
                GameLanguages.Korean => HpBarAt(0x207D5E8),
                _ => null,
            },
            _ => null,
        };

        private static PartyHpBarSites HpBarAt(uint function) => new PartyHpBarSites(function + 0x9C);

        public static PartyHpBarSites PartyHpBarSites => Here(PartyHpBarSitesIn);

        // pokeheartgold a/0/2/1 (party menu graphics): cursor cells 6 and tiles 7, panel art 22, background 17, whose
        // message box frame starts on row 18.
        public static PartyGraphicsMembers PartyGraphicsMembersIn(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.HeartGold or GameVersions.SoulSilver => new PartyGraphicsMembers(6, 7, 22, 17, 18),
            _ => null,
        };

        public static PartyGraphicsMembers PartyGraphicsMembers => Here(PartyGraphicsMembersIn);

        // The tile an egg's HP bar strip is blanked with: pokeplatinum party_menu main.c, pokeheartgold party_menu.c sub_0207A174.
        public static int PartyStripTileIn(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Platinum => 0x17,
            GameVersions.HeartGold or GameVersions.SoulSilver => 0x4B,
            _ => 0,
        };

        public static int PartyStripTile => RomID == null ? 0 : PartyStripTileIn(GameVersion, GameLanguage, RomRevision);

        // pokeplatinum src/applications/pc_boxes/ov19_021DA92C.c and ov19_021DB2FC.c, in overlay 19.
        private static readonly ScreenWindowTableSite[] PlatinumBox =
        {
            new("Box", 19, 0xF458, Named(new[] { "Species", "Dex number", "Nickname", "Held item and more", "Message", "Menu" }), 2, 0x120),
        };

        // pokeheartgold overlay 14, the start of one table: the picked Pokémon's details on the top screen, then (not listed
        // yet) its moves, the item text and the menus, then the messages. The menu buttons are placed by the code.
        private static readonly ScreenWindowTableSite[] HgssBox =
        {
            new("Top screen", 14, 0x12BB4, Named(new[]
            {
                "Species", "Nickname", "Level", "Title", "Gender", "Dex number", "Nature", "Ability", "Held item",
            }), 4, 0x69),
            new("Messages", 14, 0x12CDC, Named(new[] { "Message", "Message at the top", "Short message", "Second message", "Yes/no" }), 0, 0x2B3),
        };

        // Measured on the US dumps only.
        public static ScreenWindowTableSite[] BoxWindowSitesIn(GameVersions game, GameLanguages language, int revision) =>
            language != GameLanguages.English ? null : game switch
            {
                GameVersions.Platinum => PlatinumBox,
                GameVersions.HeartGold or GameVersions.SoulSilver => HgssBox,
                _ => null,
            };

        /// <summary>The window tables of a screen the Screens editor can lay out, or null where they aren't known.</summary>
        public static ScreenWindowTableSite[] ScreenWindowSites(string screen) => screen switch
        {
            "party" => Here(PartyWindowSitesIn),
            "box" => Here(BoxWindowSitesIn),
            _ => null,
        };
    }
}
