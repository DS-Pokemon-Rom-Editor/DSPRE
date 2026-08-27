using System.IO;

namespace DSPRE.HgEngine
{
    /// <summary>Source-text read/write for data/BaseExperienceTable.c's <c>BaseExperienceTable[]</c>. This
    /// is the value hg-engine's <c>givenExp</c> calculation actually reads; the vanilla personal-data
    /// struct's own <c>.baseExpRewardPadding</c> field it used to come from is dead padding in this engine
    /// and is never read by the game. A flat `[SPECIES_X] = N,` array of plain numbers.</summary>
    public static class HgEngineBaseExperience
    {
        private const string SourceRelPath = "data/BaseExperienceTable.c";
        private const string SpeciesHeaderRelPath = "include/constants/species.h";

        public static bool TryGetBaseExp(int speciesId, out int baseExp)
        {
            baseExp = 0;
            if (!HgEngineProject.IsActive) return false;
            var species = HgEngineSymbolTable.Load(SpeciesHeaderRelPath);
            if (species == null || !species.TryGetNameWithPrefix(speciesId, "SPECIES_", out string designator)) return false;

            string text = TryReadSource(out _);
            if (text == null || !HgEngineFlatArrayField.TryGetRawValue(text, designator, out string raw)) return false;
            return int.TryParse(raw, out baseExp);
        }

        public static bool TrySetBaseExp(int speciesId, int baseExp, out string error)
        {
            error = null;
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout linked."; return false; }
            var species = HgEngineSymbolTable.Load(SpeciesHeaderRelPath);
            if (species == null || !species.TryGetNameWithPrefix(speciesId, "SPECIES_", out string designator))
            { error = $"Could not resolve a species designator for id {speciesId}."; return false; }

            string text = TryReadSource(out string path);
            if (text == null) { error = $"Source file not found: {path}"; return false; }
            if (!HgEngineFlatArrayField.TrySetRawValue(ref text, designator, baseExp.ToString()))
            { error = $"Could not locate or insert species {speciesId} in BaseExperienceTable.c."; return false; }

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
