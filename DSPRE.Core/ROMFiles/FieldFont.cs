using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// The font the games write with, read out of the ROM that is loaded so it shows whatever the person
    /// using DSPRE has put there.
    ///
    /// The four fonts named in the leak's convert/message/font_list.txt live in one archive, which is
    /// graphic/font.narc in Diamond and Pearl, graphic/pl_font.narc in Platinum and a/0/1/6 in HeartGold
    /// and SoulSilver. include/system/font_arc.h gives their order, and entry 1 is the one an NPC talks
    /// to you with.
    ///
    /// A font is a sixteen byte header, then one 64 byte picture per letter, then one width byte per
    /// letter. Each letter is four eight by eight blocks, in reading order, and each row of a block is a
    /// little-endian ushort holding eight pixels two bits at a time with the leftmost pixel highest.
    /// The two bit value is 0 outside the letter, 3 for the paper it sits on, and 1 or 2 for the ink.
    /// </summary>
    public sealed class FieldFont
    {
        /// <summary>Entry 1 of the archive, NARC_font_talk_dat, which is the one dialogue uses.</summary>
        public const int TalkFontEntry = 1;

        public const int HeaderSize = 16;
        public const int CellSize = 16;
        public const int BytesPerGlyph = 64;

        /// <summary>Nothing at all: not the letter, not the paper it sits on.</summary>
        public const byte Nothing = 0;
        /// <summary>The paper the letter sits on, which the box has already painted.</summary>
        public const byte Paper = 3;

        private readonly byte[] _pixels;    // one byte a pixel, glyph by glyph, row by row
        private readonly byte[] _widths;

        public int GlyphCount { get; }
        public int MaxWidth { get; }
        public int Height { get; }
        public int BitsPerPixel { get; }

        private FieldFont(byte[] pixels, byte[] widths, int count, int maxWidth, int height, int bpp)
        {
            _pixels = pixels; _widths = widths;
            GlyphCount = count; MaxWidth = maxWidth; Height = height; BitsPerPixel = bpp;
        }

        /// <summary>Reads one font out of the bytes of an archive entry.</summary>
        public static FieldFont Read(byte[] data)
        {
            if (data == null || data.Length < HeaderSize) return null;

            int headerSize = BitConverter.ToInt32(data, 0);
            int tableStart = BitConverter.ToInt32(data, 4);
            int count = BitConverter.ToInt32(data, 8);
            int maxWidth = data[12], height = data[13], bpp = data[14];

            // Anything that is not this shape is not one of these fonts.
            if (headerSize != HeaderSize || bpp != 2 || count <= 0 || count > 0x4000) return null;
            if (tableStart != headerSize + count * BytesPerGlyph) return null;
            if (data.Length < tableStart + count) return null;

            var pixels = new byte[count * CellSize * CellSize];
            for (int g = 0; g < count; g++)
            {
                int read = headerSize + g * BytesPerGlyph;
                int write = g * CellSize * CellSize;
                for (int blockRow = 0; blockRow < 2; blockRow++)
                    for (int blockCol = 0; blockCol < 2; blockCol++)
                        for (int row = 0; row < 8; row++, read += 2)
                        {
                            int packed = data[read] | (data[read + 1] << 8);
                            for (int col = 0; col < 8; col++)
                                pixels[write + (blockRow * 8 + row) * CellSize + blockCol * 8 + col]
                                    = (byte)((packed >> (14 - col * 2)) & 3);
                        }
            }

            var widths = new byte[count];
            Array.Copy(data, tableStart, widths, 0, count);
            return new FieldFont(pixels, widths, count, maxWidth, height, bpp);
        }

        /// <summary>Reads the talking font out of the loaded ROM, or null when it cannot be found.</summary>
        public static FieldFont LoadTalkFont() => LoadFromArchive(TalkFontEntry);

        public static FieldFont LoadFromArchive(int entry)
        {
            try
            {
                if (!RomInfo.gameDirs.TryGetValue(RomInfo.DirNames.fonts, out var dirs)) return null;
                string path = dirs.packedDir;
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
                byte[] blob = ReadNarcEntry(File.ReadAllBytes(path), entry);
                return blob == null ? null : Read(blob);
            }
            catch { return null; }
        }

        /// <summary>How wide a letter is, which is how far along the next one starts.</summary>
        public int WidthOf(int glyph) =>
            glyph >= 0 && glyph < GlyphCount ? Math.Min((int)_widths[glyph], CellSize) : 0;

        /// <summary>What is at one spot in a letter: 0 nothing, 3 paper, 1 or 2 ink.</summary>
        public byte PixelAt(int glyph, int x, int y)
        {
            if (glyph < 0 || glyph >= GlyphCount) return Nothing;
            if (x < 0 || y < 0 || x >= CellSize || y >= CellSize) return Nothing;
            return _pixels[glyph * CellSize * CellSize + y * CellSize + x];
        }

        /// <summary>How wide a run of letters comes out, in pixels.</summary>
        public int Measure(string text, Func<char, int> glyphOf)
        {
            if (string.IsNullOrEmpty(text) || glyphOf == null) return 0;
            int w = 0;
            foreach (char c in text) w += WidthOf(glyphOf(c));
            return w;
        }

        // A NARC is a header, a file table, a name table and then the files themselves.
        private static byte[] ReadNarcEntry(byte[] narc, int index)
        {
            if (narc == null || narc.Length < 0x20) return null;
            if (narc[0] != 'N' || narc[1] != 'A' || narc[2] != 'R' || narc[3] != 'C') return null;

            int fatSize = BitConverter.ToInt32(narc, 0x14);
            int count = BitConverter.ToInt32(narc, 0x18);
            if (index < 0 || index >= count) return null;

            int fntStart = 0x10 + fatSize;
            int fntSize = BitConverter.ToInt32(narc, fntStart + 4);
            int images = fntStart + fntSize + 8;

            int entry = 0x1C + index * 8;
            int from = BitConverter.ToInt32(narc, entry);
            int to = BitConverter.ToInt32(narc, entry + 4);
            if (to < from || images + to > narc.Length) return null;

            var blob = new byte[to - from];
            Array.Copy(narc, images + from, blob, 0, blob.Length);
            return blob;
        }
    }

    /// <summary>
    /// Turns the letters people type into the numbers the font stores its pictures under.
    ///
    /// The font's pictures are in the same order as the games' own character table, one behind it: the
    /// table's code 2 is the first hiragana and that is picture 1, so the picture is the code less one.
    /// The table itself is the charmap DSPRE already uses to read the games' text.
    /// </summary>
    public static class FieldFontCharacters
    {
        private static Dictionary<char, int> _glyphByChar;

        /// <summary>The picture number for a letter, or -1 when the font has nothing for it.</summary>
        public static int GlyphFor(char c)
        {
            var map = Map();
            return map != null && map.TryGetValue(c, out int g) ? g : -1;
        }

        public static bool Ready => Map() != null && Map().Count > 0;

        /// <summary>Forgets the table, for when a different ROM is opened.</summary>
        public static void Reset() { _glyphByChar = null; }

        private static Dictionary<char, int> Map()
        {
            if (_glyphByChar != null) return _glyphByChar;
            var map = new Dictionary<char, int>();
            try
            {
                var charMap = CharMaps.CharMapManager.LoadCharMap();
                if (charMap?.CharacterMap != null)
                    foreach (var pair in charMap.CharacterMap)
                    {
                        if (!int.TryParse(pair.Key, System.Globalization.NumberStyles.HexNumber,
                                          System.Globalization.CultureInfo.InvariantCulture, out int code))
                            continue;
                        string text = pair.Value?.Character;
                        if (text != null && text.Length == 1 && !map.ContainsKey(text[0]))
                            map[text[0]] = code - 1;
                    }
            }
            catch { }
            _glyphByChar = map;
            return _glyphByChar;
        }
    }
}
