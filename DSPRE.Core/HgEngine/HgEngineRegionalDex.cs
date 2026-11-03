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
            HgEngineSymbolTable species = HgEngineProject.IsActive ? HgEngineSymbolTable.Load(SpeciesHeaderRelPath) : null;
            if (species == null || !species.TryGetNameWithPrefix(speciesId, "SPECIES_", out _)) return false;
            if (!TryGetAllDexNumbers(out Dictionary<int, int> numbers, out _)) return false;
            numbers.TryGetValue(speciesId, out dexNumber);
            return true;
        }

        /// <summary>Every species with a regional number above 0, read in one pass over the table.</summary>
        public static bool TryGetAllDexNumbers(out Dictionary<int, int> numbers, out string error)
        {
            numbers = new Dictionary<int, int>();
            error = null;
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout is linked."; return false; }
            HgEngineSymbolTable species = HgEngineSymbolTable.Load(SpeciesHeaderRelPath);
            if (species == null) { error = $"{SpeciesHeaderRelPath} couldn't be read."; return false; }
            string text = TryReadSource(out string path);
            if (text == null) return true;
            CDeclaration table = CSourceFile.For(text).Find("RegionalDex");
            if (table == null) { error = $"{SourceRelPath} has no RegionalDex table."; return false; }
            foreach (CInitItem item in table.Init.Items)
            {
                string name = item.IndexText?.Trim();
                if (name != null && species.TryGetValue(name, out int id) && int.TryParse(item.ValueText(text).Trim(), out int n) && n > 0)
                    numbers[id] = n;
            }
            return true;
        }

        public static bool TrySetDexNumber(int speciesId, int dexNumber, out string error) =>
            TrySetDexNumbers(new Dictionary<int, int> { [speciesId] = dexNumber }, out error);

        /// <summary>Sets every number given, then writes the file and the sort list once.</summary>
        public static bool TrySetDexNumbers(IReadOnlyDictionary<int, int> numbers, out string error)
        {
            error = null;
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout is linked."; return false; }
            if (numbers.Count == 0) return true;
            HgEngineSymbolTable species = HgEngineSymbolTable.Load(SpeciesHeaderRelPath);
            string text = TryReadSource(out string path);
            if (text == null) { error = $"{path} is missing from the checkout."; return false; }
            foreach ((int speciesId, int dexNumber) in numbers)
            {
                if (species == null || !species.TryGetNameWithPrefix(speciesId, "SPECIES_", out string designator))
                { error = $"Species {speciesId} has no SPECIES_ name in the checkout."; return false; }
                if (!HgEngineFlatArrayField.TrySetRawValue(ref text, designator, dexNumber.ToString()))
                { error = $"Couldn't locate or insert species {speciesId} in RegionalDex.c."; return false; }
            }

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
            // Newer checkouts generate the list from RegionalDex.c, so there is nothing to keep in step.
            if (!sort.Lists.ContainsKey("RegionalNum")) return true;

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
