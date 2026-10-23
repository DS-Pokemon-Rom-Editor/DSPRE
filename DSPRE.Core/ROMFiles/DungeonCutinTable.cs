using System;
using System.Collections.Generic;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// HeartGold's 25 location splash screens, each with tiles, colours and arrangement for four times of day. Read
    /// here so every editor uses one field order.
    /// </summary>
    public static class DungeonCutinTable
    {
        /// <summary>Rows in the retail table.</summary>
        public const int RowCount = 25;

        public enum TimeOfDay { Morning, Noon, Evening, Night }

        public sealed class Row
        {
            public int Number;                 // 1-based, for showing
            public int HeaderIndex;            // which zone this is for
            public int WipeType;
            public int NameMessageId;
            /// <summary>Colours, tiles and arrangement for each of the four times of day.</summary>
            public (int Palette, int Tiles, int Screen)[] Art = new (int, int, int)[4];

            public (int Palette, int Tiles, int Screen) At(TimeOfDay t) => Art[(int)t];
        }

        /// <summary>Where the table starts in arm9, as the code's pointer says, or -1 when this game has none.</summary>
        public static long TableOffset()
        {
            if (RomInfo.DungeonCutinTableOffsetToRAMAddress == 0) return -1;
            uint ram = BitConverter.ToUInt32(ARM9.ReadBytes(RomInfo.DungeonCutinTableOffsetToRAMAddress, 4), 0);
            return ram >= ARM9.Address ? ram - ARM9.Address : -1;
        }

        /// <summary>Reads the table, or an empty list when this game has none. Only HeartGold and
        /// SoulSilver do.</summary>
        public static List<Row> Read()
        {
            List<Row> rows = new List<Row>();
            try
            {
                long at = TableOffset();
                if (at < 0) return rows;

                using ARM9.Reader reader = new ARM9.Reader(at);
                for (int i = 0; i < RowCount; i++)
                {
                    Row row = new Row
                    {
                        Number = i + 1,
                        HeaderIndex = reader.ReadInt32(),
                        WipeType = reader.ReadInt32(),
                    };
                    for (int t = 0; t < 4; t++)
                        row.Art[t] = (reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
                    row.NameMessageId = reader.ReadInt32();
                    rows.Add(row);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error("DungeonCutinTable.Read: " + ex.Message);
                rows.Clear();
            }
            return rows;
        }

        /// <summary>The drawing one location uses at a given time of day, for an editor handing it over.</summary>
        public static int DrawingFor(int rowNumber, TimeOfDay when)
        {
            foreach (Row r in Read())
                if (r.Number == rowNumber) return r.At(when).Tiles;
            return -1;
        }
    }
}
