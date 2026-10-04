using System;
using System.Collections.Generic;

namespace DSPRE.ROMFiles
{
    /// <summary>The Pickup ability's tables in their overlay: item ids, the activation divisor and the level weights.</summary>
    public sealed class PickupTable
    {
        public const int CommonCount = 18, RareCount = 11, WeightSize = 9;

        public ushort[] Common = new ushort[CommonCount];
        public ushort[] Rare = new ushort[RareCount];
        public byte Divisor;
        public byte[] Weights = new byte[WeightSize];

        private static string Overlay => OverlayUtils.GetPath(RomInfo.pickupTableOverlayNumber);

        public static PickupTable Read()
        {
            string path = Overlay;
            var t = new PickupTable
            {
                Divisor = DSUtils.ReadFromFile(path, RomInfo.pickupActivationDivisorOffset, 1)[0],
                Weights = DSUtils.ReadFromFile(path, RomInfo.pickupWeightTableOffset, WeightSize),
            };
            byte[] common = DSUtils.ReadFromFile(path, RomInfo.pickupCommonItemsOffset, CommonCount * 2);
            for (int i = 0; i < CommonCount; i++) t.Common[i] = BitConverter.ToUInt16(common, i * 2);
            byte[] rare = DSUtils.ReadFromFile(path, RomInfo.pickupRareItemsOffset, RareCount * 2);
            for (int i = 0; i < RareCount; i++) t.Rare[i] = BitConverter.ToUInt16(rare, i * 2);
            return t;
        }

        public void Write()
        {
            string path = Overlay;
            DSUtils.WriteToFile(path, Words(Common, CommonCount), RomInfo.pickupCommonItemsOffset);
            DSUtils.WriteToFile(path, Words(Rare, RareCount), RomInfo.pickupRareItemsOffset);
            DSUtils.WriteToFile(path, new[] { Divisor }, RomInfo.pickupActivationDivisorOffset);
            DSUtils.WriteToFile(path, Weights, RomInfo.pickupWeightTableOffset);
        }

        internal static byte[] Words(IReadOnlyList<ushort> values, int count)
        {
            var bytes = new byte[count * 2];
            for (int i = 0; i < count && i < values.Count; i++) BitConverter.GetBytes(values[i]).CopyTo(bytes, i * 2);
            return bytes;
        }
    }

    /// <summary>
    /// HeartGold's hidden item table in ARM9. A record is item (u16), quantity (u8), search range (u8), two padding
    /// bytes and the script (u16). The table has no terminator: every loop over it compares against a count held in
    /// a Thumb cmp, so the smallest of those counts is the real length.
    /// </summary>
    public static class HiddenItemTable
    {
        public const int EntrySize = 8;

        public sealed class Entry
        {
            public ushort Item;
            public byte Quantity;
            public byte Range;
            public ushort Padding;
            public ushort Script;
        }

        /// <summary>The count every loop agrees on, or -1 when a count site is not the expected instruction.</summary>
        public static int Count()
        {
            var sites = RomInfo.hiddenItemCountSites;
            if (sites.Length == 0) return -1;
            int count = int.MaxValue;
            foreach (uint site in sites)
            {
                byte[] ins = ARM9.ReadBytes(site, 2);
                if ((ins[1] & 0xF8) != 0x28) return -1;   // Thumb cmp rN, #imm8
                count = Math.Min(count, ins[0]);
            }
            return count;
        }

        public static List<Entry> Read(int count)
        {
            byte[] table = ARM9.ReadBytes(RomInfo.hiddenItemTableOffset, count * EntrySize);
            var entries = new List<Entry>(count);
            for (int i = 0; i < count; i++)
            {
                int at = i * EntrySize;
                entries.Add(new Entry
                {
                    Item = BitConverter.ToUInt16(table, at),
                    Quantity = table[at + 2],
                    Range = table[at + 3],
                    Padding = BitConverter.ToUInt16(table, at + 4),
                    Script = BitConverter.ToUInt16(table, at + 6),
                });
            }
            return entries;
        }

        /// <summary>Writes the entries, zeroes the rest of the table's room, and sets every count site.</summary>
        public static void Write(IReadOnlyList<Entry> entries, int capacity)
        {
            byte[] table = new byte[capacity * EntrySize];
            for (int i = 0; i < entries.Count; i++)
            {
                int at = i * EntrySize;
                var e = entries[i];
                BitConverter.GetBytes(e.Item).CopyTo(table, at);
                table[at + 2] = e.Quantity;
                table[at + 3] = e.Range;
                BitConverter.GetBytes(e.Padding).CopyTo(table, at + 4);
                BitConverter.GetBytes(e.Script).CopyTo(table, at + 6);
            }
            ARM9.WriteBytes(table, RomInfo.hiddenItemTableOffset);
            foreach (uint site in RomInfo.hiddenItemCountSites)
                ARM9.WriteBytes(new[] { (byte)entries.Count }, site);
        }
    }

    /// <summary>HeartGold's three Rock Smash item tables in overlay 1, eight item slots each; offsets confirmed on English HeartGold only.</summary>
    public static class RockSmashItemSlots
    {
        public const int Overlay = 1, Slots = 8;
        public const uint RuinsOfAlphOffset = 0x23D04, DefaultOffset = 0x23D14, CliffCaveOffset = 0x23D24;

        public static ushort[] Read(uint offset)
        {
            // hg-engine reads its own array instead of these overlay tables.
            if (HgEngine.HgEngineProject.IsActive)
                return HgEngine.HgEngineRockSmashItems.TryRead(TableType(offset), out ushort[] items, out string error)
                    ? items : throw new InvalidOperationException(error);
            if (OverlayUtils.IsCompressed(Overlay)) OverlayUtils.Decompress(Overlay);
            byte[] raw = DSUtils.ReadFromFile(OverlayUtils.GetPath(Overlay), offset, Slots * 2);
            var slots = new ushort[Slots];
            for (int i = 0; i < Slots; i++) slots[i] = BitConverter.ToUInt16(raw, i * 2);
            return slots;
        }

        public static void Write(uint offset, IReadOnlyList<ushort> slots)
        {
            if (HgEngine.HgEngineProject.IsActive)
            {
                if (!HgEngine.HgEngineRockSmashItems.TryWrite(TableType(offset), slots, out string error))
                    throw new InvalidOperationException(error);
                return;
            }
            DSUtils.WriteToFile(OverlayUtils.GetPath(Overlay), PickupTable.Words(slots, Slots), offset);
        }

        /// <summary>The map type that picks this table, which is also its index in hg-engine's array.</summary>
        private static int TableType(uint offset) => offset switch
        {
            DefaultOffset => 0,
            RuinsOfAlphOffset => 1,
            CliffCaveOffset => 2,
            _ => throw new ArgumentException($"No Rock Smash table at 0x{offset:X}."),
        };
    }
}
