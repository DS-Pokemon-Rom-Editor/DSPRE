using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DSPRE.ROMFiles;
using static DSPRE.MoveData;

namespace DSPRE.HgEngine
{
    /// <summary>Every "[NAME] = {" entry of a data file with its brace span, found in one scan. The first
    /// occurrence of a name wins, as <see cref="HgEngineSourcePatcher.TryFindEntry"/> finds it.</summary>
    internal static class HgEngineEntryIndex
    {
        private static readonly Regex EntryStart = new(@"\[\s*([^\[\]\r\n]+?)\s*\]\s*=\s*\{", RegexOptions.Compiled);

        /// <summary>An unmatched brace is kept as (-1, -1) so a later spelling can't stand in for it.</summary>
        internal static Dictionary<string, (int Open, int Close)> Build(string text)
        {
            var spans = new Dictionary<string, (int Open, int Close)>(StringComparer.Ordinal);
            foreach (Match m in EntryStart.Matches(text))
            {
                string name = m.Groups[1].Value;
                if (spans.ContainsKey(name)) continue;
                int open = m.Index + m.Length - 1;
                spans[name] = BraceScanner.TryFindMatchingBrace(text, open, out int close) ? (open, close) : (-1, -1);
            }
            return spans;
        }

        internal static bool TryGetBlock(string text, IReadOnlyDictionary<string, (int Open, int Close)> index, string designator, out HgEngineSourceBlock block)
        {
            block = default;
            if (!index.TryGetValue(designator, out var span) || span.Open < 0) return false;
            block = new HgEngineSourceBlock(text.Substring(span.Open, span.Close - span.Open + 1));
            return true;
        }

        /// <summary>Applies a field map to one isolated "{ ... }" entry, keeping unchanged spellings. The patched
        /// block comes back even when some fields are missing; the result says whether all were placed.</summary>
        internal static bool TryPatch<T>(string block, IReadOnlyList<HgEngineSourceField<T>> fields, T model, string[] headers,
            Func<HgEngineValueSpelling.Write, Func<string, int?>> lookupFor, out string patched, out List<string> unresolved)
        {
            const string key = "[X] = ";
            string text = key + block;
            unresolved = new List<string>();
            var writes = HgEngineValueSpelling.Preserve(new HgEngineSourceBlock(block), HgEngineSourceFields.Writes(fields, model, headers), lookupFor);
            foreach (var write in writes)
            {
                if (write.ValueLiteral == null || !HgEngineSourcePatcher.TryReplaceField(ref text, "X", write.Path, write.ValueLiteral))
                    unresolved.Add(string.Concat(write.Path.Select(p => p.ToString())));
            }
            patched = text.Substring(key.Length);
            return unresolved.Count == 0;
        }
    }

    /// <summary>The Move Data editor's Moves.c fields.</summary>
    public static class HgEngineMoveSource
    {
        private const string MoveDataHeader = "include/move_data.h";
        private const string EffectsHeader = "include/constants/move_effects.h";
        private const string TypesHeader = "include/constants/pokemon.h";
        private const string RangesHeader = "include/constants/battle_constants.h";

        public static readonly string[] Headers = { EffectsHeader, MoveDataHeader, TypesHeader, RangesHeader };

        private static HgEngineSourceField<MoveData> F(string block, string field, System.Func<MoveData, int> get, System.Action<MoveData, int> set,
            int min, int max, System.Func<int, string> format = null, bool isFlags = false) => new()
        {
            Path = HgEngineSourceFields.PathOf(block, field),
            Get = get, Set = set, Min = min, Max = max, Format = format, IsFlags = isFlags,
        };

        // .battle.flags is an OR of FLAG_* names, some of which compile to zero depending on config.h.
        public static readonly IReadOnlyList<HgEngineSourceField<MoveData>> Fields = new[]
        {
            F("data", "effect", m => m.battleeffect, (m, v) => m.battleeffect = (ushort)v, 0, ushort.MaxValue, v => HgEngineSourceFields.Symbol(EffectsHeader, "MOVE_EFFECT_", v)),
            F("data", "split", m => (int)m.split, (m, v) => m.split = (MoveSplit)v, 0, byte.MaxValue, v => HgEngineSourceFields.Symbol(MoveDataHeader, "SPLIT_", v)),
            F("data", "power", m => m.damage, (m, v) => m.damage = (byte)v, 0, byte.MaxValue),
            F("data", "type", m => (int)m.movetype, (m, v) => m.movetype = (PokemonType)v, 0, byte.MaxValue, v => HgEngineSourceFields.Symbol(TypesHeader, "TYPE_", v)),
            F("data", "accuracy", m => m.accuracy, (m, v) => m.accuracy = (byte)v, 0, byte.MaxValue),
            F("data", "pp", m => m.pp, (m, v) => m.pp = (byte)v, 0, byte.MaxValue),
            F("data", "effectChance", m => m.sideEffectProbability, (m, v) => m.sideEffectProbability = (byte)v, 0, byte.MaxValue),
            F("battle", "target", m => m.target, (m, v) => m.target = (ushort)v, 0, ushort.MaxValue, v => HgEngineSourceFields.Symbol(RangesHeader, "RANGE_", v)),
            F("battle", "priority", m => m.priority, (m, v) => m.priority = (sbyte)v, sbyte.MinValue, sbyte.MaxValue),
            F("battle", "flags", m => m.flagField, (m, v) => m.flagField = (byte)v, 0, byte.MaxValue, v => HgEngineSourceFields.FlagsSymbol(MoveDataHeader, "FLAG_", v), isFlags: true),
            F("contest", "appeal", m => m.contestAppeal, (m, v) => m.contestAppeal = (byte)v, 0, byte.MaxValue, v => HgEngineSourceFields.Symbol(MoveDataHeader, "APPEAL_", v)),
            F("contest", "contestType", m => (int)m.contestConditionType, (m, v) => m.contestConditionType = (ContestCondition)v, 0, byte.MaxValue, v => HgEngineSourceFields.Symbol(MoveDataHeader, "CONTEST_", v)),
        };

        /// <summary>Fills <paramref name="move"/> from the move's Moves.c entry.</summary>
        public static bool TryLoad(int id, MoveData move, out string error) =>
            HgEngineEntrySource.TryLoad(HgEngineDomain.Moves, id, out var entry, out error)
            && HgEngineSourceFields.TryRead(entry, Fields, move, HgEngineSourceFields.NameLookup(Headers), out error);

        public static bool TryWrite(int id, MoveData move, out string error)
        {
            var fields = HgEngineValueSpelling.Preserve(HgEngineDomain.Moves, id, HgEngineSourceFields.Writes(Fields, move, Headers));
            if (HgEngineWriter.TryWriteFields(HgEngineDomain.Moves, id, fields, out var unresolved, out error, allOrNothing: true))
                return true;
            if (error == null && unresolved.Count > 0)
                error = $"Moves.c has no {string.Join(", ", unresolved)} for this move, so nothing was written.";
            error ??= "Moves.c couldn't be written.";
            return false;
        }

        private static bool TryGetSourcePath(out string path, out string error)
        {
            path = null;
            error = null;
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout linked."; return false; }
            var info = HgEngineDomains.All.First(d => d.Domain == HgEngineDomain.Moves);
            path = Path.Combine(HgEngineProject.RepoPathUnc, info.SourceFileRelPath.Replace('/', '\\'));
            if (File.Exists(path)) return true;
            error = $"Source file not found: {path}";
            return false;
        }

        /// <summary>Writes several moves to Moves.c in one pass. Nothing is written unless every move's fields
        /// can be placed.</summary>
        public static bool TryWriteMany(IReadOnlyDictionary<int, MoveData> moves, out string error)
        {
            if (!TryGetSourcePath(out string path, out error)) return false;
            var targets = new List<(string Designator, MoveData Move)>();
            foreach (var (id, move) in moves.OrderBy(kv => kv.Key))
            {
                if (!HgEngineDesignators.TryResolve(HgEngineDomain.Moves, id, out string designator))
                { error = $"No source name was found for move {id}, so nothing was written."; return false; }
                targets.Add((designator, move));
            }

            string text = HgEngineFileCache.GetText(path);
            string updated = text;
            if (!TryApply(ref updated, targets, w => HgEngineSourceFields.NameLookup(w.Headers), out error)) return false;
            if (updated == text) return true;
            try { HgEngineFileCache.WriteText(path, updated); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                error = $"{Path.GetFileName(path)} couldn't be written: {ex.Message}";
                return false;
            }
            return true;
        }

        /// <summary>Pure half of <see cref="TryWriteMany"/>. One scan finds every entry and the file is rebuilt
        /// once, so a large import doesn't rescan Moves.c per field.</summary>
        internal static bool TryApply(ref string text, IReadOnlyList<(string Designator, MoveData Move)> moves,
            Func<HgEngineValueSpelling.Write, Func<string, int?>> lookupFor, out string error)
        {
            error = null;
            var index = HgEngineEntryIndex.Build(text);
            var patched = new List<(int Open, int Close, string Block)>();
            foreach (var (designator, move) in moves)
            {
                if (!index.TryGetValue(designator, out var span) || span.Open < 0)
                { error = $"{designator} is not in Moves.c, so nothing was written."; return false; }
                if (!HgEngineEntryIndex.TryPatch(text.Substring(span.Open, span.Close - span.Open + 1), Fields, move, Headers, lookupFor, out string block, out var unresolved))
                { error = $"Moves.c has no {string.Join(", ", unresolved)} for {designator}, so nothing was written."; return false; }
                patched.Add((span.Open, span.Close, block));
            }

            patched.Sort((a, b) => a.Open.CompareTo(b.Open));
            var sb = new StringBuilder(text.Length);
            int at = 0;
            foreach (var (open, close, block) in patched)
            {
                if (open < at) { error = "Two moves name the same Moves.c entry, so nothing was written."; return false; }
                sb.Append(text, at, open - at).Append(block);
                at = close + 1;
            }
            sb.Append(text, at, text.Length - at);
            text = sb.ToString();
            return true;
        }

        /// <summary>Reads many moves from one scan of Moves.c. <paramref name="baseRecord"/> fills fields an entry
        /// lacks; a move whose entry is missing or unreadable is left out and named in <paramref name="skipped"/>.</summary>
        public static bool TryLoadMany(IEnumerable<int> ids, Func<int, MoveData> baseRecord,
            out SortedDictionary<int, MoveData> moves, out List<string> skipped, out string error)
        {
            moves = new SortedDictionary<int, MoveData>();
            skipped = new List<string>();
            if (!TryGetSourcePath(out string path, out error)) return false;

            string text = HgEngineFileCache.GetText(path);
            var index = HgEngineEntryIndex.Build(text);
            var lookup = HgEngineSourceFields.NameLookup(Headers);
            foreach (int id in ids)
            {
                if (!HgEngineDesignators.TryResolve(HgEngineDomain.Moves, id, out string designator))
                {
                    skipped.Add($"Move {id}: no source name");
                    continue;
                }
                var move = baseRecord(id);
                if (TryReadEntry(text, index, designator, move, lookup, out string readError)) moves[id] = move;
                else skipped.Add($"Move {id}: {readError}");
            }
            return true;
        }

        internal static bool TryReadEntry(string text, IReadOnlyDictionary<string, (int Open, int Close)> index, string designator,
            MoveData move, Func<string, int?> lookup, out string error)
        {
            if (HgEngineEntryIndex.TryGetBlock(text, index, designator, out var entry))
                return HgEngineSourceFields.TryRead(entry, Fields, move, lookup, out error);
            error = $"{designator} is not in Moves.c.";
            return false;
        }
    }

    /// <summary>The Item editor's itemdata.c fields. ItemData's party-use bit order matches ItemPartyUseParam
    /// in include/item.h field for field. Price has its own spellings; see HgEngineItemExpansion.</summary>
    public static class HgEngineItemSource
    {
        private const string ItemHeader = "include/constants/item.h";
        private const string HoldEffectsHeader = "include/constants/hold_item_effects.h";
        private const string TypesHeader = "include/constants/pokemon.h";

        public static readonly string[] Headers = { ItemHeader, HoldEffectsHeader, TypesHeader };

        private static string Bool(int value) => value != 0 ? "TRUE" : "FALSE";

        private static HgEngineSourceField<ItemData> F(object[] path, System.Func<ItemData, int> get, System.Action<ItemData, int> set,
            int min, int max, System.Func<int, string> format = null) => new()
        {
            Path = HgEngineSourceFields.PathOf(path), Get = get, Set = set, Min = min, Max = max, Format = format,
        };

        private static HgEngineSourceField<ItemData> Top(string field, System.Func<ItemData, int> get, System.Action<ItemData, int> set,
            int min, int max, System.Func<int, string> format = null) => F(new object[] { field }, get, set, min, max, format);

        private static HgEngineSourceField<ItemData> TopBool(string field, System.Func<ItemData, bool> get, System.Action<ItemData, bool> set) =>
            F(new object[] { field }, d => get(d) ? 1 : 0, (d, v) => set(d, v != 0), 0, 1, Bool);

        private static HgEngineSourceField<ItemData> Party(string field, System.Func<ItemData.ItemPartyUseParam, int> get, System.Action<ItemData.ItemPartyUseParam, int> set,
            int min, int max) => F(new object[] { "partyUseParam", field }, d => get(d.PartyUseParam), (d, v) => set(d.PartyUseParam, v), min, max);

        private static HgEngineSourceField<ItemData> PartyBool(string field, System.Func<ItemData.ItemPartyUseParam, bool> get, System.Action<ItemData.ItemPartyUseParam, bool> set) =>
            F(new object[] { "partyUseParam", field }, d => get(d.PartyUseParam) ? 1 : 0, (d, v) => set(d.PartyUseParam, v != 0), 0, 1, Bool);

        // Ranges are the widths include/item.h declares, so a value the build would truncate is refused.
        public static readonly IReadOnlyList<HgEngineSourceField<ItemData>> Fields = new[]
        {
            Top("holdEffect", d => (int)d.holdEffect, (d, v) => d.holdEffect = (HoldEffect)v, 0, byte.MaxValue),
            Top("holdEffectParam", d => d.HoldEffectParam, (d, v) => d.HoldEffectParam = (byte)v, 0, byte.MaxValue),
            Top("pluckEffect", d => d.PluckEffect, (d, v) => d.PluckEffect = (byte)v, 0, byte.MaxValue),
            Top("flingEffect", d => d.FlingEffect, (d, v) => d.FlingEffect = (byte)v, 0, byte.MaxValue),
            Top("flingPower", d => d.FlingPower, (d, v) => d.FlingPower = (byte)v, 0, byte.MaxValue),
            Top("naturalGiftPower", d => d.NaturalGiftPower, (d, v) => d.NaturalGiftPower = (byte)v, 0, byte.MaxValue),
            Top("naturalGiftType", d => (int)d.naturalGiftType, (d, v) => d.naturalGiftType = (NaturalGiftType)v, 0, 31, v => HgEngineSourceFields.Symbol(TypesHeader, "TYPE_", v)),
            TopBool("prevent_toss", d => d.PreventToss, (d, v) => d.PreventToss = v),
            TopBool("selectable", d => d.Selectable, (d, v) => d.Selectable = v),
            // item.h packs ITEM_*/POCKET_*/BATTLE_POCKET_* into one namespace, so names are looked up by prefix.
            Top("fieldPocket", d => (int)d.fieldPocket, (d, v) => d.fieldPocket = (FieldPocket)v, 0, 15, v => HgEngineSourceFields.Symbol(ItemHeader, "POCKET_", v)),
            Top("battlePocket", d => (int)d.battlePocket, (d, v) => d.battlePocket = (BattlePocket)v, 0, 31, v => HgEngineSourceFields.FlagsSymbol(ItemHeader, "BATTLE_POCKET_", v)),
            Top("fieldUseFunc", d => (int)d.fieldUseFunc, (d, v) => d.fieldUseFunc = (FieldUseFunc)v, 0, byte.MaxValue),
            Top("battleUseFunc", d => (int)d.battleUseFunc, (d, v) => d.battleUseFunc = (BattleUseFunc)v, 0, byte.MaxValue),
            Top("partyUse", d => d.PartyUse, (d, v) => d.PartyUse = (byte)v, 0, byte.MaxValue),

            PartyBool("slp_heal", p => p.SlpHeal, (p, v) => p.SlpHeal = v),
            PartyBool("psn_heal", p => p.PsnHeal, (p, v) => p.PsnHeal = v),
            PartyBool("brn_heal", p => p.BrnHeal, (p, v) => p.BrnHeal = v),
            PartyBool("frz_heal", p => p.FrzHeal, (p, v) => p.FrzHeal = v),
            PartyBool("prz_heal", p => p.PrzHeal, (p, v) => p.PrzHeal = v),
            PartyBool("cfs_heal", p => p.CfsHeal, (p, v) => p.CfsHeal = v),
            PartyBool("inf_heal", p => p.InfHeal, (p, v) => p.InfHeal = v),
            PartyBool("guard_spec", p => p.GuardSpec, (p, v) => p.GuardSpec = v),
            PartyBool("revive", p => p.Revive, (p, v) => p.Revive = v),
            PartyBool("revive_all", p => p.ReviveAll, (p, v) => p.ReviveAll = v),
            PartyBool("level_up", p => p.LevelUp, (p, v) => p.LevelUp = v),
            PartyBool("evolve", p => p.Evolve, (p, v) => p.Evolve = v),
            Party("atk_stages", p => p.AtkStages, (p, v) => p.AtkStages = v, 0, 15),
            Party("def_stages", p => p.DefStages, (p, v) => p.DefStages = v, 0, 15),
            Party("spatk_stages", p => p.SpAtkStages, (p, v) => p.SpAtkStages = v, 0, 15),
            Party("spdef_stages", p => p.SpDefStages, (p, v) => p.SpDefStages = v, 0, 15),
            Party("speed_stages", p => p.SpeedStages, (p, v) => p.SpeedStages = v, 0, 15),
            Party("accuracy_stages", p => p.AccuracyStages, (p, v) => p.AccuracyStages = v, 0, 15),
            Party("critrate_stages", p => p.CritRateStages, (p, v) => p.CritRateStages = v, 0, 3),
            PartyBool("pp_up", p => p.PPUps, (p, v) => p.PPUps = v),
            PartyBool("pp_max", p => p.PPMax, (p, v) => p.PPMax = v),
            PartyBool("pp_restore", p => p.PPRestore, (p, v) => p.PPRestore = v),
            PartyBool("pp_restore_all", p => p.PPRestoreAll, (p, v) => p.PPRestoreAll = v),
            PartyBool("hp_restore", p => p.HPRestore, (p, v) => p.HPRestore = v),
            PartyBool("hp_ev_up", p => p.EVHp, (p, v) => p.EVHp = v),
            PartyBool("atk_ev_up", p => p.EVAtk, (p, v) => p.EVAtk = v),
            PartyBool("def_ev_up", p => p.EVDef, (p, v) => p.EVDef = v),
            PartyBool("speed_ev_up", p => p.EVSpeed, (p, v) => p.EVSpeed = v),
            PartyBool("spatk_ev_up", p => p.EVSpAtk, (p, v) => p.EVSpAtk = v),
            PartyBool("spdef_ev_up", p => p.EVSpDef, (p, v) => p.EVSpDef = v),
            PartyBool("friendship_mod_lo", p => p.FriendshipLow, (p, v) => p.FriendshipLow = v),
            PartyBool("friendship_mod_med", p => p.FriendshipMid, (p, v) => p.FriendshipMid = v),
            PartyBool("friendship_mod_hi", p => p.FriendshipHigh, (p, v) => p.FriendshipHigh = v),
            Party("hp_ev_up_param", p => p.EVHpValue, (p, v) => p.EVHpValue = (sbyte)v, sbyte.MinValue, sbyte.MaxValue),
            Party("atk_ev_up_param", p => p.EVAtkValue, (p, v) => p.EVAtkValue = (sbyte)v, sbyte.MinValue, sbyte.MaxValue),
            Party("def_ev_up_param", p => p.EVDefValue, (p, v) => p.EVDefValue = (sbyte)v, sbyte.MinValue, sbyte.MaxValue),
            Party("speed_ev_up_param", p => p.EVSpeedValue, (p, v) => p.EVSpeedValue = (sbyte)v, sbyte.MinValue, sbyte.MaxValue),
            Party("spatk_ev_up_param", p => p.EVSpAtkValue, (p, v) => p.EVSpAtkValue = (sbyte)v, sbyte.MinValue, sbyte.MaxValue),
            Party("spdef_ev_up_param", p => p.EVSpDefValue, (p, v) => p.EVSpDefValue = (sbyte)v, sbyte.MinValue, sbyte.MaxValue),
            Party("hp_restore_param", p => p.HPRestoreParam, (p, v) => p.HPRestoreParam = (byte)v, 0, byte.MaxValue),
            Party("pp_restore_param", p => p.PPRestoreParam, (p, v) => p.PPRestoreParam = (byte)v, 0, byte.MaxValue),
            Party("friendship_mod_lo_param", p => p.FriendshipLowValue, (p, v) => p.FriendshipLowValue = (sbyte)v, sbyte.MinValue, sbyte.MaxValue),
            Party("friendship_mod_med_param", p => p.FriendshipMidValue, (p, v) => p.FriendshipMidValue = (sbyte)v, sbyte.MinValue, sbyte.MaxValue),
            Party("friendship_mod_hi_param", p => p.FriendshipHighValue, (p, v) => p.FriendshipHighValue = (sbyte)v, sbyte.MinValue, sbyte.MaxValue),
        };

        /// <summary>Fills <paramref name="item"/> from the item's itemdata.c entry, the full 20-bit price included.</summary>
        public static bool TryLoad(int id, ItemData item, out string error)
        {
            if (!HgEngineEntrySource.TryLoad(HgEngineDomain.Items, id, out var entry, out error)) return false;
            return TryRead(entry, item, HgEngineSourceFields.NameLookup(Headers), out error);
        }

        internal static bool TryRead(HgEngineSourceBlock entry, ItemData item, System.Func<string, int?> lookup, out string error)
        {
            bool priced = HgEngineItemExpansion.TryGetPrice(entry, lookup, out int price, out string rawPrice, out bool outOfRange);
            if (!priced && rawPrice != null)
            {
                error = outOfRange ? $"price = {rawPrice} is out of range." : $"price = {rawPrice} could not be read.";
                return false;
            }
            if (!HgEngineSourceFields.TryRead(entry, Fields, item, lookup, out error)) return false;
            if (priced) item.FullPrice = price;
            return true;
        }

        public static bool TryWrite(int id, ItemData item, out string error) => HgEngineItemExpansion.TryWriteItem(id, item, out error);
    }

    /// <summary>The HGSS Wild editor's Encounters.c fields. Water, rod and rock smash slots are undesignated
    /// "{ min, max, SPECIES_X }" structs, located by position.</summary>
    public static class HgEngineEncounterSource
    {
        private const string SpeciesHeader = "include/constants/species.h";
        public static readonly string[] Headers = { SpeciesHeader };

        private static string Species(int id) => HgEngineSourceFields.Symbol(SpeciesHeader, "SPECIES_", id);

        private static HgEngineSourceField<EncounterFileHGSS> Byte(System.Func<EncounterFileHGSS, byte[]> array, int i, params object[] path) => new()
        {
            Path = HgEngineSourceFields.PathOf(path), Get = e => array(e)[i], Set = (e, v) => array(e)[i] = (byte)v, Min = 0, Max = byte.MaxValue,
        };

        private static HgEngineSourceField<EncounterFileHGSS> Mon(System.Func<EncounterFileHGSS, ushort[]> array, int i, params object[] path) => new()
        {
            Path = HgEngineSourceFields.PathOf(path), Get = e => array(e)[i], Set = (e, v) => array(e)[i] = (ushort)v, Min = 0, Max = ushort.MaxValue, Format = Species,
        };

        private static HgEngineSourceField<EncounterFileHGSS> Rate(string field, System.Func<EncounterFileHGSS, byte> get, System.Action<EncounterFileHGSS, byte> set) => new()
        {
            Path = HgEngineSourceFields.PathOf(field), Get = e => get(e), Set = (e, v) => set(e, (byte)v), Min = 0, Max = byte.MaxValue,
        };

        public static readonly IReadOnlyList<HgEngineSourceField<EncounterFileHGSS>> Fields = Build();

        private static List<HgEngineSourceField<EncounterFileHGSS>> Build()
        {
            var fields = new List<HgEngineSourceField<EncounterFileHGSS>>
            {
                Rate("rateWalk", e => e.walkingRate, (e, v) => e.walkingRate = v),
                Rate("rateSurf", e => e.surfRate, (e, v) => e.surfRate = v),
                Rate("rateRockSmash", e => e.rockSmashRate, (e, v) => e.rockSmashRate = v),
                Rate("rateOldRod", e => e.oldRodRate, (e, v) => e.oldRodRate = v),
                Rate("rateGoodRod", e => e.goodRodRate, (e, v) => e.goodRodRate = v),
                Rate("rateSuperRod", e => e.superRodRate, (e, v) => e.superRodRate = v),
                Mon(e => e.swarmPokemon, 0, "landSwarm"),
                Mon(e => e.swarmPokemon, 1, "surfSwarm"),
                Mon(e => e.swarmPokemon, 2, "nightFish"),
                Mon(e => e.swarmPokemon, 3, "fishSwarm"),
            };
            for (int i = 0; i < 12; i++)
            {
                fields.Add(Byte(e => e.walkingLevels, i, "landSlots", "levels", i));
                fields.Add(Mon(e => e.morningPokemon, i, "landSlots", "speciesMorning", i));
                fields.Add(Mon(e => e.dayPokemon, i, "landSlots", "speciesDay", i));
                fields.Add(Mon(e => e.nightPokemon, i, "landSlots", "speciesNight", i));
            }
            for (int i = 0; i < 2; i++)
            {
                fields.Add(Mon(e => e.hoennMusicPokemon, i, "hoennSoundSpecies", i));
                fields.Add(Mon(e => e.sinnohMusicPokemon, i, "sinnohSoundSpecies", i));
                AddSlot("rockSmashSlots", i, e => e.rockSmashMinLevels, e => e.rockSmashMaxLevels, e => e.rockSmashPokemon);
            }
            for (int i = 0; i < 5; i++)
            {
                AddSlot("surfSlots", i, e => e.surfMinLevels, e => e.surfMaxLevels, e => e.surfPokemon);
                AddSlot("oldRodSlots", i, e => e.oldRodMinLevels, e => e.oldRodMaxLevels, e => e.oldRodPokemon);
                AddSlot("goodRodSlots", i, e => e.goodRodMinLevels, e => e.goodRodMaxLevels, e => e.goodRodPokemon);
                AddSlot("superRodSlots", i, e => e.superRodMinLevels, e => e.superRodMaxLevels, e => e.superRodPokemon);
            }
            return fields;

            void AddSlot(string array, int i, System.Func<EncounterFileHGSS, byte[]> min, System.Func<EncounterFileHGSS, byte[]> max, System.Func<EncounterFileHGSS, ushort[]> species)
            {
                fields.Add(Byte(min, i, array, i, 0));
                fields.Add(Byte(max, i, array, i, 1));
                fields.Add(Mon(species, i, array, i, 2));
            }
        }

        public static bool TryLoad(int id, EncounterFileHGSS encounters, out string error) =>
            HgEngineEntrySource.TryLoad(HgEngineDomain.Encounters, id, out var entry, out error)
            && HgEngineSourceFields.TryRead(entry, Fields, encounters, HgEngineSourceFields.NameLookup(Headers), out error);

        public static bool TryWrite(int id, EncounterFileHGSS encounters, out string error)
        {
            var fields = HgEngineValueSpelling.Preserve(HgEngineDomain.Encounters, id, HgEngineSourceFields.Writes(Fields, encounters, Headers));
            if (HgEngineWriter.TryWriteFields(HgEngineDomain.Encounters, id, fields, out var unresolved, out error, allOrNothing: true))
                return true;
            if (error == null && unresolved.Count > 0)
                error = $"Encounters.c has no {string.Join(", ", unresolved)} for this table, so nothing was written.";
            error ??= "Encounters.c couldn't be written.";
            return false;
        }
    }
}
