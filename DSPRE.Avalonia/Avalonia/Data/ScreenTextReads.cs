using System;
using System.Collections.Generic;

namespace DSPRE.Avalonia.Data
{
    /// <summary>Reads the lines the Pokédex and summary pages print, so the Text editor can find a line's pages and show your edit on them.</summary>
    internal static class ScreenTextReads
    {
        [ThreadStatic] private static HashSet<(int Bank, int Line)> _seen;
        [ThreadStatic] private static (int Bank, int Line, string Text)? _edit;

        /// <summary>A line from the archive, or your edit if it's the line being watched.</summary>
        public static string Line(int bank, int line, IReadOnlyList<string> lines)
        {
            _seen?.Add((bank, line));
            if (_edit is (int b, int l, string text) && b == bank && l == line) return text;
            return lines != null && line >= 0 && line < lines.Count ? lines[line] : null;
        }

        /// <summary>Until disposed, notes every line read on this thread and shows <paramref name="text"/> for the watched one.</summary>
        public static IDisposable Watch(int bank, int line, string text, HashSet<(int Bank, int Line)> seen)
        {
            _seen = seen;
            _edit = (bank, line, text);
            return new Stop();
        }

        private sealed class Stop : IDisposable
        {
            public void Dispose() { _seen = null; _edit = null; }
        }
    }
}
