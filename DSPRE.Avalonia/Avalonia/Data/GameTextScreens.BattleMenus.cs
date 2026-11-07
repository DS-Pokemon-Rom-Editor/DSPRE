using System;
using System.Collections.Generic;
using System.Linq;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.Data
{
    // Text and icons on the battle Pokémon and bag pages with sample data (pokeplatinum battle_party_text.c and battle_bag_text.c);
    // HGSS overlay 8 has the same window tables.
    public static partial class GameTextScreens
    {
        private const int PartyBank = 3, BagBank = 2, HgPartyBank = 6, HgBagBank = 5;
        private const int NameInk = 7, ListRow = 9;

        private static readonly (int X, int Y)[] BattleListWindows = { (0, 0), (16, 1), (0, 6), (16, 7), (0, 12), (16, 13) };
        private static readonly (int X, int Y)[] BattleMoveWindows = { (1, 6), (17, 6), (1, 12), (17, 12) };
        private static readonly (int X, int Y)[] BattleMoveTypes = { (24, 80), (152, 80), (24, 128), (152, 128) };

        // <summary>A button's window or icon spot from BattleMenuButtons.SpotsOf, or the game's spot when there's none.</summary>
        private delegate (int X, int Y) SpotOf(int button, int k, (int X, int Y) game);

        private static SpotOf Spots(IReadOnlyList<(int X, int Y)>[] spots) =>
            (button, k, game) => spots != null && button < spots.Length && k < spots[button].Count ? spots[button][k] : game;

        // A sample's stats on the summary page: Attack, Defense, Sp. Atk, Sp. Def and Speed, then the experience to the next level.
        private static readonly int[] SampleStats = { 168, 151, 207, 172, 186 };
        private const int SampleToNextLevel = 3742;

        // party_gauge.c (both games): batt_obj 340/341/342 coloured by 110. The player's arrow (animation 8) ends at 224,120 with the balls from
        // 162,114 every 16; the opponent's (7) at 32,56 with the balls from 94,50 every 16 leftwards. Balls: healthy, a status condition,
        // fainted (player 3 to 5, opponent 0 to 2), an empty slot 6. The sample trainer has three Pokémon.
        internal static void BattlePartyGauge(byte[] rgba) => WithArchive(() =>
        {
            if (Members(DirNames.BattleObj) is not Func<int, byte[]> m || NitroBgCodec.ReadColours(m(110)) is not ushort[] colours) return 0;
            Func<int, ushort[]> row = _ => DsBgScreen.Row(colours, 0);
            Sprite(rgba, m, 342, 341, 340, 224, 120, 8, row);
            Sprite(rgba, m, 342, 341, 340, 32, 56, 7, row);
            for (int i = 0; i < 6; i++)
            {
                PartySample.Pokemon who = PartySample.Party[i];
                int ours = who.Empty || who.Egg ? 6 : who.Hp == 0 ? 5 : who.Condition > 0 ? 4 : 3;
                Sprite(rgba, m, 342, 341, 340, 162 + 16 * i, 114, ours, row);
                Sprite(rgba, m, 342, 341, 340, 94 - 16 * i, 50, i < 3 ? 0 : 6, row);
            }
            return 0;
        });

        // battle_script.c SEQ_GET_EXP_LEVEL_UP_SUMMARY: a standard window at 17,7, 14 by 12, with Max. HP, Attack, Defense, Sp. Atk,
        // Sp. Def and Speed (stat names 8, 1, 2, 4, 5, 3) every 16 down and each gain ("+{0}", battle strings 948) right-aligned at 108.
        internal static void BattleLevelUp(byte[] rgba) => WithArchive(() =>
        {
            bool hg = GameFamily == GameFamilies.HGSS;
            int[] stats = { 8, 1, 2, 4, 5, 3 }, gains = { 3, 2, 2, 3, 1, 2 };
            ushort[] sys = SystemColours;
            StandardWindow(rgba, 17, 7, 14, 12, sys[15]);
            for (int i = 0; i < stats.Length; i++)
            {
                Print(rgba, SystemFont, Text(hg ? 443 : 551, stats[i]), 17 * 8, 7 * 8 + 16 * i, sys[1], sys[2]);
                string gain = Text(hg ? 197 : 368, 948, (_, kind, _) => kind == 51 ? gains[i].ToString() : null);
                Print(rgba, SystemFont, gain, 17 * 8 + 108, 7 * 8 + 16 * i, sys[1], sys[2], Align.Right);
            }
            return 0;
        });

        /// <summary>The battle Pokémon page's text and icons, in BattleMenuButtons page order.</summary>
        internal static void BattlePartyText(byte[] rgba, int page, ushort[] colours, IReadOnlyList<(int X, int Y)>[] spots = null) =>
            WithArchive(() => { DrawBattleParty(rgba, page, colours, Spots(spots)); return 0; });

        private static void DrawBattleParty(byte[] rgba, int page, ushort[] colours, SpotOf spot)
        {
            bool hg = GameFamily == GameFamilies.HGSS;
            int bank = TextBank(hg ? HgPartyBank : PartyBank);
            FieldFont names = Font(hg ? 4 : 2) ?? SystemFont;
            ushort[] list = DsBgScreen.Row(colours, ListRow);
            // battle_party.c builds row 13 from the system font's colours, with blue and red taken from row 9.
            ushort[] labels = (ushort[])SystemColours.Clone();
            (labels[7], labels[8], labels[3], labels[4]) = (list[10], list[11], list[12], list[13]);
            byte[] glyphs = NitroBgCodec.ReadCharacters(NitroBgCodec.Inflate(Members(DirNames.Fonts)?.Invoke(RomInfo.SpecialGlyphsFontEntry(hg))));
            PartySample.Pokemon first = PartySample.Party[0];

            void Button(int message, int button, int x, int y, int w)
            {
                (x, y) = spot(button, 0, (x, y));
                Centred(rgba, names, Line(bank, message), x * 8 + w * 4, y * 8 + 6, list);
            }
            void Message(int message, int cancel)
            {
                ushort[] talk = TalkColours;
                (int mx, int my) = spot(cancel, 0, (2, 21));
                Print(rgba, TalkFont, Line(bank, message), mx * 8, my * 8, talk[1], talk[2]);
            }

            switch (page)
            {
                case 0:
                    for (int n = 0; n < BattleListWindows.Length; n++)
                    {
                        (int X, int Y) icon = spot(n, 1, (BattleListWindows[n].X * 8 + 16, BattleListWindows[n].Y * 8 + 16));
                        BattleListPanel(rgba, n, spot(n, 0, BattleListWindows[n]), icon, spot(n, 2, (icon.X + 12, icon.Y + 24)), bank, names, list, glyphs, hg);
                    }
                    Message(6, 6);
                    break;
                case 1:
                {
                    // The name is centred in window 0 (10,4, 12 wide), 8 down.
                    (int nx, int ny) = spot(0, 1, (10, 4));
                    NameAndGender(rgba, first, bank, names, nx * 8 + 48, ny * 8 + 8, list, true);
                    Button(15, 0, 11, 12, 10);
                    Button(18, 1, 1, 20, 11);
                    Button(19, 2, 14, 20, 11);
                    (int ix, int iy) = spot(0, 2, (128, 72));
                    PartyIcon(rgba, first.Species, ix, iy);
                    break;
                }
                case 2:
                    SummaryPage(rgba, first, bank, labels);
                    Button(19, 2, 13, 20, 11);
                    break;
                case 3:
                case 5:
                case 6:
                    Header(rgba, first, bank, labels);
                    MoveWindows(rgba, bank, names, list, i => spot(i, 0, BattleMoveWindows[i]), i => spot(i, page == 3 ? 2 : 1, BattleMoveTypes[i]));
                    if (page == 3) Button(18, 6, 13, 20, 11);
                    if (page == 5) Message(94, 4);
                    if (page == 6) NewMove(rgba, names, list, spot(4, 0, (9, 18)), spot(4, 1, (88, 176)));
                    break;
                case 4:
                case 7:
                    Header(rgba, first, bank, labels);
                    MoveInfo(rgba, bank, labels);
                    if (page == 7) Button(58, 0, 7, 20, 12);
                    break;
            }
        }

        private static void Centred(byte[] rgba, FieldFont font, string text, int x, int y, ushort[] row) =>
            DsBgScreen.DrawText(rgba, font, text, x - DsBgScreen.MeasureText(font, text) / 2, y, row[NameInk], row[NameInk + 1], row[NameInk + 2]);

        private static string SpeciesName(PartySample.Pokemon who) => Line(PokemonNamesTextNumbers[0], who.Egg ? 494 : who.Species);

        private static void NameAndGender(byte[] rgba, PartySample.Pokemon who, int bank, FieldFont font, int x, int y, ushort[] row, bool centred)
        {
            string name = SpeciesName(who);
            int width = DsBgScreen.MeasureText(font, name), left = centred ? x - width / 2 : x;
            DsBgScreen.DrawText(rgba, font, name, left, y, row[NameInk], row[NameInk + 1], row[NameInk + 2]);
            if (who.Egg || who.Gender > 1) return;
            int ink = who.Gender == 0 ? 10 : 12;
            Print(rgba, SystemFont, Line(bank, 16 + who.Gender), left + width + 8, y, row[ink], row[ink + 1]);
        }

        private static void BattleListPanel(byte[] rgba, int n, (int X, int Y) window, (int X, int Y) icon, (int X, int Y) status, int bank,
                                            FieldFont font, ushort[] row, byte[] glyphs, bool hg)
        {
            PartySample.Pokemon who = PartySample.Party[n];
            if (who.Empty) return;
            (int wx, int wy) = (window.X * 8, window.Y * 8);
            PartyIcon(rgba, who.Egg ? 494 : who.Species, icon.X, icon.Y);
            // battle_party_sprites.c: the held item (animation 0, mail 1) 8,8 from the icon in the party menu's files, and the status
            // icon at its own spot in place of the level.
            if (who.Item > 0 && Members(DirNames.PartyMenuGraphics) is Func<int, byte[]> party)
                PartySprite(rgba, party, 18, 19, 20, icon.X + 8, icon.Y + 8, who.Item == 2 ? 1 : 0, -1, 21);
            int condition = who.Egg ? 0 : who.Hp == 0 ? 6 : who.Condition;
            if (condition > 0) StatusIcon(rgba, condition, status.X, status.Y, hg);
            string name = SpeciesName(who);
            DsBgScreen.DrawText(rgba, font, name, wx + 32, wy + 8, row[NameInk], row[NameInk + 1], row[NameInk + 2]);
            if (who.Egg) return;
            if (who.Gender < 2)
            {
                string mark = Line(bank, 16 + who.Gender);
                int ink = who.Gender == 0 ? 10 : 12;
                Print(rgba, SystemFont, mark, wx + 120 - DsBgScreen.MeasureText(SystemFont, mark), wy + 8, row[ink], row[ink + 1]);
            }
            if (who.Condition == 0 && who.Hp > 0)
            {
                Glyph(rgba, glyphs, 11, 2, wx + 8, wy + 32, row);
                Digits(rgba, glyphs, who.Level.ToString(), wx + 24, wy + 32, row);
            }
            string hp = who.Hp.ToString();
            Digits(rgba, glyphs, hp, wx + 56 + (3 - hp.Length) * 8, wy + 32, row);
            Glyph(rgba, glyphs, 10, 1, wx + 80, wy + 32, row);
            Digits(rgba, glyphs, who.MaxHp.ToString(), wx + 88, wy + 32, row);
            int length = who.MaxHp <= 0 ? 0 : Math.Max(who.Hp > 0 ? 1 : 0, who.Hp * 48 / who.MaxHp);
            int colour = length * 2 > 48 || who.Hp == who.MaxHp ? 1 : length * 5 > 48 ? 3 : 5;
            Fill(rgba, row[colour + 1], wx + 64, wy + 25, length, 1);
            Fill(rgba, row[colour], wx + 64, wy + 26, length, 2);
            Fill(rgba, row[colour + 1], wx + 64, wy + 28, length, 1);
        }

        private static void Header(byte[] rgba, PartySample.Pokemon who, int bank, ushort[] labels)
        {
            // The Layout tab's Header row (tile 2,1) moves the name, icon and types together.
            (int hx, int hy, int _, int _) = ScreenWindowLayouts.Window("battlepokemon", "Pictures", "Header", (2, 1, 24, 2));
            int dx = 8 * (hx - 2), dy = 8 * (hy - 1);
            string name = SpeciesName(who);
            Print(rgba, SystemFont, name, 5 * 8 + dx, 8 + dy, labels[15], labels[14]);
            if (who.Gender < 2)
            {
                int ink = who.Gender == 0 ? 7 : 3;
                Print(rgba, SystemFont, Line(bank, 16 + who.Gender), 5 * 8 + DsBgScreen.MeasureText(SystemFont, name) + 8 + dx, 8 + dy, labels[ink], labels[ink + 1]);
            }
            PartyIcon(rgba, who.Species, 24 + dx, 12 + dy);
            // battle_party_sprites.c DrawSelectedPokemonTypeIcons: the types at 130,16 and 164,16, the second only when it differs.
            try
            {
                PokemonPersonalData data = new PokemonPersonalData(who.Species);
                Paste(rgba, TypeIcons.PixelsFor((int)data.Type1), 130 - 16 + dx, 16 - 8 + dy);
                if (data.Type2 != data.Type1) Paste(rgba, TypeIcons.PixelsFor((int)data.Type2), 164 - 16 + dx, 16 - 8 + dy);
            }
            catch (Exception ex) { AppLogger.WarnOnce("GameTextScreens.Header", ex); }
        }

        private static void SummaryPage(byte[] rgba, PartySample.Pokemon who, int bank, ushort[] labels)
        {
            Header(rgba, who, bank, labels);
            void Label(int message, int x, int y) => Print(rgba, SystemFont, Line(bank, message), x * 8, y * 8, labels[15], labels[14]);
            void Value(string text, int right, int y) => Print(rgba, SystemFont, text, right, y * 8, labels[1], labels[2], Align.Right);
            Label(23, 1, 4);
            Print(rgba, SystemFont, who.Level.ToString(), 5 * 8, 4 * 8, labels[1], labels[2]);
            Label(25, 1, 6);
            Value(SampleToNextLevel.ToString(), 19 * 8, 6);
            Label(28, 21, 4);
            Value($"{who.Hp}/{who.MaxHp}", 31 * 8, 4);
            int[] statLabels = { 32, 34, 36, 38, 40 };
            for (int i = 0; i < statLabels.Length; i++)
            {
                Label(statLabels[i], 21, 7 + 2 * i);
                Value(SampleStats[i].ToString(), 31 * 8, 7 + 2 * i);
            }
            // Blaze, and no item.
            Print(rgba, SystemFont, Line(AbilityNamesTextNumber, 66), 8, 9 * 8, labels[1], labels[2]);
            Print(rgba, SystemFont, Line(bank, 20), 4 * 8, 16 * 8, labels[1], labels[2]);
        }

        // PP counts sit either side of the slash at x 80.
        private static void MoveWindows(byte[] rgba, int bank, FieldFont font, ushort[] row, Func<int, (int X, int Y)> window, Func<int, (int X, int Y)> typeIcon)
        {
            for (int i = 0; i < SampleMoves.Length; i++)
            {
                (int move, int type, int left, int max) = SampleMoves[i];
                (int wx, int wy) = (window(i).X * 8, window(i).Y * 8);
                Centred(rgba, font, Line(MoveNamesTextNumbers, move), wx + 56, wy + 8, row);
                Print(rgba, SystemFont, Line(bank, 43), wx + 40, wy + 24, row[15], row[14]);
                Print(rgba, SystemFont, Line(bank, 46), wx + 80, wy + 24, row[15], row[14]);
                Print(rgba, SystemFont, left.ToString(), wx + 80, wy + 24, row[15], row[14], Align.Right);
                Print(rgba, SystemFont, max.ToString(), wx + 80 + DsBgScreen.MeasureText(SystemFont, Line(bank, 46)), wy + 24, row[15], row[14]);
                Paste(rgba, TypeIcons.PixelsFor(type), typeIcon(i).X - 16, typeIcon(i).Y - 8);
            }
        }

        // Thunder Punch, the move a Typhlosion could learn.
        private static void NewMove(byte[] rgba, FieldFont font, ushort[] row, (int X, int Y) window, (int X, int Y) typeIcon)
        {
            DsBgScreen.DrawText(rgba, font, Line(MoveNamesTextNumbers, 9), window.X * 8, window.Y * 8 + 8, row[NameInk], row[NameInk + 1], row[NameInk + 2]);
            Paste(rgba, TypeIcons.PixelsFor(13), typeIcon.X - 16, typeIcon.Y - 8);
        }

        // Tackle's details: physical, 35 power, 95 accuracy.
        private static void MoveInfo(byte[] rgba, int bank, ushort[] labels)
        {
            (int move, int type, int left, int max) = SampleMoves[0];
            void Text(string text, int x, int y, bool label, Align align = Align.Left) =>
                Print(rgba, SystemFont, text, x, y, labels[label ? 15 : 1], labels[label ? 14 : 2], align);
            Text(Line(MoveNamesTextNumbers, move), 4 * 8, 5 * 8, false);
            Text(Line(bank, 43), 20 * 8, 5 * 8, true);
            Text($"{left}/{max}", 28 * 8, 5 * 8, false, Align.Right);
            Text(Line(bank, 53), 1 * 8 + 48, 8 * 8, true, Align.Centre);
            Text(Line(bank, 54), 6 * 8 + 28, 10 * 8, false, Align.Centre);
            Text(Line(bank, 48), 8, 13 * 8, true);
            Text("35", 13 * 8, 13 * 8, false, Align.Right);
            Text(Line(bank, 51), 8, 16 * 8, true);
            Text("95", 13 * 8, 16 * 8, false, Align.Right);
            string description = Line(MoveDescriptionsTextNumbers, move);
            Print(rgba, SystemFont, description, 16 * 8, 8 * 8, labels[1], labels[2]);
            Paste(rgba, TypeIcons.PixelsFor(type), 136 - 16, 48 - 8);
            Paste(rgba, TypeIcons.CategoryPixels(0), 24 - 16, 88 - 8);
        }

        // A pocket's sample: Potion, Super Potion, Hyper Potion, Max Potion, Full Restore and Revive.
        private static readonly (int Item, int Count)[] SampleItems = { (17, 5), (26, 3), (25, 2), (24, 1), (23, 1), (28, 2) };
        private static readonly (int X, int Y)[] BagNameWindows = { (1, 1), (17, 1), (1, 7), (17, 7), (1, 13), (17, 13) };
        private static readonly (int X, int Y)[] BagCountWindows = { (8, 4), (24, 4), (8, 10), (24, 10), (8, 16), (24, 16) };
        // Pocket windows, 12 tiles wide, for HP and PP, status, Poké Balls and battle items (sMenuScreenWindowTemplates).
        private static readonly (int X, int Y)[] PocketWindows = { (2, 4), (2, 13), (18, 5), (18, 14) };
        private static readonly (int X, int Y)[] BagIcons = { (44, 45), (172, 45), (44, 93), (172, 93), (44, 141), (172, 141) };

        /// <summary>The battle bag page's text and icons, in BattleMenuButtons page order.</summary>
        internal static void BattleBagText(byte[] rgba, int page, ushort[] colours, IReadOnlyList<(int X, int Y)>[] spots = null) =>
            WithArchive(() => { DrawBattleBag(rgba, page, colours, Spots(spots)); return 0; });

        private static void DrawBattleBag(byte[] rgba, int page, ushort[] colours, SpotOf spot)
        {
            bool hg = GameFamily == GameFamilies.HGSS;
            int bank = TextBank(hg ? HgBagBank : BagBank);
            FieldFont font = Font(hg ? 4 : 2) ?? SystemFont;
            ushort[] button = DsBgScreen.Row(colours, 0), plain = DsBgScreen.Row(colours, 4);
            void Button(string text, int x, int y) =>
                DsBgScreen.DrawText(rgba, font, text, x - DsBgScreen.MeasureText(font, text) / 2, y, button[3], button[2], button[1]);
            void Plain(string text, int x, int y, Align align = Align.Left) => Print(rgba, SystemFont, text, x, y, plain[1], plain[2], align);

            switch (page)
            {
                case 0:
                    // The first two pocket names take two lines.
                    int[][] pocketLines = { new[] { 0, 1 }, new[] { 2, 3 }, new[] { 7 }, new[] { 6 } };
                    for (int i = 0; i < pocketLines.Length; i++)
                    {
                        (int px, int py) = spot(i, 0, PocketWindows[i]);
                        for (int l = 0; l < pocketLines[i].Length; l++) Button(Line(bank, pocketLines[i][l]), px * 8 + 48, py * 8 + 8 + l * 16);
                    }
                    (int lx, int ly) = spot(4, 0, (5, 20));
                    Button(Line(bank, 8), lx * 8 + 80, ly * 8 + 6);
                    (int lix, int liy) = spot(4, 1, (24, 178));
                    ItemIcon(rgba, SampleItems[0].Item, lix, liy);
                    break;
                case 1:
                    for (int i = 0; i < SampleItems.Length; i++)
                    {
                        (int item, int count) = SampleItems[i];
                        (int nx, int ny) = spot(i, 0, BagNameWindows[i]);
                        (int cx, int cy) = spot(i, 1, BagCountWindows[i]);
                        (int ix, int iy) = spot(i, 4, BagIcons[i]);
                        Button(Line(ItemNamesTextNumber, item), nx * 8 + 56, ny * 8 + 8);
                        Plain($"x{count}", cx * 8, cy * 8 + 4);
                        ItemIcon(rgba, item, ix, iy);
                    }
                    // The pocket name centred in window 24 (11,19, 10 wide) and the page counter in window 25 (22,20, 4 wide).
                    int previous = hg ? 7 : 6;
                    (int nameX, int nameY) = spot(previous, 0, (11, 19));
                    (int gx, int gy) = spot(previous, 1, (22, 20));
                    Plain(Line(bank, 22), nameX * 8 + 40, nameY * 8 + 4, Align.Centre);
                    Plain(Line(bank, 23), nameX * 8 + 40, nameY * 8 + 20, Align.Centre);
                    Plain(Line(bank, 28), gx * 8 + 16, gy * 8 + 4, Align.Centre);
                    Plain("1", gx * 8 + 12, gy * 8 + 4, Align.Right);
                    Plain("1", gx * 8 + 20, gy * 8 + 4);
                    break;
                case 2:
                    (int first, int many) = SampleItems[0];
                    (int useNameX, int useNameY) = spot(0, 1, (7, 4));
                    (int useCountX, int useCountY) = spot(0, 2, (20, 4));
                    (int useTextX, int useTextY) = spot(0, 3, (2, 9));
                    Plain(Line(ItemNamesTextNumber, first), useNameX * 8, useNameY * 8);
                    Plain($"x{many}", useCountX * 8, useCountY * 8);
                    Plain(Line(ItemDescriptionsTextNumber, first), useTextX * 8 + 4, useTextY * 8);
                    // HGSS's USE window is 10 tiles wide at x 8; Platinum's is 6 wide at x 10.
                    (int ux, int uy) = spot(0, 0, (hg ? 8 : 10, 20));
                    Button(Line(bank, 31), ux * 8 + (hg ? 40 : 24), uy * 8 + 6);
                    (int useIconX, int useIconY) = spot(0, 4, (40, 44));
                    ItemIcon(rgba, first, useIconX, useIconY);
                    break;
            }
        }

        // The item's icon and palette from the item table, 32 by 32, centred on the spot.
        private static void ItemIcon(byte[] rgba, int item, int x, int y)
        {
            try
            {
                if (!ItemTable.Exists(item)) return;
                DSPRE.Editors.ItemNarcTableEntry entry = ItemTable.Read(item);
                Func<int, byte[]> m = Members(DirNames.ItemIcons);
                byte[] tiles = m == null ? null : NitroBgCodec.ReadCharacters(m((int)entry.ItemIcon));
                ushort[] palette = m == null ? null : NitroBgCodec.ReadColours(m((int)entry.ItemPalette));
                if (tiles == null || palette == null) return;
                for (int py = 0; py < 32; py++)
                    for (int px = 0; px < 32; px++)
                    {
                        int tile = py / 8 * 4 + px / 8, at = tile * 32 + (py % 8 * 8 + px % 8) / 2;
                        if (at >= tiles.Length) continue;
                        int value = (px & 1) != 0 ? tiles[at] >> 4 : tiles[at] & 0xF;
                        if (value == 0 || value >= palette.Length) continue;
                        int sx = x - 16 + px, sy = y - 16 + py;
                        if (sx < 0 || sy < 0 || sx >= W || sy >= H) continue;
                        (byte r, byte g, byte b) = NitroBgCodec.Colour(palette[value]);
                        int to = (sy * W + sx) * 4;
                        (rgba[to], rgba[to + 1], rgba[to + 2], rgba[to + 3]) = (r, g, b, 255);
                    }
            }
            catch (Exception ex) { AppLogger.WarnOnce("GameTextScreens.ItemIcon", ex); }
        }
    }
}
