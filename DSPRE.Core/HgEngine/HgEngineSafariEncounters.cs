using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using System.IO;
using DSPRE.ROMFiles;

namespace DSPRE.HgEngine
{
    /// <summary>Source-text read/write for data/SafariEncounters.c's <c>[SAFARI_ZONE_AREA_X] = { .land =
    /// {...}, .surf = {...}, .oldRod = {...}, .goodRod = {...}, .superRod = {...} }</c> array. Reuses the
    /// vanilla <see cref="SafariZoneEncounterGroup"/>/<see cref="SafariZoneEncounter"/>/
    /// <see cref="SafariZoneObjectRequirement"/> POCOs as the in-memory shape, so the existing
    /// SafariZoneGroupViewModel UI works unchanged; only the load/save call sites differ. The bonus-slot
    /// count is per rod type and read dynamically from <c>include/safari_encounter.h</c>'s
    /// <c>NUM_SAFARI_*_BONUS_ENCOUNTERS</c>/<c>NUM_ENCOUNTERS_SAFARI</c>, never assumed.</summary>
    public static class HgEngineSafariEncounters
    {
        private const string SourceRelPath = "data/SafariEncounters.c";
        private const string HeaderRelPath = "include/safari_encounter.h";
        private const string SpeciesHeaderRelPath = "include/constants/species.h";
        private const string AreaPrefix = "SAFARI_ZONE_AREA_";
        private const string ObjectTypePrefix = "SAFARI_ZONE_OBJECT_TYPE_";

        public enum RodType { Land, Surf, OldRod, GoodRod, SuperRod }

        /// <summary>The real per-type bonus-slot count this checkout declares, so callers (e.g. gating
        /// Add/Remove Object Slot in the UI) never guess at a fixed number either.</summary>
        public static int GetBonusSlotCount(RodType type)
        {
            var header = HgEngineSymbolTable.Load(HeaderRelPath);
            return header != null && header.TryGetValue(BonusCountDefineFor(type), out int n) ? n : 0;
        }

        public static bool TryLoadGroup(int areaId, RodType type, out SafariZoneEncounterGroup group, out string error)
        {
            group = null; error = null;
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout linked."; return false; }
            var areas = HgEngineSymbolTable.Load(HeaderRelPath);
            if (areas == null || !areas.TryGetNameWithPrefix(areaId, AreaPrefix, out string areaDesignator))
            { error = $"Could not resolve a safari area designator for id {areaId}."; return false; }

            string text = TryReadSource(out string path);
            if (text == null) { error = $"Source file not found: {path}"; return false; }

            var species = HgEngineSymbolTable.Load(SpeciesHeaderRelPath);
            string typeField = FieldNameFor(type);
            group = new SafariZoneEncounterGroup();
            string unreadable = null;

            void ReadSlotArray(string fieldName, BindingList<SafariZoneEncounter> dest)
            {
                var fieldPath = new[] { FieldPathSegment.Field(typeField), FieldPathSegment.Field(fieldName) };
                if (!HgEngineSourcePatcher.TryGetFieldValue(text, areaDesignator, fieldPath, out string raw)) return;
                foreach (var el in HgEngineSourcePatcher.SplitArrayValue(raw))
                {
                    var parts = HgEngineSourcePatcher.SplitArrayValue(el.Trim());
                    if (parts.Count < 2) continue;
                    dest.Add(new SafariZoneEncounter
                    {
                        pokemonID = (ushort)ResolveSpecies(parts[0], species, ref unreadable),
                        level = (byte)ResolveToken(parts[1], null),
                    });
                }
            }

            ReadSlotArray("speciesMorning", group.MorningEncounters);
            ReadSlotArray("speciesDay", group.DayEncounters);
            ReadSlotArray("speciesNight", group.NightEncounters);
            ReadSlotArray("bonusSpeciesMorning", group.MorningEncountersObject);
            ReadSlotArray("bonusSpeciesDay", group.DayEncountersObject);
            ReadSlotArray("bonusSpeciesNight", group.NightEncountersObject);
            // Saving would write SPECIES_NONE over it.
            if (unreadable != null) { error = $"SafariEncounters.c has a species DSPRE can't read: {unreadable}"; group = null; return false; }

            var condPath = new[] { FieldPathSegment.Field(typeField), FieldPathSegment.Field("bonusUnlockConditions") };
            if (HgEngineSourcePatcher.TryGetFieldValue(text, areaDesignator, condPath, out string rawConds))
            {
                var objectTypes = HgEngineSymbolTable.Load(HeaderRelPath);
                foreach (var el in HgEngineSourcePatcher.SplitArrayValue(rawConds))
                {
                    SafariZoneObjectRequirement req = new(), opt = new();
                    if (HgEngineSourcePatcher.TryGetFieldValueInBlock(el.Trim(), new[] { FieldPathSegment.Field("objects") }, out string rawObjects))
                    {
                        var objs = HgEngineSourcePatcher.SplitArrayValue(rawObjects);
                        if (objs.Count > 0) req = ParseRequirement(objs[0], objectTypes);
                        if (objs.Count > 1) opt = ParseRequirement(objs[1], objectTypes);
                    }
                    group.ObjectRequirements.Add(req);
                    group.OptionalObjectRequirements.Add(opt);
                }
            }
            group.ObjectSlots = (byte)group.ObjectRequirements.Count;
            return true;
        }

        public static bool TrySaveGroup(int areaId, RodType type, SafariZoneEncounterGroup group, out string error)
            => TrySaveGroups(areaId, new[] { (type, group) }, out error);

        /// <summary>Writes every given group of one area in a single pass: nothing is written unless every
        /// field of every group was placed.</summary>
        public static bool TrySaveGroups(int areaId, IEnumerable<(RodType Type, SafariZoneEncounterGroup Group)> groups, out string error)
        {
            error = null;
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout linked."; return false; }
            var areas = HgEngineSymbolTable.Load(HeaderRelPath);
            if (areas == null || !areas.TryGetNameWithPrefix(areaId, AreaPrefix, out string areaDesignator))
            { error = $"Could not resolve a safari area designator for id {areaId}."; return false; }

            string text = TryReadSource(out string path);
            if (text == null) { error = $"Source file not found: {path}"; return false; }

            var failed = new List<string>();
            foreach (var (type, group) in groups)
            {
                if (group == null) continue;
                foreach (string field in ApplyGroup(ref text, areaDesignator, type, group))
                    failed.Add($"{FieldNameFor(type)}.{field}");
            }
            if (failed.Count > 0)
            { error = $"{areaDesignator} has no {string.Join(", ", failed)}, so nothing was written."; return false; }

            try { HgEngineFileCache.WriteText(path, text); }
            catch (System.Exception ex) when (ex is IOException || ex is System.UnauthorizedAccessException)
            { error = $"SafariEncounters.c couldn't be written: {ex.Message}"; return false; }
            return true;
        }

        /// <summary>Patches one rod type's fields into <paramref name="text"/> and returns the ones it couldn't place.</summary>
        private static List<string> ApplyGroup(ref string text, string areaDesignator, RodType type, SafariZoneEncounterGroup group)
        {
            var header = HgEngineSymbolTable.Load(HeaderRelPath);
            if (header == null || !header.TryGetValue("NUM_ENCOUNTERS_SAFARI", out int mainCount)) mainCount = 10;
            if (header == null || !header.TryGetValue(BonusCountDefineFor(type), out int bonusCount)) bonusCount = 0;

            var species = HgEngineSymbolTable.Load(SpeciesHeaderRelPath);
            string typeField = FieldNameFor(type);

            List<string> SlotItems(BindingList<SafariZoneEncounter> list, int count)
            {
                var items = new List<string>(count);
                for (int i = 0; i < count; i++)
                {
                    var e = i < list.Count ? list[i] : new SafariZoneEncounter();
                    string sp = HgEngineTrainerSource.FormatPackedSpecies(e.pokemonID, SpeciesHeaderRelPath);
                    items.Add($"{{ {sp}, {e.level} }}");
                }
                return items;
            }

            List<string> CondItems(BindingList<SafariZoneObjectRequirement> req, BindingList<SafariZoneObjectRequirement> opt, int count)
            {
                var items = new List<string>(count);
                for (int i = 0; i < count; i++)
                {
                    var r = i < req.Count ? req[i] : new SafariZoneObjectRequirement();
                    var o = i < opt.Count ? opt[i] : new SafariZoneObjectRequirement();
                    string rt = header != null && header.TryGetNameWithPrefix(r.typeID, ObjectTypePrefix, out string rn) ? rn : r.typeID.ToString();
                    string ot = header != null && header.TryGetNameWithPrefix(o.typeID, ObjectTypePrefix, out string on) ? on : o.typeID.ToString();
                    items.Add($"{{ .objects = {{ {{ {rt}, {r.quantity} }}, {{ {ot}, {o.quantity} }} }} }}");
                }
                return items;
            }

            var writes = new (string Field, List<string> Items)[]
            {
                ("speciesMorning", SlotItems(group.MorningEncounters, mainCount)),
                ("speciesDay", SlotItems(group.DayEncounters, mainCount)),
                ("speciesNight", SlotItems(group.NightEncounters, mainCount)),
                ("bonusSpeciesMorning", SlotItems(group.MorningEncountersObject, bonusCount)),
                ("bonusSpeciesDay", SlotItems(group.DayEncountersObject, bonusCount)),
                ("bonusSpeciesNight", SlotItems(group.NightEncountersObject, bonusCount)),
                ("bonusUnlockConditions", CondItems(group.ObjectRequirements, group.OptionalObjectRequirements, bonusCount)),
            };

            var failedFields = new List<string>();
            foreach (var (field, items) in writes)
            {
                var fieldPath = new[] { FieldPathSegment.Field(typeField), FieldPathSegment.Field(field) };
                HgEngineSourcePatcher.TryGetFieldValue(text, areaDesignator, fieldPath, out string original);
                if (!HgEngineSourcePatcher.TryReplaceField(ref text, areaDesignator, fieldPath, ListLiteral(original, items)))
                    failedFields.Add(field);
            }
            return failedFields;
        }

        /// <summary>
        /// A brace list in the layout the file already gives it: one item per line with the same indent and trailing
        /// comma when it was written that way, otherwise on one line, so an edit only changes the items that changed.
        /// </summary>
        internal static string ListLiteral(string original, IReadOnlyList<string> items)
        {
            string body = original?.Replace("\r\n", "\n");
            if (body == null || !body.Contains('\n') || items.Count == 0) return "{ " + string.Join(", ", items) + " }";

            string[] lines = body.Split('\n');
            string itemLine = lines.Skip(1).FirstOrDefault(l => l.Trim().Length > 0 && l.Trim() != "}") ?? "";
            string itemIndent = itemLine.Substring(0, itemLine.Length - itemLine.TrimStart().Length);
            string last = lines[^1];
            string closeIndent = last.Substring(0, last.Length - last.TrimStart().Length);
            bool trailingComma = lines.Take(lines.Length - 1).LastOrDefault(l => l.Trim().Length > 0)?.TrimEnd().EndsWith(",") == true;

            var sb = new System.Text.StringBuilder("{");
            for (int i = 0; i < items.Count; i++)
                sb.Append('\n').Append(itemIndent).Append(items[i]).Append(i < items.Count - 1 || trailingComma ? "," : "");
            return sb.Append('\n').Append(closeIndent).Append('}').ToString();
        }

        private static SafariZoneObjectRequirement ParseRequirement(string block, HgEngineSymbolTable objectTypes)
        {
            var parts = HgEngineSourcePatcher.SplitArrayValue(block.Trim());
            if (parts.Count < 2) return new SafariZoneObjectRequirement();
            return new SafariZoneObjectRequirement((byte)ResolveToken(parts[0], objectTypes), (byte)ResolveToken(parts[1], null));
        }

        // A slot can name a form: MON_WITH_FORM(SPECIES_X, n) or SPECIES_X | (n << 11).
        private static int ResolveSpecies(string token, HgEngineSymbolTable species, ref string unreadable)
        {
            if (HgEngineEvolutions.ResolveTarget(token.Trim(), species, out int id, out int form)) return id | (form << HgEngineTrainerSource.FormShift);
            unreadable ??= token.Trim();
            return 0;
        }

        private static int ResolveToken(string token, HgEngineSymbolTable table)
        {
            token = token.Trim();
            if (int.TryParse(token, out int v)) return v;
            return table != null && table.TryGetValue(token, out int tv) ? tv : 0;
        }

        private static string FieldNameFor(RodType t) => t switch
        {
            RodType.Land => "land",
            RodType.Surf => "surf",
            RodType.OldRod => "oldRod",
            RodType.GoodRod => "goodRod",
            RodType.SuperRod => "superRod",
            _ => "land",
        };

        private static string BonusCountDefineFor(RodType t) => t switch
        {
            RodType.Land => "NUM_SAFARI_LAND_BONUS_ENCOUNTERS",
            RodType.Surf => "NUM_SAFARI_SURF_BONUS_ENCOUNTERS",
            RodType.OldRod => "NUM_SAFARI_OLD_ROD_BONUS_ENCOUNTERS",
            RodType.GoodRod => "NUM_SAFARI_GOOD_ROD_BONUS_ENCOUNTERS",
            RodType.SuperRod => "NUM_SAFARI_SUPER_ROD_BONUS_ENCOUNTERS",
            _ => "NUM_SAFARI_LAND_BONUS_ENCOUNTERS",
        };

        private static string TryReadSource(out string path)
        {
            path = Path.Combine(HgEngineProject.RepoPathUnc, SourceRelPath.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(path) ? HgEngineFileCache.GetText(path) : null;
        }
    }
}
