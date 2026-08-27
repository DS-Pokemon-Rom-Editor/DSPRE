using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using DSPRE.ROMFiles;

namespace DSPRE.HgEngine
{
    /// <summary>Source-text read/write for data/Headbutt.c: one giant struct with a hand-named dotted
    /// field per map (~540), not an indexable array. Each field's own struct type name embeds its index
    /// (<c>HeadbuttFile_009_Route_1 route1;</c>), so the index-to-field-name map is parsed from that naming
    /// convention rather than assumed from declaration order. Reuses the vanilla
    /// <see cref="HeadbuttEncounterFile"/>/<see cref="HeadbuttTreeGroup"/>/<see cref="HeadbuttTree"/>/
    /// <see cref="HeadbuttEncounter"/> POCOs as the in-memory shape, so the existing UI works unchanged.
    /// Adding/removing whole tree groups, or enabling trees on a map that has none, isn't supported: both
    /// need editing the per-map struct's type declaration, not just its data.</summary>
    public static class HgEngineHeadbutt
    {
        private const string SourceRelPath = "data/Headbutt.c";
        private const string SpeciesHeaderRelPath = "include/constants/species.h";
        private static readonly Regex MemberRegex = new(@"HeadbuttFile_(\d+)_\w+\s+(\w+)\s*;");
        private static readonly Regex DataAnchor = new(@"const\s+HeadbuttArchiveData\s+__data\s*=\s*\{");

        public static bool TryGetFieldName(int headbuttFileIndex, out string fieldName)
        {
            fieldName = null;
            string text = TryReadSource(out _);
            return text != null && TryFindFieldName(text, headbuttFileIndex, out fieldName);
        }

        /// <summary>Pure index-&gt;field-name resolution over already-loaded source text, split out from
        /// <see cref="TryGetFieldName"/> so it's directly unit-testable against a synthetic snippet.</summary>
        internal static bool TryFindFieldName(string headbuttSourceText, int headbuttFileIndex, out string fieldName)
        {
            fieldName = null;
            foreach (Match m in MemberRegex.Matches(headbuttSourceText))
            {
                if (int.TryParse(m.Groups[1].Value, out int idx) && idx == headbuttFileIndex)
                { fieldName = m.Groups[2].Value; return true; }
            }
            return false;
        }

        public static bool TryLoad(int headbuttFileIndex, out HeadbuttEncounterFile file, out string error)
        {
            file = null; error = null;
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout linked."; return false; }
            if (!TryGetFieldName(headbuttFileIndex, out string fieldName))
            { error = $"Could not resolve a Headbutt.c field for file index {headbuttFileIndex}."; return false; }

            string text = TryReadSource(out string path);
            if (text == null) { error = $"Source file not found: {path}"; return false; }
            if (!TryFindDataBlock(text, out int open, out int close))
            { error = "Could not locate the Headbutt.c __data initializer."; return false; }
            if (!ElementScanner.TryLocateValueSpan(text, open, close, new[] { FieldPathSegment.Field(fieldName) }, out int vs, out int ve))
            { error = $"Could not locate .{fieldName} in Headbutt.c."; return false; }
            string mapBlock = text.Substring(vs, ve - vs);

            file = new HeadbuttEncounterFile { ID = (ushort)headbuttFileIndex };
            var species = HgEngineSymbolTable.Load(SpeciesHeaderRelPath);

            int normalTreeCount = 0, specialTreeCount = 0;
            if (HgEngineSourcePatcher.TryGetFieldValueInBlock(mapBlock, new[] { FieldPathSegment.Field("normalTreeCount") }, out string ntc))
                int.TryParse(ntc.Trim(), out normalTreeCount);
            if (HgEngineSourcePatcher.TryGetFieldValueInBlock(mapBlock, new[] { FieldPathSegment.Field("specialTreeCount") }, out string stc))
                int.TryParse(stc.Trim(), out specialTreeCount);

            ReadSlots(mapBlock, "normalSlots", species, file.normalEncounters, 12);
            ReadSlots(mapBlock, "specialSlots", species, file.specialEncounters, 6);
            ReadGroups(mapBlock, normalTreeCount, specialTreeCount, file.normalTreeGroups, file.specialTreeGroups);

            file.normalTreeGroupsCount = (byte)file.normalTreeGroups.Count;
            file.specialTreeGroupsCount = (byte)file.specialTreeGroups.Count;
            return true;
        }

        public static bool TrySave(int headbuttFileIndex, HeadbuttEncounterFile file, out string error)
        {
            error = null;
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout linked."; return false; }
            if (!TryGetFieldName(headbuttFileIndex, out string fieldName))
            { error = $"Could not resolve a Headbutt.c field for file index {headbuttFileIndex}."; return false; }

            string text = TryReadSource(out string path);
            if (text == null) { error = $"Source file not found: {path}"; return false; }

            var species = HgEngineSymbolTable.Load(SpeciesHeaderRelPath);
            var failed = new List<string>();

            string SlotsLiteral(IReadOnlyList<HeadbuttEncounter> list, int count)
            {
                var items = new List<string>(count);
                for (int i = 0; i < count; i++)
                {
                    var e = i < list.Count ? list[i] : new HeadbuttEncounter();
                    string sp = species != null && species.TryGetNameWithPrefix(e.pokemonID, "SPECIES_", out string sn) ? sn : e.pokemonID.ToString();
                    items.Add($"{{ {sp}, {e.minLevel}, {e.maxLevel} }}");
                }
                return "{ " + string.Join(", ", items) + " }";
            }

            if (!TryReplaceMapField(ref text, fieldName, "normalSlots", SlotsLiteral(file.normalEncounters, 12))) failed.Add("normalSlots");
            if (!TryReplaceMapField(ref text, fieldName, "specialSlots", SlotsLiteral(file.specialEncounters, 6))) failed.Add("specialSlots");
            if (!TryReplaceMapField(ref text, fieldName, "normalTreeCount", file.normalTreeGroups.Count.ToString())) failed.Add("normalTreeCount");
            if (!TryReplaceMapField(ref text, fieldName, "specialTreeCount", file.specialTreeGroups.Count.ToString())) failed.Add("specialTreeCount");

            // Groups are never added/removed here, so normalTreeGroups.Count + specialTreeGroups.Count
            // always equals the treeCoords[N][6] the source already declares.
            int totalGroups = file.normalTreeGroups.Count + file.specialTreeGroups.Count;
            if (totalGroups > 0)
            {
                string GroupLiteral(HeadbuttTreeGroup g)
                {
                    var coords = new List<string>(6);
                    foreach (var t in g.trees)
                    {
                        int x = t.IsUnused ? -1 : t.globalX, y = t.IsUnused ? -1 : t.globalY;
                        coords.Add($"{{ {x}, {y} }}");
                    }
                    return "{ " + string.Join(", ", coords) + " }";
                }
                var groups = new List<string>(totalGroups);
                foreach (var g in file.normalTreeGroups) groups.Add(GroupLiteral(g));
                foreach (var g in file.specialTreeGroups) groups.Add(GroupLiteral(g));
                string treeCoordsLiteral = "{\n            " + string.Join(",\n            ", groups) + ",\n        }";
                if (!TryReplaceMapField(ref text, fieldName, "treeCoords", treeCoordsLiteral)) failed.Add("treeCoords");
            }

            File.WriteAllText(path, text);
            if (failed.Count > 0)
            { error = $"Some fields could not be located and were left unchanged: {string.Join(", ", failed)}"; return false; }
            return true;
        }

        private static void ReadSlots(string mapBlock, string fieldName, HgEngineSymbolTable species, List<HeadbuttEncounter> dest, int fixedCount)
        {
            if (HgEngineSourcePatcher.TryGetFieldValueInBlock(mapBlock, new[] { FieldPathSegment.Field(fieldName) }, out string raw))
            {
                foreach (var el in HgEngineSourcePatcher.SplitArrayValue(raw))
                {
                    var parts = HgEngineSourcePatcher.SplitArrayValue(el.Trim());
                    if (parts.Count < 3) continue;
                    dest.Add(new HeadbuttEncounter
                    {
                        pokemonID = (ushort)ResolveToken(parts[0], species),
                        minLevel = (byte)ResolveToken(parts[1], null),
                        maxLevel = (byte)ResolveToken(parts[2], null),
                    });
                }
            }
            while (dest.Count < fixedCount) dest.Add(new HeadbuttEncounter());
        }

        private static void ReadGroups(string mapBlock, int normalCount, int specialCount,
            System.ComponentModel.BindingList<HeadbuttTreeGroup> normalDest, System.ComponentModel.BindingList<HeadbuttTreeGroup> specialDest)
        {
            if (!HgEngineSourcePatcher.TryGetFieldValueInBlock(mapBlock, new[] { FieldPathSegment.Field("treeCoords") }, out string raw)) return;
            var groups = HgEngineSourcePatcher.SplitArrayValue(raw);
            for (int i = 0; i < groups.Count; i++)
            {
                var group = new HeadbuttTreeGroup();
                var coords = HgEngineSourcePatcher.SplitArrayValue(groups[i].Trim());
                for (int j = 0; j < coords.Count && j < group.trees.Count; j++)
                {
                    var xy = HgEngineSourcePatcher.SplitArrayValue(coords[j].Trim());
                    if (xy.Count < 2) continue;
                    group.trees[j].globalX = unchecked((ushort)ResolveToken(xy[0], null));
                    group.trees[j].globalY = unchecked((ushort)ResolveToken(xy[1], null));
                }
                if (i < normalCount) normalDest.Add(group);
                else specialDest.Add(group);
            }
        }

        private static bool TryReplaceMapField(ref string text, string mapFieldName, string subFieldName, string newLiteral)
        {
            if (!TryFindDataBlock(text, out int open, out int close)) return false;
            var path = new[] { FieldPathSegment.Field(mapFieldName), FieldPathSegment.Field(subFieldName) };
            if (!ElementScanner.TryLocateValueSpan(text, open, close, path, out int vs, out int ve)) return false;
            text = text.Substring(0, vs) + newLiteral + text.Substring(ve);
            return true;
        }

        private static bool TryFindDataBlock(string text, out int open, out int close)
        {
            open = close = -1;
            var m = DataAnchor.Match(text);
            if (!m.Success) return false;
            open = m.Index + m.Length - 1;
            return BraceScanner.TryFindMatchingBrace(text, open, out close);
        }

        private static int ResolveToken(string token, HgEngineSymbolTable table)
        {
            token = token.Trim();
            if (int.TryParse(token, out int v)) return v;
            return table != null && table.TryGetValue(token, out int tv) ? tv : 0;
        }

        private static string TryReadSource(out string path)
        {
            path = Path.Combine(HgEngineProject.RepoPathUnc, SourceRelPath.Replace('/', '\\'));
            return File.Exists(path) ? HgEngineFileCache.GetText(path) : null;
        }
    }
}
