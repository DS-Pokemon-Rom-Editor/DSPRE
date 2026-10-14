using System;
using System.Collections.ObjectModel;
using System.IO;

namespace DSPRE
{
    /// <summary>
    /// MrHam88's Trainer Custom Pokéballs v1.0.0: an in-place SetTrMonCapsule replacement.
    /// The original routine and SetMonData addresses match US HG/SS, EU English HG and Italian HG.
    /// </summary>
    public static class TrainerCustomPokeballsPatch
    {
        public const int Offset = 0x72A44;
        public const int Length = 0x54;

        private static readonly byte[] Original = Convert.FromHexString(
            "70B588B0051C0C1C002D02D108B0002070BDB920111C94F715FE01216D1E019102A9" +
            "009118232A1C00215A43061C94F7DBFE201CA22101AAFCF7E0F8201CAB2102AAFCF7" +
            "DBF8301C94F73EFE012008B070BD0000");

        // Exact armips output, including zero padding up to sub_02072A98.
        private static readonly byte[] Patched = BuildPatch();
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

        public static bool Supports(string romId) => romId is "IPKE" or "IPGE" or "IPKI";
        public static bool SupportsCurrentRom => !RomInfo.isHGE && Supports(RomInfo.romID);

        private static bool Matches(byte[] arm9, byte[] expected) => arm9 != null &&
            arm9.Length >= Offset + Length && arm9.AsSpan(Offset, Length).SequenceEqual(expected);

        public static bool Inspect(string romId, byte[] arm9) => Supports(romId) && Matches(arm9, Patched);

        public static bool DetectCurrentProject()
        {
            if (!SupportsCurrentRom) return false;
            try { return Inspect(RomInfo.romID, File.ReadAllBytes(RomInfo.arm9Path)); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { return false; }
        }

        public static string WhyNotApplicable(string romId, byte[] arm9)
        {
            if (!Supports(romId)) return "Supported versions: US HeartGold/SoulSilver, EU English HeartGold and Italian HeartGold.";
            if (Inspect(romId, arm9)) return "The trainer custom Pokéballs patch is already applied.";
            return Matches(arm9, Original) ? null :
                "The ARM9 capsule routine at 0x72A44 does not match the original code. Another patch or edit may have changed it.";
        }

        /// <summary>Validates the entire overwritten routine before changing any byte.</summary>
        public static void Apply(string romId, byte[] arm9)
        {
            if (WhyNotApplicable(romId, arm9) is string why) throw new InvalidDataException(why);
            Patched.CopyTo(arm9, Offset);
        }

        public static void Apply()
        {
            if (!SupportsCurrentRom) throw new InvalidOperationException("Unsupported ROM.");
            byte[] arm9 = File.ReadAllBytes(RomInfo.arm9Path);
            Apply(RomInfo.romID, arm9);
            File.WriteAllBytes(RomInfo.arm9Path, arm9);
        }
    }
}
