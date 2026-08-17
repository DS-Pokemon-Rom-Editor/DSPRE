using System.Text.RegularExpressions;

namespace DSPRE.HgEngine
{
    /// <summary>Shared read/write primitive for the several hg-engine data/*.c files that are just a flat
    /// `[SPECIES_X] = value,` array (one scalar per species), e.g. HiddenAbilityTable.c, BaseExperienceTable.c,
    /// BabyMons.c, RegionalDex.c, SpeciesToOWFormFemale.c. Unlike IconPaletteTable.c's original one-off
    /// implementation, this also handles a species having NO entry at all (sparse tables, or any table that
    /// hasn't been re-dumped since a fakemon was added) by inserting a new entry rather than failing.</summary>
    internal static class HgEngineFlatArrayField
    {
        public static bool TryGetRawValue(string text, string designator, out string rawValue)
        {
            var m = Regex.Match(text, @"\[\s*" + Regex.Escape(designator) + @"\s*\]\s*=\s*([^,\r\n]+),");
            if (!m.Success) { rawValue = null; return false; }
            rawValue = m.Groups[1].Value.Trim();
            return true;
        }

        /// <summary>Replaces the designator's value if present, otherwise inserts a new entry right before
        /// the file's final "};".</summary>
        public static bool TrySetRawValue(ref string text, string designator, string valueLiteral)
        {
            var entryPattern = new Regex(@"(\[\s*" + Regex.Escape(designator) + @"\s*\]\s*=\s*)([^,\r\n]+)(\s*,)");
            var m = entryPattern.Match(text);
            if (m.Success)
            {
                text = text.Substring(0, m.Index) + m.Groups[1].Value + valueLiteral + m.Groups[3].Value
                    + text.Substring(m.Index + m.Length);
                return true;
            }
            return HgEngineHeaderEditor.TryInsertBeforeFinalCloseBrace(ref text, $"\n    [{designator}] = {valueLiteral},");
        }
    }
}
