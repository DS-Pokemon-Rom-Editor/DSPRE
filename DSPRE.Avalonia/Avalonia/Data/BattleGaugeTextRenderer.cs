using System;
using System.Collections.Generic;
using System.IO;
using DSPRE;
using DSPRE.ROMFiles;
using System.Linq;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// Colours the pictures <see cref="BattleGaugeText"/> finds with the gauge's palette, as a battle does. Nothing here
    /// writes to the ROM.
    /// </summary>
    public sealed class BattleGaugeTextRenderer
    {
        public sealed class Drawn
        {
            public byte[] Rgba;
            public int Width, Height;
        }

        /// <summary>Whether this ROM's gauge text can be drawn, and why not when it cannot.</summary>
        public static bool IsAvailable => BattleGaugeText.IsAvailable && Palette() != null;
        public static string Unavailable =>
            BattleGaugeText.Unavailable
            ?? (Palette() == null ? "The gauge's colours couldn't be read." : null);

        // ── the gauge's colours ───────────────────────────────────────────────────────────────────

        private static System.Drawing.Color[] _palette;
        private static string _paletteFor;

        /// <summary>
        /// The gauge's own colours. The text sits in the gauge's tiles, so it uses the first bank, the
        /// same one the frame is drawn with.
        /// </summary>
        private static System.Drawing.Color[] Palette()
        {
            string forRom = RomInfo.WorkDir ?? "";
            if (_paletteFor == forRom) return _palette;
            _paletteFor = forRom;
            _palette = null;

            ScriptNarc narc = new ScriptNarc(RomInfo.DirNames.BattleObj);
            if (!narc.Available) return null;

            int colours = BattleObjects.Find("HpBar.Shared", "Colours");
            if (colours < 0) return null;

            try
            {
                byte[] raw = narc.Get(colours);
                if (raw == null) return null;
                ushort[] all = NitroBgCodec.ReadColours(raw);
                int row = Math.Min(NitroBgCodec.DeclaredRowSize(raw), all.Length);
                if (row >= 16)
                    _palette = all.Take(row).Select(c => { (byte r, byte g, byte b) = NitroBgCodec.Colour(c); return System.Drawing.Color.FromArgb(r, g, b); }).ToArray();
            }
            catch (Exception ex) { AppLogger.Warn("The gauge palette couldn't be read: " + ex.Message); }

            return _palette;
        }

        /// <summary>The gauge's first bank of colours, which its tiles and the pieces copied into them use.</summary>
        public static System.Drawing.Color[] Colours() => Palette();

        /// <summary>Forgets the colours, for when a different ROM is opened.</summary>
        public static void Reset() { _palette = null; _paletteFor = null; BattleGaugeText.Reset(); }

        // ── putting pictures together ─────────────────────────────────────────────────────────────

        /// <summary>
        /// The level as a gauge shows it: the gender symbol and "Lv" in one 16 by 16 block, then the
        /// digits beside it. The digits sit four pixels lower than the block, which is how the games
        /// place them.
        /// </summary>
        public static Drawn LevelWithGender(int level, BattleGaugeText.Gender gender)
        {
            BattleGaugeText.Tile[] block = BattleGaugeText.GenderAndLv(gender);
            if (block == null) return null;

            string digits = Math.Clamp(level, 0, 999).ToString();
            Drawn made = Blank(16 + digits.Length * 8, 16);
            if (made == null) return null;

            Put(made, block[0], 0, 0);
            Put(made, block[1], 8, 0);
            Put(made, block[2], 0, 8);
            Put(made, block[3], 8, 8);

            for (int i = 0; i < digits.Length; i++)
            {
                BattleGaugeText.Tile tile = BattleGaugeText.Digit(digits[i] - '0');
                if (tile != null) PutNumber(made, tile, 16 + i * 8, 4);
            }
            return made;
        }

        /// <summary>The two HP numbers with the slash between them, as the gauge shows them.</summary>
        public static Drawn HealthNumbers(int now, int most)
        {
            string left = Math.Clamp(now, 0, 999).ToString();
            string right = Math.Clamp(most, 0, 999).ToString();
            Drawn made = Blank((left.Length + 1 + right.Length) * 8, 8);
            if (made == null) return null;

            int at = 0;
            foreach (char c in left) { PutNumber(made, BattleGaugeText.Digit(c - '0'), at, 0); at += 8; }
            Put(made, BattleGaugeText.Slash(), at, 0); at += 8;
            foreach (char c in right) { PutNumber(made, BattleGaugeText.Digit(c - '0'), at, 0); at += 8; }
            return made;
        }

        /// <summary>
        /// A name as its gauge shows it: the system font, in the same three colours the numbers use.
        /// Not the talk font, which is a separate file on HeartGold and gives the wrong letters.
        /// </summary>
        public static Drawn Name(string name, int widthInTiles = 8)
        {
            // Refused with the rest so a gauge never mixes the game's letters with a desktop font.
            if (!IsAvailable) return null;

            FieldFont font;
            try { font = FieldFont.LoadSystemFont(); }
            catch (Exception ex) { AppLogger.WarnOnce("BattleGaugeTextRenderer.Name", ex); return null; }
            if (font == null) return null;

            Drawn made = Blank(widthInTiles * 8, 16);
            if (made == null) return null;

            // Only the letters: the panel is already behind them, and a flat fill would cover the frame's slanted edge.

            int at = 0;
            foreach (char c in name ?? "")
            {
                int glyph = FieldFontCharacters.GlyphFor(c);
                if (glyph < 0 || glyph >= font.GlyphCount) continue;

                int wide = Math.Max(1, font.WidthOf(glyph));
                if (at + wide > made.Width) break;

                for (int y = 0; y < Math.Min(font.Height, made.Height); y++)
                    for (int x = 0; x < wide; x++)
                    {
                        // A field font is two bits a pixel, and the game reads them as the same three
                        // things the numbers use: nothing, the letter, then its shadow.
                        byte v = font.PixelAt(glyph, x, y);
                        if (v == 0) continue;
                        System.Drawing.Color colour = Palette()[NumberColour(v, NameShadow) % Palette().Length];
                        int put = (y * made.Width + at + x) * 4;
                        made.Rgba[put] = colour.R;
                        made.Rgba[put + 1] = colour.G;
                        made.Rgba[put + 2] = colour.B;
                        made.Rgba[put + 3] = 255;
                    }
                at += wide;
            }
            return made;
        }

        /// <summary>The word for what is wrong with the Pokemon, or the blank when nothing is.</summary>
        public static Drawn StatusWord(BattleGaugeText.Status status)
        {
            BattleGaugeText.Tile[] tiles = BattleGaugeText.StatusWord(status);
            if (tiles == null) return null;

            Drawn made = Blank(BattleGaugeText.StatusTiles * 8, 8);
            if (made == null) return null;
            for (int i = 0; i < tiles.Length; i++) Put(made, tiles[i], i * 8, 0);
            return made;
        }

        // Number font pixels are placeholders (0 background, 1 letter, 2 shadow) that the game swaps for
        // palette indices as it loads them; the shadow's index differs by game.
        private const byte NumberLetter = 0x0e, NameShadow = 0x02, NumberBack = 0x0f;

        private static byte NumberColour(byte placeholder, byte shadow) => placeholder switch
        {
            1 => NumberLetter,
            2 => shadow,
            _ => NumberBack,
        };

        /// <summary>
        /// Paints a digit. Unlike the gauge's own pictures this covers what is under it, background and
        /// all, because that is what writing a number into the gauge's tiles does.
        /// </summary>
        private static void PutNumber(Drawn into, BattleGaugeText.Tile tile, int atX, int atY)
        {
            if (into == null || tile == null) return;
            System.Drawing.Color[] palette = Palette();
            if (palette == null) return;

            for (int y = 0; y < 8; y++)
            {
                int py = atY + y;
                if (py < 0 || py >= into.Height) continue;
                for (int x = 0; x < 8; x++)
                {
                    int px = atX + x;
                    if (px < 0 || px >= into.Width) continue;

                    System.Drawing.Color colour = palette[NumberColour(tile.At(x, y), BattleGaugeText.DigitShadow) % palette.Length];
                    int at = (py * into.Width + px) * 4;
                    into.Rgba[at] = colour.R;
                    into.Rgba[at + 1] = colour.G;
                    into.Rgba[at + 2] = colour.B;
                    into.Rgba[at + 3] = 255;
                }
            }
        }

        /// <summary>
        /// Somewhere to draw, or null when this ROM isn't one DSPRE reads. Every picture starts here, so this refuses them
        /// all.
        /// </summary>
        private static Drawn Blank(int width, int height)
        {
            if (!IsAvailable || width <= 0 || height <= 0) return null;
            return new Drawn { Rgba = new byte[width * height * 4], Width = width, Height = height };
        }

        /// <summary>
        /// Paints one tile in. Index zero is the hole the gauge shows through, so it stays clear.
        /// </summary>
        private static void Put(Drawn into, BattleGaugeText.Tile tile, int atX, int atY)
        {
            if (into == null || tile == null) return;
            System.Drawing.Color[] palette = Palette();
            if (palette == null) return;

            for (int y = 0; y < 8; y++)
            {
                int py = atY + y;
                if (py < 0 || py >= into.Height) continue;
                for (int x = 0; x < 8; x++)
                {
                    int px = atX + x;
                    if (px < 0 || px >= into.Width) continue;

                    byte index = tile.At(x, y);
                    if (index == 0) continue;

                    System.Drawing.Color colour = palette[index % palette.Length];
                    int at = (py * into.Width + px) * 4;
                    into.Rgba[at] = colour.R;
                    into.Rgba[at + 1] = colour.G;
                    into.Rgba[at + 2] = colour.B;
                    into.Rgba[at + 3] = 255;
                }
            }
        }
    }
}
