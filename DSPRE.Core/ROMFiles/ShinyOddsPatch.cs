using System;
using System.IO;
using System.Linq;
using System.Text;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// Eclipse's shiny odds patch for English HGSS. It rerolls wild, gift, roamer and egg Pokémon for extra shiny chances,
    /// and every shiny stays legal.
    /// </summary>
    public static class ShinyOddsPatch
    {
        public const string Marker = "SHINYODDS_V1";
        public const int DefaultOdds = 4096, MinOdds = 1, MaxOdds = RetailOdds - 1, RetailOdds = 8192;

        // Offsets of ExtraShinyOdds and of ShinyChance_Vars, which the game writes at runtime.
        private const int ExtraOddsAt = 0x430, VarsAt = 0x448, VarsSize = 44;
        private const string Start = "ShinyChance_start", End = "ShinyChance_end";

        private static PrebuiltPatch _patch;
        private static PrebuiltPatch Patch => _patch ??= PrebuiltPatch.Load("DSPRE.Resources.ROMToolboxDB.ShinyOdds.bin", "SHOD");

        public static int BlockLength => (SyntheticOverlaySpace.HeaderSize + Patch.PayloadSize + 3) & ~3;

        public static bool SupportsCurrentRom => RomInfo.RomID == "IPKE" || RomInfo.RomID == "IPGE";

        // The source turns "1 in N" into the odds of an extra roll on top of the retail 1 in 8192.
        private static uint ExtraOdds(int odds) => (uint)(RetailOdds * odds / (RetailOdds - odds));

        private static int? OddsFrom(uint extra)
        {
            int low = MinOdds, high = MaxOdds;
            while (low < high)
            {
                int mid = (low + high) / 2;
                if (ExtraOdds(mid) < extra) low = mid + 1; else high = mid;
            }
            return ExtraOdds(low) == extra ? low : null;
        }

        /// <summary>Where the payload starts in the synthetic overlay, or -1 when the patch isn't installed.</summary>
        public static int InstalledOffset()
        {
            if (!SupportsCurrentRom || !File.Exists(Filesystem.ExpArmPath)) return -1;
            try
            {
                if (Patch.InstalledAddress() is not uint address || address < RomInfo.SynthOverlayLoadAddress) return -1;
                long offset = address - RomInfo.SynthOverlayLoadAddress;
                byte[] synth = File.ReadAllBytes(Filesystem.ExpArmPath);
                if (offset + Patch.PayloadSize > synth.Length) return -1;
                byte[] expected = Patch.BuildPayload(address);
                byte[] installed = synth.AsSpan((int)offset, expected.Length).ToArray();
                foreach ((int at, int length) in new[] { (ExtraOddsAt, 4), (VarsAt, VarsSize) })
                {
                    Array.Clear(expected, at, length);
                    Array.Clear(installed, at, length);
                }
                return installed.AsSpan().SequenceEqual(expected) ? (int)offset : -1;
            }
            catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is InvalidOperationException) { return -1; }
        }

        public static bool IsApplied => InstalledOffset() >= 0;

        /// <summary>Why the patch can't go in now, or null.</summary>
        public static string WhyNotApplicable()
        {
            if (!SupportsCurrentRom) return "The shiny odds patch is for English HeartGold and SoulSilver.";
            if (!SyntheticOverlaySpace.Available()) return "Apply the ARM9 expansion first.";
            if (ShinyCheck.Values() != ShinyCheck.Retail) return "Another patch has changed this ROM's shiny check. This patch needs the original check.";
            if (Patch.ChangedHook() is string hook) return $"Another patch or edit changed {hook}.";
            return null;
        }

        /// <summary>Writes the block at <paramref name="blockOffset"/> with "1 in <paramref name="odds"/>", then every hook.</summary>
        public static void Apply(int blockOffset, int odds)
        {
            uint address = RomInfo.SynthOverlayLoadAddress + (uint)(blockOffset + SyntheticOverlaySpace.HeaderSize);
            byte[] block = new byte[BlockLength];
            SyntheticOverlaySpace.Header(Marker, BlockLength).CopyTo(block, 0);
            Patch.BuildPayload(address).CopyTo(block, SyntheticOverlaySpace.HeaderSize);
            BitConverter.GetBytes(ExtraOdds(odds)).CopyTo(block, SyntheticOverlaySpace.HeaderSize + ExtraOddsAt);
            DSUtils.WriteToFile(Filesystem.ExpArmPath, block, (uint)blockOffset);
            Patch.WriteHooks(address);
        }

        /// <summary>The installed "1 in N", or null when the patch isn't installed or its odds can't be read.</summary>
        public static int? ReadOdds()
        {
            int offset = InstalledOffset();
            if (offset < 0) return null;
            byte[] word = DSUtils.ReadFromFile(Filesystem.ExpArmPath, offset + ExtraOddsAt, 4);
            return OddsFrom(BitConverter.ToUInt32(word, 0));
        }

        public static void WriteOdds(int odds)
        {
            if (odds < MinOdds || odds > MaxOdds) throw new ArgumentOutOfRangeException(nameof(odds));
            int offset = InstalledOffset();
            if (offset < 0) throw new InvalidOperationException("The shiny odds patch isn't installed.");
            DSUtils.WriteToFile(Filesystem.ExpArmPath, BitConverter.GetBytes(ExtraOdds(odds)), (uint)(offset + ExtraOddsAt));
        }

        /// <summary>The range of a copy installed by Eclipse's script, which has no DSPRE header.</summary>
        public static (long Start, long End)? HandPlacedRange(byte[] synth)
        {
            int start = DSUtils.SearchBytes(synth, Encoding.ASCII.GetBytes(Start)).DefaultIfEmpty(-1).First();
            if (start < 0) return null;
            int end = DSUtils.SearchBytes(synth, Encoding.ASCII.GetBytes(End)).Where(e => e > start).DefaultIfEmpty(-1).First();
            return end < 0 ? null : (start, end + End.Length);
        }
    }
}
