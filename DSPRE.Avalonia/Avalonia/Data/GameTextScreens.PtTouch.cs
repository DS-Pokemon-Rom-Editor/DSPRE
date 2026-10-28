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
            Layer(s, m, 3, 3, 0x10000, 32, 33);
            Layer(s, m, 1, 1, 0x8000, 15, 17);
            s.InitLayer(0, 0, 0);
            s.LoadTiles(0, m(19));
            for (int i = 0; spots != null && i < spots.Count; i++) SheetButton(s, 0, i, spots[i].X, spots[i].Y, 2);
            ushort[] dial = new ushort[36];
            for (int i = 0; i < 36; i++) dial[i] = (ushort)((0x276 + i / 6 * 30 + i % 6) | 2 << 12);
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

        // trainer_case/main.c draws the open case (29, 38) in colours 10. In sprites.c each badge is a sequence in 46 (cells 44, tiles 33)
        // with its palette in file 16 plus the badge number.
        internal static byte[] TrainerCaseBottomPt(IReadOnlyList<(int X, int Y)> badges)
        {
            (DsBgScreen s, Func<int, byte[]> m) = Bg(DirNames.TrainerCardGraphics);
            if (s == null) return null;
            Palettes(s, NitroBgCodec.ReadColours(m(10)));
            Layer(s, m, 2, 2, 0, 29, 38);
            byte[] rgba = s.Render();
            ushort[] obj = NitroBgCodec.ReadColours(m(14));
            for (int i = 0; badges != null && i < badges.Count && i < 8; i++)
            {
                ushort[] own = DsBgScreen.Row(NitroBgCodec.ReadColours(m(16 + i)), 0);
                Sprite(rgba, m, 46, 44, 33, badges[i].X, badges[i].Y, i, row => row == i ? own : DsBgScreen.Row(obj, row));
            }
            return rgba;
        }

        /// <summary>The HGSS options bottom screen, drawn like its text preview.</summary>
        /// <param name="look">The options' screen, choice label x per row and Quit and Confirm spots as edited, or null for the game's.</param>
        internal static byte[] OptionsBottomHg((byte[] Screen, int[][] Labels, (int, int) Quit, (int, int) Confirm)? look) =>
            look is { } l ? WithArchive(() => OptionsHg(3, l.Screen, l.Labels, l.Quit, l.Confirm)?.Rgba) : WithArchive(() => OptionsHg(3)?.Rgba);

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
            Palettes(s, NitroBgCodec.ReadColours(m(compare ? 133 : 128)), 0, 3);
            int tiles = compare ? 132 : 127;
            s.LoadTiles(0, m(tiles));
            for (int i = 0; i < 3; i++)
            {
                s.InitLayer(3 - i, 3 - i, 0);
                s.LoadMap(3 - i, NitroBgCodec.ReadMap(m((compare ? 129 : 124) + i)).Entries, DsBgScreen.MapSide);
            }
            return s.Render();
        }
    }
}
