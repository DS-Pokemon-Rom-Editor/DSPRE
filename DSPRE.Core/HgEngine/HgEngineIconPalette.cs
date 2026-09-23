using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace DSPRE.HgEngine
{
    /// <summary>Source-text read/write for data/IconPaletteTable.c's <c>gIconPalTable[]</c> (which of the
    /// 3 party-icon palettes each species uses). A flat `[SPECIES_X] = N,` array, not a `{ ... }` block,
    /// so this reads/writes directly instead of through HgEngineSourcePatcher. Some rows (Unown and the
    /// other vanilla forms) are designated by number, e.g. `[499] = 0,`.</summary>
    public static class HgEngineIconPalette
    {
        /// <summary>The file this reads, for anything that needs to name it on screen.</summary>
        public const string SourceFile = "data/IconPaletteTable.c";

        private const string SourceRelPath = SourceFile;
        private static readonly Regex EntryPattern = new(@"\[\s*(SPECIES_\w+|\d+)\s*\]\s*=\s*(\d+)\s*,");

        public static bool TryGetPaletteId(int speciesId, out int paletteId)
        {
            paletteId = 0;
            if (!HgEngineProject.IsActive) return false;
            string text = TryReadSource(out _);
            if (text == null) return false;

            var value = FindValue(text, speciesId, DesignatorFor(speciesId));
            if (value == null) return false;
            paletteId = int.Parse(value.Value);
            return true;
        }

        public static bool TrySetPaletteId(int speciesId, int paletteId, out string error)
        {
            error = null;
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout linked."; return false; }

            string text = TryReadSource(out string path);
            if (text == null) { error = $"Source file not found: {path}"; return false; }

            if (!TryReplaceValue(ref text, speciesId, DesignatorFor(speciesId), paletteId))
            { error = $"Species {speciesId} not found in IconPaletteTable.c."; return false; }
            HgEngineFileCache.WriteText(path, text);
            return true;
        }

        /// <summary>The value of the row named <paramref name="designator"/>, or failing that the row
        /// designated by the number <paramref name="speciesId"/>.</summary>
        internal static Group FindValue(string text, int speciesId, string designator)
        {
            Group numeric = null;
            foreach (Match m in EntryPattern.Matches(text))
            {
                string d = m.Groups[1].Value;
                if (designator != null && d == designator) return m.Groups[2];
                if (numeric == null && int.TryParse(d, out int n) && n == speciesId) numeric = m.Groups[2];
            }
            return numeric;
        }

        internal static bool TryReplaceValue(ref string text, int speciesId, string designator, int paletteId)
        {
            var value = FindValue(text, speciesId, designator);
            if (value == null) return false;
            text = string.Concat(text.AsSpan(0, value.Index), paletteId.ToString(), text.AsSpan(value.Index + value.Length));
            return true;
        }

        private static string DesignatorFor(int speciesId)
        {
            var species = HgEngineSymbolTable.Load("include/constants/species.h");
            return species != null && species.TryGetNameWithPrefix(speciesId, "SPECIES_", out string designator) ? designator : null;
        }

        private static string TryReadSource(out string path)
        {
            path = Path.Combine(HgEngineProject.RepoPathUnc, SourceRelPath.Replace('/', '\\'));
            return File.Exists(path) ? HgEngineFileCache.GetText(path) : null;
        }
    }
}
