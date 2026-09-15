using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

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

    /// <summary>Locates and replaces exactly one field's value inside a C designated-initializer array
    /// entry, without touching the rest of the file. A field that can't be located fails to match and
    /// nothing is written, rather than guessing where to insert it.</summary>
    public static class HgEngineSourcePatcher
    {
        /// <summary>Finds "[designatorToken] = { ... }" and returns the span of its braces (inclusive).</summary>
        public static bool TryFindEntry(string text, string designatorToken, out int openBrace, out int closeBrace)
        {
            openBrace = closeBrace = -1;
            var m = Regex.Match(text, @"\[\s*" + Regex.Escape(designatorToken) + @"\s*\]\s*=\s*\{");
            if (!m.Success) return false;
            int brace = m.Index + m.Length - 1;
            if (!BraceScanner.TryFindMatchingBrace(text, brace, out int close)) return false;
            openBrace = brace; closeBrace = close;
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
        internal static bool SameTokens(string a, string b) => Tokens(a) == Tokens(b);

        private static string Tokens(string s)
        {
            var sb = new System.Text.StringBuilder(s.Length);
            int i = 0;
            while (i < s.Length)
            {
                char c = s[i];
                if (c == '/' && i + 1 < s.Length && (s[i + 1] == '/' || s[i + 1] == '*'))
                {
                    BraceScanner.SkipNonCode(s, ref i);
                    continue;
                }
                if (c == '"' || c == '\'')
                {
                    int start = i;
                    BraceScanner.SkipNonCode(s, ref i);
                    sb.Append(s, start, i - start);
                    continue;
                }
                if (!char.IsWhiteSpace(c))
                {
                    if ((c == '}' || c == ']') && sb.Length > 0 && sb[^1] == ',') sb.Length--;
                    sb.Append(c);
                }
                i++;
            }
            return sb.ToString();
        }

        /// <summary>Like <see cref="TryReplaceField"/>, but if the field isn't declared yet, INSERTS
        /// `.fieldName = value,` before its parent block's closing brace instead of failing. Every
        /// ancestor block in the path must already exist.</summary>
        public static bool TryUpsertField(ref string text, string designatorToken, IReadOnlyList<FieldPathSegment> path, string newValueLiteral)
        {
            if (TryReplaceField(ref text, designatorToken, path, newValueLiteral)) return true;
            if (path.Count == 0 || path[^1].IsIndex) return false;   // can't synthesize a designator-less positional element

            if (!TryFindEntry(text, designatorToken, out int open, out int close)) return false;
            if (!ElementScanner.TryLocateParentBlock(text, open, close, path, out int parentOpen, out int parentClose)) return false;

            var fields = ElementScanner.ElementSpans(text, parentOpen, parentClose);
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

            var fields = ElementScanner.ElementSpans(text, parentOpen, parentClose);
            var named = new Regex(@"\G\.\s*" + Regex.Escape(path[^1].Name) + @"\s*=(?!=)");
            for (int i = 0; i < fields.Count; i++)
            {
                if (!named.IsMatch(text, fields[i].Start)) continue;
                int cutStart = PastLineComment(text, i == 0 ? parentOpen + 1 : AfterComma(text, fields[i - 1].End, parentClose), parentClose);
                int cutEnd = PastLineComment(text, AfterComma(text, fields[i].End, parentClose), parentClose);
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
                var literal = new System.Text.StringBuilder("{");
                for (int i = 0; i < count; i++) literal.Append('\n').Append(indent).Append(newElement(i, indent)).Append(',');
                literal.Append('\n').Append(indent, 0, indent.Length - 4).Append('}');
                return TryUpsertField(ref text, designatorToken, arrayPath, literal.ToString());
            }

            while (vs < ve && char.IsWhiteSpace(text[vs])) vs++;
            if (vs >= ve || text[vs] != '{' || !BraceScanner.TryFindMatchingBrace(text, vs, out int close)) return false;

            var spans = ElementScanner.ElementSpans(text, vs, close);
            int n = spans.Count;
            if (count == n) return true;

            if (count < n)
            {
                int cutStart = PastLineComment(text, count == 0 ? vs + 1 : AfterComma(text, spans[count - 1].End, close), close);
                int cutEnd = PastLineComment(text, AfterComma(text, spans[n - 1].End, close), close);
                text = string.Concat(text.AsSpan(0, cutStart), text.AsSpan(cutEnd));
                return true;
            }

            string elementIndent = n > 0 ? IndentOfLine(text, spans[0].Start) : IndentOfLine(text, vs) + "    ";
            var added = new System.Text.StringBuilder();
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
            int k = j;
            while (k < limit && (text[k] == ' ' || text[k] == '\t')) k++;
            if (k + 1 >= limit || text[k] != '/') return j;
            if (text[k + 1] == '/')
            {
                int nl = text.IndexOf('\n', k);
                if (nl < 0 || nl > limit) return limit;
                return text[nl - 1] == '\r' ? nl - 1 : nl;
            }
            if (text[k + 1] == '*')
            {
                int end = text.IndexOf("*/", k + 2, StringComparison.Ordinal);
                int nl = text.IndexOf('\n', k);
                if (end >= 0 && end + 2 <= limit && (nl < 0 || end < nl)) return PastLineComment(text, end + 2, limit);
            }
            return j;
        }

        // Adds the comma the last element lacks right after its value, and the new lines after any
        // comment on that line, so the comment stays with the element it describes.
        internal static void InsertAfterLastElement(ref string text, int valueEnd, int limit, string lines)
        {
            int afterComma = AfterComma(text, valueEnd, limit);
            string comma = afterComma == valueEnd ? "," : "";
            int insertAt = afterComma;
            int i = afterComma;
            while (i < limit && (text[i] == ' ' || text[i] == '\t')) i++;
            if (i + 1 < limit && text[i] == '/' && text[i + 1] == '/')
            {
                int nl = text.IndexOf('\n', i);
                insertAt = nl < 0 || nl > limit ? limit : (nl > 0 && text[nl - 1] == '\r' ? nl - 1 : nl);
            }
            text = text.Substring(0, valueEnd) + comma + text.Substring(valueEnd, insertAt - valueEnd) + lines + text.Substring(insertAt);
        }

        // A CRLF file must not gain bare LF lines.
        private static string MatchNewlines(string text, string inserted)
        {
            string lf = inserted.Replace("\r\n", "\n");
            return text.Contains("\r\n") ? lf.Replace("\n", "\r\n") : lf;
        }

        // Past the comma after a value, even with a comment before it; the value's end when no comma follows.
        private static int AfterComma(string text, int pos, int limit)
        {
            int i = pos;
            while (i < limit)
            {
                if (char.IsWhiteSpace(text[i])) { i++; continue; }
                if (text[i] == '/' && i + 1 < limit && (text[i + 1] == '/' || text[i + 1] == '*')) { BraceScanner.SkipNonCode(text, ref i); continue; }
                break;
            }
            return i < limit && text[i] == ',' ? i + 1 : pos;
        }

        internal static string IndentOfLine(string text, int pos)
        {
            int lineStart = text.LastIndexOf('\n', Math.Max(0, pos - 1)) + 1;
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
            var result = new List<string>();
            if (string.IsNullOrEmpty(arrayFieldValue) || arrayFieldValue[0] != '{') return result;
            if (!BraceScanner.TryFindMatchingBrace(arrayFieldValue, 0, out int close)) return result;
            return ElementScanner.SplitElementValues(arrayFieldValue, 0, close);
        }
    }

    /// <summary>Finds the brace matching an opening '{', skipping over string/char literals and comments
    /// so braces mentioned inside quoted game text (e.g. trainer messages) never confuse the scan.</summary>
    internal static class BraceScanner
    {
        public static bool TryFindMatchingBrace(string text, int openIndex, out int closeIndex)
        {
            closeIndex = -1;
            if (openIndex < 0 || openIndex >= text.Length || text[openIndex] != '{') return false;
            int depth = 0;
            int i = openIndex;
            while (i < text.Length)
            {
                if (SkipNonCode(text, ref i)) continue;
                char c = text[i];
                if (c == '{') depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0) { closeIndex = i; return true; }
                }
                i++;
            }
            return false;
        }

        /// <summary>If the position at i starts a comment or string/char literal, advances i past it and
        /// returns true (caller should re-check from the new i). Otherwise leaves i untouched.</summary>
        internal static bool SkipNonCode(string text, ref int i)
        {
            char c = text[i];
            if (c == '/' && i + 1 < text.Length && text[i + 1] == '/')
            {
                int nl = text.IndexOf('\n', i);
                i = nl < 0 ? text.Length : nl + 1;
                return true;
            }
            if (c == '/' && i + 1 < text.Length && text[i + 1] == '*')
            {
                int end = text.IndexOf("*/", i + 2);
                i = end < 0 ? text.Length : end + 2;
                return true;
            }
            if (c == '"' || c == '\'')
            {
                char quote = c;
                int j = i + 1;
                while (j < text.Length && text[j] != quote)
                {
                    if (text[j] == '\\') j++;
                    j++;
                }
                i = j < text.Length ? j + 1 : text.Length;
                return true;
            }
            return false;
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
                var elements = Split(text, open, close);
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
                var elements = Split(text, open, close);
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
            var spans = new List<(int, int)>();
            int i = openBrace + 1, elemStart = i, depth = 0;

            void Flush(int elemEnd)
            {
                int s = elemStart, e = elemEnd;
                while (s < e && char.IsWhiteSpace(text[s])) s++;
                SkipLeadingComments(text, ref s, e);
                TrimTrailingComments(text, s, ref e);
                if (s < e) spans.Add((s, e));
            }

            while (i < closeBrace)
            {
                if (BraceScanner.SkipNonCode(text, ref i)) continue;
                char c = text[i];
                if (c == '{' || c == '(' || c == '[') depth++;
                else if (c == '}' || c == ')' || c == ']') depth--;
                else if (c == ',' && depth == 0) { Flush(i); elemStart = i + 1; }
                i++;
            }
            Flush(closeBrace);
            return spans;
        }

        /// <summary>Wraps <see cref="Split"/> for callers that just need each element's raw value text.</summary>
        internal static List<string> SplitElementValues(string text, int openBrace, int closeBrace)
        {
            var elements = Split(text, openBrace, closeBrace);
            var result = new List<string>(elements.Count);
            foreach (var e in elements) result.Add(text.Substring(e.ValueStart, e.ValueEnd - e.ValueStart));
            return result;
        }

        private static bool TryFind(List<Element> elements, FieldPathSegment seg, out Element match)
        {
            foreach (var e in elements)
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

        private static List<Element> Split(string text, int openBrace, int closeBrace)
        {
            var result = new List<Element>();
            int i = openBrace + 1;
            int elemStart = i;
            int depth = 0;
            int autoIndex = 0;

            void Flush(int elemEnd)
            {
                int s = elemStart, e = elemEnd;
                while (s < e && char.IsWhiteSpace(text[s])) s++;
                SkipLeadingComments(text, ref s, e);
                TrimTrailingComments(text, s, ref e);
                if (s >= e) return;   // empty (trailing comma before closing brace, or a trailing comment with no value after it)

                var (name, idx, valueStart) = ParseDesignator(text, s, e, autoIndex);
                result.Add(new Element(name, idx, valueStart, e));
                autoIndex = idx + 1;
            }

            while (i < closeBrace)
            {
                if (BraceScanner.SkipNonCode(text, ref i)) continue;
                char c = text[i];
                if (c == '{' || c == '(' || c == '[') { depth++; i++; continue; }
                if (c == '}' || c == ')' || c == ']') { depth--; i++; continue; }
                if (c == ',' && depth == 0)
                {
                    Flush(i);
                    elemStart = i + 1;
                    i++;
                    continue;
                }
                i++;
            }
            Flush(closeBrace);
            return result;
        }

        /// <summary>Parses an element's leading designator, if any: ".name =" (named) or "[N] =" (indexed).
        /// Returns the field name (or null for positional), the resolved index, and where the value starts.</summary>
        private static (string name, int index, int valueStart) ParseDesignator(string text, int start, int end, int autoIndex)
        {
            int i = start;
            if (i < end && text[i] == '.')
            {
                int nameStart = ++i;
                while (i < end && (char.IsLetterOrDigit(text[i]) || text[i] == '_')) i++;
                string name = text.Substring(nameStart, i - nameStart);
                int j = i;
                while (j < end && char.IsWhiteSpace(text[j])) j++;
                if (j < end && text[j] == '=' && (j + 1 >= end || text[j + 1] != '='))
                    return (name, autoIndex, SkipWhitespace(text, j + 1, end));
                // ".name" without "=" isn't a field designator DSPRE understands here; treat as positional.
                return (null, autoIndex, start);
            }
            if (i < end && text[i] == '[')
            {
                int numStart = ++i;
                while (i < end && text[i] != ']') i++;
                if (i < end && int.TryParse(text.Substring(numStart, i - numStart).Trim(), out int idx))
                {
                    int j = i + 1;
                    while (j < end && char.IsWhiteSpace(text[j])) j++;
                    if (j < end && text[j] == '=' && (j + 1 >= end || text[j + 1] != '='))
                        return (null, idx, SkipWhitespace(text, j + 1, end));
                }
            }
            return (null, autoIndex, start);
        }

        private static int SkipWhitespace(string text, int i, int end)
        {
            while (i < end && char.IsWhiteSpace(text[i])) i++;
            return i;
        }

        // A comma-delimited entry can be preceded by its own trailing comment ("}, // Location\n    { ...",
        // the comment belongs to the PRIOR entry but sits before this one since there's no comma between
        // them), which BraceScanner.SkipNonCode only protects depth-tracking from, it doesn't advance where
        // an element starts. Skip any run of leading "//"/"/* */" comments (and the whitespace around them)
        // so they never end up prepended to the next real value.
        // The last element before '}' has no comma, so a "// note" after it would otherwise count as its value.
        private static void TrimTrailingComments(string text, int s, ref int e)
        {
            int i = s, lastCode = s - 1;
            while (i < e)
            {
                char c = text[i];
                bool comment = c == '/' && i + 1 < e && (text[i + 1] == '/' || text[i + 1] == '*');
                if (comment || c == '"' || c == '\'')
                {
                    int j = i;
                    BraceScanner.SkipNonCode(text, ref j);
                    j = Math.Min(j, e);
                    if (!comment) lastCode = j - 1;
                    i = j;
                    continue;
                }
                if (!char.IsWhiteSpace(c)) lastCode = i;
                i++;
            }
            e = lastCode + 1;
        }

        private static void SkipLeadingComments(string text, ref int s, int end)
        {
            while (s < end && text[s] == '/' && s + 1 < end && (text[s + 1] == '/' || text[s + 1] == '*'))
            {
                if (text[s + 1] == '/')
                {
                    int nl = text.IndexOf('\n', s);
                    s = (nl < 0 || nl >= end) ? end : nl + 1;
                }
                else
                {
                    int close = text.IndexOf("*/", s + 2, System.StringComparison.Ordinal);
                    s = (close < 0 || close + 2 > end) ? end : close + 2;
                }
                while (s < end && char.IsWhiteSpace(text[s])) s++;
            }
        }
    }
}
