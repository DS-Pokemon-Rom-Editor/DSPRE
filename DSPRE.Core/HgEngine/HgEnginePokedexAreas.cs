using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE.ROMFiles;

namespace DSPRE.HgEngine
{
    /// <summary>
    /// data/PokedexArea.c, which pokedexdatagen builds into a/1/3/3: sPokedexAreaLists names one u32 array per member,
    /// in the game's order (<see cref="PokedexAreaData"/>) but counted from 0, the build adding the two map members.
    /// Each array lists DEX_* area ids and ends in DEX_END_AREA_DATA. The special areas are the designators of
    /// sPokedexAreaSpecialMapInfo and the routes and towns those of sPokedexAreaOverworldMapInfo.entries, which share
    /// numbers, so a new id is spelled from its own group.
    /// </summary>
    public static class HgEnginePokedexAreas
    {
        public const string RelPath = "data/PokedexArea.c";
        private const string PokedexH = "include/constants/pokedex.h";

        private static string FilePath => Path.Combine(HgEngineProject.RepoPathUnc, RelPath.Replace('/', Path.DirectorySeparatorChar));

        private sealed class Parsed
        {
            public string Text;
            public CSourceFile File;
            public Dictionary<int, string> Arrays = new();
            public int Base, Stride;
            public Dictionary<int, string>[] Groups = { new(), new() };
            public int IndexOf(int list, int species) => Base + list * Stride + species;
        }

        private static string Parse(string text, HgEngineSymbolTable table, out Parsed p)
        {
            p = new Parsed { Text = text, File = CSourceFile.For(text) };
            var lists = p.File.Find("sPokedexAreaLists");
            if (lists == null) return $"{RelPath} has no sPokedexAreaLists.";
            foreach (var item in lists.Init.Items)
                if (item.Position >= 0 && item.List != null && item.List.Items.Count > 0)
                    p.Arrays[item.Position] = item.List.Items[0].ValueText(text).Trim();

            // Species 0's first two lists give where the lists start and how many species each kind holds.
            int m0 = p.Arrays.Where(kv => kv.Value == "sPokedexAreaSpecialAreasMorning_None").Select(kv => kv.Key).DefaultIfEmpty(-1).First();
            int d0 = p.Arrays.Where(kv => kv.Value == "sPokedexAreaSpecialAreasDay_None").Select(kv => kv.Key).DefaultIfEmpty(-1).First();
            if (m0 < 0 || d0 <= m0) return $"{RelPath}: sPokedexAreaLists has no _None lists to size the lists by.";
            p.Base = m0;
            p.Stride = d0 - m0;

            var special = p.File.Find("sPokedexAreaSpecialMapInfo")?.Init;
            var overworld = p.File.Find("sPokedexAreaOverworldMapInfo")?.Init.Field("entries")?.List;
            if (special == null || overworld == null) return $"{RelPath} has no sPokedexAreaSpecialMapInfo or sPokedexAreaOverworldMapInfo.entries.";
            var groups = new[] { special, overworld };
            for (int g = 0; g < 2; g++)
                foreach (var item in groups[g].Items)
                {
                    string symbol = item.IndexText?.Trim();
                    if (symbol != null && table.TryGetValue(symbol, out int id) && id > 0) p.Groups[g].TryAdd(id, symbol);
                }
            return null;
        }

        private static bool TryLoad(out Parsed p, out HgEngineSymbolTable table, out string error)
        {
            p = null;
            table = null;
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout is linked."; return false; }
            if (!File.Exists(FilePath)) { error = $"{RelPath} is missing from the checkout."; return false; }
            table = HgEngineSymbolTable.Load(PokedexH);
            if (table == null) { error = $"{PokedexH} could not be read."; return false; }
            error = Parse(HgEngineFileCache.GetText(FilePath).Replace("\r\n", "\n"), table, out p);
            return error == null;
        }

        /// <summary>Area ids and names for the special areas (group 0) and routes and towns (group 1).</summary>
        public static bool TryGetAreas(out List<(int Id, string Name)>[] groups, out string error)
        {
            groups = new[] { new List<(int, string)>(), new List<(int, string)>() };
            if (!TryLoad(out var p, out _, out error)) return false;
            string[][] friendly = { PokedexAreaData.SpecialAreaNames, PokedexAreaData.RouteAreaNames };
            for (int g = 0; g < 2; g++)
            {
                bool retail = p.Groups[g].Count == friendly[g].Length && p.Groups[g].Keys.Max() == friendly[g].Length;
                foreach (var (id, symbol) in p.Groups[g].OrderBy(kv => kv.Key))
                    groups[g].Add((id, retail ? friendly[g][id - 1] : Humanize(symbol)));
            }
            return true;
        }

        private static string ReadLists(Parsed p, HgEngineSymbolTable table, int species, out int[][] lists, out CInitList[] inits)
        {
            lists = new int[PokedexAreaData.ListCount][];
            inits = new CInitList[PokedexAreaData.ListCount];
            if (species < 0 || species >= p.Stride) return $"{RelPath} has no area lists for species {species}.";
            for (int k = 0; k < lists.Length; k++)
            {
                if (!p.Arrays.TryGetValue(p.IndexOf(k, species), out string array) || p.File.Find(array) is not CDeclaration decl)
                    return $"{RelPath} has no list {k} for species {species}.";
                inits[k] = decl.Init;
                var ids = new List<int>();
                foreach (var item in decl.Init.Items)
                {
                    if (item.IsConditional) return $"{RelPath}: {array} has an area under #if, which DSPRE doesn't edit.";
                    string token = item.ValueText(p.Text);
                    if (!HgEngineSourceExpression.TryEvaluate(token, n => table.TryGetValue(n, out int v) ? v : null, out int id))
                        return $"{RelPath}: {array} has {token}, which DSPRE can't read.";
                    if (id == 0) break;
                    ids.Add(id);
                }
                lists[k] = ids.ToArray();
            }
            return null;
        }

        public static bool TryLoad(int species, out int[][] lists, out string error)
        {
            lists = null;
            if (!TryLoad(out var p, out var table, out error)) return false;
            error = ReadLists(p, table, species, out lists, out _);
            return error == null;
        }

        /// <summary>Rewrites the species' lists that changed. Areas it already listed keep their spelling and order.</summary>
        public static bool TryWrite(int species, int[][] lists, out string error)
        {
            if (!TryLoad(out var p, out var table, out error)) return false;
            if ((error = ReadLists(p, table, species, out var current, out var inits)) != null) return false;
            string text = p.Text;
            var edits = new List<(int Start, int End, string Text)>();
            for (int k = 0; k < lists.Length; k++)
            {
                if (current[k].SequenceEqual(lists[k])) continue;
                var spelled = new Dictionary<int, string>();
                string end = "DEX_END_AREA_DATA";
                foreach (var item in inits[k].Items)
                {
                    string token = item.ValueText(text);
                    if (!HgEngineSourceExpression.TryEvaluate(token, n => table.TryGetValue(n, out int v) ? v : null, out int id)) continue;
                    if (id == 0) end = token; else spelled.TryAdd(id, token);
                }
                var group = p.Groups[k is 0 or 1 or 2 or 6 ? 0 : 1];
                string indent = inits[k].Items.Count > 0 ? HgEngineSwarms.Indent(text, inits[k].Items[0].Start) : "    ";
                var lines = lists[k].Select(id => spelled.TryGetValue(id, out string t) ? t : group.TryGetValue(id, out string n) ? n : id.ToString()).Append(end);
                string closeIndent = HgEngineSwarms.Indent(text, inits[k].Close);
                edits.Add((inits[k].Open + 1, inits[k].Close, "\n" + string.Concat(lines.Select(t => indent + t + ",\n")) + closeIndent));
            }
            if (edits.Count == 0) return true;
            foreach (var e in edits.OrderByDescending(e => e.Start)) text = text.Substring(0, e.Start) + e.Text + text.Substring(e.End);

            return HgEngineVerifiedWrite.TryWrite(FilePath, RelPath, text, written =>
            {
                string problem = Parse(written, table, out var back);
                if (problem != null) return problem;
                if ((problem = ReadLists(back, table, species, out var reread, out _)) != null) return problem;
                return reread.Zip(lists).All(z => z.First.SequenceEqual(z.Second)) ? null : "the lists differ";
            }, out error);
        }

        private static string Humanize(string symbol)
        {
            var words = symbol.Substring(4).ToLowerInvariant().Split('_', StringSplitOptions.RemoveEmptyEntries);
            return string.Join(" ", words.Select(w => char.ToUpperInvariant(w[0]) + w.Substring(1)));
        }
    }
}
