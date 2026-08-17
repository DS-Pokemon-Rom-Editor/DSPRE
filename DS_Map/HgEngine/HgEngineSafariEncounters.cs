using System.Collections.Generic;
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
                        pokemonID = (ushort)ResolveToken(parts[0], species),
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
        {
            error = null;
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout linked."; return false; }
            var areas = HgEngineSymbolTable.Load(HeaderRelPath);
            if (areas == null || !areas.TryGetNameWithPrefix(areaId, AreaPrefix, out string areaDesignator))
            { error = $"Could not resolve a safari area designator for id {areaId}."; return false; }

            var header = HgEngineSymbolTable.Load(HeaderRelPath);
            if (header == null || !header.TryGetValue("NUM_ENCOUNTERS_SAFARI", out int mainCount)) mainCount = 10;
            if (header == null || !header.TryGetValue(BonusCountDefineFor(type), out int bonusCount)) bonusCount = 0;

            var species = HgEngineSymbolTable.Load(SpeciesHeaderRelPath);
            string typeField = FieldNameFor(type);

            string text = TryReadSource(out string path);
            if (text == null) { error = $"Source file not found: {path}"; return false; }

            string SlotLiteral(BindingList<SafariZoneEncounter> list, int count)
            {
                var items = new List<string>(count);
                for (int i = 0; i < count; i++)
                {
                    var e = i < list.Count ? list[i] : new SafariZoneEncounter();
                    string sp = species != null && species.TryGetNameWithPrefix(e.pokemonID, "SPECIES_", out string sn) ? sn : e.pokemonID.ToString();
                    items.Add($"{{ {sp}, {e.level} }}");
                }
                return "{ " + string.Join(", ", items) + " }";
            }

            string CondLiteral(BindingList<SafariZoneObjectRequirement> req, BindingList<SafariZoneObjectRequirement> opt, int count)
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
                return "{\n            " + string.Join(",\n            ", items) + ",\n        }";
            }

            var writes = new (string Field, string Literal)[]
            {
                ("speciesMorning", SlotLiteral(group.MorningEncounters, mainCount)),
                ("speciesDay", SlotLiteral(group.DayEncounters, mainCount)),
                ("speciesNight", SlotLiteral(group.NightEncounters, mainCount)),
                ("bonusSpeciesMorning", SlotLiteral(group.MorningEncountersObject, bonusCount)),
                ("bonusSpeciesDay", SlotLiteral(group.DayEncountersObject, bonusCount)),
                ("bonusSpeciesNight", SlotLiteral(group.NightEncountersObject, bonusCount)),
                ("bonusUnlockConditions", CondLiteral(group.ObjectRequirements, group.OptionalObjectRequirements, bonusCount)),
            };

            var failedFields = new List<string>();
            foreach (var (field, literal) in writes)
            {
                var fieldPath = new[] { FieldPathSegment.Field(typeField), FieldPathSegment.Field(field) };
                if (!HgEngineSourcePatcher.TryReplaceField(ref text, areaDesignator, fieldPath, literal))
                    failedFields.Add(field);
            }

            File.WriteAllText(path, text);
            if (failedFields.Count > 0)
            { error = $"Some fields could not be located and were left unchanged: {string.Join(", ", failedFields)}"; return false; }
            return true;
        }

        private static SafariZoneObjectRequirement ParseRequirement(string block, HgEngineSymbolTable objectTypes)
        {
            var parts = HgEngineSourcePatcher.SplitArrayValue(block.Trim());
            if (parts.Count < 2) return new SafariZoneObjectRequirement();
            return new SafariZoneObjectRequirement((byte)ResolveToken(parts[0], objectTypes), (byte)ResolveToken(parts[1], null));
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
            path = Path.Combine(HgEngineProject.RepoPathUnc, SourceRelPath.Replace('/', '\\'));
            return File.Exists(path) ? HgEngineFileCache.GetText(path) : null;
        }
    }
}
