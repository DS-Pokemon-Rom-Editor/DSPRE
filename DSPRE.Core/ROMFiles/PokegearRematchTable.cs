using System;
using System.Collections.Generic;

namespace DSPRE
{
    /// <summary>
    /// The HeartGold/SoulSilver rematch table, one row per trainer who can phone the player. The address
    /// comes from the overlay's own code and the row count from where its data ends.
    /// </summary>
    public static class PokegearRematchTable
    {
        public const int RowSize = RematchTable.RowSize;
        public const int RematchLevelCount = RematchTable.RematchLevelCount;
        public const ushort NoRematch = RematchTable.NoRematch;
        public const ushort ChainEnd = RematchTable.ChainEnd;

        /// <summary>
        /// What sets the story flag each rematch level waits for, levels 1 to 5. The game falls back one
        /// level when a flag is clear, and nothing ever sets the flags for levels 1 and 5.
        /// </summary>
        public static readonly string[] LevelUnlocks =
        {
            "never unlocked", "after the Radio Tower", "after the Hall of Fame", "after beating Blue", "never unlocked",
        };

        /// <summary>Row layouts the game handles badly, as short sentences. Empty when the row is fine.</summary>
        public static List<string> Problems(IReadOnlyList<RematchTable.Row> rows, int rowIndex)
        {
            var problems = new List<string>();
            if (rows == null || rowIndex < 0 || rowIndex >= rows.Count) return problems;

            RematchTable.Row row = rows[rowIndex];
            if (row.IsEmpty) return problems;

            ushort first = row.Rematch(0);
            if (first != ChainEnd && first != NoRematch && first != row.BaseTrainerId)
                problems.Add("Rematch 1 is never unlocked, so this row stays on the base battle. Use the base trainer or skip it.");

            ushort last = row.Rematch(RematchLevelCount - 1);
            if (last != ChainEnd && last != NoRematch)
                problems.Add("Rematch 5 is never unlocked.");

            // The search stops at the first end and returns the slot before it. A skip there is returned
            // as a trainer once its level unlocks; levels 1 and 5 never unlock, so they fall back instead.
            int end = Array.IndexOf(row.Ids, ChainEnd, 1);
            if (end >= 3 && end - 1 <= RematchLevelCount - 1 && row.Ids[end - 1] == NoRematch)
                problems.Add($"Rematch {end - 1} is a skip right before the end, so the game can pick 0xFFFF as a trainer. End the chain there instead.");

            for (int other = 0; other < rowIndex; other++)
            {
                if (rows[other].BaseTrainerId != row.BaseTrainerId) continue;
                problems.Add($"Row {other} has the same base trainer and is found first, so this row is never used.");
                break;
            }
            return problems;
        }

        public static bool IsSupported =>
            RomInfo.gameFamily == RomInfo.GameFamilies.HGSS &&
            RomInfo.pokegearRematchOverlayNumber >= 0;

        private static RematchTable.Descriptor Descriptor => new RematchTable.Descriptor
        {
            Name = "Pokégear rematch table",
            OverlayNumber = RomInfo.pokegearRematchOverlayNumber,
            FallbackOffset = RomInfo.pokegearRematchFallbackTableOffset,
            FixedRowCount = 0,
            DeriveOffset = true,
        };

        public static RematchTable.Location Resolve(out string error)
        {
            error = null;
            if (!IsSupported)
            {
                error = "Only HeartGold and SoulSilver have a Pokégear rematch table.";
                return null;
            }
            return RematchTable.Resolve(Descriptor, out error);
        }

        public static List<RematchTable.Row> ReadAll(out RematchTable.Location location, out string error)
        {
            location = Resolve(out error);
            return location == null ? new List<RematchTable.Row>() : RematchTable.ReadAll(location);
        }

        public static List<RematchTable.Row> ReadAll()
        {
            return ReadAll(out _, out _);
        }

        public static bool WriteRow(RematchTable.Location location, int rowIndex, RematchTable.Row row,
            out string error)
        {
            error = null;
            if (location == null)
            {
                location = Resolve(out error);
                if (location == null)
                {
                    error ??= "The Pokégear rematch table couldn't be located.";
                    return false;
                }
            }
            return RematchTable.WriteRow(location, rowIndex, row, out error);
        }
    }
}
