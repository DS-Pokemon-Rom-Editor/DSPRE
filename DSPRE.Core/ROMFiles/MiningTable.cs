using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// What the Underground's walls hold: 85 rows of 20 bytes, treasures first and the 14 rocks last. A row is its shape
    /// grid's address (0 for a full rectangle), four weights, its size in tiles, its id, and its art in ug_parts.narc.
    /// The row count is compiled in and rocks must stay last; weights, art and shape cells are edited in place.
    /// </summary>
    public class MiningTable
    {
        public const int RowCount = 85, RowSize = 20, Size = RowCount * RowSize, FirstRockId = 60, SphereCount = 10, ItemListCount = 49;

        // Diamond reads the first and third weights, Pearl the second and fourth, the later two once the National Dex is
        // obtained. Platinum picks by trainer ID parity instead.
        public static string[] Columns => RomInfo.gameFamily == RomInfo.GameFamilies.DP
            ? new[] { "Diamond", "Pearl", "Diamond after the National Dex", "Pearl after the National Dex" }
            : new[] { "Odd trainer ID", "Even trainer ID", "Odd trainer ID after the National Dex", "Even trainer ID after the National Dex" };

        private static readonly string[] Spheres =
        {
            "Small Prism Sphere", "Small Pale Sphere", "Small Red Sphere", "Small Blue Sphere", "Small Green Sphere",
            "Large Prism Sphere", "Large Pale Sphere", "Large Red Sphere", "Large Blue Sphere", "Large Green Sphere",
        };

        public sealed class Row
        {
            public int Index;
            public int MiningId;
            public ushort BagItem;           // 0 for spheres and rocks
            public ushort[] Weights = new ushort[4];
            /// <summary>Size in 8-pixel tiles; the shape grid has a cell per 2 by 2 tiles.</summary>
            public int Width, Height;
            /// <summary>ug_parts.narc members: the tiles and the palette.</summary>
            public ushort Sprite, Palette;
            /// <summary>The shape grid's RAM address, 0 for a full rectangle.</summary>
            public uint ShapeAddress;
            /// <summary>One byte per cell, 'x' solid and 'o' empty; rows with the same address share this array. Null for a full rectangle.</summary>
            public byte[] Shape;
            public bool IsRock => MiningId >= FirstRockId;
            public int ShapeColumns => Width / 2;
            public int ShapeRows => Height / 2;
            public bool Solid(int column, int row) => Shape == null || Shape[row * ShapeColumns + column] == (byte)'x';
            internal byte[] Raw;
        }

        public List<Row> Rows { get; } = new List<Row>();
        public IEnumerable<Row> Treasures => Rows.Where(r => !r.IsRock);
        /// <summary>The wall code values, or null where they are not mapped.</summary>
        public MiningCode Code { get; private set; }

        private uint _overlayAddress;
        private string _overlayPath;

        public static string WhyNot()
        {
            if (RomInfo.gameFamily == RomInfo.GameFamilies.HGSS) return "HeartGold and SoulSilver have no Underground.";
            if (RomLocator.WhyNot(RomLocator.Thing.MiningTreasures, Size) is string why) return why;
            return RomLocator.WhyNot(RomLocator.Thing.MiningItems, ItemListCount * 2);
        }

        public static MiningTable Load()
        {
            byte[] all = RomLocator.Read(RomLocator.Thing.MiningTreasures, Size);
            byte[] items = RomLocator.Read(RomLocator.Thing.MiningItems, ItemListCount * 2);
            RomLocator.Spot spot = RomLocator.Where(RomLocator.Thing.MiningTreasures);
            MiningTable table = new MiningTable
            {
                _overlayAddress = OverlayUtils.OverlayTable.GetRAMAddress(spot.Overlay),
                _overlayPath = RomLocator.PathOf(spot),
            };
            byte[] overlay = File.ReadAllBytes(table._overlayPath);
            Dictionary<uint, byte[]> shapes = new Dictionary<uint, byte[]>();
            for (int r = 0; r < RowCount; r++)
            {
                int at = r * RowSize;
                Row row = new Row
                {
                    Index = r,
                    MiningId = all[at + 14],
                    Width = all[at + 12],
                    Height = all[at + 13],
                    ShapeAddress = BitConverter.ToUInt32(all, at),
                    Sprite = BitConverter.ToUInt16(all, at + 16),
                    Palette = BitConverter.ToUInt16(all, at + 18),
                    Raw = all.AsSpan(at, RowSize).ToArray(),
                };
                for (int c = 0; c < 4; c++) row.Weights[c] = BitConverter.ToUInt16(all, at + 4 + c * 2);
                if (row.MiningId > SphereCount && row.MiningId < FirstRockId && row.MiningId - SphereCount - 1 < ItemListCount)
                    row.BagItem = BitConverter.ToUInt16(items, (row.MiningId - SphereCount - 1) * 2);
                long shapeAt = (long)row.ShapeAddress - table._overlayAddress;
                int cells = row.ShapeColumns * row.ShapeRows;
                if (row.ShapeAddress != 0 && shapeAt >= 0 && shapeAt + cells <= overlay.Length)
                {
                    if (!shapes.TryGetValue(row.ShapeAddress, out byte[] shape))
                        shapes[row.ShapeAddress] = shape = overlay.AsSpan((int)shapeAt, cells).ToArray();
                    row.Shape = shape;
                }
                table.Rows.Add(row);
            }
            table.Code = MiningCode.Read(overlay);
            return table;
        }

        public string NameOf(Row row, string[] itemNames) =>
            row.IsRock ? $"Rock {row.Width / 2}x{row.Height / 2}"
            : row.MiningId >= 1 && row.MiningId <= SphereCount ? Spheres[row.MiningId - 1]
            : row.BagItem < itemNames.Length ? itemNames[row.BagItem] : $"Treasure {row.MiningId}";

        /// <summary>The other rows drawn with the same shape grid as <paramref name="row"/>.</summary>
        public IEnumerable<Row> SharingShape(Row row) =>
            row.Shape == null ? Enumerable.Empty<Row>() : Rows.Where(r => r != row && ReferenceEquals(r.Shape, row.Shape));

        public int Total(int column) => Treasures.Sum(t => t.Weights[column]);

        // Plates can only be dug once; a wall with nothing else left to place never finishes filling.
        private static bool IsPlate(ushort item) => item >= 298 && item <= 313;

        /// <summary>Why the table can't be saved, or null.</summary>
        public string Problem()
        {
            for (int c = 0; c < 4; c++)
            {
                if (Total(c) == 0) return $"{Columns[c]}: at least one treasure needs a weight.";
                if (!Treasures.Any(t => t.Weights[c] > 0 && !IsPlate(t.BagItem))) return $"{Columns[c]}: something besides plates needs a weight.";
            }
            return Code?.Problem();
        }

        private byte[] TableBytes()
        {
            byte[] data = new byte[Size];
            foreach (Row r in Rows)
            {
                int at = r.Index * RowSize;
                r.Raw.CopyTo(data, at);
                for (int c = 0; c < 4; c++) BitConverter.GetBytes(r.Weights[c]).CopyTo(data, at + 4 + c * 2);
                BitConverter.GetBytes(r.Sprite).CopyTo(data, at + 16);
                BitConverter.GetBytes(r.Palette).CopyTo(data, at + 18);
            }
            return data;
        }

        private IEnumerable<Row> ShapeOwners() => Rows.Where(r => r.Shape != null).GroupBy(r => r.ShapeAddress).Select(g => g.First());

        /// <summary>Everything the editor changes, as one byte string for undo and the unsaved check.</summary>
        public byte[] State()
        {
            List<byte> state = new List<byte>(TableBytes());
            foreach (Row r in ShapeOwners()) state.AddRange(r.Shape);
            if (Code != null) state.AddRange(Code.State());
            return state.ToArray();
        }

        public void Restore(byte[] state)
        {
            foreach (Row r in Rows)
            {
                int at = r.Index * RowSize;
                for (int c = 0; c < 4; c++) r.Weights[c] = BitConverter.ToUInt16(state, at + 4 + c * 2);
                r.Sprite = BitConverter.ToUInt16(state, at + 16);
                r.Palette = BitConverter.ToUInt16(state, at + 18);
            }
            int pos = Size;
            foreach (Row r in ShapeOwners()) { Array.Copy(state, pos, r.Shape, 0, r.Shape.Length); pos += r.Shape.Length; }
            Code?.Restore(state.AsSpan(pos).ToArray());
        }

        public void Save()
        {
            if (Problem() is string p) throw new InvalidOperationException(p);
            RomLocator.Write(RomLocator.Thing.MiningTreasures, TableBytes());
            byte[] overlay = File.ReadAllBytes(_overlayPath);
            foreach (Row r in ShapeOwners()) r.Shape.CopyTo(overlay, (int)(r.ShapeAddress - _overlayAddress));
            Code?.WriteInto(overlay);
            DSUtils.WriteWholeFile(_overlayPath, overlay);
        }
    }

    /// <summary>
    /// The Underground wall code values: treasures per wall (rand(range) + minimum), the first dig's count, the rock
    /// placement tries and, in Diamond and Pearl, the Prism or Pale Sphere bonus once the Adamant or Lustrous Orb is
    /// obtained. A wall has room for 8 buried objects and 4 treasures, so the treasure counts stay at 4 or less.
    /// </summary>
    public sealed class MiningCode
    {
        public const int MaxTreasures = 4;

        public int TreasureMin, TreasureMax, FirstDig, Tries;
        public bool HasOrbBonus;
        public int OrbRowA, OrbBonusA, OrbRowB, OrbBonusB;

        private static ushort Hw(byte[] d, uint at) => BitConverter.ToUInt16(d, (int)at);

        /// <summary>Reads the values, or null when the sites are unknown or a site is not the instruction it should be.</summary>
        public static MiningCode Read(byte[] overlay)
        {
            RomInfo.MiningCodeSites s = RomLocator.MiningCodeSites;
            if (s == null || s.Tries + 2 > overlay.Length) return null;
            ushort range = Hw(overlay, s.Range), min = Hw(overlay, s.Minimum), first = Hw(overlay, s.FirstDig), tries = Hw(overlay, s.Tries);
            // movs r2, #range; adds rd, rn, #min; movs rN, #first; cmp rN, #tries
            if ((range & 0xFF00) != 0x2200 || (min & 0xFE00) != 0x1C00 || (first & 0xF800) != 0x2000 || (tries & 0xF800) != 0x2800) return null;
            MiningCode code = new MiningCode
            {
                TreasureMin = (min >> 6) & 7,
                FirstDig = first & 0xFF,
                Tries = tries & 0xFF,
            };
            code.TreasureMax = code.TreasureMin + (range & 0xFF) - 1;
            if (s.OrbBonusA != 0)
            {
                ushort rowA = Hw(overlay, s.OrbRowA), bonusA = Hw(overlay, s.OrbBonusA), rowB = Hw(overlay, s.OrbRowB), bonusB = Hw(overlay, s.OrbBonusB);
                // cmp r1, #row; adds r0, #bonus
                if ((rowA & 0xF800) != 0x2800 || (bonusA & 0xF800) != 0x3000 || (rowB & 0xF800) != 0x2800 || (bonusB & 0xF800) != 0x3000) return null;
                code.HasOrbBonus = true;
                code.OrbRowA = rowA & 0xFF; code.OrbBonusA = bonusA & 0xFF;
                code.OrbRowB = rowB & 0xFF; code.OrbBonusB = bonusB & 0xFF;
            }
            return code;
        }

        public string Problem()
        {
            if (TreasureMin < 1 || TreasureMax > MaxTreasures || TreasureMax < TreasureMin)
                return $"Treasures per wall must be between 1 and {MaxTreasures}, the smallest no larger than the largest.";
            if (FirstDig < 1 || FirstDig > MaxTreasures) return $"The first dig needs 1 to {MaxTreasures} treasures.";
            if (Tries < 1 || Tries > 255) return "Rock placement tries must be 1 to 255.";
            return null;
        }

        private static void SetLowByte(byte[] d, uint at, int value) => d[at] = (byte)value;

        public void WriteInto(byte[] overlay)
        {
            RomInfo.MiningCodeSites s = RomLocator.MiningCodeSites;
            SetLowByte(overlay, s.Range, TreasureMax - TreasureMin + 1);
            ushort min = (ushort)((Hw(overlay, s.Minimum) & ~0x1C0) | (TreasureMin << 6));
            BitConverter.GetBytes(min).CopyTo(overlay, (int)s.Minimum);
            SetLowByte(overlay, s.FirstDig, FirstDig);
            SetLowByte(overlay, s.Tries, Tries);
            if (HasOrbBonus)
            {
                SetLowByte(overlay, s.OrbBonusA, OrbBonusA);
                SetLowByte(overlay, s.OrbBonusB, OrbBonusB);
            }
        }

        internal byte[] State() => new[] { (byte)TreasureMin, (byte)TreasureMax, (byte)FirstDig, (byte)Tries, (byte)OrbBonusA, (byte)OrbBonusB };

        internal void Restore(byte[] state)
        {
            TreasureMin = state[0]; TreasureMax = state[1]; FirstDig = state[2]; Tries = state[3];
            OrbBonusA = state[4]; OrbBonusB = state[5];
        }
    }
}
