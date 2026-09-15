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

        /// <summary>A new species worked out against the checkout's text: its id, its constant and the full
        /// species.h and Species.c texts that add it. Nothing has been written.</summary>
        public sealed record FakemonPlan(int SpeciesId, string Designator, string DisplayName, string HeaderText, string SourceText);

        /// <summary>Works out a new species from the checkout as it is now, writing nothing.</summary>
        public static bool TryPlanFakemon(string displayName, out FakemonPlan plan, out string error)
        {
            plan = null;
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout linked."; return false; }
            if (!TryGetPaths(out string headerPath, out string sourcePath, out error)) return false;
            return TryPlanFakemon(displayName, HgEngineSymbolTable.Load(HeaderRelPath),
                HgEngineFileCache.GetText(headerPath), HgEngineFileCache.GetText(sourcePath), out plan, out error);
        }

        internal static bool TryPlanFakemon(string displayName, HgEngineSymbolTable species, string headerText, string sourceText,
            out FakemonPlan plan, out string error)
        {
            plan = null;
            error = null;
            if (species == null) { error = $"Could not load {HeaderRelPath}."; return false; }
            if (!species.TryGetValue("MAX_CANONICAL_MON_NUM", out int canonicalMax))
            { error = "Could not find MAX_CANONICAL_MON_NUM in species.h."; return false; }
            if (!species.TryGetValue("NUM_OF_FAKEMONS", out int fakemonCount))
            { error = "Could not find NUM_OF_FAKEMONS in species.h."; return false; }

            int candidateId = canonicalMax + fakemonCount + 1;
            string designator = Prefix + HgEngineNameSlug.ToUniqueSlug(displayName, species, Prefix);

            if (!HgEngineHeaderEditor.TryInsertBeforeDefine(ref headerText, "NUM_OF_FAKEMONS", $"#define {designator} (MAX_CANONICAL_MON_NUM + {fakemonCount + 1})\n\n"))
            { error = "Could not find NUM_OF_FAKEMONS in species.h to anchor the new species next to."; return false; }
            if (!HgEngineHeaderEditor.TryReplaceDefineValue(ref headerText, "NUM_OF_FAKEMONS", (fakemonCount + 1).ToString()))
            { error = "Could not update NUM_OF_FAKEMONS."; return false; }

            if (!HgEngineHeaderEditor.TryInsertBeforeFinalCloseBrace(ref sourceText, BuildSpeciesEntry(designator, displayName)))
            { error = $"Could not find the end of {SourceRelPath} to insert the new species."; return false; }

            plan = new FakemonPlan(candidateId, designator, displayName, headerText, sourceText);
            return true;
        }

        /// <summary>Plans the species again from the files as they are now, so edits saved since it was
        /// first planned are kept, and writes it. Inside an HgEngineWriteSession the writes wait for the commit.</summary>
        public static bool TryWriteFakemon(string displayName, out FakemonPlan written, out string error)
        {
            if (!TryPlanFakemon(displayName, out written, out error)) return false;
            if (!TryGetPaths(out string headerPath, out string sourcePath, out error)) return false;
            WriteFakemon(written, headerPath, sourcePath);
            return true;
        }

        internal static void WriteFakemon(FakemonPlan plan, string headerPath, string sourcePath)
        {
            HgEngineFileCache.WriteText(headerPath, plan.HeaderText);
            HgEngineFileCache.WriteText(sourcePath, plan.SourceText);
        }

        /// <summary>Call once the species' source writes are on disk. The ROM copy's name archive isn't held
        /// by a write session, so it must not be touched before then.</summary>
        public static void FinishFakemon(FakemonPlan written)
        {
            HgEngineSymbolTable.ClearCache();

            // Shows the name now instead of only after a full "compile ROM" rebuild. Forms are numbered after
            // the new species and move up one, as they will in the rebuilt archive, so the name is inserted.
            var names = new ROMFiles.TextArchive(RomInfo.pokemonNamesTextNumbers[0]);
            while (names.messages.Count < written.SpeciesId) names.messages.Add("");
            names.messages.Insert(written.SpeciesId, written.DisplayName);
            names.SaveToExpandedDir(RomInfo.pokemonNamesTextNumbers[0], showSuccessMessage: false);
        }

        private static bool TryGetPaths(out string headerPath, out string sourcePath, out string error)
        {
            error = null;
            headerPath = Path.Combine(HgEngineProject.RepoPathUnc, HeaderRelPath.Replace('/', '\\'));
            sourcePath = Path.Combine(HgEngineProject.RepoPathUnc, SourceRelPath.Replace('/', '\\'));
            if (!File.Exists(headerPath)) { error = $"Source file not found: {headerPath}"; return false; }
            if (!File.Exists(sourcePath)) { error = $"Source file not found: {sourcePath}"; return false; }
            return true;
        }

        /// <summary>A Species.c entry with every field a real one declares, so editors that write a
        /// sub-field find its block. Goes right before the table's closing "};".</summary>
        internal static string BuildSpeciesEntry(string designator, string displayName)
        {
            string safeName = displayName.Replace("\\", "\\\\").Replace("\"", "\\\"");
            return
                $"    [{designator}] = {{\n" +
                "        .textData = {\n" +
                $"            .name = \"{safeName}\",\n" +
                // These are const char*: an unset one is NULL, and speciesdatagen fputs()'s them with no NULL check.
                "            .pokedexEntry = \"\",\n" +
                "            .classification = \"????? Pokémon\",\n" +
                "            .height = \"???’??”\",\n" +
                "            .weight = \"????.? lbs.\",\n" +
                "        },\n" +
                "        .speciesData = {\n" +
                "            .baseStats = {\n" +
                "                .hp = 50,\n" +
                "                .attack = 50,\n" +
                "                .defense = 50,\n" +
                "                .spAttack = 50,\n" +
                "                .spDefense = 50,\n" +
                "                .speed = 50,\n" +
                "            },\n" +
                "            .types = { TYPE_NORMAL, TYPE_NORMAL },\n" +
                "            .catchRate = 45,\n" +
                "            .baseExpRewardPadding = 0,\n" +
                "            .evYields = {\n" +
                "                .hp = 0,\n" +
                "                .attack = 0,\n" +
                "                .defense = 0,\n" +
                "                .spAttack = 0,\n" +
                "                .spDefense = 0,\n" +
                "                .speed = 0,\n" +
                "            },\n" +
                "            .wildHeldItems = {\n" +
                "                .common = ITEM_NONE,\n" +
                "                .rare = ITEM_NONE,\n" +
                "            },\n" +
                "            .genderRatio = 127,\n" +
                "            .hatchCycles = 20,\n" +
                "            .baseFriendship = 50,\n" +
                "            .expRate = GROWTH_MEDIUM_FAST,\n" +
                "            .eggGroups = { EGG_GROUP_NONE, EGG_GROUP_NONE },\n" +
                "            .abilities = { ABILITY_NONE, ABILITY_NONE },\n" +
                "            .safariFleeRate = 0,\n" +
                "            .bodyColor = BODY_COLOR_RED,\n" +
                "            .flipSprite = 0,\n" +
                "        },\n" +
                // SPECIES_NONE's metrics.
                "        .metricsData = {\n" +
                "            .heightDecimetres = 7,\n" +
                "            .weightHectograms = 69,\n" +
                "            .bodyType = DEX_SEARCH_BODYTYPE_QUADRUPED,\n" +
                "            .femaleTrainerScale = 272,\n" +
                "            .femalePokemonScale = 337,\n" +
                "            .maleTrainerScale = 256,\n" +
                "            .malePokemonScale = 337,\n" +
                "            .femaleTrainerYOffset = 8,\n" +
                "            .femalePokemonYOffset = 24,\n" +
                "            .maleTrainerYOffset = 9,\n" +
                "            .malePokemonYOffset = 24,\n" +
                "        },\n" +
                "    },\n\n";
        }
    }
}
