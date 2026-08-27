using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace DSPRE.HgEngine
{
    /// <summary>Parses a hg-engine C header's constant declarations into name/value maps: both plain
    /// `#define NAME 123` and C enum `NAME = 123,` styles, resolving references between #defines
    /// (e.g. "(SPECIES_MEGA_START + 1)") recursively rather than assuming a plain literal.</summary>
    public sealed class HgEngineSymbolTable
    {
        public IReadOnlyDictionary<string, int> ByName { get; }
        public IReadOnlyDictionary<int, string> ByValue { get; }

        private HgEngineSymbolTable(Dictionary<string, int> byName, Dictionary<int, string> byValue)
        {
            ByName = byName;
            ByValue = byValue;
        }

        public bool TryGetName(int value, out string name) => ByValue.TryGetValue(value, out name);
        public bool TryGetValue(string name, out int value) => ByName.TryGetValue(name, out value);

        /// <summary>Reverse lookup restricted to names starting with <paramref name="prefix"/>, needed
        /// for headers that pack multiple constant families into one flat namespace (e.g. item.h's
        /// ITEM_*/POCKET_*/BATTLE_POCKET_* sharing the 0..N range). Picks the shortest matching name to
        /// prefer a family's base constant over its version-suffixed aliases.</summary>
        public bool TryGetNameWithPrefix(int value, string prefix, out string name)
        {
            name = null;
            string adminFallback = null;
            foreach (var kv in ByName)
            {
                if (kv.Value != value || !kv.Key.StartsWith(prefix, StringComparison.Ordinal)) continue;
                // A "_START"/"_MAX"/etc. range marker often aliases the same value as the real entry
                // right after it; prefer a non-administrative name, only fall back to the marker.
                if (IsAdministrativeName(kv.Key))
                {
                    if (adminFallback == null || kv.Key.Length < adminFallback.Length) adminFallback = kv.Key;
                    continue;
                }
                if (name == null || kv.Key.Length < name.Length) name = kv.Key;
            }
            name ??= adminFallback;
            return name != null;
        }

        /// <summary>Reverse lookup for bit-flag fields written as an OR expression (e.g. "FLAG_CONTACT |
        /// FLAG_PROTECT"). Decomposes the value into single-bit named constants and joins with " | ".
        /// Fails if some bits aren't covered by any single-bit name in the family.</summary>
        public bool TryGetFlagsExpression(int value, string prefix, out string expression)
        {
            expression = null;
            if (value == 0) return TryGetNameWithPrefix(0, prefix, out expression);

            var bitNames = new SortedDictionary<int, string>();
            foreach (var kv in ByName)
            {
                if (!kv.Key.StartsWith(prefix, StringComparison.Ordinal)) continue;
                int v = kv.Value;
                if (v == 0 || (v & (v - 1)) != 0) continue;   // skip 0 and non-single-bit values
                if (!bitNames.TryGetValue(v, out string existing) || kv.Key.Length < existing.Length)
                    bitNames[v] = kv.Key;
            }

            int remaining = value;
            var parts = new List<string>();
            foreach (var (bit, name) in bitNames)
            {
                if ((remaining & bit) == 0) continue;
                parts.Add(name);
                remaining &= ~bit;
            }
            if (remaining != 0 || parts.Count == 0) return false;
            expression = string.Join(" | ", parts);
            return true;
        }

        // Cached per header for the link session. HgEngineSpeciesExpansion/HgEngineItemExpansion/
        // HgEngineMoveExpansion do write new #defines into these headers, so they call ClearCache()
        // after writing.
        private static readonly Dictionary<string, HgEngineSymbolTable> _cache = new(StringComparer.OrdinalIgnoreCase);

        internal static void ClearCache() => _cache.Clear();

        /// <summary>Loads and fully resolves every constant declared in a header file (relative to the
        /// linked checkout's root, e.g. "include/constants/species.h"). Returns null if the file, or the
        /// checkout itself, isn't available.</summary>
        public static HgEngineSymbolTable Load(string headerRelPath)
        {
            if (!HgEngineProject.IsLinked) return null;
            if (_cache.TryGetValue(headerRelPath, out var cached)) return cached;

            string path = Path.Combine(HgEngineProject.RepoPathUnc, headerRelPath.Replace('/', '\\'));
            if (!File.Exists(path)) return null;
            var table = Parse(File.ReadAllText(path));
            _cache[headerRelPath] = table;
            return table;
        }

        internal static HgEngineSymbolTable Parse(string text)
        {
            // Strip "//" line comments first: a commented-out "// #define X (Y + 1)" is dead code, but
            // the #define regex below has no notion of comments and would parse it as real.
            text = StripLineComments(text);

            var rawExpr = new Dictionary<string, string>(StringComparer.Ordinal);

            // [ \t]+ (not \s+): a value-less header guard must not match, or \s+ would cross the
            // newline and swallow the next line's real #define as this one's value.
            foreach (Match m in Regex.Matches(text, @"#define[ \t]+([A-Za-z_]\w*)[ \t]+(.+)"))
                rawExpr[m.Groups[1].Value] = StripComment(m.Groups[2].Value);

            foreach (Match m in Regex.Matches(text, @"\b([A-Za-z_]\w*)\s*=\s*(-?\d+)\s*,"))
                rawExpr.TryAdd(m.Groups[1].Value, m.Groups[2].Value);   // enum: only plain literals seen so far

            ResolveImplicitEnumMembers(text, rawExpr);

            var cache = new Dictionary<string, int>(StringComparer.Ordinal);
            var byName = new Dictionary<string, int>(StringComparer.Ordinal);
            var byValue = new Dictionary<int, string>();

            var resolved = new List<(string name, int value)>();
            foreach (var name in rawExpr.Keys)
            {
                if (!TryResolve(name, rawExpr, cache, 0, out int value)) continue;
                byName[name] = value;
                resolved.Add((name, value));
            }

            // Administrative markers (SPECIES_MEGA_START, NUM_OF_FAKEMONS, ...) often alias the same
            // value as the real entity right after them; only win the tie if nothing else claims it.
            foreach (var (name, value) in resolved)
                if (!IsAdministrativeName(name)) byValue.TryAdd(value, name);
            foreach (var (name, value) in resolved)
                byValue.TryAdd(value, name);

            return new HgEngineSymbolTable(byName, byValue);
        }

        private static bool IsAdministrativeName(string name) =>
            name.EndsWith("_START", StringComparison.Ordinal) || name.EndsWith("_END", StringComparison.Ordinal) ||
            name.Contains("_MAX_", StringComparison.Ordinal) || name.EndsWith("_MAX", StringComparison.Ordinal) ||
            name.Contains("_NUM_", StringComparison.Ordinal) || name.EndsWith("_NUM", StringComparison.Ordinal) ||
            name.StartsWith("NUM_", StringComparison.Ordinal);

        private static string StripComment(string expr)
        {
            int i = expr.IndexOf("//", StringComparison.Ordinal);
            return (i >= 0 ? expr[..i] : expr).Trim();
        }

        /// <summary>Removes every "// ..." run through end-of-line from the whole text, preserving line
        /// breaks (nothing here depends on column position, but keeping line counts intact is cheap and
        /// avoids surprises if that ever changes).</summary>
        private static string StripLineComments(string text)
        {
            var sb = new System.Text.StringBuilder(text.Length);
            int i = 0;
            while (i < text.Length)
            {
                if (text[i] == '/' && i + 1 < text.Length && text[i + 1] == '/')
                {
                    while (i < text.Length && text[i] != '\n') i++;
                    continue;
                }
                sb.Append(text[i]);
                i++;
            }
            return sb.ToString();
        }

        /// <summary>Fills in enum members with no explicit "= N" (e.g. only "EVO_NONE = 0" is pinned,
        /// the rest auto-increment), which the two regexes above miss entirely. A member whose explicit
        /// value isn't a plain literal stops auto-numbering for the rest of that enum.</summary>
        private static void ResolveImplicitEnumMembers(string text, Dictionary<string, string> rawExpr)
        {
            foreach (Match block in Regex.Matches(text, @"enum\s*\w*\s*\{([^{}]*)\}"))
            {
                int current = 0;
                bool known = true;
                foreach (string member in SplitTopLevelCommas(block.Groups[1].Value))
                {
                    string trimmed = member.Trim();
                    if (trimmed.Length == 0) continue;
                    var m = Regex.Match(trimmed, @"^([A-Za-z_]\w*)\s*(?:=\s*(.+))?$", RegexOptions.Singleline);
                    if (!m.Success) { known = false; continue; }

                    string name = m.Groups[1].Value;
                    if (m.Groups[2].Success)
                    {
                        known = TryParseLiteral(m.Groups[2].Value.Trim(), out int lit);
                        if (known) current = lit;
                    }

                    if (known)
                    {
                        rawExpr.TryAdd(name, current.ToString());
                        current++;
                    }
                }
            }
        }

        /// <summary>Splits on top-level commas only, so a member's own "(1 &lt;&lt; 3)"-style value
        /// expression can't be mistaken for a member separator.</summary>
        private static List<string> SplitTopLevelCommas(string body)
        {
            var result = new List<string>();
            int depth = 0, start = 0;
            for (int i = 0; i < body.Length; i++)
            {
                char c = body[i];
                if (c == '(' || c == '[') depth++;
                else if (c == ')' || c == ']') depth--;
                else if (c == ',' && depth == 0)
                {
                    result.Add(body.Substring(start, i - start));
                    start = i + 1;
                }
            }
            if (start < body.Length) result.Add(body.Substring(start));
            return result;
        }

        /// <summary>Resolves a #define's value: a literal, a bare "(NAME)", or "(A + B)"/"(A - B)" where
        /// A/B are each a literal or another #define name, resolved recursively. Bounded depth guards
        /// against reference cycles.</summary>
        private static bool TryResolve(string name, Dictionary<string, string> rawExpr, Dictionary<string, int> cache, int depth, out int value)
        {
            value = 0;
            if (cache.TryGetValue(name, out value)) return true;
            if (depth > 25 || !rawExpr.TryGetValue(name, out string expr)) return false;
            if (!TryResolveExpr(expr, rawExpr, cache, depth, out value)) return false;
            cache[name] = value;
            return true;
        }

        private static bool TryResolveExpr(string expr, Dictionary<string, string> rawExpr, Dictionary<string, int> cache, int depth, out int value)
        {
            value = 0;
            expr = expr.Trim();
            if (expr.StartsWith("(", StringComparison.Ordinal) && expr.EndsWith(")", StringComparison.Ordinal))
                expr = expr[1..^1].Trim();

            // hg-engine declares bit-flag families as shift expressions (e.g. "(1 << 13)"), not just +/-.
            const string operand = @"[A-Za-z_]\w*|-?0[xX][0-9a-fA-F]+|-?\d+";
            var m = Regex.Match(expr, $@"^({operand})\s*(?:(<<|>>|[+-])\s*({operand}))?$");
            if (!m.Success) return false;

            if (!TryResolveOperand(m.Groups[1].Value, rawExpr, cache, depth, out int left)) return false;
            if (!m.Groups[2].Success) { value = left; return true; }

            if (!TryResolveOperand(m.Groups[3].Value, rawExpr, cache, depth, out int right)) return false;
            value = m.Groups[2].Value switch
            {
                "+" => left + right,
                "-" => left - right,
                "<<" => left << right,
                ">>" => left >> right,
                _ => 0
            };
            return true;
        }

        // hg-engine mixes decimal and hex ("0x01") literals across headers; plain int.TryParse rejects hex.
        private static bool TryParseLiteral(string token, out int value)
        {
            bool negative = token.StartsWith("-", StringComparison.Ordinal);
            string unsigned = negative ? token[1..] : token;
            if (unsigned.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                if (!int.TryParse(unsigned[2..], System.Globalization.NumberStyles.HexNumber,
                        System.Globalization.CultureInfo.InvariantCulture, out value))
                    return false;
                if (negative) value = -value;
                return true;
            }
            return int.TryParse(token, out value);
        }

        private static bool TryResolveOperand(string token, Dictionary<string, string> rawExpr, Dictionary<string, int> cache, int depth, out int value)
            => TryParseLiteral(token, out value) || TryResolve(token, rawExpr, cache, depth + 1, out value);
    }
}
