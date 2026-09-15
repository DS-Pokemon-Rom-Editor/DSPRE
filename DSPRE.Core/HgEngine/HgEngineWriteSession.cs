using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace DSPRE.HgEngine
{
    /// <summary>A comment a save would delete from source, and where it was.</summary>
    public sealed class HgEngineLostComment
    {
        public string FilePath { get; init; }
        /// <summary>The entry it sat in, such as "trainer 5", or the file name outside every entry.</summary>
        public string Entry { get; init; }
        /// <summary>Where inside the entry, such as "party 2 moves"; empty directly under the entry.</summary>
        public string Place { get; init; }
        public string Text { get; init; }
    }

    /// <summary>
    /// Holds one save's source writes in memory until it is committed. A save that fails part way writes
    /// nothing, and the comments it would delete are known before anything reaches disk.
    /// </summary>
    public sealed class HgEngineWriteSession : IDisposable
    {
        private static readonly AsyncLocal<HgEngineWriteSession> _current = new();
        internal static HgEngineWriteSession Current => _current.Value;

        private sealed class Pending
        {
            public string Original;
            public string Text;
            public Encoding Encoding;
            public bool Crlf;
        }

        private readonly Dictionary<string, Pending> _pending = new(StringComparer.OrdinalIgnoreCase);
        private bool _ended;

        public static HgEngineWriteSession Begin()
        {
            var session = new HgEngineWriteSession();
            _current.Value = session;
            return session;
        }

        internal bool TryGetText(string path, out string text)
        {
            text = _pending.TryGetValue(path, out var p) ? p.Text : null;
            return text != null;
        }

        internal bool Holds(string path) => _pending.ContainsKey(path);

        internal void Record(string path, string original, string text, Encoding encoding, bool crlf)
        {
            if (_pending.TryGetValue(path, out var p))
            {
                p.Text = text;
                p.Encoding = encoding;
                p.Crlf = crlf;
            }
            else _pending[path] = new Pending { Original = original, Text = text, Encoding = encoding, Crlf = crlf };
        }

        public IReadOnlyList<HgEngineLostComment> LostComments()
        {
            var lost = new List<HgEngineLostComment>();
            foreach (var (path, p) in _pending)
                lost.AddRange(HgEngineSourceComments.Lost(path, p.Original, p.Text));
            return lost;
        }

        /// <summary>Writes every held file. Deleted comments are appended to their file when kept.</summary>
        public bool Commit(bool keepLostComments, out string error)
        {
            error = null;
            End();
            foreach (var (path, p) in _pending)
            {
                string text = keepLostComments ? HgEngineSourceComments.AppendKept(p.Text, HgEngineSourceComments.Lost(path, p.Original, p.Text)) : p.Text;
                try { HgEngineFileCache.WriteText(path, text, p.Encoding, p.Crlf, keepLostComments: false); }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    error = $"Could not write {Path.GetFileName(path)}: {ex.Message}";
                    return false;
                }
            }
            _pending.Clear();
            return true;
        }

        /// <summary>Later writes go straight to disk again; what was held stays until committed or disposed.</summary>
        public void StopRecording() => End();

        private void End()
        {
            if (_ended) return;
            _ended = true;
            if (_current.Value == this) _current.Value = null;
        }

        /// <summary>Ends the session. Anything not committed is dropped.</summary>
        public void Dispose() => End();
    }

    /// <summary>Finds the comments one version of a C source file has that the next version lacks.</summary>
    internal static class HgEngineSourceComments
    {
        private static readonly HashSet<string> CommentedExtensions = new(StringComparer.OrdinalIgnoreCase) { ".c", ".h", ".s", ".asm" };
        private static readonly Regex EntryStart = new(@"\[\s*([^\[\]\r\n]+?)\s*\]\s*=\s*\{", RegexOptions.Compiled);
        private static readonly Regex Designator = new(@"\G\.\s*(\w+)\s*=(?!=)|\G\[\s*([^\]\r\n]+?)\s*\]\s*=");

        private readonly record struct Comment(int Start, string Text);

        internal static List<HgEngineLostComment> Lost(string path, string before, string after)
        {
            var lost = new List<HgEngineLostComment>();
            if (before == null || after == null || !CommentedExtensions.Contains(Path.GetExtension(path))) return lost;

            var remaining = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var c in Find(after)) remaining[c.Text] = remaining.GetValueOrDefault(c.Text) + 1;

            List<(int Open, int Close, string Name)> entries = null;
            foreach (var c in Find(before))
            {
                if (remaining.TryGetValue(c.Text, out int left) && left > 0) { remaining[c.Text] = left - 1; continue; }
                entries ??= TopLevelEntries(before);
                var (entry, place) = Locate(path, before, entries, c.Start);
                lost.Add(new HgEngineLostComment { FilePath = path, Entry = entry, Place = place, Text = c.Text });
            }
            return lost;
        }

        /// <summary>One line per entry: "// Comments that existed on trainer 5: text party 2: text".</summary>
        internal static string AppendKept(string text, IReadOnlyList<HgEngineLostComment> lost)
        {
            if (lost.Count == 0) return text;
            var sb = new StringBuilder(text);
            if (!text.EndsWith("\n", StringComparison.Ordinal)) sb.Append('\n');
            foreach (var group in lost.GroupBy(c => c.Entry))
            {
                sb.Append("\n// Comments that existed on ").Append(group.Key).Append(':');
                foreach (var c in group)
                {
                    if (c.Place.Length > 0) sb.Append(' ').Append(c.Place).Append(':');
                    sb.Append(' ').Append(c.Text);
                }
            }
            sb.Append('\n');
            return sb.ToString();
        }

        private static List<Comment> Find(string text)
        {
            var found = new List<Comment>();
            int i = 0;
            while (i < text.Length)
            {
                char c = text[i];
                bool comment = c == '/' && i + 1 < text.Length && (text[i + 1] == '/' || text[i + 1] == '*');
                if (comment || c == '"' || c == '\'')
                {
                    int start = i;
                    BraceScanner.SkipNonCode(text, ref i);
                    if (comment)
                    {
                        string clean = Clean(text.Substring(start, i - start));
                        if (clean.Length > 0) found.Add(new Comment(start, clean));
                    }
                    continue;
                }
                i++;
            }
            return found;
        }

        private static string Clean(string comment)
        {
            string body = comment.StartsWith("//", StringComparison.Ordinal)
                ? comment.Substring(2)
                : comment.Substring(2, Math.Max(0, comment.Length - (comment.EndsWith("*/", StringComparison.Ordinal) ? 4 : 2)));
            return Regex.Replace(body, @"\s+", " ").Trim();
        }

        private static List<(int Open, int Close, string Name)> TopLevelEntries(string text)
        {
            var entries = new List<(int, int, string)>();
            int lastClose = -1;
            foreach (Match m in EntryStart.Matches(text))
            {
                int open = m.Index + m.Length - 1;
                if (open < lastClose || !BraceScanner.TryFindMatchingBrace(text, open, out int close)) continue;
                entries.Add((open, close, m.Groups[1].Value));
                lastClose = close;
            }
            return entries;
        }

        private static (string entry, string place) Locate(string path, string text, List<(int Open, int Close, string Name)> entries, int offset)
        {
            foreach (var (open, close, name) in entries)
            {
                if (offset < open || offset > close) continue;
                string entry = Path.GetFileName(path).Equals("Trainers.c", StringComparison.OrdinalIgnoreCase) ? "trainer " + name : name;
                return (entry, PlaceIn(text, open, close, offset));
            }
            return (Path.GetFileName(path), "");
        }

        private static string PlaceIn(string text, int open, int close, int offset)
        {
            var words = new List<string>();
            while (true)
            {
                var spans = ElementScanner.ElementSpans(text, open, close);
                int k = ElementAt(text, spans, offset);
                if (k < 0) break;

                var (start, end) = spans[k];
                var named = Designator.Match(text, start);
                bool designated = named.Success && named.Index + named.Length <= end;
                // Positions count from 1, as the editors number party slots and rows.
                words.Add(!designated ? (k + 1).ToString() : named.Groups[1].Success ? named.Groups[1].Value : named.Groups[2].Value);

                int value = designated ? named.Index + named.Length : start;
                while (value < end && char.IsWhiteSpace(text[value])) value++;
                if (value < end && text[value] == '{' && BraceScanner.TryFindMatchingBrace(text, value, out int inner)
                    && value < offset && offset < inner)
                {
                    open = value;
                    close = inner;
                    continue;
                }
                break;
            }
            return string.Join(" ", words);
        }

        // A comment above an element or inside it belongs to that element; one after a value on the same line belongs to that value.
        private static int ElementAt(string text, List<(int Start, int End)> spans, int offset)
        {
            for (int k = 0; k < spans.Count; k++)
            {
                if (offset <= spans[k].End) return k;
                int nl = text.IndexOf('\n', spans[k].End);
                if (nl < 0 || offset < nl) return k;
            }
            return -1;
        }
    }
}
