using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DSPRE.HgEngine
{
    /// <summary>
    /// data/FormReversionMapping.c: the form a battle-only form goes back to when the battle ends, indexed by the form's
    /// species minus SPECIES_MEGA_START. A form with no row goes back to form 0.
    /// </summary>
    public static class HgEngineFormReversion
    {
        public const string SourceRelPath = "data/FormReversionMapping.c";
        private const string Table = "FormReversionMapping", Start = "SPECIES_MEGA_START";

        private static string FilePath => Path.Combine(HgEngineProject.RepoPathUnc, SourceRelPath.Replace('/', Path.DirectorySeparatorChar));

        public static bool Available => HgEngineProject.IsActive && File.Exists(FilePath);

        /// <summary>One row: the form's species name, the form it goes back to, and whether an #if decides it.</summary>
        public sealed record Row(string Species, int Form, bool Conditional);

        private static string SpeciesOf(CInitItem item)
        {
            string index = item.IndexText?.Trim();
            if (index == null) return null;
            string[] parts = index.Split('-');
            return parts.Length == 2 && parts[1].Trim() == Start ? parts[0].Trim() : null;
        }

        /// <summary>Every row by form species name.</summary>
        public static bool TryRead(out Dictionary<string, Row> rows, out string error)
        {
            rows = new Dictionary<string, Row>(StringComparer.Ordinal);
            error = null;
            if (!File.Exists(FilePath)) { error = $"{SourceRelPath} is missing from the checkout."; return false; }
            rows = Parse(HgEngineFileCache.GetText(FilePath));
            if (rows == null) { error = $"{SourceRelPath} has no {Table}."; return false; }
            return true;
        }

        private static Dictionary<string, Row> Parse(string text)
        {
            CDeclaration table = CSourceFile.For(text).Find(Table);
            if (table?.Init == null) return null;
            Dictionary<string, Row> rows = new Dictionary<string, Row>(StringComparer.Ordinal);
            foreach (CInitItem item in table.Init.Items)
                if (SpeciesOf(item) is string species && CLexer.TryParseInt(item.ValueText(text), out int form))
                    rows[species] = new Row(species, form, item.IsConditional);
            return rows;
        }

        /// <summary>
        /// Sets the forms each species goes back to, by name. A form of 0 with no row stays without one. A row under
        /// an #if is refused.
        /// </summary>
        public static bool TryWrite(IReadOnlyDictionary<string, int> forms, out string error)
        {
            error = null;
            string text = HgEngineFileCache.GetText(FilePath), original = text;
            CDeclaration table = CSourceFile.For(text).Find(Table);
            if (table?.Init == null) { error = $"{SourceRelPath} has no {Table}."; return false; }

            Dictionary<string, CInitItem> items = new Dictionary<string, CInitItem>(StringComparer.Ordinal);
            foreach (CInitItem item in table.Init.Items)
                if (SpeciesOf(item) is string species) items[species] = item;

            List<(int At, int Length, string With)> edits = new List<(int, int, string)>();
            string newline = text.Contains("\r\n") ? "\r\n" : "\n";
            List<string> added = new List<string>();
            foreach ((string species, int form) in forms)
            {
                if (items.TryGetValue(species, out CInitItem item))
                {
                    if (item.ValueText(original).Trim() == form.ToString()) continue;
                    if (item.IsConditional) { error = $"{species}'s row in {SourceRelPath} sits under an #if. Change it there."; return false; }
                    edits.Add((item.ValueStart, item.ValueEnd - item.ValueStart, form.ToString()));
                }
                else if (form != 0)
                {
                    added.Add($"    [{species} - {Start}] = {form},");
                }
            }
            foreach ((int at, int length, string with) in edits.OrderByDescending(e => e.At))
                text = text.Remove(at, length).Insert(at, with);
            if (added.Count > 0)
            {
                int close = CSourceFile.For(text).Find(Table).Init.Close;
                text = text.Insert(HgEngineSourcePatcher.LineStart(text, close), string.Join(newline, added) + newline);
            }
            if (text == original) return true;

            return HgEngineVerifiedWrite.TryWrite(FilePath, SourceRelPath, text, written =>
                Parse(written) is Dictionary<string, Row> back && forms.All(f => (back.TryGetValue(f.Key, out Row row) ? row.Form : 0) == f.Value)
                    ? null : "the forms differ", out error);
        }
    }
}
