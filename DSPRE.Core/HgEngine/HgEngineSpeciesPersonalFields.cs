using System;
using System.Collections.Generic;
using System.Linq;
using DSPRE.ROMFiles;

namespace DSPRE.HgEngine
{
    /// <summary>The data/Species.c fields a personal data record holds. One map drives both reading an
    /// entry into a record and writing a record back, so the two can't disagree.</summary>
    public static class HgEngineSpeciesPersonalFields
    {
        private const string PokemonH = "include/constants/pokemon.h";
        private const string BattleH = "include/constants/battle_constants.h";
        private const string ItemH = "include/constants/item.h";
        private const string AbilityH = "include/constants/ability.h";

        private static FieldPathSegment[] P(string field, string sub = null, int index = -1)
        {
            var path = new List<FieldPathSegment> { FieldPathSegment.Field("speciesData"), FieldPathSegment.Field(field) };
            if (sub != null) path.Add(FieldPathSegment.Field(sub));
            if (index >= 0) path.Add(FieldPathSegment.At(index));
            return path.ToArray();
        }

        private static HgEngineSourceField<PokemonPersonalData> F(FieldPathSegment[] path, int max, Func<PokemonPersonalData, int> get, Action<PokemonPersonalData, int> set,
            string prefix = null, string[] headers = null, string zeroName = null) => new()
        {
            Path = path, Min = 0, Max = max, Get = get, Set = set,
            Prefix = prefix, Headers = headers ?? Array.Empty<string>(), ZeroName = zeroName,
        };

        private static readonly string[] TypeHeaders = { PokemonH, BattleH };
        private static readonly string[] PokemonHeaders = { PokemonH };
        private static readonly string[] ItemHeaders = { ItemH };
        private static readonly string[] AbilityHeaders = { AbilityH };

        // Order within a block follows Species.c, so an absent block is written in the file's own layout.
        public static IReadOnlyList<HgEngineSourceField<PokemonPersonalData>> All { get; } = new[]
        {
            F(P("baseStats", "hp"), 255, d => d.baseHP, (d, v) => d.baseHP = (byte)v),
            F(P("baseStats", "attack"), 255, d => d.baseAtk, (d, v) => d.baseAtk = (byte)v),
            F(P("baseStats", "defense"), 255, d => d.baseDef, (d, v) => d.baseDef = (byte)v),
            F(P("baseStats", "spAttack"), 255, d => d.baseSpAtk, (d, v) => d.baseSpAtk = (byte)v),
            F(P("baseStats", "spDefense"), 255, d => d.baseSpDef, (d, v) => d.baseSpDef = (byte)v),
            F(P("baseStats", "speed"), 255, d => d.baseSpeed, (d, v) => d.baseSpeed = (byte)v),
            F(P("types", index: 0), 255, d => (int)d.type1, (d, v) => d.type1 = (PokemonType)v, "TYPE_", TypeHeaders),
            F(P("types", index: 1), 255, d => (int)d.type2, (d, v) => d.type2 = (PokemonType)v, "TYPE_", TypeHeaders),
            F(P("catchRate"), 255, d => d.catchRate, (d, v) => d.catchRate = (byte)v),
            // The built record packs each yield into 2 bits.
            F(P("evYields", "hp"), 3, d => d.evHP, (d, v) => d.evHP = (byte)v),
            F(P("evYields", "attack"), 3, d => d.evAtk, (d, v) => d.evAtk = (byte)v),
            F(P("evYields", "defense"), 3, d => d.evDef, (d, v) => d.evDef = (byte)v),
            F(P("evYields", "spAttack"), 3, d => d.evSpAtk, (d, v) => d.evSpAtk = (byte)v),
            F(P("evYields", "spDefense"), 3, d => d.evSpDef, (d, v) => d.evSpDef = (byte)v),
            F(P("evYields", "speed"), 3, d => d.evSpeed, (d, v) => d.evSpeed = (byte)v),
            F(P("wildHeldItems", "common"), ushort.MaxValue, d => d.item1, (d, v) => d.item1 = (ushort)v, "ITEM_", ItemHeaders, "ITEM_NONE"),
            F(P("wildHeldItems", "rare"), ushort.MaxValue, d => d.item2, (d, v) => d.item2 = (ushort)v, "ITEM_", ItemHeaders, "ITEM_NONE"),
            F(P("genderRatio"), 255, d => d.genderVec, (d, v) => d.genderVec = (byte)v),
            F(P("hatchCycles"), 255, d => d.eggSteps, (d, v) => d.eggSteps = (byte)v),
            F(P("baseFriendship"), 255, d => d.baseFriendship, (d, v) => d.baseFriendship = (byte)v),
            F(P("expRate"), 255, d => (int)d.growthCurve, (d, v) => d.growthCurve = (PokemonGrowthCurve)v, "GROWTH_", PokemonHeaders),
            F(P("eggGroups", index: 0), 255, d => d.eggGroup1, (d, v) => d.eggGroup1 = (byte)v, "EGG_GROUP_", PokemonHeaders),
            F(P("eggGroups", index: 1), 255, d => d.eggGroup2, (d, v) => d.eggGroup2 = (byte)v, "EGG_GROUP_", PokemonHeaders),
            // hg-engine widens abilities to u16.
            F(P("abilities", index: 0), ushort.MaxValue, d => d.firstAbility, (d, v) => d.firstAbility = (ushort)v, "ABILITY_", AbilityHeaders, "ABILITY_NONE"),
            F(P("abilities", index: 1), ushort.MaxValue, d => d.secondAbility, (d, v) => d.secondAbility = (ushort)v, "ABILITY_", AbilityHeaders, "ABILITY_NONE"),
            F(P("safariFleeRate"), 255, d => d.escapeRate, (d, v) => d.escapeRate = (byte)v),
            // Shares a byte with the flip bit.
            F(P("bodyColor"), 127, d => (int)d.color, (d, v) => d.color = (PokemonDexColor)v, "BODY_COLOR_", PokemonHeaders),
            F(P("flipSprite"), 1, d => d.flip ? 1 : 0, (d, v) => d.flip = v != 0),
        };

        /// <summary>Loads the species' Species.c entry into <paramref name="data"/>, keeping the values of
        /// fields the entry doesn't declare.</summary>
        public static bool TryLoadInto(int speciesId, PokemonPersonalData data, out string error)
        {
            if (!HgEngineEntrySource.TryLoad(HgEngineDomain.Species, speciesId, out var entry, out error)) return false;
            return TryRead(entry, data, HgEngineSymbolTable.Load, out error);
        }

        /// <summary>Writes every mapped field of <paramref name="data"/> to the species' Species.c entry,
        /// all or nothing.</summary>
        public static bool TryWriteSource(int speciesId, PokemonPersonalData data, out string error)
        {
            if (!HgEngineEntrySource.TryLoad(HgEngineDomain.Species, speciesId, out var entry, out error)) return false;
            var writes = CollapseAbsentParents(BuildWrites(data, entry, HgEngineSymbolTable.Load), p => entry.TryGetRaw(p, out _));
            // Rewriting an unchanged entry would still touch the file and make the next build redo it.
            if (writes.All(w => entry.TryGetRaw(w.Path, out string raw) && raw == w.ValueLiteral)) return true;
            return HgEngineWriter.TryWriteFields(HgEngineDomain.Species, speciesId, writes, out _, out error, allowInsert: true, allOrNothing: true);
        }

        private static readonly System.Text.RegularExpressions.Regex SpeciesEntry = new(@"\[\s*(SPECIES_\w+)\s*\]\s*=\s*\{");

        /// <summary>Every species' two abilities from one pass over Species.c, by species id. An ability the
        /// entry doesn't declare is -1.</summary>
        public static bool TryLoadAllAbilities(out Dictionary<int, (int first, int second)> abilities, out string error)
        {
            abilities = new Dictionary<int, (int, int)>();
            error = null;
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout is linked."; return false; }

            var info = HgEngineDomains.All.FirstOrDefault(d => d.Domain == HgEngineDomain.Species);
            var species = HgEngineSymbolTable.Load("include/constants/species.h");
            if (info == null || species == null) { error = "include/constants/species.h could not be read."; return false; }
            string path = System.IO.Path.Combine(HgEngineProject.RepoPathUnc, info.SourceFileRelPath.Replace('/', '\\'));
            if (!System.IO.File.Exists(path)) { error = $"Source file not found: {path}"; return false; }

            var abilityFields = All.Where(f => f.Path[1].Name == "abilities").ToArray();
            var lookups = abilityFields.Select(f => HgEngineSourceFields.NameLookup(f.Headers, HgEngineSymbolTable.Load)).ToArray();
            string text = HgEngineFileCache.GetText(path);
            int lastClose = -1;
            foreach (System.Text.RegularExpressions.Match m in SpeciesEntry.Matches(text))
            {
                int open = m.Index + m.Length - 1;
                if (open < lastClose || !BraceScanner.TryFindMatchingBrace(text, open, out int close)) continue;
                lastClose = close;
                if (!species.TryGetValue(m.Groups[1].Value, out int id)) continue;

                var entry = new HgEngineSourceBlock(text.Substring(open, close - open + 1));
                var values = new int[abilityFields.Length];
                for (int i = 0; i < abilityFields.Length; i++)
                {
                    if (!entry.TryGetRaw(abilityFields[i].Path, out string raw)) { values[i] = -1; continue; }
                    if (!HgEngineSourceExpression.TryEvaluate(raw, lookups[i], out values[i]))
                    { error = $"{m.Groups[1].Value} {abilityFields[i].Name} = {raw} could not be read."; return false; }
                }
                abilities[id] = (values[0], values[1]);
            }
            return true;
        }

        /// <summary>Copies each field the entry declares into <paramref name="data"/>. A declared field that
        /// can't be resolved or doesn't fit fails the read and leaves <paramref name="data"/> untouched.</summary>
        public static bool TryRead(HgEngineSourceBlock entry, PokemonPersonalData data, Func<string, HgEngineSymbolTable> loadTable, out string error)
            => HgEngineSourceFields.TryRead(entry, All, data, loadTable, out error);

        /// <summary>One write per mapped field. A field whose current spelling in <paramref name="existing"/>
        /// already means the same value keeps that spelling.</summary>
        public static List<HgEngineFieldWrite> BuildWrites(PokemonPersonalData data, HgEngineSourceBlock? existing, Func<string, HgEngineSymbolTable> loadTable)
            => HgEngineValueSpelling.Preserve(existing, HgEngineSourceFields.Writes(All, data, loadTable), w => HgEngineSourceFields.NameLookup(w.Headers, loadTable));

        /// <summary>Replaces the writes under each absent parent block with one write of the whole block
        /// literal at the parent, laid out like Species.c. Blocks that exist are left to per-field writes.</summary>
        public static List<HgEngineFieldWrite> CollapseAbsentParents(IReadOnlyList<HgEngineFieldWrite> fields, Func<IReadOnlyList<FieldPathSegment>, bool> exists)
        {
            static string Key(IEnumerable<FieldPathSegment> path) => string.Concat(path.Select(p => p.ToString()));
            var result = new List<HgEngineFieldWrite>();
            var parentExists = new Dictionary<string, bool>();
            foreach (var field in fields)
            {
                if (field.Path.Count < 3 || field.ValueLiteral == null) { result.Add(field); continue; }
                var parentPath = field.Path.Take(field.Path.Count - 1).ToArray();
                string key = Key(parentPath);
                if (parentExists.TryGetValue(key, out bool known))
                {
                    if (known) result.Add(field);
                    continue;   // already folded into its block
                }
                parentExists[key] = exists(parentPath);
                if (parentExists[key]) { result.Add(field); continue; }

                var group = fields.Where(f => f.Path.Count == field.Path.Count && f.ValueLiteral != null && Key(f.Path.Take(f.Path.Count - 1)) == key).ToList();
                string literal = field.Path[^1].IsIndex
                    ? "{ " + string.Join(", ", group.OrderBy(f => f.Path[^1].Index).Select(f => f.ValueLiteral)) + " }"
                    : "{\n" + string.Concat(group.Select(f => $"                .{f.Path[^1].Name} = {f.ValueLiteral},\n")) + "            }";
                result.Add(new HgEngineFieldWrite(parentPath, literal));
            }
            return result;
        }
    }
}
