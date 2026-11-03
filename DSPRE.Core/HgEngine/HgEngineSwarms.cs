using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DSPRE.HgEngine
{
    /// <summary>
    /// hg-engine replaces the game's swarm table with src/swarms.c: GetSwarmInfoFromRand picks row rand %
    /// SWARM_MAP_COUNT of sSwarmMapLUT, each row a map header and SWARM_GRASS, SWARM_SURFING or SWARM_FISHING, and
    /// reads that map's walking, surfing or fishing swarm Pokémon from its encounters.
    /// </summary>
    public static class HgEngineSwarms
    {
        public const string RelPath = "src/swarms.c";
        private const string MapsH = "include/constants/maps.h";
        private const string Table = "sSwarmMapLUT";

        private const string CountName = "SWARM_MAP_COUNT";

        private static string FilePath => Path.Combine(HgEngineProject.RepoPathUnc, RelPath.Replace('/', Path.DirectorySeparatorChar));

        /// <summary>Why the source can't be used, or null.</summary>
        public static string WhyNot() => HgEngineProject.IsActive && !File.Exists(FilePath) ? $"{RelPath} is missing from the checkout." : null;

        private sealed class Parsed
        {
            public CSourceFile File;
            public CDeclaration Table;
            public List<(string Header, string Method)> Raw = new();
            public List<(ushort Header, ushort Method)> Rows = new();
            public Dictionary<string, int> Methods = new();
        }

        private static string TryParse(string text, out Parsed parsed)
        {
            Parsed p = parsed = new Parsed { File = new CSourceFile(text) };
            p.Table = p.File.Find(Table);
            CDirective count = p.File.FindDefine(CountName);
            if (p.Table == null || count == null) return $"{RelPath} has no {Table} or {CountName} that DSPRE can read.";
            foreach (CDirective d in p.File.Directives)
                if (d.Keyword == "define" && d.Name != CountName && d.Name?.StartsWith("SWARM_", StringComparison.Ordinal) == true && int.TryParse(d.Body, out int method))
                    p.Methods[d.Name] = method;

            HgEngineSymbolTable maps = HgEngineSymbolTable.Load(MapsH);
            int? Map(string name) => maps != null && maps.TryGetValue(name, out int v) ? v : null;
            foreach (CInitItem item in parsed.Table.Init.Items)
            {
                if (item.IsConditional) return $"{RelPath}: a swarm row sits under #if, which DSPRE doesn't edit.";
                if (item.List == null || item.List.Items.Count != 2) return $"{RelPath}: swarm row {parsed.Raw.Count} isn't {{ map, kind }}.";
                string header = item.List.Items[0].ValueText(text), method = item.List.Items[1].ValueText(text);
                parsed.Raw.Add((header, method));
                if (!HgEngineSourceExpression.TryEvaluate(header, Map, out int h) || h < 0 || h > ushort.MaxValue) return $"{RelPath}: map {header} couldn't be read.";
                if (!HgEngineSourceExpression.TryEvaluate(method, n => p.Methods.TryGetValue(n, out int v) ? v : null, out int m) || m < 0 || m > 2) return $"{RelPath}: swarm kind {method} couldn't be read.";
                parsed.Rows.Add(((ushort)h, (ushort)m));
            }
            if (!int.TryParse(count.Body, out int n) || n != parsed.Rows.Count)
                return $"{RelPath} declares {count.Body} swarm rows but lists {parsed.Rows.Count}.";
            return null;
        }

        private static bool TryLoadParsed(out Parsed parsed, out string error)
        {
            parsed = null;
            error = WhyNot();
            if (error != null) return false;
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout is linked."; return false; }
            // Edits work on LF text; the writer puts the file's own line endings back.
            error = TryParse(HgEngineFileCache.GetText(FilePath).Replace("\r\n", "\n"), out parsed);
            return error == null;
        }

        public static bool TryRead(out List<(ushort Header, ushort Method)> rows, out string error)
        {
            rows = new List<(ushort, ushort)>();
            if (!TryLoadParsed(out Parsed p, out error)) return false;
            rows = p.Rows;
            return true;
        }

        /// <summary>Rewrites the rows and SWARM_MAP_COUNT. A row whose values didn't change keeps its text.</summary>
        public static bool TryWrite(IReadOnlyList<(ushort Header, ushort Method)> rows, out string error)
        {
            if (rows.Count == 0 || rows.Count > 256) { error = "hg-engine picks a swarm with a byte, so it needs 1 to 256 rows."; return false; }
            if (!TryLoadParsed(out Parsed p, out error)) return false;
            if (p.Rows.SequenceEqual(rows)) return true;

            string text = p.File.Text;
            HgEngineSymbolTable maps = HgEngineSymbolTable.Load(MapsH);
            string MapName(ushort v) => maps?.TryGetNameWithPrefix(v, "MAP_", out string n) == true ? n : v.ToString();
            string MethodName(ushort v) => p.Methods.FirstOrDefault(kv => kv.Value == v).Key ?? v.ToString();
            List<CInitItem> items = p.Table.Init.Items;
            string indent = items.Count > 0 ? HgEngineSourcePatcher.Indent(text, items[0].Start) : "    ";

            // Changed rows are replaced in place and new ones go after the last, so comments stay where they were.
            List<(int Start, int End, string Text)> edits = new List<(int Start, int End, string Text)>();
            for (int i = 0; i < Math.Min(rows.Count, items.Count); i++)
                if (p.Rows[i] != rows[i]) edits.Add((items[i].Start, items[i].End, $"{{ {MapName(rows[i].Header)}, {MethodName(rows[i].Method)} }}"));
            if (rows.Count > items.Count)
            {
                int at = items.Count > 0 ? HgEngineSourcePatcher.LineEnd(text, items[^1].End) : p.Table.Init.Open + 1;
                edits.Add((at, at, string.Concat(rows.Skip(items.Count).Select(r => $"\n{indent}{{ {MapName(r.Header)}, {MethodName(r.Method)} }},"))));
            }
            for (int i = rows.Count; i < items.Count; i++)
            {
                int start = HgEngineSourcePatcher.LineStart(text, items[i].Start), end = HgEngineSourcePatcher.LineEnd(text, items[i].End);
                edits.Add((start - 1, end, ""));
            }
            foreach ((int Start, int End, string Text) e in edits.OrderByDescending(e => e.Start)) text = text.Substring(0, e.Start) + e.Text + text.Substring(e.End);
            if (!HgEngineHeaderEditor.TryReplaceDefineValue(ref text, CountName, rows.Count.ToString()))
            { error = $"{RelPath} has no {CountName}."; return false; }

            return HgEngineVerifiedWrite.TryWrite(FilePath, RelPath, text, written =>
                TryParse(written, out Parsed back) ?? (back.Rows.SequenceEqual(rows) ? null : "the rows differ"), out error, keepLostComments: false);
        }

    }
}
