using System.IO;

namespace DSPRE.HgEngine
{
    /// <summary>Source-text read/write for data/RegionalDex.c's <c>RegionalDex[]</c>. Genuinely sparse in
    /// hg-engine's own source (only species in the regional dex get an entry), so a missing entry is a
    /// normal "not in dex" (0) read, not an error.</summary>
    public static class HgEngineRegionalDex
    {
        private const string SourceRelPath = "data/RegionalDex.c";
        private const string SpeciesHeaderRelPath = "include/constants/species.h";

        /// <summary>Always succeeds when the checkout/species resolve; a missing entry reads as 0 (not in
        /// the regional dex), matching the game's own behavior for species this table omits.</summary>
        public static bool TryGetDexNumber(int speciesId, out int dexNumber)
        {
            dexNumber = 0;
            if (!HgEngineProject.IsActive) return false;
            var species = HgEngineSymbolTable.Load(SpeciesHeaderRelPath);
            if (species == null || !species.TryGetNameWithPrefix(speciesId, "SPECIES_", out string designator)) return false;

            string text = TryReadSource(out _);
            if (text == null) return false;
            if (HgEngineFlatArrayField.TryGetRawValue(text, designator, out string raw)) int.TryParse(raw, out dexNumber);
            return true;
        }

        public static bool TrySetDexNumber(int speciesId, int dexNumber, out string error)
        {
            error = null;
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout linked."; return false; }
            var species = HgEngineSymbolTable.Load(SpeciesHeaderRelPath);
            if (species == null || !species.TryGetNameWithPrefix(speciesId, "SPECIES_", out string designator))
            { error = $"Could not resolve a species designator for id {speciesId}."; return false; }

            string text = TryReadSource(out string path);
            if (text == null) { error = $"Source file not found: {path}"; return false; }
            if (!HgEngineFlatArrayField.TrySetRawValue(ref text, designator, dexNumber.ToString()))
            { error = $"Could not locate or insert species {speciesId} in RegionalDex.c."; return false; }

            File.WriteAllText(path, text);
            return true;
        }

        private static string TryReadSource(out string path)
        {
            path = Path.Combine(HgEngineProject.RepoPathUnc, SourceRelPath.Replace('/', '\\'));
            return File.Exists(path) ? HgEngineFileCache.GetText(path) : null;
        }
    }
}
