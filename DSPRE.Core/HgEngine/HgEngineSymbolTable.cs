using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DSPRE.HgEngine
{
    /// <summary>Parses a hg-engine C header's constant declarations into name/value maps: both plain
    /// `#define NAME 123` and C enum `NAME = 123,` styles, resolving references between #defines
    /// (e.g. "(SPECIES_MEGA_START + 1)") recursively, not only plain literals.</summary>
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

        /// <summary>A number as written, or a name from <paramref name="table"/>; 0 when it is neither.</summary>
        public static int ValueOrZero(string token, HgEngineSymbolTable table) => TryValue(token, out int v, table) ? v : 0;

        /// <summary>A number as written (decimal or 0x hex), or a name from the first table that has it.</summary>
        public static bool TryValue(string token, out int value, params HgEngineSymbolTable[] tables)
        {
            value = 0;
            if (token == null) return false;
            token = token.Trim();
            if (CLexer.TryParseInt(token, out value)) return true;
            foreach (HgEngineSymbolTable t in tables)
                if (t != null && t.TryGetValue(token, out value)) return true;
            value = 0;
            return false;
        }

        /// <summary>Reverse lookup restricted to names starting with <paramref name="prefix"/>, needed
        /// for headers that pack multiple constant families into one flat namespace (e.g. item.h's
        /// ITEM_*/POCKET_*/BATTLE_POCKET_* sharing the 0..N range). Picks the shortest matching name to
        /// prefer a family's base constant over its version-suffixed aliases.</summary>
        public bool TryGetNameWithPrefix(int value, string prefix, out string name)
        {
            name = null;
            string adminFallback = null;
            foreach (KeyValuePair<string, int> kv in ByName)
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

            SortedDictionary<int, string> bitNames = new SortedDictionary<int, string>();
            foreach (KeyValuePair<string, int> kv in ByName)
            {
                if (!kv.Key.StartsWith(prefix, StringComparison.Ordinal)) continue;
                int v = kv.Value;
                if (v == 0 || (v & (v - 1)) != 0) continue;   // skip 0 and non-single-bit values
                if (!bitNames.TryGetValue(v, out string existing) || kv.Key.Length < existing.Length)
                    bitNames[v] = kv.Key;
            }

            int remaining = value;
            List<string> parts = new List<string>();
            foreach ((int bit, string name) in bitNames)
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
        private static readonly Dictionary<string, HgEngineSymbolTable> Cache = new(StringComparer.OrdinalIgnoreCase);
        private const string ConfigHeaderRelPath = "include/config.h";

        internal static void ClearCache() => Cache.Clear();

        /// <summary>Loads and fully resolves every constant declared in a header file (relative to the
        /// linked checkout's root, e.g. "include/constants/species.h"). Returns null if the file, or the
        /// checkout itself, isn't available.</summary>
        public static HgEngineSymbolTable Load(string headerRelPath)
        {
            if (!HgEngineProject.IsLinked) return null;
            if (Cache.TryGetValue(headerRelPath, out HgEngineSymbolTable cached)) return cached;

            string path = Path.Combine(HgEngineProject.RepoPathUnc, headerRelPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path)) return null;
            // #if conditions in other headers test config.h switches such as DISALLOW_DEXIT_GEN.
            IReadOnlyDictionary<string, int> config = headerRelPath == ConfigHeaderRelPath ? null : Load(ConfigHeaderRelPath)?.ByName;
            HgEngineSymbolTable table = Parse(File.ReadAllText(path), config);
            Cache[headerRelPath] = table;
            return table;
        }

        internal static HgEngineSymbolTable Parse(string text, IReadOnlyDictionary<string, int> outside = null)
        {
            Dictionary<string, string> rawExpr = new Dictionary<string, string>(StringComparer.Ordinal);
            List<CToken> code = CompiledTokens(text, outside, rawExpr);
            ReadEnums(text, code, rawExpr);

            Dictionary<string, int> cache = new Dictionary<string, int>(StringComparer.Ordinal);
            Dictionary<string, int> byName = new Dictionary<string, int>(StringComparer.Ordinal);
            Dictionary<int, string> byValue = new Dictionary<int, string>();

            List<(string name, int value)> resolved = new List<(string name, int value)>();
            foreach (string name in rawExpr.Keys)
            {
                if (!TryResolve(name, rawExpr, cache, 0, out int value)) continue;
                byName[name] = value;
                resolved.Add((name, value));
            }

            // Administrative markers (SPECIES_MEGA_START, NUM_OF_FAKEMONS, ...) often alias the same
            // value as the real entity right after them; only win the tie if nothing else claims it.
            foreach ((string name, int value) in resolved)
                if (!IsAdministrativeName(name)) byValue.TryAdd(value, name);
            foreach ((string name, int value) in resolved)
                byValue.TryAdd(value, name);

            return new HgEngineSymbolTable(byName, byValue);
        }

        /// <summary>The tokens the compiler would see, with the file's #defines and config.h's applied, and the file's
        /// value #defines added to <paramref name="rawExpr"/>. An #if this can't evaluate keeps every branch.</summary>
        private static List<CToken> CompiledTokens(string text, IReadOnlyDictionary<string, int> outside, Dictionary<string, string> rawExpr)
        {
            List<CToken> code = new List<CToken>();
            Dictionary<string, string> known = new Dictionary<string, string>(StringComparer.Ordinal);
            // Each open #if: whether its current branch is live, and whether an earlier branch already was.
            Stack<(bool live, bool taken, bool unknown)> stack = new Stack<(bool live, bool taken, bool unknown)>();
            bool Live() => stack.All(s => s.live);
            bool IsDefined(string name) => known.ContainsKey(name) || (outside?.ContainsKey(name) ?? false);

            foreach (CToken token in CLexer.Tokenize(text))
            {
                if (token.Kind != CTokenKind.Directive)
                {
                    if (Live()) code.Add(token);
                    continue;
                }

                CDirective directive = CDirective.Parse(text, token);
                switch (directive.Keyword)
                {
                    case "define":
                        if (!Live() || directive.Name == null || directive.Parameters != null) break;
                        known[directive.Name] = directive.Body.Length > 0 ? directive.Body : "1";
                        if (directive.Body.Length > 0) rawExpr[directive.Name] = directive.Body;
                        break;
                    case "undef":
                        if (!Live() || directive.Name == null) break;
                        known.Remove(directive.Name);
                        rawExpr.Remove(directive.Name);
                        break;
                    case "ifdef":
                    case "ifndef":
                        {
                            bool live = directive.Keyword == "ifdef" == IsDefined(directive.Name ?? "");
                            stack.Push((live, live, false));
                            break;
                        }
                    case "if":
                        {
                            bool? v = EvaluateCondition(directive.Rest, known, outside, IsDefined);
                            stack.Push(v == null ? (true, false, true) : (v.Value, v.Value, false));
                            break;
                        }
                    case "elif":
                        {
                            if (stack.Count == 0) break;
                            (bool live, bool taken, bool unknown) top = stack.Pop();
                            bool? v = top.unknown ? null : EvaluateCondition(directive.Rest, known, outside, IsDefined);
                            if (v == null) stack.Push((true, top.taken, true));
                            else stack.Push((!top.taken && v.Value, top.taken || v.Value, false));
                            break;
                        }
                    case "else":
                        {
                            if (stack.Count == 0) break;
                            (bool live, bool taken, bool unknown) top = stack.Pop();
                            stack.Push(top.unknown ? (true, true, true) : (!top.taken, true, false));
                            break;
                        }
                    case "endif":
                        if (stack.Count > 0) stack.Pop();
                        break;
                }
            }
            return code;
        }

        // An undefined name is 0, as in C. A defined name with no numeric value makes the condition unknown.
        private static bool? EvaluateCondition(string condition, Dictionary<string, string> known,
            IReadOnlyDictionary<string, int> outside, Func<string, bool> isDefined)
        {
            int? Value(string name, int depth)
            {
                if (depth > 25) return null;
                if (known.TryGetValue(name, out string raw))
                    return HgEngineSourceExpression.TryEvaluate(raw, n => Value(n, depth + 1), out int v) ? v : null;
                return outside != null && outside.TryGetValue(name, out int o) ? o : 0;
            }
            string expression = CConditions.ResolveDefined(condition, isDefined);
            return HgEngineSourceExpression.TryEvaluate(expression, n => Value(n, 0), out int result) ? result != 0 : null;
        }

        private static bool IsAdministrativeName(string name) =>
            name.EndsWith("_START", StringComparison.Ordinal) || name.EndsWith("_END", StringComparison.Ordinal) ||
            name.Contains("_MAX_", StringComparison.Ordinal) || name.EndsWith("_MAX", StringComparison.Ordinal) ||
            name.Contains("_NUM_", StringComparison.Ordinal) || name.EndsWith("_NUM", StringComparison.Ordinal) ||
            name.StartsWith("NUM_", StringComparison.Ordinal);

        /// <summary>Adds each enum member. One with no "= value" counts on from the member before it.
        /// A #define of the same name wins.</summary>
        private static void ReadEnums(string text, List<CToken> code, Dictionary<string, string> rawExpr)
        {
            bool IsPunct(int t, string p) => t < code.Count && code[t].Kind == CTokenKind.Punct && code[t].Is(text, p);

            for (int t = 0; t < code.Count; t++)
            {
                if (code[t].Kind != CTokenKind.Identifier || !code[t].Is(text, "enum")) continue;
                int k = t + 1;
                if (k < code.Count && code[k].Kind == CTokenKind.Identifier) k++;
                if (!IsPunct(k, "{")) continue;
                k++;

                string start = "0";
                int offset = 0;
                while (k < code.Count && code[k].Kind == CTokenKind.Identifier)
                {
                    string name = code[k++].Text(text);
                    if (IsPunct(k, "="))
                    {
                        int first = ++k;
                        k = CLexer.NextOutsideBrackets(text, code, k, code.Count, c => c.Kind == CTokenKind.Punct && (c.Is(text, ",") || c.Is(text, "}")));
                        if (k == first || k >= code.Count) break;
                        start = text.Substring(code[first].Start, code[k - 1].End - code[first].Start);
                        offset = 0;
                    }
                    rawExpr.TryAdd(name, offset == 0 ? start : $"({start}) + {offset}");
                    offset++;
                    if (IsPunct(k, ",")) k++;
                }
                t = k;
            }
        }

        /// <summary>Resolves a constant's value through other constants, recursively. Bounded depth guards
        /// against reference cycles.</summary>
        private static bool TryResolve(string name, Dictionary<string, string> rawExpr, Dictionary<string, int> cache, int depth, out int value)
        {
            value = 0;
            if (cache.TryGetValue(name, out value)) return true;
            if (depth > 25 || !rawExpr.TryGetValue(name, out string expr)) return false;
            if (!HgEngineSourceExpression.TryEvaluate(expr, n => TryResolve(n, rawExpr, cache, depth + 1, out int v) ? v : null, out value)) return false;
            cache[name] = value;
            return true;
        }
    }
}
