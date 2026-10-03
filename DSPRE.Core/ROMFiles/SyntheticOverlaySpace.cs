using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// Free space in the synthetic overlay for tables DSPRE moves there. Each block starts with a 12-byte ASCII marker
    /// and keeps its length at +0x10, so every allocator can see the others' blocks.
    /// </summary>
    public static class SyntheticOverlaySpace
    {
        public const int HeaderSize = 0x20;

        /// <summary>Markers of every block DSPRE places here. Add a new block's marker before allocating it.</summary>
        public static readonly string[] BlockMarkers = { "MARTEXPANDV1", "BPSHOPEXPV1\0", "TYPECHARTXP1", "SWARMTABLEX1", VsIntroTables.ClassTableMarker };

        /// <summary>Whether the ARM9 expansion is applied and its overlay is large enough to hold tables.</summary>
        public static bool Available()
        {
            DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.synthOverlay });
            return (RomPatchState.flag_arm9Expanded || PatchToolboxLogic.CheckFilesArm9ExpansionApplied())
                && File.Exists(Filesystem.expArmPath)
                && new FileInfo(Filesystem.expArmPath).Length >= 0x16000;
        }

        /// <summary>Byte ranges already owned by a marked block.</summary>
        public static List<(long Start, long End)> Blocks(byte[] data, string onlyMarker = null)
        {
            var ranges = new List<(long, long)>();
            foreach (string marker in onlyMarker != null ? new[] { onlyMarker } : BlockMarkers)
            {
                foreach (int hit in DSUtils.SearchBytes(data, Encoding.ASCII.GetBytes(marker)))
                {
                    if (hit + HeaderSize > data.Length) continue;
                    uint length = BitConverter.ToUInt32(data, hit + 0x10);
                    if (length >= HeaderSize && (long)hit + length <= data.Length)
                        ranges.Add((hit, hit + length));
                }
            }
            return ranges;
        }

        /// <summary>Everything an allocator must not touch: marked blocks and the overworld expansion.</summary>
        public static List<(long Start, long End)> Reserved(byte[] data)
        {
            var ranges = Blocks(data);
            OverworldSpriteTableExpansion.Detect();
            var ow = OverworldSpriteTableExpansion.GetReservedByteRange();
            if (ow.HasValue) ranges.Add(ow.Value);
            // A chart moved here by another patch has no marker.
            var chart = TypeChart.UnmarkedRangeInExpansion();
            if (chart.HasValue) ranges.Add(chart.Value);
            ranges.AddRange(PlatPatchesBlocks(data));
            ranges.AddRange(TrainerClassTableExpansion.MovedTableRanges());
            // PlatPatches installs item payloads at fixed offsets and refuses when they are occupied.
            if (RomInfo.gameFamily == RomInfo.GameFamilies.Plat && data.Length > PlatPatchesFixedStart)
                ranges.Add((PlatPatchesFixedStart, Math.Min(PlatPatchesFixedEnd, data.Length)));
            return ranges;
        }

        public const int PlatPatchesFixedStart = 0x10000, PlatPatchesFixedEnd = 0x16000;

        // PlatPatches' blocks store no length and their unused rows read as zero, so each is reserved by its full layout.
        private static readonly (string Marker, int Length)[] PlatPatchesLayouts =
        {
            ("EXTRATMSV1", 0x608 + 4 * 1024),
            ("ITEMEXPV2", -1),
            ("ITEMEXPV1", 0x1000),
        };

        public static IEnumerable<(long Start, long End)> PlatPatchesBlocks(byte[] data)
        {
            foreach (var (marker, fixedLength) in PlatPatchesLayouts)
                foreach (int hit in DSUtils.SearchBytes(data, Encoding.ASCII.GetBytes(marker)))
                {
                    long length = fixedLength;
                    if (length < 0)
                        length = 0x298 + 8L * Math.Max((ushort)128, hit + 0x296 <= data.Length ? BitConverter.ToUInt16(data, hit + 0x294) : (ushort)128);
                    yield return (hit, Math.Min(hit + length, data.Length));
                }
        }

        /// <summary>The first all-zero, aligned run of <paramref name="length"/> bytes outside the reserved ranges, or -1.</summary>
        public static int FindFree(byte[] data, int length, int alignment, IReadOnlyList<(long Start, long End)> reserved)
        {
            for (int offset = 0; offset + length <= data.Length; offset += alignment)
            {
                if (reserved.Any(r => offset + length > r.Start && offset < r.End)) continue;
                bool clear = true;
                for (int i = 0; i < length; i++)
                    if (data[offset + i] != 0) { clear = false; break; }
                if (clear) return offset;
            }
            return -1;
        }
    }
}
