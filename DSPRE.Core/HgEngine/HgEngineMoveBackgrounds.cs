using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DSPRE.HgEngine
{
    /// <summary>
    /// data/BackgroundGfx.c: sBackgroundGfxIds[][5], the move-effect background table hg-engine builds into a028 9_17
    /// and reads in place of the game's (ov07_0221FB7C in src/battle/battle_input.c), as u16s.
    /// </summary>
    public static class HgEngineMoveBackgrounds
    {
        public const string RelPath = "data/BackgroundGfx.c";
        private const string Table = "sBackgroundGfxIds";

        private static string FilePath => Path.Combine(HgEngineProject.RepoPathUnc, RelPath.Replace('/', Path.DirectorySeparatorChar));

        private static string Parse(string text, out CDeclaration table, out List<int[]> rows)
        {
            rows = new List<int[]>();
            table = new CSourceFile(text).Find(Table);
            if (table == null) return $"{RelPath} has no {Table} DSPRE can read.";
            foreach (CInitItem item in table.Init.Items)
            {
                if (item.IsConditional) return $"{RelPath}: background {rows.Count} sits under #if, which DSPRE doesn't edit.";
                if (item.List == null || item.List.Items.Count != 5) return $"{RelPath}: background {rows.Count} doesn't have 5 values.";
                int[] values = new int[5];
                for (int i = 0; i < 5; i++)
                {
                    string v = item.List.Items[i].ValueText(text);
                    if (!HgEngineSourceExpression.TryEvaluate(v, _ => null, out values[i]) || values[i] < 0 || values[i] > ushort.MaxValue)
                        return $"{RelPath}: background {rows.Count} has {v}, which DSPRE can't read.";
                }
                rows.Add(values);
            }
            return null;
        }

        private static bool TryLoad(out string text, out CDeclaration table, out List<int[]> rows, out string error)
        {
            text = null; table = null; rows = new List<int[]>();
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout is linked."; return false; }
            if (!File.Exists(FilePath)) { error = $"{RelPath} is missing from the checkout."; return false; }
            text = HgEngineFileCache.GetText(FilePath).Replace("\r\n", "\n");
            error = Parse(text, out table, out rows);
            return error == null;
        }

        public static bool TryRead(out List<int[]> rows, out string error) => TryLoad(out _, out _, out rows, out error);

        /// <summary>Changes rows in place and adds or removes them at the end, so each row keeps its comment.</summary>
        public static bool TryWrite(IReadOnlyList<int[]> rows, out string error)
        {
            if (!TryLoad(out string text, out CDeclaration table, out List<int[]> current, out error)) return false;
            if (rows.Count == current.Count && rows.Zip(current).All(z => z.First.SequenceEqual(z.Second))) return true;
            if (rows.Count == 0 || rows.Any(r => r.Length != 5 || r.Any(v => v < 0 || v > ushort.MaxValue))) { error = "Each background needs five values from 0 to 65535."; return false; }

            List<CInitItem> items = table.Init.Items;
            string indent = items.Count > 0 ? HgEngineSwarms.Indent(text, items[0].Start) : "    ";
            static string Literal(int[] r) => "{ " + string.Join(", ", r.Select(v => v.ToString().PadLeft(3))) + " }";
            List<(int Start, int End, string Text)> edits = new List<(int Start, int End, string Text)>();
            for (int i = 0; i < Math.Min(rows.Count, items.Count); i++)
                if (!current[i].SequenceEqual(rows[i])) edits.Add((items[i].Start, items[i].End, Literal(rows[i])));
            if (rows.Count > items.Count)
            {
                int at = items.Count > 0 ? HgEngineSwarms.LineEnd(text, items[^1].End) : table.Init.Open + 1;
                edits.Add((at, at, string.Concat(rows.Skip(items.Count).Select(r => "\n" + indent + Literal(r) + ","))));
            }
            for (int i = rows.Count; i < items.Count; i++)
                edits.Add((HgEngineSwarms.LineStart(text, items[i].Start) - 1, HgEngineSwarms.LineEnd(text, items[i].End), ""));
            foreach ((int Start, int End, string Text) e in edits.OrderByDescending(e => e.Start)) text = text.Substring(0, e.Start) + e.Text + text.Substring(e.End);

            // A removed background's comment goes with it.
            return HgEngineVerifiedWrite.TryWrite(FilePath, RelPath, text, written =>
                Parse(written, out _, out List<int[]> back) ?? (back.Count == rows.Count && back.Zip(rows).All(z => z.First.SequenceEqual(z.Second)) ? null : "the rows differ"),
                out error, keepLostComments: false);
        }
    }
}
