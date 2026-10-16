using System;
using System.Collections.Generic;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// The Pickup ability's tables: item ids, the activation divisor and the level weights. The game gives level range r
    /// (levels 10r+1 to 10r+10) common slot j from <see cref="Common"/>[r + j] and rare slot k from <see cref="Rare"/>[r + k],
    /// sliding lists of 18 and 11; with <see cref="PickupSlotsPatch"/> applied every range has its own row of 9.
    /// </summary>
    public sealed class PickupTable
    {
        public const int CommonCount = 18, RareCount = 11, WeightSize = 9, Ranges = 10, CommonSlots = 9, RareSlots = 2;

        public ushort[] Common = new ushort[CommonCount];
        public ushort[] Rare = new ushort[RareCount];
        public byte Divisor;
        public byte[] Weights = new byte[WeightSize];
        /// <summary>Read from the patch's tables, where every range's slots are its own.</summary>
        public bool Independent;

        public int CommonIndex(int range, int slot) => Independent ? range * PickupSlotsPatch.Stride + slot : range + slot;
        public int RareIndex(int range, int slot) => Independent ? range * PickupSlotsPatch.Stride + slot : range + slot;

        private static string Overlay => OverlayUtils.GetPath(RomInfo.pickupTableOverlayNumber);

        public static PickupTable Read()
        {
            string path = Overlay;
            PickupTable t = new PickupTable
            {
                Divisor = DSUtils.ReadFromFile(path, RomInfo.pickupActivationDivisorOffset, 1)[0],
                Weights = DSUtils.ReadFromFile(path, RomInfo.pickupWeightTableOffset, WeightSize),
            };
            int patched = PickupSlotsPatch.CommonTableOffset();
            if (patched >= 0)
            {
                byte[] synth = DSUtils.ReadFromFile(Filesystem.expArmPath, (uint)patched, PickupSlotsPatch.TableLength * 2);
                t.Independent = true;
                t.Common = Ushorts(synth, 0, PickupSlotsPatch.TableLength / 2);
                t.Rare = Ushorts(synth, PickupSlotsPatch.TableLength, PickupSlotsPatch.TableLength / 2);
                return t;
            }
            t.Common = Ushorts(DSUtils.ReadFromFile(path, RomInfo.pickupCommonItemsOffset, CommonCount * 2), 0, CommonCount);
            t.Rare = Ushorts(DSUtils.ReadFromFile(path, RomInfo.pickupRareItemsOffset, RareCount * 2), 0, RareCount);
            return t;
        }

        public void Write()
        {
            string path = Overlay;
            if (Independent)
            {
                uint at = (uint)PickupSlotsPatch.CommonTableOffset();
                DSUtils.WriteToFile(Filesystem.expArmPath, Words(Common, Common.Length), at);
                DSUtils.WriteToFile(Filesystem.expArmPath, Words(Rare, Rare.Length), at + PickupSlotsPatch.TableLength);
            }
            else
            {
                DSUtils.WriteToFile(path, Words(Common, CommonCount), RomInfo.pickupCommonItemsOffset);
                DSUtils.WriteToFile(path, Words(Rare, RareCount), RomInfo.pickupRareItemsOffset);
            }
            DSUtils.WriteToFile(path, new[] { Divisor }, RomInfo.pickupActivationDivisorOffset);
            DSUtils.WriteToFile(path, Weights, RomInfo.pickupWeightTableOffset);
        }

        private static ushort[] Ushorts(byte[] bytes, int start, int count)
        {
            ushort[] values = new ushort[count];
            for (int i = 0; i < count; i++) values[i] = BitConverter.ToUInt16(bytes, start + i * 2);
            return values;
        }

        internal static byte[] Words(IReadOnlyList<ushort> values, int count)
        {
            byte[] bytes = new byte[count * 2];
            for (int i = 0; i < count && i < values.Count; i++) BitConverter.GetBytes(values[i]).CopyTo(bytes, i * 2);
            return bytes;
        }
    }

    /// <summary>
    /// The hidden item table in ARM9. A record is item (u16), quantity (u8), search range (u8), two padding bytes and
    /// the script (u16). The table has no terminator: every loop over it compares against a count, held in a Thumb cmp
    /// (DP, HGSS) or a literal-pool word (Platinum, 257), so the smallest of those counts is the real length.
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
            uint[] sites = RomInfo.hiddenItemCountSites, literals = RomInfo.hiddenItemCountLiterals;
            if (sites.Length == 0 && literals.Length == 0) return -1;
            int count = int.MaxValue;
            foreach (uint site in sites)
            {
                byte[] ins = ARM9.ReadBytes(site, 2);
                if ((ins[1] & 0xF8) != 0x28) return -1;   // Thumb cmp rN, #imm8
                count = Math.Min(count, ins[0]);
            }
            foreach (uint literal in literals)
            {
                uint value = BitConverter.ToUInt32(ARM9.ReadBytes(literal, 4), 0);
                if (value > RomInfo.hiddenItemTableCapacity) return -1;
                count = Math.Min(count, (int)value);
            }
            return count;
        }

        public static List<Entry> Read(int count)
        {
            byte[] table = ARM9.ReadBytes(RomInfo.hiddenItemTableOffset, count * EntrySize);
            List<Entry> entries = new List<Entry>(count);
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
                Entry e = entries[i];
                BitConverter.GetBytes(e.Item).CopyTo(table, at);
                table[at + 2] = e.Quantity;
                table[at + 3] = e.Range;
                BitConverter.GetBytes(e.Padding).CopyTo(table, at + 4);
                BitConverter.GetBytes(e.Script).CopyTo(table, at + 6);
            }
            ARM9.WriteBytes(table, RomInfo.hiddenItemTableOffset);
            foreach (uint site in RomInfo.hiddenItemCountSites)
                ARM9.WriteBytes(new[] { (byte)entries.Count }, site);
            foreach (uint literal in RomInfo.hiddenItemCountLiterals)
                ARM9.WriteBytes(BitConverter.GetBytes((uint)entries.Count), literal);
        }

        /// <summary>Hidden-item map events run script <see cref="ScriptBase"/> plus the entry's script field; the
        /// window ends where the Safari Game scripts start (8800).</summary>
        public const int ScriptBase = 8000, ScriptWindow = 800;

        /// <summary>A map event that picks up a hidden item: its event file, spawnable, first header and tile.</summary>
        public sealed record Placement(int EventFile, int Spawnable, int Header, int X, int Y);

        /// <summary>Every hidden-item event in the project, by the entry script it runs.</summary>
        public static Dictionary<int, List<Placement>> Placements()
        {
            Dictionary<int, int> headerOfEvents = new Dictionary<int, int>();
            int headers = RomInfo.GetHeaderCount();
            for (int h = 0; h < headers; h++)
            {
                MapHeader header;
                try { header = MapHeader.GetMapHeader((ushort)h); } catch { continue; }
                if (header != null) headerOfEvents.TryAdd(header.eventFileID, h);
            }

            Dictionary<int, List<Placement>> found = new Dictionary<int, List<Placement>>();
            int files = Filesystem.GetEventFileCount();
            for (int f = 0; f < files; f++)
            {
                EventFile events;
                try { events = new EventFile(f); } catch { continue; }
                for (int i = 0; i < events.spawnables.Count; i++)
                {
                    Spawnable sp = events.spawnables[i];
                    int script = sp.scriptNumber - ScriptBase;
                    if (script < 0 || script >= ScriptWindow) continue;
                    if (!found.TryGetValue(script, out List<Placement> list)) found[script] = list = new List<Placement>();
                    list.Add(new Placement(f, i, headerOfEvents.TryGetValue(f, out int hd) ? hd : -1, sp.xMapPosition, sp.yMapPosition));
                }
            }
            return found;
        }
    }

    /// <summary>HeartGold and SoulSilver's three Rock Smash item tables in overlay 1, eight item slots each, back to back.</summary>
    public static class RockSmashItemSlots
    {
        public const int Slots = 8;
        public static int Overlay => RomInfo.rockSmashItemTablesOverlayNumber;
        public static uint RuinsOfAlphOffset => RomInfo.rockSmashItemTablesOffset;
        public static uint DefaultOffset => RomInfo.rockSmashItemTablesOffset + 0x10;
        public static uint CliffCaveOffset => RomInfo.rockSmashItemTablesOffset + 0x20;

        public static ushort[] Read(uint offset)
        {
            // hg-engine reads its own array instead of these overlay tables.
            if (HgEngine.HgEngineProject.IsActive)
                return HgEngine.HgEngineRockSmashItems.TryRead(TableType(offset), out ushort[] items, out string error)
                    ? items : throw new InvalidOperationException(error);
            if (OverlayUtils.IsCompressed(Overlay)) OverlayUtils.Decompress(Overlay);
            byte[] raw = DSUtils.ReadFromFile(OverlayUtils.GetPath(Overlay), offset, Slots * 2);
            ushort[] slots = new ushort[Slots];
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
        private static int TableType(uint offset) =>
            offset == DefaultOffset ? 0
            : offset == RuinsOfAlphOffset ? 1
            : offset == CliffCaveOffset ? 2
            : throw new ArgumentException($"No Rock Smash table at 0x{offset:X}.");
    }
}
