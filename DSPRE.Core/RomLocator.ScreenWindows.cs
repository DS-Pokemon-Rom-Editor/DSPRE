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
            // pokeplatinum main.c sMemberPanelTemplates; sCursorPosTable_Basic, _SelectOrder, _SelectEgg; sprites.c sSpriteTemplates;
            // the ball buttons' TouchScreenPartyBalls and sTouchScreenButtonCoords.
            if (game == GameVersions.Platinum)
                return new PartyPanelSites(a + 0x224, new[] { a + 0x1E4, a + 0x164, a + 0x1A4 }, a + 0x4EC, System.Array.Empty<uint>(),
                                           a + 0xC0, a - 0x1C);
            if (HgssPartyTouch(game, language) is not uint t) return null;
            // pokeheartgold party_menu.c _0210150C; cursor tables _0210140C, _0210148C, _021014CC; party_menu_sprites.c sSpriteTemplates.
            return new PartyPanelSites(a + 0x148, new[] { a + 0x48, a + 0xC8, a + 0x108 }, a + 0x534, new[] { t + 0x24, t });
        }

        public static PartyPanelSites PartyPanelSites => Here(PartyPanelSitesIn);

        /// <summary>The party cursor tables (normal, multi battle, battle entry, egg choice) and the back rows.</summary>
        public static PartyCursorSites PartyCursorSitesIn(GameVersions game, GameLanguages language, int revision)
        {
            if (PartyAnchor(game, language) is not uint a) return null;
            // pokeplatinum main.c sCursorPosTable_Basic, _MultiBattle, _SelectOrder, _SelectEgg; the back rows are the anchor itself.
            if (game == GameVersions.Platinum) return new PartyCursorSites(new[] { a + 0x1E4, a + 0x124, a + 0x164, a + 0x1A4 }, a);
            // pokeheartgold party_menu.c _0210140C, _0210144C, _0210148C, _021014CC; back rows _021012CC.
            return new PartyCursorSites(new[] { a + 0x48, a + 0x88, a + 0xC8, a + 0x108 }, a - 0xF8);
        }

        public static PartyCursorSites PartyCursorSites => Here(PartyCursorSitesIn);

        // pokeplatinum battle_sub_menus (overlay 13) and pokeheartgold overlay 8: sScreenCursorPositions / ov08_022254BC. Every other
        // battle party and bag table sits at a fixed distance from it, measured on every Pt and HGSS dump.
        public static BattleMenuSites BattleMenuSitesIn(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Platinum => language switch
            {
                GameLanguages.English => new BattleMenuSites(13, 0x976C),
                GameLanguages.German => new BattleMenuSites(13, 0x976C),
                GameLanguages.French => new BattleMenuSites(13, 0x976C),
                GameLanguages.Italian => new BattleMenuSites(13, 0x976C),
                GameLanguages.Spanish => new BattleMenuSites(13, 0x976C),
                GameLanguages.Japanese => new BattleMenuSites(13, 0x9734),
                GameLanguages.Korean => new BattleMenuSites(13, 0x9770),
                _ => null,
            },
            GameVersions.HeartGold or GameVersions.SoulSilver => language switch
            {
                GameLanguages.English => new BattleMenuSites(8, 0x969C),
                GameLanguages.German => new BattleMenuSites(8, 0x969C),
                GameLanguages.French => new BattleMenuSites(8, 0x969C),
                GameLanguages.Italian => new BattleMenuSites(8, 0x969C),
                GameLanguages.Spanish => new BattleMenuSites(8, 0x969C),
                GameLanguages.Japanese => new BattleMenuSites(8, 0x9674),
                GameLanguages.Korean => new BattleMenuSites(8, 0x9698),
                _ => null,
            },
            _ => null,
        };

        public static BattleMenuSites BattleMenuSites => Here(BattleMenuSitesIn);

        // pokeheartgold bag_app_main.c sContextMenuDPadDestinations in overlay 15, measured on every HGSS dump.
        public static BagButtonSites BagButtonSitesIn(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.HeartGold or GameVersions.SoulSilver => language switch
            {
                GameLanguages.English => new BagButtonSites(15, 0x71A8),
                GameLanguages.German => new BagButtonSites(15, 0x71A8),
                GameLanguages.French => new BagButtonSites(15, 0x71A8),
                GameLanguages.Italian => new BagButtonSites(15, 0x71A8),
                GameLanguages.Spanish => new BagButtonSites(15, 0x71A8),
                GameLanguages.Japanese => new BagButtonSites(15, 0x7130),
                GameLanguages.Korean => new BagButtonSites(15, 0x71A8),
                _ => null,
            },
            _ => null,
        };

        public static BagButtonSites BagButtonSites => Here(BagButtonSitesIn);

        // pokeplatinum battle_subscreen.c sSelectTargetMenuButtonLayout (overlay 16) and its HGSS twin in overlay 12, measured on
        // every Pt and HGSS dump.
        public static BattleCommandSites BattleCommandSitesIn(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Platinum => language switch
            {
                GameLanguages.English => new BattleCommandSites(16, 0x3509C),
                GameLanguages.German => new BattleCommandSites(16, 0x350B8),
                GameLanguages.French => new BattleCommandSites(16, 0x351A4),
                GameLanguages.Italian => new BattleCommandSites(16, 0x350B8),
                GameLanguages.Spanish => new BattleCommandSites(16, 0x351A4),
                GameLanguages.Japanese => new BattleCommandSites(16, 0x35090),
                GameLanguages.Korean => new BattleCommandSites(16, 0x350B4),
                _ => null,
            },
            GameVersions.HeartGold or GameVersions.SoulSilver => language switch
            {
                GameLanguages.English => new BattleCommandSites(12, 0x36950),
                GameLanguages.German => new BattleCommandSites(12, 0x36950),
                GameLanguages.French => new BattleCommandSites(12, 0x36950),
                GameLanguages.Italian => new BattleCommandSites(12, 0x36950),
                GameLanguages.Spanish => new BattleCommandSites(12, 0x36950),
                GameLanguages.Japanese => new BattleCommandSites(12, 0x3690C),
                GameLanguages.Korean => new BattleCommandSites(12, 0x36954),
                _ => null,
            },
            _ => null,
        };

        public static BattleCommandSites BattleCommandSites => Here(BattleCommandSitesIn);

        // pokeheartgold overlay 14 ov14_021F8B10, the eleven PC layouts, measured on every HGSS dump.
        public static PcBoxSites PcBoxSitesIn(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.HeartGold or GameVersions.SoulSilver => language switch
            {
                GameLanguages.English => new PcBoxSites(14, 0x13210),
                GameLanguages.German => new PcBoxSites(14, 0x13210),
                GameLanguages.French => new PcBoxSites(14, 0x13210),
                GameLanguages.Italian => new PcBoxSites(14, 0x13210),
                GameLanguages.Spanish => new PcBoxSites(14, 0x13210),
                GameLanguages.Japanese => new PcBoxSites(14, 0x130D4),
                GameLanguages.Korean => new PcBoxSites(14, 0x13210),
                _ => null,
            },
            _ => null,
        };

        public static PcBoxSites PcBoxSites => Here(PcBoxSitesIn);

        // pokeheartgold party_context_menu.c: sButtonRects and sButtonWindowIDs off the party anchor, the hitboxes and arrow tables
        // off the party touch site, measured on every HGSS dump.
        public static PartyMenuSites PartyMenuSitesIn(GameVersions game, GameLanguages language, int revision) =>
            game is GameVersions.HeartGold or GameVersions.SoulSilver && PartyAnchor(game, language) is uint a && HgssPartyTouch(game, language) is uint t
                ? new PartyMenuSites(a, t) : null;

        public static PartyMenuSites PartyMenuSites => Here(PartyMenuSitesIn);

        // pokeheartgold overlay 27 ov27_0225CF68, ov27_0225D120, ov27_0225D118, ov27_0225D480 and ov27_0225D3C4, measured on every HGSS dump.
        public static TouchMenuSites TouchMenuSitesIn(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.HeartGold or GameVersions.SoulSilver => language switch
            {
                GameLanguages.English => new TouchMenuSites(27, 0x2FE8, 0x31A0, 0x3198, 0x3500, 0x3444),
                GameLanguages.German => new TouchMenuSites(27, 0x2FE8, 0x31A0, 0x3198, 0x3500, 0x3444),
                GameLanguages.French => new TouchMenuSites(27, 0x2FE8, 0x31A0, 0x3198, 0x3500, 0x3444),
                GameLanguages.Italian => new TouchMenuSites(27, 0x2FE8, 0x31A0, 0x3198, 0x3500, 0x3444),
                GameLanguages.Spanish => new TouchMenuSites(27, 0x2FE8, 0x31A0, 0x3198, 0x3500, 0x3444),
                GameLanguages.Japanese => new TouchMenuSites(27, 0x3118, 0x32C8, 0x32C0, 0x3620, 0x356C),
                GameLanguages.Korean => new TouchMenuSites(27, 0x2FE8, 0x31A0, 0x3198, 0x3500, 0x3444),
                _ => null,
            },
            _ => null,
        };

        public static TouchMenuSites TouchMenuSites => Here(TouchMenuSitesIn);

        // pokeheartgold asm/unk_02088288.s, the summary's Cancel rect in arm9.bin, measured on every HGSS dump.
        public static SummaryTouchSites SummaryTouchSitesIn(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English => new SummaryTouchSites(0x1038AC),
                GameLanguages.German => new SummaryTouchSites(0x103860),
                GameLanguages.French => new SummaryTouchSites(0x103890),
                GameLanguages.Italian => new SummaryTouchSites(0x103824),
                GameLanguages.Spanish => new SummaryTouchSites(0x103894),
                GameLanguages.Japanese => new SummaryTouchSites(0x103044),
                GameLanguages.Korean => new SummaryTouchSites(0x10404C),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English => new SummaryTouchSites(0x1038AC),
                GameLanguages.German => new SummaryTouchSites(0x103860),
                GameLanguages.French => new SummaryTouchSites(0x103890),
                GameLanguages.Italian => new SummaryTouchSites(0x103824),
                GameLanguages.Spanish => new SummaryTouchSites(0x10389C),
                GameLanguages.Japanese => new SummaryTouchSites(0x103044),
                GameLanguages.Korean => new SummaryTouchSites(0x104044),
                _ => null,
            },
            _ => null,
        };

        public static SummaryTouchSites SummaryTouchSites => Here(SummaryTouchSitesIn);

        // pokeheartgold src/application/pokegear: the bar's sTouchscreenButtonHitboxes (overlay 100, the same offset everywhere) and
        // the map's area rects in overlay 101, and the radio's Radio_ToggleChannelSelectionButtons coords, measured on every HGSS dump.
        public static PokegearSites PokegearSitesIn(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.HeartGold or GameVersions.SoulSilver => language switch
            {
                GameLanguages.English => new PokegearSites(100, 0x1BC4, 101, 0x10764, false, 0x13B88),
                GameLanguages.German => new PokegearSites(100, 0x1BC4, 101, 0x10770, false, 0x13B88),
                GameLanguages.French => new PokegearSites(100, 0x1BC4, 101, 0x10770, false, 0x13B88),
                GameLanguages.Italian => new PokegearSites(100, 0x1BC4, 101, 0x1076C, false, 0x13B88),
                GameLanguages.Spanish => new PokegearSites(100, 0x1BC4, 101, 0x10770, false, 0x13B88),
                GameLanguages.Japanese => new PokegearSites(100, 0x1BC4, 101, 0x106FC, true, 0x13B08),
                GameLanguages.Korean => new PokegearSites(100, 0x1BC4, 101, 0x10774, false, 0x13B88),
                _ => null,
            },
            _ => null,
        };

        public static PokegearSites PokegearSites => Here(PokegearSitesIn);

        // pokeheartgold overlay 18, the eight search pages in PokedexButtons order: search, order, name, type, height, weight, area,
        // shape. Measured on every HGSS dump; Japanese and Korean keep them in another order.
        private static readonly uint[] DexTouchUs = { 0x15F78, 0x15F28, 0x16194, 0x16140, 0x15DC8, 0x15DDC, 0x15E44, 0x160A8 };
        private static readonly uint[] DexFlowUs = { 0x160F0, 0x16068, 0x16334, 0x16294, 0x15E80, 0x15EA0, 0x15FD4, 0x1620C };
        private static readonly uint[] DexTouchJp = { 0x15B84, 0x15B34, 0x15EC8, 0x15D4C, 0x159D4, 0x159FC, 0x15A50, 0x15CB4 };
        private static readonly uint[] DexFlowJp = { 0x15CFC, 0x15C74, 0x15F88, 0x15E28, 0x15AAC, 0x15A6C, 0x15BE0, 0x15DA0 };
        private static readonly uint[] DexTouchKr = { 0x15C40, 0x15BCC, 0x15DB8, 0x15E50, 0x15A90, 0x15ACC, 0x15B0C, 0x15D70 };
        private static readonly uint[] DexFlowKr = { 0x15E00, 0x15D30, 0x15F2C, 0x15FB4, 0x15B68, 0x15B28, 0x15C9C, 0x15EA4 };
        // The touch-only lists: top page, list page, height and weight arrows, detail tabs, area page, forms list, forms compare,
        // detail return.
        private static readonly uint[] DexListsUs = { 0x15E2C, 0x15FA4, 0x15E18, 0x15E04, 0x16034, 0x15F04, 0x15EE0, 0x15D30 };
        private static readonly uint[] DexListsJp = { 0x15A38, 0x15BB0, 0x15A24, 0x15A10, 0x15C40, 0x15B10, 0x15AEC, 0x1593C };
        private static readonly uint[] DexListsKr = { 0x15AF4, 0x15C6C, 0x15AE0, 0x15AB8, 0x15CFC, 0x15BA8, 0x15BF0, 0x159F8 };

        // ov18_021F9FDC's window templates and the ov18_021F9780, ov18_021F990C and ov18_021F9770 highlights; their distance from
        // the touch tables differs by language.
        private static readonly uint[] DexArtUs = { 0x146DC, 0x13E80, 0x1400C, 0x13E70 };
        private static readonly uint[] DexArtJp = { 0x14358, 0x13B0C, 0x13C98, 0x13AEC };
        private static readonly uint[] DexArtKr = { 0x1440C, 0x13BBC, 0x13D48, 0x13B9C };

        private static PokedexSearchSites Dex(uint[] touch, uint[] flow, uint[] lists, int shift, uint[] art, int artShift, uint flash) =>
            new PokedexSearchSites(18, touch.Select(t => (uint)(t + shift)).ToArray(), flow.Select(f => (uint)(f + shift)).ToArray(),
                                   lists.Select(l => (uint)(l + shift)).ToArray(), art.Select(a => (uint)(a + artShift)).ToArray(), flash);

        public static PokedexSearchSites PokedexSearchSitesIn(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English => Dex(DexTouchUs, DexFlowUs, DexListsUs, 0, DexArtUs, 0, 0x44DA),
                GameLanguages.German => Dex(DexTouchUs, DexFlowUs, DexListsUs, -0x3E4, DexArtUs, -0x37C, 0x44CE),
                GameLanguages.French => Dex(DexTouchUs, DexFlowUs, DexListsUs, -0x3E4, DexArtUs, -0x37C, 0x44CE),
                GameLanguages.Italian => Dex(DexTouchUs, DexFlowUs, DexListsUs, -0x3E4, DexArtUs, -0x37C, 0x44CE),
                GameLanguages.Spanish => Dex(DexTouchUs, DexFlowUs, DexListsUs, -0x3E4, DexArtUs, -0x37C, 0x44CE),
                GameLanguages.Japanese => Dex(DexTouchJp, DexFlowJp, DexListsJp, 0, DexArtJp, 0, 0x44A6),
                GameLanguages.Korean => Dex(DexTouchKr, DexFlowKr, DexListsKr, 0, DexArtKr, 0, 0x452E),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English => Dex(DexTouchUs, DexFlowUs, DexListsUs, -4, DexArtUs, -4, 0x44D6),
                GameLanguages.German => Dex(DexTouchUs, DexFlowUs, DexListsUs, -0x3E8, DexArtUs, -0x380, 0x44CA),
                GameLanguages.French => Dex(DexTouchUs, DexFlowUs, DexListsUs, -0x3E8, DexArtUs, -0x380, 0x44CA),
                GameLanguages.Italian => Dex(DexTouchUs, DexFlowUs, DexListsUs, -0x3E8, DexArtUs, -0x380, 0x44CA),
                GameLanguages.Spanish => Dex(DexTouchUs, DexFlowUs, DexListsUs, -0x3E8, DexArtUs, -0x380, 0x44CA),
                GameLanguages.Japanese => Dex(DexTouchJp, DexFlowJp, DexListsJp, 0, DexArtJp, 0, 0x44A6),
                GameLanguages.Korean => Dex(DexTouchKr, DexFlowKr, DexListsKr, 4, DexArtKr, 4, 0x4532),
                _ => null,
            },
            _ => null,
        };

        public static PokedexSearchSites PokedexSearchSites => Here(PokedexSearchSitesIn);

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

        // pokeplatinum overlay 5 map_name_popup.c, pokeheartgold overlay 1 draw_map_name.c. Measured on every Platinum and HGSS dump.
        private static readonly PlaceNameBannerSites BannerHgWest = new PlaceNameBannerSites(1, 0x9EC0, 0x9F7C, 0x9FB8, 0xA046, 0xA154, 0x9F48);
        private static readonly PlaceNameBannerSites BannerHgJp = new PlaceNameBannerSites(1, 0x9D78, 0x9E34, 0x9E70, 0x9EFE, 0xA00C, 0x9E00);

        public static PlaceNameBannerSites PlaceNameBannerSitesIn(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Platinum => language switch
            {
                GameLanguages.English => new PlaceNameBannerSites(5, 0xC99C, 0xCAAA, 0xCAE6, 0xCB72, 0xCC80, 0),
                GameLanguages.German => new PlaceNameBannerSites(5, 0xCA00, 0xCB0E, 0xCB4A, 0xCBD6, 0xCCE4, 0),
                GameLanguages.French => new PlaceNameBannerSites(5, 0xC9EC, 0xCAFA, 0xCB36, 0xCBC2, 0xCCD0, 0),
                GameLanguages.Italian => new PlaceNameBannerSites(5, 0xC9EC, 0xCAFA, 0xCB36, 0xCBC2, 0xCCD0, 0),
                GameLanguages.Spanish => new PlaceNameBannerSites(5, 0xC9EC, 0xCAFA, 0xCB36, 0xCBC2, 0xCCD0, 0),
                GameLanguages.Japanese => new PlaceNameBannerSites(5, 0xC8E0, 0xC9EE, 0xCA2A, 0xCAB6, 0xCBC4, 0),
                GameLanguages.Korean => new PlaceNameBannerSites(5, 0xC9E0, 0xCAEE, 0xCB2A, 0xCBB6, 0xCCC4, 0),
                _ => null,
            },
            GameVersions.HeartGold or GameVersions.SoulSilver => language switch
            {
                GameLanguages.English => BannerHgWest,
                GameLanguages.German => BannerHgWest,
                GameLanguages.French => BannerHgWest,
                GameLanguages.Italian => BannerHgWest,
                GameLanguages.Spanish => BannerHgWest,
                GameLanguages.Japanese => BannerHgJp,
                GameLanguages.Korean => BannerHgWest,
                _ => null,
            },
            _ => null,
        };

        public static PlaceNameBannerSites PlaceNameBannerSites => Here(PlaceNameBannerSitesIn);
    }
}
