using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;

namespace DSPRE
{
    /// <summary>
    /// MrHam88's Trainer Custom Poké Balls v1.0.0: an in-place SetTrMonCapsule replacement.
    /// The routine sits at 0x72A44 in most HeartGold and SoulSilver ARM9s and elsewhere in Japanese, Korean and
    /// Spanish HeartGold, so it is found by its code with the calls left out, and the patch's calls are aimed at the
    /// SetMonData that ROM's own routine calls.
    /// </summary>
    public static class TrainerCustomPokeballsPatch
    {
        public const int Length = 0x54;

        private static readonly byte[] Original = Convert.FromHexString(
            "70B588B0051C0C1C002D02D108B0002070BDB920111C94F715FE01216D1E019102A9" +
            "009118232A1C00215A43061C94F7DBFE201CA22101AAFCF7E0F8201CAB2102AAFCF7" +
            "DBF8301C94F73EFE012008B070BD0000");
        private static readonly int[] OriginalCalls = { 0x16, 0x2E, 0x38, 0x42, 0x48 };
        // Both SetMonData calls of the original; the patch makes the same calls.
        private const int OriginalSetMonData = 0x38;

        // Exact armips output, including zero padding up to sub_02072A98.
        private static readonly byte[] Patched = BuildPatch();
        private static readonly int[] PatchedCalls = { 0x1A, 0x24 };
        private static byte[] BuildPatch()
        {
            byte[] bytes = new byte[Length];
            Convert.FromHexString("30B584B0051C0C1C002D02D104B0002030BD0095201C9B216A46FCF7EFF8" +
                "201CB4216A46FCF7EAF8012004B030BD").CopyTo(bytes, 0);
            return bytes;
        }

        // SetBoxMonDataInternal accepts item IDs 1..16 and 492..499, converting the latter
        // to HGSS ball numbers 17..24. Park Ball (500) is not accepted by that setter.
        public static ReadOnlyCollection<int> BallItemIds { get; } = Array.AsReadOnly(new[]
        {
            0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16,
            492, 493, 494, 495, 496, 497, 498, 499,
        });

        public static bool Supports(string romId) => romId != null && (romId.StartsWith("IPK") || romId.StartsWith("IPG"));
        public static bool SupportsCurrentRom => !RomInfo.IsHGE && Supports(RomInfo.RomID);

        /// <summary>Every offset where <paramref name="code"/> sits, comparing all but its calls.</summary>
        private static List<int> Find(byte[] arm9, byte[] code, int[] calls)
        {
            List<int> found = new List<int>();
            if (arm9 == null) return found;
            bool[] skip = new bool[code.Length];
            foreach (int call in calls) for (int k = 0; k < 4; k++) skip[call + k] = true;
            for (int at = 0; at + code.Length <= arm9.Length; at += 2)
            {
                bool same = true;
                for (int k = 0; k < code.Length && same; k++)
                    same = skip[k] || arm9[at + k] == code[k];
                if (same) found.Add(at);
            }
            return found;
        }

        private static int Only(List<int> found) => found.Count == 1 ? found[0] : -1;

        public static bool Inspect(string romId, byte[] arm9) => Supports(romId) && Only(Find(arm9, Patched, PatchedCalls)) >= 0;

        public static bool DetectCurrentProject()
        {
            if (!SupportsCurrentRom) return false;
            try { return Inspect(RomInfo.RomID, File.ReadAllBytes(RomInfo.Arm9Path)); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { return false; }
        }

        public static string WhyNotApplicable(string romId, byte[] arm9)
        {
            if (!Supports(romId)) return "This patch is for HeartGold and SoulSilver.";
            if (Inspect(romId, arm9)) return "The trainer custom Poké Balls patch is already applied.";
            return Only(Find(arm9, Original, OriginalCalls)) >= 0 ? null :
                "The ARM9 trainer capsule routine does not match the original code. Another patch or edit may have changed it.";
        }

        /// <summary>Validates the entire overwritten routine before changing any byte.</summary>
        public static void Apply(string romId, byte[] arm9)
        {
            if (WhyNotApplicable(romId, arm9) is string why) throw new InvalidDataException(why);
            int at = Only(Find(arm9, Original, OriginalCalls));
            int setMonData = BranchTarget(arm9, at + OriginalSetMonData);
            byte[] patch = (byte[])Patched.Clone();
            foreach (int call in PatchedCalls) WriteBranch(patch, call, setMonData - (at + call));
            patch.CopyTo(arm9, at);
        }

        public static void Apply()
        {
            if (!SupportsCurrentRom) throw new InvalidOperationException("Unsupported ROM.");
            byte[] arm9 = File.ReadAllBytes(RomInfo.Arm9Path);
            Apply(RomInfo.RomID, arm9);
            DSUtils.WriteWholeFile(RomInfo.Arm9Path, arm9);
        }

        // Thumb BL: two halfwords holding the offset from the instruction plus 4, in halfwords.
        private static int BranchTarget(byte[] code, int at)
        {
            int high = BitConverter.ToUInt16(code, at) & 0x7FF, low = BitConverter.ToUInt16(code, at + 2) & 0x7FF;
            int offset = (high << 12) | (low << 1);
            if ((offset & 0x400000) != 0) offset -= 0x800000;
            return at + 4 + offset;
        }

        private static void WriteBranch(byte[] code, int at, int distance)
        {
            int offset = distance - 4;
            ushort high = (ushort)(0xF000 | ((offset >> 12) & 0x7FF));
            ushort low = (ushort)(0xF800 | ((offset >> 1) & 0x7FF));
            BitConverter.GetBytes(high).CopyTo(code, at);
            BitConverter.GetBytes(low).CopyTo(code, at + 2);
        }
    }
}
