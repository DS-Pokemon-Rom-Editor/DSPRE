using System;
using System.Collections.Generic;

namespace DSPRE
{
    /// <summary>
    /// The Vs. Seeker rematch table (Diamond/Pearl/Platinum, English only): 240 rows in overlay 5, sparse
    /// and keyed by the encounter trainer in slot 0 rather than by row index. The address and row count
    /// are read from the lookup code, with the known offset as a checked fallback.
    /// </summary>
    public static class VsSeekerRematchTable
    {
        public const int RowCount = 240;
        public const int RowSize = RematchTable.RowSize;
        public const int RematchLevelCount = RematchTable.RematchLevelCount;
        public const ushort NoRematch = RematchTable.NoRematch;
        public const ushort ChainEnd = RematchTable.ChainEnd;

        /// <summary>What sets the story flag each rematch level waits for, levels 1 to 5, in DP and Pt.</summary>
        public static readonly string[] LevelUnlocks =
        {
            "Route 207", "Celestic Town", "Spear Pillar", "Hall of Fame", "Stark Mountain",
        };

        public static bool IsSupported =>
            !RomInfo.isHGE &&
            RomInfo.gameLanguage == RomInfo.GameLanguages.English &&
            RomInfo.vsSeekerRematchOverlayNumber >= 0 &&
            (RomInfo.gameFamily == RomInfo.GameFamilies.Plat || RomInfo.gameFamily == RomInfo.GameFamilies.DP);

        private static RematchTable.Descriptor Descriptor => new RematchTable.Descriptor
        {
            Name = "Vs. Seeker rematch table",
            OverlayNumber = RomInfo.vsSeekerRematchOverlayNumber,
            FallbackOffset = RomInfo.vsSeekerRematchTableOffset,
            FixedRowCount = RowCount,
            DeriveOffset = false,
            PointerWordOffsets = RomInfo.vsSeekerRematchPointerOffsets,
            RowCountCompareOffset = RomInfo.vsSeekerRematchRowCompareOffset,
            ValidateRows = true,
        };

        /// <summary>
        /// Row layouts the game handles badly, as short sentences. Every level unlocks in DP and Pt, so
        /// a skip the level search stops on is handed back as trainer 0xFFFF.
        /// </summary>
        public static List<string> Problems(IReadOnlyList<RematchTable.Row> rows, int rowIndex)
        {
            List<string> problems = new List<string>();
            if (rows == null || rowIndex < 0 || rowIndex >= rows.Count) return problems;

            RematchTable.Row row = rows[rowIndex];
            if (row.IsEmpty) return problems;

            // pokeplatinum VsSeeker_GetCurrentLevelForRematchData: an end returns the slot before it, and
            // running off slot 5 returns slot 5.
            int end = Array.IndexOf(row.Ids, ChainEnd, 1);
            if (end < 0 && row.Ids[RematchLevelCount] == NoRematch)
                problems.Add($"Rematch {RematchLevelCount} is a skip with no end after it, so the game can pick 0xFFFF as a trainer. Put a trainer or the end there.");
            else if (end >= 2 && row.Ids[end - 1] == NoRematch)
                problems.Add($"Rematch {end - 1} is a skip right before the end, so the game can pick 0xFFFF as a trainer. End the chain there instead.");

            for (int other = 0; other < rowIndex; other++)
            {
                if (rows[other].BaseTrainerId != row.BaseTrainerId) continue;
                problems.Add($"Row {other} has the same encounter trainer and is found first, so this row is never used.");
                break;
            }
            return problems;
        }

        public static RematchTable.Location Resolve(out string error)
        {
            error = null;
            if (!IsSupported)
            {
                error = "The Vs. Seeker rematch table isn't supported for this game and language.";
                return null;
            }
            return RematchTable.Resolve(Descriptor, out error);
        }

        public static List<RematchTable.Row> ReadAll(out RematchTable.Location location, out string error)
        {
            location = Resolve(out error);
            return location == null ? new List<RematchTable.Row>() : RematchTable.ReadAll(location);
        }

        public static List<RematchTable.Row> ReadAll() => ReadAll(out _, out _);

        public static bool WriteRow(int rowIndex, RematchTable.Row row, out string error)
        {
            RematchTable.Location location = Resolve(out error);
            if (location == null)
            {
                error ??= "The Vs. Seeker rematch table couldn't be located.";
                return false;
            }
            return RematchTable.WriteRow(location, rowIndex, row, out error);
        }
    }
}
