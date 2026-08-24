using System;
using System.Collections.Generic;
using System.IO;

namespace DSPRE
{
    /// <summary>
    /// Reads/writes the Vs. Seeker Trainer Rematch table (Diamond/Pearl/Platinum, English only).
    /// Fixed vanilla table in Overlay 5, 240 rows of 12 bytes (6 x u16 LE): the "encounter" trainer ID
    /// this row belongs to, followed by 5 rematch-level trainer IDs (A-E, each unlocked as the player
    /// clears more of the story; which specific story beat maps to which lettered level isn't confirmed
    /// here, so callers should just label them A-E). Rows are sparse and keyed by the stored encounter
    /// trainer ID, not by row index. 0xFFFF in a rematch slot means no rematch at that level, 0x0000
    /// means the chain ends there. Offsets and structure from issue #241 (pret/pokeplatinum decomp +
    /// community research), not guessed.
    /// </summary>
    public static class VsSeekerRematchTable
    {
        public const int RowCount = 240;
        public const int RowSize = 12;
        public const int RematchLevelCount = 5;
        public const ushort NoRematch = 0xFFFF;
        public const ushort ChainEnd = 0x0000;

        public struct Row
        {
            public ushort EncounterTrainerId;
            public ushort[] RematchTrainerIds; // length RematchLevelCount, levels A..E
        }

        public static bool IsSupported =>
            !RomInfo.isHGE &&
            RomInfo.gameLanguage == RomInfo.GameLanguages.English &&
            (RomInfo.gameFamily == RomInfo.GameFamilies.Plat || RomInfo.gameFamily == RomInfo.GameFamilies.DP);

        private static long TableOffset =>
            RomInfo.gameFamily == RomInfo.GameFamilies.Plat ? 0x280C8 : 0x1F43C;

        private static string TablePath => OverlayUtils.GetPath(5);

        public static List<Row> ReadAll()
        {
            var rows = new List<Row>();
            if (!IsSupported) return rows;

            byte[] data = File.ReadAllBytes(TablePath);
            for (int r = 0; r < RowCount; r++)
            {
                long off = TableOffset + (long)r * RowSize;
                if (off + RowSize > data.Length) break;

                var row = new Row { RematchTrainerIds = new ushort[RematchLevelCount] };
                row.EncounterTrainerId = BitConverter.ToUInt16(data, (int)off);
                for (int s = 0; s < RematchLevelCount; s++)
                {
                    row.RematchTrainerIds[s] = BitConverter.ToUInt16(data, (int)(off + 2 + s * 2));
                }
                rows.Add(row);
            }
            return rows;
        }

        public static bool WriteRow(int rowIndex, Row row, out string error)
        {
            error = null;
            if (!IsSupported)
            {
                error = "The Vs. Seeker rematch table isn't supported for this game/language.";
                return false;
            }
            if (rowIndex < 0 || rowIndex >= RowCount)
            {
                error = "Row index out of range.";
                return false;
            }

            byte[] buffer = new byte[RowSize];
            Array.Copy(BitConverter.GetBytes(row.EncounterTrainerId), 0, buffer, 0, 2);
            for (int s = 0; s < RematchLevelCount; s++)
            {
                Array.Copy(BitConverter.GetBytes(row.RematchTrainerIds[s]), 0, buffer, 2 + s * 2, 2);
            }

            long off = TableOffset + (long)rowIndex * RowSize;
            DSUtils.WriteToFile(TablePath, buffer, (uint)off);
            return true;
        }
    }
}
