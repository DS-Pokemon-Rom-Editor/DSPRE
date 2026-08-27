using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace DSPRE.HgEngine
{
    /// <summary>Source-text read/write for data/IconPaletteTable.c's <c>gIconPalTable[]</c> (which of the
    /// 3 party-icon palettes each species uses). A flat `[SPECIES_X] = N,` array, not a `{ ... }` block,
    /// so this reads/writes directly instead of through HgEngineSourcePatcher.</summary>
    public static class HgEngineIconPalette
    {
        private const string SourceRelPath = "data/IconPaletteTable.c";
        private static readonly Regex EntryPattern = new(@"\[\s*(SPECIES_\w+)\s*\]\s*=\s*(\d+)\s*,");

        public static bool TryGetPaletteId(int speciesId, out int paletteId)
        {
            paletteId = 0;
            if (!HgEngineProject.IsActive) return false;
            var species = HgEngineSymbolTable.Load("include/constants/species.h");
            if (species == null || !species.TryGetNameWithPrefix(speciesId, "SPECIES_", out string designator)) return false;

            string text = TryReadSource(out _);
            if (text == null) return false;

            foreach (Match m in EntryPattern.Matches(text))
                if (m.Groups[1].Value == designator)
                { paletteId = int.Parse(m.Groups[2].Value); return true; }
            return false;
        }

        public static bool TrySetPaletteId(int speciesId, int paletteId, out string error)
        {
            error = null;
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout linked."; return false; }
            var species = HgEngineSymbolTable.Load("include/constants/species.h");
            if (species == null || !species.TryGetNameWithPrefix(speciesId, "SPECIES_", out string designator))
            { error = $"Could not resolve a species designator for id {speciesId}."; return false; }

            string text = TryReadSource(out string path);
            if (text == null) { error = $"Source file not found: {path}"; return false; }

            var entryPattern = new Regex(@"(\[\s*" + Regex.Escape(designator) + @"\s*\]\s*=\s*)(\d+)(\s*,)");
            var match = entryPattern.Match(text);
            if (!match.Success) { error = $"Species {speciesId} not found in IconPaletteTable.c."; return false; }

            string newText = text.Substring(0, match.Index) + match.Groups[1].Value + paletteId + match.Groups[3].Value
                + text.Substring(match.Index + match.Length);
            File.WriteAllText(path, newText);
            return true;
        }

        private static string TryReadSource(out string path)
        {
            path = Path.Combine(HgEngineProject.RepoPathUnc, SourceRelPath.Replace('/', '\\'));
            return File.Exists(path) ? HgEngineFileCache.GetText(path) : null;
        }
    }
}
