using System;
using System.Collections.Generic;
using System.IO;
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

        /// <summary>
        /// Diamond, Pearl and Platinum: the class switch, read from its jump table. Null where the table is
        /// not known or does not look as expected, and then the built-in lists are used.
        /// </summary>
        public ClassJumpTable ClassJumps { get; private set; }

        /// <summary>
        /// False when the music reader's own pointer does not sit two bytes into the combo table, so the
        /// music actually played is not the one read here. True where that pointer is not known.
        /// </summary>
        public bool MusicPointerAgrees { get; private set; } = true;

        /// <summary>
        /// DP/Pt's class switch: <c>subs r0,#first; movs r1,#default; cmp r0,#count-1; bhi end</c>, a signed
        /// halfword table, then one <c>movs r1,#combo; b end</c> stub per result. An entry can point at any stub.
        /// </summary>
        public sealed class ClassJumpTable
        {
            public int FirstClass, DefaultCombo;
            /// <summary>arm9 file offset of the first entry.</summary>
            public int Start;
            public int Count => Entries.Length;
            /// <summary>Raw entries: each jumps to <see cref="Start"/> + 2 + entry.</summary>
            public short[] Entries = Array.Empty<short>();
            /// <summary>Every place an entry can jump to, with the combo it picks.</summary>
            public readonly Dictionary<int, int> ComboAt = new Dictionary<int, int>();

            public int TargetOf(short entry) => Start + 2 + entry;
            public short EntryFor(int target) => (short)(target - (Start + 2));

            /// <summary>The combo entry <paramref name="index"/> picks, or -1 when it jumps somewhere unknown.</summary>
            public int ComboOfEntry(short entry) => ComboAt.TryGetValue(TargetOf(entry), out int c) ? c : -1;

            public int ComboOf(int trainerClass)
            {
                int i = trainerClass - FirstClass;
                return i >= 0 && i < Entries.Length ? ComboOfEntry(Entries[i]) : DefaultCombo;
            }

            /// <summary>A place that picks <paramref name="combo"/>, or -1 when no stub does.</summary>
            public int TargetFor(int combo)
            {
                int best = -1;
                foreach (KeyValuePair<int, int> kv in ComboAt)
                    if (kv.Value == combo && (best < 0 || kv.Key < best)) best = kv.Key;
                return best;
            }

            private const ushort AddPcR0 = 0x4487, ReturnR1 = 0x1C08;

            /// <summary>Decodes the switch around <paramref name="table"/> in <paramref name="arm9"/>, or null.</summary>
            public static ClassJumpTable Decode(byte[] arm9, int table, int count)
            {
                ushort At(int o) => o >= 0 && o + 2 <= arm9.Length ? BitConverter.ToUInt16(arm9, o) : (ushort)0;
                if (table < 0x14 || count <= 0 || table + 2 * count > arm9.Length) return null;
                ushort subs = At(table - 0x14), movs = At(table - 0x12), cmp = At(table - 0x10);
                if ((subs & 0xFF00) != 0x3800 || (movs & 0xFF00) != 0x2100 || cmp != (0x2800 | (count - 1))
                    || At(table - 2) != AddPcR0) return null;

                ClassJumpTable t = new ClassJumpTable { FirstClass = subs & 0xFF, DefaultCombo = movs & 0xFF, Start = table };
                t.Entries = new short[count];
                for (int i = 0; i < count; i++) t.Entries[i] = (short)At(table + 2 * i);

                // Stubs follow the table four bytes apart; the last falls through into the return.
                int p = table + 2 * count;
                while ((At(p) & 0xFF00) == 0x2100)
                {
                    t.ComboAt[p] = At(p) & 0xFF;
                    if ((At(p + 2) & 0xF800) == 0xE000) p += 4; else { p += 2; break; }
                }
                if (At(p) == ReturnR1) t.ComboAt[p] = t.DefaultCombo;
                foreach (short e in t.Entries)
                {
                    int target = t.TargetOf(e);
                    if (!t.ComboAt.ContainsKey(target) && (At(target) & 0xFF00) == 0x2100) t.ComboAt[target] = At(target) & 0xFF;
                }
                return t;
            }
        }

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

        /// <summary>DP/Pt: the wild species the selection code gives their own combo. Empty for HGSS, which keeps a table.</summary>
        public IReadOnlyDictionary<int, int> CodeSpeciesCombos =>
            gameFamily == GameFamilies.DP ? DpSpeciesCombo : gameFamily == GameFamilies.Plat ? PtSpeciesCombo : new Dictionary<int, int>();

        /// <summary>The theme for a battle against this trainer class, or -1.</summary>
        public int TrainerSequence(int trainerClass, bool kanto = false)
        {
            if (ClassJumps != null)
                return ComboSequence(ClassJumps.ComboOf(trainerClass));
            if (gameFamily == GameFamilies.DP)
                return ComboSequence(DpClassCombo.TryGetValue(trainerClass, out int d) ? d : DpNormalTrainer);
            if (gameFamily == GameFamilies.Plat)
                return ComboSequence(PtClassCombo.TryGetValue(trainerClass, out int c) ? c : PtNormalTrainer);

            int combo = HgssTrainerCombo;
            foreach ((int Class, int Combo) row in Classes.Rows)
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
            foreach ((int Species, int Combo) row in Species.Rows)
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
            BattleMusicTables t = new BattleMusicTables();

            bool hgss = gameFamily == GameFamilies.HGSS;
            if (gameFamily == GameFamilies.DP && !DpPointersAgree()) return null;
            int comboCount = hgss ? ARM9.ReadByte(effectsComboTableOffsetToSizeLimiter)
                : gameFamily == GameFamilies.DP ? DpComboCount : PtNormalWild + 1;
            Locate(t.Combos, effectsComboTableOffsetToRAMAddress);
            using (DSUtils.EasyReader r = new DSUtils.EasyReader(t.Combos.Path, t.Combos.Start))
                for (int i = 0; i < comboCount; i++) t.Combos.Rows.Add((r.ReadUInt16(), r.ReadUInt16()));

            if (hgss)
            {
                ReadPacked(t.Classes, vsTrainerEntryTableOffsetToRAMAddress, ARM9.ReadByte(vsTrainerEntryTableOffsetToSizeLimiter));
                ReadPacked(t.Species, vsPokemonEntryTableOffsetToRAMAddress, ARM9.ReadByte(vsPokemonEntryTableOffsetToSizeLimiter));
            }

            VsIntroSites sites = VsIntroCodeSites;
            uint musicLiteral = effectsComboTableSecondPointerOffset != 0 ? effectsComboTableSecondPointerOffset
                : sites != null && sites.ComboMusicLiteral >= 0 ? (uint)sites.ComboMusicLiteral : 0;
            if (musicLiteral != 0)
                t.MusicPointerAgrees = BitConverter.ToUInt32(ARM9.ReadBytes(musicLiteral, 4), 0)
                    == BitConverter.ToUInt32(ARM9.ReadBytes(effectsComboTableOffsetToRAMAddress, 4), 0) + 2;
            if (!hgss && sites != null && sites.ClassJumpTable >= 0 && File.Exists(arm9Path))
                t.ClassJumps = ClassJumpTable.Decode(File.ReadAllBytes(arm9Path), sites.ClassJumpTable, sites.ClassJumpCount);
            return t;
        }

        /// <summary>Writes combo row <paramref name="index"/> back where the table was read from.</summary>
        public void WriteCombo(int index)
        {
            (ushort transition, ushort sequence) = Combos.Rows[index];
            using DSUtils.EasyWriter w = new DSUtils.EasyWriter(Combos.Path, Combos.Start + 4 * (uint)index);
            w.Write(transition);
            w.Write(sequence);
        }

        /// <summary>Writes class row <paramref name="index"/> back, the class in the low 10 bits and the combo above.</summary>
        public void WriteClass(int index)
        {
            (int trainerClass, int combo) = Classes.Rows[index];
            using DSUtils.EasyWriter w = new DSUtils.EasyWriter(Classes.Path, Classes.Start + 2 * (uint)index);
            w.Write((ushort)((trainerClass & 0x3FF) | (combo << 10)));
        }

        /// <summary>Writes species row <paramref name="index"/> back, the species in the low 10 bits and the combo above.</summary>
        public void WriteSpecies(int index)
        {
            (int species, int combo) = Species.Rows[index];
            using DSUtils.EasyWriter w = new DSUtils.EasyWriter(Species.Path, Species.Start + 2 * (uint)index);
            w.Write((ushort)((species & 0x3FF) | (combo << 10)));
        }

        // A second pointer sits two bytes into the same table; if they disagree, the layout is not the known one.
        private static bool DpPointersAgree() =>
            BitConverter.ToUInt32(ARM9.ReadBytes(effectsComboTableSecondPointerOffset, 4), 0)
            == BitConverter.ToUInt32(ARM9.ReadBytes(effectsComboTableOffsetToRAMAddress, 4), 0) + 2;

        // A row is an id in the low 10 bits and a combo in the high 6.
        private static void ReadPacked(Table<(int, int)> table, uint pointerOffset, int count)
        {
            // An emptied table may keep a null pointer, as the trainer class metadata patch (PR #272) leaves the class table.
            if (count == 0) return;
            Locate(table, pointerOffset);
            using DSUtils.EasyReader r = new DSUtils.EasyReader(table.Path, table.Start);
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
