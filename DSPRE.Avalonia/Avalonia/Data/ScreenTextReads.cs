using System;
using System.Collections.Generic;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// Every text archive line a screen book prints goes through here, so the Text editor can tell which pages show a
    /// line and draw them with the line as it is being edited rather than as saved.
    /// </summary>
    internal static class ScreenTextReads
    {
        [ThreadStatic] private static HashSet<(int Bank, int Line)> _seen;
        [ThreadStatic] private static (int Bank, int Line, string Text)? _edit;

        /// <summary>The line a page prints: the edit when one is being watched, else the archive's own.</summary>
        public static string Line(int bank, int line, IReadOnlyList<string> lines)
        {
            _seen?.Add((bank, line));
            if (_edit is (int b, int l, string text) && b == bank && l == line) return text;
            return lines != null && line >= 0 && line < lines.Count ? lines[line] : null;
        }

        /// <summary>Records what this thread reads until disposed, printing <paramref name="text"/> for the watched line.</summary>
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
