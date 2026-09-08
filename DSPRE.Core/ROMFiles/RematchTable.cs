using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DSPRE
{
    /// <summary>
    /// Rows of six little-endian u16, shared by the Vs. Seeker (DP/Pt) and Pokégear (HGSS) rematch
    /// tables. Slot 0 is the lookup trainer and the battle used before any rematch, slots 1 to 5 are the
    /// rematch levels.
    /// </summary>
    public static class RematchTable
    {
        public const int SlotCount = 6;
        public const int RowSize = SlotCount * 2;
        public const int RematchLevelCount = SlotCount - 1;

        /// <summary>Replays the previous real battle.</summary>
        public const ushort NoRematch = 0xFFFF;

        /// <summary>No further rematch.</summary>
        public const ushort ChainEnd = 0x0000;

        public sealed class Row
        {
            public ushort[] Ids = new ushort[SlotCount];

            public ushort BaseTrainerId
            {
                get => Ids[0];
                set => Ids[0] = value;
            }

            /// <summary>Levels 0 to 4, numbered 1 to 5 in game.</summary>
            public ushort Rematch(int level) => Ids[level + 1];

            public void SetRematch(int level, ushort trainerId) => Ids[level + 1] = trainerId;

            public bool IsEmpty => Ids.All(id => id == 0);

            public Row Copy() => new Row { Ids = (ushort[])Ids.Clone() };
        }

        /// <summary>Where a game keeps its table and how to find it.</summary>
        public sealed class Descriptor
        {
            public string Name;
            public int OverlayNumber = -1;

            /// <summary>Used when the address cannot be found in the overlay.</summary>
            public long FallbackOffset;

            /// <summary>Zero derives the count from the overlay.</summary>
            public int FixedRowCount;

            /// <summary>Read the address from the overlay's literal pool.</summary>
            public bool DeriveOffset;
        }

        /// <summary>Where the table turned out to be.</summary>
        public sealed class Location
        {
            public int OverlayNumber;
            public string Path;
            public long Offset;
            public int RowCount;

            /// <summary>False when the fallback address was used.</summary>
            public bool FoundInOverlay;

            public string Description =>
                $"overlay {OverlayNumber}, offset 0x{Offset:X}, {RowCount} rows " +
                (FoundInOverlay ? "(address read from the overlay)" : "(address from the built-in fallback)");
        }

        private const int RowsScored = 16;

        /// <summary>Locates the table in the loaded ROM.</summary>
        public static Location Resolve(Descriptor descriptor, out string error)
        {
            error = null;

            if (descriptor == null || descriptor.OverlayNumber < 0)
            {
                error = "This game has no known rematch table.";
                return null;
            }

            int overlayNumber = descriptor.OverlayNumber;
            string path = OverlayUtils.GetPath(overlayNumber);
            if (!File.Exists(path))
            {
                error = $"Overlay {overlayNumber} is missing from this project.";
                return null;
            }

            // An overlay with bss is smaller on disk than its run size, so the flag decides, not the size.
            if (!RomInfo.IsDsRomProject &&
                OverlayUtils.OverlayTable.IsDefaultCompressed(overlayNumber) &&
                OverlayUtils.IsCompressed(overlayNumber))
            {
                error = $"Overlay {overlayNumber} is still compressed. Convert this project to ds-rom format first.";
                return null;
            }

            byte[] data;
            try { data = File.ReadAllBytes(path); }
            catch (Exception ex)
            {
                error = $"Overlay {overlayNumber} couldn't be read: {ex.Message}";
                return null;
            }

            uint ramBase = OverlayUtils.OverlayTable.GetRAMAddress(overlayNumber);
            int dataEnd = DataRegionEnd(overlayNumber, ramBase, data.Length);

            long offset = descriptor.FallbackOffset;
            bool foundInOverlay = false;

            if (descriptor.DeriveOffset && ramBase > 0)
            {
                long found = FindTableOffset(data, ramBase, dataEnd);
                if (found >= 0)
                {
                    offset = found;
                    foundInOverlay = true;
                }
            }

            if (offset <= 0 || offset + RowSize > data.Length)
            {
                error = $"The {descriptor.Name} isn't where this ROM keeps it, and it couldn't be found " +
                    $"in overlay {overlayNumber}.";
                return null;
            }

            long available = Math.Min(dataEnd, data.Length) - offset;
            int rowCount = descriptor.FixedRowCount > 0
                ? descriptor.FixedRowCount
                : (int)(available / RowSize);
            rowCount = (int)Math.Min(rowCount, (data.Length - offset) / RowSize);

            if (rowCount <= 0)
            {
                error = $"The {descriptor.Name} has no readable rows in overlay {overlayNumber}.";
                return null;
            }

            return new Location
            {
                OverlayNumber = overlayNumber,
                Path = path,
                Offset = offset,
                RowCount = rowCount,
                FoundInOverlay = foundInOverlay,
            };
        }

        public static List<Row> ReadAll(Location location)
        {
            var rows = new List<Row>();
            if (location == null) return rows;

            byte[] data = File.ReadAllBytes(location.Path);
            for (int r = 0; r < location.RowCount; r++)
            {
                long at = location.Offset + (long)r * RowSize;
                if (at + RowSize > data.Length) break;

                var row = new Row();
                for (int slot = 0; slot < SlotCount; slot++)
                {
                    row.Ids[slot] = BitConverter.ToUInt16(data, (int)(at + slot * 2));
                }
                rows.Add(row);
            }
            return rows;
        }

        public static bool WriteRow(Location location, int rowIndex, Row row, out string error)
        {
            error = null;

            if (location == null)
            {
                error = "The rematch table hasn't been located in this ROM.";
                return false;
            }
            if (rowIndex < 0 || rowIndex >= location.RowCount)
            {
                error = "Row index out of range.";
                return false;
            }
            if (row?.Ids == null || row.Ids.Length != SlotCount)
            {
                error = "The row does not have the six entries the game expects.";
                return false;
            }

            byte[] buffer = new byte[RowSize];
            for (int slot = 0; slot < SlotCount; slot++)
            {
                Array.Copy(BitConverter.GetBytes(row.Ids[slot]), 0, buffer, slot * 2, 2);
            }

            DSUtils.WriteToFile(location.Path, buffer, (uint)(location.Offset + (long)rowIndex * RowSize));
            return true;
        }

        /// <summary>Overlay data ends where the static initialiser list starts.</summary>
        private static int DataRegionEnd(int overlayNumber, uint ramBase, int fileLength)
        {
            uint ctorStart = OverlayUtils.OverlayTable.GetStaticInitStart(overlayNumber);
            if (ctorStart > ramBase)
            {
                long end = ctorStart - ramBase;
                if (end > 0 && end <= fileLength) return (int)end;
            }
            return fileLength;
        }

        /// <summary>
        /// Scores every word pointing back inside the overlay as a table address. -1 when none scores
        /// well enough.
        /// </summary>
        private static long FindTableOffset(byte[] data, uint ramBase, int dataEnd)
        {
            var candidates = new HashSet<long>();
            for (int at = 0; at + 4 <= data.Length; at += 4)
            {
                uint word = BitConverter.ToUInt32(data, at);
                if (word <= ramBase) continue;

                long target = word - ramBase;
                if (target % 4 != 0 || target + RowSize > dataEnd) continue;
                candidates.Add(target);
            }

            long best = -1;
            int bestScore = 0;
            int bestPossible = 0;

            foreach (long candidate in candidates)
            {
                int score = ScoreAsTable(data, candidate, dataEnd, out int possible);
                if (score <= bestScore) continue;

                bestScore = score;
                bestPossible = possible;
                best = candidate;
            }

            // Two thirds of a perfect score, so an edited table still resolves.
            return bestPossible > 0 && bestScore * 3 >= bestPossible * 2 ? best : -1;
        }

        private static int ScoreAsTable(byte[] data, long offset, int dataEnd, out int possible)
        {
            possible = 0;

            int rows = (int)((Math.Min(dataEnd, data.Length) - offset) / RowSize);
            if (rows <= 0) return 0;
            rows = Math.Min(rows, RowsScored);

            int score = 0;
            for (int r = 0; r < rows; r++)
            {
                long at = offset + (long)r * RowSize;
                ushort lookup = BitConverter.ToUInt16(data, (int)at);
                ushort first = BitConverter.ToUInt16(data, (int)at + 2);
                ushort last = BitConverter.ToUInt16(data, (int)(at + (SlotCount - 1) * 2));

                // A row nothing can look up is not a row.
                if (lookup == 0) return 0;

                possible += 3;
                if (lookup == first) score += 2;
                if (last == ChainEnd || last == NoRematch) score += 1;
            }
            return score;
        }
    }
}
