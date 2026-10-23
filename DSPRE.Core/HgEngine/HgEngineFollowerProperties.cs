using System.Collections.Generic;
using System.IO;

namespace DSPRE.HgEngine
{
    /// <summary>data/FollowerProperties.c's .size and .bounce. Despite the name, a .size of 1 keeps the follower out
    /// of maps that limit tall followers.</summary>
    /// <remarks>.size is OVERWORLD_CAN_ENTER (0) or OVERWORLD_NO_ENTRY (1); .bounce uses the file's OVERWORLD_BOUNCE_* names.
    /// unk0 and unk3 are left alone.</remarks>
    public static class HgEngineFollowerProperties
    {
        private const string SourceRelPath = "data/FollowerProperties.c";
        private const string SpeciesHeaderRelPath = "include/constants/species.h";
        private static readonly FieldPathSegment[] SizePath = { FieldPathSegment.Field("size") };
        private static readonly FieldPathSegment[] BouncePath = { FieldPathSegment.Field("bounce") };

        /// <summary>
        /// A species with no entry reads as size 0 and bounce 0, the struct's zero default, with <paramref
        /// name="hasEntry"/> false.
        /// </summary>
        public static bool TryGet(int speciesId, out int size, out int bounce, out bool hasEntry, out string error)
        {
            size = 0; bounce = 0; hasEntry = false; error = null;
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout is linked."; return false; }
            HgEngineSymbolTable species = HgEngineSymbolTable.Load(SpeciesHeaderRelPath);
            if (species == null || !species.TryGetNameWithPrefix(speciesId, "SPECIES_", out string designator))
            { error = $"Species {speciesId} has no SPECIES_ name in the checkout."; return false; }

            string text = TryReadSource(out string path);
            if (text == null) { error = $"{path} is missing from the checkout."; return false; }
            if (!HgEngineSourcePatcher.TryFindEntry(text, designator, out _, out _)) return true;   // no entry: defaults
            hasEntry = true;

            HgEngineSymbolTable bounceNames = HgEngineSymbolTable.Load(SourceRelPath);
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
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout is linked."; return false; }
            HgEngineSymbolTable species = HgEngineSymbolTable.Load(SpeciesHeaderRelPath);
            if (species == null || !species.TryGetNameWithPrefix(speciesId, "SPECIES_", out string designator))
            { error = $"Species {speciesId} has no SPECIES_ name in the checkout."; return false; }

            string text = TryReadSource(out string path);
            if (text == null) { error = $"{path} is missing from the checkout."; return false; }

            HgEngineSymbolTable bounceNames = HgEngineSymbolTable.Load(SourceRelPath);
            string bounceLiteral = bounceNames != null && bounceNames.TryGetNameWithPrefix(bounce, "OVERWORLD_BOUNCE_", out string bn)
                ? bn : bounce.ToString();

            if (HgEngineSourcePatcher.TryFindEntry(text, designator, out _, out _))
            {
                bool okSize = HgEngineSourcePatcher.TryUpsertField(ref text, designator, SizePath, size.ToString());
                bool okBounce = HgEngineSourcePatcher.TryUpsertField(ref text, designator, BouncePath, bounceLiteral);
                if (!okSize || !okBounce) { error = "Couldn't locate or insert .size/.bounce in the existing entry."; return false; }
            }
            else
            {
                string newEntry = $"\n    [{designator}] = {{.size = {size}, .bounce = {bounceLiteral}}},";
                if (!HgEngineHeaderEditor.TryInsertBeforeFinalCloseBrace(ref text, newEntry))
                { error = $"Couldn't insert a new FollowerProperties entry for species {speciesId}."; return false; }
            }

            HgEngineFileCache.WriteText(path, text);
            return true;
        }

        /// <summary>The real bounce-speed names declared in this file, in declaration order, for a
        /// dynamic UI dropdown (never hardcode which speeds exist).</summary>
        public static List<(string Name, int Value)> GetBounceOptions()
        {
            List<(string Name, int Value)> result = new List<(string Name, int Value)>();
            HgEngineSymbolTable table = HgEngineSymbolTable.Load(SourceRelPath);
            if (table == null) return result;
            foreach (KeyValuePair<string, int> kv in table.ByName)
                if (kv.Key.StartsWith("OVERWORLD_BOUNCE_")) result.Add((kv.Key, kv.Value));
            result.Sort((a, b) => a.Value.CompareTo(b.Value));
            return result;
        }

        private static string TryReadSource(out string path)
        {
            path = Path.Combine(HgEngineProject.RepoPathUnc, SourceRelPath.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(path) ? HgEngineFileCache.GetText(path) : null;
        }
    }
}
