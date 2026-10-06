using System.Collections.Generic;
using System.Linq;

namespace DSPRE.HgEngine
{
    /// <summary>Shared read/write primitive for the several hg-engine data/*.c files that are just a flat
    /// `[SPECIES_X] = value,` array (one scalar per species), e.g. HiddenAbilityTable.c, BaseExperienceTable.c,
    /// BabyMons.c, RegionalDex.c, SpeciesToOWFormFemale.c. A species with no entry (sparse tables, or a table not
    /// re-dumped since a fakemon was added) gets one inserted rather than failing.</summary>
    internal static class HgEngineFlatArrayField
    {
        private const string SpeciesH = "include/constants/species.h";

        public static bool TryGetRawValue(string text, string designator, out string rawValue)
        {
            rawValue = null;
            if (!TryLocate(text, designator, out CInitItem item, out _) || item == null) return false;
            rawValue = item.ValueText(text).Trim();
            return rawValue.Length > 0;
        }

        /// <summary>Replaces the designator's value if present, otherwise inserts a new entry before the table's
        /// closing brace. False when the entry can't be told apart (it appears under both sides of an #if) or the
        /// edit wouldn't read back as written.</summary>
        public static bool TrySetRawValue(ref string text, string designator, string valueLiteral)
        {
            string edited;
            if (TryLocate(text, designator, out CInitItem item, out CDeclaration table))
            {
                if (item == null) return false;
                edited = text.Substring(0, item.ValueStart) + valueLiteral + text.Substring(item.ValueEnd);
            }
            else
            {
                if (table == null) return false;
                CInitItem last = table.Init.Items.LastOrDefault();
                string indent = last != null ? HgEngineSwarms.Indent(text, last.Start) : "    ";
                int at = HgEngineSwarms.LineStart(text, table.Init.Close);
                // An entry before the brace needs the previous one to end in a comma.
                string before = text.Substring(0, at);
                if (last != null && text.IndexOf(',', last.End, at - last.End) < 0)
                    before = text.Substring(0, last.End) + "," + text.Substring(last.End, at - last.End);
                edited = before + $"{indent}[{designator}] = {valueLiteral},\n" + text.Substring(at);
            }
            if (!TryGetRawValue(edited, designator, out string back) || CSourceFile.Normalize(back) != CSourceFile.Normalize(valueLiteral)) return false;
            text = edited;
            return true;
        }

        // The table is the declaration with the most [NAME] entries. An entry matches by name, or by the species id its
        // name stands for, so an alias isn't mistaken for a missing entry. Found under both sides of an #if: item is null.
        private static bool TryLocate(string text, string designator, out CInitItem item, out CDeclaration table)
        {
            item = null;
            table = CSourceFile.For(text).Declarations
                .OrderByDescending(d => d.Init.Items.Count(i => i.IndexText != null)).FirstOrDefault();
            if (table == null) return false;

            HgEngineSymbolTable species = HgEngineProject.IsActive ? HgEngineSymbolTable.Load(SpeciesH) : null;
            int? wanted = species != null && species.TryGetValue(designator, out int w) ? w : null;
            List<CInitItem> matches = new List<CInitItem>();
            foreach (CInitItem i in table.Init.Items)
            {
                string name = i.IndexText?.Trim();
                if (name == null) continue;
                if (name == designator || (wanted != null && species.TryGetValue(name, out int v) && v == wanted)) matches.Add(i);
            }
            if (matches.Count == 0) return false;
            item = matches.Count == 1 ? matches[0] : null;
            return true;
        }
    }
}
