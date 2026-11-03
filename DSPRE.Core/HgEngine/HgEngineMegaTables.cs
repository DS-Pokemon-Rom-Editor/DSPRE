using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DSPRE.HgEngine
{
    /// <summary>src/battle/mega.c: sMegaTable, a form reached by holding an item, and sMegaMoveTable, one reached by
    /// knowing a move. Rows are read by field name.</summary>
    public static class HgEngineMegaTables
    {
        public const string SourceRelPath = "src/battle/mega.c";
        public const string StoneTable = "sMegaTable", MoveTable = "sMegaMoveTable";

        private static readonly string[] Headers =
            { "include/constants/species.h", "include/constants/item.h", "include/constants/moves.h" };

        /// <summary>One row. Trigger is the item in sMegaTable and the move in sMegaMoveTable.</summary>
        public sealed record Row(int Species, int Trigger, int TargetForm, int BaseForm);

        private static string FilePath => Path.Combine(HgEngineProject.RepoPathUnc, SourceRelPath.Replace('/', Path.DirectorySeparatorChar));

        /// <summary>Whether this checkout builds Mega Evolution, so the tables can be edited.</summary>
        public static bool Available =>
            HgEngineProject.IsActive && File.Exists(FilePath) && CSourceFile.For(HgEngineFileCache.GetText(FilePath)).Find(StoneTable) is CDeclaration table
            && HgEngineConfigState.Compiles(table.Conditions) != false;

        /// <summary>Makes a species' rows in one table match <paramref name="rows"/>. A row with the same forms keeps
        /// its place and only its trigger changes. Other species' rows and every comment stay as they are.</summary>
        public static bool TryWrite(string table, int species, IReadOnlyList<Row> rows, out string error)
        {
            error = null;
            string text = HgEngineFileCache.GetText(FilePath), original = text;
            string trigger = table == StoneTable ? "itemindex" : "moveindex", prefix = table == StoneTable ? "ITEM_" : "MOVE_";
            HgEngineSymbolTable names = HgEngineSymbolTable.Load(table == StoneTable ? Headers[1] : Headers[2]);
            HgEngineSymbolTable speciesNames = HgEngineSymbolTable.Load(Headers[0]);
            Func<string, int?> lookup = HgEngineSourceFields.NameLookup(Headers);
            int? Number(CInitItem item, string field) => item.List?.Field(field) is CInitItem value
                && HgEngineSourceExpression.TryEvaluate(value.ValueText(original), lookup, out int number) ? number : null;
            string TriggerName(int id) => names != null && names.TryGetNameWithPrefix(id, prefix, out string n) ? n : null;

            CDeclaration declaration = CSourceFile.For(text).Find(table);
            if (declaration == null) { error = $"{SourceRelPath} has no {table}."; return false; }
            if (HgEngineLoadedEntries.ChangedOutside(FilePath, LoadedKey(table, species), RowsText(Parse(table, text, out _).Where(r => r.Species == species))))
            { error = HgEngineLoadedEntries.ConflictMessage("mega.c", new[] { $"{table} ({(speciesNames != null && speciesNames.TryGetNameWithPrefix(species, "SPECIES_", out string monName) ? monName : species.ToString())})" }); return false; }

            List<Row> waiting = rows.ToList();
            bool Same(CInitItem item, Row row) => Number(item, "targetForm") == row.TargetForm && Number(item, "baseForm") == row.BaseForm;

            // Unchanged rows claim their match first, so a changed row can't take one of theirs.
            List<CInitItem> unmatched = new List<CInitItem>();
            foreach (CInitItem item in declaration.Init.Items.Where(i => Number(i, "monindex") == species))
            {
                Row exact = waiting.FirstOrDefault(r => Same(item, r) && Number(item, trigger) == r.Trigger);
                if (exact != null) waiting.Remove(exact);
                else unmatched.Add(item);
            }

            List<(CInitItem Item, Row Row)> edits = new List<(CInitItem, Row)>();
            foreach (CInitItem item in unmatched)
            {
                Row match = waiting.FirstOrDefault(r => Same(item, r)) ?? waiting.FirstOrDefault(r => Number(item, trigger) == r.Trigger);
                if (match != null) waiting.Remove(match);
                edits.Add((item, match));
            }

            if (edits.Any(e => e.Item.IsConditional))
            { error = $"A {table} row for this species sits under an #if. Change it in {SourceRelPath}."; return false; }

            foreach ((CInitItem item, Row row) in edits.OrderByDescending(e => e.Item.Start))
            {
                if (row == null)
                {
                    text = HgEngineSourcePatcher.RemoveElement(text, item.Start, item.End);
                    continue;
                }
                if (TriggerName(row.Trigger) is not string name) { error = $"There is no {prefix} name for {row.Trigger}."; return false; }
                (string Field, string Value)[] fields = { (trigger, name), ("targetForm", row.TargetForm.ToString()), ("baseForm", row.BaseForm.ToString()) };
                foreach ((CInitItem field, string value) in fields.Select(f => (item.List.Field(f.Field), f.Value)).OrderByDescending(f => f.Item1.ValueStart))
                    if (field.ValueText(original).Trim() != value)
                        text = text.Remove(field.ValueStart, field.ValueEnd - field.ValueStart).Insert(field.ValueStart, value);
            }

            string newline = text.Contains("\r\n") ? "\r\n" : "\n";
            foreach (Row row in waiting)
            {
                if (TriggerName(row.Trigger) is not string name) { error = $"There is no {prefix} name for {row.Trigger}."; return false; }
                if (speciesNames == null || !speciesNames.TryGetNameWithPrefix(species, "SPECIES_", out string mon)) { error = $"Species {species} has no SPECIES_ name."; return false; }
                int close = CSourceFile.For(text).Find(table).Init.Close;
                string block = string.Join(newline, "    {", $"        .monindex = {mon},", $"        .{trigger} = {name},",
                    $"        .targetForm = {row.TargetForm},", $"        .baseForm = {row.BaseForm},", "    },", "");
                text = text.Insert(HgEngineSourcePatcher.LineStart(text, close), block);
            }
            if (text == original) return true;

            // Only this species' rows are checked, so an unreadable row elsewhere doesn't block the save.
            if (!HgEngineVerifiedWrite.TryWrite(FilePath, SourceRelPath, text,
                    written => Parse(table, written, out _).Where(b => b.Species == species).OrderBy(Key).SequenceEqual(rows.OrderBy(Key)) ? null : $"{table} differs",
                    out error))
                return false;
            HgEngineLoadedEntries.Remember(FilePath, LoadedKey(table, species), RowsText(rows));
            return true;
        }

        private static (int, int, int) Key(Row row) => (row.TargetForm, row.BaseForm, row.Trigger);

        // Each species' rows as the editor loaded them, so a save sees changes made outside DSPRE.
        private static string LoadedKey(string table, int species) => $"@{table}:{species}";
        private static string RowsText(IEnumerable<Row> rows) => string.Join(";", rows.OrderBy(Key).Select(r => $"{r.Trigger},{r.TargetForm},{r.BaseForm}"));

        /// <summary>The rows of a table, for editing. A row that can't be read is counted in <paramref name="refused"/>.</summary>
        public static bool TryRead(string table, out List<Row> rows, out int refused, out string error)
        {
            rows = new List<Row>();
            refused = 0;
            error = null;
            string path = FilePath;
            if (!File.Exists(path)) { error = $"{SourceRelPath} is missing from the checkout."; return false; }

            string text = HgEngineFileCache.GetText(path);
            if (CSourceFile.For(text).Find(table) == null) { error = $"{SourceRelPath} has no {table}."; return false; }
            rows = Parse(table, text, out refused);
            foreach (IGrouping<int, Row> species in rows.GroupBy(r => r.Species))
                HgEngineLoadedEntries.Store(path, LoadedKey(table, species.Key), RowsText(species));
            return true;
        }

        private static List<Row> Parse(string table, string text, out int refused)
        {
            List<Row> rows = new List<Row>();
            refused = 0;
            string trigger = table == StoneTable ? "itemindex" : "moveindex";
            Func<string, int?> lookup = HgEngineSourceFields.NameLookup(Headers);
            foreach (CInitItem item in CSourceFile.For(text).Find(table)?.Init?.Items ?? new List<CInitItem>())
            {
                int? Field(string name) => item.List?.Field(name) is CInitItem field
                    && HgEngineSourceExpression.TryEvaluate(field.ValueText(text), lookup, out int value) ? value : null;

                if (Field("monindex") is int species && Field(trigger) is int by && Field("targetForm") is int target && Field("baseForm") is int from)
                    rows.Add(new Row(species, by, target, from));
                else refused++;
            }
            return rows;
        }
    }
}
