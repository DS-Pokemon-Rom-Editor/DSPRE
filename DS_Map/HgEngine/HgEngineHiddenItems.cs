using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace DSPRE.HgEngine
{
    /// <summary>Source-text read/write for data/HiddenItems.c's <c>sHiddenItemParam[]</c>: a plain
    /// sequential array with no <c>[N] =</c> designators, so the whole array is rewritten on save rather
    /// than patched entry-by-entry, matching how the vanilla Hidden Items editor already treats this table.
    /// hg-engine's <c>index</c> field is the same byte as vanilla's "ScriptID". <c>unk3</c>/<c>unk4</c> are
    /// always 0 in this checkout and vanilla's own save already zeroes the equivalent bytes, so they're
    /// hardcoded to 0 here rather than exposed in the UI.
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

            string text = TryReadSource(out string path);
            if (text == null) { error = $"Source file not found: {path}"; return false; }
            if (!TryFindArrayBlock(text, out int open, out int close))
            { error = "Could not locate sHiddenItemParam[] in HiddenItems.c."; return false; }

            var items = HgEngineSymbolTable.Load(ItemHeaderRelPath);
            var lines = new List<string>(entries.Count);
            foreach (var e in entries)
            {
                string item = items != null && items.TryGetNameWithPrefix(e.ItemId, "ITEM_", out string n) ? n : e.ItemId.ToString();
                lines.Add($"    {{ {item}, {e.Quantity}, 0, 0, {e.Index} }},");
            }
            string newBlock = "{\n" + string.Join("\n", lines) + "\n}";
            text = text.Substring(0, open) + newBlock + text.Substring(close + 1);
            File.WriteAllText(path, text);

            if (!TryUpdateConsumerCount(entries.Count, out string countError))
            { error = countError; return false; }
            return true;
        }

        private static bool TryUpdateConsumerCount(int count, out string error)
        {
            error = null;
            string path = Path.Combine(HgEngineProject.RepoPathUnc, ConsumerRelPath.Replace('/', '\\'));
            if (!File.Exists(path)) { error = $"Source file not found: {path}"; return false; }
            string text = File.ReadAllText(path);
            if (!HgEngineHeaderEditor.TryReplaceDefineValue(ref text, CountDefineName, count.ToString()))
            { error = $"Could not find {CountDefineName} in {ConsumerRelPath} to keep in sync with the entry count."; return false; }
            File.WriteAllText(path, text);
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
