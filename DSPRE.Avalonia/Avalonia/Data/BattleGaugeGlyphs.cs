using System;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// The pieces of gauge text, packed as tiles ready to write into a gauge's graphic. These are
    /// palette indices, not colours: a battle asks for letter 0x0e, shadow 0x02 and background 0x0f
    /// for all of them, which is why the name, level and numbers match on screen.
    /// </summary>
    public static class BattleGaugeGlyphs
    {
        public const byte Letter = 0x0e, Shadow = 0x02, Background = 0x0f;

        private const int Tile = 8;
        private const int TileBytes = 32;      // 8 by 8 at four bits a pixel

        /// <summary>The name, eight tiles across and two down, in the order a gauge expects them.</summary>
        public static byte[] NameBlock(string name)
        {
            FieldFont font;
            try { font = FieldFont.LoadSystemFont(); }
            catch { return null; }
            if (font == null) return null;

            const int wide = 8 * Tile, tall = 2 * Tile;
            var pixels = new byte[wide * tall];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Background;

            int at = 0;
            foreach (char c in name ?? "")
            {
                int glyph = FieldFontCharacters.GlyphFor(c);
                if (glyph < 0 || glyph >= font.GlyphCount) continue;

                int step = Math.Max(1, font.WidthOf(glyph));
                if (at + step > wide) break;

                for (int y = 0; y < Math.Min(font.Height, tall); y++)
                    for (int x = 0; x < step; x++)
                        pixels[y * wide + at + x] = Ink(font.PixelAt(glyph, x, y));
                at += step;
            }
            return Pack(pixels, wide, tall);
        }

        /// <summary>The gender symbol and the "Lv" beside it: two tiles across, two down.</summary>
        public static byte[] GenderAndLvBlock(BattleGaugeText.Gender gender)
        {
            var block = BattleGaugeText.GenderAndLv(gender);
            if (block == null || block.Length < 4) return null;

            // the game keeps this as four tiles already, upper pair then lower pair
            var made = new byte[4 * TileBytes];
            for (int t = 0; t < 4; t++) PackTile(block[t], made, t * TileBytes);
            return made;
        }

        /// <summary>
        /// A number across so many tiles, in the gauge's own colours. The games put the level and the
        /// maximum health against the left and the current health against the right, padding it with
        /// blanks, which is why the two numbers either side of the "/" line up the way they do.
        /// </summary>
        public static byte[] NumberRow(int value, int tiles, bool againstTheRight = false)
        {
            if (tiles <= 0) return null;

            int wide = tiles * Tile;
            var pixels = new byte[wide * Tile];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Background;

            string digits = Math.Clamp(value, 0, 999).ToString();
            int from = againstTheRight ? Math.Max(0, tiles - digits.Length) : 0;
            for (int i = 0; i < digits.Length && from + i < tiles; i++)
            {
                var tile = BattleGaugeText.Digit(digits[i] - '0');
                if (tile == null) continue;
                for (int y = 0; y < Tile; y++)
                    for (int x = 0; x < Tile; x++)
                        pixels[y * wide + (from + i) * Tile + x] = Ink(tile.At(x, y));
            }
            return Pack(pixels, wide, Tile);
        }

        /// <summary>The word for what is wrong with a Pokemon, three tiles across.</summary>
        public static byte[] StatusRow(BattleGaugeText.Status status)
        {
            var tiles = BattleGaugeText.StatusWord(status);
            if (tiles == null) return null;

            var made = new byte[tiles.Length * TileBytes];
            for (int t = 0; t < tiles.Length; t++) PackTile(tiles[t], made, t * TileBytes);
            return made;
        }

        /// <summary>
        /// The three placeholders a font uses become the three colours a battle asks for. Both the field
        /// font and the number font mean the same by them: nothing, the letter, then its shadow.
        /// </summary>
        internal static byte Ink(byte placeholder) => placeholder switch
        {
            1 => Letter,
            2 => Shadow,
            _ => Background,
        };

        /// <summary>Packs a picture into tiles, left to right then top to bottom, two pixels a byte.</summary>
        private static byte[] Pack(byte[] pixels, int wide, int tall)
        {
            int across = wide / Tile, down = tall / Tile;
            var made = new byte[across * down * TileBytes];
            for (int t = 0; t < across * down; t++)
            {
                int col = t % across, row = t / across;
                for (int y = 0; y < Tile; y++)
                    for (int x = 0; x < Tile; x += 2)
                    {
                        byte low = pixels[(row * Tile + y) * wide + col * Tile + x];
                        byte high = pixels[(row * Tile + y) * wide + col * Tile + x + 1];
                        made[t * TileBytes + y * (Tile / 2) + x / 2] = (byte)((low & 0xF) | (high << 4));
                    }
            }
            return made;
        }

        private static void PackTile(BattleGaugeText.Tile tile, byte[] into, int at)
        {
            if (tile == null) return;
            for (int y = 0; y < Tile; y++)
                for (int x = 0; x < Tile; x += 2)
                    into[at + y * (Tile / 2) + x / 2] =
                        (byte)((tile.At(x, y) & 0xF) | (tile.At(x + 1, y) << 4));
        }
    }
}
