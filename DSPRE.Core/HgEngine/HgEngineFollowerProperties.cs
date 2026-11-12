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
        private const string NamePrefix = "OVERWORLD_";
        private const string BouncePrefix = "OVERWORLD_BOUNCE_";
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

            HgEngineSymbolTable names = HgEngineSymbolTable.Load(SourceRelPath);
            if (HgEngineSourcePatcher.TryGetFieldValue(text, designator, SizePath, out string sizeRaw))
                HgEngineSymbolTable.TryValue(sizeRaw, out size, names);
            if (HgEngineSourcePatcher.TryGetFieldValue(text, designator, BouncePath, out string bounceRaw))
                HgEngineSymbolTable.TryValue(bounceRaw, out bounce, names);
            return true;
        }

        private static string SizeLiteral(int size, HgEngineSymbolTable names)
        {
            if (names != null)
                foreach (KeyValuePair<string, int> kv in names.ByName)
                    if (kv.Value == size && IsSizeName(kv.Key)) return kv.Key;
            return size.ToString();
        }

        private static bool IsSizeName(string name) => name.StartsWith(NamePrefix, System.StringComparison.Ordinal)
            && !name.StartsWith(BouncePrefix, System.StringComparison.Ordinal);

        public static bool TrySet(int speciesId, int size, int bounce, out string error)
        {
            error = null;
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout is linked."; return false; }
            HgEngineSymbolTable species = HgEngineSymbolTable.Load(SpeciesHeaderRelPath);
            if (species == null || !species.TryGetNameWithPrefix(speciesId, "SPECIES_", out string designator))
            { error = $"Species {speciesId} has no SPECIES_ name in the checkout."; return false; }

            string text = TryReadSource(out string path);
            if (text == null) { error = $"{path} is missing from the checkout."; return false; }

            HgEngineSymbolTable names = HgEngineSymbolTable.Load(SourceRelPath);
            string bounceLiteral = names != null && names.TryGetNameWithPrefix(bounce, BouncePrefix, out string bn)
                ? bn : bounce.ToString();

            if (HgEngineSourcePatcher.TryFindEntry(text, designator, out _, out _))
            {
                // Skip unchanged fields so values that can't be parsed, like (1), stay as written.
                bool hasSize = HgEngineSourcePatcher.TryGetFieldValue(text, designator, SizePath, out string sizeRaw);
                HgEngineSymbolTable.TryValue(sizeRaw, out int oldSize, names);
                bool okSize = (hasSize && oldSize == size)
                    || HgEngineSourcePatcher.TryUpsertField(ref text, designator, SizePath, SizeLiteral(size, names));
                bool hasBounce = HgEngineSourcePatcher.TryGetFieldValue(text, designator, BouncePath, out string bounceRaw);
                HgEngineSymbolTable.TryValue(bounceRaw, out int oldBounce, names);
                bool okBounce = (hasBounce && oldBounce == bounce)
                    || HgEngineSourcePatcher.TryUpsertField(ref text, designator, BouncePath, bounceLiteral);
                if (!okSize || !okBounce) { error = "Couldn't locate or insert .size/.bounce in the existing entry."; return false; }
            }
            else
            {
                string newEntry = $"    [{designator}] = {{.size = {SizeLiteral(size, names)}, .bounce = {bounceLiteral}}},\n";
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
