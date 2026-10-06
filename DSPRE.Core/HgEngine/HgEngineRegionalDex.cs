using System.Collections.Generic;
using System.IO;
using System.Linq;

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
            HgEngineSymbolTable species = HgEngineSymbolTable.Load(SpeciesHeaderRelPath);
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
            HgEngineSymbolTable species = HgEngineSymbolTable.Load(SpeciesHeaderRelPath);
            if (species == null || !species.TryGetNameWithPrefix(speciesId, "SPECIES_", out string designator))
            { error = $"Could not resolve a species designator for id {speciesId}."; return false; }

            string text = TryReadSource(out string path);
            if (text == null) { error = $"Source file not found: {path}"; return false; }
            if (!HgEngineFlatArrayField.TrySetRawValue(ref text, designator, dexNumber.ToString()))
            { error = $"Could not locate or insert species {speciesId} in RegionalDex.c."; return false; }

            HgEngineFileCache.WriteText(path, text);
            return TrySyncSortOrder(text, out error);
        }

        /// <summary>
        /// The Pokedex's regional-number sort is RegionalDex.c's species in number order, listed again in
        /// PokedexSort.c, so it is rebuilt from the table after a number changes.
        /// </summary>
        private static bool TrySyncSortOrder(string regionalDex, out string error)
        {
            error = null;
            if (!HgEngineDexSortLists.Exists) return true;
            if (!HgEngineDexSortLists.TryLoad(out HgEngineDexSortLists sort, out error)) return false;
            if (!sort.Lists.ContainsKey("RegionalNum")) { error = $"{HgEngineDexSortLists.RelPath} has no sPokedexSort_RegionalNum to keep in order."; return false; }

            CDeclaration table = CSourceFile.For(regionalDex).Find("RegionalDex");
            if (table == null) { error = $"{SourceRelPath} has no RegionalDex table."; return false; }
            List<string> order = table.Init.Items
                .Select((item, i) => (Species: item.IndexText?.Trim(), Number: int.TryParse(item.ValueText(regionalDex).Trim(), out int n) ? n : 0, At: i))
                .Where(e => e.Species != null && e.Number > 0).OrderBy(e => e.Number).ThenBy(e => e.At).Select(e => e.Species).ToList();
            if (sort.Lists["RegionalNum"].SequenceEqual(order)) return true;
            sort.Lists["RegionalNum"] = order;
            return sort.TryWrite(new[] { "RegionalNum" }, out error);
        }

        private static string TryReadSource(out string path)
        {
            path = Path.Combine(HgEngineProject.RepoPathUnc, SourceRelPath.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(path) ? HgEngineFileCache.GetText(path) : null;
        }
    }
}
