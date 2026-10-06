using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace DSPRE.HgEngine
{
    /// <summary>Species to overworld-follower assignment. <c>src/field/overworld_table.c</c> gives each species
    /// <c>gfx = MON_OVERWORLD_GFX_START + speciesId</c> through one <c>MON_FOLLOWER_ENTRY(species, cbparams)</c>
    /// line, read and written here with a line scan. The sprite itself is the species' sprites/&lt;name&gt;/overworld.png
    /// (<see cref="HgEngineOverworldSprite"/>).</summary>
    public static class HgEngineOverworldFollowerSprite
    {
        private const string TableRelPath = "src/field/overworld_table.c";
        private const string SpeciesHeaderRelPath = "include/constants/species.h";
        private const string SizeClassPrefix = "OVERWORLD_SIZE_";

        // Sits right before the array's closing "};". A new entry must be inserted before this line,
        // not before the literal "};", which is followed by unrelated function definitions.
        private const string TerminatorLine = "{ 0xFFFF, 0, 0 },";

        private static readonly Regex EntryRegex = new(@"MON_FOLLOWER_ENTRY\(\s*(SPECIES_\w+)\s*,\s*(\w+)\s*\)");

        /// <summary>Pure text lookup, split out for direct unit testing.</summary>
        internal static bool TryFindEntry(string tableText, string designator, out Match match)
        {
            foreach (Match m in EntryRegex.Matches(tableText))
            {
                if (m.Groups[1].Value == designator) { match = m; return true; }
            }
            match = null;
            return false;
        }

        /// <summary>Inserts a new MON_FOLLOWER_ENTRY line (default size class OVERWORLD_SIZE_SMALL) right
        /// before the array's terminator sentinel. Pure text transform, unit-testable directly.</summary>
        internal static bool TryInsertEntry(ref string tableText, string designator, string sizeClassName)
        {
            int idx = tableText.IndexOf(TerminatorLine, System.StringComparison.Ordinal);
            if (idx < 0) return false;
            string newLine = $"MON_FOLLOWER_ENTRY({designator}, {sizeClassName})\n        ";
            tableText = tableText.Substring(0, idx) + newLine + tableText.Substring(idx);
            return true;
        }

        public static List<string> GetSizeClassOptions()
        {
            List<string> result = new List<string>();
            HgEngineSymbolTable table = HgEngineSymbolTable.Load(TableRelPath);
            if (table == null) return result;
            foreach (KeyValuePair<string, int> kv in table.ByName)
                if (kv.Key.StartsWith(SizeClassPrefix, System.StringComparison.Ordinal)) result.Add(kv.Key);
            result.Sort();
            return result;
        }

        /// <summary>Resolves a species' current gfx index (sprite-file number) and size class. Fails (does
        /// NOT synthesize a value) if the species has no MON_FOLLOWER_ENTRY line yet, use
        /// <see cref="TryEnsureEntry"/> first if the caller wants to create one.</summary>
        public static bool TryGetAssignment(int speciesId, out int gfxIndex, out string sizeClassName, out string error)
        {
            gfxIndex = -1; sizeClassName = null; error = null;
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout linked."; return false; }
            HgEngineSymbolTable species = HgEngineSymbolTable.Load(SpeciesHeaderRelPath);
            if (species == null || !species.TryGetNameWithPrefix(speciesId, "SPECIES_", out string designator))
            { error = $"Could not resolve a species designator for id {speciesId}."; return false; }

            string text = TryReadTable(out string path);
            if (text == null) { error = $"Source file not found: {path}"; return false; }

            if (!TryFindEntry(text, designator, out Match m))
            { error = $"No overworld follower entry for {designator} yet."; return false; }
            sizeClassName = m.Groups[2].Value;

            HgEngineSymbolTable table = HgEngineSymbolTable.Load(TableRelPath);
            if (table == null || !table.TryGetValue("MON_OVERWORLD_GFX_START", out int baseGfx))
            { error = "Could not resolve MON_OVERWORLD_GFX_START."; return false; }

            gfxIndex = baseGfx + speciesId;
            return true;
        }

        public static bool TrySetSizeClass(int speciesId, string sizeClassName, out string error)
        {
            error = null;
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout linked."; return false; }
            HgEngineSymbolTable species = HgEngineSymbolTable.Load(SpeciesHeaderRelPath);
            if (species == null || !species.TryGetNameWithPrefix(speciesId, "SPECIES_", out string designator))
            { error = $"Could not resolve a species designator for id {speciesId}."; return false; }

            string text = TryReadTable(out string path);
            if (text == null) { error = $"Source file not found: {path}"; return false; }
            if (!TryFindEntry(text, designator, out Match m))
            { error = $"No overworld follower entry for {designator} yet."; return false; }

            Group g2 = m.Groups[2];
            text = text.Substring(0, g2.Index) + sizeClassName + text.Substring(g2.Index + g2.Length);
            HgEngineFileCache.WriteText(path, text);
            return true;
        }

        /// <summary>Creates a MON_FOLLOWER_ENTRY line for a species that doesn't have one (e.g. a freshly
        /// added fakemon), defaulting to OVERWORLD_SIZE_SMALL. No-op (success) if one already exists.</summary>
        public static bool TryEnsureEntry(int speciesId, out int gfxIndex, out string error)
        {
            if (TryGetAssignment(speciesId, out gfxIndex, out _, out _)) { error = null; return true; }

            gfxIndex = -1; error = null;
            HgEngineSymbolTable species = HgEngineSymbolTable.Load(SpeciesHeaderRelPath);
            if (species == null || !species.TryGetNameWithPrefix(speciesId, "SPECIES_", out string designator))
            { error = $"Could not resolve a species designator for id {speciesId}."; return false; }

            string text = TryReadTable(out string path);
            if (text == null) { error = $"Source file not found: {path}"; return false; }
            if (!TryInsertEntry(ref text, designator, "OVERWORLD_SIZE_SMALL"))
            { error = "Could not locate the overworld table's terminator entry to insert next to."; return false; }
            HgEngineFileCache.WriteText(path, text);

            HgEngineSymbolTable table = HgEngineSymbolTable.Load(TableRelPath);
            if (table == null || !table.TryGetValue("MON_OVERWORLD_GFX_START", out int baseGfx))
            { error = "Could not resolve MON_OVERWORLD_GFX_START."; return false; }
            gfxIndex = baseGfx + speciesId;
            return true;
        }

        private static string TryReadTable(out string path)
        {
            path = Path.Combine(HgEngineProject.RepoPathUnc, TableRelPath.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(path) ? HgEngineFileCache.GetText(path) : null;
        }
    }
}
