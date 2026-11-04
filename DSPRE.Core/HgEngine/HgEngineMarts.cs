using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
            if (!TryLoad(out string text, out HgEngineSymbolTable items, out List<string> order, out error)) return false;
            error = Parse(text, items, order, out badgeMart, out specialty, out _, out _);
            if (error == null) HgEngineLoadedEntries.Store(Path(SourceRelPath), LoadedKey, ListsText(badgeMart, specialty.Select(s => (IReadOnlyList<int>)s.Items)));
            return error == null;
        }

        private const string LoadedKey = "marts";

        private static string ListsText(IEnumerable<(int Item, int Badges)> badgeMart, IEnumerable<IReadOnlyList<int>> specialty) =>
            string.Join(" ", badgeMart.Select(b => $"{b.Item}:{b.Badges}")) + " | " + string.Join(" | ", specialty.Select(s => string.Join(" ", s)));

        private static string Parse(string text, HgEngineSymbolTable items, List<string> order, out List<(int Item, int Badges)> badgeMart,
            out List<Specialty> specialty, out CDeclaration badgeDecl, out List<CDeclaration> arrays)
        {
            badgeMart = new List<(int, int)>(); specialty = new List<Specialty>(); arrays = new List<CDeclaration>();
            CSourceFile file = CSourceFile.For(text);
            badgeDecl = file.Find("sBadgeMart");
            if (badgeDecl == null) return $"{SourceRelPath} has no sBadgeMart.";
            foreach (CInitItem row in badgeDecl.Init.Items)
            {
                if (row.IsConditional) return $"{SourceRelPath}: a badge mart row sits under #if, which DSPRE doesn't edit.";
                if (row.List == null || row.List.Items.Count != 2) return $"{SourceRelPath}: {row.ValueText(text)} isn't {{ item, badges }}.";
                int item = Value(row.List.Items[0].ValueText(text).Trim(), items), badges = Value(row.List.Items[1].ValueText(text).Trim(), null);
                if (item < 0 || badges < 0) return $"{SourceRelPath}: {row.ValueText(text)} couldn't be read.";
                badgeMart.Add((item, badges));
            }
            foreach (string array in order)
            {
                CDeclaration decl = file.Find(array);
                if (decl == null) return $"{SourceRelPath} has no {array}, which {RepointsRelPath} puts in a mart slot.";
                arrays.Add(decl);
                List<int> list = new List<int>();
                foreach (CInitItem entry in decl.Init.Items)
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
            if (!TryLoad(out string text, out HgEngineSymbolTable items, out List<string> order, out error)) return false;
            if ((error = Parse(text, items, order, out List<(int Item, int Badges)> oldBadge, out List<Specialty> oldSpecialty, out CDeclaration badgeDecl, out List<CDeclaration> arrays)) != null) return false;
            if (HgEngineLoadedEntries.ChangedOutside(Path(SourceRelPath), LoadedKey, ListsText(oldBadge, oldSpecialty.Select(s => (IReadOnlyList<int>)s.Items))))
            { error = HgEngineLoadedEntries.ConflictMessage(System.IO.Path.GetFileName(SourceRelPath), new[] { "The mart lists" }); return false; }
            if (specialty.Count != oldSpecialty.Count) { error = "hg-engine's specialty marts are fixed slots; their number can't change."; return false; }
            if (badgeMart.Count > BadgeMartLimit) { error = $"The badge mart can list at most {BadgeMartLimit} items."; return false; }
            string Name(int item, string kept) => kept != null && Value(kept, items) == item ? kept
                : items.TryGetNameWithPrefix(item, "ITEM_", out string n) ? n : item.ToString();

            List<(int Start, int End, string Text)> edits = new List<(int Start, int End, string Text)>();
            if (!badgeMart.SequenceEqual(oldBadge))
            {
                // Rows change in place, so their comments stay; new ones follow the last, removed ones lose their line.
                List<CInitItem> rows = badgeDecl.Init.Items;
                string indent = rows.Count > 0 ? HgEngineSourcePatcher.Indent(text, rows[0].Start) : "    ";
                for (int i = 0; i < Math.Min(rows.Count, badgeMart.Count); i++)
                    if (oldBadge[i] != badgeMart[i])
                        edits.Add((rows[i].Start, rows[i].End, $"{{ {Name(badgeMart[i].Item, rows[i].List.Items[0].ValueText(text).Trim())}, {badgeMart[i].Badges} }}"));
                if (badgeMart.Count > rows.Count)
                {
                    int at = rows.Count > 0 ? HgEngineSourcePatcher.LineEnd(text, rows[^1].End) : badgeDecl.Init.Open + 1;
                    edits.Add((at, at, string.Concat(badgeMart.Skip(rows.Count).Select(r => $"\n{indent}{{ {Name(r.Item, null)}, {r.Badges} }},"))));
                }
                for (int i = badgeMart.Count; i < rows.Count; i++)
                    edits.Add((HgEngineSourcePatcher.LineStart(text, rows[i].Start) - 1, HgEngineSourcePatcher.LineEnd(text, rows[i].End), ""));
            }
            for (int s = 0; s < specialty.Count; s++)
            {
                if (specialty[s].SequenceEqual(oldSpecialty[s].Items)) continue;
                CInitList init = arrays[s].Init;
                List<string> tokens = init.Items.Select(i => i.ValueText(text).Trim()).ToList();
                string indent = init.Items.Count > 0 ? HgEngineSourcePatcher.Indent(text, init.Items[0].Start) : "    ";
                IEnumerable<string> names = specialty[s].Select((item, i) => Name(item, i < tokens.Count ? tokens[i] : null)).Append("0xFFFF");
                edits.Add((init.Open + 1, init.Close, "\n" + indent + string.Join(", ", names) + "\n"));
            }
            if (edits.Count == 0) return true;
            foreach ((int Start, int End, string Text) e in edits.OrderByDescending(e => e.Start)) text = text.Substring(0, e.Start) + e.Text + text.Substring(e.End);

            try
            {
                bool saved = HgEngineVerifiedWrite.TryWrite(Path(SourceRelPath), SourceRelPath, text, written =>
                {
                    string problem = Parse(written, items, order, out List<(int Item, int Badges)> badge, out List<Specialty> spec, out _, out _);
                    if (problem != null) return problem;
                    if (!badge.SequenceEqual(badgeMart)) return "the badge mart differs";
                    for (int s = 0; s < specialty.Count; s++) if (!spec[s].Items.SequenceEqual(specialty[s])) return $"{spec[s].Array} differs";
                    return null;
                }, out error);
                if (saved) HgEngineLoadedEntries.Remember(Path(SourceRelPath), LoadedKey, ListsText(badgeMart, specialty));
                return saved;
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
            // The game orders specialty slots by the address of their repoint inside MART_EXPANSION.
            HgEnginePatchList repoints = HgEnginePatchList.Read(HgEnginePatchKind.Repoint);
            List<HgEnginePatchEntry> inside = repoints?.Entries.Where(e => e.Conditions.Count == 1 && e.Conditions[0] == "MART_EXPANSION").ToList();
            if (inside == null || inside.Count == 0) { error = $"{RepointsRelPath} has no MART_EXPANSION block."; return false; }
            order = inside.Where(e => e.Parsed && e.OverlayNumber < 0).OrderBy(e => e.Address).Select(e => e.Symbol).ToList();
            if (order.Count == 0) { error = $"{RepointsRelPath} repoints no marts."; return false; }
            return true;
        }

        private static int Value(string token, HgEngineSymbolTable symbols) => HgEngineSymbolTable.TryValue(token, out int v, symbols) ? v : -1;

        private static string Path(string rel) => System.IO.Path.Combine(HgEngineProject.RepoPathUnc, rel.Replace('/', System.IO.Path.DirectorySeparatorChar));
    }
}
