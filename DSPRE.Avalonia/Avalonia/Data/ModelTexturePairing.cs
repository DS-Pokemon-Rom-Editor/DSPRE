using System;
using System.Collections.Generic;
using LibNDSFormats.NSBMD;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// Suggests a palette for a standalone NSBTX texture. NSBTX stores two independent dictionaries,
    /// so this is only a convenience for packs whose names carry the relationship; model material
    /// bindings remain the authoritative association when a model is available.
    /// </summary>
    public static class ModelTexturePairing
    {
        /// <summary>
        /// The palette whose name follows the texture's, or -1 when none does. The rule is the one DSPRE has always
        /// used: the texture name, then shorter and shorter cuts of it, each tried as "name_pl" and as "name"; then a
        /// palette that starts with the whole texture name. Nothing looser, because a near name is often the wrong
        /// palette (sea_on is drawn with sea_f02_pl, not the first "sea_" palette).
        /// </summary>
        public static int MatchPaletteIndex(IReadOnlyList<NSBMDPalette> palettes, string textureName)
        {
            if (palettes == null || palettes.Count == 0 || string.IsNullOrEmpty(textureName)) return -1;
            Dictionary<string, int> byName = new(StringComparer.Ordinal);
            for (int i = 0; i < palettes.Count; i++)
            {
                string name = palettes[i]?.palname;
                if (!string.IsNullOrEmpty(name)) byName.TryAdd(name, i);
            }
            for (string cut = textureName; cut.Length > 0; cut = cut.Substring(0, cut.Length - 1))
            {
                if (byName.TryGetValue(cut + "_pl", out int withSuffix)) return withSuffix;
                if (byName.TryGetValue(cut, out int plain)) return plain;
            }
            for (int i = 0; i < palettes.Count; i++)
                if (palettes[i]?.palname?.StartsWith(textureName, StringComparison.Ordinal) == true) return i;
            return -1;
        }

        /// <summary>The matching palette, or the first one when none matches and a preview needs some palette.</summary>
        public static int BestPaletteIndex(IReadOnlyList<NSBMDPalette> palettes, string textureName)
        {
            if (palettes == null || palettes.Count == 0) return -1;
            int match = MatchPaletteIndex(palettes, textureName);
            return match >= 0 ? match : 0;
        }
    }
}
