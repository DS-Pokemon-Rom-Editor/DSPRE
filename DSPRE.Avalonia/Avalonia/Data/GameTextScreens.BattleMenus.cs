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
            byte[] glyphs = NitroBgCodec.ReadCharacters(NitroBgCodec.Inflate(Members(DirNames.Fonts)?.Invoke(hg ? 5 : 4)));
            PartySample.Pokemon first = PartySample.Party[0];

            void Button(int message, int button, int x, int y, int w)
            {
                (x, y) = spot(button, 0, (x, y));
                Centred(rgba, names, Line(bank, message), x * 8 + w * 4, y * 8 + 6, list);
            }
            void Message(int message)
            {
                ushort[] talk = TalkColours;
                Print(rgba, TalkFont, Line(bank, message), 2 * 8, 21 * 8, talk[1], talk[2]);
            }

            switch (page)
            {
                case 0:
                    for (int n = 0; n < BattleListWindows.Length; n++)
                        BattleListPanel(rgba, n, spot(n, 0, BattleListWindows[n]), spot(n, 1, (BattleListWindows[n].X * 8 + 16, BattleListWindows[n].Y * 8 + 16)), bank, names, list, glyphs);
                    Message(6);
                    break;
                case 1:
                    NameAndGender(rgba, first, bank, names, 80 + 48, 40, list, true);
                    Button(15, 0, 11, 12, 10);
                    Button(18, 1, 1, 20, 11);
                    Button(19, 2, 14, 20, 11);
                    PartyIcon(rgba, first.Species, 128, 72);
                    break;
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
                    if (page == 5) Message(94);
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

        private static void BattleListPanel(byte[] rgba, int n, (int X, int Y) window, (int X, int Y) icon, int bank, FieldFont font, ushort[] row, byte[] glyphs)
        {
            PartySample.Pokemon who = PartySample.Party[n];
            if (who.Empty) return;
            (int wx, int wy) = (window.X * 8, window.Y * 8);
            PartyIcon(rgba, who.Egg ? 494 : who.Species, icon.X, icon.Y);
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
            string name = SpeciesName(who);
            Print(rgba, SystemFont, name, 5 * 8, 8, labels[15], labels[14]);
            if (who.Gender < 2)
            {
                int ink = who.Gender == 0 ? 7 : 3;
                Print(rgba, SystemFont, Line(bank, 16 + who.Gender), 5 * 8 + DsBgScreen.MeasureText(SystemFont, name) + 8, 8, labels[ink], labels[ink + 1]);
            }
            PartyIcon(rgba, who.Species, 24, 12);
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
                    Plain(Line(bank, 22), 16 * 8, 19 * 8 + 4, Align.Centre);
                    Plain(Line(bank, 23), 16 * 8, 19 * 8 + 20, Align.Centre);
                    Plain(Line(bank, 28), 24 * 8, 20 * 8 + 4, Align.Centre);
                    Plain("1", 24 * 8 - 4, 20 * 8 + 4, Align.Right);
                    Plain("1", 24 * 8 + 4, 20 * 8 + 4);
                    break;
                case 2:
                    (int first, int many) = SampleItems[0];
                    Plain(Line(ItemNamesTextNumber, first), 7 * 8, 4 * 8);
                    Plain($"x{many}", 20 * 8, 4 * 8);
                    Plain(Line(ItemDescriptionsTextNumber, first), 2 * 8 + 4, 9 * 8);
                    // HGSS's USE window is 10 tiles wide at x 8; Platinum's is 6 wide at x 10.
                    (int ux, int uy) = spot(0, 0, (hg ? 8 : 10, 20));
                    Button(Line(bank, 31), ux * 8 + (hg ? 40 : 24), uy * 8 + 6);
                    ItemIcon(rgba, first, 40, 44);
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
