using System;
using System.Collections.Generic;
using System.Text;

namespace DSPRE.ROMFiles
{
    /// <summary>What the box does once the player presses a button.</summary>
    public enum MessageWait
    {
        /// <summary>Nothing more to read; the box closes.</summary>
        None,
        /// <summary>Wipes the box and starts again at the top line. NORMAL_WAIT_, written "\r".</summary>
        Clear,
        /// <summary>Slides everything up one line and carries on underneath. SCROLL_WAIT_, written "\f".</summary>
        Scroll,
        /// <summary>Just waits, then carries on where it was. SIMPLE_WAIT_, the "■" code.</summary>
        Simple,
    }

    /// <summary>One moment where the box sits and waits for the player to read it.</summary>
    public sealed class FieldMessageFrame
    {
        /// <summary>The lines on show, top first. Never more than the box holds.</summary>
        public IReadOnlyList<string> Lines { get; }

        /// <summary>What happens next once the player presses.</summary>
        public MessageWait Wait { get; }

        /// <summary>A line here is wider than the box, so the game would cut it off.</summary>
        public bool TooWide { get; }

        /// <summary>More lines were written than the box holds, so the game would lose them.</summary>
        public bool TooManyLines { get; }

        public FieldMessageFrame(IReadOnlyList<string> lines, MessageWait wait, bool tooWide, bool tooManyLines)
        {
            Lines = lines; Wait = wait; TooWide = tooWide; TooManyLines = tooManyLines;
        }

        public string Text => string.Join("\n", Lines);
        public override string ToString() => Text;
    }

    /// <summary>Works out what the message box shows, step by step, the way the games do it.</summary>
    public static class FieldMessageScript
    {
        private enum Token { Text, LineBreak, WaitClear, WaitScroll, WaitSimple }

        /// <summary>The frames a message plays out as. </summary>
        public static List<FieldMessageFrame> Frames(string text, Func<string, int> measure,
                                                     int width = FieldMessageWindow.TextWidth,
                                                     int linesPerBox = FieldMessageWindow.LinesPerPage,
                                                     bool wrapWhenUnmarked = true)
        {
            var frames = new List<FieldMessageFrame>();
            if (string.IsNullOrEmpty(text)) return frames;

            var parts = Tokenise(text);

            // Script text that carries none of the games' own breaks has nothing to say about how it
            // should sit in the box, so it is fitted to the box instead of running off the edge.
            bool marked = parts.Exists(p => p.token != Token.Text);
            if (!marked && wrapWhenUnmarked)
                return Wrapped(text, measure, width, linesPerBox);

            var onScreen = new List<string>();
            var current = new StringBuilder();
            bool tooWide = false, tooMany = false;

            void EndLine()
            {
                string line = current.ToString();
                current.Clear();
                if (measure != null && measure(line) > width) tooWide = true;
                if (onScreen.Count >= linesPerBox) tooMany = true;
                else onScreen.Add(line);
            }

            void Emit(MessageWait wait)
            {
                EndLine();
                frames.Add(new FieldMessageFrame(new List<string>(onScreen), wait, tooWide, tooMany));
                tooWide = false; tooMany = false;
            }

            foreach (var (token, value) in parts)
            {
                switch (token)
                {
                    case Token.Text: current.Append(value); break;
                    case Token.LineBreak: EndLine(); break;

                    case Token.WaitClear:
                        Emit(MessageWait.Clear);
                        onScreen.Clear();
                        break;

                    case Token.WaitScroll:
                        Emit(MessageWait.Scroll);
                        // Everything slides up one line, so the top line goes and the rest moves up.
                        if (onScreen.Count > 0) onScreen.RemoveAt(0);
                        break;

                    case Token.WaitSimple:
                        Emit(MessageWait.Simple);
                        break;
                }
            }

            EndLine();
            if (onScreen.Count > 0 || frames.Count == 0)
                frames.Add(new FieldMessageFrame(new List<string>(onScreen), MessageWait.None, tooWide, tooMany));
            else
                frames[frames.Count - 1] = new FieldMessageFrame(frames[frames.Count - 1].Lines,
                    MessageWait.None, frames[frames.Count - 1].TooWide, frames[frames.Count - 1].TooManyLines);

            return frames;
        }

        // Text with none of the games' breaks in it, fitted to the box a boxful at a time.
        private static List<FieldMessageFrame> Wrapped(string text, Func<string, int> measure, int width, int linesPerBox)
        {
            var frames = new List<FieldMessageFrame>();
            var layout = new FieldTextLayout(measure ?? (t => (t ?? "").Length * 6), width, linesPerBox);
            var lines = layout.Lines(text);

            for (int i = 0; i < lines.Count; i += linesPerBox)
            {
                var box = lines.GetRange(i, Math.Min(linesPerBox, lines.Count - i));
                bool last = i + linesPerBox >= lines.Count;
                frames.Add(new FieldMessageFrame(box, last ? MessageWait.None : MessageWait.Clear, false, false));
            }
            if (frames.Count == 0) frames.Add(new FieldMessageFrame(new List<string> { "" }, MessageWait.None, false, false));
            return frames;
        }

        // The breaks turn up both as real control characters and as the two-character spellings the
        // text editor shows, so both are read here.
        private static List<(Token token, string value)> Tokenise(string text)
        {
            var parts = new List<(Token, string)>();
            var run = new StringBuilder();

            void Flush() { if (run.Length > 0) { parts.Add((Token.Text, run.ToString())); run.Clear(); } }

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (c == '\\' && i + 1 < text.Length)
                {
                    char next = text[i + 1];
                    if (next == 'n') { Flush(); parts.Add((Token.LineBreak, null)); i++; continue; }
                    if (next == 'r') { Flush(); parts.Add((Token.WaitClear, null)); i++; continue; }
                    if (next == 'f') { Flush(); parts.Add((Token.WaitScroll, null)); i++; continue; }
                }

                switch (c)
                {
                    case '\n': Flush(); parts.Add((Token.LineBreak, null)); continue;
                    case '\r': Flush(); parts.Add((Token.WaitClear, null)); continue;
                    case '\f': Flush(); parts.Add((Token.WaitScroll, null)); continue;
                    case '▼': Flush(); parts.Add((Token.WaitClear, null)); continue;   // ▼
                    case '▽': Flush(); parts.Add((Token.WaitScroll, null)); continue;  // ▽
                    case '■': Flush(); parts.Add((Token.WaitSimple, null)); continue;  // ■
                }
                run.Append(c);
            }
            Flush();
            return parts;
        }
    }
}
