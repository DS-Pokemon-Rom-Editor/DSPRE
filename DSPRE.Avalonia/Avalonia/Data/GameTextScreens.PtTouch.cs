using System;
using System.Collections.Generic;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.Data
{
    // Bottom screens for the touch area tabs: the Platinum bag, summary, badge case and PC (pokeplatinum src/applications), and the
    // HGSS options, main menu, trainer card and number pad.
    public static partial class GameTextScreens
    {
        // A 5 by 5 button from a sheet 30 tiles wide: each type's three looks sit side by side, two types to a 150-tile band.
        private static void SheetButton(DsBgScreen s, int bg, int type, int x, int y, int palette)
        {
            int first = type / 2 * 150 + type % 2 * 15 + 30;
            ushort[] block = new ushort[25];
            for (int yy = 0; yy < 5; yy++)
                for (int xx = 0; xx < 5; xx++)
                    block[yy * 5 + xx] = (ushort)((first + yy * 30 + xx) | palette << 12);
            s.Put(bg, block, x, y, 5, 5);
        }

        // bag/main.c draws the Poké Ball (32, 33), the frame (15, 17), the pocket buttons (19, palettes 18 from row 2) at their spots, and
        // the dial button at 13,7.
        internal static byte[] BagBottomPt(IReadOnlyList<(int X, int Y)> spots)
        {
            (DsBgScreen s, Func<int, byte[]> m) = Bg(DirNames.BagGraphics);
            if (s == null) return null;
            Palettes(s, NitroBgCodec.ReadColours(m(16)));
            Palettes(s, NitroBgCodec.ReadColours(m(18)), 2, 2);
            // The dial is an affine layer: 256-colour tiles on the standard palette.
            Layer(s, m, 3, 3, 0x10000, 32, 33);
            s.UseWholePalette(3);
            Layer(s, m, 1, 1, 0x8000, 15, 17);
            ExtendedColours(s, m, 1, BottomPalettesPatch.Bag);
            s.InitLayer(0, 0, 0);
            s.LoadTiles(0, m(19));
            for (int i = 0; spots != null && i < spots.Count; i++) SheetButton(s, 0, i, spots[i].X, spots[i].Y, 2);
            ushort[] dial = new ushort[36];
            // bag/main.c DrawDialButton: palette 3.
            for (int i = 0; i < 36; i++) dial[i] = (ushort)((0x276 + i / 6 * 30 + i % 6) | 3 << 12);
            s.Put(0, dial, 13, 7, 6, 6);
            return s.Render();
        }

        // subscreen.c draws the backdrop (16, 15, palette 14), then each page button from sheet 2. An entry is page, palette, tile x, tile y.
        internal static byte[] SummaryBottomPt(IReadOnlyList<byte[]> entries, bool noContest)
        {
            (DsBgScreen s, Func<int, byte[]> m) = Bg(DirNames.SummaryGraphics);
            if (s == null) return null;
            Palettes(s, NitroBgCodec.ReadColours(m(14)));
            Layer(s, m, 1, 1, 0x8000, 16, 15);
            ExtendedColours(s, m, 1, BottomPalettesPatch.Summary);
            s.InitLayer(0, 0, 0);
            s.LoadTiles(0, m(2));
            foreach (byte[] entry in entries ?? Array.Empty<byte[]>())
            {
                if (entry[0] == 0xFF) break;
                SheetButton(s, 0, entry[0], entry[2], entry[3], entry[1]);
            }
            // The list without contest buttons scrolls the button layer 4 px so its five buttons sit centred.
            if (noContest) s.SetScroll(0, 4, 0);
            return s.Render();
        }

        // trainer_case/main.c: the case (29, 38) under the lid (30, 39) on BG3, the lid scaled to nothing once open. Each badge is a
        // sequence in 46 (cells 44, tiles 33), palette 16 plus the badge number.
        // trainer_case/sprites.c sSparkleCoordinates less sBadgeCoordinates: the top row's sparkles sit on the badge, the bottom row's 24 lower.
        private static readonly int[] SparkleDown = { 0, 0, 0, 0, 24, 24, 24, 24 };
        private const int FourSparkles = 9;

        internal static byte[] TrainerCaseBottomPt(IReadOnlyList<(int X, int Y)> badges, bool lid = false, bool polished = false)
        {
            (DsBgScreen s, Func<int, byte[]> m) = Bg(DirNames.TrainerCardGraphics);
            if (s == null) return null;
            Palettes(s, NitroBgCodec.ReadColours(m(PlatinumTrainerCardCase.LidNclr)));
            Palettes(s, NitroBgCodec.ReadColours(m(PlatinumTrainerCardCase.CaseNclr)), 0, 1);
            Layer(s, m, 2, 3, 0, 29, 38);
            if (lid)
            {
                Layer(s, m, 3, 0, 0x10000, PlatinumTrainerCardCase.LidNcgr, PlatinumTrainerCardCase.LidNscr);
                s.UseWholePalette(3);
                return s.Render();
            }
            byte[] rgba = s.Render();
            ushort[] obj = NitroBgCodec.ReadColours(m(14));
            for (int i = 0; badges != null && i < badges.Count && i < 8; i++)
            {
                ushort[] own = DsBgScreen.Row(NitroBgCodec.ReadColours(m(16 + i)), 0);
                Sprite(rgba, m, 46, 44, 33, badges[i].X, badges[i].Y, i, row => row == i ? own : DsBgScreen.Row(obj, row));
            }
            // Fully polished, each badge shows its four-sparkle sequence on top.
            for (int i = 0; polished && badges != null && i < badges.Count && i < 8; i++)
                Sprite(rgba, m, 46, 44, 33, badges[i].X, badges[i].Y + SparkleDown[i], FourSparkles, row => DsBgScreen.Row(obj, row));
            return rgba;
        }

        /// <summary>Platinum's trainer card front, on the top screen above the badge case.</summary>
        internal static byte[] TrainerCardTopPt() => WithArchive(() => TrainerCard(0, false)?.Rgba);

        // HGSS badge case: BG2 holds the case and portraits, BG3 the backdrop, and badges are sprites at BadgePos. After the League it
        // adds Kanto's row and the Hall of Fame mark moves from 0,7 to 0,14. Before it, the eight badges sit 40 lower.
        internal static byte[] TrainerCardTopHg(bool cleared = false) => WithArchive(() =>
        {
            (DsBgScreen s, Func<int, byte[]> m) = Bg(DirNames.TrainerCardGraphics);
            if (s == null) return null;
            Palettes(s, NitroBgCodec.ReadColours(m(HgssTrainerCardCase.BadgeCaseNclr)));
            Layer(s, m, 2, 2, 0, HgssTrainerCardCase.BadgeCaseNcgr, cleared ? HgssTrainerCardCase.ClearedNscr : HgssTrainerCardCase.JohtoNscr);
            Layer(s, m, 3, 3, 0, -1, HgssTrainerCardCase.BackdropNscr);
            (int markWidth, ushort[] mark) = NitroBgCodec.ReadMap(m(HgssTrainerCardCase.MarkNscr));
            // The mark moves with its Layout row (Screen positions patch), 7 lower after the League.
            (int hx, int hy, int _, int _) = ScreenWindowLayouts.Window("trainercard", "Pictures", "Hall of Fame mark", (0, 7, 7, 9));
            if (mark != null) s.Copy(2, hx, hy + (cleared ? 7 : 0), 7, 9, mark, cleared ? 7 : 0, 0, Math.Max(1, markWidth));
            byte[] rgba = s.Render();
            ushort[] badgeColours = NitroBgCodec.ReadColours(m(HgssTrainerCardCase.BadgeNclr));
            int[] across = { 96, 144, 192, 240 }, down = { 48, 88, 136, 176 };
            // The Layout's Badges block (RomLocator.TrainerCardCodeSitesIn) moves them all in whole tiles from 10,4.
            (int bx, int by, int _, int _) = ScreenWindowLayouts.Window("trainercard", "Pictures", "Badges", (10, 4, 22, 20));
            for (int i = 0; badgeColours != null && i < (cleared ? 16 : 8); i++)
                Sprite(rgba, m, HgssTrainerCardCase.BadgeNanr, HgssTrainerCardCase.BadgeNcer, HgssTrainerCardCase.BadgeNcgr,
                       across[i % 4] + 8 * (bx - 10), down[i / 4] + 8 * (by - 4) + (cleared ? 0 : 40), i, row => DsBgScreen.Row(badgeColours, row));
            return rgba;
        });

        /// <summary>The HGSS options bottom screen, drawn like its text preview.</summary>
        /// <param name="look">The options' screen, choice label x per row and Quit and Confirm spots as edited, or null for the game's.</param>
        internal static byte[] OptionsBottomHg((byte[] Screen, int[][] Labels, (int, int) Quit, (int, int) Confirm)? look) =>
            look is { } l ? WithArchive(() => OptionsHg(3, l.Screen, l.Labels, l.Quit, l.Confirm)?.Rgba) : WithArchive(() => OptionsHg(3)?.Rgba);

        /// <summary>The HGSS options top screen: the backdrop and the text speed test window.</summary>
        internal static byte[] OptionsTopHg() => WithArchive(() => OptionsHg(40)?.Rgba);

        /// <param name="buttons">The New game warning's two windows in tiles, drawn on page 1.</param>
        internal static byte[] MainMenuBottomHg(int page, IReadOnlyList<(int X, int Y)> buttons) =>
            WithArchive(() => (page == 1 && buttons is { Count: 2 } ? NewGameWarningHg(buttons[0], buttons[1]) : MainMenu(0, true))?.Rgba);

        /// <param name="cancel">The Cancel label's window spot in tiles.</param>
        /// <param name="hint">The hint label's window spot in tiles.</param>
        internal static byte[] TrainerCardBottomHg((int X, int Y)? cancel, (int X, int Y)? hint) =>
            WithArchive(() => TrainerCard(0, true, cancel, hint)?.Rgba);

        // ov29_0225D6C8 draws the pad (tiles 9, screen 10, palette 8). Back, Confirm and Quit are sprites under bank 196 lines 24 to 26,
        // centred in 8-tile windows in font 4.
        internal static byte[] NumberPadHg(IReadOnlyList<(int X, int Y)> keys, IReadOnlyList<(int X, int Y)> labels) => WithArchive(() =>
        {
            (DsBgScreen s, Func<int, byte[]> m) = Bg(DirNames.NumberPadGraphics);
            if (s == null) return null;
            ushort[] colours = NitroBgCodec.ReadColours(m(8));
            Palettes(s, colours);
            Layer(s, m, 1, 1, 0, 9, 10);
            byte[] rgba = s.Render();
            foreach ((int x, int y) in keys ?? Array.Empty<(int, int)>()) PartySprite(rgba, m, 7, 6, 5, x, y, 3, -1, 4);
            ushort[] row = DsBgScreen.Row(colours, 0);
            FieldFont font = Font(4) ?? SystemFont;
            for (int i = 0; labels != null && i < labels.Count && i < 3; i++)
                Print(rgba, font, Text(196, 24 + i), labels[i].X * 8 + 32, labels[i].Y * 8, row[15], row[14], Align.Centre);
            return rgba;
        });

        // pc_boxes draws the bottom screen's three layers from tiles 127 and palette 128, or 132 and 133 for the compare screen.
        internal static byte[] PcBottomPt(int page)
        {
            (DsBgScreen s, Func<int, byte[]> m) = Bg(DirNames.BoxGraphics);
            if (s == null) return null;
            bool compare = page == 1;
            Palettes(s, NitroBgCodec.ReadColours(m(compare ? 133 : 128)), 0, compare ? 4 : 3);
            s.LoadTiles(0, m(compare ? 132 : 127));
            // The box list stacks maps 124 to 126 on BG3 to BG1. Compare mode shows one layer at a time, battle stats (129 on BG1) when it opens.
            (int Bg, int Map)[] layers = compare ? new[] { (1, 129) } : new[] { (3, 124), (2, 125), (1, 126) };
            foreach ((int bg, int map) in layers)
            {
                s.InitLayer(bg, bg, 0);
                s.LoadMap(bg, NitroBgCodec.ReadMap(m(map)).Entries, DsBgScreen.MapSide);
                ExtendedColours(s, m, bg, BottomPalettesPatch.Pc, compare ? 1 : 0);
            }
            return s.Render();
        }
    }
}
