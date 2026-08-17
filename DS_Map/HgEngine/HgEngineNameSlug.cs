using System.Text;

namespace DSPRE.HgEngine
{
    /// <summary>
    /// Turns free-form display text (e.g. "Fire Blast Deluxe") into a valid, unique C identifier
    /// fragment for a brand new hg-engine source constant (e.g. "FIRE_BLAST_DELUXE" for a new
    /// "#define ITEM_FIRE_BLAST_DELUXE ..."). Used only when DSPRE mints a new named entry; existing
    /// entries are always referenced by their real constant, never re-derived from display text.
    /// </summary>
    public static class HgEngineNameSlug
    {
        /// <summary>Uppercases, collapses every run of characters outside [A-Z0-9] into a single
        /// underscore, trims leading/trailing underscores, and prefixes with "_" if the result would
        /// start with a digit (not a legal C identifier). Falls back to "UNNAMED" if nothing usable
        /// survives (e.g. the input was empty or pure punctuation).</summary>
        public static string ToSlug(string displayText)
        {
            if (string.IsNullOrWhiteSpace(displayText)) return "UNNAMED";

            var sb = new StringBuilder();
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

        /// <summary>Same as <see cref="ToSlug"/>, but appends a numeric suffix (_2, _3, ...) until
        /// "&lt;prefix&gt;&lt;slug&gt;" isn't already a name in <paramref name="existing"/>, so two
        /// entries typed with the same display text never collide on one constant.</summary>
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
