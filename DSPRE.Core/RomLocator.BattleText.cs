using System.Collections.Generic;
using System.Linq;
using static DSPRE.RomInfo;

namespace DSPRE
{
    // Battle touch screen label and icon spots from BattleCommandSites.TargetGrid, measured on every dump: pokeplatinum
    // BattleSubscreen_NewFontOAM and pokeheartgold BattleInput_CreateTextObject calls and their position tables.
    public static partial class RomLocator
    {
        /// <summary>One label or icon spot: x and y as movs immediates (one byte each), or a u16 x, y table entry.</summary>
        public sealed record BattleTextSpot(int Page, int Button, int X, int Y, bool Table);

        private static IEnumerable<BattleTextSpot> Code(int anchor, int page, int button, params (int X, int Y)[] at) =>
            at.Select(a => new BattleTextSpot(page, button, a.X - anchor, a.Y - anchor, false));

        private static IEnumerable<BattleTextSpot> Rows(int anchor, int page, int firstButton, int table, int count) =>
            Enumerable.Range(0, count).Select(i => new BattleTextSpot(page, firstButton + i, table - anchor + i * 4, table - anchor + i * 4 + 2, true));

        /// <summary>
        /// Each battle touch screen button's text and icon spots, in BattleCommandButtons page and button order: on Fight's moves the
        /// name, PP label, PP count and type icon; on Target the name and icon. The first battle and Pal Park draw Command's labels.
        /// </summary>
        public static IReadOnlyList<BattleTextSpot> BattleTextSpots(bool hgss)
        {
            // The offsets below are English ones.
            int a = (int)BattleCommandSitesIn(hgss ? GameVersions.HeartGold : GameVersions.Platinum, GameLanguages.English, 0).TargetGrid;
            List<BattleTextSpot> spots = new List<BattleTextSpot>();
            if (!hgss)
            {
                spots.AddRange(Code(a, 0, 0, (0x2E5FA, 0x2E5FE)));
                spots.AddRange(Code(a, 0, 1, (0x2E624, 0x2E628)));
                spots.AddRange(Code(a, 0, 2, (0x2E64C, 0x2E650)));
                spots.AddRange(Code(a, 0, 3, (0x2E674, 0x2E678)));
                spots.AddRange(Code(a, 1, 0, (0x2EA0C, 0x2EA10)));
                int[] names = { 0x2E8AA, 0x2E8D8, 0x2E904, 0x2E930 };
                for (int i = 0; i < 4; i++) spots.AddRange(Code(a, 1, i + 1, (names[i], names[i] + 4)));
                spots.AddRange(Rows(a, 1, 1, 0x35144, 4));
                spots.AddRange(Rows(a, 1, 1, 0x35194, 4));
                spots.AddRange(Rows(a, 1, 1, 0x35174, 4));
                spots.AddRange(Rows(a, 2, 0, 0x351E8, 5));
                spots.AddRange(Rows(a, 2, 0, 0x351C4, 4));
                // Yes or no, forget a move, give up, next Pokémon and switch Pokémon each place a separate pair.
                spots.AddRange(Code(a, 3, 0, (0x2EB6E, 0x2EB72), (0x2EC04, 0x2EC08), (0x2ECDC, 0x2ECE0), (0x2EE94, 0x2EE98), (0x2EF7A, 0x2EF7E)));
                spots.AddRange(Code(a, 3, 1, (0x2EB96, 0x2EB9A), (0x2EC2C, 0x2EC30), (0x2ED04, 0x2ED08), (0x2EEBC, 0x2EEC0), (0x2EFA2, 0x2EFA6)));
                spots.AddRange(Code(a, 6, 0, (0x2EDF2, 0x2EDF6)));
            }
            else
            {
                spots.AddRange(Code(a, 0, 0, (0x2F7CA, 0x2F7CE)));
                spots.AddRange(Code(a, 0, 1, (0x2F7F4, 0x2F7F8)));
                spots.AddRange(Code(a, 0, 2, (0x2F81E, 0x2F822)));
                spots.AddRange(Code(a, 0, 3, (0x2F848, 0x2F84C)));
                spots.AddRange(Code(a, 1, 0, (0x2FD6C, 0x2FD70)));
                int[] names = { 0x2FC0A, 0x2FC38, 0x2FC64, 0x2FC90 };
                for (int i = 0; i < 4; i++) spots.AddRange(Code(a, 1, i + 1, (names[i], names[i] + 4)));
                spots.AddRange(Rows(a, 1, 1, 0x36A08, 4));
                spots.AddRange(Rows(a, 1, 1, 0x36A48, 4));
                spots.AddRange(Rows(a, 1, 1, 0x369F8, 4));
                spots.AddRange(Rows(a, 2, 0, 0x36AB0, 5));
                spots.AddRange(Rows(a, 2, 0, 0x36A68, 4));
                spots.AddRange(Code(a, 3, 0, (0x2FED2, 0x2FED6), (0x2FF6C, 0x2FF70), (0x30048, 0x3004C), (0x30204, 0x30208), (0x302EE, 0x302F2)));
                spots.AddRange(Code(a, 3, 1, (0x2FEFE, 0x2FF02), (0x2FF98, 0x2FF9C), (0x30074, 0x30078), (0x30230, 0x30234), (0x3031A, 0x3031E)));
                spots.AddRange(Code(a, 6, 0, (0x30162, 0x30166)));
            }
            return spots;
        }

        /// <summary>A battle touch screen page's touch list and cursor grid, from BattleCommandSites.TargetGrid.</summary>
        public sealed record BattleCommandTable(int Touch, int? Grid = null, int Width = 0, int Height = 0);

        /// <summary>The battle touch screen's pages in BattleCommandButtons order; HGSS overlay 12 keeps the tables in another order.</summary>
        public static BattleCommandTable[] BattleCommandTables(bool hgss) => new[]
        {
            new BattleCommandTable(hgss ? 0x14C : 0x174, 0x10, 3, 2),
            new BattleCommandTable(0x1B0, 0x08, 2, 3),
            new BattleCommandTable(0x1E0, 0x00, 2, 3),
            new BattleCommandTable(hgss ? 0x7C : 0x70, -0x14, 1, 2),
            new BattleCommandTable(hgss ? 0x30 : 0x38),
            new BattleCommandTable(hgss ? 0x64 : 0x7C, -0x10, 1, 2),
            new BattleCommandTable(hgss ? 0x1E : 0x28),
        };

        /// <summary>
        /// A battle Pokémon or bag screen's tables from BattleMenuSites.PartyCursors: its pointer table to the pages' cursor tables,
        /// each page's touch list, and the art position table (tile x, y, width, height per entry).
        /// </summary>
        public sealed record BattleMenuTables(int Pointers, int[] Touch, int Art, int ArtCount);

        // pokeplatinum battle_party_buttons.c and battle_bag_buttons.c sButtonDimensions; HGSS overlay 8 keeps the same distances
        // except for the bag's art table. Measured on every dump.
        public static BattleMenuTables BattleMenuTablesOf(bool bag, bool hgss) => bag
            ? new BattleMenuTables(0x83C, new[] { 0x690, 0x6AC, 0x620 }, hgss ? 0x9E0 : 0x9F0, 17)
            : new BattleMenuTables(0, new[] { -0x5A0, -0x668, -0x654, -0x560, -0x640, -0x628, -0x580, -0x678 }, 0x598, 34);

        /// <summary>A battle Pokémon or bag button's window template (tile x, y at +1) or icon (s32 pixel x, y), from PartyCursors.</summary>
        public sealed record BattleMenuSpot(int Page, int Button, int At, bool Window);

        private static BattleMenuSpot W(int page, int button, int table, int window) => new BattleMenuSpot(page, button, table + window * 8, true);

        private static BattleMenuSpot S(int page, int button, int table, int entry) => new BattleMenuSpot(page, button, table + entry * 8, false);

        // pokeplatinum battle_party_text.c and battle_bag_text.c window templates, battle_party_sprites.c and battle_bag_sprites.c
        // positions; HGSS overlay 8 keeps every table the same distance from the cursor tables. Measured on every dump.
        /// <summary>Each battle Pokémon or bag button's text windows and icons, in BattleMenuButtons page and button order.</summary>
        public static IReadOnlyList<BattleMenuSpot> BattleMenuSpots(bool bag)
        {
            List<BattleMenuSpot> spots = new List<BattleMenuSpot>();
            if (!bag)
            {
                const int party = -0x438, select = -0x4B0, summary = -0x290, moves = -0x3D0, restore = -0x490, learn = -0x468;
                const int confirm = -0x378, contest = -0x408, partyIcons = 0x78, statusIcons = 0xA8, moveTypes = -0x68, learnTypes = 0x50;
                for (int i = 0; i < 6; i++) spots.AddRange(new[] { W(0, i, party, i), S(0, i, partyIcons, i), S(0, i, statusIcons, i) });
                for (int i = 0; i < 3; i++) spots.Add(W(1, i, select, i + 1));
                spots.Add(W(2, 2, summary, 21));
                for (int i = 0; i < 4; i++) spots.AddRange(new[] { W(3, i, moves, i + 1), W(3, i, moves, i + 7), S(3, i, moveTypes, i) });
                spots.Add(W(3, 6, moves, 5));
                // Restore PP and Forget a move draw their move types from one table.
                for (int i = 0; i < 4; i++) spots.AddRange(new[] { W(5, i, restore, i + 1), S(5, i, learnTypes, i) });
                for (int i = 0; i < 5; i++) spots.AddRange(new[] { W(6, i, learn, i + 1), S(6, i, learnTypes, i) });
                spots.AddRange(new[] { W(7, 0, confirm, 11), W(7, 0, contest, 6) });
            }
            else
            {
                const int pockets = 0x6FC, items = 0x754, use = 0x6DC, lastUsed = 0x834, itemIcons = 0x8B8;
                for (int i = 0; i < 5; i++) spots.Add(W(0, i, pockets, i));
                spots.Add(S(0, 4, lastUsed, 0));
                // Each item's name and count, then the copies the page draws while it turns.
                for (int i = 0; i < 6; i++)
                    spots.AddRange(new[] { W(1, i, items, 2 * i), W(1, i, items, 2 * i + 1), W(1, i, items, 12 + 2 * i), W(1, i, items, 13 + 2 * i), S(1, i, itemIcons, i) });
                spots.Add(W(2, 0, use, 3));
            }
            return spots;
        }
    }
}
