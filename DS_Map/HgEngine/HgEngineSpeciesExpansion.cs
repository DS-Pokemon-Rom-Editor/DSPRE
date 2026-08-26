using System.IO;

namespace DSPRE.HgEngine
{
    /// <summary>Mints a new base species ("fakemon"): a #define in species.h (bumping NUM_OF_FAKEMONS),
    /// and a minimal entry in Species.c with its own embedded name. Every downstream form family is
    /// defined relative to NUM_OF_FAKEMONS, so this shifts them all consistently. MAX_CANONICAL_MON_NUM
    /// stays untouched as the vanilla boundary marker. Adding a new form of an existing species has no
    /// equivalent official hook and isn't supported here.</summary>
    public static class HgEngineSpeciesExpansion
    {
        private const string HeaderRelPath = "include/constants/species.h";
        private const string SourceRelPath = "data/Species.c";
        private const string Prefix = "SPECIES_";

        public static bool TryGetVanillaBoundary(out int lastVanillaSpeciesId)
        {
            lastVanillaSpeciesId = -1;
            var species = HgEngineSymbolTable.Load(HeaderRelPath);
            return species != null && species.TryGetValue("MAX_CANONICAL_MON_NUM", out lastVanillaSpeciesId);
        }

        /// <summary>The [firstCustomId, firstCustomId + count) range of actually custom-added fakemons.</summary>
        public static bool TryGetCustomRange(out int firstCustomId, out int count)
        {
            firstCustomId = -1;
            count = 0;
            var species = HgEngineSymbolTable.Load(HeaderRelPath);
            if (species == null) return false;
            if (!species.TryGetValue("MAX_CANONICAL_MON_NUM", out int canonicalMax)) return false;
            if (!species.TryGetValue("NUM_OF_FAKEMONS", out int fakemonCount)) return false;
            firstCustomId = canonicalMax + 1;
            count = fakemonCount;
            return true;
        }

        /// <summary>pokegra.mk is static and never regenerated when a fakemon is inserted, even though
        /// every downstream form species shifts up by NUM_OF_FAKEMONS in personal.narc. Maps an id past
        /// the fakemon block back down to what pokegra.mk still calls it by; an id inside the fakemon
        /// block has no dump-time entry and correctly resolves to "not found". Shared by every domain
        /// that looks a species id up in pokegra.mk (icons, battle sprites).</summary>
        public static int AdjustForPokegraMkLookup(int speciesId)
        {
            if (!TryGetCustomRange(out int firstCustomId, out int fakemonCount) || fakemonCount == 0)
                return speciesId;

            if (speciesId < firstCustomId) return speciesId;
            if (speciesId < firstCustomId + fakemonCount) return -1;
            return speciesId - fakemonCount;
        }

        public static bool TryAddFakemon(string displayName, out int newSpeciesId, out string error)
        {
            newSpeciesId = -1;
            error = null;
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout linked."; return false; }

            var species = HgEngineSymbolTable.Load(HeaderRelPath);
            if (species == null) { error = $"Could not load {HeaderRelPath}."; return false; }
            if (!species.TryGetValue("MAX_CANONICAL_MON_NUM", out int canonicalMax))
            { error = "Could not find MAX_CANONICAL_MON_NUM in species.h."; return false; }
            if (!species.TryGetValue("NUM_OF_FAKEMONS", out int fakemonCount))
            { error = "Could not find NUM_OF_FAKEMONS in species.h."; return false; }

            int candidateId = canonicalMax + fakemonCount + 1;
            string slug = HgEngineNameSlug.ToUniqueSlug(displayName, species, Prefix);
            string designator = Prefix + slug;

            string headerPath = Path.Combine(HgEngineProject.RepoPathUnc, HeaderRelPath.Replace('/', '\\'));
            if (!File.Exists(headerPath)) { error = $"Source file not found: {headerPath}"; return false; }
            string headerText = File.ReadAllText(headerPath);

            if (!HgEngineHeaderEditor.TryInsertBeforeDefine(ref headerText, "NUM_OF_FAKEMONS", $"#define {designator} (MAX_CANONICAL_MON_NUM + {fakemonCount + 1})\n\n"))
            { error = "Could not find NUM_OF_FAKEMONS in species.h to anchor the new species next to."; return false; }
            if (!HgEngineHeaderEditor.TryReplaceDefineValue(ref headerText, "NUM_OF_FAKEMONS", (fakemonCount + 1).ToString()))
            { error = "Could not update NUM_OF_FAKEMONS."; return false; }

            string sourcePath = Path.Combine(HgEngineProject.RepoPathUnc, SourceRelPath.Replace('/', '\\'));
            if (!File.Exists(sourcePath)) { error = $"Source file not found: {sourcePath}"; return false; }
            string sourceText = File.ReadAllText(sourcePath);

            string safeName = displayName.Replace("\\", "\\\\").Replace("\"", "\\\"");
            string newEntry =
                $"\n[{designator}] = {{\n" +
                "    .textData = {\n" +
                $"        .name = \"{safeName}\",\n" +
                // pokedexEntry/classification/height/weight are const char* in species_data.h: leaving
                // any unset zero-inits to NULL, and speciesdatagen fputs()'s them with no NULL check.
                "        .pokedexEntry = \"\",\n" +
                "        .classification = \"????? Pokémon\",\n" +
                "        .height = \"???' ??\\\"\",\n" +
                "        .weight = \"????.? lbs.\",\n" +
                "    },\n" +
                "    .speciesData = {\n" +
                "        .baseStats = {\n" +
                "            .hp = 50,\n" +
                "            .attack = 50,\n" +
                "            .defense = 50,\n" +
                "            .spAttack = 50,\n" +
                "            .spDefense = 50,\n" +
                "            .speed = 50,\n" +
                "        },\n" +
                "        .types = { TYPE_NORMAL, TYPE_NORMAL },\n" +
                "        .catchRate = 45,\n" +
                "        .expRate = GROWTH_MEDIUM_FAST,\n" +
                "        .abilities = { ABILITY_NONE, ABILITY_NONE },\n" +
                "    },\n" +
                "},\n";
            if (!HgEngineHeaderEditor.TryInsertBeforeFinalCloseBrace(ref sourceText, newEntry))
            { error = $"Could not find the end of {SourceRelPath} to insert the new species."; return false; }

            File.WriteAllText(headerPath, headerText);
            File.WriteAllText(sourcePath, sourceText);
            HgEngineSymbolTable.ClearCache();

            // Also set the name in the binary text archive RomInfo.GetPokemonNames reads, so it shows
            // up immediately instead of only after a full "compile ROM" rebuild.
            var names = new ROMFiles.TextArchive(RomInfo.pokemonNamesTextNumbers[0]);
            while (names.messages.Count <= candidateId) names.messages.Add("");
            names.messages[candidateId] = displayName;
            names.SaveToExpandedDir(RomInfo.pokemonNamesTextNumbers[0], showSuccessMessage: false);

            newSpeciesId = candidateId;
            return true;
        }
    }
}
