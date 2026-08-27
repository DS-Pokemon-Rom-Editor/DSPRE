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

        /// <summary>Formats a plain string as a quoted C string literal, escaping `\` and `"` only.</summary>
        public static string ToCStringLiteral(string value)
        {
            var sb = new StringBuilder(value.Length + 2);
            sb.Append('"');
            foreach (char c in value)
            {
                if (c == '\\' || c == '"') sb.Append('\\');
                sb.Append(c);
            }
            sb.Append('"');
            return sb.ToString();
        }
    }
}
