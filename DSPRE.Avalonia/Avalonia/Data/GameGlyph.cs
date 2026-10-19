using System.Collections.Generic;
using DSPRE.ROMFiles;
using global::Avalonia.Media.Imaging;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// One character of the loaded ROM's own font as a mask (ink opaque, the rest clear), for symbols such as the
    /// Pokédollar that have no character of their own outside the games. Fill it with a brush to colour it.
    /// </summary>
    public static class GameGlyph
    {
        private static readonly Dictionary<char, Bitmap> Cache = new();
        private static string _cachedFor;
        private static FieldFont _font;

        /// <summary>The mask for <paramref name="c"/> as the character map names it, or null when the font lacks it.</summary>
        public static Bitmap Mask(char c)
        {
            string rom = RomInfo.WorkDir;
            if (_cachedFor != rom) { Cache.Clear(); _font = null; _cachedFor = rom; }
            if (Cache.TryGetValue(c, out Bitmap known)) return known;
            Bitmap mask = null;
            try
            {
                _font ??= FieldFont.LoadTalkFont();
                int glyph = FieldFontCharacters.GlyphFor(c);
                if (_font != null && glyph >= 0)
                {
                    int w = System.Math.Max(1, _font.WidthOf(glyph));
                    int h = FieldFont.CellSize;
                    byte[] rgba = new byte[w * h * 4];
                    for (int y = 0; y < h; y++)
                        for (int x = 0; x < w; x++)
                        {
                            // Only the letter itself; the games' drop shadow would read as a smudge in the editor.
                            if (_font.PixelAt(glyph, x, y) != 1) continue;
                            int i = (y * w + x) * 4;
                            rgba[i] = rgba[i + 1] = rgba[i + 2] = rgba[i + 3] = 255;
                        }
                    mask = ImageConverter.FromRgba(rgba, w, h);
                }
            }
            catch (System.Exception ex) { AppLogger.WarnOnce("GameGlyph.Mask", ex); mask = null; }
            Cache[c] = mask;
            return mask;
        }
    }
}
