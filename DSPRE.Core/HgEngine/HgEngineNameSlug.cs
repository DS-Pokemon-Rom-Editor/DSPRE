using System.Text;

namespace DSPRE.HgEngine
{
    /// <summary>
    /// A C name for a new entry from its display text ("Fire Blast Deluxe" to FIRE_BLAST_DELUXE). Existing entries keep
    /// their real constants.
    /// </summary>
    public static class HgEngineNameSlug
    {
        /// <summary>
        /// Uppercase, with runs of other characters as one underscore, the ends trimmed and a leading "_" before a digit;
        /// "UNNAMED" when nothing is left.
        /// </summary>
        public static string ToSlug(string displayText)
        {
            if (string.IsNullOrWhiteSpace(displayText)) return "UNNAMED";

            StringBuilder sb = new StringBuilder();
            bool lastWasUnderscore = false;
            foreach (char c in displayText.ToUpperInvariant())
            {
                bool ok = (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9');
                if (ok) { sb.Append(c); lastWasUnderscore = false; }
                else if (!lastWasUnderscore && sb.Length > 0) { sb.Append('_'); lastWasUnderscore = true; }
            }

            string slug = sb.ToString().Trim('_');
            if (slug.Length == 0) return "UNNAMED";
            if (char.IsDigit(slug[0])) slug = "_" + slug;
            return slug;
        }

        /// <summary><see cref="ToSlug"/> with _2, _3 and so on added until the name is free in <paramref name="existing"/>.</summary>
        public static string ToUniqueSlug(string displayText, HgEngineSymbolTable existing, string prefix)
        {
            string baseSlug = ToSlug(displayText);
            string candidate = baseSlug;
            int suffix = 2;
            while (existing != null && existing.TryGetValue(prefix + candidate, out _))
            {
                candidate = $"{baseSlug}_{suffix}";
                suffix++;
            }
            return candidate;
        }
    }
}
