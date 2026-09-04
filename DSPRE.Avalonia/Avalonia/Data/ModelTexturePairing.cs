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
        public static int BestPaletteIndex(IReadOnlyList<NSBMDPalette> palettes, string textureName)
        {
            if (palettes == null || palettes.Count == 0) return -1;
            string texture = textureName ?? "";
            for (int i = 0; i < palettes.Count; i++)
                if (String.Equals(palettes[i]?.palname, texture, StringComparison.Ordinal)) return i;
            for (int i = 0; i < palettes.Count; i++)
            {
                string palette = palettes[i]?.palname;
                if (!string.IsNullOrEmpty(palette)
                    && (texture.StartsWith(palette, StringComparison.Ordinal)
                        || palette.StartsWith(texture, StringComparison.Ordinal)))
                    return i;
            }
            // A few standalone HGSS packs name the surface texture "*_on" while its palette carries
            // a more specific surface name, and keep a separate "*_un" underground pair. NSBTX has
            // no stored texture-to-palette binding, so this remains a name suggestion; avoid choosing
            // the clearly underground palette for the surface texture.
            int split = texture.IndexOf('_');
            if (split > 0 && texture.EndsWith("_on", StringComparison.Ordinal))
            {
                string stem = texture.Substring(0, split) + "_";
                for (int i = 0; i < palettes.Count; i++)
                {
                    string palette = palettes[i]?.palname;
                    if (!string.IsNullOrEmpty(palette)
                        && palette.StartsWith(stem, StringComparison.Ordinal)
                        && !palette.EndsWith("_un", StringComparison.Ordinal))
                        return i;
                }
            }
            return 0;
        }
    }
}
