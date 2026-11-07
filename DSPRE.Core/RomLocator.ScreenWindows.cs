using System;
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

        // The Confirm and Cancel button sprites' s16 x and y in sSpriteTemplates, and the code that puts Cancel at row 22 in the
        // confirm and Frontier modes (pokeplatinum windows.c PartyMenu_AddWindows, pokeheartgold party_context_menu.c).
        private static ScreenWindowFollow[] PartyButtons(uint a, GameVersions game, GameLanguages language)
        {
            bool pt = game == GameVersions.Platinum;
            uint? row22 = pt
                ? language switch
                {
                    GameLanguages.English => 0x81AE6, GameLanguages.Japanese => 0x813B6, GameLanguages.Korean => 0x81FD2,
                    GameLanguages.French or GameLanguages.German or GameLanguages.Italian or GameLanguages.Spanish => 0x81B86,
                    _ => null,
                }
                : language switch
                {
                    GameLanguages.Japanese => 0x7C942,
                    GameLanguages.Korean => game == GameVersions.HeartGold ? 0x7CFC2u : 0x7CFBAu,
                    GameLanguages.Spanish when game == GameVersions.HeartGold => 0x7CEB6,
                    GameLanguages.English or GameLanguages.French or GameLanguages.German or GameLanguages.Italian or GameLanguages.Spanish => 0x7CEBE,
                    _ => null,
                };
            uint confirm = a + (pt ? 0x540u : 0x588u), cancel = a + (pt ? 0x568u : 0x5B0u);
            ScreenWindowSpot Sprite(uint at, int add) => new ScreenWindowSpot(at, Add: add, Scale: 8, Half: true);
            ScreenWindowSpot[] cancelY = row22 is uint r
                ? new[] { Sprite(cancel + 2, -16), new ScreenWindowSpot(r, Add: -1) }
                : new[] { Sprite(cancel + 2, -16) };
            List<ScreenWindowSpot> cancelX = new List<ScreenWindowSpot> { Sprite(cancel, -24) }, confirmX = new List<ScreenWindowSpot> { Sprite(confirm, -32) };
            List<ScreenWindowSpot> cancelRows = cancelY.ToList(), confirmY = new List<ScreenWindowSpot> { Sprite(confirm + 2, -8) };
            // HGSS touch rects (u8 top, bottom, left, right): entry 6 is Cancel and 7 Confirm in party_menu.c _02110104, and entry 6
            // is Cancel in both rows of _02110128. The right edges stay at the screen's edge.
            if (!pt && HgssPartyTouch(game, language) is uint t)
            {
                ScreenWindowSpot Edge(uint at, int add) => new ScreenWindowSpot(at, Add: add, Scale: 8, Byte: true);
                cancelX.Add(Edge(t + 26, 8));
                cancelRows.AddRange(new[] { Edge(t + 24, -8), Edge(t + 25, -24) });
                confirmX.Add(Edge(t + 30, 0));
                confirmY.AddRange(new[] { Edge(t + 28, 0), Edge(t + 29, -16) });
                foreach (uint row in new[] { t + 0x24 + 24, t + 0x24 + 32 + 24 })
                {
                    cancelX.Add(Edge(row + 2, 8));
                    cancelRows.AddRange(new[] { Edge(row, 16), Edge(row + 1, -24) });
                }
            }
            List<ScreenWindowFollow> follows = new List<ScreenWindowFollow>
            {
                new ScreenWindowFollow("Cancel button", cancelX.ToArray(), cancelRows.ToArray(), System.Array.Empty<ScreenWindowSpot>()),
                new ScreenWindowFollow("Confirm button", confirmX.ToArray(), confirmY.ToArray(), System.Array.Empty<ScreenWindowSpot>()),
            };
            // HGSS menu labels stay in the part of BG0 the game clears when the menu closes (PartyContextMenu.ClearedAreas).
            if (!pt)
            {
                ScreenWindowSpot[] none = System.Array.Empty<ScreenWindowSpot>();
                (int, int, int, int) menuArea = ROMFiles.PartyContextMenu.ClearedAreas[0], quitArea = ROMFiles.PartyContextMenu.ClearedAreas[1];
                follows.AddRange(Numbered("Menu button", 1, 7).Concat(Numbered("Submenu button", 1, 4))
                    .Select(n => new ScreenWindowFollow(n, none, none, none, Within: menuArea)));
                follows.Add(new ScreenWindowFollow("Quit button", none, none, none, Within: quitArea));
            }
            return follows.ToArray();
        }

        // party_menu_sprites.c PartyMenu_SetTopScreenSelectionPanelYDisplacement: the top screen's icon at x 30 and status icon at
        // x 50, 26 and 6 pixels left of the name's corner. Their y is 456 less a scroll, so the name keeps its row. Measured on every dump.
        private static ScreenWindowFollow[] HgssTopIcons(GameVersions game, GameLanguages language)
        {
            uint? icon = language switch
            {
                GameLanguages.Japanese => 0x7EDCA,
                GameLanguages.Korean => game == GameVersions.HeartGold ? 0x7F476u : 0x7F46Eu,
                GameLanguages.Spanish when game == GameVersions.HeartGold => 0x7F36A,
                GameLanguages.English or GameLanguages.French or GameLanguages.German or GameLanguages.Italian or GameLanguages.Spanish => 0x7F372,
                _ => null,
            };
            if (icon is not uint at) return null;
            return new[]
            {
                new ScreenWindowFollow("Name", new[] { new ScreenWindowSpot(at, Add: 26, Scale: 8), new ScreenWindowSpot(at + 0xC, Add: 6, Scale: 8) },
                                       System.Array.Empty<ScreenWindowSpot>(), System.Array.Empty<ScreenWindowSpot>(), KeepsRow: true),
            };
        }

        public static ScreenWindowTableSite[] PartyWindowSitesIn(GameVersions game, GameLanguages language, int revision)
        {
            if (PartyAnchor(game, language) is not uint a) return null;
            // pokeplatinum src/applications/party_menu/windows.c: sTextWindows, sMemberWindowTemplates and the multi battle ones.
            if (game == GameVersions.Platinum)
                return new ScreenWindowTableSite[]
                {
                    new("Messages and buttons", -1, a + 0x2FC, Named(PartyText), 1, 0x160, PartyButtons(a, game, language)),
                    new("Pokémon", -1, a + 0x334, PartyPokemon(1, 2, 3, 4, 5, 6), 1, 0x28),
                    new("Pokémon in a multi battle", -1, a + 0x424, PartyPokemon(2, 4, 6), 1, 0x5C),
                };
            // pokeheartgold src/party_context_menu.c.
            return new ScreenWindowTableSite[]
            {
                new("Messages and buttons", -1, a + 0x258, Named(PartyText.Concat(Numbered("Menu button", 1, 7)).Append("Quit button")
                    .Concat(Numbered("Submenu button", 1, 4))), 1, 0x180, PartyButtons(a, game, language)),
                new("Pokémon", -1, a + 0x370, PartyPokemon(1, 2, 3, 4, 5, 6), 1, 0x48),
                new("Pokémon in a multi battle", -1, a + 0x460, PartyPokemon(2, 4, 6), 1, 0x7C),
                new("Top screen", -1, a + 0x4D8, Named(new[] { "Name", "Item heading", "Held item" }), 6, 0x1, HgssTopIcons(game, language)),
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

        // pokeheartgold asm/unk_02088288.s, the summary's Cancel rect in arm9.bin, _021105B0 and the ribbon cursor code (unk_0208B1AC.s), measured on every HGSS dump.
        public static SummaryTouchSites SummaryTouchSitesIn(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English => new SummaryTouchSites(0x1038AC, 0x1105B0, 0x8C39C),
                GameLanguages.German => new SummaryTouchSites(0x103860, 0x110590, 0x8C39C),
                GameLanguages.French => new SummaryTouchSites(0x103890, 0x1105D0, 0x8C39C),
                GameLanguages.Italian => new SummaryTouchSites(0x103824, 0x110550, 0x8C39C),
                GameLanguages.Spanish => new SummaryTouchSites(0x103894, 0x1105D0, 0x8C394),
                GameLanguages.Japanese => new SummaryTouchSites(0x103044, 0x10FAE4, 0x8BCB4),
                GameLanguages.Korean => new SummaryTouchSites(0x10404C, 0x110FA4, 0x8CA60),
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English => new SummaryTouchSites(0x1038AC, 0x1105B0, 0x8C39C),
                GameLanguages.German => new SummaryTouchSites(0x103860, 0x110590, 0x8C39C),
                GameLanguages.French => new SummaryTouchSites(0x103890, 0x1105D0, 0x8C39C),
                GameLanguages.Italian => new SummaryTouchSites(0x103824, 0x110550, 0x8C39C),
                GameLanguages.Spanish => new SummaryTouchSites(0x10389C, 0x1105F0, 0x8C39C),
                GameLanguages.Japanese => new SummaryTouchSites(0x103044, 0x10FAE4, 0x8BCB4),
                GameLanguages.Korean => new SummaryTouchSites(0x104044, 0x110FA4, 0x8CA58),
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

        // pokeplatinum party_menu main.c DrawMemberPanel and PartyMenu_GetHealthbarTilemap, context_menu.c UpdateSlotHealthbar.
        // Measured on every Platinum dump.
        public static PtPartyStripSites PtPartyStripSitesIn(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Platinum => language switch
            {
                GameLanguages.English => PtStrip(0x7F2B4, 0x7F248, 0x8444E),
                GameLanguages.French or GameLanguages.German or GameLanguages.Italian or GameLanguages.Spanish => PtStrip(0x7F354, 0x7F2E8, 0x844EE),
                GameLanguages.Japanese => PtStrip(0x7EB84, 0x7EB18, 0x83D0A),
                GameLanguages.Korean => PtStrip(0x7F7A0, 0x7F734, 0x8493A),
                _ => null,
            },
            _ => null,
        };

        private static PtPartyStripSites PtStrip(uint egg, uint getter, uint swap) =>
            new PtPartyStripSites(egg, getter, new[] { swap + 0xC, swap + 0x12, swap + 0x36, swap + 0x48 });

        public static PtPartyStripSites PtPartyStripSites => Here(PtPartyStripSitesIn);

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

        // menu_graphics.c BoxMenuGraphics_PutMenuToTilemap places the menu again (Japanese builds it differently, so its menu stays put).
        // preview_display.c PCMonPreviewDisplay_CreateInfoSprites puts the held item's frame and type icons from that window's corner.
        private static ScreenWindowTableSite[] PlatinumBox(uint at, uint? menu, uint rotation, uint shift = 0)
        {
            ScreenWindowSpot Pixels(uint offset, int extra) => new ScreenWindowSpot(offset - shift, Add: -extra, Scale: 8);
            List<ScreenWindowFollow> follows = new List<ScreenWindowFollow>
            {
                // PCMonPreviewDisplay_DrawRotationPage puts the type icons back at 176 + 16 per row on the types page.
                new("Held item and more", new[] { Pixels(0x9EBC, 48), Pixels(0x9DF8, 16) },
                    new[] { Pixels(0x9EBE, 8), Pixels(0x9E4A, 8), new ScreenWindowSpot(rotation, Add: -8, Scale: 8) },
                    Array.Empty<ScreenWindowSpot>()),
            };
            if (menu is uint m)
                follows.Add(new ScreenWindowFollow("Menu", Movs(m + 0x46), new[] { new ScreenWindowSpot(m + 0x48, Imm3: true) }, Movs(m)));
            return new ScreenWindowTableSite[]
            {
                new("Box", 19, at, Named(new[] { "Species", "Dex number", "Nickname", "Held item and more", "Message", "Menu" }), 2, 0x120,
                    follows.ToArray(), menu == null ? new[] { "Menu" } : null),
            };
        }

        // buttons_graphics.c BOX_BUTTONS_X/Y with the cursor stops, preview_display.c's picture and cursor_graphics.c's arrows, in pixels.
        // Measured on every dump: Japanese sits earlier. Past 0xA000 the other languages sit 0xC after English.
        public static ScreenCodeWindowSites BoxCodeWindowSitesIn(GameVersions game, GameLanguages language, int revision)
        {
            if (game is GameVersions.HeartGold or GameVersions.SoulSilver) return HgssBoxCode(language);
            if (game != GameVersions.Platinum) return null;
            bool japanese = language == GameLanguages.Japanese;
            uint late = japanese ? unchecked((uint)-0x5E) : language == GameLanguages.English ? 0u : 0xCu;
            uint Early(uint offset, uint jp) => japanese ? offset - jp : offset;
            uint Tabs(uint offset) => japanese ? offset - 0x5C : language == GameLanguages.English ? offset : offset + 0xC;
            ScreenWindowSpot P(uint offset, int add) => new ScreenWindowSpot(offset, Add: add, Scale: 8);
            const string group = "Pictures";
            // The box: the 21 by 20 wallpaper on BG3 at tile 11,0 (template 0xF148 in English), and the icon code from grid corner
            // 112,40, scroll-off edge 72 and name stop 168,8 (view.c, cursor_graphics.c, item_icon_graphics.c).
            uint wallpaper = Tabs(0xF148);
            ScreenCodeWindow box = new ScreenCodeWindow(group, "Box", 0, 21, 20,
                new[]
                {
                    P(Early(0x71AE, 0x48), -24), P(Early(0x71C4, 0x48), -24), P(Early(0x77FE, 0x48), -24), P(Early(0x78A0, 0x48), -24),
                    P(Early(0x79AC, 0x48), -24), P(Early(0x900E, 0x50), -24), P(Tabs(0xD96C), -24), P(Early(0x6CA0, 0x48), 16),
                    P(Early(0x9050, 0x50), -80), new ScreenWindowSpot(wallpaper, Word: true, Add: -88, Scale: -8),
                },
                new[]
                {
                    // Not 0x72D4's 40: the compiler shifts it into the icon tile stride, so box-change icons snap down at the end.
                    P(Early(0x780C, 0x48), -40), P(Early(0x7852, 0x48), -40), P(Early(0x7998, 0x48), -40),
                    P(Early(0x9020, 0x50), -24), P(Tabs(0xD980), -48), P(Early(0x9054, 0x50), -8),
                    new ScreenWindowSpot(wallpaper + 4, Word: true, Scale: -8),
                });
            return new ScreenCodeWindowSites(19, new[]
            {
                box,
                new ScreenCodeWindow(group, "Party and Close buttons", 0, 18, 3,
                    new[] { P(Early(0x9B42, 0x54), -79), P(Early(0x9062, 0x50), -55), P(Early(0x9074, 0x50), -131) },
                    new[] { P(Early(0x9B44, 0x54), -16), P(Early(0x9066, 0x50), 0), P(Early(0x9078, 0x50), 0) }),
                new ScreenCodeWindow(group, "Pokémon picture", 0, 10, 10,
                    new[] { P(Early(0x9D36, 0x54), -44), P(0xA474 + late, -44) }, new[] { P(Early(0x9D38, 0x54), -44), P(0xA476 + late, -44) }),
                new ScreenCodeWindow(group, "Box arrows", 0, 19, 3,
                    new[] { P(Early(0x7F50, 0x4C), -12), P(Early(0x7F52, 0x4C), -140), P(Early(0x7F56, 0x4C), 32), P(Early(0x7F58, 0x4C), -96) },
                    new[] { P(Early(0x7F6C, 0x4C), -12), P(Early(0x7F82, 0x4C), -12) }),
                // BoxDialGraphics_CreateTabSprites: x 0 and 256 (markings tab flipped, left of its x), y 142 × 4 from the top screen's
                // 384, so a tile row is 2 of it. Each tab is 6 by 6 tiles above its y.
                new ScreenCodeWindow(group, "Box list tab", 4, 6, 6, new[] { P(Tabs(0xC4FE), 0) },
                    new[] { new ScreenWindowSpot(Tabs(0xC4F8), Add: -108, Scale: 2) }),
                new ScreenCodeWindow(group, "Markings tab", 4, 6, 6, new[] { P(Tabs(0xC516), -46) },
                    new[] { new ScreenWindowSpot(Tabs(0xC50A), Add: -108, Scale: 2) }),
                // DrawMarkings' six marks at 4,19 on BG2, with the literal holding their tilemap offset in bytes.
                Split(group, "Markings", 2, 6, 1, (4, 19), new[] { ROMFiles.ScreenPositionsPatch.Spot("Markings", 2) },
                      new[] { ROMFiles.ScreenPositionsPatch.Spot("Markings", 0) },
                      new[] { new ScreenWindowSpot((uint)WhereIn(Thing.BoxMarkings, game, language, revision).Offsets[1], Word: true) }),
            });
        }

        private static ScreenWindowFollow InFrame(string window, int width, int height) =>
            new(window, System.Array.Empty<ScreenWindowSpot>(), System.Array.Empty<ScreenWindowSpot>(), System.Array.Empty<ScreenWindowSpot>(),
                Within: (0, 0, width, height));

        // pokeheartgold overlay 14 keeps the top screen windows, the moves and item panel text, and the messages in one table.
        // HGSS overlay 14, measured on every HGSS dump, on the sub engine's layers. The picture's two sprite templates hold its
        // centre as s16 (140,100). ov14_021E895C writes the six marks from `movs r0, #18` and `adds r3, #15`, inside an 8 by 2 box.
        private static ScreenCodeWindowSites HgssBoxCode(GameLanguages language)
        {
            uint picture = language == GameLanguages.Japanese ? 0x127D4u : 0x12910u;
            uint second = picture + 0x34;
            ScreenWindowSpot Centre(uint at) => new ScreenWindowSpot(at, Add: -44, Scale: 8, Half: true);
            return new ScreenCodeWindowSites(14, new[]
            {
                new ScreenCodeWindow("Pictures", "Pokémon picture", 4, 10, 10, new[] { Centre(picture), Centre(second) },
                                     new[] { Centre(picture + 2), Centre(second + 2) }),
                new ScreenCodeWindow("Pictures", "Markings", 5, 8, 2, new[] { new ScreenWindowSpot(0x3090, Add: -1) }, new[] { new ScreenWindowSpot(0x307E) }),
            });
        }

        private static ScreenWindowTableSite[] HgssBox(uint top, uint messages, uint itemIcon) => new ScreenWindowTableSite[]
        {
            new("Top screen", 14, top, Named(new[]
            {
                "Species", "Nickname", "Level", "Title", "Gender", "Dex number", "Nature", "Ability", "Held item",
                "Move 1", "Move 2", "Move 3", "Move 4", "Copy of move 1", "Copy of move 2", "Copy of move 3", "Copy of move 4",
                "Nature label", "Ability label", "Held item label", "Item name", "Item description", "Copy of item name", "Copy of item description",
            }), 4, 0x69, new[]
            {
                // The type icons' sprite templates sit just before the table and put the icons beside the species (s16 x and y, 160,48 and 194,48).
                new ScreenWindowFollow("Species",
                    new[] { new ScreenWindowSpot(top - 0x68, Add: -96, Scale: 8, Half: true), new ScreenWindowSpot(top - 0x34, Add: -130, Scale: 8, Half: true) },
                    new[] { new ScreenWindowSpot(top - 0x66, Add: -8, Scale: 8, Half: true), new ScreenWindowSpot(top - 0x32, Add: -8, Scale: 8, Half: true) },
                    System.Array.Empty<ScreenWindowSpot>()),
                // Move Items mode puts the item icon beside the name: a movs x in pixels and a movs y in 4-pixel steps from the panel's top.
                new ScreenWindowFollow("Item name",
                    new[] { new ScreenWindowSpot(itemIcon, Add: -10, Scale: 8) },
                    new[] { new ScreenWindowSpot(itemIcon + 4, Add: -88, Scale: 2) },
                    System.Array.Empty<ScreenWindowSpot>(), Within: (0, 0, 32, 9)),
                // Windows drawn into a slid-in frame count from its corner and stay inside it: the 32 by 9 item panel, the 12 by 10 moves panel.
                InFrame("Item description", 32, 9), InFrame("Move 1", 12, 10), InFrame("Move 2", 12, 10), InFrame("Move 3", 12, 10), InFrame("Move 4", 12, 10),
            }),
            // The box menu's text goes into a frame the code puts at 21,8 or 21,14, so its table spot never shows.
            new("Messages", 14, messages, Named(new[] { "Message", "Message at the top", "Short message", "Second message", "Change box button" }), 0, 0x2B3,
                Locked: new[] { "Change box button" }),
        };

        // Measured on every dump.
        public static ScreenWindowTableSite[] BoxWindowSitesIn(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Platinum => language switch
            {
                GameLanguages.English => PlatinumBox(0xF458, 0xA91A, 0xA15C),
                GameLanguages.Japanese => PlatinumBox(0xF3FC, null, 0xA0FE, 0x54),
                GameLanguages.French => PlatinumBox(0xF464, 0xA926, 0xA168),
                GameLanguages.German => PlatinumBox(0xF464, 0xA926, 0xA168),
                GameLanguages.Italian => PlatinumBox(0xF464, 0xA926, 0xA168),
                GameLanguages.Spanish => PlatinumBox(0xF464, 0xA926, 0xA168),
                GameLanguages.Korean => PlatinumBox(0xF464, 0xA926, 0xA168),
                _ => null,
            },
            GameVersions.HeartGold or GameVersions.SoulSilver => language switch
            {
                GameLanguages.Japanese => HgssBox(0x12A78, 0x12BA0, 0xE268),
                GameLanguages.English => HgssBox(0x12BB4, 0x12CDC, 0xE2E4),
                GameLanguages.French => HgssBox(0x12BB4, 0x12CDC, 0xE2E4),
                GameLanguages.German => HgssBox(0x12BB4, 0x12CDC, 0xE2E4),
                GameLanguages.Italian => HgssBox(0x12BB4, 0x12CDC, 0xE2E4),
                GameLanguages.Spanish => HgssBox(0x12BB4, 0x12CDC, 0xE2E4),
                GameLanguages.Korean => HgssBox(0x12BB4, 0x12CDC, 0xE2E4),
                _ => null,
            },
            _ => null,
        };

        /// <summary>Null where they aren't known for this ROM.</summary>
        public static ScreenWindowTableSite[] ScreenWindowSites(string screen) => screen switch
        {
            "party" => Here(PartyWindowSitesIn),
            "box" => Here(BoxWindowSitesIn),
            "pokegear" => Here(PokegearWindowSitesIn),
            "trainercard" => Here(TrainerCardWindowSitesIn),
            "options" => Here(OptionsWindowSitesIn),
            _ => null,
        };

        // pokeheartgold application/pokegear: each app's sWindowTemplates, overlay 101. Measured on every dump.
        /// <summary>HGSS ov51_021E7F48: the card's eleven text windows and its message box. Cancel and the hint are Buttons spots.</summary>
        public static readonly string[] HgssCardWindows =
        {
            "ID No.", "Name", "Money", "Pokédex", "Score", "Time", "Adventure started",
            "Hall of Fame debut", "Times linked", "Link battles", "Link trades", "Message",
        };

        // Platinum's card_text.c sTrainerCardWindowTemplates (overlay 71): the same eleven windows on the top screen's SUB_3, without
        // the message box. Measured on every Platinum dump.
        public static ScreenWindowTableSite[] TrainerCardWindowSitesIn(GameVersions game, GameLanguages language, int revision)
        {
            if (game == GameVersions.Platinum)
            {
                uint at = language switch { GameLanguages.Japanese => 0x2428, GameLanguages.Korean => 0x2548, _ => 0x2530 };
                return new[] { new ScreenWindowTableSite("Card", 71, at, Named(HgssCardWindows.Take(11)), 7, 111) };
            }
            return HgssTrainerCardSitesIn(game, language, revision) is { } s
                ? new[] { new ScreenWindowTableSite("Card", s.Overlay, s.Windows, Named(HgssCardWindows), 7, 129) }
                : null;
        }

        // pokeplatinum options_menu.c (overlay 74): sConfirmationWindowTemplate, the save yes/no, 25,13 on BG3. Measured on every
        // Platinum dump: Korean sits 0x60 earlier.
        public static ScreenWindowTableSite[] OptionsWindowSitesIn(GameVersions game, GameLanguages language, int revision) =>
            game != GameVersions.Platinum ? null
            : new[] { new ScreenWindowTableSite("Confirm", 74, language == GameLanguages.Korean ? 0xE34u : 0xE94u, Named(new[] { "Save yes/no" }), 3, 601) };

        // OptionsMenu_SetupWindows (Pt overlay 74) and options_app.c (HGSS overlay 54): the description or text speed test box's x
        // (movs r3) and y (movs r0), and the title's y. The title's x and the entries share their registers with other arguments,
        // so they stay. Measured on every dump: only Japanese HGSS sits 0x58 earlier.
        public static ScreenCodeWindowSites OptionsCodeSitesIn(GameVersions game, GameLanguages language, int revision)
        {
            if (game == GameVersions.Platinum)
                return new ScreenCodeWindowSites(74, new[]
                {
                    Split(new ScreenCodeWindow("Text", "Title", 1, 12, 2, Spots(ROMFiles.ScreenPositionsPatch.Spot("Options title x", 0)),
                                               new[] { new ScreenWindowSpot(0x77A) }, FixedX: 1)),
                    // SetupWindows' entries at 1,3 (30 by 14). The cursor bar scrolls by -(16 × row + 8 × y): the setup's subs #27 from 3
                    // and the two moves' adds #24, or the Options menu patch's routine.
                    Split(new ScreenCodeWindow("Text", "Entries", 1, 30, 14, Spots(ROMFiles.ScreenPositionsPatch.Spot("Options entries x", 0)),
                        new[] { new ScreenWindowSpot(0x79C), new ScreenWindowSpot(0x744, Add: -3, Scale: 8) }.Concat(OptionsBarSteps(language)).ToArray(),
                        FixedX: 1)),
                    new ScreenCodeWindow("Text", "Description", 1, 27, 4, new[] { new ScreenWindowSpot(0x7DA) }, new[] { new ScreenWindowSpot(0x7BE) }),
                });
            if (game != GameVersions.HeartGold && game != GameVersions.SoulSilver) return null;
            uint early = language == GameLanguages.Japanese ? 0x58u : 0;
            return new ScreenCodeWindowSites(54, new[]
            {
                new ScreenCodeWindow("Text", "Title", 1, 12, 3, System.Array.Empty<ScreenWindowSpot>(), new[] { new ScreenWindowSpot(0x70A - early) }, FixedX: 1),
                new ScreenCodeWindow("Text", "Text speed test", 5, 27, 4, new[] { new ScreenWindowSpot(0x78C - early) }, new[] { new ScreenWindowSpot(0x770 - early) }),
            });
        }

        private static ScreenWindowSpot[] OptionsBarSteps(GameLanguages language)
        {
            long routine = ROMFiles.OptionsMenuPatch.PtScrollStepAt();
            if (routine >= 0) return new[] { new ScreenWindowSpot((uint)routine, Scale: 8, Synthetic: true) };
            uint shift = language == GameLanguages.Korean ? 0x60u : 0;
            return new[] { new ScreenWindowSpot(0xD42 - shift, Scale: 8), new ScreenWindowSpot(0xD94 - shift, Scale: 8) };
        }

        private static ScreenWindowSpot[] Spots(params ScreenWindowSpot[] spots) =>
            spots.All(s => s != null) ? spots : System.Array.Empty<ScreenWindowSpot>();

        // A window that needs ScreenPositionsPatch: an axis with no spots stays put and offers the patch.
        private static ScreenCodeWindow Split(ScreenCodeWindow window) =>
            window.X.Length > 0 && window.Y.Length > 0 ? window : window with { LockedFor = "screenPositions" };

        private static ScreenCodeWindow Split(string group, string name, int layer, int width, int height, (int X, int Y) game,
                                              ScreenWindowSpot[] x, ScreenWindowSpot[] y, ScreenWindowSpot[] packed = null)
        {
            ScreenWindowSpot[] sx = Spots(x), sy = Spots(y);
            return sx.Length > 0 && sy.Length > 0
                ? new ScreenCodeWindow(group, name, layer, width, height, sx, sy, Packed: packed)
                : new ScreenCodeWindow(group, name, layer, width, height, System.Array.Empty<ScreenWindowSpot>(), System.Array.Empty<ScreenWindowSpot>(),
                                       FixedX: game.X, FixedY: game.Y, LockedFor: "screenPositions");
        }

        // pokeplatinum start_menu.c (arm9): the menu window's Window_Add x and y (movs), the cursor and icon sprite templates (s16
        // 204,20 and 174,20) and StartMenu_SetCursorPos's 20 + 24 × row (an adds). Measured on every Platinum dump.
        public static ScreenCodeWindowSites StartMenuCodeSitesIn(GameVersions game, GameLanguages language, int revision)
        {
            if (game != GameVersions.Platinum) return null;
            (int code, uint templates) = language switch
            {
                GameLanguages.English => (0, 0xEA0A4u),
                GameLanguages.German => (0xA4, 0xEA0FCu),
                GameLanguages.French => (0xA4, 0xEA12Cu),
                GameLanguages.Italian => (0xA4, 0xEA0C0u),
                GameLanguages.Spanish => (0xA4, 0xEA138u),
                GameLanguages.Japanese => (-0x444, 0xE9778u),
                GameLanguages.Korean => (0x4DC, 0xEAB2Cu),
                _ => (0, 0u),
            };
            if (templates == 0) return null;
            uint At(uint english) => (uint)(english + code);
            ScreenWindowSpot Template(uint at, int add) => new ScreenWindowSpot(at, Add: add, Scale: 8, Half: true);
            return new ScreenCodeWindowSites(-1, new[]
            {
                new ScreenCodeWindow("Windows", "Menu", 3, 11, 21,
                    new[] { new ScreenWindowSpot(At(0x3AE3C)), Template(templates, -44), Template(templates + 0x34, -14) },
                    new[] { new ScreenWindowSpot(At(0x3AE1A)), Template(templates + 2, -12), Template(templates + 0x36, -12),
                            new ScreenWindowSpot(At(0x3B578), Add: -12, Scale: 8) }),
                // The Safari or Park ball count at 1,1 (12 by 4): x and y share one register until the patch splits them.
                Split("Windows", "Ball count", 3, 12, 4, (1, 1), new[] { ROMFiles.ScreenPositionsPatch.Spot("Ball count", 2) },
                      new[] { ROMFiles.ScreenPositionsPatch.Spot("Ball count", 0) }),
            });
        }

        // pokeheartgold main_menu.c MainMenu_SetupSprites (overlay 74): the scroll arrows' centres, movs 236,20 and 236,172, each moved
        // as a 3 by 3 picture. Measured on every HGSS dump: Japanese sits 0x30 earlier.
        public static ScreenCodeWindowSites MainMenuCodeSitesIn(GameVersions game, GameLanguages language, int revision)
        {
            if (game == GameVersions.Platinum) return MainMenuAlerts(97, language switch
            {
                GameLanguages.Japanese => 0xF8B0u, GameLanguages.Korean => 0x13110u, _ => 0x13150u,
            }, 28);
            if (HgssMainMenuSitesIn(game, language, revision) is not { } sites) return null;
            uint up = language == GameLanguages.Japanese ? 0xC64u : 0xC94u, down = up + 0x2E;
            ScreenWindowSpot Centre(uint at) => new ScreenWindowSpot(at, Add: -12, Scale: 8);
            return new ScreenCodeWindowSites(74, new[]
            {
                new ScreenCodeWindow("Pictures", "Up arrow", 0, 3, 3, new[] { Centre(up) }, new[] { Centre(up + 2) }),
                new ScreenCodeWindow("Pictures", "Down arrow", 0, 3, 3, new[] { Centre(down) }, new[] { Centre(down + 2) }),
            }.Concat(MainMenuAlerts(74, sites.Windows, 32).Windows).ToArray());
        }

        // pokeplatinum party_menu/windows.c PartyMenu_DrawContextMenu (arm9): x 19 (movs r3) and the bottom row 23 the menu grows up
        // from (movs r1), shown 12 by 8 for four choices. Measured on every Platinum dump.
        public static ScreenCodeWindowSites PartyCodeSitesIn(GameVersions game, GameLanguages language, int revision)
        {
            if (game != GameVersions.Platinum) return null;
            int shift = language switch
            {
                GameLanguages.English => 0, GameLanguages.Japanese => -0x730, GameLanguages.Korean => 0x4EC, _ => 0xA0,
            };
            uint bottom = (uint)(0x81CFE + shift);
            return new ScreenCodeWindowSites(-1, new[]
            {
                new ScreenCodeWindow("Messages and buttons", "Context menu", 0, 12, 8, new[] { new ScreenWindowSpot(bottom + 0x26) },
                                     new[] { new ScreenWindowSpot(bottom, Add: -8) }),
            });
        }

        /// <summary>The main menu alerts' names, in sMainMenuAlerts order.</summary>
        public static readonly string[] MainMenuAlertNames =
            { "Mystery Gift", "Mystery Gift without a Pokédex", "Ranger", "Ranger without a Pokédex", "New game warning" };

        // pokeplatinum main_menu.c sMainMenuAlerts (overlay 97) and pokeheartgold ov74_0223BC30: x, y, width, height and message as ints,
        // 20 bytes an entry. Measured on every Platinum dump. Only the warning's width differs between the games.
        private static ScreenCodeWindowSites MainMenuAlerts(int overlay, uint table, int warningWidth)
        {
            (int W, int H)[] sizes = { (24, 20), (24, 16), (24, 22), (24, 18), (warningWidth, warningWidth == 28 ? 22 : 11) };
            return new ScreenCodeWindowSites(overlay, Enumerable.Range(0, sizes.Length).Select(i => new ScreenCodeWindow("Alerts", MainMenuAlertNames[i], 0,
                sizes[i].W, sizes[i].H, new[] { new ScreenWindowSpot(table + (uint)(20 * i), Word: true) },
                new[] { new ScreenWindowSpot(table + (uint)(20 * i) + 4, Word: true) })).ToArray());
        }

        // ov51_021E801C: the 16 badges' centres as u32 x, y pairs, 4 across from 96 every 48 and 4 down at 48, 88, 136 and 176, read by
        // ov51_021E7AF4 (which adds 40 before the League). Moved as one 22 by 20 block from tile 10,4. On every HGSS dump the table
        // sits 0xD4 after the card's window table.
        public static ScreenCodeWindowSites TrainerCardCodeSitesIn(GameVersions game, GameLanguages language, int revision)
        {
            if (HgssTrainerCardSitesIn(game, language, revision) is not { } s) return null;
            uint table = s.Windows + 0xD4;
            int[] down = { 48, 88, 136, 176 };
            ScreenWindowSpot[] x = Enumerable.Range(0, 16).Select(i => new ScreenWindowSpot(table + (uint)(8 * i), Word: true, Add: 80 - (96 + 48 * (i % 4)), Scale: 8)).ToArray();
            ScreenWindowSpot[] y = Enumerable.Range(0, 16).Select(i => new ScreenWindowSpot(table + (uint)(8 * i) + 4, Word: true, Add: 32 - down[i / 4], Scale: 8)).ToArray();
            // The Hall of Fame mark (map 53, 7 by 9) at 0,7, and 7 lower after the League from its own movs.
            int[] mark = WhereIn(Thing.TrainerCardHallOfFame, game, language, revision).Offsets;
            return new ScreenCodeWindowSites(s.Overlay, new[]
            {
                new ScreenCodeWindow("Pictures", "Badges", 2, 22, 20, x, y),
                Split("Pictures", "Hall of Fame mark", 2, 7, 9, (0, 7),
                      new[] { ROMFiles.ScreenPositionsPatch.Spot("Hall of Fame x", 3), ROMFiles.ScreenPositionsPatch.Spot("Hall of Fame x, after the League", 2) },
                      new[] { ROMFiles.ScreenPositionsPatch.Spot("Hall of Fame y", 2), new ScreenWindowSpot((uint)mark[3], Add: -7) }),
            });
        }

        public static ScreenWindowTableSite[] PokegearWindowSitesIn(GameVersions game, GameLanguages language, int revision)
        {
            if (game != GameVersions.HeartGold && game != GameVersions.SoulSilver) return null;
            int? known = language switch
            {
                GameLanguages.English => 0,
                GameLanguages.German or GameLanguages.French or GameLanguages.Spanish => 0xC,
                GameLanguages.Italian => 8,
                GameLanguages.Korean => 0x10,
                GameLanguages.Japanese => -0x70,
                _ => null,
            };
            if (known is not int shift) return null;
            int map = language == GameLanguages.Japanese ? -0x68 : shift;
            uint band = language == GameLanguages.Japanese ? 0x9378u : 0x93B4u;
            string[] places = { "Region", "Place", "Description", "Landmark 1", "Landmark 2", "Landmark 3", "Landmark 4" };
            bool jp = language == GameLanguages.Japanese;
            uint mapMarkers = jp ? 0x11C6u : 0x11D6u, flyMarkers = jp ? 0x6CF2u : 0x6D0Eu, scrollMarkers = jp ? 0x26C0u : 0x26D0u, scrollRow = jp ? 0x273Cu : 0x274Cu;
            return new ScreenWindowTableSite[]
            {
                // PokegearPhone_PrintContextMenuTooltip copies and clears the hint's 4-row band from the row above it.
                new("Phone", 101, (uint)(0x10CE0 + shift), Named(new[] { "Call", "Caller", "Contacts", "Hint" }), 6, 0x375,
                    new[] { new ScreenWindowFollow("Hint", System.Array.Empty<ScreenWindowSpot>(),
                        new[] { new ScreenWindowSpot(band, Add: 1), new ScreenWindowSpot(band + 0x5A, Add: 1) }, System.Array.Empty<ScreenWindowSpot>()) }),
                new("Radio", 101, (uint)(0x1109C + shift), Named(new[] { "Broadcast", "Station", "Heading" }), 6, 0x38F),
                new("Map", 101, (uint)(0x10424 + map), Named(places.Append("Map heading")), 5, 0x3F0,
                    MarkerFollows(new[] { (mapMarkers, mapMarkers + 10), (scrollMarkers, scrollMarkers + 4) }, new[] { new ScreenWindowSpot(mapMarkers + 12, Add: -59, Scale: 8),
                        new ScreenWindowSpot(scrollRow, Word: true, Add: -187, Scale: 8) })),
                new("Fly map", 101, (uint)(0x10814 + map), Named(places.Append("Fly heading").Append("Fly close")), 5, 0x3F0,
                    MarkerFollows(new[] { (flyMarkers, flyMarkers + 10) }, new[] { new ScreenWindowSpot(flyMarkers + 12, Add: -59, Scale: 8) })),
            };
        }

        // The four marking icons sit 8 left of the two marking word windows, two rows each: x is 32 (an adds) plus 104 (a movs)
        // for the right column, and one y number serves both windows.
        private static ScreenWindowFollow[] MarkerFollows((uint Gap, uint Left)[] x, ScreenWindowSpot[] row) => new[]
        {
            new ScreenWindowFollow("Landmark 1", x.Select(at => new ScreenWindowSpot(at.Left, Add: 8, Scale: 8)).ToArray(), row,
                System.Array.Empty<ScreenWindowSpot>()),
            new ScreenWindowFollow("Landmark 2", x.Select(at => new ScreenWindowSpot(at.Gap, Scale: 8, From: "Landmark 1")).ToArray(), row,
                System.Array.Empty<ScreenWindowSpot>()),
        };

        public static PtBagItemBoxSites PtBagItemBox => Here(PtBagItemBoxSitesIn);

        // Measured on every dump.
        public static PtBagItemBoxSites PtBagItemBoxSitesIn(GameVersions game, GameLanguages language, int revision) =>
            game != GameVersions.Platinum ? null
            : new PtBagItemBoxSites(84, language == GameLanguages.Japanese ? 0x5A34u : 0x5AC4u, language == GameLanguages.Japanese ? 0x5A66u : 0x5AF6u, 14);

        /// <summary>Null where they aren't known for this ROM.</summary>
        public static ScreenCodeWindowSites ScreenCodeWindows(string screen) => screen switch
        {
            "bag" => Here(BagWindowSitesIn),
            "naming" => Here(NamingWindowSitesIn),
            "box" => Here(BoxCodeWindowSitesIn),
            "trainercard" => Here(TrainerCardCodeSitesIn),
            "options" => Here(OptionsCodeSitesIn),
            "mainmenu" => Here(MainMenuCodeSitesIn),
            "startmenu" => Here(StartMenuCodeSitesIn),
            "party" => Here(PartyCodeSitesIn),
            "battlepokemon" => Here(BattlePokemonCodeSitesIn),
            _ => null,
        };

        // battle_party_sprites.c, battle_party_text.c: each header page keeps its own copy of the types, icon, status and name window,
        // at fixed distances from BattleMenuSites.PartyCursors on all 24 Pt and HGSS dumps. The Header row's tile is 2,1.
        public static ScreenCodeWindowSites BattlePokemonCodeSitesIn(GameVersions game, GameLanguages language, int revision)
        {
            if (BattleMenuSitesIn(game, language, revision) is not BattleMenuSites sites) return null;
            uint At(int distance) => (uint)(sites.PartyCursors + distance);
            ScreenWindowSpot Px(int distance, int add) => new ScreenWindowSpot(At(distance), Word: true, Add: add, Scale: 8);
            ScreenWindowSpot Tile(int distance, int add) => new ScreenWindowSpot(At(distance), Byte: true, Add: add);
            int[] types = { -0x108, -0xF8, -0xE8, -0xD8, -0xC8 }, icons = { -0x158, -0x150, -0x148, -0x128, -0x110 }, statuses = { -0x168, -0x140, -0x138 };
            int[] names = { -0x490, -0x468, -0x408, -0x3D0, -0x3A0, -0x378, -0x2E8, -0x290, -0x1E0 };
            ScreenWindowSpot[] x = types.SelectMany(t => new[] { Px(t, -114), Px(t + 8, -148) })
                .Concat(icons.Select(i => Px(i, -8))).Concat(statuses.Select(s => Px(s, -182))).Concat(names.Select(n => Tile(n + 1, -3))).ToArray();
            ScreenWindowSpot[] y = types.SelectMany(t => new[] { Px(t + 4, -8), Px(t + 12, -8) })
                .Concat(icons.Select(i => Px(i + 4, -4))).Concat(statuses.Select(s => Px(s + 4, -12))).Concat(names.Select(n => Tile(n + 2, 0))).ToArray();
            return new ScreenCodeWindowSites(sites.Overlay, new[] { new ScreenCodeWindow("Pictures", "Header", 5, 24, 2, x, y) });
        }

        // The prompt window's y, in pokeplatinum and pokeheartgold naming_screen.c, which build their windows alike in arm9.
        private static uint? NamingAnchor(GameVersions game, GameLanguages language) => game switch
        {
            GameVersions.Platinum => language switch
            {
                GameLanguages.English => 0x88146,
                GameLanguages.Japanese => 0x87A60,
                GameLanguages.Korean => 0x8867A,
                GameLanguages.French => 0x881E6,
                GameLanguages.German => 0x881E6,
                GameLanguages.Italian => 0x881E6,
                GameLanguages.Spanish => 0x881E6,
                _ => null,
            },
            GameVersions.HeartGold => language switch
            {
                GameLanguages.English => 0x84122,
                GameLanguages.Spanish => 0x8411A,   // Spanish HeartGold's arm9 sits 8 bytes earlier here. SoulSilver's doesn't.
                GameLanguages.Japanese => 0x83BC0,
                GameLanguages.Korean => 0x8426E,
                GameLanguages.French => 0x84122,
                GameLanguages.German => 0x84122,
                GameLanguages.Italian => 0x84122,
                _ => null,
            },
            GameVersions.SoulSilver => language switch
            {
                GameLanguages.English => 0x84122,
                GameLanguages.Spanish => 0x84122,
                GameLanguages.Japanese => 0x83BC0,
                GameLanguages.Korean => 0x84266,
                GameLanguages.French => 0x84122,
                GameLanguages.German => 0x84122,
                GameLanguages.Italian => 0x84122,
                _ => null,
            },
            _ => null,
        };

        // pokeplatinum sCharCodes and pokeheartgold sKeyboardLayoutPtrs: five pages of five key rows, as arm9 pointers. Measured on
        // every dump. SoulSilver Spanish sits 0x20 after HeartGold Spanish.
        public static uint? NamingKeyTableIn(GameVersions game, GameLanguages language) => game switch
        {
            GameVersions.Platinum => language switch
            {
                GameLanguages.English => 0x100C54, GameLanguages.French => 0x100E30, GameLanguages.German => 0x100DF0,
                GameLanguages.Italian => 0x100DB0, GameLanguages.Spanish => 0x100E50, GameLanguages.Japanese => 0x100048,
                GameLanguages.Korean => 0x101B30, _ => null,
            },
            GameVersions.HeartGold or GameVersions.SoulSilver => language switch
            {
                GameLanguages.English => 0x1104F8, GameLanguages.French => 0x110518, GameLanguages.German => 0x1104D8,
                GameLanguages.Italian => 0x110498, GameLanguages.Japanese => 0x10FA2C, GameLanguages.Korean => 0x110ED8,
                GameLanguages.Spanish => game == GameVersions.SoulSilver ? 0x110538u : 0x110518u, _ => null,
            },
            _ => null,
        };

        public static uint? NamingKeyTable => RomID == null ? null : NamingKeyTableIn(GameVersion, GameLanguage);

        // Measured on every dump. The keys and the title only slide across: one register holds their y and their palette or height.
        // Japanese keys are placed elsewhere, so they stay out.
        public static ScreenCodeWindowSites NamingWindowSitesIn(GameVersions game, GameLanguages language, int revision)
        {
            if (NamingAnchor(game, language) is not uint m) return null;
            const string bottom = "Keyboard", top = "Top screen";
            ScreenWindowSpot[] none = System.Array.Empty<ScreenWindowSpot>();
            // The name's side window sits just right of it, and its underscores (80 + 12 per letter, 39) and the gender sign
            // (80 + 13 per letter, 27) hang off it. Japanese builds those differently, so its name stays put.
            bool pt = game == GameVersions.Platinum, korean = language == GameLanguages.Korean;
            uint icon = m - (language == GameLanguages.Japanese ? 0x5ACu : pt ? 0x55Eu : 0x56Au);
            uint underscore = m - (pt ? (korean ? 0x5C8u : 0x5D4u) : (korean ? 0x5D4u : 0x5E0u)), gender = m - (pt ? 0x49Au : 0x4A6u);
            ScreenWindowSpot Pixel(uint at, int add) => new ScreenWindowSpot(at, Add: add, Scale: 8);
            ScreenCodeWindow name = language == GameLanguages.Japanese
                ? new ScreenCodeWindow(bottom, "Name", 2, 13, 2, none, none, FixedX: 10, FixedY: 3)
                : new ScreenCodeWindow(bottom, "Name", 2, 13, 2,
                    new[] { new ScreenWindowSpot(m - 0x62), new ScreenWindowSpot(m - 0x44, Add: 1), Pixel(underscore, 0), Pixel(gender, 0) },
                    new[] { new ScreenWindowSpot(m - 0x84), new ScreenWindowSpot(m - 0x50), Pixel(underscore + 8, -15), Pixel(gender + 6, -3) });
            // The keys are text on a layer the code scrolls to -11 in several places, over key art on another layer, with a pixel
            // touch grid and a cursor at 26 + 16 per key, so they stay put.
            List<ScreenCodeWindow> windows = new List<ScreenCodeWindow>
            {
                new ScreenCodeWindow(bottom, "Title", 2, 22, 2, Movs(m - 0x9C), none, FixedY: 2),
                name,
                new ScreenCodeWindow(top, "Prompt", 4, 27, 4, Movs(m + 0x1C), Movs(m)),
                // InitIconSprite's FX32_CONST(24), FX32_CONST(8): movs of a quarter pixel, the icon centred 16,8 inside its box.
                new ScreenCodeWindow(bottom, "Icon", 2, 4, 3, new[] { new ScreenWindowSpot(icon, Add: -4, Scale: 2) },
                                     new[] { new ScreenWindowSpot(icon + 8, Add: -2, Scale: 2) }),
            };
            if (language != GameLanguages.Japanese)
                windows.Insert(0, new ScreenCodeWindow(bottom, "Keys", 0, 26, 12, none, none, FixedX: 2, FixedY: 1));
            return new ScreenCodeWindowSites(-1, windows.ToArray());
        }

        private static ScreenWindowSpot[] Movs(uint at) => new[] { new ScreenWindowSpot(at) };

        // Where the code copies x from a register that places something else too, x stays at copiedX.
        // bag_app_sprite.c MOVE_TYPE_ICON and MOVE_CATEGORY_ICON, templates 7 and 8 of sSpriteTemplates, put the icons at 48,112 and
        // 144,112 (Japanese 136) as s16, 8 below the TM details window at 0,13. Measured on every HGSS dump.
        private static ScreenCodeWindow TmDetails(ScreenCodeWindow tm, bool japanese)
        {
            uint templates = japanese ? 0x7714u : 0x778Cu, type = templates + 7 * 0x34, category = templates + 8 * 0x34;
            ScreenWindowSpot Px(uint at, int add) => new ScreenWindowSpot(at, Add: add, Scale: 8, Half: true);
            return tm with
            {
                X = tm.X.Concat(new[] { Px(type, -48), Px(category, japanese ? -136 : -144) }).ToArray(),
                Y = tm.Y.Concat(new[] { Px(type + 2, -8), Px(category + 2, -8) }).ToArray(),
            };
        }

        private static ScreenCodeWindow Coded(string group, string name, int layer, int width, int height, uint xAt, uint yAt, int copiedX = -1) =>
            copiedX >= 0
                ? new ScreenCodeWindow(group, name, layer, width, height, System.Array.Empty<ScreenWindowSpot>(), Movs(yAt), FixedX: copiedX)
                : new ScreenCodeWindow(group, name, layer, width, height, Movs(xAt), Movs(yAt));

        private static ScreenCodeWindow Tabled(string group, string name, int layer, int width, int height, params uint[] at) =>
            new ScreenCodeWindow(group, name, layer, width, height, at.Select(a => new ScreenWindowSpot(a, true)).ToArray(),
                                 at.Select(a => new ScreenWindowSpot(a + 4, true)).ToArray());

        private static readonly uint[] PtHighlight = { 0x1090, 0x1770, 0x1DFE, 0x1F74, 0x1FC4, 0x3854, 0x3970, 0x54AC };
        private static readonly uint[] PtHighlightJapanese = { 0x108C, 0x176C, 0x1DFA, 0x1F70, 0x1FC0, 0x3808, 0x3924, 0x541C };

        // pokeplatinum applications/bag/windows.c and pokeheartgold application/bag/bag_app_window.c. Measured on every dump.
        // The money windows are left out because one register holds their x and y.
        public static ScreenCodeWindowSites BagWindowSitesIn(GameVersions game, GameLanguages language, int revision)
        {
            bool japanese = language == GameLanguages.Japanese;
            const string top = "Top screen", bottom = "Bottom screen";
            if (game == GameVersions.Platinum)
            {
                uint c = japanese ? 0x4Cu : 0;
                ScreenCodeWindow Pt(string name, int layer, int width, int height, uint xAt, uint yAt, int copiedX = -1) =>
                    Coded(top, name, layer, width, height, xAt - c, yAt - c, copiedX);
                uint a = japanese ? 0x90u : 0;
                ScreenWindowSpot Px(uint at, int add) => new ScreenWindowSpot(at, Add: add, Scale: 8);
                ScreenCodeWindow Counted(string name, int width, uint xAt, uint yAt, uint upX, uint downX, int addX, uint upY, uint downY) =>
                    new ScreenCodeWindow(top, name, 0, width, 4, new[] { new ScreenWindowSpot(xAt), Px(upX, addX), Px(downX, addX) },
                                         new[] { new ScreenWindowSpot(yAt), Px(upY, -4), Px(downY, -28) });
                return new ScreenCodeWindowSites(84, new[]
                {
                    // The item highlight and move bar sit at x 177 (its template and 8 movs). Their y is 16 per row with no number
                    // to move, so the list keeps its row.
                    new ScreenCodeWindow(top, "Item list", 2, 17, 18,
                        Movs(0x3AC0 - c).Concat((japanese ? PtHighlightJapanese : PtHighlight).Select(o => new ScreenWindowSpot(o, Add: -65, Scale: 8)))
                            .Append(new ScreenWindowSpot(japanese ? 0x5C50u : 0x5CE0u, Add: -65, Scale: 8, Half: true)).ToArray(),
                        System.Array.Empty<ScreenWindowSpot>()),
                    Pt("Description", 0, 32, 6, 0x3AE2, 0x3AC6, 0),
                    Pt("Message", 0, 14, 4, 0x3B24, 0x3B08),
                    Pt("Short message", 0, 13, 4, 0x3B46, 0x3B2A),
                    Pt("Long message", 0, 27, 4, 0x3B8A, 0x3B6E),
                    // bag/sprites.c BagUI_ShowItemCountArrows puts the up and down arrows beside these two, from movs pairs.
                    Counted("Sell amount", 12, 0x3BAC - c, 0x3B90 - c, 0x577C - a, 0x578A - a, -10, 0x577E - a, 0x578C - a),
                    Counted("Toss amount", 7, 0x3BEE - c, 0x3BD2 - c, 0x575E - a, 0x576C - a, -28, 0x5760 - a, 0x576E - a),
                    Pt("Poffin count", 0, 11, 4, 0x3C10, 0x3BF4),
                    // BagUI_ShowItemActionsMenu outside the Berries pocket: x 24 (movs r3) and the bottom row 23 the menu grows up from
                    // (movs r1), shown 7 by 8 for four choices. The Berries menu reuses one register for its x and bottom, so it stays.
                    new ScreenCodeWindow(top, "Item actions", 0, 7, 8, new[] { new ScreenWindowSpot(japanese ? 0x45D2u : 0x4646u) },
                                         new[] { new ScreenWindowSpot(japanese ? 0x45AEu : 0x4622u, Add: -8) }),
                    // The Berries menu at x 23, up from row 23 (8 wide): one register holds both until the patch splits them.
                    Split(top, "Berry actions", 0, 8, 8, (23, 15), new[] { ROMFiles.ScreenPositionsPatch.Spot("Berry actions", 2) },
                          new[] { ROMFiles.ScreenPositionsPatch.Spot("Berry actions", 0, -8) }),
                    // The pocket icons' window at 0,11 (its x shares the layer's register), with the pocket highlight (template and
                    // BagUI_SetPocketHighlight's movs, y 97) and the two scroll arrows' templates (y 96) one row down inside it.
                    new ScreenCodeWindow(top, "Pocket icons", 0, 12, 2, System.Array.Empty<ScreenWindowSpot>(),
                        new[]
                        {
                            new ScreenWindowSpot(0x3B4C - c), new ScreenWindowSpot(0x5C46 - a, Add: -9, Scale: 8, Half: true),
                            new ScreenWindowSpot(0x548C - a, Add: -9, Scale: 8), new ScreenWindowSpot(0x5C7A - a, Add: -8, Scale: 8, Half: true),
                            new ScreenWindowSpot(0x5CAE - a, Add: -8, Scale: 8, Half: true),
                        }),
                    // Code clears this box at the same rows, so those move too. Its columns come from a loop.
                    new ScreenCodeWindow(top, "Pocket names", 2, 12, 3, System.Array.Empty<ScreenWindowSpot>(),
                                         new[] { new ScreenWindowSpot(0x3AE8 - c), new ScreenWindowSpot(0x3CC4 - c), new ScreenWindowSpot(0x3CE6 - c, Add: -1) }),
                    // bag/main.c ToggleHideItemSprite writes the 5 by 5 item box at 0,18 each time, from two movs pairs.
                    new ScreenCodeWindow("Pictures", "Item frame", 0, 5, 5,
                                         new[] { new ScreenWindowSpot(japanese ? 0x225Eu : 0x2262u), new ScreenWindowSpot(japanese ? 0x2282u : 0x2286u) },
                                         new[] { new ScreenWindowSpot(japanese ? 0x224Eu : 0x2252u), new ScreenWindowSpot(japanese ? 0x2272u : 0x2276u) }),
                    // bag/sprites.c BAG_SPRITE_ITEM's template, x 22 and y 172 as s16, which nothing moves later. The picture is
                    // the 4 by 4 tiles the icon covers. Measured on every dump.
                    new ScreenCodeWindow("Pictures", "Item icon", 0, 4, 4,
                                         new[] { new ScreenWindowSpot((japanese ? 0x5CECu : 0x5D7Cu), Add: -14, Scale: 8, Half: true) },
                                         new[] { new ScreenWindowSpot((japanese ? 0x5CEEu : 0x5D7Eu), Add: -20, Scale: 8, Half: true) }),
                });
            }
            if (game != GameVersions.HeartGold && game != GameVersions.SoulSilver) return null;
            uint code = japanese ? 0x44u : 0, data = japanese ? 0x78u : 0;
            ScreenCodeWindow Code(string group, string name, int layer, int width, int height, uint xAt, uint yAt, int copiedX = -1) =>
                Coded(group, name, layer, width, height, xAt - code, yAt - code, copiedX);
            uint digits = 0x7550 - data, options = 0x7568 - data, items = 0x7588 - data;
            List<ScreenCodeWindow> windows = new List<ScreenCodeWindow>
            {
                Code(top, "Description", 1, 32, 6, 0x4CBE, 0x4CA6),
                TmDetails(Code(top, "TM details", 1, 32, 4, 0x4CDE, 0x4CC4), japanese),
                Code(top, "Amount", 1, 12, 4, 0x4D3C, 0x4D22),
                Code(top, "Poffin count", 1, 11, 4, 0x4D5C, 0x4D42, 1),
                Code(bottom, "Page", 4, 7, 2, 0x4D7E, 0x4D62),
                Code(bottom, "Cancel", 4, japanese ? 6 : 7, 2, 0x4DAA, 0x4D8E),
                Code(bottom, "Selected item", 4, 11, 4, 0x4EB0, 0x4E96),
                Code(bottom, "Sell or toss", 4, japanese ? 5 : 7, 2, 0x4F92, 0x4F70),
                Code(bottom, "Prompt", 4, 18, 4, 0x4FB4, 0x4FA6),
                Code(bottom, "Long message", 4, 27, 4, 0x4D1C, 0x4D02),
                // The item menu's 2,1 line: y shares the base tile and x the height until the patch splits them.
                Split(bottom, "Item action", 4, 27, 2, (2, 1), new[] { ROMFiles.ScreenPositionsPatch.Spot("Item action x", 2) },
                      new[] { ROMFiles.ScreenPositionsPatch.Spot("Item action y", 0) }),
                Code(bottom, "Price", 4, 8, 3, 0x5028, 0x5006),
            };
            // Each item has two windows at one spot that the game swaps on a page turn. Its button, icon and cursor are fixed, so the
            // text stays on its 16 by 6 button unless the bag item buttons patch is in.
            windows.AddRange(Enumerable.Range(0, 6).Select(i => Tabled(bottom, $"Item {i + 1}", 4, 11, 4, items + (uint)i * 12, items + (uint)(i + 6) * 12)
                with { Within = (16 * (i % 2), 4 + 5 * (i / 2), 16, 6), FreeWithBagButtons = true }));
            windows.AddRange(Enumerable.Range(0, 4).Select(i => Tabled(bottom, $"Option {i + 1}", 4, 10, 2, options + (uint)i * 8)));
            windows.AddRange(Enumerable.Range(0, 3).Select(i => Tabled(bottom, $"Digit {i + 1}", 4, 2, 3, digits + (uint)i * 8)));
            return new ScreenCodeWindowSites(15, windows.ToArray());
        }

        // pokeplatinum overlay 5 map_name_popup.c, pokeheartgold overlay 1 draw_map_name.c. Measured on every Platinum and HGSS dump.
        private static readonly PlaceNameBannerSites BannerHgWest = new PlaceNameBannerSites(1, 0x9EC0, 0x9F7C, 0x9FB8, 0xA046, 0xA154, 0x9F48, 0xA0AE);
        private static readonly PlaceNameBannerSites BannerHgJp = new PlaceNameBannerSites(1, 0x9D78, 0x9E34, 0x9E70, 0x9EFE, 0xA00C, 0x9E00, 0x9F66);

        public static PlaceNameBannerSites PlaceNameBannerSitesIn(GameVersions game, GameLanguages language, int revision) => game switch
        {
            GameVersions.Platinum => language switch
            {
                GameLanguages.English => new PlaceNameBannerSites(5, 0xC99C, 0xCAAA, 0xCAE6, 0xCB72, 0xCC80, 0, 0xC264),
                GameLanguages.German => new PlaceNameBannerSites(5, 0xCA00, 0xCB0E, 0xCB4A, 0xCBD6, 0xCCE4, 0, 0xC2CC),
                GameLanguages.French => new PlaceNameBannerSites(5, 0xC9EC, 0xCAFA, 0xCB36, 0xCBC2, 0xCCD0, 0, 0xC2B4),
                GameLanguages.Italian => new PlaceNameBannerSites(5, 0xC9EC, 0xCAFA, 0xCB36, 0xCBC2, 0xCCD0, 0, 0xC2B4),
                GameLanguages.Spanish => new PlaceNameBannerSites(5, 0xC9EC, 0xCAFA, 0xCB36, 0xCBC2, 0xCCD0, 0, 0xC2B4),
                GameLanguages.Japanese => new PlaceNameBannerSites(5, 0xC8E0, 0xC9EE, 0xCA2A, 0xCAB6, 0xCBC4, 0, 0xC1E8),
                GameLanguages.Korean => new PlaceNameBannerSites(5, 0xC9E0, 0xCAEE, 0xCB2A, 0xCBB6, 0xCCC4, 0, 0xC2B0),
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
