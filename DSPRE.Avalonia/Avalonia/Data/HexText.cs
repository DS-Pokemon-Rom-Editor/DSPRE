using System;
using System.Globalization;

namespace DSPRE.Avalonia.Data
{
    /// <summary>Reads a hex number typed into a box, with or without a leading 0x.</summary>
    public static class HexText
    {
        public static bool TryParseUShort(string s, out ushort value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(s)) return false;
            s = s.Trim();
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) s = s.Substring(2);
            return ushort.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
        }
    }
}
