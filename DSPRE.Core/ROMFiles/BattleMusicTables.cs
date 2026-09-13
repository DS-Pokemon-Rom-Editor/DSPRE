using System;
using System.Collections.Generic;
using DSPRE.HgEngine;
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
        /// <summary>HGSS only. Diamond, Pearl and Platinum decide these in code.</summary>
        public readonly Table<(int Class, int Combo)> Classes = new Table<(int, int)>();
        public readonly Table<(int Species, int Combo)> Species = new Table<(int, int)>();

        /// <summary>True when the rows came from hg-engine's source rather than the ROM.</summary>
        public bool FromHgEngineSource { get; }

        internal BattleMusicTables(bool fromHgEngineSource = false) => FromHgEngineSource = fromHgEngineSource;

        // Only the English Diamond and Pearl layout is known.
        public static bool IsSupported => gameFamily == GameFamilies.HGSS || gameFamily == GameFamilies.Plat
            || (gameFamily == GameFamilies.DP && gameLanguage == GameLanguages.English);

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

        // Diamond and Pearl pick rows in code like Platinum, from their own lists; the table holds 31 rows.
        private const int DpComboCount = 31, DpNormalTrainer = 29, DpNormalWild = 30;
        private static readonly Dictionary<int, int> DpClassCombo = new Dictionary<int, int>
        {
            [62] = 0, [74] = 1, [75] = 2, [76] = 3, [77] = 4, [78] = 5, [64] = 6, [79] = 7,   // gym leaders
            [65] = 8, [66] = 9, [67] = 10, [68] = 11,                                         // Elite Four
            [69] = 12, [63] = 13,                                                             // Cynthia, rival
            [73] = 21, [89] = 21, [72] = 22, [87] = 22, [88] = 22, [86] = 23,                 // Team Galactic
            [97] = 28,                                                                        // Tower Tycoon
        };
        private static readonly Dictionary<int, int> DpSpeciesCombo = new Dictionary<int, int>
        {
            [492] = 14, [483] = 15, [484] = 15, [480] = 16, [482] = 16, [481] = 17, [493] = 18,
            [479] = 19, [485] = 19, [486] = 19, [487] = 19, [491] = 19, [488] = 20,
        };

        /// <summary>The theme for a battle against this trainer class, or -1.</summary>
        public int TrainerSequence(int trainerClass, bool kanto = false)
        {
            if (gameFamily == GameFamilies.DP)
                return ComboSequence(DpClassCombo.TryGetValue(trainerClass, out int d) ? d : DpNormalTrainer);
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
            if (gameFamily == GameFamilies.DP)
                return ComboSequence(DpSpeciesCombo.TryGetValue(species, out int d) ? d : DpNormalWild);
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

        /// <summary>Reads hg-engine's source when it builds the tables, else the ROM.</summary>
        public static BattleMusicTables Load()
        {
            if (!IsSupported) return null;
            if (gameFamily == GameFamilies.HGSS && HgEngineMusicTables.TablesInSource) return HgEngineMusicTables.ReadBattle();
            return LoadRom();
        }

        /// <summary>Reads the ROM's tables. Null on an hg-engine ROM whose tables live in its own code.</summary>
        public static BattleMusicTables LoadRom()
        {
            if (!IsSupported) return null;
            SetBattleEffectsData();
            if (isHGE && BitConverter.ToUInt32(ARM9.ReadBytes(effectsComboTableOffsetToRAMAddress, 4), 0) >= synthOverlayLoadAddress) return null;
            var t = new BattleMusicTables();

            bool hgss = gameFamily == GameFamilies.HGSS;
            if (gameFamily == GameFamilies.DP && !DpPointersAgree()) return null;
            int comboCount = hgss ? ARM9.ReadByte(effectsComboTableOffsetToSizeLimiter)
                : gameFamily == GameFamilies.DP ? DpComboCount : PtNormalWild + 1;
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

        // A second pointer sits two bytes into the same table; if they disagree, the layout is not the known one.
        private static bool DpPointersAgree() =>
            BitConverter.ToUInt32(ARM9.ReadBytes(effectsComboTableSecondPointerOffset, 4), 0)
            == BitConverter.ToUInt32(ARM9.ReadBytes(effectsComboTableOffsetToRAMAddress, 4), 0) + 2;

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
