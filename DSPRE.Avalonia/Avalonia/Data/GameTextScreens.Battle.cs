using System;
using System.Collections.Generic;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.Data
{
    // The battle touch screen's labels with a sample Pokémon and moves (pokeplatinum battle_subscreen.c, pokeheartgold battle_input.c).
    public static partial class GameTextScreens
    {
        // A label: its battle string, where the game centres it in Platinum and in HGSS, and its colours in label palette row 2.
        private sealed record BattleLabel(int Message, int X, int PtY, int HgY, int Ink, bool Centred = true);

        private const int Fight = 924, Bag = 925, PokemonLabel = 926, Run = 927, Cancel = 929, TargetCancel = 930, Yes = 940, No = 941;
        private const int ParkBall = 1223, Stop = 1260, PpLabel = 938;
        private const int LabelRow = 2, MoveRow = 3, PpRow = 4, TargetRow = 6;

        // Each colour set is letter, shadow and outline.
        private static readonly BattleLabel FightLabel = new(Fight, 128, 84, 83, 1);
        private static readonly BattleLabel RunLabel = new(Run, 128, 178, 176, 10);
        private static readonly BattleLabel[][] BattlePageLabels =
        {
            new[] { FightLabel, new BattleLabel(Bag, 40, 170, 169, 4), new BattleLabel(PokemonLabel, 216, 170, 168, 7), RunLabel },
            new[] { new BattleLabel(Cancel, 128, 178, 175, 10) },
            new[] { new BattleLabel(TargetCancel, 128, 178, 175, 10) },
            new[] { new BattleLabel(Yes, 128, 68, 67, 1), new BattleLabel(No, 128, 140, 139, 10) },
            new[] { FightLabel },
            new[] { FightLabel with { Message = ParkBall }, RunLabel },
            new[] { new BattleLabel(Stop, 128, 178, 171, 1) },
        };

        // Tackle, Ember, Water Gun and Vine Whip, as the Fight panel's sample move types, with one PP of each colour.
        private static readonly (int Move, int Type, int Pp, int MaxPp)[] SampleMoves = { (33, 0, 35, 35), (52, 10, 12, 25), (55, 11, 5, 25), (22, 12, 0, 25) };
        private static readonly (int X, int PtY, int HgY)[] MoveSpots = { (64, 46, 45), (192, 46, 44), (64, 110, 108), (192, 110, 107) };
        private static readonly (int PtY, int HgY)[] PpRows = { (62, 61), (62, 60), (126, 124), (126, 123) };
        private static readonly (int X, int PtY, int HgY)[] MoveTypeIcons = { (32, 62, 61), (160, 62, 60), (32, 126, 124), (160, 126, 123) };

        // The player's first Pokémon at bottom left, a foe at top right, the partner at bottom right and the other foe at top left.
        private static readonly (int X, int Y)[] TargetSpots = { (60, 116), (196, 32), (196, 116), (60, 32) };
        private static readonly int[] SampleFoes = { 74, 41 };

        // Which button's spots each label uses; the first battle and Pal Park use Command's.
        private static (int Page, int Button) LabelButton(int page, int label) => page switch
        {
            0 or 3 => (page, label),
            1 => (1, 0),
            2 => (2, 4),
            4 => (0, 0),
            5 => (0, label == 0 ? 0 : 3),
            _ => (page, 0),
        };

        /// <summary>
        /// Draws a battle touch screen page's labels (BattleCommandButtons order) at the text spots <paramref name="textOf"/> gives for each
        /// page and button.
        /// </summary>
        internal static void BattleTouchLabels(byte[] rgba, int page, Func<int, int, IReadOnlyList<(int X, int Y)>> textOf)
        {
            if (rgba == null || page < 0 || page >= BattlePageLabels.Length || BattleStringsTextNumber < 0) return;
            WithArchive(() => { DrawBattleLabels(rgba, page, textOf); return 0; });
        }

        private static void DrawBattleLabels(byte[] rgba, int page, Func<int, int, IReadOnlyList<(int X, int Y)>> textOf)
        {
            (int X, int Y)? Spot(int p, int b, int k) => textOf?.Invoke(p, b) is { } list && list.Count > k ? list[k] : null;
            bool hg = GameFamily == GameFamilies.HGSS;
            FieldFont font = Font(hg ? 4 : 2) ?? SystemFont;
            ushort[] colours = LabelColours();
            ushort[] Row(int row) => DsBgScreen.Row(colours, row);
            int Y(int pt, int hgY) => hg ? hgY : pt;

            for (int j = 0; j < BattlePageLabels[page].Length; j++)
            {
                BattleLabel label = BattlePageLabels[page][j];
                string text = Line(BattleStringsTextNumber, label.Message);
                (int lp, int lb) = LabelButton(page, j);
                (int x, int y) = Spot(lp, lb, 0) ?? (label.X, Y(label.PtY, label.HgY));
                int top = y - 8;
                ushort[] row = Row(LabelRow);
                int left = x - DsBgScreen.MeasureText(font, text) / 2;
                DsBgScreen.DrawText(rgba, font, text, left, top, row[label.Ink], row[label.Ink + 1], row[label.Ink + 2]);
                // ov16 places the active Pokémon's icon left of FIGHT; Pal Park shows none.
                if (label.Message == Fight && page != 5 && !PartySample.Party[0].Empty) PartyIcon(rgba, PartySample.Party[0].Species, left - 16, top);
            }

            if (page == 1) FightPage(rgba, font, Row, Y, Spot);
            if (page == 2) TargetPage(rgba, font, Row(TargetRow), Spot);
        }

        private static void FightPage(byte[] rgba, FieldFont font, Func<int, ushort[]> row, Func<int, int, int> y, Func<int, int, int, (int X, int Y)?> spot)
        {
            ushort[] move = row(MoveRow), pp = row(PpRow);
            string ppLabel = Line(BattleStringsTextNumber, PpLabel);
            for (int i = 0; i < SampleMoves.Length; i++)
            {
                (int id, int type, int left, int max) = SampleMoves[i];
                string name = Line(MoveNamesTextNumbers, id);
                (int mx, int mpt, int mhg) = MoveSpots[i];
                int x = i % 2 == 0 ? 0 : 128, ink = PpInk(left, max);
                (int nx, int ny) = spot(1, i + 1, 0) ?? (mx, y(mpt, mhg));
                (int lx, int ly) = spot(1, i + 1, 1) ?? (59 + x, y(PpRows[i].PtY, PpRows[i].HgY));
                (int cx, int cy) = spot(1, i + 1, 2) ?? (76 + x, ly);
                (int tx, int ty) = spot(1, i + 1, 3) ?? (MoveTypeIcons[i].X, y(MoveTypeIcons[i].PtY, MoveTypeIcons[i].HgY));
                DsBgScreen.DrawText(rgba, font, name, nx - DsBgScreen.MeasureText(font, name) / 2, ny - 8, move[7], move[8], move[9]);
                DsBgScreen.DrawText(rgba, SystemFont, ppLabel, lx, ly - 8, pp[ink], pp[ink + 1]);
                DsBgScreen.DrawText(rgba, SystemFont, $"{left,2}/{max,2}", cx, cy - 8, pp[ink], pp[ink + 1]);
                Paste(rgba, TypeIcons.PixelsFor(type), tx - 16, ty - 8);
            }
        }

        // GetPPTextColor colours empty, full, quarter-left and half-left PP; moves with 7 PP or less count whole points.
        private static int PpInk(int left, int max)
        {
            if (left == 0) return 7;
            if (left == max) return 1;
            if (max <= 2) return left == 1 ? 5 : 1;
            if (max <= 7) return left == 1 ? 5 : left == 2 ? 3 : 1;
            return left <= max / 4 ? 5 : left <= max / 2 ? 3 : 1;
        }

        // Foes use colours 1 to 3 and the player's side 4 to 6; male marks use 7 and 8, female 9 and 10.
        private static void TargetPage(byte[] rgba, FieldFont font, ushort[] row, Func<int, int, int, (int X, int Y)?> spot)
        {
            int[] species = { PartySample.Party[0].Species, SampleFoes[0], PartySample.Party[1].Species, SampleFoes[1] };
            int[] genders = { PartySample.Party[0].Gender, 1, PartySample.Party[1].Gender, 0 };
            for (int slot = 0; slot < TargetSpots.Length; slot++)
            {
                if (species[slot] <= 0) continue;
                bool foe = slot % 2 == 1;
                int ink = foe ? 1 : 4;
                string name = Line(PokemonNamesTextNumbers[0], species[slot]);
                string mark = genders[slot] switch { 0 => "♂", 1 => "♀", _ => "" };
                int width = DsBgScreen.MeasureText(font, name + mark);
                (int x, int y) = spot(2, slot, 0) ?? TargetSpots[slot];
                int left = x - width / 2, top = y - 8;
                DsBgScreen.DrawText(rgba, font, name, left, top, row[ink], row[ink + 1], row[ink + 2]);
                int markInk = genders[slot] == 0 ? 7 : 9;
                DsBgScreen.DrawText(rgba, font, mark, left + DsBgScreen.MeasureText(font, name), top, row[markInk], row[markInk + 1], row[ink + 2]);
                (int ix, int iy) = spot(2, slot, 1) ?? (x, 56);
                if (foe) PartyIcon(rgba, species[slot], ix, iy);
            }
        }

        // The touch screen labels' sprite palette (battle object member 72).
        private static ushort[] LabelColours()
        {
            int at = BattleObjects.Find("MessageFrame.Shared", "Colours");
            byte[] file = at < 0 ? null : Members(DirNames.BattleObj)?.Invoke(at);
            return file == null ? null : NitroBgCodec.ReadColours(GraphicAssets.Unsqueeze(file));
        }

        private static void Paste(byte[] rgba, GraphicAssets.Preview p, int left, int top)
        {
            if (p?.Rgba == null) return;
            for (int yy = 0; yy < p.Height; yy++)
                for (int xx = 0; xx < p.Width; xx++)
                {
                    int from = (yy * p.Width + xx) * 4, x = left + xx, y = top + yy;
                    if (p.Rgba[from + 3] == 0 || x < 0 || y < 0 || x >= W || y >= H) continue;
                    Array.Copy(p.Rgba, from, rgba, (y * W + x) * 4, 4);
                }
        }
    }
}
