using System;
using System.Collections.Generic;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// Babies that need an incense: without <see cref="Row.Item"/> on either parent, <see cref="Row.Baby"/> hatches as
    /// <see cref="Row.Fallback"/>. The game uses the first row naming the baby, and the row count is compiled in.
    /// </summary>
    public class IncenseBreedingTable
    {
        public const int RowCount = 9, RowSize = 6, Size = RowCount * RowSize;

        public class Row
        {
            public ushort Baby, Item, Fallback;
        }

        public List<Row> Rows { get; } = new List<Row>();

        public IncenseBreedingTable(byte[] data)
        {
            if (data == null || data.Length < Size) throw new ArgumentException($"The incense table is {Size} bytes.");
            for (int r = 0; r < RowCount; r++)
                Rows.Add(new Row
                {
                    Baby = BitConverter.ToUInt16(data, r * RowSize),
                    Item = BitConverter.ToUInt16(data, r * RowSize + 2),
                    Fallback = BitConverter.ToUInt16(data, r * RowSize + 4),
                });
        }

        public byte[] ToBytes()
        {
            byte[] data = new byte[Size];
            for (int r = 0; r < RowCount; r++)
            {
                BitConverter.GetBytes(Rows[r].Baby).CopyTo(data, r * RowSize);
                BitConverter.GetBytes(Rows[r].Item).CopyTo(data, r * RowSize + 2);
                BitConverter.GetBytes(Rows[r].Fallback).CopyTo(data, r * RowSize + 4);
            }
            return data;
        }

        /// <summary>Why the table can't be saved, or null.</summary>
        public string Problem(int speciesCount, int itemCount)
        {
            HashSet<ushort> seen = new HashSet<ushort>();
            for (int r = 0; r < RowCount; r++)
            {
                Row row = Rows[r];
                if (row.Baby >= speciesCount || row.Fallback >= speciesCount) return $"Row {r + 1}: pick a Pokémon from the list.";
                if (row.Item >= itemCount) return $"Row {r + 1}: pick an item from the list.";
                if (row.Baby != 0 && !seen.Add(row.Baby)) return $"Row {r + 1}: this baby already has a row above; the game only uses the first.";
            }
            return null;
        }

        public static string WhyNot() => GameTableFile.WhyNot(RomInfo.GameTable.IncenseBabies, Size);

        public static IncenseBreedingTable Load() => new IncenseBreedingTable(GameTableFile.Read(RomInfo.GameTable.IncenseBabies, Size));

        public void Save(int speciesCount, int itemCount)
        {
            if (Problem(speciesCount, itemCount) is string p) throw new InvalidOperationException(p);
            GameTableFile.Write(RomInfo.GameTable.IncenseBabies, ToBytes());
        }
    }
}
