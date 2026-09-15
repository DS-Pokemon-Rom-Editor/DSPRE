using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace DSPRE.HgEngine
{
    /// <summary>Read-only, layout-agnostic typed access to one C designated-initializer block's fields.
    /// Built entirely on source text; a field not present in the block's current source just returns
    /// false, never guessed at.</summary>
    public readonly struct HgEngineSourceBlock
    {
        public string Raw { get; }
        public HgEngineSourceBlock(string raw) { Raw = raw ?? ""; }

        public bool TryGetRaw(IReadOnlyList<FieldPathSegment> path, out string raw) =>
            HgEngineSourcePatcher.TryGetFieldValueInBlock(Raw, path, out raw);

        /// <summary>Resolves a field to an int: a plain literal, or a symbolic constant looked up in
        /// <paramref name="headerRelPath"/>'s table if given.</summary>
        public bool TryGetSymbol(IReadOnlyList<FieldPathSegment> path, string headerRelPath, out int value)
        {
            value = 0;
            if (!TryGetRaw(path, out string raw)) return false;
            return TryResolveToken(raw, headerRelPath, out value);
        }

        /// <summary>Resolves a field against the first of several headers that defines its name.</summary>
        public bool TryGetSymbolIn(IReadOnlyList<FieldPathSegment> path, IReadOnlyList<string> headers, out int value)
        {
            value = 0;
            return TryGetRaw(path, out string raw) && TryResolveTokenIn(raw, headers, out value);
        }

        /// <summary>An OR of names, each resolved against several headers. Fails on any unknown term.</summary>
        public bool TryGetFlagsValueIn(IReadOnlyList<FieldPathSegment> path, IReadOnlyList<string> headers, out int value)
        {
            value = 0;
            if (!TryGetRaw(path, out string raw)) return false;
            foreach (string part in raw.Split('|'))
            {
                string token = part.Trim();
                if (token.Length == 0) continue;
                if (!TryResolveTokenIn(token, headers, out int term)) { value = 0; return false; }
                value |= term;
            }
            return true;
        }

        public static bool TryResolveTokenIn(string token, IReadOnlyList<string> headers, out int value)
        {
            if (TryResolveToken(token, (string)null, out value)) return true;
            if (headers != null)
                foreach (string header in headers)
                    if (TryResolveToken(token, header, out value)) return true;
            value = 0;
            return false;
        }

        /// <summary>Shorthand for <see cref="TryGetSymbol"/> with no header, for fields that are always plain literals.</summary>
        public bool TryGetInt(IReadOnlyList<FieldPathSegment> path, out int value) => TryGetSymbol(path, null, out value);

        /// <summary>Reads a quoted C string field, unescaping `\\` and `\"`. Other backslash escapes
        /// (the game's own `\n`/`\r` markup) are left as literal characters.</summary>
        public bool TryGetString(IReadOnlyList<FieldPathSegment> path, out string value)
        {
            value = null;
            if (!TryGetRaw(path, out string raw)) return false;
            value = UnquoteCString(raw);
            return value != null;
        }

        /// <summary>Resolves a flags-style field written as an OR-expression of names to its combined
        /// int value. Fails if any term can't be resolved, rather than guessing.</summary>
        public bool TryGetFlagsValue(IReadOnlyList<FieldPathSegment> path, string headerRelPath, out int value)
        {
            value = 0;
            if (!TryGetRaw(path, out string raw)) return false;

            int result = 0;
            foreach (string part in raw.Split('|'))
            {
                string token = part.Trim();
                if (token.Length == 0) continue;
                if (!TryResolveToken(token, headerRelPath, out int termValue)) return false;
                result |= termValue;
            }
            value = result;
            return true;
        }

        /// <summary>Returns however many elements are actually written in an array field's source text.</summary>
        public IReadOnlyList<HgEngineSourceBlock> GetArrayElements(IReadOnlyList<FieldPathSegment> path)
        {
            if (!TryGetRaw(path, out string raw)) return System.Array.Empty<HgEngineSourceBlock>();
            return HgEngineSourcePatcher.SplitArrayValue(raw).Select(e => new HgEngineSourceBlock(e)).ToList();
        }

        /// <summary>Resolves a raw token (a literal, or a symbolic constant if a header is given) to an
        /// int. Public so bare-scalar array elements (no `{ }` wrapper) can resolve directly.</summary>
        public static bool TryResolveToken(string token, string headerRelPath, out int value)
        {
            token = token.Trim();
            if (TryParseLiteral(token, out value)) return true;
            if (headerRelPath == null) return false;
            var table = HgEngineSymbolTable.Load(headerRelPath);
            return table != null && table.TryGetValue(token, out value);
        }

        // Mirrors HgEngineSymbolTable's literal parser: decimal or 0x-hex, optional leading '-'.
        private static bool TryParseLiteral(string token, out int value)
        {
            bool negative = token.StartsWith("-");
            string unsigned = negative ? token[1..] : token;
            if (unsigned.StartsWith("0x", System.StringComparison.OrdinalIgnoreCase))
            {
                if (!int.TryParse(unsigned[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value))
                    return false;
                if (negative) value = -value;
                return true;
            }
            return int.TryParse(token, out value);
        }

        private static string UnquoteCString(string raw)
        {
            raw = raw.Trim();
            if (raw.Length < 2 || raw[0] != '"' || raw[^1] != '"') return null;
            var sb = new StringBuilder(raw.Length - 2);
            for (int i = 1; i < raw.Length - 1; i++)
            {
                if (raw[i] == '\\' && i + 1 < raw.Length - 1 && (raw[i + 1] == '\\' || raw[i + 1] == '"'))
                {
                    sb.Append(raw[i + 1]);
                    i++;
                }
                else
                {
                    sb.Append(raw[i]);
                }
            }
            return sb.ToString();
        }
    }

    /// <summary>Loads one trainer's whole `[id] = { ... }` entry out of data/Trainers.c, and formats
    /// plain strings as escaped C-string literals for `.name`/`.text` fields.</summary>
    public static class HgEngineTrainerSource
    {
        private const string SourceRelPath = "data/Trainers.c";

        public static bool TryLoad(int trainerId, out HgEngineSourceBlock block, out string error)
        {
            block = default;
            error = null;
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout linked."; return false; }

            string path = Path.Combine(HgEngineProject.RepoPathUnc, SourceRelPath.Replace('/', '\\'));
            if (!File.Exists(path)) { error = $"Source file not found: {path}"; return false; }

            string text = HgEngineFileCache.GetText(path);
            if (!HgEngineSourcePatcher.TryFindEntry(text, trainerId.ToString(), out int open, out int close))
            { error = $"Trainer {trainerId} not found in Trainers.c."; return false; }

            block = new HgEngineSourceBlock(text.Substring(open, close - open + 1));
            return true;
        }

        private static readonly System.Text.RegularExpressions.Regex TextOrderStart =
            new(@"\bsTrainerTextOrder\s*\[\s*\]\s*=\s*\{");

        /// <summary>The trainers trainerdatagen builds messages for: only those listed in sTrainerTextOrder.</summary>
        public static bool TryReadTextOrder(out HashSet<int> listed, out string error)
        {
            listed = new HashSet<int>();
            if (!TryFindTextOrder(out _, out string text, out int open, out int close, out error)) return false;
            foreach (string token in ElementScanner.SplitElementValues(text, open, close))
            {
                if (!HgEngineSourceBlock.TryResolveToken(token, (string)null, out int id))
                { error = $"sTrainerTextOrder lists \"{token.Trim()}\", which is not a trainer number."; return false; }
                listed.Add(id);
            }
            return true;
        }

        /// <summary>Appends a trainer to sTrainerTextOrder when it is missing. Appending keeps every listed
        /// trainer's message numbers where they were.</summary>
        public static bool TryAddToTextOrder(int trainerId, out string error)
        {
            if (!TryReadTextOrder(out var listed, out error)) return false;
            if (listed.Contains(trainerId)) return true;
            TryFindTextOrder(out string path, out string text, out int open, out int close, out _);

            var spans = ElementScanner.ElementSpans(text, open, close);
            if (spans.Count > 0)
            {
                string line = "\n" + HgEngineSourcePatcher.IndentOfLine(text, spans[0].Start) + trainerId + ",";
                HgEngineSourcePatcher.InsertAfterLastElement(ref text, spans[^1].End, close, line);
            }
            else text = text.Insert(open + 1, "\n    " + trainerId + ",\n");

            HgEngineFileCache.WriteText(path, text);
            return true;
        }

        private static bool TryFindTextOrder(out string path, out string text, out int open, out int close, out string error)
        {
            text = null;
            open = close = -1;
            error = null;
            path = null;
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout is linked."; return false; }

            path = Path.Combine(HgEngineProject.RepoPathUnc, SourceRelPath.Replace('/', '\\'));
            if (!File.Exists(path)) { error = $"Source file not found: {path}"; return false; }

            text = HgEngineFileCache.GetText(path);
            var m = TextOrderStart.Match(text);
            if (!m.Success || !BraceScanner.TryFindMatchingBrace(text, m.Index + m.Length - 1, out close))
            { error = "sTrainerTextOrder was not found in Trainers.c."; return false; }
            open = m.Index + m.Length - 1;
            return true;
        }

        /// <summary>Every trainer entry from 0 up to the first missing id, in one pass over the file.</summary>
        public static List<HgEngineSourceBlock> LoadAll()
        {
            var blocks = new List<HgEngineSourceBlock>();
            if (!HgEngineProject.IsActive) return blocks;
            string path = Path.Combine(HgEngineProject.RepoPathUnc, SourceRelPath.Replace('/', '\\'));
            if (!File.Exists(path)) return blocks;

            string text = HgEngineFileCache.GetText(path);
            int pos = 0;
            for (int id = 0; ; id++)
            {
                var m = new System.Text.RegularExpressions.Regex(@"\[\s*" + id + @"\s*\]\s*=\s*\{").Match(text, pos);
                if (!m.Success) break;
                int open = m.Index + m.Length - 1;
                if (!BraceScanner.TryFindMatchingBrace(text, open, out int close)) break;
                blocks.Add(new HgEngineSourceBlock(text.Substring(open, close - open + 1)));
                pos = close + 1;
            }
            return blocks;
        }

        /// <summary>Reads a party species value: a name or number, <c>MON_WITH_FORM(species, form)</c>, or
        /// <c>species | (form &lt;&lt; 11)</c>. The form sits in the top five bits of the species word.</summary>
        public static bool TryParseSpecies(string raw, string speciesHeader, out int species, out int form)
        {
            species = form = 0;
            if (raw == null) return false;
            raw = raw.Trim();
            string speciesToken = raw, formToken = null;

            var macro = System.Text.RegularExpressions.Regex.Match(raw, @"^MON_WITH_FORM\s*\(\s*([^,]+?)\s*,\s*([^)]+?)\s*\)$");
            var shifted = System.Text.RegularExpressions.Regex.Match(raw, @"^\(?\s*([^|()]+?)\s*\|\s*\(\s*([^<()]+?)\s*<<\s*11\s*\)\s*\)?$");
            if (macro.Success) { speciesToken = macro.Groups[1].Value; formToken = macro.Groups[2].Value; }
            else if (shifted.Success) { speciesToken = shifted.Groups[1].Value; formToken = shifted.Groups[2].Value; }

            if (!HgEngineSourceBlock.TryResolveToken(speciesToken, speciesHeader, out int value)) return false;
            if (formToken != null && !HgEngineSourceBlock.TryResolveToken(formToken, null, out form)) return false;
            if (formToken == null && value > 0x7FF) form = value >> 11;
            species = value & 0x7FF;
            return true;
        }

        /// <summary>The species source value, wrapped in hg-engine's own macro when a form is set.</summary>
        public static string FormatSpecies(string speciesLiteral, int form) =>
            form > 0 ? $"MON_WITH_FORM({speciesLiteral}, {form})" : speciesLiteral;

        /// <summary>The build encodes only 0-9, A-Z and a-z and stops at 10 bytes.</summary>
        public static string ToEncodableNickname(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            var sb = new StringBuilder(10);
            foreach (char c in value)
            {
                if (sb.Length == 10) break;
                if (c is >= '0' and <= '9' or >= 'A' and <= 'Z' or >= 'a' and <= 'z') sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>Reads 0/1 style fields that the source may also spell TRUE or FALSE.</summary>
        public static bool TryParseBool(string raw, out bool value)
        {
            value = false;
            if (raw == null) return false;
            raw = raw.Trim();
            if (raw == "TRUE") { value = true; return true; }
            if (raw == "FALSE") return true;
            if (!HgEngineSourceBlock.TryResolveToken(raw, null, out int n)) return false;
            value = n != 0;
            return true;
        }

        /// <summary>Formats a plain string as a quoted C string literal, escaping `\` and `"` only.</summary>
        public static string ToCStringLiteral(string value)
        {
            var sb = new StringBuilder(value.Length + 2);
            sb.Append('"');
            foreach (char c in value)
            {
                // A typed line break becomes the game's \n markup; a raw newline would break the C literal.
                if (c == '\r') continue;
                if (c == '\n') { sb.Append(@"\\n"); continue; }
                if (c == '\\' || c == '"') sb.Append('\\');
                sb.Append(c);
            }
            sb.Append('"');
            return sb.ToString();
        }
    }
}
