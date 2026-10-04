using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace DSPRE.HgEngine
{
    /// <summary>
    /// The items a smashed rock can hold, from src/field/rock_smash_item.c's <c>RockSmashItemTable</c>. hg-engine
    /// replaces the game's three overlay 1 tables with this array; each map's a/2/5/3 file still picks the table by
    /// its type (0 default, 1 Ruins of Alph, 2 Cliff Cave), which is the array's first index.
    /// </summary>
    public static class HgEngineRockSmashItems
    {
        private const string SourceRelPath = "src/field/rock_smash_item.c";
        private const string ItemHeaderRelPath = "include/constants/item.h";

        private static readonly Regex Table = new(@"RockSmashItemTable\s*\[[^\]]*\]\s*\[[^\]]*\]\s*=\s*\{(?<body>.*?)^\s*\};",
            RegexOptions.Singleline | RegexOptions.Multiline);
        private static readonly Regex Group = new(@"\{(?<items>[^{}]*)\}");
        private static readonly Regex Token = new(@"[A-Za-z_][A-Za-z0-9_]*|\d+");
        private static readonly Regex Comment = new(@"//[^\n]*");

        private sealed record Slot(int Item, int Start, int Length);

        public static bool TryRead(int table, out ushort[] items, out string error)
        {
            items = null;
            if (!TryParse(out var tables, out _, out error)) return false;
            if (table < 0 || table >= tables.Count) { error = $"{SourceRelPath} has no Rock Smash table {table}."; return false; }
            items = tables[table].ConvertAll(s => (ushort)s.Item).ToArray();
            return true;
        }

        /// <summary>Writes one table's items by name, leaving the comments and layout as they were.</summary>
        public static bool TryWrite(int table, IReadOnlyList<ushort> items, out string error)
        {
            if (!TryParse(out var tables, out string text, out error)) return false;
            if (table < 0 || table >= tables.Count) { error = $"{SourceRelPath} has no Rock Smash table {table}."; return false; }
            var slots = tables[table];
            if (slots.Count != items.Count)
            { error = $"Rock Smash table {table} in {SourceRelPath} holds {slots.Count} items, not {items.Count}."; return false; }

            var names = HgEngineSymbolTable.Load(ItemHeaderRelPath);
            var sb = new System.Text.StringBuilder(text);
            for (int i = slots.Count - 1; i >= 0; i--)
            {
                if (slots[i].Item == items[i]) continue;
                if (!names.TryGetNameWithPrefix(items[i], "ITEM_", out string name))
                { error = $"item.h has no ITEM_ name for item {items[i]}."; return false; }
                sb.Remove(slots[i].Start, slots[i].Length).Insert(slots[i].Start, name);
            }
            HgEngineFileCache.WriteText(SourcePath(), sb.ToString());
            return true;
        }

        private static bool TryParse(out List<List<Slot>> tables, out string text, out string error)
        {
            tables = new List<List<Slot>>();
            error = null;
            text = null;
            if (!HgEngineProject.IsActive) { error = "No hg-engine folder is open."; return false; }

            var names = HgEngineSymbolTable.Load(ItemHeaderRelPath);
            if (names == null) { error = $"Could not read {ItemHeaderRelPath} from the checkout."; return false; }
            string path = SourcePath();
            text = File.Exists(path) ? HgEngineFileCache.GetText(path) : null;
            if (text == null) { error = $"Could not read {SourceRelPath} from the checkout."; return false; }

            Match table = Table.Match(text);
            if (!table.Success) { error = $"{SourceRelPath} has no RockSmashItemTable array."; return false; }

            // Comments are blanked rather than removed, so every position still points into the real text.
            string body = Comment.Replace(table.Groups["body"].Value, m => new string(' ', m.Length));
            int bodyStart = table.Groups["body"].Index;
            foreach (Match group in Group.Matches(body))
            {
                var slots = new List<Slot>();
                foreach (Match token in Token.Matches(group.Groups["items"].Value))
                {
                    if (!names.TryGetValue(token.Value, out int item) && !int.TryParse(token.Value, out item))
                    { error = $"{token.Value} in {SourceRelPath} is not an item item.h names."; return false; }
                    slots.Add(new Slot(item, bodyStart + group.Groups["items"].Index + token.Index, token.Length));
                }
                tables.Add(slots);
            }
            return true;
        }

        private static string SourcePath() =>
            Path.Combine(HgEngineProject.RepoPathUnc, SourceRelPath.Replace('/', Path.DirectorySeparatorChar));
    }
}
