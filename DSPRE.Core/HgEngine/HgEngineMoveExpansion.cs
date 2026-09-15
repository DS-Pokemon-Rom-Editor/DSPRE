using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace DSPRE.HgEngine
{
    /// <summary>Mints a new move: a #define in moves.h (bumping NUM_OF_CUSTOM_MOVES), and a matching
    /// entry in Moves.c with its own embedded name/description. NUM_OF_CANONICAL_MOVES stays untouched
    /// as the vanilla boundary marker. The G-Max moves are numbered from NUM_OF_MOVES, so they move up
    /// by one with every custom move and never share its id once the header is rewritten.</summary>
    public static class HgEngineMoveExpansion
    {
        private const string HeaderRelPath = "include/constants/moves.h";
        private const string SourceRelPath = "data/Moves.c";
        private const string Prefix = "MOVE_";
        private static readonly Regex TerminatorEntry = new(@"^[ \t]*\[\s*NUM_OF_MOVES\s*\]\s*=", RegexOptions.Multiline);

        public static bool TryGetVanillaBoundary(out int lastVanillaMoveId)
        {
            lastVanillaMoveId = -1;
            var moves = HgEngineSymbolTable.Load(HeaderRelPath);
            // NUM_OF_CANONICAL_MOVES is a count, not a max id.
            if (moves == null || !moves.TryGetValue("NUM_OF_CANONICAL_MOVES", out int canonicalCount) || canonicalCount <= 0)
                return false;
            lastVanillaMoveId = canonicalCount - 1;
            return true;
        }

        /// <summary>The [firstCustomId, firstCustomId + count) range of actually custom-added moves.</summary>
        public static bool TryGetCustomRange(out int firstCustomId, out int count)
        {
            firstCustomId = -1;
            count = 0;
            var moves = HgEngineSymbolTable.Load(HeaderRelPath);
            if (moves == null) return false;
            if (!moves.TryGetValue("NUM_OF_CANONICAL_MOVES", out int canonicalCount)) return false;
            if (!moves.TryGetValue("NUM_OF_CUSTOM_MOVES", out int customCount)) return false;
            firstCustomId = canonicalCount;
            count = customCount;
            return true;
        }

        /// <summary>A new move that Add has numbered and shaped but not written. Save commits it.</summary>
        public sealed class PendingMove
        {
            public int Id { get; init; }
            public string Designator { get; init; }
            public string DisplayName { get; init; }
            /// <summary>The new Moves.c entry as it is inserted, before any edits.</summary>
            public HgEngineSourceBlock Entry { get; init; }
        }

        /// <summary>Numbers and shapes a new move without writing anything, so moves.h readers keep seeing
        /// the current ids until <see cref="TryCommitMove"/> runs.</summary>
        public static bool TryPrepareMove(string displayName, out PendingMove pending, out string error)
        {
            pending = null;
            if (!TryReadSources(out _, out string headerText, out _, out string sourceText, out error)) return false;
            return TryPrepare(headerText, sourceText, displayName, HgEngineSymbolTable.Load(ConfigRelPath)?.ByName, out pending, out _, out _, out error);
        }

        /// <summary>Fills <paramref name="move"/> from the pending move's new entry.</summary>
        public static bool TryReadTemplate(PendingMove pending, MoveData move, out string error) =>
            HgEngineSourceFields.TryRead(pending.Entry, HgEngineMoveSource.Fields, move, HgEngineSourceFields.NameLookup(HgEngineMoveSource.Headers), out error);

        /// <summary>Writes the pending move's define, its Moves.c entry and <paramref name="move"/>'s fields as one
        /// change. Run it inside a save session and call <see cref="CompleteAdd"/> once that commits.</summary>
        public static bool TryCommitMove(PendingMove pending, MoveData move, out string error)
        {
            if (!TryReadSources(out string headerPath, out string headerText, out string sourcePath, out string sourceText, out error)) return false;
            if (!TryBuildCommit(headerText, sourceText, pending, move, HgEngineSymbolTable.Load(ConfigRelPath)?.ByName,
                    w => HgEngineSourceFields.NameLookup(w.Headers), out string newHeader, out string newSource, out error))
                return false;

            try
            {
                HgEngineFileCache.WriteText(headerPath, newHeader);
                try { HgEngineFileCache.WriteText(sourcePath, newSource); }
                catch
                {
                    HgEngineFileCache.WriteText(headerPath, headerText);
                    throw;
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                error = $"The new move couldn't be written, so nothing was added: {ex.Message}";
                return false;
            }
            return true;
        }

        /// <summary>Runs after the add is on disk. The build regenerates move names from Moves.c, so the ROM
        /// copy only previews the name. Later slots move up one, as they will in the rebuilt archive.</summary>
        public static void CompleteAdd(PendingMove pending)
        {
            HgEngineSymbolTable.ClearCache();
            var names = new ROMFiles.TextArchive(RomInfo.attackNamesTextNumber);
            while (names.messages.Count < pending.Id) names.messages.Add("");
            names.messages.Insert(pending.Id, pending.DisplayName);
            names.SaveToExpandedDir(RomInfo.attackNamesTextNumber, showSuccessMessage: false);
        }

        private const string ConfigRelPath = "include/config.h";

        private static bool TryReadSources(out string headerPath, out string headerText, out string sourcePath, out string sourceText, out string error)
        {
            headerText = sourceText = sourcePath = null;
            error = null;
            headerPath = null;
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout linked."; return false; }
            headerPath = Path.Combine(HgEngineProject.RepoPathUnc, HeaderRelPath.Replace('/', '\\'));
            if (!File.Exists(headerPath)) { error = $"Source file not found: {headerPath}"; return false; }
            sourcePath = Path.Combine(HgEngineProject.RepoPathUnc, SourceRelPath.Replace('/', '\\'));
            if (!File.Exists(sourcePath)) { error = $"Source file not found: {sourcePath}"; return false; }
            headerText = HgEngineFileCache.GetText(headerPath);
            sourceText = HgEngineFileCache.GetText(sourcePath);
            return true;
        }

        /// <summary>Pure half of <see cref="TryPrepareMove"/>: the id, the name and the rewritten texts.</summary>
        internal static bool TryPrepare(string headerText, string sourceText, string displayName, IReadOnlyDictionary<string, int> config,
            out PendingMove pending, out string newHeader, out string newSource, out string error)
        {
            pending = null;
            newHeader = newSource = null;
            error = null;
            var moves = HgEngineSymbolTable.Parse(headerText, config);
            if (!moves.TryGetValue("NUM_OF_CANONICAL_MOVES", out int canonicalCount))
            { error = "Could not find NUM_OF_CANONICAL_MOVES in moves.h."; return false; }
            if (!moves.TryGetValue("NUM_OF_CUSTOM_MOVES", out int customCount))
            { error = "Could not find NUM_OF_CUSTOM_MOVES in moves.h."; return false; }

            int candidateId = canonicalCount + customCount;
            string designator = Prefix + HgEngineNameSlug.ToUniqueSlug(displayName, moves, Prefix);
            if (!TryBuildSources(headerText, sourceText, displayName, designator, customCount, candidateId, config, out newHeader, out newSource, out error))
                return false;
            if (!HgEngineSourcePatcher.TryFindEntry(newSource, designator, out int open, out int close))
            { error = $"The new {designator} entry could not be read back, so nothing was added."; return false; }

            pending = new PendingMove
            {
                Id = candidateId,
                Designator = designator,
                DisplayName = displayName,
                Entry = new HgEngineSourceBlock(newSource.Substring(open, close - open + 1)),
            };
            return true;
        }

        /// <summary>Pure half of <see cref="TryCommitMove"/>. The add is prepared again from the current texts, so
        /// a header changed since Add refuses instead of reusing a stale id.</summary>
        internal static bool TryBuildCommit(string headerText, string sourceText, PendingMove pending, MoveData move, IReadOnlyDictionary<string, int> config,
            Func<HgEngineValueSpelling.Write, Func<string, int?>> lookupFor, out string newHeader, out string newSource, out string error)
        {
            newHeader = newSource = null;
            if (!TryPrepare(headerText, sourceText, pending.DisplayName, config, out var fresh, out string header, out string source, out error)) return false;
            if (fresh.Id != pending.Id || fresh.Designator != pending.Designator)
            { error = "moves.h changed after the move was added, so it was not saved. Discard it and add it again."; return false; }
            if (!HgEngineMoveSource.TryApply(ref source, new[] { (pending.Designator, move) }, lookupFor, out error)) return false;
            newHeader = header;
            newSource = source;
            return true;
        }

        /// <summary>Pure text half of <see cref="TryPrepare"/>. Refuses when the rewritten header would give
        /// the new id to any other move constant.</summary>
        internal static bool TryBuildSources(string headerText, string sourceText, string displayName, string designator,
            int customCount, int candidateId, IReadOnlyDictionary<string, int> config,
            out string newHeader, out string newSource, out string error)
        {
            newHeader = headerText;
            newSource = sourceText;
            error = null;

            if (!HgEngineHeaderEditor.TryInsertBeforeDefine(ref newHeader, "NUM_OF_CUSTOM_MOVES", $"#define {designator} (NUM_OF_CANONICAL_MOVES + {customCount})\n\n"))
            { error = "Could not find NUM_OF_CUSTOM_MOVES in moves.h to anchor the new move next to."; return false; }
            if (!HgEngineHeaderEditor.TryReplaceDefineValue(ref newHeader, "NUM_OF_CUSTOM_MOVES", (customCount + 1).ToString()))
            { error = "Could not update NUM_OF_CUSTOM_MOVES."; return false; }

            var rebuilt = HgEngineSymbolTable.Parse(newHeader, config);
            if (!rebuilt.TryGetValue(designator, out int newId) || newId != candidateId)
            { error = $"moves.h would not number {designator} as {candidateId}, so nothing was added."; return false; }
            var clash = rebuilt.ByName
                .Where(kv => kv.Value == candidateId && kv.Key != designator && kv.Key.StartsWith(Prefix, StringComparison.Ordinal))
                .Select(kv => kv.Key).FirstOrDefault();
            if (clash != null)
            { error = $"{clash} would share id {candidateId} with the new move, so nothing was added."; return false; }

            string safeName = EscapeCString(displayName);
            string safeCaps = EscapeCString(displayName.ToUpperInvariant());
            string newEntry =
                $"[{designator}] = {{\n" +
                "        .names = {\n" +
                $"            .name = \"{safeName}\",\n" +
                $"            .capsName = \"{safeCaps}\",\n" +
                $"            .fullName = \"{safeName}\",\n" +
                "        },\n" +
                "        .data = {\n" +
                "            .effect = MOVE_EFFECT_HIT,\n" +
                "            .split = SPLIT_STATUS,\n" +
                "            .power = 0,\n" +
                "            .type = TYPE_NORMAL,\n" +
                "            .accuracy = 0,\n" +
                "            .pp = 5,\n" +
                "            .effectChance = 0,\n" +
                "        },\n" +
                "        .battle = {\n" +
                "            .target = RANGE_SINGLE_TARGET,\n" +
                "            .priority = 0,\n" +
                "            .flags = 0x00,\n" +
                "        },\n" +
                "        .contest = {\n" +
                "            .appeal = 0,\n" +
                "            .contestType = CONTEST_COOL,\n" +
                "        },\n" +
                "        .description = \"\\\\n\\\\n\\\\n\\\\n\",\n" +
                "    },\n\n";

            // Moves.c ends with the [NUM_OF_MOVES] terminator entry; the new move goes just before it.
            Match terminator = TerminatorEntry.Match(newSource);
            if (terminator.Success)
            {
                int lineStart = terminator.Index;
                int indentEnd = lineStart;
                while (indentEnd < newSource.Length && (newSource[indentEnd] == ' ' || newSource[indentEnd] == '\t')) indentEnd++;
                string indent = newSource.Substring(lineStart, indentEnd - lineStart);
                newSource = string.Concat(newSource.AsSpan(0, indentEnd), newEntry, indent, newSource.AsSpan(indentEnd));
            }
            else if (!HgEngineHeaderEditor.TryInsertBeforeFinalCloseBrace(ref newSource, "\n    " + newEntry))
            { error = $"Could not find the end of {SourceRelPath} to insert the new move."; return false; }

            return true;
        }

        private static string EscapeCString(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    /// <summary>
    /// Keeps a source field's own spelling when a save doesn't change its value. A value-based rewrite
    /// can't spell a marker that compiles to zero, and may pick a different name for a shared value.
    /// </summary>
    public static class HgEngineValueSpelling
    {
        /// <summary>One symbolic field of an entry: its new value, the literal to write if it changed, and
        /// the headers its current spelling resolves against.</summary>
        public sealed class Write
        {
            public IReadOnlyList<FieldPathSegment> Path { get; init; }
            public int Value { get; init; }
            public string Literal { get; init; }
            public bool IsFlags { get; init; }
            public string[] Headers { get; init; } = Array.Empty<string>();
        }

        /// <summary>Turns the writes into field writes, keeping each field's current text where its value
        /// is unchanged. Fields the entry doesn't declare pass through, so the writer still reports them.</summary>
        public static List<HgEngineFieldWrite> Preserve(HgEngineDomain domain, int id, IEnumerable<Write> writes)
        {
            HgEngineSourceBlock? entry = null;
            var info = HgEngineDomains.All.FirstOrDefault(d => d.Domain == domain);
            if (HgEngineProject.IsActive && info != null && HgEngineDesignators.TryResolve(domain, id, out string designator))
            {
                string path = System.IO.Path.Combine(HgEngineProject.RepoPathUnc, info.SourceFileRelPath.Replace('/', '\\'));
                if (File.Exists(path))
                {
                    string text = HgEngineFileCache.GetText(path);
                    if (HgEngineSourcePatcher.TryFindEntry(text, designator, out int open, out int close))
                        entry = new HgEngineSourceBlock(text.Substring(open, close - open + 1));
                }
            }
            return Preserve(entry, writes, w => HgEngineSourceFields.NameLookup(w.Headers));
        }

        /// <summary>Pure half of <see cref="Preserve(HgEngineDomain, int, IEnumerable{Write})"/> over an
        /// already-loaded entry; a null entry keeps no spelling.</summary>
        internal static List<HgEngineFieldWrite> Preserve(HgEngineSourceBlock? entry, IEnumerable<Write> writes, Func<Write, Func<string, int?>> lookupFor)
        {
            var result = new List<HgEngineFieldWrite>();
            foreach (var w in writes)
            {
                string raw = null;
                entry?.TryGetRaw(w.Path, out raw);
                var lookup = lookupFor(w);
                // Whole expressions resolve, so an unchanged OR of names or a config ternary keeps its text.
                int? Resolve(string token) => HgEngineSourceExpression.TryEvaluate(token, lookup, out int v) ? v : null;
                string literal = w.IsFlags ? MergeFlags(raw, w.Value, w.Literal, Resolve) : KeepSpelling(raw, w.Value, w.Literal, Resolve);
                result.Add(new HgEngineFieldWrite(w.Path, literal));
            }
            return result;
        }

        internal static string KeepSpelling(string originalRaw, int newValue, string newLiteral, Func<string, int?> resolve)
            => originalRaw != null && resolve(originalRaw.Trim()) == newValue ? originalRaw : newLiteral;

        /// <summary>An OR of flag names. Unchanged value: the original text. Changed: the original terms that
        /// are still set, in their order, then new terms for the bits left over. Terms that compile to zero
        /// stay, so disabled markers survive.</summary>
        internal static string MergeFlags(string originalRaw, int newValue, string newExpression, Func<string, int?> resolve)
        {
            if (string.IsNullOrWhiteSpace(originalRaw)) return newExpression;

            var terms = originalRaw.Split('|').Select(t => t.Trim()).Where(t => t.Length > 0).ToList();
            int value = 0;
            bool known = true;
            foreach (string term in terms)
            {
                int? v = resolve(term);
                if (v == null) { known = false; continue; }
                value |= v.Value;
            }
            if (known && value == newValue) return originalRaw;

            // Keeping the original order leaves the added or removed flag as the only change in the diff.
            var parts = new List<string>();
            int covered = 0;
            foreach (string term in terms)
            {
                int? v = resolve(term);
                if (v == null || (v.Value == 0 && IsLiteral(term)) || (v.Value & ~newValue) != 0) continue;
                parts.Add(term);
                covered |= v.Value;
            }
            foreach (string part in newExpression.Split('|').Select(t => t.Trim()).Where(t => t.Length > 0))
            {
                int? v = resolve(part);
                if (parts.Contains(part) || (v != null && (v.Value & ~covered) == 0)) continue;
                parts.Add(part);
                if (v != null) covered |= v.Value;
            }
            return parts.Count == 0 ? newExpression : string.Join(" | ", parts);
        }

        private static bool IsLiteral(string term) => term.Length > 0 && (char.IsDigit(term[0]) || term[0] == '-');
    }
}
