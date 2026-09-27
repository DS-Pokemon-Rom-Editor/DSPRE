using System;
using System.Collections.Generic;
using System.Linq;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// What the Underground's walls hold: 85 rows, each with four weights picked by trainer ID parity and National Dex.
    /// Rocks must stay last and the row count is compiled in, so only the weights are editable.
    /// </summary>
    public class MiningTable
    {
        public const int RowCount = 85, RowSize = 20, Size = RowCount * RowSize, FirstRockId = 60, SphereCount = 10, ItemListCount = 49;

        public static readonly string[] Columns = { "Odd trainer ID", "Even trainer ID", "Odd ID, National Dex", "Even ID, National Dex" };

        private static readonly string[] Spheres =
        {
            "Small Prism Sphere", "Small Pale Sphere", "Small Red Sphere", "Small Blue Sphere", "Small Green Sphere",
            "Large Prism Sphere", "Large Pale Sphere", "Large Red Sphere", "Large Blue Sphere", "Large Green Sphere",
        };

        public sealed class Row
        {
            public int MiningId;
            public ushort BagItem;           // 0 for spheres
            public ushort[] Weights = new ushort[4];
            internal byte[] Raw;
        }

        /// <summary>The treasure rows, in the game's order; rotated shapes of one treasure are separate rows.</summary>
        public List<Row> Treasures { get; } = new List<Row>();
        private byte[] _all;

        public static string WhyNot()
        {
            if (RomInfo.gameFamily == RomInfo.GameFamilies.HGSS) return "HeartGold and SoulSilver have no Underground.";
            if (GameTableFile.WhyNot(RomInfo.GameTable.MiningTreasures, Size) is string why) return why;
            return GameTableFile.WhyNot(RomInfo.GameTable.MiningItems, ItemListCount * 2);
        }

        public static MiningTable Load()
        {
            var table = new MiningTable { _all = GameTableFile.Read(RomInfo.GameTable.MiningTreasures, Size) };
            byte[] items = GameTableFile.Read(RomInfo.GameTable.MiningItems, ItemListCount * 2);
            for (int r = 0; r < RowCount; r++)
            {
                int at = r * RowSize;
                int id = table._all[at + 14];
                if (id >= FirstRockId) break;
                var row = new Row { MiningId = id, Raw = table._all.AsSpan(at, RowSize).ToArray() };
                for (int c = 0; c < 4; c++) row.Weights[c] = BitConverter.ToUInt16(table._all, at + 4 + c * 2);
                if (id > SphereCount && id - SphereCount - 1 < ItemListCount) row.BagItem = BitConverter.ToUInt16(items, (id - SphereCount - 1) * 2);
                table.Treasures.Add(row);
            }
            return table;
        }

        public string NameOf(Row row, string[] itemNames) =>
            row.MiningId >= 1 && row.MiningId <= SphereCount ? Spheres[row.MiningId - 1]
            : row.BagItem < itemNames.Length ? itemNames[row.BagItem] : $"Treasure {row.MiningId}";

        public int Total(int column) => Treasures.Sum(t => t.Weights[column]);

        // Plates can only be dug once; a wall with nothing else left to place never finishes filling.
        private static bool IsPlate(ushort item) => item >= 298 && item <= 313;

        /// <summary>Why the weights can't be saved, or null.</summary>
        public string Problem()
        {
            for (int c = 0; c < 4; c++)
            {
                if (Total(c) == 0) return $"{Columns[c]}: at least one treasure needs a weight.";
                if (!Treasures.Any(t => t.Weights[c] > 0 && !IsPlate(t.BagItem))) return $"{Columns[c]}: something besides plates needs a weight.";
            }
            return null;
        }

        public byte[] ToBytes()
        {
            var data = (byte[])_all.Clone();
            for (int r = 0; r < Treasures.Count; r++)
                for (int c = 0; c < 4; c++) BitConverter.GetBytes(Treasures[r].Weights[c]).CopyTo(data, r * RowSize + 4 + c * 2);
            return data;
        }

        /// <summary>Puts the weights back from bytes <see cref="ToBytes"/> produced.</summary>
        public void RestoreWeights(byte[] bytes)
        {
            for (int r = 0; r < Treasures.Count; r++)
                for (int c = 0; c < 4; c++)
                    Treasures[r].Weights[c] = BitConverter.ToUInt16(bytes, r * RowSize + 4 + c * 2);
        }

        public void Save()
        {
            if (Problem() is string p) throw new InvalidOperationException(p);
            GameTableFile.Write(RomInfo.GameTable.MiningTreasures, ToBytes());
        }
    }
}
