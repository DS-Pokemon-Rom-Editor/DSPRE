using System;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// Platinum's field bottom screen out of graphic/poketch.narc: the Poké Ball picture before the player has
    /// a Pokétch, and the Pokétch frame over the Digital Watch once they do. Recipe from the pokeplatinum
    /// decomp's applications/poketch (poketch_graphics.c, digital_watch/graphics.c, unavailable/graphics.c).
    /// </summary>
    public sealed class PoketchScreen
    {
        /// <summary>How a side button looks: at rest, touched while a script runs, or held down.</summary>
        public enum Look { Free, Lock, Hold }

        /// <summary>What a touch landed on.</summary>
        public enum Spot { None, Screen, Up, Down }

        /// <summary>SEQ_SE_DP_DENSI01, a side button held down.</summary>
        public const int HoldSound = 1647;
        /// <summary>SEQ_SE_DP_DENSI04, a side button touched while a script runs.</summary>
        public const int LockSound = 1649;
        /// <summary>SEQ_SE_DP_BEEP, the watch face touched while a script runs.</summary>
        public const int BeepSound = 1646;

        private const int ThemePalettes = 0, UnavailableTiles = 10, UnavailableMap = 11, UnavailablePalette = 12,
                          BorderPalettes = 13, BorderTiles = 14, BorderMap = 15,
                          WatchTiles = 23, WatchMap = 24, WatchDigits = 25;

        // Board tiles are loaded 64 tiles in, and the board arrangement already counts from there.
        private const int BoardTileOffset = 64, BoardCharBase = 0x4000;
        private const int SolidTile = 164;

        private readonly Func<int, byte[]> _member;

        private PoketchScreen(Func<int, byte[]> member) { _member = member; }

        /// <summary>
        /// Reads the Pokétch archive out of the loaded ROM, or null when this ROM has none. Every edit in
        /// DSPRE lands in the unpacked files and the ROM save packs them, so reading them here is what makes
        /// a changed graphic show up straight away instead of after a save and reload.
        /// </summary>
        public static PoketchScreen Load()
        {
            try
            {
                if (RomInfo.gameFamily != RomInfo.GameFamilies.Plat) return null;
                var narc = new ScriptNarc(RomInfo.DirNames.poketch);
                if (!narc.Available) return null;
                var cache = new byte[narc.Count][];
                return new PoketchScreen(i =>
                {
                    if (i < 0 || i >= cache.Length) return null;
                    return cache[i] ??= NitroBgCodec.Inflate(narc.Get(i));
                });
            }
            catch { return null; }
        }

        /// <summary>The Poké Ball picture shown until the player is given a Pokétch.</summary>
        public byte[] RenderUnavailable()
        {
            var s = new DsBgScreen();
            s.InitLayer(0, 0, 0);
            s.LoadTiles(0, _member(UnavailableTiles));
            var (w, map) = DsBgScreen.ReadMap(_member(UnavailableMap));
            s.LoadMap(0, map, w);
            s.SetPalette(0, DsBgScreen.ReadColours(_member(UnavailablePalette)));
            return s.Render();
        }

        /// <summary>The Pokétch showing the Digital Watch.</summary>
        /// <param name="female">A girl's Pokétch has the pink casing, a boy's the blue.</param>
        /// <param name="theme">The Color Changer's colour, 0 for a new Pokétch.</param>
        /// <param name="backlight">Lit while the watch face is held.</param>
        public byte[] RenderWatch(bool female, int theme, bool backlight, int hour, int minute, Look up, Look down)
        {
            var s = new DsBgScreen();

            // BG0 the casing, BG1 solid round a see-through window over the watch face, BG2 the watch.
            s.InitLayer(0, 0, BoardCharBase);
            s.InitLayer(1, 1, BoardCharBase);
            s.InitLayer(2, 2, 0);
            s.LoadTiles(BoardCharBase, _member(BorderTiles), BoardTileOffset);
            var (bw, board) = DsBgScreen.ReadMap(_member(BorderMap));
            s.LoadMap(0, board, bw);
            s.SetPalette(15, DsBgScreen.ReadColours(_member(BorderPalettes)), (female ? 0 : 1) * 16);
            s.Fill(1, BoardTileOffset + SolidTile, 0, 0, DsBgScreen.MapSide, 24, 15);
            s.Fill(1, BoardTileOffset, 2, 2, 24, 20, 15);

            s.LoadTiles(0, _member(WatchTiles));
            var (ww, watch) = DsBgScreen.ReadMap(_member(WatchMap));
            s.LoadMap(2, watch, ww);
            var themes = DsBgScreen.ReadColours(_member(ThemePalettes));
            int palettes = Math.Max(1, themes.Length / 32);
            s.SetPalette(0, themes, Math.Clamp(theme, 0, palettes - 1) * 32 + (backlight ? 16 : 0));

            var digits = DigitStrip(DsBgScreen.ReadMap(_member(WatchDigits)).Entries);
            hour = Math.Clamp(hour, 0, 23);
            minute = Math.Clamp(minute, 0, 59);
            foreach (var (x, d) in new[] { (3, hour / 10), (8, hour % 10), (15, minute / 10), (20, minute % 10) })
                s.Copy(2, x, 7, 4, 9, digits, 4 * d, 0, DigitStripWidth);

            s.Put(0, ButtonBlock(up == Look.Hold ? 16 : up == Look.Lock ? 12 : 8, 4, 6), 28, 4, 4, 8);
            s.Put(0, ButtonBlock(down == Look.Hold ? 28 : down == Look.Lock ? 24 : 20, 2, 4), 28, 12, 4, 8);
            return s.Render();
        }

        /// <summary>
        /// The Pokétch showing one of its applications. The casing is the same either way; what changes is
        /// the screen inside the window, which every application draws with the theme colours.
        /// </summary>
        /// <param name="tiles">The application's drawing, or -1 for an application that has none.</param>
        /// <param name="arrangement">Where those tiles go, or -1.</param>
        /// <summary>How many cell banks a sprite layout holds, so one of them can be picked.</summary>
        public int CellBankCount(int cells)
            => cells < 0 ? 0 : DsBgScreen.ReadCells(_member(cells)).Count;

        // The window the casing leaves for an application, in pixels: tile (2,2), 24 by 20 tiles.
        private const int WindowX = 16, WindowY = 16, WindowW = 192, WindowH = 160;

        /// <summary>
        /// The middle of the window an application draws in. Where a sprite really goes is in the game's
        /// code, so this is only for showing a frame that has no recorded position, never for claiming one.
        /// </summary>
        public static (int X, int Y) MiddleOfScreen => (WindowX + WindowW / 2, WindowY + WindowH / 2);

        /// <summary>
        /// What one frame of an animation does to the sprite. Five of this archive's animations carry a turn,
        /// a stretch or a shift per frame, so a frame drawn without them is the right picture in the wrong
        /// place. The resting values leave the sprite exactly where the cells put it.
        /// </summary>
        public readonly record struct Motion(double Degrees, double ScaleX, double ScaleY, int ShiftX, int ShiftY)
        {
            public static readonly Motion Still = new(0, 1, 1, 0, 0);
            public bool Moves => Degrees != 0 || ScaleX != 1 || ScaleY != 1 || ShiftX != 0 || ShiftY != 0;
        }

        /// <param name="slots">Where to repeat the sprite, or null to show it in the middle.</param>
        /// <param name="fills">Rectangles a running game fills in, drawn only when asked for.</param>
        /// <param name="motion">What the frame being shown does to the sprite.</param>
        public byte[] RenderApp(bool female, int theme, bool backlight, int tiles, int arrangement,
                                int sprites = -1, int cells = -1, int bank = 0,
                                (int X, int Y)[] slots = null,
                                (int X, int Y, int W, int H, int Colour)[] fills = null,
                                Motion? motion = null)
        {
            var s = new DsBgScreen();

            s.InitLayer(0, 0, BoardCharBase);
            s.InitLayer(1, 1, BoardCharBase);
            s.InitLayer(2, 2, 0);
            s.LoadTiles(BoardCharBase, _member(BorderTiles), BoardTileOffset);
            var (bw, board) = DsBgScreen.ReadMap(_member(BorderMap));
            s.LoadMap(0, board, bw);
            s.SetPalette(15, DsBgScreen.ReadColours(_member(BorderPalettes)), (female ? 0 : 1) * 16);
            s.Fill(1, BoardTileOffset + SolidTile, 0, 0, DsBgScreen.MapSide, 24, 15);
            s.Fill(1, BoardTileOffset, 2, 2, 24, 20, 15);

            if (tiles >= 0)
            {
                s.LoadTiles(0, _member(tiles));
                if (arrangement >= 0)
                {
                    var (aw, map) = DsBgScreen.ReadMap(_member(arrangement));
                    s.LoadMap(2, map, aw);
                }
                var themes = DsBgScreen.ReadColours(_member(ThemePalettes));
                int rows = Math.Max(1, themes.Length / 16);
                int row = Math.Clamp(theme * 2 + (backlight ? 1 : 0), 0, rows - 1);
                s.SetPalette(0, themes, row * 16);
            }

            byte[] rgba = s.Render();

            var palette = DsBgScreen.ReadColours(_member(ThemePalettes));
            int paletteRows = Math.Max(1, palette.Length / 16);
            ushort[] colours = DsBgScreen.Row(
                palette, Math.Clamp(theme * 2 + (backlight ? 1 : 0), 0, paletteRows - 1));

            // The parts a running game fills in: a health bar, a blank note page. Flat rectangles in the
            // theme's own colours, drawn under the sprites the same way the game draws them.
            if (fills != null)
                foreach (var (fx, fy, fw, fh, index) in fills)
                {
                    uint argb = Argb(colours, index);
                    for (int y = fy; y < fy + fh; y++)
                        for (int x = fx; x < fx + fw; x++)
                        {
                            if (x < 0 || y < 0 || x >= DsBgScreen.Width || y >= DsBgScreen.Height) continue;
                            int at = (y * DsBgScreen.Width + x) * 4;
                            rgba[at] = (byte)(argb >> 16);
                            rgba[at + 1] = (byte)(argb >> 8);
                            rgba[at + 2] = (byte)argb;
                            rgba[at + 3] = 0xFF;
                        }
                }

            // The moving pieces sit on top. A sprite's own layout says where its parts go within the sprite;
            // where the sprite goes is in the game's code, so it is drawn at the places that are known and
            // in the middle of the window for the ones that are not.
            if (sprites >= 0 && cells >= 0)
            {
                var banks = DsBgScreen.ReadCells(_member(cells));
                if (banks.Count > 0)
                {
                    var chars = DsBgScreen.ReadCharacters(_member(sprites));
                    var cell = banks[Math.Clamp(bank, 0, banks.Count - 1)];
                    // Only where the game's own table says. Drawing a sprite in the middle of the window
                    // because its real place is unknown looks right and is wrong, which is worse than
                    // leaving it out.
                    var m = motion ?? Motion.Still;
                    if (slots != null)
                        foreach (var (sx, sy) in slots)
                        {
                            if (m.Moves)
                                DsBgScreen.DrawCellTurned(rgba, cell, chars, _ => colours,
                                                          sx + m.ShiftX, sy + m.ShiftY,
                                                          m.Degrees, m.ScaleX, m.ScaleY);
                            else
                                DsBgScreen.DrawCell(rgba, cell, chars, _ => colours, sx, sy);
                        }
                }
            }
            return rgba;
        }

        // A 15-bit colour opened out to the eight bits a screen wants.
        private static uint Argb(ushort[] colours, int index)
        {
            ushort v = colours != null && index >= 0 && index < colours.Length ? colours[index] : (ushort)0;
            int r = v & 0x1F, g = (v >> 5) & 0x1F, b = (v >> 10) & 0x1F;
            return (uint)(((r << 3 | r >> 2) << 16) | ((g << 3 | g >> 2) << 8) | (b << 3 | b >> 2));
        }

        /// <summary>Which part of the Pokétch a touch at bottom-screen pixel x, y lands on.</summary>
        public static Spot HitTest(int x, int y)
        {
            if (x >= 16 && x < 207 && y >= 16 && y < 175) return Spot.Screen;
            if (x >= 224 && x < 255 && y >= 32 && y < 96) return Spot.Up;
            if (x >= 224 && x < 255 && y >= 96 && y < 160) return Spot.Down;
            return Spot.None;
        }

        private const int DigitStripWidth = 40;

        // The file keeps digits 0 to 7 as nine rows of 32 entries, then 8 and 9 as nine rows of 8.
        private static ushort[] DigitStrip(ushort[] raw)
        {
            var strip = new ushort[DigitStripWidth * 9];
            for (int row = 0; row < 9; row++)
                for (int col = 0; col < DigitStripWidth; col++)
                {
                    int from = col < 32 ? row * 32 + col : 9 * 32 + row * 8 + (col - 32);
                    if (from < raw.Length) strip[row * DigitStripWidth + col] = raw[from];
                }
            return strip;
        }

        // A button is 4 by 8 tiles built from six source rows, one of them repeated over rows start to end.
        private static ushort[] ButtonBlock(int baseTile, int stretchStart, int stretchEnd)
        {
            var block = new ushort[4 * 8];
            int row = 0;
            for (int y = 0; y < 8; y++)
            {
                for (int x = 0; x < 4; x++)
                    block[y * 4 + x] = (ushort)((15 << 12) | (BoardTileOffset + baseTile + row + x));
                if (y < stretchStart || y >= stretchEnd) row += 32;
            }
            return block;
        }
    }
}
