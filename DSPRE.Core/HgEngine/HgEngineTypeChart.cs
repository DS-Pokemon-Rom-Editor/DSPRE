using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DSPRE.HgEngine
{
    /// <summary>
    /// hg-engine's type chart, src/battle/battle_pokemon.c's TypeEffectivenessTable, which its battle code is
    /// repointed to. Rows run in three parts: ordinary matchups, then immunities Ring Target lifts after the
    /// TYPE_RING_TARGET row, then those Foresight lifts too after TYPE_FORESIGHT. Rows an #if switches off are
    /// left alone; a save changes, removes or adds single rows.
    /// </summary>
    public static class HgEngineTypeChart
    {
        public const string SourceRelPath = "src/battle/battle_pokemon.c";
        private const string ConstantsRelPath = "include/constants/battle_constants.h";
        private const string TableName = "TypeEffectivenessTable";

        public enum Section { Main, RingTarget, Foresight }

        public sealed record Row(int Attacker, int Defender, int Tenths, Section Section);

        private sealed class Table
        {
            public string Text;
            public CDeclaration Decl;
            public readonly List<(Row Row, CInitItem Item)> Rows = new();
            public readonly CInitItem[] Marker = new CInitItem[3];   // the ring target, foresight and end rows
        }

        private static string FilePath => Path.Combine(HgEngineProject.RepoPathUnc, SourceRelPath.Replace('/', Path.DirectorySeparatorChar));

        public static bool TryRead(out List<Row> rows, out string error)
        {
            rows = null;
            if (!TryLoad(out var table, out error)) return false;
            rows = table.Rows.Select(r => r.Row).ToList();
            return true;
        }

        /// <summary>Writes the chart as <paramref name="wanted"/> lists it: every pair not listed is neutral.</summary>
        public static bool TryWrite(IReadOnlyList<Row> wanted, out string error)
        {
            if (!TryLoad(out var table, out error)) return false;
            var constants = HgEngineSymbolTable.Load(ConstantsRelPath);
            string text = table.Text;
            // A type is written the way the table already spells it, else by its TYPE_ name.
            var spelled = new Dictionary<int, string>();
            foreach (var (row, item) in table.Rows)
            {
                spelled.TryAdd(row.Attacker, item.List.Items[0].ValueText(text).Trim());
                spelled.TryAdd(row.Defender, item.List.Items[1].ValueText(text).Trim());
            }
            string Type(int t) => spelled.TryGetValue(t, out string s) ? s
                : constants?.ByName.Where(kv => kv.Value == t && kv.Key.StartsWith("TYPE_", StringComparison.Ordinal)
                      && !kv.Key.StartsWith("TYPE_MUL_", StringComparison.Ordinal) && !kv.Key.Contains("TARGET") && !kv.Key.Contains("FORESIGHT") && !kv.Key.Contains("ENDTABLE"))
                    .Select(kv => kv.Key).OrderBy(k => k.Length).FirstOrDefault() ?? t.ToString();
            string Mul(int tenths) => constants != null && constants.TryGetNameWithPrefix(tenths, "TYPE_MUL_", out string n) ? n : tenths.ToString();
            string Entry(Row r) => $"{{ {Type(r.Attacker)}, {Type(r.Defender)}, {Mul(r.Tenths)} }},";

            var edits = new List<(int Start, int End, string Text)>();
            var add = new Dictionary<Section, List<string>> { [Section.Main] = new(), [Section.RingTarget] = new(), [Section.Foresight] = new() };
            var byPair = wanted.GroupBy(w => (w.Attacker, w.Defender)).ToDictionary(g => g.Key, g => g.Last());
            var seen = new HashSet<(int, int)>();

            foreach (var (row, item) in table.Rows)
            {
                var pair = (row.Attacker, row.Defender);
                if (!seen.Add(pair)) continue;
                if (!byPair.TryGetValue(pair, out var want)) { edits.Add(Removal(text, item)); continue; }
                if (want.Section != row.Section) { edits.Add(Removal(text, item)); add[want.Section].Add(Entry(want)); continue; }
                if (want.Tenths == row.Tenths) continue;
                // Keeps the row's own spelling of its types.
                var mul = item.List.Items[2];
                edits.Add((mul.ValueStart, mul.ValueEnd, Mul(want.Tenths)));
            }
            foreach (var want in byPair.Values)
                if (!seen.Contains((want.Attacker, want.Defender))) add[want.Section].Add(Entry(want));

            if (edits.Count == 0 && add.Values.All(a => a.Count == 0)) return true;
            if (table.Marker.Any(m => m == null)) { error = $"{TableName} in {SourceRelPath} doesn't have its ring target, foresight and end rows."; return false; }

            // New rows go right before the next part's marker, indented like it.
            string indent = HgEngineSwarms.Indent(text, table.Marker[0].Start);
            for (int s = 0; s < 3; s++)
                if (add[(Section)s].Count > 0)
                {
                    int at = HgEngineSwarms.LineStart(text, table.Marker[s].Start);
                    edits.Add((at, at, string.Concat(add[(Section)s].Select(e => indent + e + "\n"))));
                }
            foreach (var e in edits.OrderByDescending(e => e.Start).ThenByDescending(e => e.End)) text = text.Substring(0, e.Start) + e.Text + text.Substring(e.End);

            var expected = byPair.Values.ToDictionary(w => (w.Attacker, w.Defender));
            try
            {
                return HgEngineVerifiedWrite.TryWrite(FilePath, SourceRelPath, text, written =>
                {
                    string problem = Parse(written, out var back);
                    if (problem != null) return problem;
                    var got = back.Rows.GroupBy(r => (r.Row.Attacker, r.Row.Defender)).ToDictionary(g => g.Key, g => g.First().Row);
                    if (got.Count != expected.Count || got.Any(kv => !expected.TryGetValue(kv.Key, out var w) || w.Tenths != kv.Value.Tenths || w.Section != kv.Value.Section))
                        return "the matchups differ";
                    return null;
                }, out error);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { error = ex.Message; return false; }
        }

        // A row alone on its line goes with its line; otherwise just the row and its comma.
        private static (int, int, string) Removal(string text, CInitItem item)
        {
            int lineStart = HgEngineSwarms.LineStart(text, item.Start), lineEnd = HgEngineSwarms.LineEnd(text, item.End);
            int end = item.End;
            while (end < text.Length && (text[end] == ' ' || text[end] == '\t')) end++;
            if (end < text.Length && text[end] == ',') end++;
            string before = text.Substring(lineStart, item.Start - lineStart), after = text.Substring(end, lineEnd - end);
            bool alone = before.Trim().Length == 0 && (after.Trim().Length == 0 || after.TrimStart().StartsWith("//"));
            return alone ? (lineStart, Math.Min(text.Length, lineEnd + 1), "") : (item.Start, end, "");
        }

        private static string Parse(string text, out Table table)
        {
            table = new Table { Text = text };
            var constants = HgEngineSymbolTable.Load(ConstantsRelPath);
            if (constants == null) return $"{ConstantsRelPath} is missing from the checkout.";
            table.Decl = CSourceFile.For(text).Find(TableName);
            if (table.Decl == null) return $"{SourceRelPath} has no {TableName}.";

            int ring = constants.TryGetValue("TYPE_RING_TARGET", out int r) ? r : 0xFD;
            int foresight = constants.TryGetValue("TYPE_FORESIGHT", out int f) ? f : 0xFE;
            int end = constants.TryGetValue("TYPE_ENDTABLE", out int e) ? e : 0xFF;
            var section = Section.Main;
            foreach (var item in table.Decl.Init.Items)
            {
                // A row an #if switches off is left as it is; one DSPRE can't decide about counts as compiled.
                if (item.IsConditional && HgEngineConfigState.Compiles(item.Conditions) == false) continue;
                if (item.List == null || item.List.Items.Count != 3) return $"{TableName}: {item.ValueText(text)} isn't {{ attacker, defender, multiplier }}.";
                int[] v = item.List.Items.Select(i => HgEngineSourceExpression.TryEvaluate(i.ValueText(text), n => constants.TryGetValue(n, out int x) ? x : null, out int x2) ? x2 : -1).ToArray();
                if (v.Any(x => x < 0)) return $"{TableName}: {item.ValueText(text)} couldn't be read.";
                if (v[0] == ring) { table.Marker[0] = item; section = Section.RingTarget; continue; }
                if (v[0] == foresight) { table.Marker[1] = item; section = Section.Foresight; continue; }
                if (v[0] == end) { table.Marker[2] = item; continue; }
                table.Rows.Add((new Row(v[0], v[1], v[2], section), item));
            }
            return null;
        }

        private static bool TryLoad(out Table table, out string error)
        {
            table = null;
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout linked."; return false; }
            if (!File.Exists(FilePath)) { error = $"{SourceRelPath} is missing from the checkout."; return false; }
            error = Parse(HgEngineFileCache.GetText(FilePath).Replace("\r\n", "\n"), out table);
            return error == null;
        }
    }
}
