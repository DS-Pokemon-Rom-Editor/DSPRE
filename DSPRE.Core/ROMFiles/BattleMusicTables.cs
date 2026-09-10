using System;
using System.Collections.Generic;
using static DSPRE.RomInfo;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// A battle's transition and music combos, and in HGSS the classes and species that pick one.
    /// </summary>
    public sealed class BattleMusicTables
    {
        public sealed class Table<T>
        {
            public readonly List<T> Rows = new List<T>();
            /// <summary>File offset of the first row, in <see cref="Path"/>.</summary>
            public uint Start;
            public bool Repointed;
            public string Path;
        }

        public readonly Table<(ushort Transition, ushort Sequence)> Combos = new Table<(ushort, ushort)>();
        /// <summary>HGSS only. Platinum decides these in code.</summary>
        public readonly Table<(int Class, int Combo)> Classes = new Table<(int, int)>();
        public readonly Table<(int Species, int Combo)> Species = new Table<(int, int)>();

        public static bool IsSupported => gameFamily == GameFamilies.HGSS || gameFamily == GameFamilies.Plat;

        // ── Selection ────────────────────────────────────────────────────────────────────────────

        // HGSS falls back to these combos, and swaps the standard themes for their Kanto versions in Kanto.
        private const int HgssTrainerCombo = 41, HgssWildCombo = 42;
        private const ushort HgssWild = 1116, HgssTrainer = 1117, HgssWildKanto = 1125, HgssTrainerKanto = 1126;

        // Platinum's combo indices, in the order of sEncEffectsTable in the decomp's enc_effects.c.
        private const int PtNormalTrainer = 33, PtNormalWild = 34;
        private static readonly Dictionary<int, int> PtClassCombo = new Dictionary<int, int>
        {
            [62] = 0, [74] = 1, [75] = 2, [76] = 3, [77] = 4, [78] = 5, [64] = 6, [79] = 7,   // gym leaders
            [65] = 8, [66] = 9, [67] = 10, [68] = 11,                                         // Elite Four
            [69] = 12, [63] = 13,                                                             // Cynthia, rival
            [73] = 24, [89] = 24, [72] = 25, [87] = 25, [88] = 25, [86] = 26,                 // Team Galactic
            [97] = 31, [99] = 31, [100] = 31, [101] = 31, [102] = 31,                         // Frontier Brains
        };
        private static readonly Dictionary<int, int> PtSpeciesCombo = new Dictionary<int, int>
        {
            [492] = 14, [483] = 15, [484] = 15, [480] = 16, [482] = 16, [481] = 17, [493] = 18,
            [486] = 19, [485] = 19, [491] = 19, [479] = 19, [488] = 20,
            [144] = 21, [145] = 21, [146] = 21, [487] = 22, [377] = 23, [378] = 23, [379] = 23,
        };

        /// <summary>The theme for a battle against this trainer class, or -1.</summary>
        public int TrainerSequence(int trainerClass, bool kanto = false)
        {
            if (gameFamily == GameFamilies.Plat)
                return ComboSequence(PtClassCombo.TryGetValue(trainerClass, out int c) ? c : PtNormalTrainer);

            int combo = HgssTrainerCombo;
            foreach (var row in Classes.Rows)
                if (row.Class == trainerClass) { combo = row.Combo; break; }
            return Kanto(ComboSequence(combo), kanto);
        }

        /// <summary>The theme for a wild battle with this species, or -1.</summary>
        public int WildSequence(int species, bool kanto = false)
        {
            if (gameFamily == GameFamilies.Plat)
                return ComboSequence(PtSpeciesCombo.TryGetValue(species, out int c) ? c : PtNormalWild);

            int combo = HgssWildCombo;
            foreach (var row in Species.Rows)
                if (row.Species == species) { combo = row.Combo; break; }
            // Only a combo before the standard wild one overrides it.
            if (combo >= HgssWildCombo) combo = HgssWildCombo;
            return Kanto(ComboSequence(combo), kanto);
        }

        private int ComboSequence(int combo) =>
            combo >= 0 && combo < Combos.Rows.Count ? Combos.Rows[combo].Sequence : -1;

        private static int Kanto(int seq, bool kanto) =>
            !kanto || seq < 0 ? seq : seq == HgssWild ? HgssWildKanto : seq == HgssTrainer ? HgssTrainerKanto : seq;

        // ── Reading ──────────────────────────────────────────────────────────────────────────────

        /// <summary>Reads the loaded ROM's tables. Null for other games.</summary>
        public static BattleMusicTables Load() => LoadRom();

        /// <summary>Reads the ROM's tables.</summary>
        public static BattleMusicTables LoadRom()
        {
            if (!IsSupported) return null;
            SetBattleEffectsData();
            var t = new BattleMusicTables();

            bool hgss = gameFamily == GameFamilies.HGSS;
            int comboCount = hgss ? ARM9.ReadByte(effectsComboTableOffsetToSizeLimiter) : PtNormalWild + 1;
            Locate(t.Combos, effectsComboTableOffsetToRAMAddress);
            using (var r = new DSUtils.EasyReader(t.Combos.Path, t.Combos.Start))
                for (int i = 0; i < comboCount; i++) t.Combos.Rows.Add((r.ReadUInt16(), r.ReadUInt16()));

            if (hgss)
            {
                ReadPacked(t.Classes, vsTrainerEntryTableOffsetToRAMAddress, ARM9.ReadByte(vsTrainerEntryTableOffsetToSizeLimiter));
                ReadPacked(t.Species, vsPokemonEntryTableOffsetToRAMAddress, ARM9.ReadByte(vsPokemonEntryTableOffsetToSizeLimiter));
            }
            return t;
        }

        // A row is an id in the low 10 bits and a combo in the high 6.
        private static void ReadPacked(Table<(int, int)> table, uint pointerOffset, int count)
        {
            Locate(table, pointerOffset);
            using var r = new DSUtils.EasyReader(table.Path, table.Start);
            for (int i = 0; i < count; i++)
            {
                ushort v = r.ReadUInt16();
                table.Rows.Add((v & 0x3FF, v >> 10));
            }
        }

        private static void Locate<T>(Table<T> table, uint pointerOffset)
        {
            uint ram = BitConverter.ToUInt32(ARM9.ReadBytes(pointerOffset, 4), 0);
            table.Repointed = ram >= synthOverlayLoadAddress;
            table.Start = ram - (table.Repointed ? synthOverlayLoadAddress : ARM9.address);
            table.Path = table.Repointed ? Filesystem.expArmPath : arm9Path;
        }
    }
}
