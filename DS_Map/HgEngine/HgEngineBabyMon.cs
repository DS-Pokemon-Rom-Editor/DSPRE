using System.IO;

namespace DSPRE.HgEngine
{
    /// <summary>Source-text read/write for data/BabyMons.c's <c>sBabyMons[]</c> (the species an egg from
    /// this species/family actually hatches into, e.g. Ivysaur/Venusaur -> Bulbasaur). A flat
    /// `[SPECIES_X] = SPECIES_Y,` array.</summary>
    public static class HgEngineBabyMon
    {
        private const string SourceRelPath = "data/BabyMons.c";
        private const string SpeciesHeaderRelPath = "include/constants/species.h";

        public static bool TryGetBabySpecies(int speciesId, out int babySpeciesId)
        {
            babySpeciesId = 0;
            if (!HgEngineProject.IsActive) return false;
            var species = HgEngineSymbolTable.Load(SpeciesHeaderRelPath);
            if (species == null || !species.TryGetNameWithPrefix(speciesId, "SPECIES_", out string designator)) return false;

            string text = TryReadSource(out _);
            if (text == null || !HgEngineFlatArrayField.TryGetRawValue(text, designator, out string raw)) return false;

            if (species.TryGetValue(raw, out int v)) { babySpeciesId = v; return true; }
            return int.TryParse(raw, out babySpeciesId);
        }

        public static bool TrySetBabySpecies(int speciesId, int babySpeciesId, out string error)
        {
            error = null;
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout linked."; return false; }
            var species = HgEngineSymbolTable.Load(SpeciesHeaderRelPath);
            if (species == null || !species.TryGetNameWithPrefix(speciesId, "SPECIES_", out string designator))
            { error = $"Could not resolve a species designator for id {speciesId}."; return false; }

            string valueLiteral = species.TryGetNameWithPrefix(babySpeciesId, "SPECIES_", out string babyName)
                ? babyName : babySpeciesId.ToString();

            string text = TryReadSource(out string path);
            if (text == null) { error = $"Source file not found: {path}"; return false; }
            if (!HgEngineFlatArrayField.TrySetRawValue(ref text, designator, valueLiteral))
            { error = $"Could not locate or insert species {speciesId} in BabyMons.c."; return false; }

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
