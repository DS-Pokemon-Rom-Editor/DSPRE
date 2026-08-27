using System.IO;

namespace DSPRE.HgEngine
{
    /// <summary>Source-text read/write for data/HiddenAbilityTable.c's <c>HiddenAbilityTable[]</c> (each
    /// species' hidden ability). A flat `[SPECIES_X] = ABILITY_Y,` array, so this reads/writes directly via
    /// <see cref="HgEngineFlatArrayField"/> instead of through HgEngineSourcePatcher.</summary>
    public static class HgEngineHiddenAbility
    {
        private const string SourceRelPath = "data/HiddenAbilityTable.c";
        private const string SpeciesHeaderRelPath = "include/constants/species.h";
        private const string AbilityHeaderRelPath = "include/constants/ability.h";

        public static bool TryGetAbilityId(int speciesId, out int abilityId)
        {
            abilityId = 0;
            if (!HgEngineProject.IsActive) return false;
            var species = HgEngineSymbolTable.Load(SpeciesHeaderRelPath);
            if (species == null || !species.TryGetNameWithPrefix(speciesId, "SPECIES_", out string designator)) return false;

            string text = TryReadSource(out _);
            if (text == null || !HgEngineFlatArrayField.TryGetRawValue(text, designator, out string raw)) return false;

            var abilities = HgEngineSymbolTable.Load(AbilityHeaderRelPath);
            if (abilities != null && abilities.TryGetValue(raw, out int v)) { abilityId = v; return true; }
            return int.TryParse(raw, out abilityId);
        }

        public static bool TrySetAbilityId(int speciesId, int abilityId, out string error)
        {
            error = null;
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout linked."; return false; }
            var species = HgEngineSymbolTable.Load(SpeciesHeaderRelPath);
            if (species == null || !species.TryGetNameWithPrefix(speciesId, "SPECIES_", out string designator))
            { error = $"Could not resolve a species designator for id {speciesId}."; return false; }

            var abilities = HgEngineSymbolTable.Load(AbilityHeaderRelPath);
            string valueLiteral = abilities != null && abilities.TryGetNameWithPrefix(abilityId, "ABILITY_", out string abilityName)
                ? abilityName : abilityId.ToString();

            string text = TryReadSource(out string path);
            if (text == null) { error = $"Source file not found: {path}"; return false; }
            if (!HgEngineFlatArrayField.TrySetRawValue(ref text, designator, valueLiteral))
            { error = $"Could not locate or insert species {speciesId} in HiddenAbilityTable.c."; return false; }

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
