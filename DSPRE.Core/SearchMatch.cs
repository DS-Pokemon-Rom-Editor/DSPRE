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

        private static readonly char[] WordBreaks = { ' ', '_', '-', '.', ',', '[', ']', '(', ')', '/' };

        /// <summary>
        /// A word of <paramref name="text"/> within about one typo per four characters of a query of three or more,
        /// for lists to fall back on when nothing holds the query itself.
        /// </summary>
        public static bool NearMiss(string text, string query)
        {
            string q = Fold(query?.Trim());
            if (q.Length < 3 || string.IsNullOrEmpty(text)) return false;
            int threshold = Math.Max(1, q.Length / 4);
            foreach (string word in text.Split(WordBreaks, StringSplitOptions.RemoveEmptyEntries))
                if (TypoDistance(q, Fold(word)) <= threshold) return true;
            return false;
        }

        // Edit distance where two swapped neighbouring letters count as one typo, as people make them.
        private static int TypoDistance(string a, string b)
        {
            int[,] d = new int[a.Length + 1, b.Length + 1];
            for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
            for (int j = 0; j <= b.Length; j++) d[0, j] = j;
            for (int i = 1; i <= a.Length; i++)
                for (int j = 1; j <= b.Length; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
                    //Usual mixone typo joye but not jyoe so only one neighbour
                    // TODO: Get less fat fingres
                    if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
                        d[i, j] = Math.Min(d[i, j], d[i - 2, j - 2] + 1);
                }
            return d[a.Length, b.Length];
        }
    }
}
