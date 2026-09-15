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
            rawValue = null;
            if (!TryLocate(text, designator, out int start, out int end)) return false;
            rawValue = text.Substring(start, end - start).Trim();
            return true;
        }

        /// <summary>Replaces the designator's value if present, otherwise inserts a new entry right before
        /// the file's final "};".</summary>
        public static bool TrySetRawValue(ref string text, string designator, string valueLiteral)
        {
            if (TryLocate(text, designator, out int start, out int end))
            {
                // Keep the spacing around the old value.
                int valueStart = start;
                while (valueStart < end && char.IsWhiteSpace(text[valueStart])) valueStart++;
                int valueEnd = end;
                while (valueEnd > valueStart && char.IsWhiteSpace(text[valueEnd - 1])) valueEnd--;
                text = text.Substring(0, valueStart) + valueLiteral + text.Substring(valueEnd);
                return true;
            }
            return HgEngineHeaderEditor.TryInsertBeforeFinalCloseBrace(ref text, $"\n    [{designator}] = {valueLiteral},");
        }

        // The value runs to the next comma outside parentheses, or to the line end or closing brace when the
        // last entry has no comma, so MON_WITH_FORM(a, b) and OR'd expressions stay whole.
        private static bool TryLocate(string text, string designator, out int start, out int end)
        {
            start = end = -1;
            var m = Regex.Match(text, @"\[\s*" + Regex.Escape(designator) + @"\s*\]\s*=");
            if (!m.Success) return false;
            int i = m.Index + m.Length, depth = 0;
            start = i;
            for (; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '/' && i + 1 < text.Length && text[i + 1] == '/') break;
                if (c == '(') depth++;
                else if (c == ')') depth--;
                else if (depth == 0 && (c == ',' || c == '\n' || c == '\r' || c == '}')) break;
            }
            end = i;
            return text.Substring(start, end - start).Trim().Length > 0;
        }
    }
}
