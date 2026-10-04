using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace DSPRE.HgEngine
{
    /// <summary>
    /// hg-engine's marts under MART_EXPANSION: src/field/mart.c's sBadgeMart { item, required badges } replaces the
    /// badge-gated mart, and each specialty mart is its own 0xFFFF-ended item array, put in the game's slot by the
    /// repoints file. The slots are the game's, so marts can't be added, but every list can be any length.
    /// </summary>
    public static class HgEngineMarts
    {
        public const string SourceRelPath = "src/field/mart.c";
        private const string RepointsRelPath = "repoints";
        private const string ItemHeaderRelPath = "include/constants/item.h";
        /// <summary>The note in mart.c: the badge mart's list can't pass this.</summary>
        public const int BadgeMartLimit = 203;

        public sealed record Specialty(string Array, List<int> Items);

        public static bool Enabled =>
            HgEngineProject.IsActive && File.Exists(Path(SourceRelPath)) && HgEngineOwnedFiles.ConfigEnabled(HgEngineProject.RepoPathUnc, "MART_EXPANSION");

        public static bool TryRead(out List<(int Item, int Badges)> badgeMart, out List<Specialty> specialty, out string error)
        {
            badgeMart = null; specialty = null;
            if (!TryLoad(out string text, out var items, out var order, out error)) return false;
            error = Parse(text, items, order, out badgeMart, out specialty, out _, out _);
            return error == null;
        }

        private static string Parse(string text, HgEngineSymbolTable items, List<string> order, out List<(int Item, int Badges)> badgeMart,
            out List<Specialty> specialty, out CDeclaration badgeDecl, out List<CDeclaration> arrays)
        {
            badgeMart = new List<(int, int)>(); specialty = new List<Specialty>(); arrays = new List<CDeclaration>();
            var file = CSourceFile.For(text);
            badgeDecl = file.Find("sBadgeMart");
            if (badgeDecl == null) return $"{SourceRelPath} has no sBadgeMart.";
            foreach (var row in badgeDecl.Init.Items)
            {
                if (row.IsConditional) return $"{SourceRelPath}: a badge mart row sits under #if, which DSPRE doesn't edit.";
                if (row.List == null || row.List.Items.Count != 2) return $"{SourceRelPath}: {row.ValueText(text)} isn't {{ item, badges }}.";
                int item = Value(row.List.Items[0].ValueText(text).Trim(), items), badges = Value(row.List.Items[1].ValueText(text).Trim(), null);
                if (item < 0 || badges < 0) return $"{SourceRelPath}: {row.ValueText(text)} couldn't be read.";
                badgeMart.Add((item, badges));
            }
            foreach (string array in order)
            {
                var decl = file.Find(array);
                if (decl == null) return $"{SourceRelPath} has no {array}, which {RepointsRelPath} puts in a mart slot.";
                arrays.Add(decl);
                var list = new List<int>();
                foreach (var entry in decl.Init.Items)
                {
                    if (entry.IsConditional) return $"{SourceRelPath}: {array} has an item under #if, which DSPRE doesn't edit.";
                    string token = entry.ValueText(text).Trim();
                    int item = Value(token, items);
                    if (item == 0xFFFF) break;
                    if (item < 0) return $"{SourceRelPath}: {array} names \"{token}\", which isn't an item.";
                    list.Add(item);
                }
                specialty.Add(new Specialty(array, list));
            }
            return null;
        }

        /// <summary>Writes the lists that changed, keeping each item's spelling where it stays at the same place.</summary>
        public static bool TryWrite(IReadOnlyList<(int Item, int Badges)> badgeMart, IReadOnlyList<IReadOnlyList<int>> specialty, out string error)
        {
            if (!TryLoad(out string text, out var items, out var order, out error)) return false;
            if ((error = Parse(text, items, order, out var oldBadge, out var oldSpecialty, out var badgeDecl, out var arrays)) != null) return false;
            if (specialty.Count != oldSpecialty.Count) { error = "hg-engine's specialty marts are fixed slots; their number can't change."; return false; }
            if (badgeMart.Count > BadgeMartLimit) { error = $"The badge mart can list at most {BadgeMartLimit} items."; return false; }
            string Name(int item, string kept) => kept != null && Value(kept, items) == item ? kept
                : items.TryGetNameWithPrefix(item, "ITEM_", out string n) ? n : item.ToString();

            var edits = new List<(int Start, int End, string Text)>();
            if (!badgeMart.SequenceEqual(oldBadge))
            {
                // Rows change in place, so their comments stay; new ones follow the last, removed ones lose their line.
                var rows = badgeDecl.Init.Items;
                string indent = rows.Count > 0 ? HgEngineSwarms.Indent(text, rows[0].Start) : "    ";
                for (int i = 0; i < Math.Min(rows.Count, badgeMart.Count); i++)
                    if (oldBadge[i] != badgeMart[i])
                        edits.Add((rows[i].Start, rows[i].End, $"{{ {Name(badgeMart[i].Item, rows[i].List.Items[0].ValueText(text).Trim())}, {badgeMart[i].Badges} }}"));
                if (badgeMart.Count > rows.Count)
                {
                    int at = rows.Count > 0 ? HgEngineSwarms.LineEnd(text, rows[^1].End) : badgeDecl.Init.Open + 1;
                    edits.Add((at, at, string.Concat(badgeMart.Skip(rows.Count).Select(r => $"\n{indent}{{ {Name(r.Item, null)}, {r.Badges} }},"))));
                }
                for (int i = badgeMart.Count; i < rows.Count; i++)
                    edits.Add((HgEngineSwarms.LineStart(text, rows[i].Start) - 1, HgEngineSwarms.LineEnd(text, rows[i].End), ""));
            }
            for (int s = 0; s < specialty.Count; s++)
            {
                if (specialty[s].SequenceEqual(oldSpecialty[s].Items)) continue;
                var init = arrays[s].Init;
                var tokens = init.Items.Select(i => i.ValueText(text).Trim()).ToList();
                string indent = init.Items.Count > 0 ? HgEngineSwarms.Indent(text, init.Items[0].Start) : "    ";
                var names = specialty[s].Select((item, i) => Name(item, i < tokens.Count ? tokens[i] : null)).Append("0xFFFF");
                edits.Add((init.Open + 1, init.Close, "\n" + indent + string.Join(", ", names) + "\n"));
            }
            if (edits.Count == 0) return true;
            foreach (var e in edits.OrderByDescending(e => e.Start)) text = text.Substring(0, e.Start) + e.Text + text.Substring(e.End);

            try
            {
                return HgEngineVerifiedWrite.TryWrite(Path(SourceRelPath), SourceRelPath, text, written =>
                {
                    string problem = Parse(written, items, order, out var badge, out var spec, out _, out _);
                    if (problem != null) return problem;
                    if (!badge.SequenceEqual(badgeMart)) return "the badge mart differs";
                    for (int s = 0; s < specialty.Count; s++) if (!spec[s].Items.SequenceEqual(specialty[s])) return $"{spec[s].Array} differs";
                    return null;
                }, out error);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { error = ex.Message; return false; }
        }

        private static bool TryLoad(out string text, out HgEngineSymbolTable items, out List<string> order, out string error)
        {
            text = null; order = null; error = null;
            items = HgEngineSymbolTable.Load(ItemHeaderRelPath);
            if (!Enabled) { error = "This checkout doesn't build its marts from source (MART_EXPANSION is off)."; return false; }
            if (items == null || !File.Exists(Path(RepointsRelPath))) { error = $"{ItemHeaderRelPath} or {RepointsRelPath} is missing from the checkout."; return false; }
            text = HgEngineFileCache.GetText(Path(SourceRelPath)).Replace("\r\n", "\n");
            // The specialty slots, in the game's order: the repoints lines inside MART_EXPANSION, by address.
            string repoints = HgEngineFileCache.GetText(Path(RepointsRelPath));
            Match block = Regex.Match(repoints, @"#ifdef\s+MART_EXPANSION\b(.*?)#endif", RegexOptions.Singleline);
            if (!block.Success) { error = $"{RepointsRelPath} has no MART_EXPANSION block."; return false; }
            order = Regex.Matches(block.Groups[1].Value, @"^\s*arm9\s+(\w+)\s+([0-9A-Fa-f]{8})\s*$", RegexOptions.Multiline)
                .Select(m => (Name: m.Groups[1].Value, At: Convert.ToUInt32(m.Groups[2].Value, 16)))
                .OrderBy(x => x.At).Select(x => x.Name).ToList();
            if (order.Count == 0) { error = $"{RepointsRelPath} repoints no marts."; return false; }
            return true;
        }

        private static int Value(string token, HgEngineSymbolTable symbols)
        {
            if (token == null) return -1;
            if (token.StartsWith("0x", StringComparison.OrdinalIgnoreCase) && int.TryParse(token.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out int hex)) return hex;
            if (int.TryParse(token, out int dec)) return dec;
            return symbols != null && symbols.TryGetValue(token, out int v) ? v : -1;
        }

        private static string Path(string rel) => System.IO.Path.Combine(HgEngineProject.RepoPathUnc, rel.Replace('/', System.IO.Path.DirectorySeparatorChar));
    }
}
