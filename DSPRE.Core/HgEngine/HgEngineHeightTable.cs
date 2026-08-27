using System.IO;
using System.Text.RegularExpressions;

namespace DSPRE.HgEngine
{
    /// <summary>Source-text read/write for data/HeightTable.c's <c>__data[]</c> (per-species front/back
    /// sprite height offsets, separate male/female, used by battle-scene sprite Y placement). A flat
    /// `[SPECIES_X] = { femaleBack, maleBack, femaleFront, maleFront },` positional 4-tuple, so this
    /// can't reuse HgEngineFlatArrayField as-is.</summary>
    public static class HgEngineHeightTable
    {
        private const string SourceRelPath = "data/HeightTable.c";
        private const string SpeciesHeaderRelPath = "include/constants/species.h";

        public static bool TryGet(int speciesId, out int femaleBack, out int maleBack, out int femaleFront, out int maleFront)
        {
            femaleBack = maleBack = femaleFront = maleFront = 0;
            if (!HgEngineProject.IsActive) return false;
            var species = HgEngineSymbolTable.Load(SpeciesHeaderRelPath);
            if (species == null || !species.TryGetNameWithPrefix(speciesId, "SPECIES_", out string designator)) return false;

            string text = TryReadSource(out _);
            if (text == null) return false;

            var m = EntryRegex(designator).Match(text);
            if (!m.Success) return false;

            var parts = m.Groups[1].Value.Split(',');
            if (parts.Length < 4) return false;
            int.TryParse(parts[0].Trim(), out femaleBack);
            int.TryParse(parts[1].Trim(), out maleBack);
            int.TryParse(parts[2].Trim(), out femaleFront);
            int.TryParse(parts[3].Trim(), out maleFront);
            return true;
        }

        public static bool TrySet(int speciesId, int femaleBack, int maleBack, int femaleFront, int maleFront, out string error)
        {
            error = null;
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout linked."; return false; }
            var species = HgEngineSymbolTable.Load(SpeciesHeaderRelPath);
            if (species == null || !species.TryGetNameWithPrefix(speciesId, "SPECIES_", out string designator))
            { error = $"Could not resolve a species designator for id {speciesId}."; return false; }

            string text = TryReadSource(out string path);
            if (text == null) { error = $"Source file not found: {path}"; return false; }

            string valueLiteral = $"{{ {femaleBack}, {maleBack}, {femaleFront}, {maleFront} }}";
            var wholeEntry = new Regex(@"\[\s*" + Regex.Escape(designator) + @"\s*\]\s*=\s*\{[^}]*\}");
            var m = wholeEntry.Match(text);
            if (m.Success)
            {
                text = text.Substring(0, m.Index) + $"[{designator}] = {valueLiteral}" + text.Substring(m.Index + m.Length);
            }
            else if (!HgEngineHeaderEditor.TryInsertBeforeFinalCloseBrace(ref text, $"\n    [{designator}] = {valueLiteral},"))
            {
                error = $"Could not locate or insert species {speciesId} in HeightTable.c.";
                return false;
            }

            File.WriteAllText(path, text);
            return true;
        }

        private static Regex EntryRegex(string designator) =>
            new(@"\[\s*" + Regex.Escape(designator) + @"\s*\]\s*=\s*\{([^}]*)\}");

        private static string TryReadSource(out string path)
        {
            path = Path.Combine(HgEngineProject.RepoPathUnc, SourceRelPath.Replace('/', '\\'));
            return File.Exists(path) ? HgEngineFileCache.GetText(path) : null;
        }
    }
}
