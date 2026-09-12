using System;
using System.Collections.Generic;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// HeartGold and SoulSilver's field bottom screen: the touch menu panel out of a/0/1/4, and the Poké Ball
    /// screen out of a/2/3/7 that scripts put up for touch yes/no questions and touch lists. Layouts are
    /// overlay 27's tables in the pokeheartgold decomp.
    /// </summary>
    public sealed class HgssTouchScreen
    {
        /// <summary>What a touch on the panel landed on.</summary>
        public enum MenuSpot { None, Strip, Icon, Item, Shoes, AButton }

        // a/0/1/4
        private const int MenuPalettes = 7, MenuTiles = 8, MenuMap = 9, IconCells = 16, IconPalettes = 14,
                          ButtonCells = 68, ButtonCharacters = 70;
        private static readonly int[] IconCharacters = { 18, 21, 24, 30, 33, 36, 39 };
        private static readonly (int X, int Y)[] IconSlots =
            { (24, 22), (24, 62), (24, 102), (24, 142), (104, 22), (104, 62), (104, 102) };
        // Label windows of 9 by 2 tiles under each icon, and the text bank entry each prints.
        private static readonly (int X, int Y)[] IconLabels =
            { (8, 48), (8, 88), (8, 128), (8, 168), (88, 48), (88, 88), (88, 128) };

        /// <summary>The panel's words, text bank 196, in slot order: POKéDEX, POKéMON, BAG, POKéGEAR, the player, SAVE, OPTIONS.</summary>
        public static readonly int[] IconLabelMessages = { 0, 1, 2, 14, 3, 4, 5 };
        public const int MenuMessage = 12, TalkMessage = 17, CheckMessage = 18, NextMessage = 23;

        /// <summary>The font archive entry the touch screens write in.</summary>
        public const int FontEntry = 4;

        // a/2/3/7
        private const int ChoicePalettes = 0, ChoiceTiles = 1, PokeBallMap = 9, YesNoMap = 10,
                          CursorPalette = 11, CursorCharacters = 12, CursorCells = 13;
        private const int BigFrameCell = 1, SmallFrameCell = 0;

        private readonly Func<int, byte[]> _menu, _choices;

        private HgssTouchScreen(Func<int, byte[]> menu, Func<int, byte[]> choices) { _menu = menu; _choices = choices; }

        /// <summary>
        /// Reads both archives out of the loaded ROM, or null when this is not HeartGold or SoulSilver.
        /// Every edit in DSPRE lands in the unpacked files and the ROM save packs them, so reading them here
        /// is what makes a changed graphic show up straight away instead of after a save and reload.
        /// </summary>
        public static HgssTouchScreen Load()
        {
            try
            {
                if (RomInfo.gameFamily != RomInfo.GameFamilies.HGSS) return null;
                var menu = Members(RomInfo.DirNames.fieldTouchMenu);
                var choices = Members(RomInfo.DirNames.fieldTouchChoices);
                return menu == null || choices == null ? null : new HgssTouchScreen(menu, choices);
            }
            catch { return null; }
        }

        private static Func<int, byte[]> Members(RomInfo.DirNames dir)
        {
            var narc = new ScriptNarc(dir);
            if (!narc.Available) return null;
            var cache = new byte[narc.Count][];
            return i => i < 0 || i >= cache.Length ? null : cache[i] ??= NitroBgCodec.Inflate(narc.Get(i));
        }

        // ── the panel ────────────────────────────────────────────────────────────────

        /// <summary>
        /// The touch menu. While a script runs the icons and side buttons go see-through and only the A button
        /// stays solid.
        /// </summary>
        /// <param name="text">Text bank 196 by entry, with the player's name already put in.</param>
        /// <param name="highlighted">The icon being touched, drawn in its red palette, or -1.</param>
        public byte[] RenderMenu(FieldFont font, Func<int, string> text, bool dimmed, bool aHeld, bool shoesOn,
                                 int highlighted, int aLabelMessage)
        {
            var palettes = DsBgScreen.ReadColours(_menu(MenuPalettes));
            var s = new DsBgScreen();
            s.InitLayer(0, 2, 0);
            s.LoadTiles(0, _menu(MenuTiles));
            var (w, map) = DsBgScreen.ReadMap(_menu(MenuMap));
            s.LoadMap(0, map, w);
            for (int slot = 0; slot < 16; slot++) s.SetPalette(slot, palettes, slot * 16);
            byte[] rgba = s.Render();

            ushort[] words = DsBgScreen.Row(palettes, 4);
            void Label(string line, int x, int y, int width, bool centre)
            {
                if (string.IsNullOrWhiteSpace(line)) return;
                line = line.Trim();
                int at = centre ? x + (width - DsBgScreen.MeasureText(font, line)) / 2 : x;
                DsBgScreen.DrawText(rgba, font, line, at, y, words[15], words[1]);
            }

            Label(text?.Invoke(MenuMessage), 72, 0, 80, false);

            var iconCells = DsBgScreen.ReadCells(_menu(IconCells));
            var iconColours = DsBgScreen.ReadColours(_menu(IconPalettes));
            if (iconCells.Count > 0)
                for (int i = 0; i < IconSlots.Length; i++)
                {
                    var colours = DsBgScreen.Row(iconColours, i == highlighted ? 1 : 0);
                    DsBgScreen.DrawCell(rgba, iconCells[0], DsBgScreen.ReadCharacters(_menu(IconCharacters[i])),
                                        _ => colours, IconSlots[i].X, IconSlots[i].Y, dimmed);
                    Label(text?.Invoke(IconLabelMessages[i]), IconLabels[i].X, IconLabels[i].Y, 72, true);
                }

            var buttons = DsBgScreen.ReadCells(_menu(ButtonCells));
            byte[] chars = DsBgScreen.ReadCharacters(_menu(ButtonCharacters));
            Func<int, ushort[]> buttonColours = p => DsBgScreen.Row(palettes, p);
            void Button(int cell, int x, int y, bool translucent)
            {
                if (cell < buttons.Count) DsBgScreen.DrawCell(rgba, buttons[cell], chars, buttonColours, x, y, translucent);
            }
            Button(0, 200, 8, dimmed);                         // registered item one
            Button(2, 200, 48, dimmed);                        // registered item two
            Button(shoesOn ? 5 : 4, 184, 86, dimmed);          // running shoes
            Button(shoesOn ? 8 : 12, 210, 94, dimmed);         // their on or off light
            Button(aHeld ? 7 : 6, 168, 144, false);            // the A button never dims
            Button(13, 54, 0, dimmed);                         // the X button mark before MENU
            Label(text?.Invoke(aLabelMessage), 193, 160, 62, false);
            return rgba;
        }

        /// <summary>What a touch at bottom-screen pixel x, y lands on, and which icon when it is one.</summary>
        public static (MenuSpot Spot, int Index) HitMenu(int x, int y)
        {
            if (x >= 168 && x < 256 && y >= 144 && y < 189) return (MenuSpot.AButton, 0);
            if (x >= 184 && x < 253 && y >= 86 && y < 135) return (MenuSpot.Shoes, 0);
            if (x >= 203 && x < 256 && y >= 8 && y < 40) return (MenuSpot.Item, 0);
            if (x >= 203 && x < 256 && y >= 46 && y < 78) return (MenuSpot.Item, 1);
            if (x >= 8 && x < 161 && y >= 0 && y < 17) return (MenuSpot.Strip, 0);
            for (int i = 0; i < IconSlots.Length; i++)
            {
                int left = i < 4 ? 16 : 96, top = IconSlots[i].Y;
                if (x >= left && x < left + 61 && y >= top && y < top + 33) return (MenuSpot.Icon, i);
            }
            return (MenuSpot.None, 0);
        }

        // ── the Poké Ball screen ─────────────────────────────────────────────────────

        /// <summary>
        /// The Poké Ball screen, with a yes/no or a list of 2 to 8 entries on it when <paramref name="labels"/>
        /// has any, and the red frame round the entry under the cursor while it is showing.
        /// </summary>
        public byte[] RenderChoices(FieldFont font, IReadOnlyList<string> labels, bool yesNo, int cursor, bool cursorShown)
        {
            var palettes = DsBgScreen.ReadColours(_choices(ChoicePalettes));
            var s = new DsBgScreen();
            int count = labels?.Count ?? 0;
            bool bars = count >= 2 && count <= 8;

            s.InitLayer(2, 2, 0x4000);
            s.LoadTiles(0x4000, _choices(ChoiceTiles));
            var (bw, background) = DsBgScreen.ReadMap(_choices(PokeBallMap));
            s.LoadMap(2, background, bw);
            if (bars)
            {
                s.InitLayer(0, 1, 0);
                s.LoadTiles(0, _choices(ChoiceTiles));
                var (fw, front) = DsBgScreen.ReadMap(_choices(yesNo ? YesNoMap : count));
                s.LoadMap(0, front, fw);
            }
            for (int slot = 0; slot < 5; slot++) s.SetPalette(slot, palettes, slot * 16);
            byte[] rgba = s.Render();
            if (!bars) return rgba;

            ushort[] words = DsBgScreen.Row(palettes, 4);
            for (int i = 0; i < count; i++)
            {
                string line = labels[i]?.Trim() ?? "";
                var (wx, wy, ww, wh) = WindowFor(count, yesNo, i);
                int x = yesNo ? wx + (ww - DsBgScreen.MeasureText(font, line)) / 2 : wx;
                int y = wy + (wh - 16) / 2;
                DsBgScreen.DrawText(rgba, font, line, x, y, words[2], words[1]);
            }

            if (cursorShown && cursor >= 0 && cursor < count)
            {
                var cells = DsBgScreen.ReadCells(_choices(CursorCells));
                bool big = yesNo || count <= 4;
                int cell = big ? BigFrameCell : SmallFrameCell;
                var (cx, cy) = CursorAt(count, yesNo, cursor);
                var colours = DsBgScreen.Row(DsBgScreen.ReadColours(_choices(CursorPalette)), 0);
                if (cell < cells.Count)
                    DsBgScreen.DrawCell(rgba, cells[cell], DsBgScreen.ReadCharacters(_choices(CursorCharacters)), _ => colours, cx, cy);
            }
            return rgba;
        }

        /// <summary>
        /// Whether the red frame is up this many frames into the blink that confirms a choice: off for four,
        /// on for four, off for four, on for two.
        /// </summary>
        public static bool BlinkShows(int frame) => frame < 0 || (frame >= 4 && frame < 8) || frame >= 12;

        /// <summary>How long the confirming blink lasts before the choice is made.</summary>
        public const int BlinkFrames = 14;

        // Text windows in pixels, from the tile tables.
        private static readonly (int X, int Y)[][] ListWindows =
        {
            null, null,
            new[] { (16, 64), (16, 112) },
            new[] { (16, 40), (16, 88), (16, 136) },
            new[] { (16, 16), (16, 64), (16, 112), (16, 160) },
            new[] { (16, 32), (144, 32), (16, 80), (144, 80), (144, 128) },
            new[] { (16, 32), (144, 32), (16, 80), (144, 80), (16, 128), (144, 128) },
            new[] { (16, 8), (144, 8), (16, 56), (144, 56), (16, 104), (144, 104), (144, 152) },
            new[] { (16, 8), (144, 8), (16, 56), (144, 56), (16, 104), (144, 104), (16, 152), (144, 152) },
        };

        private static (int X, int Y, int W, int H) WindowFor(int count, bool yesNo, int index)
        {
            if (yesNo) return (96, index == 0 ? 64 : 112, 64, 16);
            var (x, y) = ListWindows[count][index];
            return count <= 4 ? (x, y, 224, 16) : (x, y, 104, 32);
        }

        private static readonly (int X, int Y)[][] ListCursors =
        {
            null, null,
            new[] { (128, 72), (128, 120) },
            new[] { (128, 48), (128, 96), (128, 144) },
            new[] { (128, 24), (128, 72), (128, 120), (128, 168) },
            new[] { (64, 48), (192, 48), (64, 96), (192, 96), (192, 144) },
            new[] { (64, 48), (192, 48), (64, 96), (192, 96), (64, 144), (192, 144) },
            new[] { (64, 24), (192, 24), (64, 72), (192, 72), (64, 120), (192, 120), (192, 168) },
            new[] { (64, 24), (192, 24), (64, 72), (192, 72), (64, 120), (192, 120), (64, 168), (192, 168) },
        };

        /// <summary>Where the red frame centres for an entry.</summary>
        public static (int X, int Y) CursorAt(int count, bool yesNo, int index)
        {
            if (yesNo) return index == 0 ? (128, 72) : (128, 120);
            if (count < 2 || count > 8 || index < 0 || index >= count) return (128, 72);
            return ListCursors[count][index];
        }

        /// <summary>The entry a touch at bottom-screen pixel x, y lands on, or -1.</summary>
        public static int HitChoice(int count, bool yesNo, int x, int y)
        {
            if (yesNo) count = 2;
            if (count < 2 || count > 8) return -1;
            for (int i = 0; i < count; i++)
            {
                var (cx, cy) = CursorAt(count, yesNo, i);
                bool wide = yesNo || count <= 4;
                int left = wide ? 3 : cx < 128 ? 3 : 131;
                int right = wide ? 252 : cx < 128 ? 124 : 253;
                int top = cy - 22, bottom = cy + 21;
                if (x >= left && x < right && y >= top && y < bottom) return i;
            }
            return -1;
        }

        /// <summary>
        /// Where the d-pad takes the cursor on a touch list, by the layout: up and down stay in a column, left
        /// and right cross to the other one on the same row. -1 when there is nowhere to go.
        /// </summary>
        public static int Neighbour(int count, bool yesNo, int index, int dx, int dy)
        {
            if (yesNo) count = 2;
            if (count < 2 || count > 8 || index < 0 || index >= count) return -1;
            var (x, y) = CursorAt(count, yesNo, index);
            int best = -1, bestDistance = int.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (i == index) continue;
                var (ix, iy) = CursorAt(count, yesNo, i);
                bool ok = dy != 0 ? ix == x && Math.Sign(iy - y) == dy : iy == y && Math.Sign(ix - x) == dx;
                int distance = Math.Abs(ix - x) + Math.Abs(iy - y);
                if (ok && distance < bestDistance) { best = i; bestDistance = distance; }
            }
            return best;
        }
    }
}
