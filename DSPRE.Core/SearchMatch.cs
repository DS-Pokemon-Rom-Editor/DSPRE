using System;
using System.Globalization;
using System.Text;

namespace DSPRE
{
    /// <summary>Search box matching with case and accents ignored.</summary>
    public static class SearchMatch
    {
        // Decomposed rather than culture-compared, so it works without culture data.
        public static string Fold(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            bool ascii = true;
            foreach (char c in s) if (c > 0x7F) { ascii = false; break; }
            if (ascii) return s.ToLowerInvariant();

            var sb = new StringBuilder(s.Length);
            foreach (char c in s.Normalize(NormalizationForm.FormD))
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    sb.Append(char.ToLowerInvariant(c));
            return sb.ToString();
        }

        public static bool Contains(string text, string query) =>
            Fold(text).Contains(Fold(query), StringComparison.Ordinal);

        public static bool StartsWith(string text, string query) =>
            Fold(text).StartsWith(Fold(query), StringComparison.Ordinal);
    }
}
