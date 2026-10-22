using System.Collections.Generic;
using System.Linq;
using static DSPRE.RomInfo;

namespace DSPRE
{
    public static partial class RomLocator
    {
        // A read checks each table's first window's background and first tile, so a dump these offsets are wrong for gets no layout.

        private static ScreenWindowName[] PartyPokemon(params int[] slots) => slots.SelectMany(n =>
            new[] { "name", "level", "HP", "HP bar", "comment" }
                .Select(part => new ScreenWindowName(ROMFiles.ScreenWindowLayout.PokemonWindow(n, part), n, part))).ToArray();

        private static ScreenWindowName[] Named(IEnumerable<string> names) => names.Select(n => new ScreenWindowName(n)).ToArray();

        private static IEnumerable<string> Numbered(string what, int from, int count) =>
            Enumerable.Range(from, count).Select(i => $"{what} {i}");

        private static readonly string[] PartyText =
            { "Cancel button", "Confirm button", "Short message", "Medium message", "Long message", "Item menu", "Move list" };

        // The party tables sit at fixed distances from a constant table: pokeplatinum main.c Unk_020F1BD4, pokeheartgold party_menu.c
        // sFieldMoves. Measured on every Pt and HGSS dump.
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

        // pokeheartgold party_menu.c _02110104 (Frontier) and _02110128 (single and multi), 0x24 bytes apart.
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
            // pokeheartgold src/party_context_menu.c.
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
        /// Tables that place each Pokémon in the single layout: panels, cursor stops, status icon sprites and HGSS touch areas.
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

        // pokeheartgold party_menu.c and party_menu_list_items.c; same code in every HGSS dump, at different addresses.
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

        // pokeheartgold party_context_menu.c PartyMenu_DrawMonHpBarOnWindow, the movs r2, #48 before CalculateHpBarPixelsLength. Measured
        // on every HGSS dump.
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

        // pokeheartgold a/0/2/1. The background's message box frame starts on row 18.
        public static PartyGraphicsMembers PartyGraphicsMembersIn(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.HeartGold or GameVersions.SoulSilver => new PartyGraphicsMembers(6, 7, 22, 17, 18),
            _ => null,
        };

        public static PartyGraphicsMembers PartyGraphicsMembers => Here(PartyGraphicsMembersIn);

        // pokeplatinum party_menu main.c, pokeheartgold party_menu.c sub_0207A174.
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

        // pokeheartgold overlay 14. One table: the top screen details, then (not listed yet) moves, item text and menus, then the messages.
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

        /// <summary>Null where they aren't known for this ROM.</summary>
        public static ScreenWindowTableSite[] ScreenWindowSites(string screen) => screen switch
        {
            "party" => Here(PartyWindowSitesIn),
            "box" => Here(BoxWindowSitesIn),
            _ => null,
        };
    }
}
