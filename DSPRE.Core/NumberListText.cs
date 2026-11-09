using System;
using System.Collections.Generic;
using System.Globalization;

namespace DSPRE
{
    /// <summary>Whole numbers typed as a list, split on commas, spaces or tabs, decimal or 0x hex. Anything else is skipped.</summary>
    public static class NumberListText
    {
        public static List<int> Parse(string text)
        {
            List<int> list = new List<int>();
            if (string.IsNullOrWhiteSpace(text)) return list;
            foreach (string part in text.Split(new[] { ',', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string t = part.Trim();
                bool ok = t.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                    ? int.TryParse(t.Substring(2), NumberStyles.HexNumber, null, out int v)
                    : int.TryParse(t, out v);
                if (ok) list.Add(v);
            }
            return list;
        }
    }
}
