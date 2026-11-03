using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// The Bug-Catching Contest prizes from overlay 24's judging code: unpatched, first place counts up from one of two
    /// starting items. With the prize table patch every place reads a table in the synthetic overlay.
    /// </summary>
    public sealed class BugContestPrizes
    {
        public int RollCount { get; set; }
        public int FirstRunLength { get; set; }
        public int FirstStart { get; set; }
        public int SecondStart { get; set; }
        public int Second { get; set; }
        public int Third { get; set; }
        public int Consolation { get; set; }

        /// <summary>First place's slots when the patch is in; null otherwise.</summary>
        public List<int> Slots { get; set; }
        public bool Patched => Slots != null;
        private int _block = -1;

        /// <summary>The highest item a one-byte prize can be.</summary>
        public const int ByteItemMax = 255;

        // Each site's instruction and register, in the order RomLocator lists them.
        private static readonly (ThumbImm8.Op Op, int Register)[] Sites =
        {
            (ThumbImm8.Op.Movs, 1), (ThumbImm8.Op.Cmp, 0), (ThumbImm8.Op.Adds, 0), (ThumbImm8.Op.Adds, 0), (ThumbImm8.Op.Movs, 0), (ThumbImm8.Op.Movs, 0),
        };

        private const RomLocator.Thing Thing = RomLocator.Thing.BugContestPrizes;
        private static RomLocator.Spot Where => RomLocator.Where(Thing);

        /// <summary>The prizes, or null with <paramref name="why"/> set when they can't be edited here.</summary>
        public static BugContestPrizes TryRead(out string why)
        {
            why = Where == null ? "HeartGold and SoulSilver only." : null;
            if (why != null) return null;
            try { return Read(); }
            catch (Exception e) when (e is InvalidDataException || e is IOException || e is InvalidOperationException) { why = e.Message; return null; }
        }

        private static byte[] ReadCode() => RomLocator.ReadFile(Thing);

        private static string CodePath => RomLocator.PathOf(Where);

        /// <summary>The file the prize code is in, for backups.</summary>
        public static string CodeFile => Where == null ? null : CodePath;

        private static int Imm(byte[] d, int site, int k)
        {
            ushort op = site + 1 < d.Length ? BitConverter.ToUInt16(d, site) : (ushort)0;
            if (site + 1 >= d.Length || !ThumbImm8.Is(op, Sites[k].Op) || ThumbImm8.Register(op) != Sites[k].Register)
                throw new InvalidDataException("The Bug Contest prize code doesn't look like the game's. It may have been patched.");
            return ThumbImm8.Value(op);
        }

        // ── The prize table patch ──────────────────────────────────────────────────
        // Block: second, third, everyone else, then first place's slots, all u16. Offsets are from the "cmp r0, #6" site.
        // The bl and blx around the roll stay put so their offsets hold.
        public const string Marker = "BUGPRIZETBL1";
        public const int Capacity = 64;
        private const int Fixed = 3;
        public const int BlockLength = SyntheticOverlaySpace.HeaderSize + (Fixed + Capacity) * 2 + 2;

        private const int BranchFirst = -0x14, BranchOne = -0x20, BranchTwo = -0x1C, BranchOther = -0x1A;
        private const int Rewrite = -8, RewriteEnd = 0x28, TableLiteral = 0x18;

        private static readonly (int At, byte[] Vanilla, byte[] Patched)[] Branches =
        {
            (BranchFirst, new byte[] { 0x07, 0xD0 }, new byte[] { 0x0C, 0xD0 }),
            (BranchOne, new byte[] { 0x16, 0xD0 }, new byte[] { 0x0C, 0xD0 }),
            (BranchTwo, new byte[] { 0x17, 0xD0 }, new byte[] { 0x0C, 0xD0 }),
            (BranchOther, new byte[] { 0x19, 0xE0 }, new byte[] { 0x0D, 0xE0 }),
        };

        // lsls r0, r1, #24; lsrs r0, r0, #24; b +0; movs r0, #0
        private static readonly byte[] VanillaRoll = { 0x08, 0x06, 0x00, 0x0E, 0x00, 0xE0, 0x00, 0x20 };

        private static readonly byte[] PatchedCode =
        {
            0x03, 0x31, 0x06, 0xE0,   // first place: adds r1, #3 (past the fixed three); b lookup
            0x00, 0x21, 0x04, 0xE0,   // second: movs r1, #0; b lookup
            0x01, 0x21, 0x02, 0xE0,   // third: movs r1, #1; b lookup
            0x02, 0x21, 0x00, 0xE0,   // everyone else: movs r1, #2; b lookup
            0x03, 0x21,               // first place before the National Dex: movs r1, #3
            0x49, 0x00,               // lookup: lsls r1, r1, #1
            0x02, 0x48,               // ldr r0, =table
            0x40, 0x5A,               // ldrh r0, [r0, r1]
            0x60, 0x83,               // strh r0, [r4, #26]
            0xF8, 0xBD,               // pop {r3-r7, pc}
            0xC0, 0x46, 0xC0, 0x46,   // nop; nop
        };

        private static bool Matches(byte[] d, int r, bool patched)
        {
            if (r + Rewrite < 0 || r + RewriteEnd > d.Length) return false;
            foreach ((int at, byte[] vanilla, byte[] done) in Branches)
                if (!d.AsSpan(r + at, 2).SequenceEqual(patched ? done : vanilla)) return false;
            return patched ? d.AsSpan(r + Rewrite, PatchedCode.Length).SequenceEqual(PatchedCode)
                           : d.AsSpan(r + Rewrite, VanillaRoll.Length).SequenceEqual(VanillaRoll);
        }

        /// <summary>Whether the prize table patch is in.</summary>
        public static bool IsApplied
        {
            get
            {
                RomLocator.Spot spot = Where;
                if (spot == null) return false;
                try { return Matches(ReadCode(), spot.Offsets[1], true); }
                catch (Exception e) when (e is IOException || e is InvalidOperationException) { AppLogger.WarnOnce("BugContestPrizes.IsApplied", e); return false; }
            }
        }

        public static bool SupportsCurrentRom => Where != null;

        /// <summary>Why the prize table patch can't go in now, or null.</summary>
        public static string WhyNotApplicable()
        {
            RomLocator.Spot spot = Where;
            if (spot == null) return "HeartGold and SoulSilver only.";
            if (!SyntheticOverlaySpace.Available()) return "Apply the ARM9 expansion first.";
            byte[] d = ReadCode();
            return Matches(d, spot.Offsets[1], false) ? null
                : "Something else has already changed the Bug Contest prize code, so DSPRE leaves it alone.";
        }

        /// <summary>Writes a table holding the current prizes into a marked block at <paramref name="blockOffset"/> and points the code at it.</summary>
        public static void ApplyPatch(int blockOffset)
        {
            BugContestPrizes now = Read();
            RomLocator.Spot spot = Where;
            int r = spot.Offsets[1];
            byte[] d = ReadCode();

            List<int> slots = new List<int>();
            for (int k = 0; k < now.RollCount; k++)
                slots.Add(k < now.FirstRunLength ? now.FirstStart + k : now.SecondStart + k - now.FirstRunLength);
            now.Slots = slots;
            now._block = blockOffset;
            DSUtils.WriteToFile(Filesystem.ExpArmPath, SyntheticOverlaySpace.Header(Marker, BlockLength), (uint)blockOffset);
            now.WriteBlock();

            byte[] code = new byte[RewriteEnd - Rewrite];
            PatchedCode.CopyTo(code, 0);
            BitConverter.GetBytes(RomInfo.SynthOverlayLoadAddress + (uint)blockOffset + SyntheticOverlaySpace.HeaderSize).CopyTo(code, TableLiteral - Rewrite);
            code.CopyTo(d, r + Rewrite);
            foreach ((int at, byte[] _, byte[] done) in Branches) done.CopyTo(d, r + at);
            DSUtils.WriteWholeFile(CodePath, d);
        }

        private void WriteBlock()
        {
            byte[] words = new byte[(Fixed + Capacity) * 2];
            int[] head = { Second, Third, Consolation };
            for (int k = 0; k < Fixed; k++) BitConverter.GetBytes((ushort)head[k]).CopyTo(words, k * 2);
            for (int k = 0; k < Slots.Count; k++) BitConverter.GetBytes((ushort)Slots[k]).CopyTo(words, (Fixed + k) * 2);
            DSUtils.WriteToFile(Filesystem.ExpArmPath, words, (uint)(_block + SyntheticOverlaySpace.HeaderSize));
        }

        // The block the patched code points at, or -1.
        private static int BlockOffset(byte[] d, int r)
        {
            long at = (long)BitConverter.ToUInt32(d, r + TableLiteral) - SyntheticOverlaySpace.HeaderSize - RomInfo.SynthOverlayLoadAddress;
            if (at < 0 || !File.Exists(Filesystem.ExpArmPath) || at + BlockLength > new FileInfo(Filesystem.ExpArmPath).Length) return -1;
            return Encoding.ASCII.GetString(DSUtils.ReadFromFile(Filesystem.ExpArmPath, (uint)at, Marker.Length)) == Marker ? (int)at : -1;
        }

        public static BugContestPrizes Read()
        {
            RomLocator.Spot spot = Where ?? throw new InvalidDataException("HeartGold and SoulSilver only.");
            byte[] d = ReadCode();
            int[] at = spot.Offsets;
            if (at[6] + 4 > d.Length) throw new InvalidDataException("The Bug Contest code file is shorter than the game's.");
            if (Matches(d, at[1], true)) return ReadPatched(d, at);
            int run = Imm(d, at[1], 1);
            return new BugContestPrizes
            {
                RollCount = Imm(d, at[0], 0),
                FirstRunLength = run,
                FirstStart = Imm(d, at[2], 2),
                SecondStart = Imm(d, at[3], 3) + run,
                Second = Imm(d, at[4], 4),
                Third = Imm(d, at[5], 5),
                Consolation = (int)BitConverter.ToUInt32(d, at[6]),
            };
        }

        private static BugContestPrizes ReadPatched(byte[] d, int[] at)
        {
            int block = BlockOffset(d, at[1]);
            if (block < 0) throw new InvalidDataException("The Bug Contest prize table isn't in the synthetic overlay.");
            int count = Imm(d, at[0], 0);
            if (count < 1 || count > Capacity) throw new InvalidDataException("The Bug Contest prize table's slot count is out of range.");
            byte[] words = DSUtils.ReadFromFile(Filesystem.ExpArmPath, (uint)(block + SyntheticOverlaySpace.HeaderSize), (Fixed + count) * 2);
            List<int> slots = new List<int>();
            for (int k = 0; k < count; k++) slots.Add(BitConverter.ToUInt16(words, (Fixed + k) * 2));
            return new BugContestPrizes
            {
                Slots = slots,
                RollCount = count,
                Second = BitConverter.ToUInt16(words, 0),
                Third = BitConverter.ToUInt16(words, 2),
                Consolation = BitConverter.ToUInt16(words, 4),
                _block = block,
            };
        }

        /// <summary>Why these values can't be written, or null.</summary>
        public string Problem(int itemCount)
        {
            if (Patched)
            {
                if (Slots.Count < 1 || Slots.Count > Capacity) return $"First place needs 1 to {Capacity} slots.";
                foreach (int item in Slots) if (item < 0 || item >= itemCount) return "A slot isn't an item.";
                if (Second < 0 || Second >= itemCount || Third < 0 || Third >= itemCount || Consolation < 0 || Consolation >= itemCount)
                    return "A place's prize isn't an item.";
                return null;
            }
            if (RollCount < 1 || RollCount > 255) return "First place needs 1 to 255 slots.";
            if (FirstRunLength < 1 || FirstRunLength > RollCount) return $"The first group has to fill 1 to {RollCount} slots.";
            if (FirstStart > ByteItemMax) return $"The first group can only start at items up to #{ByteItemMax}.";
            if (Second > ByteItemMax || Third > ByteItemMax) return $"Second and third place can only be items up to #{ByteItemMax}.";
            if (RollCount > FirstRunLength && (SecondStart < FirstRunLength || SecondStart - FirstRunLength > ByteItemMax))
                return $"The second group can only start at items from #{FirstRunLength} to #{ByteItemMax + FirstRunLength}.";
            if (FirstStart + FirstRunLength > itemCount || RollCount > FirstRunLength && SecondStart + RollCount - FirstRunLength > itemCount)
                return "A group runs past the last item.";
            if (Consolation < 0 || Consolation >= itemCount) return "The prize for everyone else isn't an item.";
            return null;
        }

        public void Write(int itemCount)
        {
            if (Problem(itemCount) is string p) throw new InvalidOperationException(p);
            RomLocator.Spot spot = Where;
            byte[] d = ReadCode();
            int[] at = spot.Offsets;
            if (Patched)
            {
                Imm(d, at[0], 0);
                d[at[0]] = (byte)Slots.Count;
                WriteBlock();
                DSUtils.WriteWholeFile(CodePath, d);
                return;
            }
            // With no second group its base is never read, so it keeps what the game has.
            int secondBase = RollCount > FirstRunLength ? SecondStart - FirstRunLength : Imm(d, at[3], 3);
            int[] values = { RollCount, FirstRunLength, FirstStart, secondBase, Second, Third };
            for (int k = 0; k < values.Length; k++) { Imm(d, at[k], k); d[at[k]] = (byte)values[k]; }
            BitConverter.GetBytes((uint)Consolation).CopyTo(d, at[6]);
            DSUtils.WriteWholeFile(CodePath, d);
        }
    }
}
