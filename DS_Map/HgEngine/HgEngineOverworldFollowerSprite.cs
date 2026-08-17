using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace DSPRE.HgEngine
{
    /// <summary>Species to overworld-follower-sprite assignment + sprite-sheet source files. This fork
    /// uses a direct formula in <c>src/field/overworld_table.c</c>: <c>gfx = MON_OVERWORLD_GFX_START +
    /// speciesId</c>, set via one <c>MON_FOLLOWER_ENTRY(species, cbparams)</c> macro-call line per species.
    /// That's macro-call syntax, not a designated-initializer struct array, so it doesn't fit
    /// <see cref="HgEngineSourcePatcher"/>'s model; read/write here is a plain regex line scan/replace.
    /// Sprite files (<c>data/graphics/overworlds/&lt;gfx:D4&gt;.png/.json/.pal</c>) are read by decoding
    /// the PNG directly; <see cref="TryImportSprite"/> clones an existing entry's JSON+PAL and only
    /// replaces the art, since no atlas-frame authoring template exists to build a fresh one from.</summary>
    public static class HgEngineOverworldFollowerSprite
    {
        private const string TableRelPath = "src/field/overworld_table.c";
        private const string SpeciesHeaderRelPath = "include/constants/species.h";
        private const string SpriteDirRelPath = "data/graphics/overworlds";
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
            var result = new List<string>();
            var table = HgEngineSymbolTable.Load(TableRelPath);
            if (table == null) return result;
            foreach (var kv in table.ByName)
                if (kv.Key.StartsWith(SizeClassPrefix, System.StringComparison.Ordinal)) result.Add(kv.Key);
            result.Sort();
            return result;
        }

        /// <summary>Resolves a species' current gfx index (sprite-file number) and size class. Fails (does
        /// NOT synthesize a value) if the species has no MON_FOLLOWER_ENTRY line yet — use
        /// <see cref="TryEnsureEntry"/> first if the caller wants to create one.</summary>
        public static bool TryGetAssignment(int speciesId, out int gfxIndex, out string sizeClassName, out string error)
        {
            gfxIndex = -1; sizeClassName = null; error = null;
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout linked."; return false; }
            var species = HgEngineSymbolTable.Load(SpeciesHeaderRelPath);
            if (species == null || !species.TryGetNameWithPrefix(speciesId, "SPECIES_", out string designator))
            { error = $"Could not resolve a species designator for id {speciesId}."; return false; }

            string text = TryReadTable(out string path);
            if (text == null) { error = $"Source file not found: {path}"; return false; }

            if (!TryFindEntry(text, designator, out var m))
            { error = $"No overworld follower entry for {designator} yet."; return false; }
            sizeClassName = m.Groups[2].Value;

            var table = HgEngineSymbolTable.Load(TableRelPath);
            if (table == null || !table.TryGetValue("MON_OVERWORLD_GFX_START", out int baseGfx))
            { error = "Could not resolve MON_OVERWORLD_GFX_START."; return false; }

            gfxIndex = baseGfx + speciesId;
            return true;
        }

        public static bool TrySetSizeClass(int speciesId, string sizeClassName, out string error)
        {
            error = null;
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout linked."; return false; }
            var species = HgEngineSymbolTable.Load(SpeciesHeaderRelPath);
            if (species == null || !species.TryGetNameWithPrefix(speciesId, "SPECIES_", out string designator))
            { error = $"Could not resolve a species designator for id {speciesId}."; return false; }

            string text = TryReadTable(out string path);
            if (text == null) { error = $"Source file not found: {path}"; return false; }
            if (!TryFindEntry(text, designator, out var m))
            { error = $"No overworld follower entry for {designator} yet."; return false; }

            var g2 = m.Groups[2];
            text = text.Substring(0, g2.Index) + sizeClassName + text.Substring(g2.Index + g2.Length);
            File.WriteAllText(path, text);
            return true;
        }

        /// <summary>Creates a MON_FOLLOWER_ENTRY line for a species that doesn't have one (e.g. a freshly
        /// added fakemon), defaulting to OVERWORLD_SIZE_SMALL. No-op (success) if one already exists.</summary>
        public static bool TryEnsureEntry(int speciesId, out int gfxIndex, out string error)
        {
            if (TryGetAssignment(speciesId, out gfxIndex, out _, out _)) { error = null; return true; }

            gfxIndex = -1; error = null;
            var species = HgEngineSymbolTable.Load(SpeciesHeaderRelPath);
            if (species == null || !species.TryGetNameWithPrefix(speciesId, "SPECIES_", out string designator))
            { error = $"Could not resolve a species designator for id {speciesId}."; return false; }

            string text = TryReadTable(out string path);
            if (text == null) { error = $"Source file not found: {path}"; return false; }
            if (!TryInsertEntry(ref text, designator, "OVERWORLD_SIZE_SMALL"))
            { error = "Could not locate the overworld table's terminator entry to insert next to."; return false; }
            File.WriteAllText(path, text);

            var table = HgEngineSymbolTable.Load(TableRelPath);
            if (table == null || !table.TryGetValue("MON_OVERWORLD_GFX_START", out int baseGfx))
            { error = "Could not resolve MON_OVERWORLD_GFX_START."; return false; }
            gfxIndex = baseGfx + speciesId;
            return true;
        }

        public static bool HasSpriteFiles(int gfxIndex) =>
            File.Exists(SpritePath(gfxIndex, "png")) && File.Exists(SpritePath(gfxIndex, "json"));

        public static string TryGetSpritePngPath(int gfxIndex)
        {
            string p = SpritePath(gfxIndex, "png");
            return File.Exists(p) ? p : null;
        }

        private static readonly Regex PalFileNameFieldRegex = new("\"fileName\"\\s*:\\s*\"([^\"]*)\"");

        /// <summary>If the target gfx index has no JSON/palette yet, clones a template's metadata (never
        /// synthesizes a fresh atlas), then writes the chosen PNG. The cloned palette is renamed using
        /// <paramref name="targetLabel"/> and the JSON's "fileName" field updated to match, so two targets
        /// cloning the same template don't end up sharing one .pal file.</summary>
        public static bool TryImportSprite(int gfxIndex, string sourcePngPath, int? templateGfxIndex, string targetLabel, out string error)
        {
            error = null;
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout linked."; return false; }
            if (!File.Exists(sourcePngPath)) { error = "Source image not found."; return false; }

            string targetJson = SpritePath(gfxIndex, "json");
            if (!File.Exists(targetJson))
            {
                if (templateGfxIndex == null)
                { error = "This species has no sprite metadata yet; pick a template species to clone it from."; return false; }

                string templateJsonPath = SpritePath(templateGfxIndex.Value, "json");
                string templatePalPath = FindPalPath(templateGfxIndex.Value);
                if (!File.Exists(templateJsonPath) || templatePalPath == null)
                { error = "The chosen template species has no sprite metadata to clone."; return false; }

                string newPalFileName = $"{gfxIndex:D4}-{MakeFileSlug(targetLabel)}.pal";
                string jsonText = File.ReadAllText(templateJsonPath);
                var m = PalFileNameFieldRegex.Match(jsonText);
                if (m.Success)
                    jsonText = jsonText.Substring(0, m.Groups[1].Index) + newPalFileName + jsonText.Substring(m.Groups[1].Index + m.Groups[1].Length);
                File.WriteAllText(targetJson, jsonText);

                string dir = Path.GetDirectoryName(templatePalPath);
                File.Copy(templatePalPath, Path.Combine(dir!, newPalFileName), overwrite: true);
            }

            File.Copy(sourcePngPath, SpritePath(gfxIndex, "png"), overwrite: true);
            return true;
        }

        private static string MakeFileSlug(string label)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char c in (label ?? "").ToLowerInvariant())
                if (char.IsLetterOrDigit(c)) sb.Append(c);
            return sb.Length > 0 ? sb.ToString() : "sprite";
        }

        // The .pal file has a "-<name>" suffix DSPRE never generates content for (e.g. "0068-pikachu.pal"),
        // so it has to be located by prefix rather than assumed at a fixed path.
        private static string FindPalPath(int gfxIndex)
        {
            string dir = Path.Combine(HgEngineProject.RepoPathUnc, SpriteDirRelPath.Replace('/', '\\'));
            if (!Directory.Exists(dir)) return null;
            string prefix = gfxIndex.ToString("D4");
            foreach (var f in Directory.GetFiles(dir, prefix + "*.pal"))
                return f;
            return null;
        }

        private static string SpritePath(int gfxIndex, string extension) =>
            Path.Combine(HgEngineProject.RepoPathUnc, SpriteDirRelPath.Replace('/', '\\'), $"{gfxIndex:D4}.{extension}");

        private static string TryReadTable(out string path)
        {
            path = Path.Combine(HgEngineProject.RepoPathUnc, TableRelPath.Replace('/', '\\'));
            return File.Exists(path) ? HgEngineFileCache.GetText(path) : null;
        }
    }
}
