using System;
using System.Collections.Generic;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// The four screens that show a move's text, built from the ROM's own art: the summary's moves page, the
    /// battle party menu's move page, the Move Relearner, and the bag's TM pocket. Layers, members and text
    /// windows follow pokeplatinum (pokemon_summary_screen, move_reminder.c, battle_sub_menus, bag) and the
    /// matching pokediamond and pokeheartgold code. Text is printed as stored, 16 pixels a line, and cut off at
    /// the window's edges as the game's window does.
    /// </summary>
    public sealed class MoveTextScreens
    {
        public enum Screen { Summary, Battle, Relearner, Bag }

        private sealed class Built
        {
            public byte[] Rgba;
            public int TextX, TextY, WinX, WinY, WinW, WinH;
            public ushort Ink, Shadow;
        }

        private readonly Dictionary<Screen, Built> _built = new();

        /// <summary>Width of the bag's TM text: HeartGold starts it 20 pixels in, DP and Pt 40.</summary>
        public static int BagTextWidth => RomInfo.gameFamily == RomInfo.GameFamilies.HGSS ? 236 : 216;

        public byte[] Render(Screen screen, string text, FieldFont font)
        {
            if (!_built.TryGetValue(screen, out Built b))
            {
                try { b = Build(screen); }
                catch (Exception ex) { AppLogger.Warn($"Move text preview ({screen}): {ex.Message}"); b = null; }
                _built[screen] = b;
            }
            if (b?.Rgba == null) return null;

            byte[] rgba = (byte[])b.Rgba.Clone();
            byte[] ink = (byte[])b.Rgba.Clone();
            string[] lines = (text ?? "").Split('\n');
            for (int i = 0; i < lines.Length; i++)
                DsBgScreen.DrawText(ink, font, lines[i], b.TextX, b.TextY + i * 16, b.Ink, b.Shadow);
            for (int y = b.WinY; y < Math.Min(DsBgScreen.Height, b.WinY + b.WinH); y++)
                Array.Copy(ink, (y * DsBgScreen.Width + b.WinX) * 4, rgba, (y * DsBgScreen.Width + b.WinX) * 4,
                           Math.Min(b.WinW, DsBgScreen.Width - b.WinX) * 4);
            return rgba;
        }

        // A preview must not unpack anything: Save ROM repacks every unpacked folder. An archive already
        // unpacked is read there, so edits show; any other is read straight from its packed file.
        private static Func<int, byte[]> Members(RomInfo.DirNames dir)
        {
            if (!RomInfo.gameDirs.TryGetValue(dir, out (string packedDir, string unpackedDir) paths)) return null;
            ArchiveFiles files = System.IO.Directory.Exists(paths.unpackedDir)
                ? ArchiveFiles.Mapped(dir)
                : ArchiveFiles.Loose(paths.packedDir, dir.ToString());
            if (!files.Available) return null;
            byte[][] cache = new byte[files.Count][];
            return i => i < 0 || i >= cache.Length ? null : cache[i] ??= NitroBgCodec.Inflate(files.Get(i));
        }

        private static Built Build(Screen screen)
        {
            bool hg = RomInfo.gameFamily == RomInfo.GameFamilies.HGSS;
            DsBgScreen s = new DsBgScreen();
            ushort[] text;
            Built b = new Built();

            void Layer(Func<int, byte[]> m, int bg, int priority, int charBase, int ncgr, int nscr)
            {
                s.InitLayer(bg, priority, charBase);
                if (ncgr >= 0) s.LoadTiles(charBase, m(ncgr));
                // Wide arrangements are stored as 32 by 32 blocks; at no scroll the first block is the one shown.
                s.LoadMap(bg, DsBgScreen.ReadMap(m(nscr)).Entries, DsBgScreen.MapSide);
            }
            void Palettes(ushort[] colours) { for (int slot = 0; slot < 16; slot++) s.SetPalette(slot, colours, slot * 16); }
            void Window(int x, int y, int w, int h, int textX = 0)
            {
                b.WinX = x * 8; b.WinY = y * 8; b.WinW = w * 8; b.WinH = h * 8;
                b.TextX = b.WinX + textX; b.TextY = b.WinY;
            }

            switch (screen)
            {
                case Screen.Summary:
                {
                        Func<int, byte[]> m = Members(RomInfo.DirNames.summaryGraphics);
                    if (m == null) return null;
                        ushort[] colours = DsBgScreen.ReadColours(m(hg ? 0 : 1));
                    Palettes(colours);
                    if (hg)
                    {
                        // Bottom screen. The move panel is scrolled 128 pixels across, so the right half of the
                        // first block and the left half of the second are what show.
                        s.InitLayer(1, 2, 0x8000);
                        s.LoadTiles(0x8000, m(20));
                            ushort[] panel = DsBgScreen.ReadMap(m(21)).Entries;
                        s.Copy(1, 0, 0, 16, 32, panel, 16, 0, 32);
                        if (panel.Length >= 2048) s.Copy(1, 16, 0, 16, 32, panel[1024..2048], 0, 0, 32);
                        Layer(m, 2, 3, 0, 1, 18);
                        text = DsBgScreen.Row(colours, 13);
                        Window(17, 10, 15, 10);
                    }
                    else
                    {
                        s.LoadTiles(0, m(0));
                        Layer(m, 2, 1, 0, -1, 11);
                        Layer(m, 3, 3, 0, -1, 12);
                        text = DsBgScreen.Row(colours, 15);
                        Window(1, 14, 15, 10);
                    }
                    b.Ink = text[1]; b.Shadow = text[2];
                    break;
                }
                case Screen.Relearner:
                {
                        Func<int, byte[]> m = Members(RomInfo.DirNames.moveRelearnerGraphics);
                    if (m == null) return null;
                    // The art's character base is past the 64 KB this screen keeps; nothing else uses the start.
                    if (hg)
                    {
                        Palettes(DsBgScreen.ReadColours(m(0)));
                        Layer(m, 3, 3, 0, 1, 2);
                        text = DsBgScreen.Row(DsBgScreen.ReadColours(Members(RomInfo.DirNames.fonts)?.Invoke(7)), 0);
                        Window(17, 10, 15, 10);
                    }
                    else
                    {
                            ushort[] colours = DsBgScreen.ReadColours(m(12));
                        Palettes(colours);
                        Layer(m, 2, 2, 0, 10, 11);
                        text = DsBgScreen.Row(colours, 15);
                        Window(1, 8, 15, 10);
                    }
                    b.Ink = text[1]; b.Shadow = text[2];
                    break;
                }
                case Screen.Battle:
                {
                        Func<int, byte[]> m = Members(RomInfo.DirNames.battlePartyGraphics);
                    if (m == null) return null;
                        ushort[] colours = DsBgScreen.ReadColours(m(23));
                    Palettes(colours);
                    s.LoadTiles(0x8000, m(22));
                    Layer(m, 3, 3, 0x8000, -1, 6);
                    Layer(m, 2, 2, 0x8000, -1, 7);
                    text = DsBgScreen.Row(DsBgScreen.ReadColours(Members(RomInfo.DirNames.fonts)?.Invoke(hg ? 7 : 6)), 0);
                    Window(16, 8, 15, 10);
                    b.Ink = text[1]; b.Shadow = text[2];
                    break;
                }
                case Screen.Bag:
                {
                        Func<int, byte[]> m = Members(RomInfo.DirNames.bagGraphics);
                    if (m == null) return null;
                    ushort[] colours;
                    if (hg)
                    {
                        colours = DsBgScreen.ReadColours(m(8));
                        Palettes(colours);
                        s.SetPalette(13, DsBgScreen.Row(DsBgScreen.ReadColours(m(17)), 0));
                        s.LoadTiles(0x8000, m(7));
                        Layer(m, 2, 1, 0x8000, -1, 54);
                        Layer(m, 3, 2, 0x8000, -1, 94);
                        text = DsBgScreen.Row(colours, 4);
                        Window(0, 18, 32, 6, 20);
                    }
                    else
                    {
                        bool dp = RomInfo.gameFamily == RomInfo.GameFamilies.DP;
                        colours = DsBgScreen.ReadColours(m(dp ? 8 : 12));
                        Palettes(colours);
                        s.SetPalette(13, DsBgScreen.Row(DsBgScreen.ReadColours(m(dp ? 18 : 22)), 0));
                        s.LoadTiles(0, m(dp ? 7 : 11));
                        Layer(m, 1, 1, 0, -1, dp ? 10 : 14);
                        Layer(m, 3, 3, 0, -1, dp ? 9 : 13);
                        text = DsBgScreen.Row(colours, 3);
                        Window(0, 18, 32, 6, 40);
                    }
                    b.Ink = text[15]; b.Shadow = text[14];
                    break;
                }
                default: return null;
            }

            b.Rgba = s.Render();
            return b;
        }
    }
}
