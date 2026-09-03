using System;
using System.Collections.Generic;
using System.IO;
using DSPRE;
using DSPRE.ROMFiles;
using Images;   // NCLR reader

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// Puts the pictures <see cref="BattleGaugeText"/> finds into colour, using the gauge's own palette
    /// because that is what a battle draws them with. Nothing here writes to the ROM.
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
            ?? (Palette() == null ? "The gauge's colours could not be read." : null);

        // ── the gauge's colours ───────────────────────────────────────────────────────────────────

        private static System.Drawing.Color[] _palette;
        private static string _paletteFor;

        /// <summary>
        /// The gauge's own colours. The text sits in the gauge's tiles, so it uses the first bank, the
        /// same one the frame is drawn with.
        /// </summary>
        private static System.Drawing.Color[] Palette()
        {
            string forRom = RomInfo.workDir ?? "";
            if (_paletteFor == forRom) return _palette;
            _paletteFor = forRom;
            _palette = null;

            var narc = new ScriptNarc(RomInfo.DirNames.battleObj);
            if (!narc.Available) return null;

            int colours = BattleObjects.Find("GAGE_PALETTE", "Colours");
            if (colours < 0) return null;

            string temp = null;
            try
            {
                byte[] raw = narc.Get(colours);
                if (raw == null) return null;
                temp = Path.Combine(Path.GetTempPath(), "dspre_gage_pal_" + Guid.NewGuid().ToString("N") + ".nclr");
                File.WriteAllBytes(temp, raw);
                var nclr = new NCLR(temp, colours, Path.GetFileName(temp));
                var banks = nclr.Palette;
                if (banks != null && banks.Length > 0 && banks[0] != null && banks[0].Length >= 16)
                    _palette = banks[0];
            }
            catch (Exception ex) { AppLogger.Warn("The gauge palette could not be read: " + ex.Message); }
            finally { try { if (temp != null) File.Delete(temp); } catch { } }

            return _palette;
        }

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
            var block = BattleGaugeText.GenderAndLv(gender);
            if (block == null) return null;

            string digits = Math.Clamp(level, 0, 999).ToString();
            var made = Blank(16 + digits.Length * 8, 16);
            if (made == null) return null;

            Put(made, block[0], 0, 0);
            Put(made, block[1], 8, 0);
            Put(made, block[2], 0, 8);
            Put(made, block[3], 8, 8);

            for (int i = 0; i < digits.Length; i++)
            {
                var tile = BattleGaugeText.Digit(digits[i] - '0');
                if (tile != null) PutNumber(made, tile, 16 + i * 8, 4);
            }
            return made;
        }

        /// <summary>The two HP numbers with the slash between them, as the gauge shows them.</summary>
        public static Drawn HealthNumbers(int now, int most)
        {
            string left = Math.Clamp(now, 0, 999).ToString();
            string right = Math.Clamp(most, 0, 999).ToString();
            var made = Blank((left.Length + 1 + right.Length) * 8, 8);
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
            // The name alone would come out right on Diamond, since it needs only the font and the
            // gauge's colours. It is refused all the same: half a gauge in the game's own letters and
            // half in a desktop font is a worse thing to look at than the old sample.
            if (!IsAvailable) return null;

            FieldFont font;
            try { font = FieldFont.LoadSystemFont(); }
            catch { return null; }
            if (font == null) return null;

            var made = Blank(widthInTiles * 8, 16);
            if (made == null) return null;

            // Only the letters. The games do fill this block with the panel colour first, but they
            // write it into the gauge's own tiles, split across two sprite pieces, so it can never
            // land outside the gauge. Drawn flat at one spot it can: a 64 pixel block of panel colour
            // painted straight over the frame's slanted edge, which is what it was doing. The panel is
            // already there behind the letters, so leaving it be looks the same and stays inside.

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
                        var colour = Palette()[NumberColour(v) % Palette().Length];
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
            var tiles = BattleGaugeText.StatusWord(status);
            if (tiles == null) return null;

            var made = Blank(BattleGaugeText.StatusTiles * 8, 8);
            if (made == null) return null;
            for (int i = 0; i < tiles.Length; i++) Put(made, tiles[i], i * 8, 0);
            return made;
        }

        // The digits in the number font are not colours. Every pixel is one of three placeholders,
        // 0 for background, 1 for the letter and 2 for its shadow, and the game swaps them for real
        // palette indices as it loads them. A battle asks for letter 0xe, shadow 2, background 0xf,
        // and the level deliberately shares the same one as the HP numbers. Drawing the placeholders
        // as if they were palette indices is why they came out washed out before.
        private const byte NumberLetter = 0x0e, NumberShadow = 0x02, NumberBack = 0x0f;

        private static byte NumberColour(byte placeholder) => placeholder switch
        {
            1 => NumberLetter,
            2 => NumberShadow,
            _ => NumberBack,
        };

        /// <summary>
        /// Paints a digit. Unlike the gauge's own pictures this covers what is under it, background and
        /// all, because that is what writing a number into the gauge's tiles does.
        /// </summary>
        private static void PutNumber(Drawn into, BattleGaugeText.Tile tile, int atX, int atY)
        {
            if (into == null || tile == null) return;
            var palette = Palette();
            if (palette == null) return;

            for (int y = 0; y < 8; y++)
            {
                int py = atY + y;
                if (py < 0 || py >= into.Height) continue;
                for (int x = 0; x < 8; x++)
                {
                    int px = atX + x;
                    if (px < 0 || px >= into.Width) continue;

                    var colour = palette[NumberColour(tile.At(x, y)) % palette.Length];
                    int at = (py * into.Width + px) * 4;
                    into.Rgba[at] = colour.R;
                    into.Rgba[at + 1] = colour.G;
                    into.Rgba[at + 2] = colour.B;
                    into.Rgba[at + 3] = 255;
                }
            }
        }

        /// <summary>
        /// Somewhere to draw, or nothing at all when this ROM is not one we read. Every picture starts
        /// here, so refusing here refuses all of them: the HP numbers used to come back as an empty
        /// strip on Diamond because only the colours were checked, and the colours read fine there.
        /// </summary>
        private static Drawn Blank(int width, int height)
        {
            if (!IsAvailable || width <= 0 || height <= 0) return null;
            return new Drawn { Rgba = new byte[width * height * 4], Width = width, Height = height };
        }

        /// <summary>
        /// Paints one tile in. Index zero is the hole the gauge shows through, so it stays clear rather
        /// than being painted the palette's first colour.
        /// </summary>
        private static void Put(Drawn into, BattleGaugeText.Tile tile, int atX, int atY)
        {
            if (into == null || tile == null) return;
            var palette = Palette();
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

                    var colour = palette[index % palette.Length];
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
