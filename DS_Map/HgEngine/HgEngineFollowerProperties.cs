using System.Collections.Generic;
using System.IO;

namespace DSPRE.HgEngine
{
    /// <summary>Source-text read/write for data/FollowerProperties.c's <c>FollowerProperties[]</c>
    /// (<c>.size</c>, <c>.bounce</c>; <c>unk0</c>/<c>unk3</c> are never set, left alone). A real
    /// designated-initializer struct array, so this goes through <see cref="HgEngineSourcePatcher"/>
    /// rather than <see cref="HgEngineFlatArrayField"/>. <c>.size</c> is exposed as a raw 0/1 instead of
    /// resolving <c>OVERWORLD_CAN_ENTER</c>/<c>OVERWORLD_NO_ENTRY</c>, since those names describe an
    /// unrelated door/area-entry concept this fork repurposed for size. <c>.bounce</c> resolves through
    /// the real <c>OVERWORLD_BOUNCE_*</c> names, read dynamically from this file.</summary>
    public static class HgEngineFollowerProperties
    {
        private const string SourceRelPath = "data/FollowerProperties.c";
        private const string SpeciesHeaderRelPath = "include/constants/species.h";
        private static readonly FieldPathSegment[] SizePath = { FieldPathSegment.Field("size") };
        private static readonly FieldPathSegment[] BouncePath = { FieldPathSegment.Field("bounce") };

        /// <summary>Species with no entry at all (never dumped, or a fakemon added after the last dump)
        /// read as size=0/bounce=0 (the struct's own C zero-init default) with <paramref name="hasEntry"/>
        /// false, rather than failing.</summary>
        public static bool TryGet(int speciesId, out int size, out int bounce, out bool hasEntry, out string error)
        {
            size = 0; bounce = 0; hasEntry = false; error = null;
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout linked."; return false; }
            var species = HgEngineSymbolTable.Load(SpeciesHeaderRelPath);
            if (species == null || !species.TryGetNameWithPrefix(speciesId, "SPECIES_", out string designator))
            { error = $"Could not resolve a species designator for id {speciesId}."; return false; }

            string text = TryReadSource(out string path);
            if (text == null) { error = $"Source file not found: {path}"; return false; }
            if (!HgEngineSourcePatcher.TryFindEntry(text, designator, out _, out _)) return true;   // no entry: defaults
            hasEntry = true;

            var bounceNames = HgEngineSymbolTable.Load(SourceRelPath);
            if (HgEngineSourcePatcher.TryGetFieldValue(text, designator, SizePath, out string sizeRaw))
                int.TryParse(sizeRaw, out size);
            if (HgEngineSourcePatcher.TryGetFieldValue(text, designator, BouncePath, out string bounceRaw))
            {
                if (bounceNames != null && bounceNames.TryGetValue(bounceRaw, out int bv)) bounce = bv;
                else int.TryParse(bounceRaw, out bounce);
            }
            return true;
        }

        public static bool TrySet(int speciesId, int size, int bounce, out string error)
        {
            error = null;
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout linked."; return false; }
            var species = HgEngineSymbolTable.Load(SpeciesHeaderRelPath);
            if (species == null || !species.TryGetNameWithPrefix(speciesId, "SPECIES_", out string designator))
            { error = $"Could not resolve a species designator for id {speciesId}."; return false; }

            string text = TryReadSource(out string path);
            if (text == null) { error = $"Source file not found: {path}"; return false; }

            var bounceNames = HgEngineSymbolTable.Load(SourceRelPath);
            string bounceLiteral = bounceNames != null && bounceNames.TryGetNameWithPrefix(bounce, "OVERWORLD_BOUNCE_", out string bn)
                ? bn : bounce.ToString();

            if (HgEngineSourcePatcher.TryFindEntry(text, designator, out _, out _))
            {
                bool okSize = HgEngineSourcePatcher.TryUpsertField(ref text, designator, SizePath, size.ToString());
                bool okBounce = HgEngineSourcePatcher.TryUpsertField(ref text, designator, BouncePath, bounceLiteral);
                if (!okSize || !okBounce) { error = "Could not locate or insert .size/.bounce in the existing entry."; return false; }
            }
            else
            {
                string newEntry = $"\n    [{designator}] = {{.size = {size}, .bounce = {bounceLiteral}}},";
                if (!HgEngineHeaderEditor.TryInsertBeforeFinalCloseBrace(ref text, newEntry))
                { error = $"Could not insert a new FollowerProperties entry for species {speciesId}."; return false; }
            }

            File.WriteAllText(path, text);
            return true;
        }

        /// <summary>The real bounce-speed names declared in this file, in declaration order, for a
        /// dynamic UI dropdown (never hardcode which speeds exist).</summary>
        public static List<(string Name, int Value)> GetBounceOptions()
        {
            var result = new List<(string Name, int Value)>();
            var table = HgEngineSymbolTable.Load(SourceRelPath);
            if (table == null) return result;
            foreach (var kv in table.ByName)
                if (kv.Key.StartsWith("OVERWORLD_BOUNCE_")) result.Add((kv.Key, kv.Value));
            result.Sort((a, b) => a.Value.CompareTo(b.Value));
            return result;
        }

        private static string TryReadSource(out string path)
        {
            path = Path.Combine(HgEngineProject.RepoPathUnc, SourceRelPath.Replace('/', '\\'));
            return File.Exists(path) ? HgEngineFileCache.GetText(path) : null;
        }
    }
}
