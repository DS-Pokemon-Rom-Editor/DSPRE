using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DSPRE.HgEngine
{
    /// <summary>
    /// The items a smashed rock can hold, from src/field/rock_smash_item.c's <c>RockSmashItemTable</c>. hg-engine
    /// replaces the game's three overlay 1 tables with this array; each map's a/2/5/3 file still picks the table by
    /// its type (0 default, 1 Ruins of Alph, 2 Cliff Cave), which is the array's first index.
    /// </summary>
    public static class HgEngineRockSmashItems
    {
        internal const string SourceRelPath = "src/field/rock_smash_item.c";
        private const string ItemHeaderRelPath = "include/constants/item.h";

        private const string TableName = "RockSmashItemTable";

        private sealed record Slot(int Item, int Start, int Length);

        public static bool TryRead(int table, out ushort[] items, out string error)
        {
            items = null;
            if (!TryParse(out List<List<Slot>> tables, out _, out error)) return false;
            if (table < 0 || table >= tables.Count) { error = $"{SourceRelPath} has no Rock Smash table {table}."; return false; }
            items = tables[table].ConvertAll(s => (ushort)s.Item).ToArray();
            return true;
        }

        /// <summary>Writes one table's items by name, leaving the comments and layout as they were.</summary>
        public static bool TryWrite(int table, IReadOnlyList<ushort> items, out string error)
        {
            if (!TryParse(out List<List<Slot>> tables, out string text, out error)) return false;
            if (table < 0 || table >= tables.Count) { error = $"{SourceRelPath} has no Rock Smash table {table}."; return false; }
            List<Slot> slots = tables[table];
            if (slots.Count != items.Count)
            { error = $"Rock Smash table {table} in {SourceRelPath} holds {slots.Count} items, not {items.Count}."; return false; }

            HgEngineSymbolTable names = HgEngineSymbolTable.Load(ItemHeaderRelPath);
            StringBuilder sb = new System.Text.StringBuilder(text);
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
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout is linked."; return false; }

            HgEngineSymbolTable names = HgEngineSymbolTable.Load(ItemHeaderRelPath);
            if (names == null) { error = $"Couldn't read {ItemHeaderRelPath} from the checkout."; return false; }
            string path = SourcePath();
            text = File.Exists(path) ? HgEngineFileCache.GetText(path) : null;
            if (text == null) { error = $"Couldn't read {SourceRelPath} from the checkout."; return false; }

            CDeclaration table = CSourceFile.For(text).Find(TableName);
            if (table == null) { error = $"{SourceRelPath} has no {TableName} array."; return false; }

            foreach (CInitItem group in table.Init.Items)
            {
                if (group.List == null || group.IsConditional)
                { error = $"{SourceRelPath} has a table in {TableName} DSPRE can't read."; return false; }
                List<Slot> slots = new List<Slot>();
                foreach (CInitItem entry in group.List.Items)
                {
                    string token = entry.ValueText(text).Trim();
                    if (entry.IsConditional || (!names.TryGetValue(token, out int item) && !CLexer.TryParseInt(token, out item)))
                    { error = $"{token} in {SourceRelPath} isn't an item that item.h names."; return false; }
                    slots.Add(new Slot(item, entry.ValueStart, entry.ValueEnd - entry.ValueStart));
                }
                tables.Add(slots);
            }
            return true;
        }

        private static string SourcePath() =>
            Path.Combine(HgEngineProject.RepoPathUnc, SourceRelPath.Replace('/', Path.DirectorySeparatorChar));
    }
}
