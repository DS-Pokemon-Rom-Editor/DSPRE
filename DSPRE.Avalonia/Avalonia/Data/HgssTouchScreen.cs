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
        private const int MenuPalettes = 7, MenuTiles = 8, MenuMap = 9, IconCells = 16, IconAnimations = 17,
                          IconPalettes = 14, ItemIconCells = 54, ButtonCells = 68, ButtonCharacters = 70;

        // Overlay 27's icon table: the drawing each kind of icon is made from and the text bank entry
        // under it. -1 is the Bug Contest Pokémon, which is its own icon rather than a drawing here.
        private static readonly (int Characters, int Message)[] IconKinds =
        {
            (18, 0), (21, 1), (24, 2), (30, 14), (33, 3), (36, 4), (39, 5), (42, 8),
            (18, 32), (18, 32), (-1, 32), (45, 34), (48, 35),
        };
        private const int NoIcon = 13;
        // Which kind of icon goes in each slot, for a normal walk and for the Bug Contest.
        private static readonly int[] NormalIcons = { 0, 1, 2, 3, 4, 5, 6 };
        private static readonly int[] BugContestIcons = { 7, 0, 1, 3, 4, 6, 10 };

        // Sprite positions from overlay 27: the seven icons, then the two registered item icons.
        private static readonly (int X, int Y)[] IconSlots =
            { (24, 22), (24, 62), (24, 102), (24, 142), (104, 22), (104, 62), (104, 102) };
        private static readonly (int X, int Y)[] ItemIconSlots = { (220, 11), (220, 51) };
        // Label windows of 9 by 2 tiles under each icon.
        private static readonly (int X, int Y)[] IconLabels =
            { (8, 48), (8, 88), (8, 128), (8, 168), (88, 48), (88, 88), (88, 128) };
        // The Bug Contest's ball and the Pokémon caught in it.
        private static readonly (int X, int Y) BallSlot = (100, 104), CaughtSlot = (104, 136);
        private const int BallSequence = 6;

        /// <summary>Text bank 196 entries the panel prints outside the icon table.</summary>
        public const int MenuMessage = 12, TalkMessage = 17, CheckMessage = 18, FishingMessage = 22, NextMessage = 23;

        /// <summary>What the A button can say, in the order overlay 27 picks between them.</summary>
        public static readonly int[] ALabelMessages = { CheckMessage, TalkMessage, FishingMessage, NextMessage };

        /// <summary>The font archive entry MENU and the A button's word are written in.</summary>
        public const int FontEntry = 4;
        /// <summary>The font archive entry the icon names are written in.</summary>
        public const int IconFontEntry = 0;

        // Items the preview shows for the registered items and the Bug Contest.
        private const int ShownItemOne = 450, ShownItemTwo = 447, SportBall = 499, ShownCatch = 123;

        /// <summary>How the panel looks, for the parts the game changes as it runs.</summary>
        public sealed class MenuLook
        {
            /// <summary>A script or a menu has control, so most of the panel goes see-through.</summary>
            public bool Busy;
            /// <summary>The icon the open menu's cursor is on, which stays solid while busy, or -1.</summary>
            public int Cursor = -1;
            /// <summary>The icon being touched, drawn in its red palette, or -1.</summary>
            public int Highlighted = -1;
            public bool AHeld;
            /// <summary>The player has running shoes and is not on the bicycle.</summary>
            public bool Shoes = true;
            public bool ShoesOn;
            /// <summary>Both item slots have an item registered.</summary>
            public bool RegisteredItems;
            public bool BugContest;
            public int ALabel = CheckMessage;
        }

        // a/2/3/7
        private const int ChoicePalettes = 0, ChoiceTiles = 1, PokeBallMap = 9, YesNoMap = 10,
                          CursorPalette = 11, CursorCharacters = 12, CursorCells = 13;
        private const int BigFrameCell = 1, SmallFrameCell = 0;

        private readonly Func<int, byte[]> _menu, _choices;
        private Func<int, byte[]> _itemIcons;

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

        /// <summary>The touch menu in a plain walk-about state, for callers that only track the basics.</summary>
        public byte[] RenderMenu(FieldFont font, Func<int, string> text, bool dimmed, bool aHeld, bool shoesOn,
                                 int highlighted, int aLabelMessage)
            => RenderMenu(font, font, text, new MenuLook
            {
                Busy = dimmed, AHeld = aHeld, ShoesOn = shoesOn, Highlighted = highlighted, ALabel = aLabelMessage,
                RegisteredItems = true,
            });

        /// <summary>
        /// The touch menu, put together in the order the hardware shows it: the panel, the side buttons,
        /// the words over them, then the icons over everything. While busy the icons, item slots and running
        /// shoes go see-through; the panel, the A button, the X mark and the Bug Contest's ball and catch
        /// never do.
        /// </summary>
        /// <param name="font">Font 4, for MENU and the A button's word.</param>
        /// <param name="iconFont">Font 0, for the names under the icons.</param>
        /// <param name="text">Text bank 196 by entry, with the player's name already put in.</param>
        public byte[] RenderMenu(FieldFont font, FieldFont iconFont, Func<int, string> text, MenuLook look)
        {
            look ??= new MenuLook();
            var palettes = DsBgScreen.ReadColours(_menu(MenuPalettes));
            var s = new DsBgScreen();
            s.InitLayer(0, 2, 0);
            s.LoadTiles(0, _menu(MenuTiles));
            var (w, map) = DsBgScreen.ReadMap(_menu(MenuMap));
            s.LoadMap(0, map, w);
            for (int slot = 0; slot < 16; slot++) s.SetPalette(slot, palettes, slot * 16);
            byte[] rgba = s.Render();

            // Dimmed sprites blend with the backgrounds under them: the panel and the words, not each other.
            byte[] under = (byte[])rgba.Clone();
            ushort[] words = DsBgScreen.Row(palettes, 4);
            int[] kinds = look.BugContest ? BugContestIcons : NormalIcons;

            void Words(byte[] into)
            {
                void Line(FieldFont f, string line, int x, int y, int width, bool centre, ushort letter, ushort shadow)
                {
                    if (string.IsNullOrWhiteSpace(line)) return;
                    line = line.Trim();
                    int at = centre ? x + (width - DsBgScreen.MeasureText(f, line)) / 2 : x;
                    DsBgScreen.DrawText(into, f, line, at, y, letter, shadow);
                }
                Line(font, text?.Invoke(MenuMessage), 72, 0, 80, false, words[15], words[1]);
                for (int i = 0; i < IconLabels.Length; i++)
                {
                    int kind = kinds[i];
                    if (kind == NoIcon) continue;
                    Line(iconFont ?? font, text?.Invoke(IconKinds[kind].Message), IconLabels[i].X, IconLabels[i].Y,
                         72, true, words[14], words[2]);
                }
                Line(font, text?.Invoke(look.ALabel), 192, 160, 64, false, words[15], words[1]);
            }
            Words(under);

            // BG1, where the words are, sits over the side buttons and under the icons.
            var buttons = DsBgScreen.ReadCells(_menu(ButtonCells));
            byte[] chars = DsBgScreen.ReadCharacters(_menu(ButtonCharacters));
            Func<int, ushort[]> buttonColours = p => DsBgScreen.Row(palettes, p);
            void Button(int cell, int x, int y, bool translucent)
            {
                if (cell < buttons.Count)
                    DsBgScreen.DrawCell(rgba, buttons[cell], chars, buttonColours, x, y, translucent, under);
            }
            if (look.RegisteredItems)
            {
                Button(0, 200, 8, look.Busy);
                Button(2, 200, 48, look.Busy);
            }
            if (look.Shoes)
            {
                Button(look.ShoesOn ? 5 : 4, 184, 86, look.Busy);
                Button(look.ShoesOn ? 8 : 12, 210, 94, look.Busy);
            }
            Button(look.AHeld ? 7 : 6, 168, 144, false);
            Button(13, 54, 0, false);

            Words(rgba);

            var iconCells = DsBgScreen.ReadCells(_menu(IconCells));
            var iconColours = DsBgScreen.ReadColours(_menu(IconPalettes));
            if (iconCells.Count > 0)
                for (int i = 0; i < IconSlots.Length; i++)
                {
                    int drawing = IconKinds[kinds[i]].Characters;
                    if (kinds[i] == NoIcon || drawing < 0) continue;
                    var colours = DsBgScreen.Row(iconColours, i == look.Highlighted ? 1 : 0);
                    DsBgScreen.DrawCell(rgba, iconCells[0], DsBgScreen.ReadCharacters(_menu(drawing)),
                                        _ => colours, IconSlots[i].X, IconSlots[i].Y,
                                        look.Busy && i != look.Cursor, under);
                }

            if (look.RegisteredItems)
            {
                var itemCells = DsBgScreen.ReadCells(_menu(ItemIconCells));
                int[] items = { ShownItemOne, ShownItemTwo };
                for (int i = 0; i < items.Length; i++)
                    if (itemCells.Count > 0 && ItemIcon(items[i]) is var (c, p) && c != null)
                        DsBgScreen.DrawCell(rgba, itemCells[0], c, _ => p, ItemIconSlots[i].X, ItemIconSlots[i].Y,
                                            look.Busy && 7 + i != look.Cursor, under);
            }

            if (look.BugContest)
            {
                int cell = CellOf(_menu(IconAnimations), BallSequence);
                if (cell >= 0 && cell < iconCells.Count && ItemIcon(SportBall) is var (c, p) && c != null)
                    DsBgScreen.DrawCell(rgba, iconCells[cell], c, _ => p, BallSlot.X, BallSlot.Y);
                DsBgScreen.DrawImage(rgba, CaughtIcon(), CaughtSlot.X, CaughtSlot.Y);
            }
            return rgba;
        }

        /// <summary>Which slot a kind of icon sits in, 0 for POKéDEX up to 7 for RETIRE, or -1 when not shown.</summary>
        public static int SlotOf(int kind, bool bugContest) =>
            Array.IndexOf(bugContest ? BugContestIcons : NormalIcons, kind);

        /// <summary>The first cell a sequence of an animation file shows, or -1.</summary>
        private static int CellOf(byte[] nanr, int sequence)
        {
            try { return nanr == null ? -1 : NanrFile.Read(nanr).CellOf(sequence, 0); }
            catch { return -1; }
        }

        // An item's icon as the game loads it for the panel: its drawing and first row of colours out of the
        // item icon archive, by the item table.
        private (byte[] Characters, ushort[] Colours) ItemIcon(int item)
        {
            try
            {
                if (!DSPRE.ROMFiles.ItemTable.Exists(item)) return (null, null);
                var row = DSPRE.ROMFiles.ItemTable.Read(item);
                _itemIcons ??= Members(RomInfo.DirNames.itemIcons);
                if (_itemIcons == null) return (null, null);
                byte[] chars = DsBgScreen.ReadCharacters(_itemIcons((int)row.itemIcon));
                ushort[] colours = DsBgScreen.Row(DsBgScreen.ReadColours(_itemIcons((int)row.itemPalette)), 0);
                return chars.Length == 0 ? (null, null) : (chars, colours);
            }
            catch { return (null, null); }
        }

        private DSPRE.RawImage _caught;
        private DSPRE.RawImage CaughtIcon()
        {
            if (_caught != null) return _caught;
            try
            {
                DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.monIcons });
                _caught = DSUtils.GetPokePicRaw(ShownCatch, 32, 32);
            }
            catch { _caught = null; }
            return _caught;
        }

        // Overlay 27's touch rectangles, top, bottom, left and right, each taking in its top and left edge
        // but not its bottom and right one. The strip, the icons and the item slots are one table, the
        // shoes and the A button another.
        private static readonly (int Top, int Bottom, int Left, int Right) Strip = (0, 16, 8, 160);
        private static readonly (int Top, int Bottom, int Left, int Right)[] IconRects =
        {
            (22, 54, 16, 76), (62, 94, 16, 76), (102, 134, 16, 76), (142, 174, 16, 76),
            (22, 54, 96, 156), (62, 94, 96, 156), (102, 134, 96, 156),
        };
        private static readonly (int Top, int Bottom, int Left, int Right)[] ItemRects =
            { (8, 39, 203, 255), (46, 77, 203, 255) };
        private static readonly (int Top, int Bottom, int Left, int Right)
            ShoesRect = (86, 134, 184, 252), ARect = (144, 188, 168, 255);

        private static bool In((int Top, int Bottom, int Left, int Right) r, int x, int y) =>
            x >= r.Left && x < r.Right && y >= r.Top && y < r.Bottom;

        /// <summary>What a touch at bottom-screen pixel x, y lands on, and which icon when it is one.</summary>
        public static (MenuSpot Spot, int Index) HitMenu(int x, int y)
        {
            if (In(ARect, x, y)) return (MenuSpot.AButton, 0);
            if (In(ShoesRect, x, y)) return (MenuSpot.Shoes, 0);
            for (int i = 0; i < ItemRects.Length; i++)
                if (In(ItemRects[i], x, y)) return (MenuSpot.Item, i);
            if (In(Strip, x, y)) return (MenuSpot.Strip, 0);
            for (int i = 0; i < IconRects.Length; i++)
                if (In(IconRects[i], x, y)) return (MenuSpot.Icon, i);
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

        // Overlay 27's touch rectangles for a list of 2 to 8 entries, top, bottom, left and right, half
        // open like the menu's. The yes/no uses the two-entry list's.
        private static readonly (int Top, int Bottom, int Left, int Right)[][] ChoiceRects =
        {
            null, null,
            new[] { (50, 92, 3, 251), (99, 140, 3, 251) },
            new[] { (27, 68, 3, 251), (74, 115, 3, 251), (123, 164, 3, 251) },
            new[] { (2, 43, 3, 251), (52, 92, 3, 251), (99, 140, 3, 251), (148, 188, 3, 251) },
            new[] { (25, 68, 3, 123), (25, 68, 131, 252), (75, 115, 3, 123), (75, 115, 131, 252),
                    (123, 163, 131, 252) },
            new[] { (25, 68, 3, 123), (25, 68, 131, 252), (75, 115, 3, 123), (75, 115, 131, 252),
                    (123, 163, 3, 123), (123, 163, 131, 252) },
            new[] { (3, 44, 3, 123), (3, 44, 131, 252), (51, 91, 3, 123), (51, 91, 131, 252),
                    (100, 139, 3, 123), (100, 139, 131, 252), (147, 188, 131, 252) },
            new[] { (3, 44, 3, 123), (3, 44, 131, 252), (51, 91, 3, 123), (51, 91, 131, 252),
                    (100, 139, 3, 123), (100, 139, 131, 252), (147, 188, 3, 123), (147, 188, 131, 252) },
        };

        /// <summary>The entry a touch at bottom-screen pixel x, y lands on, or -1.</summary>
        public static int HitChoice(int count, bool yesNo, int x, int y)
        {
            if (yesNo) count = 2;
            if (count < 2 || count > 8) return -1;
            var rects = ChoiceRects[count];
            for (int i = 0; i < rects.Length; i++)
                if (In(rects[i], x, y)) return i;
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
