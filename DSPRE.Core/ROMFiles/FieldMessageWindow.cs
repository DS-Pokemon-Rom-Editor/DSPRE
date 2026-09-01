using System;
using System.Collections.Generic;
using System.Text;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// Where the games put the box an NPC talks to you from, and how the words are laid out in it.
    /// </summary>
    public static class FieldMessageWindow
    {
        public const int ScreenWidth = 256;
        public const int ScreenHeight = 192;

        public const int TileSize = 8;
        public const int TileX = 2;      // FLD_MSG_WIN_PX
        public const int TileY = 19;     // FLD_MSG_WIN_PY
        public const int TilesWide = 27; // FLD_MSG_WIN_SX
        public const int TilesHigh = 4;  // FLD_MSG_WIN_SY

        public const int TextLeft = TileX * TileSize;
        public const int TextTop = TileY * TileSize;
        public const int TextWidth = TilesWide * TileSize;
        public const int TextHeight = TilesHigh * TileSize;

        /// <summary>Two lines of writing fit in the four tiles the window is high.</summary>
        public const int LinesPerPage = 2;
        public const int LineHeight = TextHeight / LinesPerPage;

        // BmpTalkWinWriteMain in window.c:356 lays the frame out in eighteen tiles: two columns to the left
        // of the writing, three to the right, and a row above and below.
        public const int FrameTilesLeft = 2;
        public const int FrameTilesRight = 3;

        public const int FrameLeft = (TileX - FrameTilesLeft) * TileSize;
        public const int FrameTop = (TileY - 1) * TileSize;
        public const int FrameWidth = (FrameTilesLeft + TilesWide + FrameTilesRight) * TileSize;
        public const int FrameHeight = (TilesHigh + 2) * TileSize;
    }

    /// <summary>Fits a run of words to the width of the box.</summary>
    public sealed class FieldTextLayout
    {
        private readonly Func<string, int> _measure;
        private readonly int _width;
        private readonly int _linesPerPage;

        public FieldTextLayout(Func<string, int> measure,
                               int width = FieldMessageWindow.TextWidth,
                               int linesPerPage = FieldMessageWindow.LinesPerPage)
        {
            _measure = measure ?? throw new ArgumentNullException(nameof(measure));
            _width = Math.Max(8, width);
            _linesPerPage = Math.Max(1, linesPerPage);
        }

        /// <summary>The lines the text breaks into, keeping the breaks the text already asks for.</summary>
        public List<string> Lines(string text)
        {
            var lines = new List<string>();
            if (string.IsNullOrEmpty(text)) return lines;

            foreach (string paragraph in Split(text))
            {
                if (paragraph.Length == 0) { lines.Add(""); continue; }
                var line = new StringBuilder();
                foreach (string word in paragraph.Split(' '))
                {
                    if (word.Length == 0) continue;
                    string candidate = line.Length == 0 ? word : line + " " + word;
                    if (line.Length > 0 && _measure(candidate) > _width)
                    {
                        lines.Add(line.ToString());
                        line.Clear();
                        line.Append(word);
                    }
                    else
                    {
                        line.Clear();
                        line.Append(candidate);
                    }
                }
                lines.Add(line.ToString());
            }
            return lines;
        }

        /// <summary>
        /// The text fitted into boxfuls, for text that carries none of the games' own breaks.
        /// </summary>
        public List<string> Pages(string text)
        {
            var pages = new List<string>();
            var lines = Lines(text);
            for (int i = 0; i < lines.Count; i += _linesPerPage)
                pages.Add(string.Join("\n", lines.GetRange(i, Math.Min(_linesPerPage, lines.Count - i))));
            return pages;
        }

        // Only a real newline breaks a line here. The games' own break codes are not plain newlines and
        // are dealt with where their meanings are known.
        private static IEnumerable<string> Split(string text) => text.Split('\n');
    }
}
