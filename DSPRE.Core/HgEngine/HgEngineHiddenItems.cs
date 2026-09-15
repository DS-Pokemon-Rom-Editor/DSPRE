using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace DSPRE.HgEngine
{
    /// <summary>Source-text read/write for data/HiddenItems.c's <c>sHiddenItemParam[]</c>: a plain
    /// sequential array with no <c>[N] =</c> designators, so the whole array is rewritten on save rather
    /// than patched entry-by-entry, matching how the vanilla Hidden Items editor already treats this table.
    /// hg-engine's <c>index</c> field is the same byte as vanilla's "ScriptID". Rows keep their trailing
    /// comment and their <c>unk3</c>/<c>unk4</c> values by position; new rows get 0.
    ///
    /// <c>src/field/hidden_items.c</c>'s own lookup loop bounds itself with a hand-maintained
    /// <c>#define HIDDEN_ITEM_PARAM_COUNT 231</c>, not the array's real length, so adding/removing entries
    /// without also updating that constant either hides new entries from the game or makes it read
    /// past the end of the compiled array. <see cref="TrySave"/> keeps the two in sync.</summary>
    public static class HgEngineHiddenItems
    {
        private const string SourceRelPath = "data/HiddenItems.c";
        private const string ConsumerRelPath = "src/field/hidden_items.c";
        private const string ItemHeaderRelPath = "include/constants/item.h";
        private const string CountDefineName = "HIDDEN_ITEM_PARAM_COUNT";
        private static readonly Regex ArrayAnchor = new(@"const\s+HiddenItemData\s+sHiddenItemParam\s*\[\s*\]\s*=\s*\{");
        private static readonly Regex RowLine = new(@"^\s*(\{[^{}]*\})\s*,?\s*(//.*|/\*.*\*/)?\s*$");

        public struct Entry { public int ItemId; public int Quantity; public int Index; }

        public static bool TryLoad(out List<Entry> entries, out string error)
        {
            entries = null; error = null;
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout linked."; return false; }

            string text = TryReadSource(out string path);
            if (text == null) { error = $"Source file not found: {path}"; return false; }
            if (!TryFindArrayBlock(text, out int open, out int close))
            { error = "Could not locate sHiddenItemParam[] in HiddenItems.c."; return false; }

            var items = HgEngineSymbolTable.Load(ItemHeaderRelPath);
            entries = ParseEntries(text.Substring(open, close - open + 1), items);
            return true;
        }

        /// <summary>Pure parse over an already-isolated "{ ... }" array block, split out from
        /// <see cref="TryLoad"/> so it's directly unit-testable against a real multi-entry excerpt.</summary>
        internal static List<Entry> ParseEntries(string arrayBlock, HgEngineSymbolTable items)
        {
            var entries = new List<Entry>();
            foreach (var el in HgEngineSourcePatcher.SplitArrayValue(arrayBlock))
            {
                var parts = HgEngineSourcePatcher.SplitArrayValue(el.Trim());
                if (parts.Count < 5) continue;
                entries.Add(new Entry
                {
                    ItemId = ResolveToken(parts[0], items),
                    Quantity = ResolveToken(parts[1], null),
                    Index = ResolveToken(parts[4], null),
                });
            }
            return entries;
        }

        public static bool TrySave(IReadOnlyList<Entry> entries, out string error)
        {
            error = null;
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout linked."; return false; }

            string original = TryReadSource(out string path);
            if (original == null) { error = $"Source file not found: {path}"; return false; }
            if (!TryFindArrayBlock(original, out int open, out int close))
            { error = "Could not locate sHiddenItemParam[] in HiddenItems.c."; return false; }

            var items = HgEngineSymbolTable.Load(ItemHeaderRelPath);
            string ItemSymbol(int id) => items != null && items.TryGetNameWithPrefix(id, "ITEM_", out string n) ? n : id.ToString();
            if (!TryBuildArrayBlock(original.Substring(open, close - open + 1), entries, ItemSymbol, out string newBlock, out error))
                return false;
            string text = original.Substring(0, open) + newBlock + original.Substring(close + 1);

            string consumerPath = Path.Combine(HgEngineProject.RepoPathUnc, ConsumerRelPath.Replace('/', '\\'));
            if (!File.Exists(consumerPath)) { error = $"Source file not found: {consumerPath}"; return false; }
            string consumerText = HgEngineFileCache.GetText(consumerPath);
            if (!HgEngineHeaderEditor.TryReplaceDefineValue(ref consumerText, CountDefineName, entries.Count.ToString()))
            { error = $"Could not find {CountDefineName} in {ConsumerRelPath}, so nothing was written."; return false; }

            try { HgEngineFileCache.WriteText(path, text); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            { error = $"{SourceRelPath} couldn't be written: {ex.Message}"; return false; }

            try { HgEngineFileCache.WriteText(consumerPath, consumerText); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                // The table and its count must agree, so the table goes back to what it was.
                try { HgEngineFileCache.WriteText(path, original); }
                catch (Exception restoreEx) when (restoreEx is IOException || restoreEx is UnauthorizedAccessException)
                {
                    error = $"{ConsumerRelPath} couldn't be written ({ex.Message}), and {SourceRelPath} couldn't be restored ({restoreEx.Message}). " +
                            $"Set {CountDefineName} to {entries.Count} by hand.";
                    return false;
                }
                error = $"{ConsumerRelPath} couldn't be written, so nothing was saved: {ex.Message}";
                return false;
            }
            return true;
        }

        /// <summary>Rebuilds the "{ ... }" block, one row per entry. Row i keeps row i's trailing comment,
        /// unk fields and any comment lines above it. Refuses a block it can't rewrite without losing text.</summary>
        internal static bool TryBuildArrayBlock(string oldBlock, IReadOnlyList<Entry> entries, Func<int, string> itemSymbol,
            out string newBlock, out string error)
        {
            newBlock = null;
            error = null;
            string inner = oldBlock.Replace("\r\n", "\n");
            inner = inner.Substring(1, inner.Length - 2);

            var comments = new List<string>();
            var unknowns = new List<(string, string)>();
            var linesAbove = new List<List<string>>();
            var pending = new List<string>();

            foreach (string line in inner.Split('\n'))
            {
                string trimmed = line.Trim();
                if (trimmed.Length == 0) continue;
                if (trimmed.StartsWith("#", StringComparison.Ordinal))
                { error = $"sHiddenItemParam[] has preprocessor lines, which rewriting the table would drop. Edit {SourceRelPath} by hand."; return false; }
                if (trimmed.StartsWith("//", StringComparison.Ordinal) || (trimmed.StartsWith("/*", StringComparison.Ordinal) && trimmed.EndsWith("*/", StringComparison.Ordinal)))
                { pending.Add(trimmed); continue; }

                Match m = RowLine.Match(line);
                var parts = m.Success ? HgEngineSourcePatcher.SplitArrayValue(m.Groups[1].Value) : null;
                if (parts == null || parts.Count != 5)
                { error = $"sHiddenItemParam[] has a line DSPRE can't rewrite safely: {trimmed}"; return false; }

                comments.Add(m.Groups[2].Success ? m.Groups[2].Value.TrimEnd() : null);
                unknowns.Add((parts[2].Trim(), parts[3].Trim()));
                linesAbove.Add(pending);
                pending = new List<string>();
            }

            var output = new List<string>(entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                if (i < linesAbove.Count) foreach (string above in linesAbove[i]) output.Add("    " + above);
                var e = entries[i];
                (string unk3, string unk4) = i < unknowns.Count ? unknowns[i] : ("0", "0");
                string row = $"    {{ {itemSymbol(e.ItemId)}, {e.Quantity}, {unk3}, {unk4}, {e.Index} }},";
                if (i < comments.Count && comments[i] != null) row += " " + comments[i];
                output.Add(row);
            }
            for (int i = entries.Count; i < linesAbove.Count; i++)
                foreach (string above in linesAbove[i]) output.Add("    " + above);
            foreach (string above in pending) output.Add("    " + above);

            newBlock = "{\n" + string.Join("\n", output) + "\n}";
            return true;
        }

        private static bool TryFindArrayBlock(string text, out int open, out int close)
        {
            open = close = -1;
            var m = ArrayAnchor.Match(text);
            if (!m.Success) return false;
            int braceStart = m.Index + m.Length - 1;
            if (!BraceScanner.TryFindMatchingBrace(text, braceStart, out int braceEnd)) return false;
            open = braceStart; close = braceEnd;
            return true;
        }

        private static int ResolveToken(string token, HgEngineSymbolTable table)
        {
            token = token.Trim();
            if (int.TryParse(token, out int v)) return v;
            return table != null && table.TryGetValue(token, out int tv) ? tv : 0;
        }

        private static string TryReadSource(out string path)
        {
            path = Path.Combine(HgEngineProject.RepoPathUnc, SourceRelPath.Replace('/', '\\'));
            return File.Exists(path) ? HgEngineFileCache.GetText(path) : null;
        }
    }
}
