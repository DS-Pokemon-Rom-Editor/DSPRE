using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace DSPRE.HgEngine
{
    /// <summary>
    /// data/AbilityFlags.c: one AbilityFlags bitfield per ability (include/battle.h), built into a028 9_19 and read by
    /// GetAbilityFlags for Mold Breaker, Neutralizing Gas, Trace, Skill Swap, Receiver, Entrainment, Role Play and the
    /// other checks. The flag names are read from the struct, so a checkout that adds one gets it too.
    /// </summary>
    public static class HgEngineAbilityFlags
    {
        public const string RelPath = "data/AbilityFlags.c";
        private const string StructHeader = "include/battle.h";
        private const string AbilityH = "include/constants/ability.h";
        private const string Table = "sAbilityFlags";

        private static readonly Regex Struct = new(@"typedef\s+struct\s+AbilityFlags\s*\{(.*?)\}\s*AbilityFlags\s*;", RegexOptions.Singleline);
        private static readonly Regex Bit = new(@"\bu\d+\s+(\w+)\s*:\s*1\s*;");

        private static string PathOf(string rel) => Path.Combine(HgEngineProject.RepoPathUnc, rel.Replace('/', Path.DirectorySeparatorChar));

        /// <summary>The struct's one-bit flags in declaration order, without the unused padding.</summary>
        public static List<string> FlagNames()
        {
            var names = new List<string>();
            if (!HgEngineProject.IsActive || !File.Exists(PathOf(StructHeader))) return names;
            string header = Regex.Replace(HgEngineFileCache.GetText(PathOf(StructHeader)), @"//[^\n]*|/\*.*?\*/", "", RegexOptions.Singleline);
            var m = Struct.Match(header);
            if (m.Success)
                names.AddRange(Bit.Matches(m.Groups[1].Value).Select(b => b.Groups[1].Value).Where(n => !n.StartsWith("unused", StringComparison.Ordinal)));
            return names;
        }

        private sealed class Entry
        {
            public int Id;
            public CInitItem Item;
            public HashSet<string> Flags;
            public bool Understood;
        }

        private static string Parse(string text, HgEngineSymbolTable abilities, HashSet<string> known, out CDeclaration table, out List<Entry> entries)
        {
            entries = new List<Entry>();
            table = new CSourceFile(text).Find(Table);
            if (table == null) return $"{RelPath} has no {Table} DSPRE can read.";
            foreach (var item in table.Init.Items)
            {
                if (item.IsConditional) return $"{RelPath}: an entry sits under #if, which DSPRE doesn't edit.";
                string designator = item.IndexText?.Trim();
                if (designator == null || !abilities.TryGetValue(designator, out int id)) return $"{RelPath}: {designator ?? item.ValueText(text)} isn't an ability in {AbilityH}.";
                var flags = new HashSet<string>();
                bool understood = item.List != null;
                foreach (var field in item.List?.Items ?? new List<CInitItem>())
                {
                    string value = field.ValueText(text).Trim();
                    if (field.FieldName == null) { if (value != "0") understood = false; continue; }
                    if (!known.Contains(field.FieldName)) { understood = false; continue; }
                    if (value is "TRUE" or "1") flags.Add(field.FieldName);
                    else if (value is not ("FALSE" or "0")) understood = false;
                }
                entries.Add(new Entry { Id = id, Item = item, Flags = flags, Understood = understood });
            }
            return null;
        }

        private static bool TryLoad(out string text, out CDeclaration table, out List<Entry> entries, out HgEngineSymbolTable abilities, out string error)
        {
            text = null; table = null; entries = new List<Entry>(); abilities = null;
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout is linked."; return false; }
            if (!File.Exists(PathOf(RelPath))) { error = $"{RelPath} is missing from the checkout."; return false; }
            abilities = HgEngineSymbolTable.Load(AbilityH);
            if (abilities == null) { error = $"{AbilityH} could not be read."; return false; }
            var known = new HashSet<string>(FlagNames());
            if (known.Count == 0) { error = $"{StructHeader} has no AbilityFlags bitfield DSPRE can read."; return false; }
            text = HgEngineFileCache.GetText(PathOf(RelPath)).Replace("\r\n", "\n");
            error = Parse(text, abilities, known, out table, out entries);
            return error == null;
        }

        /// <summary>Each ability's set flags, by ability id. Abilities the file leaves out have none.</summary>
        public static bool TryRead(out Dictionary<int, HashSet<string>> flags, out string error)
        {
            flags = new Dictionary<int, HashSet<string>>();
            if (!TryLoad(out _, out _, out var entries, out _, out error)) return false;
            foreach (var e in entries) flags[e.Id] = new HashSet<string>(e.Flags);
            return true;
        }

        /// <summary>Rewrites the entries of the abilities given, in the struct's flag order; an ability without an
        /// entry gets one in id order. An entry DSPRE couldn't fully read is refused rather than rewritten.</summary>
        public static bool TryWrite(IReadOnlyDictionary<int, HashSet<string>> changes, out string error)
        {
            if (!TryLoad(out string text, out var table, out var entries, out var abilities, out error)) return false;
            var order = FlagNames();
            string Literal(HashSet<string> set) => set.Count == 0 ? "{ 0 }"
                : "{ " + string.Join(", ", order.Where(set.Contains).Select(n => $".{n} = TRUE")) + " }";

            var edits = new List<(int Start, int End, string Text)>();
            string indent = entries.Count > 0 ? HgEngineSwarms.Indent(text, entries[0].Item.Start) : "    ";
            foreach (var (id, set) in changes.OrderBy(c => c.Key))
            {
                if (set.Any(f => !order.Contains(f))) { error = $"Ability {id} has a flag {StructHeader} doesn't declare."; return false; }
                var entry = entries.FirstOrDefault(e => e.Id == id);
                if (entry != null)
                {
                    if (entry.Flags.SetEquals(set)) continue;
                    if (!entry.Understood) { error = $"{RelPath}: ability {id}'s entry has values DSPRE can't rewrite safely."; return false; }
                    edits.Add((entry.Item.List.Open, entry.Item.List.Close + 1, Literal(set)));
                    continue;
                }
                if (set.Count == 0) continue;
                if (!abilities.TryGetNameWithPrefix(id, "ABILITY_", out string name)) { error = $"Ability {id} has no ABILITY_ name in {AbilityH}."; return false; }
                var next = entries.Where(e => e.Id > id).OrderBy(e => e.Item.Start).FirstOrDefault();
                int at = next != null ? HgEngineSwarms.LineStart(text, next.Item.Start) : HgEngineSwarms.LineStart(text, table.Init.Close);
                edits.Add((at, at, $"{indent}[{name}] = {Literal(set)},\n"));
            }
            if (edits.Count == 0) return true;
            foreach (var e in edits.OrderByDescending(e => e.Start).ThenByDescending(e => e.End)) text = text.Substring(0, e.Start) + e.Text + text.Substring(e.End);

            var known = new HashSet<string>(order);
            return HgEngineVerifiedWrite.TryWrite(PathOf(RelPath), RelPath, text, written =>
            {
                string problem = Parse(written, abilities, known, out _, out var back);
                if (problem != null) return problem;
                foreach (var (id, set) in changes)
                    if (!(back.FirstOrDefault(e => e.Id == id)?.Flags ?? new HashSet<string>()).SetEquals(set)) return $"ability {id} reads back differently";
                return null;
            }, out error);
        }
    }
}
