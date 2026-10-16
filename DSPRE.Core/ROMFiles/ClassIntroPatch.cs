using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using static DSPRE.RomInfo;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// Diamond, Pearl and Platinum: the class-to-intro switch (EncEffects_TrainerClassEffect, sub_020475C0 in Diamond and
    /// Pearl) is replaced by one 32-byte row per class in the synthetic overlay: style, battle music, a gym leader record
    /// and an Elite Four record. Each style borrows an effect pair only the switch picks; the routine writes the class's
    /// music into it and copies the record into the buffer one gym leader's and Aaron's intro read. Applying fills every
    /// row with the game's current intros. Source: Resources/ROMToolboxDB/ClassIntros/class_intros.s.
    /// </summary>
    public static class ClassIntroPatch
    {
        public const string Marker = "CLASSINTRO01";
        public const int Classes = 256, RowSize = 32, GymRecordAt = 4, LeagueRecordAt = 24, LeagueRecordSize = 8;

        public enum Style { Ordinary, Gym, League, GalacticGrunt, GalacticBoss, FrontierBrain, Double }
        public const int StyleCount = 7;

        // Per game: the pair each style borrows (StylePairs in class_intros.s), the intro that pair must keep, the gym
        // record whose routine reads the class's record, and what the style list calls the Frontier Brain style.
        private sealed record Game(int[] StylePair, ushort[] StyleCutIn, int BorrowedGym, string BrainStyle);
        private const ushort UseTerrain = 0xFFFF;
        private static readonly Game Platinum = new(new[] { 33, 7, 8, 24, 25, 31, 13 }, new ushort[] { UseTerrain, 19, 20, 27, 28, 29, 30 }, 7, "Frontier Brain");
        private static readonly Game DiamondPearl = new(new[] { 29, 0, 8, 21, 22, 28, 13 }, new ushort[] { UseTerrain, 12, 20, 27, 28, 29, 30 }, 0, "Tower Tycoon");
        private static Game Current => gameFamily == GameFamilies.DP ? DiamondPearl : Platinum;

        public static string[] StyleNames => new[]
        {
            "Ordinary (Poké Ball transition)", "Gym leader", "Elite Four or Champion", "Galactic grunt", "Galactic boss",
            Current.BrainStyle, "Double battle",
        };

        /// <summary>The intro table rows the styles borrow. With the patch the class's row sets their music for each battle.</summary>
        public static IReadOnlyCollection<int> BorrowedPairs => new HashSet<int>(Current.StylePair);

        /// <summary>A gym leader record's length: 20 bytes in Platinum, 8 in Diamond and Pearl, which cut the face from the class's sprite.</summary>
        public static int GymRecordSize => VsIntroCodeSites?.GymSize ?? 20;
        private const int GymCutIn = 12, LeagueCutIn = 20, GymCount = 8, LeagueCount = 5;

        // Assembled from class_intros.s: header, ClassEffect, the pair table word, the style pairs and the two buffers.
        private static readonly byte[] Code =
        {
            0x44, 0x53, 0x43, 0x49, 0x01, 0x00, 0x94, 0x00, 0x6c, 0x00, 0x00, 0x00,
            0x78, 0x00, 0x00, 0x00, 0x8c, 0x00, 0x00, 0x00, 0x18, 0x00, 0x00, 0x00,
            0x10, 0xb5, 0xff, 0x28, 0x23, 0xd8, 0x44, 0x01, 0x1c, 0xa1, 0x64, 0x18,
            0x21, 0x78, 0x06, 0x29, 0x00, 0xd9, 0x00, 0x21, 0x10, 0xa2, 0x50, 0x5c,
            0x0e, 0x4a, 0x83, 0x00, 0xd2, 0x18, 0x63, 0x88, 0x53, 0x80, 0x01, 0x29,
            0x0b, 0xd1, 0x0e, 0xa2, 0x63, 0x68, 0x13, 0x60, 0xa3, 0x68, 0x53, 0x60,
            0xe3, 0x68, 0x93, 0x60, 0x23, 0x69, 0xd3, 0x60, 0x63, 0x69, 0x13, 0x61,
            0x06, 0xe0, 0x02, 0x29, 0x04, 0xd1, 0x0c, 0xa2, 0xa3, 0x69, 0x13, 0x60,
            0xe3, 0x69, 0x53, 0x60, 0x10, 0xbd, 0x02, 0xa2, 0x10, 0x78, 0x10, 0xbd,
            0x00, 0x00, 0x00, 0x00, 0x21, 0x07, 0x08, 0x18, 0x19, 0x1f, 0x0d, 0x21,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
        };
        private const int ClassEffectAt = 0x18, PairTableAt = 0x6C, StylePairsAt = 0x70, GymBufferAt = 0x78, LeagueBufferAt = 0x8C, RowsAt = 0x94;
        private static int CodeStart => SyntheticOverlaySpace.HeaderSize;
        public static int BlockLength => CodeStart + RowsAt + Classes * RowSize;

        // ── Sites ────────────────────────────────────────────────────────────────────────────────

        private sealed record Sites(int SwitchStart, uint PairTable, int Overlay, int GymLiteral, int LeagueLiteral, uint GymTable, uint LeagueTable);

        // European English reads these through its code twins.
        private static readonly HashSet<(string, int)> Measured = new() { ("CPUE", 1), ("ADAE", 5), ("APAE", 5) };

        /// <summary>The dumps the patch is mapped for, with the European English revisions that copy their code.</summary>
        public static List<(string id, int rev)> SupportedDumps => WithTwins(Measured);
        public static bool SupportsCurrentRom => (gameFamily == GameFamilies.Plat || gameFamily == GameFamilies.DP) && romID != null
                                                 && Measured.Contains(CodeKey) && VsIntroCodeSites is VsIntroSites s
                                                 && s.ClassJumpTable >= 0 && s.GymTable >= 0 && s.LeagueTable >= 0;

        // The switch starts 0x14 bytes before its jump table: subs, movs, cmp, bhi, then four lines into add pc.
        private static int SwitchStart => VsIntroCodeSites.ClassJumpTable - 0x14;

        /// <summary>The literal in <paramref name="code"/> that holds <paramref name="value"/>, when exactly one does.</summary>
        private static int OnlyLiteral(byte[] code, uint value)
        {
            int found = -1;
            for (int i = 0; i + 4 <= code.Length; i += 4)
                if (BitConverter.ToUInt32(code, i) == value) { if (found >= 0) return -1; found = i; }
            return found;
        }

        private static Sites Find(out string why)
        {
            why = null;
            VsIntroSites s = VsIntroCodeSites;
            uint pairs = BitConverter.ToUInt32(ARM9.ReadBytes(effectsComboTableOffsetToRAMAddress, 4), 0);
            if (pairs < ARM9.address || pairs >= synthOverlayLoadAddress) { why = "The intro table isn't where the game keeps it."; return null; }
            string ovPath = OverlayUtils.GetPath(s.RecordOverlay);
            if (OverlayUtils.IsCompressed(s.RecordOverlay)) OverlayUtils.Decompress(s.RecordOverlay);
            byte[] ov = File.ReadAllBytes(ovPath);
            uint ovBase = OverlayUtils.OverlayTable.GetRAMAddress(s.RecordOverlay);
            uint gym = ovBase + (uint)(s.GymTable + GymRecordSize * Current.BorrowedGym), league = ovBase + (uint)s.LeagueTable;
            int gymLiteral = OnlyLiteral(ov, gym), leagueLiteral = OnlyLiteral(ov, league);
            if (gymLiteral < 0 || leagueLiteral < 0) { why = "The gym and Elite Four intros don't read their records as expected."; return null; }
            return new Sites(SwitchStart, pairs, s.RecordOverlay, gymLiteral, leagueLiteral, gym, league);
        }

        // ldr r3, [pc]; bx r3; the routine's address. A switch on a halfword boundary needs a nop to align the word.
        private static byte[] Hook(int at, uint target) => at % 4 == 0
            ? new byte[] { 0x00, 0x4B, 0x18, 0x47 }.Concat(BitConverter.GetBytes(target)).ToArray()
            : new byte[] { 0x01, 0x4B, 0x18, 0x47, 0xC0, 0x46 }.Concat(BitConverter.GetBytes(target)).ToArray();

        /// <summary>Where the patch's block starts in the synthetic overlay, or -1 when the patch is not applied.</summary>
        public static int InstalledOffset()
        {
            if (!SupportsCurrentRom || !File.Exists(Filesystem.expArmPath)) return -1;
            try
            {
                int at = SwitchStart;
                byte[] now = ARM9.ReadBytes((uint)at, 10);
                int literal = now[0] == 0x00 && now[1] == 0x4B && now[2] == 0x18 && now[3] == 0x47 ? 4
                            : now[0] == 0x01 && now[1] == 0x4B && now[2] == 0x18 && now[3] == 0x47 ? 6 : -1;
                if (literal < 0) return -1;
                long block = (long)(BitConverter.ToUInt32(now, literal) & ~1u) - synthOverlayLoadAddress - CodeStart - ClassEffectAt;
                if (block < 0 || block + BlockLength > new FileInfo(Filesystem.expArmPath).Length) return -1;
                byte[] head = DSUtils.ReadFromFile(Filesystem.expArmPath, (uint)block, Marker.Length);
                return Encoding.ASCII.GetString(head) == Marker ? (int)block : -1;
            }
            catch (Exception ex) when (ex is IOException || ex is ArgumentException) { return -1; }
        }

        public static bool IsApplied => InstalledOffset() >= 0;

        /// <summary>Why the patch can't go in now, or null.</summary>
        public static string WhyNotApplicable()
        {
            if (!SupportsCurrentRom) return "The intro code isn't mapped for this ROM.";
            if (!SyntheticOverlaySpace.Available()) return "Apply the ARM9 expansion first.";
            if (BattleMusicTables.LoadRom()?.ClassJumps == null) return "The class intro switch has already been changed by something else.";
            return Find(out string why) == null ? why : null;
        }

        // ── Rows ─────────────────────────────────────────────────────────────────────────────────

        /// <summary>One class's row as the game reads it.</summary>
        public sealed class Row
        {
            public readonly byte[] Bytes;
            public Row(byte[] bytes) { Bytes = bytes; }

            public Style Style { get => (Style)Math.Min((int)Bytes[0], StyleCount - 1); set => Bytes[0] = (byte)value; }
            public ushort Music { get => BitConverter.ToUInt16(Bytes, 2); set => BitConverter.GetBytes(value).CopyTo(Bytes, 2); }

            // Gym leader record (GymLeaderEncounterParam): end X in 1/4096 pixel, trainer, class, then file numbers.
            public int GymEndX { get => BitConverter.ToInt32(Bytes, GymRecordAt) >> 12; set => BitConverter.GetBytes(value << 12).CopyTo(Bytes, GymRecordAt); }
            public int GymTrainer { get => (int)BitConverter.ToUInt32(Bytes, GymRecordAt + 4); set => BitConverter.GetBytes((uint)value).CopyTo(Bytes, GymRecordAt + 4); }
            public int GymClass { get => BitConverter.ToUInt16(Bytes, GymRecordAt + 8); set => BitConverter.GetBytes((ushort)value).CopyTo(Bytes, GymRecordAt + 8); }
            /// <summary>Portrait palette, tiles, cells, animation.</summary>
            public int[] GymPortrait { get => Members(GymRecordAt + 12, 4); set => SetMembers(GymRecordAt + 12, value); }
            /// <summary>Banner palette, tiles, screen.</summary>
            public int[] GymBanner { get => Members(GymRecordAt + 16, 3); set => SetMembers(GymRecordAt + 16, value); }

            // Elite Four record: portrait palette (tiles, cells and animation follow it), frame palette, clash frames, class, trainer.
            public int LeaguePortrait { get => BitConverter.ToUInt16(Bytes, LeagueRecordAt); set => BitConverter.GetBytes((ushort)value).CopyTo(Bytes, LeagueRecordAt); }
            public int LeagueFrame { get => Bytes[LeagueRecordAt + 2]; set => Bytes[LeagueRecordAt + 2] = (byte)value; }
            public int LeagueClash { get => Bytes[LeagueRecordAt + 3]; set => Bytes[LeagueRecordAt + 3] = (byte)value; }
            public int LeagueClass { get => BitConverter.ToUInt16(Bytes, LeagueRecordAt + 4); set => BitConverter.GetBytes((ushort)value).CopyTo(Bytes, LeagueRecordAt + 4); }
            public int LeagueTrainer { get => BitConverter.ToUInt16(Bytes, LeagueRecordAt + 6); set => BitConverter.GetBytes((ushort)value).CopyTo(Bytes, LeagueRecordAt + 6); }

            // Diamond and Pearl gym leader record: end X, the class whose battle sprite the face is cut from, and the
            // 8-pixel column the cut starts at.
            public int DpGymClass { get => BitConverter.ToUInt16(Bytes, GymRecordAt + 4); set => BitConverter.GetBytes((ushort)value).CopyTo(Bytes, GymRecordAt + 4); }
            public int DpFaceColumn { get => BitConverter.ToUInt16(Bytes, GymRecordAt + 6); set => BitConverter.GetBytes((ushort)value).CopyTo(Bytes, GymRecordAt + 6); }

            // Diamond and Pearl Elite Four record: the camera turn and the frames it takes, the class the face comes from,
            // and the banner palette.
            public int DpCameraTurn { get => (int)BitConverter.ToUInt32(Bytes, LeagueRecordAt); set => BitConverter.GetBytes((uint)value).CopyTo(Bytes, LeagueRecordAt); }
            public int DpPanFrames { get => Bytes[LeagueRecordAt + 4]; set => Bytes[LeagueRecordAt + 4] = (byte)value; }
            public int DpLeagueClass { get => Bytes[LeagueRecordAt + 5]; set => Bytes[LeagueRecordAt + 5] = (byte)value; }
            public int DpBannerPalette { get => Bytes[LeagueRecordAt + 6]; set => Bytes[LeagueRecordAt + 6] = (byte)value; }

            private int[] Members(int at, int n) => Enumerable.Range(0, n).Select(i => (int)Bytes[at + i]).ToArray();
            private void SetMembers(int at, int[] m) { for (int i = 0; i < m.Length; i++) Bytes[at + i] = (byte)m[i]; }
        }

        /// <summary>The game's own intro for every class, as rows: what the switch, the pairs and the records say now.</summary>
        private static List<Row> RowsFromGame(Sites sites)
        {
            BattleMusicTables music = BattleMusicTables.LoadRom();
            BattleMusicTables.ClassJumpTable jumps = music.ClassJumps;
            byte[] ov = File.ReadAllBytes(OverlayUtils.GetPath(sites.Overlay));
            VsIntroSites s = VsIntroCodeSites;
            byte[] Gym(int i) => ov.AsSpan(s.GymTable + GymRecordSize * i, GymRecordSize).ToArray();
            byte[] League(int i) => ov.AsSpan(s.LeagueTable + LeagueRecordSize * i, LeagueRecordSize).ToArray();

            List<Row> rows = new List<Row>();
            for (int c = 0; c < Classes; c++)
            {
                int pair = jumps.ComboOf(c);
                if (pair < 0 || pair >= music.Combos.Rows.Count) pair = jumps.DefaultCombo;
                (ushort cutIn, ushort sequence) = music.Combos.Rows[pair];
                Row row = new Row(new byte[RowSize]) { Music = sequence };
                // A class that isn't a gym leader or in the League keeps Roark's and Aaron's records as a starting point.
                // Platinum's gym records are 20 bytes and Diamond and Pearl's 8; the rest of the 20 stays zero.
                Gym(cutIn >= GymCutIn && cutIn < GymCutIn + GymCount ? cutIn - GymCutIn : 0).CopyTo(row.Bytes, GymRecordAt);
                League(cutIn >= LeagueCutIn && cutIn < LeagueCutIn + LeagueCount ? cutIn - LeagueCutIn : 0).CopyTo(row.Bytes, LeagueRecordAt);
                row.Style = cutIn switch
                {
                    >= GymCutIn and < GymCutIn + GymCount => Style.Gym,
                    >= LeagueCutIn and < LeagueCutIn + LeagueCount => Style.League,
                    27 => Style.GalacticGrunt,
                    28 => Style.GalacticBoss,
                    29 => Style.FrontierBrain,
                    30 => Style.Double,
                    _ => Style.Ordinary,
                };
                rows.Add(row);
            }
            return rows;
        }

        public static List<Row> ReadRows()
        {
            int block = InstalledOffset();
            if (block < 0) return null;
            byte[] all = DSUtils.ReadFromFile(Filesystem.expArmPath, (uint)(block + CodeStart + RowsAt), Classes * RowSize);
            return Enumerable.Range(0, Classes).Select(c => new Row(all.AsSpan(c * RowSize, RowSize).ToArray())).ToList();
        }

        public static void WriteRow(int trainerClass, Row row)
        {
            int block = InstalledOffset();
            if (block < 0 || trainerClass < 0 || trainerClass >= Classes) throw new InvalidOperationException("The class intros patch is not applied.");
            DSUtils.WriteToFile(Filesystem.expArmPath, row.Bytes, (uint)(block + CodeStart + RowsAt + trainerClass * RowSize));
        }

        // ── Applying ─────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Writes the block at <paramref name="blockOffset"/> (4-byte aligned) with every class's row taken from the game,
        /// keeps the borrowed pairs on their intros, points the borrowed gym leader's and Aaron's intros at the buffers and
        /// hooks the switch.
        /// </summary>
        public static void Apply(int blockOffset)
        {
            if (blockOffset % 4 != 0) throw new ArgumentException("The block must start on a word.", nameof(blockOffset));
            Sites sites = Find(out string why) ?? throw new InvalidOperationException(why);
            List<Row> rows = RowsFromGame(sites);

            uint code = synthOverlayLoadAddress + (uint)blockOffset + (uint)CodeStart;
            byte[] block = new byte[BlockLength];
            Encoding.ASCII.GetBytes(Marker).CopyTo(block, 0);
            BitConverter.GetBytes(1u).CopyTo(block, 0x0C);
            BitConverter.GetBytes((uint)BlockLength).CopyTo(block, 0x10);
            Code.CopyTo(block, CodeStart);
            BitConverter.GetBytes(sites.PairTable).CopyTo(block, CodeStart + PairTableAt);
            for (int st = 0; st < StyleCount; st++) block[CodeStart + StylePairsAt + st] = (byte)Current.StylePair[st];
            block[CodeStart + StylePairsAt + StyleCount] = (byte)Current.StylePair[0];
            for (int c = 0; c < Classes; c++) rows[c].Bytes.CopyTo(block, CodeStart + RowsAt + c * RowSize);
            DSUtils.WriteToFile(Filesystem.expArmPath, block, (uint)blockOffset);

            // Each borrowed pair keeps the intro its style draws, whatever an earlier edit pointed it at.
            uint pairsFile = sites.PairTable - ARM9.address;
            for (int st = 0; st < StyleCount; st++)
                ARM9.WriteBytes(BitConverter.GetBytes(Current.StyleCutIn[st]), pairsFile + 4 * (uint)Current.StylePair[st]);

            string ovPath = OverlayUtils.GetPath(sites.Overlay);
            DSUtils.WriteToFile(ovPath, BitConverter.GetBytes(code + GymBufferAt), (uint)sites.GymLiteral);
            DSUtils.WriteToFile(ovPath, BitConverter.GetBytes(code + LeagueBufferAt), (uint)sites.LeagueLiteral);
            ARM9.WriteBytes(Hook(sites.SwitchStart, (code + ClassEffectAt) | 1), (uint)sites.SwitchStart);
        }
    }
}
