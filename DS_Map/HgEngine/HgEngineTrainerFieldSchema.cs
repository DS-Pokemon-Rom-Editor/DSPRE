using System;
using System.Collections.Generic;
using System.Linq;

namespace DSPRE.HgEngine
{
    /// <summary>Resolves the real flag/enum names the linked checkout declares in
    /// include/trainer_data.h, so the Trainer Editor never shows hardcoded vanilla labels.</summary>
    public static class HgEngineTrainerFieldSchema
    {
        private const string HeaderRelPath = "include/trainer_data.h";

        public readonly struct NamedFlag
        {
            public string Name { get; }
            public int Bit { get; }
            public NamedFlag(string name, int bit) { Name = name; Bit = bit; }
        }

        /// <summary>The real F_* AI-flag bits (e.g. F_PRIORITIZE_SUPER_EFFECTIVE, F_DOUBLE_BATTLE, ...),
        /// in ascending bit order.</summary>
        public static IReadOnlyList<NamedFlag> GetAiFlags() => GetSingleBitFlags("F_");

        /// <summary>The real TRAINER_DATA_TYPE_* bits (MOVES/ITEMS/ABILITY/BALL/IV_EV_SET/NATURE_SET/
        /// SHINY_LOCK/ADDITIONAL_FLAGS), in ascending bit order.</summary>
        public static IReadOnlyList<NamedFlag> GetTrainerDataTypeFlags() => GetSingleBitFlags("TRAINER_DATA_TYPE_");

        /// <summary>The real TRAINER_DATA_EXTRA_TYPE_* bits (STATUS/HP/ATK/DEF/SPEED/SP_ATK/SP_DEF/
        /// PP_COUNTS/NICKNAME), in ascending bit order.</summary>
        public static IReadOnlyList<NamedFlag> GetExtraFlags() => GetSingleBitFlags("TRAINER_DATA_EXTRA_TYPE_");

        private static readonly string[] BattleTypeNames = { "SINGLE_BATTLE", "DOUBLE_BATTLE", "NO_PARTNER_DOUBLE_BATTLE" };

        /// <summary>The real named battleType values, resolved by exact name (not a common prefix).
        /// Falls back to known-good defaults only if the header couldn't be read at all.</summary>
        public static IReadOnlyList<NamedFlag> GetBattleTypes()
        {
            var table = HgEngineSymbolTable.Load(HeaderRelPath);
            if (table == null) return DefaultBattleTypes;

            var result = new List<NamedFlag>();
            foreach (var name in BattleTypeNames)
                if (table.TryGetValue(name, out int value)) result.Add(new NamedFlag(name, value));
            return result.Count > 0 ? result : DefaultBattleTypes;
        }

        private static readonly string[] AbilitySlotNames = { "TRAINER_POKEMON_ABILITY_1", "TRAINER_POKEMON_ABILITY_HIDDEN", "TRAINER_POKEMON_ABILITY_2" };
        private static readonly IReadOnlyList<NamedFlag> DefaultAbilitySlots = new[]
        {
            new NamedFlag("TRAINER_POKEMON_ABILITY_1", 0x00),
            new NamedFlag("TRAINER_POKEMON_ABILITY_HIDDEN", 0x02),
            new NamedFlag("TRAINER_POKEMON_ABILITY_2", 0x20),
        };

        /// <summary>The real named abilitySlot values, always present regardless of trainerType flags
        /// (distinct from the optional `.ability` field gated by TRAINER_DATA_TYPE_ABILITY).</summary>
        public static IReadOnlyList<NamedFlag> GetAbilitySlots()
        {
            var table = HgEngineSymbolTable.Load(HeaderRelPath);
            if (table == null) return DefaultAbilitySlots;

            var result = new List<NamedFlag>();
            foreach (var name in AbilitySlotNames)
                if (table.TryGetValue(name, out int value)) result.Add(new NamedFlag(name, value));
            return result.Count > 0 ? result : DefaultAbilitySlots;
        }

        private static readonly IReadOnlyList<NamedFlag> DefaultBattleTypes = new[]
        {
            new NamedFlag("SINGLE_BATTLE", 0),
            new NamedFlag("DOUBLE_BATTLE", 2),
            new NamedFlag("NO_PARTNER_DOUBLE_BATTLE", 3),
        };

        // Skips 0-valued markers and multi-bit combo aliases, which would double-count bits if toggled directly.
        private static IReadOnlyList<NamedFlag> GetSingleBitFlags(string prefix)
        {
            var table = HgEngineSymbolTable.Load(HeaderRelPath);
            if (table == null) return Array.Empty<NamedFlag>();

            var result = new List<NamedFlag>();
            foreach (var kv in table.ByName)
            {
                if (!kv.Key.StartsWith(prefix, StringComparison.Ordinal)) continue;
                int v = kv.Value;
                if (v == 0 || (v & (v - 1)) != 0) continue;   // skip 0 and non-single-bit values
                result.Add(new NamedFlag(kv.Key, v));
            }
            return result.OrderBy(f => f.Bit).ToList();
        }
    }
}
