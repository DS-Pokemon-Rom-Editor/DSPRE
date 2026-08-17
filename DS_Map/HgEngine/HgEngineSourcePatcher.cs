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
            text = string.Concat(text.AsSpan(0, vs), newValueLiteral, text.AsSpan(ve));
            return true;
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

            // Only add a leading comma if the prior field doesn't already end with one.
            int checkPos = parentClose - 1;
            while (checkPos > parentOpen && char.IsWhiteSpace(text[checkPos])) checkPos--;
            bool needsLeadingComma = checkPos > parentOpen && text[checkPos] != ',';
            string newField = (needsLeadingComma ? "," : "") + $"\n            .{path[^1].Name} = {newValueLiteral},";
            text = string.Concat(text.AsSpan(0, parentClose), newField, text.AsSpan(parentClose));
            return true;
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
                while (e > s && char.IsWhiteSpace(text[e - 1])) e--;
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
