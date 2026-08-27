using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using System.Collections.Generic;

namespace DSPRE.HgEngine
{
    /// <summary>Resolves data/Species.c's "-----" placeholder names into a readable name derived from
    /// the entry's own species.h constant (e.g. SPECIES_MEGA_VENUSAUR -> "Mega Venusaur"). Falls back to
    /// "&lt;base species&gt; (Form)" via data/FormToSpeciesMapping.c if the constant can't be resolved.</summary>
    public static class HgEngineFormNames
    {
        private const string PlaceholderName = "-----";
        private const string SpeciesPrefix = "SPECIES_";

        /// <summary>Replaces "-----" placeholder names in place, preferring a name derived from the
        /// entry's own species.h constant and falling back to "&lt;base species&gt; (Form)". No-op
        /// unless hg-engine is linked+active.</summary>
        public static void ApplyFallback(string[] names)
        {
            if (!HgEngineProject.IsActive || names == null) return;

            var species = HgEngineSymbolTable.Load("include/constants/species.h");
            var formToBase = HgEngineFormMapping.Load();
            if (species == null && formToBase == null) return;

            for (int i = 0; i < names.Length; i++)
            {
                if (names[i] != PlaceholderName) continue;

                if (species != null && TryReadableNameFromConstant(i, species, out string readable))
                {
                    names[i] = readable;
                    continue;
                }

                if (formToBase == null || !formToBase.TryGetValue(i, out int baseId)) continue;
                if (baseId < 0 || baseId >= names.Length) continue;
                if (names[baseId] == PlaceholderName) continue;   // base itself unresolved, don't chain garbage
                names[i] = $"{names[baseId]} (Form)";
            }
        }

        /// <summary>Turns a species.h constant into a readable name by stripping the "SPECIES_" prefix
        /// and title-casing each underscore-separated word (e.g. SPECIES_GIGANTAMAX_VENUSAUR -> "Gigantamax Venusaur").</summary>
        internal static bool TryReadableNameFromConstant(int speciesId, HgEngineSymbolTable species, out string name)
        {
            name = null;
            if (!species.TryGetNameWithPrefix(speciesId, SpeciesPrefix, out string symbol)) return false;
            string body = symbol[SpeciesPrefix.Length..];
            string[] words = body.Split('_', System.StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) return false;

            var sb = new StringBuilder();
            foreach (string w in words)
            {
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(char.ToUpperInvariant(w[0]));
                if (w.Length > 1) sb.Append(w[1..].ToLowerInvariant());
            }
            name = sb.ToString();
            return true;
        }
    }

    /// <summary>Shared parse of data/FormToSpeciesMapping.c (form species ID -> base species ID), used by
    /// both the display-name fallback and the Form Editor.</summary>
    public static class HgEngineFormMapping
    {
        public static Dictionary<int, int> Load()
        {
            var species = HgEngineSymbolTable.Load("include/constants/species.h");
            if (species == null) return null;

            string mappingPath = Path.Combine(HgEngineProject.RepoPathUnc, "data", "FormToSpeciesMapping.c");
            if (!File.Exists(mappingPath)) return null;

            var result = new Dictionary<int, int>();
            foreach (Match m in Regex.Matches(HgEngineFileCache.GetText(mappingPath), @"\[\s*(SPECIES_\w+)\s*-\s*SPECIES_MEGA_START\s*\]\s*=\s*(SPECIES_\w+)\s*,"))
            {
                if (species.TryGetValue(m.Groups[1].Value, out int formId) &&
                    species.TryGetValue(m.Groups[2].Value, out int baseId))
                    result[formId] = baseId;
            }
            return result;
        }
    }
}
