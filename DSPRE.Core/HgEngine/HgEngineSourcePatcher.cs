using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DSPRE.HgEngine
{
    /// <summary>One step of a field path into a designated-initializer entry: either a named field
    /// (.data.trainerClass -> Field("data"), Field("trainerClass")) or a positional array slot
    /// (.party[1].species -> Field("party"), At(1), Field("species")).</summary>
    public readonly struct FieldPathSegment
    {
        public string Name { get; }
        public int Index { get; }
        public bool IsIndex { get; }
        private FieldPathSegment(string name, int index, bool isIndex) { Name = name; Index = index; IsIndex = isIndex; }
        public static FieldPathSegment Field(string name) => new(name, -1, false);
        public static FieldPathSegment At(int index) => new(null, index, true);
        public override string ToString() => IsIndex ? $"[{Index}]" : $".{Name}";
    }

    /// <summary>
    /// Replaces one field's value in a C designated-initializer entry and nothing else; a field that can't be found is
    /// never guessed at.
    /// </summary>
    public static class HgEngineSourcePatcher
    {
        /// <summary>Finds "[designatorToken] = { ... }" and returns the span of its braces (inclusive).</summary>
        public static bool TryFindEntry(string text, string designatorToken, out int openBrace, out int closeBrace)
        {
            openBrace = closeBrace = -1;
            if (CSourceFile.For(text).FindEntry(designatorToken) is not CEntry entry || entry.Close < 0) return false;
            openBrace = entry.Open; closeBrace = entry.Close;
            return true;
        }

        /// <summary>Reads a field's current raw source text (trimmed), without modifying anything.</summary>
        public static bool TryGetFieldValue(string text, string designatorToken, IReadOnlyList<FieldPathSegment> path, out string rawValue)
        {
            rawValue = null;
            if (!TryFindEntry(text, designatorToken, out int open, out int close)) return false;
            if (!ElementScanner.TryLocateValueSpan(text, open, close, path, out int vs, out int ve)) return false;
            rawValue = text.Substring(vs, ve - vs).Trim();
            return true;
        }

        /// <summary>Replaces exactly one field's value token in place, leaving the rest of the source
        /// untouched. Returns false (no mutation) if the entry or the field within it can't be located.</summary>
        public static bool TryReplaceField(ref string text, string designatorToken, IReadOnlyList<FieldPathSegment> path, string newValueLiteral)
        {
            if (!TryFindEntry(text, designatorToken, out int open, out int close)) return false;
            if (!ElementScanner.TryLocateValueSpan(text, open, close, path, out int vs, out int ve)) return false;
            // An unchanged value keeps its own layout and comments; a multi-line block would otherwise be
            // flattened onto one line every save.
            if (SameTokens(text.Substring(vs, ve - vs), newValueLiteral)) return true;
            text = string.Concat(text.AsSpan(0, vs), newValueLiteral, text.AsSpan(ve));
            return true;
        }

        /// <summary>True when two values are the same C tokens, ignoring whitespace, comments and a trailing comma
        /// before a closing brace or bracket.</summary>
        internal static bool SameTokens(string a, string b) => Tokens(a).SequenceEqual(Tokens(b));

        private static List<string> Tokens(string s)
        {
            List<string> texts = new List<string>();
            foreach (CToken token in CLexer.Tokenize(s))
            {
                string text = token.Text(s);
                if (text is "}" or "]" && texts.Count > 0 && texts[^1] == ",") texts.RemoveAt(texts.Count - 1);
                texts.Add(text);
            }
            return texts;
        }

        /// <summary>
        /// <see cref="TryReplaceField"/> that adds the field before its block's closing brace when it is missing; every
        /// parent block must exist.
        /// </summary>
        public static bool TryUpsertField(ref string text, string designatorToken, IReadOnlyList<FieldPathSegment> path, string newValueLiteral)
        {
            if (TryReplaceField(ref text, designatorToken, path, newValueLiteral)) return true;
            if (path.Count == 0 || path[^1].IsIndex) return false;   // can't synthesize a designator-less positional element

            if (!TryFindEntry(text, designatorToken, out int open, out int close)) return false;
            if (!ElementScanner.TryLocateParentBlock(text, open, close, path, out int parentOpen, out int parentClose)) return false;

            List<(int Start, int End)> fields = ElementScanner.ElementSpans(text, parentOpen, parentClose);
            if (fields.Count > 0)
            {
                // New field goes on its own line after the last one, at the same indent.
                string line = "\n" + IndentOfLine(text, fields[0].Start) + $".{path[^1].Name} = {newValueLiteral},";
                InsertAfterLastElement(ref text, fields[^1].End, parentClose, MatchNewlines(text, line));
                return true;
            }

            // Only add a leading comma if the prior field doesn't already end with one.
            int checkPos = parentClose - 1;
            while (checkPos > parentOpen && char.IsWhiteSpace(text[checkPos])) checkPos--;
            bool needsLeadingComma = checkPos > parentOpen && text[checkPos] != ',';
            string newField = (needsLeadingComma ? "," : "") + $"\n            .{path[^1].Name} = {newValueLiteral},";
            text = string.Concat(text.AsSpan(0, parentClose), MatchNewlines(text, newField), text.AsSpan(parentClose));
            return true;
        }

        /// <summary>Deletes a named field and its comma. An absent field counts as removed; false only when
        /// the entry or the field's parent block can't be found.</summary>
        public static bool TryRemoveField(ref string text, string designatorToken, IReadOnlyList<FieldPathSegment> path)
        {
            if (path.Count == 0 || path[^1].IsIndex) return false;
            if (!TryFindEntry(text, designatorToken, out int open, out int close)) return false;
            if (!ElementScanner.TryLocateParentBlock(text, open, close, path, out int parentOpen, out int parentClose)) return false;

            List<(int Start, int End)> fields = ElementScanner.ElementSpans(text, parentOpen, parentClose);
            for (int i = 0; i < fields.Count; i++)
            {
                if (CSourceFile.LeadingField(text, fields[i].Start, fields[i].End) != path[^1].Name) continue;
                int cutStart = PastLineComment(text, i == 0 ? parentOpen + 1 : CLexer.PastComma(text, fields[i - 1].End, parentClose), parentClose);
                int cutEnd = PastLineComment(text, CLexer.PastComma(text, fields[i].End, parentClose), parentClose);
                text = string.Concat(text.AsSpan(0, cutStart), text.AsSpan(cutEnd));
                return true;
            }
            return true;
        }

        /// <summary>Makes the array at <paramref name="arrayPath"/> hold exactly <paramref name="count"/>
        /// elements: drops the tail, or appends <paramref name="newElement"/>(index, indent) blocks.</summary>
        public static bool TrySetArrayCount(ref string text, string designatorToken, IReadOnlyList<FieldPathSegment> arrayPath,
            int count, Func<int, string, string> newElement)
        {
            if (count < 0 || !TryFindEntry(text, designatorToken, out int entryOpen, out int entryClose)) return false;

            if (!ElementScanner.TryLocateValueSpan(text, entryOpen, entryClose, arrayPath, out int vs, out int ve))
            {
                if (count == 0) return true;
                string indent = IndentOfLine(text, entryOpen) + "        ";
                StringBuilder literal = new System.Text.StringBuilder("{");
                for (int i = 0; i < count; i++) literal.Append('\n').Append(indent).Append(newElement(i, indent)).Append(',');
                literal.Append('\n').Append(indent, 0, indent.Length - 4).Append('}');
                return TryUpsertField(ref text, designatorToken, arrayPath, literal.ToString());
            }

            while (vs < ve && char.IsWhiteSpace(text[vs])) vs++;
            if (vs >= ve || text[vs] != '{' || !BraceScanner.TryFindMatchingBrace(text, vs, out int close)) return false;

            List<(int Start, int End)> spans = ElementScanner.ElementSpans(text, vs, close);
            int n = spans.Count;
            if (count == n) return true;

            if (count < n)
            {
                int cutStart = PastLineComment(text, count == 0 ? vs + 1 : CLexer.PastComma(text, spans[count - 1].End, close), close);
                int cutEnd = PastLineComment(text, CLexer.PastComma(text, spans[n - 1].End, close), close);
                text = string.Concat(text.AsSpan(0, cutStart), text.AsSpan(cutEnd));
                return true;
            }

            string elementIndent = n > 0 ? IndentOfLine(text, spans[0].Start) : IndentOfLine(text, vs) + "    ";
            StringBuilder added = new System.Text.StringBuilder();
            for (int i = n; i < count; i++) added.Append('\n').Append(elementIndent).Append(newElement(i, elementIndent)).Append(',');
            if (n > 0)
            {
                InsertAfterLastElement(ref text, spans[n - 1].End, close, MatchNewlines(text, added.ToString()));
                return true;
            }
            added.Append('\n').Append(IndentOfLine(text, vs));
            text = string.Concat(text.AsSpan(0, vs + 1), MatchNewlines(text, added.ToString()), text.AsSpan(vs + 1));
            return true;
        }

        // A cut starting or ending at j moves past a comment that finishes j's line, so the comment goes
        // with the element on that line: kept for the element before a cut, removed with a cut element.
        private static int PastLineComment(string text, int j, int limit)
        {
            while (CommentOnLine(text, j, limit) is CToken comment)
            {
                if (IsLineComment(text, comment)) return LineCommentEnd(text, comment);
                bool closed = comment.Length >= 4 && text[comment.End - 2] == '*' && text[comment.End - 1] == '/';
                if (!closed || text.IndexOf('\n', comment.Start, comment.Length) >= 0) break;
                j = comment.End;
            }
            return j;
        }

        // The comment that comes next on pos's line with only spaces or tabs before it, or null.
        private static CToken? CommentOnLine(string text, int pos, int limit)
        {
            if (CLexer.FirstToken(text, pos, limit, keepComments: true) is not CToken next || next.Kind != CTokenKind.Comment) return null;
            for (int i = pos; i < next.Start; i++)
                if (text[i] is not (' ' or '\t')) return null;
            return next;
        }

        private static bool IsLineComment(string text, CToken comment) => text[comment.Start + 1] == '/';

        // A // comment ends before any \r.
        private static int LineCommentEnd(string text, CToken comment) =>
            comment.End > comment.Start && text[comment.End - 1] == '\r' ? comment.End - 1 : comment.End;

        // Adds the comma the last element lacks right after its value, and the new lines after any
        // comment on that line, so the comment stays with the element it describes.
        internal static void InsertAfterLastElement(ref string text, int valueEnd, int limit, string lines)
        {
            int afterComma = CLexer.PastComma(text, valueEnd, limit);
            string comma = afterComma == valueEnd ? "," : "";
            int insertAt = CommentOnLine(text, afterComma, limit) is CToken comment && IsLineComment(text, comment)
                ? LineCommentEnd(text, comment) : afterComma;
            text = text.Substring(0, valueEnd) + comma + text.Substring(valueEnd, insertAt - valueEnd) + lines + text.Substring(insertAt);
        }

        /// <summary>
        /// Removes one list element from <c>start</c> to <c>end</c>. An element alone on its line takes the line with it,
        /// with any comment after it. One sharing a line with others leaves them and their comments in place.
        /// </summary>
        internal static string RemoveElement(string text, int start, int end)
        {
            (int cutStart, int cutEnd) = RemovalSpan(text, start, end);
            return text.Remove(cutStart, cutEnd - cutStart);
        }

        /// <summary>The span <see cref="RemoveElement"/> cuts.</summary>
        internal static (int Start, int End) RemovalSpan(string text, int start, int end)
        {
            int line = LineStart(text, start), lineEnd = LineEnd(text, end);
            int rest = CLexer.PastComma(text, end, lineEnd);
            while (rest < lineEnd && (text[rest] == ' ' || text[rest] == '\t' || text[rest] == '\r')) rest++;
            bool commentFollows = CLexer.FirstToken(text, rest, lineEnd, keepComments: true) is CToken next
                && next.Kind == CTokenKind.Comment && next.Start == rest;
            if (string.IsNullOrWhiteSpace(text.Substring(line, start - line)) && (rest == lineEnd || commentFollows))
                return (line, Math.Min(text.Length, lineEnd + 1));
            return (start, rest);
        }

        // A CRLF file must not gain bare LF lines.
        private static string MatchNewlines(string text, string inserted)
        {
            string lf = inserted.Replace("\r\n", "\n");
            return text.Contains("\r\n") ? lf.Replace("\n", "\r\n") : lf;
        }


        /// <summary>The indent of the line <paramref name="at"/> starts, or four spaces when code comes before it.</summary>
        internal static string Indent(string text, int at)
        {
            int start = LineStart(text, at);
            return text.Substring(start, at - start).All(char.IsWhiteSpace) ? text.Substring(start, at - start) : "    ";
        }

        internal static int LineStart(string text, int at) => text.LastIndexOf('\n', Math.Max(0, at - 1)) + 1;

        internal static int LineEnd(string text, int at)
        {
            int n = text.IndexOf('\n', at);
            return n < 0 ? text.Length : n;
        }

        /// <summary>The spaces and tabs that start the line <paramref name="pos"/> is on.</summary>
        internal static string IndentOfLine(string text, int pos)
        {
            int lineStart = LineStart(text, pos);
            int i = lineStart;
            while (i < pos && (text[i] == ' ' || text[i] == '\t')) i++;
            return text.Substring(lineStart, i - lineStart);
        }

        /// <summary>Reads a field's raw value directly from an already-isolated "{ ... }" block (e.g. one
        /// element returned by <see cref="SplitArrayValue"/>), with no `[designator] = ` prefix to locate first.</summary>
        public static bool TryGetFieldValueInBlock(string block, IReadOnlyList<FieldPathSegment> path, out string rawValue)
        {
            rawValue = null;
            if (string.IsNullOrEmpty(block) || block[0] != '{') return false;
            if (!BraceScanner.TryFindMatchingBrace(block, 0, out int close)) return false;
            if (!ElementScanner.TryLocateValueSpan(block, 0, close, path, out int vs, out int ve)) return false;
            rawValue = block.Substring(vs, ve - vs).Trim();
            return true;
        }

        /// <summary>Splits an already-isolated "{ ... }" array-field value into its raw top-level element
        /// substrings, respecting nested braces/parens/brackets and string/char literals.</summary>
        public static List<string> SplitArrayValue(string arrayFieldValue)
        {
            List<string> result = new List<string>();
            if (string.IsNullOrEmpty(arrayFieldValue) || arrayFieldValue[0] != '{') return result;
            if (!BraceScanner.TryFindMatchingBrace(arrayFieldValue, 0, out int close)) return result;
            return ElementScanner.SplitElementValues(arrayFieldValue, 0, close);
        }
    }

    /// <summary>Finds the brace matching an opening '{' through the syntax reader, so braces in quoted game text
    /// (e.g. trainer messages), comments and preprocessor lines never confuse the match.</summary>
    internal static class BraceScanner
    {
        public static bool TryFindMatchingBrace(string text, int openIndex, out int closeIndex)
        {
            closeIndex = -1;
            if (text == null || openIndex < 0 || openIndex >= text.Length || text[openIndex] != '{') return false;
            // An entry is a few KB in files of megabytes that edits rewrite one field at a time, so only a window from the
            // brace on is read, doubled until the match is inside it. Tokens before the match read the same as in the whole file.
            for (long window = 4096; ; window *= 2)
            {
                int end = (int)Math.Min(text.Length, openIndex + window);
                closeIndex = new CSourceFile(text, openIndex, end).MatchingBrace(openIndex);
                if (closeIndex >= 0 || end == text.Length) return closeIndex >= 0;
            }
        }
    }

    /// <summary>Splits a { ... } span into its top-level, comma-separated elements (ignoring commas
    /// nested inside sub-braces/parens/brackets or string/char literals), and descends a field path
    /// through those elements by designator name (.field = ...) or plain position ([i] or undesignated).</summary>
    internal static class ElementScanner
    {
        private readonly struct Element
        {
            public readonly string DesignatorName;   // null if positional/indexed-only
            public readonly int Index;                // resolved positional index
            public readonly int ValueStart, ValueEnd; // span of the value text (after any "= ")
            public Element(string name, int index, int vs, int ve) { DesignatorName = name; Index = index; ValueStart = vs; ValueEnd = ve; }
        }

        public static bool TryLocateValueSpan(string text, int openBrace, int closeBrace, IReadOnlyList<FieldPathSegment> path, out int valueStart, out int valueEnd)
        {
            int open = openBrace, close = closeBrace;
            for (int segIdx = 0; segIdx < path.Count; segIdx++)
            {
                List<Element> elements = Split(text, open, close);
                if (!TryFind(elements, path[segIdx], out Element match))
                {
                    valueStart = valueEnd = -1;
                    return false;
                }

                bool isLast = segIdx == path.Count - 1;
                if (isLast)
                {
                    valueStart = match.ValueStart;
                    valueEnd = match.ValueEnd;
                    return true;
                }

                int vs = match.ValueStart, ve = match.ValueEnd;
                while (vs < ve && char.IsWhiteSpace(text[vs])) vs++;
                if (vs >= ve || text[vs] != '{')
                {
                    valueStart = valueEnd = -1;
                    return false;   // expected a nested struct here, found a scalar instead
                }
                if (!BraceScanner.TryFindMatchingBrace(text, vs, out int innerClose) || innerClose >= ve)
                {
                    valueStart = valueEnd = -1;
                    return false;
                }
                open = vs; close = innerClose;
            }
            valueStart = valueEnd = -1;
            return false;
        }

        /// <summary>Walks every segment of <paramref name="path"/> except the last, returning the brace
        /// span of the block the final segment would be a direct field of, even when absent.</summary>
        public static bool TryLocateParentBlock(string text, int openBrace, int closeBrace, IReadOnlyList<FieldPathSegment> path, out int parentOpen, out int parentClose)
        {
            int open = openBrace, close = closeBrace;
            for (int segIdx = 0; segIdx < path.Count - 1; segIdx++)
            {
                List<Element> elements = Split(text, open, close);
                if (!TryFind(elements, path[segIdx], out Element match))
                {
                    parentOpen = parentClose = -1;
                    return false;
                }

                int vs = match.ValueStart, ve = match.ValueEnd;
                while (vs < ve && char.IsWhiteSpace(text[vs])) vs++;
                if (vs >= ve || text[vs] != '{')
                {
                    parentOpen = parentClose = -1;
                    return false;
                }
                if (!BraceScanner.TryFindMatchingBrace(text, vs, out int innerClose) || innerClose >= ve)
                {
                    parentOpen = parentClose = -1;
                    return false;
                }
                open = vs; close = innerClose;
            }
            parentOpen = open;
            parentClose = close;
            return true;
        }

        /// <summary>Where each top-level element starts (designator included) and where its value ends.</summary>
        internal static List<(int Start, int End)> ElementSpans(string text, int openBrace, int closeBrace)
        {
            CInitList list = new CSourceFile(text, openBrace, closeBrace + 1).ListAt(openBrace);
            List<(int, int)> spans = new List<(int, int)>();
            if (list != null) foreach (CInitItem item in list.Items) spans.Add((item.Start, item.End));
            return spans;
        }

        /// <summary>Wraps <see cref="Split"/> for callers that just need each element's raw value text.</summary>
        internal static List<string> SplitElementValues(string text, int openBrace, int closeBrace)
        {
            List<Element> elements = Split(text, openBrace, closeBrace);
            List<string> result = new List<string>(elements.Count);
            foreach (Element e in elements) result.Add(text.Substring(e.ValueStart, e.ValueEnd - e.ValueStart));
            return result;
        }

        private static bool TryFind(List<Element> elements, FieldPathSegment seg, out Element match)
        {
            foreach (Element e in elements)
            {
                if (seg.IsIndex ? e.Index == seg.Index : e.DesignatorName == seg.Name)
                {
                    match = e;
                    return true;
                }
            }
            match = default;
            return false;
        }

        // The syntax reader splits the list (directives, strings and comments are not code to it). A single .field or
        // numeric [N] designator names or numbers the element; anything else, a symbolic [NAME] included, counts by position.
        private static List<Element> Split(string text, int openBrace, int closeBrace)
        {
            List<Element> result = new List<Element>();
            CInitList list = new CSourceFile(text, openBrace, closeBrace + 1).ListAt(openBrace);
            if (list == null) return result;
            int autoIndex = 0;
            foreach (CInitItem item in list.Items)
            {
                string name = null;
                int index = autoIndex;
                if (item.Designators.Count == 1 && item.Designators[0].IsField) name = item.Designators[0].Text;
                else if (item.Designators.Count == 1 && item.Position >= 0) index = item.Position;
                result.Add(new Element(name, index, item.ValueStart, item.ValueEnd));
                autoIndex = index + 1;
            }
            return result;
        }
    }
}
