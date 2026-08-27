using System.IO;

namespace DSPRE.HgEngine
{
    /// <summary>Source-text read/write for data/SpeciesToOWFormFemale.c's <c>SpeciesToOWFormFemale[]</c>.
    /// Values are either the literal <c>FALSE</c> or a compound <c>OW_FEMALE_MASK | SPECIES_X_..._FEMALE</c>
    /// expression, so this exposes the whole right-hand side as a raw, verbatim-edited text field rather
    /// than parsing/rebuilding that expression shape.</summary>
    public static class HgEngineSpeciesOwFormFemale
    {
        private const string SourceRelPath = "data/SpeciesToOWFormFemale.c";
        private const string SpeciesHeaderRelPath = "include/constants/species.h";

        public static bool TryGetRawExpression(int speciesId, out string expression)
        {
            expression = null;
            if (!HgEngineProject.IsActive) return false;
            var species = HgEngineSymbolTable.Load(SpeciesHeaderRelPath);
            if (species == null || !species.TryGetNameWithPrefix(speciesId, "SPECIES_", out string designator)) return false;

            string text = TryReadSource(out _);
            if (text == null) return false;
            // Absent = FALSE (no female overworld form), same as the game's own default for this table.
            if (!HgEngineFlatArrayField.TryGetRawValue(text, designator, out expression)) expression = "FALSE";
            return true;
        }

        public static bool TrySetRawExpression(int speciesId, string expression, out string error)
        {
            error = null;
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout linked."; return false; }
            if (string.IsNullOrWhiteSpace(expression)) { error = "Expression cannot be empty."; return false; }
            var species = HgEngineSymbolTable.Load(SpeciesHeaderRelPath);
            if (species == null || !species.TryGetNameWithPrefix(speciesId, "SPECIES_", out string designator))
            { error = $"Could not resolve a species designator for id {speciesId}."; return false; }

            string text = TryReadSource(out string path);
            if (text == null) { error = $"Source file not found: {path}"; return false; }
            if (!HgEngineFlatArrayField.TrySetRawValue(ref text, designator, expression.Trim()))
            { error = $"Could not locate or insert species {speciesId} in SpeciesToOWFormFemale.c."; return false; }

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
