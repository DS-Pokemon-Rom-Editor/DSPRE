using System;
using System.Collections.Generic;

namespace DSPRE.Avalonia
{
    /// <summary>
    /// The marks an emote action puts over somebody's head, drawn to the games' shape rather than read out
    /// of the field effect archive.
    /// </summary>
    public static class FieldEmoteMarks
    {
        // '#' outline, '.' balloon, 'r' the mark itself, ' ' see-through.
        private static readonly string[] Exclamation =
        {
            "   ##########   ",
            "  #..........#  ",
            " #.....rr.....# ",
            " #.....rr.....# ",
            " #.....rr.....# ",
            " #.....rr.....# ",
            " #.....rr.....# ",
            " #............# ",
            " #.....rr.....# ",
            "  #..........#  ",
            "   ####..####   ",
            "      #..#      ",
            "       ##       ",
            "                ",
            "                ",
            "                ",
        };

        private static readonly string[] Question =
        {
            "   ##########   ",
            "  #..........#  ",
            " #....rrrr....# ",
            " #...rr..rr...# ",
            " #.......rr...# ",
            " #......rr....# ",
            " #.....rr.....# ",
            " #............# ",
            " #.....rr.....# ",
            "  #..........#  ",
            "   ####..####   ",
            "      #..#      ",
            "       ##       ",
            "                ",
            "                ",
            "                ",
        };

        private static readonly string[] Double =
        {
            "   ##########   ",
            "  #..........#  ",
            " #...rr..rr...# ",
            " #...rr..rr...# ",
            " #...rr..rr...# ",
            " #...rr..rr...# ",
            " #...rr..rr...# ",
            " #............# ",
            " #...rr..rr...# ",
            "  #..........#  ",
            "   ####..####   ",
            "      #..#      ",
            "       ##       ",
            "                ",
            "                ",
            "                ",
        };

        private static readonly Dictionary<string, OverworldSprites.SpritePixels> _cache =
            new Dictionary<string, OverworldSprites.SpritePixels>();

        /// <summary>The mark an emote action by this name puts up.</summary>
        public static OverworldSprites.SpritePixels For(string actionName)
        {
            string[] art = actionName != null && actionName.Contains("Question", StringComparison.Ordinal) ? Question
                         : actionName != null && actionName.Contains("Double", StringComparison.Ordinal) ? Double
                         : Exclamation;
            string key = art == Question ? "?" : art == Double ? "!!" : "!";
            if (_cache.TryGetValue(key, out var hit)) return hit;

            int h = art.Length, w = art[0].Length;
            var rgba = new byte[w * h * 4];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    (byte r, byte g, byte b, byte a) c = art[y][x] switch
                    {
                        '#' => ((byte)0x38, (byte)0x38, (byte)0x38, (byte)0xFF),
                        '.' => ((byte)0xF8, (byte)0xF8, (byte)0xF8, (byte)0xFF),
                        'r' => ((byte)0xE0, (byte)0x30, (byte)0x28, (byte)0xFF),
                        _ => ((byte)0, (byte)0, (byte)0, (byte)0),
                    };
                    int at = (y * w + x) * 4;
                    rgba[at] = c.r; rgba[at + 1] = c.g; rgba[at + 2] = c.b; rgba[at + 3] = c.a;
                }

            var pix = new OverworldSprites.SpritePixels { Rgba = rgba, Width = w, Height = h };
            _cache[key] = pix;
            return pix;
        }
    }
}
