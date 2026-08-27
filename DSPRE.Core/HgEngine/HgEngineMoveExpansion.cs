using System.IO;

namespace DSPRE.HgEngine
{
    /// <summary>Mints a new move: a #define in moves.h (bumping NUM_OF_CUSTOM_MOVES), and a matching
    /// entry in Moves.c with its own embedded name/description. NUM_OF_CANONICAL_MOVES stays untouched
    /// as the vanilla boundary marker.</summary>
    public static class HgEngineMoveExpansion
    {
        private const string HeaderRelPath = "include/constants/moves.h";
        private const string SourceRelPath = "data/Moves.c";
        private const string Prefix = "MOVE_";

        public static bool TryGetVanillaBoundary(out int lastVanillaMoveId)
        {
            lastVanillaMoveId = -1;
            var moves = HgEngineSymbolTable.Load(HeaderRelPath);
            // NUM_OF_CANONICAL_MOVES is a count, not a max id.
            if (moves == null || !moves.TryGetValue("NUM_OF_CANONICAL_MOVES", out int canonicalCount) || canonicalCount <= 0)
                return false;
            lastVanillaMoveId = canonicalCount - 1;
            return true;
        }

        /// <summary>The [firstCustomId, firstCustomId + count) range of actually custom-added moves.</summary>
        public static bool TryGetCustomRange(out int firstCustomId, out int count)
        {
            firstCustomId = -1;
            count = 0;
            var moves = HgEngineSymbolTable.Load(HeaderRelPath);
            if (moves == null) return false;
            if (!moves.TryGetValue("NUM_OF_CANONICAL_MOVES", out int canonicalCount)) return false;
            if (!moves.TryGetValue("NUM_OF_CUSTOM_MOVES", out int customCount)) return false;
            firstCustomId = canonicalCount;
            count = customCount;
            return true;
        }

        public static bool TryAddMove(string displayName, out int newMoveId, out string error)
        {
            newMoveId = -1;
            error = null;
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout linked."; return false; }

            var moves = HgEngineSymbolTable.Load(HeaderRelPath);
            if (moves == null) { error = $"Could not load {HeaderRelPath}."; return false; }
            if (!moves.TryGetValue("NUM_OF_CANONICAL_MOVES", out int canonicalCount))
            { error = "Could not find NUM_OF_CANONICAL_MOVES in moves.h."; return false; }
            if (!moves.TryGetValue("NUM_OF_CUSTOM_MOVES", out int customCount))
            { error = "Could not find NUM_OF_CUSTOM_MOVES in moves.h."; return false; }

            int candidateId = canonicalCount + customCount;
            string slug = HgEngineNameSlug.ToUniqueSlug(displayName, moves, Prefix);
            string designator = Prefix + slug;

            string headerPath = Path.Combine(HgEngineProject.RepoPathUnc, HeaderRelPath.Replace('/', '\\'));
            if (!File.Exists(headerPath)) { error = $"Source file not found: {headerPath}"; return false; }
            string headerText = File.ReadAllText(headerPath);

            if (!HgEngineHeaderEditor.TryInsertBeforeDefine(ref headerText, "NUM_OF_CUSTOM_MOVES", $"#define {designator} (NUM_OF_CANONICAL_MOVES + {customCount})\n\n"))
            { error = "Could not find NUM_OF_CUSTOM_MOVES in moves.h to anchor the new move next to."; return false; }
            if (!HgEngineHeaderEditor.TryReplaceDefineValue(ref headerText, "NUM_OF_CUSTOM_MOVES", (customCount + 1).ToString()))
            { error = "Could not update NUM_OF_CUSTOM_MOVES."; return false; }

            string sourcePath = Path.Combine(HgEngineProject.RepoPathUnc, SourceRelPath.Replace('/', '\\'));
            if (!File.Exists(sourcePath)) { error = $"Source file not found: {sourcePath}"; return false; }
            string sourceText = File.ReadAllText(sourcePath);

            string safeName = EscapeCString(displayName);
            string safeCaps = EscapeCString(displayName.ToUpperInvariant());
            string newEntry =
                $"\n[{designator}] = {{\n" +
                "    .names = {\n" +
                $"        .name = \"{safeName}\",\n" +
                $"        .capsName = \"{safeCaps}\",\n" +
                $"        .fullName = \"{safeName}\",\n" +
                "    },\n" +
                "    .data = {\n" +
                "        .effect = MOVE_EFFECT_HIT,\n" +
                "        .split = SPLIT_STATUS,\n" +
                "        .power = 0,\n" +
                "        .type = TYPE_NORMAL,\n" +
                "        .accuracy = 0,\n" +
                "        .pp = 5,\n" +
                "        .effectChance = 0,\n" +
                "    },\n" +
                "    .battle = {\n" +
                "        .target = RANGE_SINGLE_TARGET,\n" +
                "        .priority = 0,\n" +
                "        .flags = 0x00,\n" +
                "    },\n" +
                "    .contest = {\n" +
                "        .appeal = 0,\n" +
                "        .contestType = CONTEST_COOL,\n" +
                "    },\n" +
                "    .description = \"\\\\n\\\\n\\\\n\\\\n\",\n" +
                "},\n";
            if (!HgEngineHeaderEditor.TryInsertBeforeFinalCloseBrace(ref sourceText, newEntry))
            { error = $"Could not find the end of {SourceRelPath} to insert the new move."; return false; }

            File.WriteAllText(headerPath, headerText);
            File.WriteAllText(sourcePath, sourceText);
            HgEngineSymbolTable.ClearCache();

            // Also set the name in the binary text archive RomInfo.GetAttackNames reads, so it shows
            // up immediately instead of only after a full "compile ROM" rebuild.
            var names = new ROMFiles.TextArchive(RomInfo.attackNamesTextNumber);
            while (names.messages.Count <= candidateId) names.messages.Add("");
            names.messages[candidateId] = displayName;
            names.SaveToExpandedDir(RomInfo.attackNamesTextNumber, showSuccessMessage: false);

            newMoveId = candidateId;
            return true;
        }

        private static string EscapeCString(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
