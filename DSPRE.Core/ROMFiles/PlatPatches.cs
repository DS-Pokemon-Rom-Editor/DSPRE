using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DSPRE.Editors;
using static DSPRE.RomInfo;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// hzla's PlatPatches for US Platinum: Item Expansion and Extra TMs (TM93 to TM152). Payloads are found by following
    /// the ARM9 hooks, never by searching for their marker, because stale copies can remain after a relocation.
    /// </summary>
    public static class PlatPatches
    {
        private static readonly byte[] HookBytes = { 0x00, 0x4B, 0x18, 0x47 };
        private const uint ItemFileIdHook = 0x0207CE78, ItemIsTmHmHook = 0x0205E060;
        private const int MarkerLength = 0x10;

        /// <summary>The marker offset a hook leads to, or -1 when the hook or marker isn't there.</summary>
        private static int FollowHook(byte[] arm9, byte[] synth, uint hookRam, string marker)
        {
            int at = (int)(hookRam - ARM9.address);
            if (at < 0 || at + 8 > arm9.Length || !arm9.AsSpan(at, 4).SequenceEqual(HookBytes)) return -1;
            uint target = BitConverter.ToUInt32(arm9, at + 4) & ~1u;
            if (target < synthOverlayLoadAddress + MarkerLength) return -1;
            long m = target - synthOverlayLoadAddress - MarkerLength;
            byte[] want = Encoding.ASCII.GetBytes(marker);
            if (m < 0 || m + MarkerLength > synth.Length || !synth.AsSpan((int)m, want.Length).SequenceEqual(want)) return -1;
            for (int i = want.Length; i < MarkerLength; i++) if (synth[m + i] != 0) return -1;
            return (int)m;
        }

        private static (string key, byte[] arm9, byte[] synth) _files;

        /// <summary>arm9 and the synthetic overlay, re-read only when either changed on disk.</summary>
        private static (byte[] arm9, byte[] synth) Files()
        {
            if (gameFamily != GameFamilies.Plat || isHGE || !File.Exists(arm9Path)) return (null, null);
            if (!File.Exists(Filesystem.expArmPath)) DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.synthOverlay });
            if (!File.Exists(Filesystem.expArmPath)) return (null, null);
            string key = arm9Path + File.GetLastWriteTimeUtc(arm9Path).Ticks + Filesystem.expArmPath + File.GetLastWriteTimeUtc(Filesystem.expArmPath).Ticks;
            if (_files.key != key) _files = (key, File.ReadAllBytes(arm9Path), File.ReadAllBytes(Filesystem.expArmPath));
            return (_files.arm9, _files.synth);
        }

        /// <summary>Drops cached reads, for callers that just wrote either file outside these methods.</summary>
        public static void Forget() => _files = default;

        // ---------------------------------------------------------------- Item Expansion

        public sealed class ItemExpansion
        {
            public int Marker { get; init; }
            public ushort FirstItem { get; init; }
            public ushort Count { get; init; }
            public ushort Capacity { get; init; }
            public int RowsAt => Marker + 0x298;
            public bool Covers(int itemId) => itemId >= FirstItem && itemId < FirstItem + Count;
        }

        public static ItemExpansion Items()
        {
            try
            {
                var (arm9, synth) = Files();
                if (arm9 == null) return null;
                int i = FollowHook(arm9, synth, ItemFileIdHook, "ITEMEXPV2");
                if (i < 0 || i + 0x298 > synth.Length) return null;
                var e = new ItemExpansion
                {
                    Marker = i,
                    FirstItem = BitConverter.ToUInt16(synth, i + 0x290),
                    Count = BitConverter.ToUInt16(synth, i + 0x292),
                    Capacity = BitConverter.ToUInt16(synth, i + 0x294),
                };
                if (e.Count > e.Capacity || e.RowsAt + e.Capacity * 8 > synth.Length) return null;
                return e;
            }
            catch (IOException) { return null; }
        }

        public static bool TryReadItem(int itemId, out ItemNarcTableEntry entry)
        {
            entry = default;
            var e = Items();
            if (e == null || !e.Covers(itemId)) return false;
            byte[] synth = Files().synth;
            int o = e.RowsAt + (itemId - e.FirstItem) * 8;
            entry = new ItemNarcTableEntry
            {
                itemData = BitConverter.ToUInt16(synth, o),
                itemIcon = BitConverter.ToUInt16(synth, o + 2),
                itemPalette = BitConverter.ToUInt16(synth, o + 4),
                itemAGB = BitConverter.ToUInt16(synth, o + 6),
            };
            return true;
        }

        public static bool TryWriteItem(int itemId, ItemNarcTableEntry entry)
        {
            var e = Items();
            if (e == null || !e.Covers(itemId)) return false;
            byte[] synth = (byte[])Files().synth.Clone();
            int o = e.RowsAt + (itemId - e.FirstItem) * 8;
            BitConverter.GetBytes((ushort)entry.itemData).CopyTo(synth, o);
            BitConverter.GetBytes((ushort)entry.itemIcon).CopyTo(synth, o + 2);
            BitConverter.GetBytes((ushort)entry.itemPalette).CopyTo(synth, o + 4);
            BitConverter.GetBytes((ushort)entry.itemAGB).CopyTo(synth, o + 6);
            File.WriteAllBytes(Filesystem.expArmPath, synth);
            Forget();
            return true;
        }

        // ---------------------------------------------------------------- Extra TMs

        public const int MaxExtraTms = 60, PersonalMaskRows = 28, FirstExtraTmNumber = 93, FirstExtraMachineIndex = 100;
        private const int PersonalMaskOffset = 0x28, PersonalMaskFirstBit = 4;

        public sealed class ExtraTms
        {
            public int Marker { get; init; }
            public int Count { get; init; }
            public ushort[] ItemIds { get; } = new ushort[MaxExtraTms];
            public ushort[] MoveIds { get; } = new ushort[MaxExtraTms];
            internal int MasksAt => Marker + 0x608;
            public static string Label(int row) => $"TM{FirstExtraTmNumber + row}";
        }

        public static ExtraTms Tms()
        {
            try
            {
                var (arm9, synth) = Files();
                if (arm9 == null) return null;
                int m = FollowHook(arm9, synth, ItemIsTmHmHook, "EXTRATMSV1");
                if (m < 0 || m + 0x608 > synth.Length) return null;
                uint table = BitConverter.ToUInt32(synth, m + 0x5C);
                if (table != synthOverlayLoadAddress + (uint)m + 0x510) return null;
                int count = (int)BitConverter.ToUInt32(synth, m + 0x510);
                if (count < 0 || count > MaxExtraTms) return null;
                var t = new ExtraTms { Marker = m, Count = count };
                for (int i = 0; i < MaxExtraTms; i++)
                {
                    t.ItemIds[i] = BitConverter.ToUInt16(synth, m + 0x518 + i * 2);
                    t.MoveIds[i] = BitConverter.ToUInt16(synth, m + 0x590 + i * 2);
                }
                return t;
            }
            catch (IOException) { return null; }
        }

        /// <summary>Writes one extra TM's move. The item's description and colour are separate.</summary>
        public static void SetExtraTmMove(int row, ushort move) => SetExtraTmMoves(new Dictionary<int, ushort> { [row] = move });

        /// <summary>Writes extra TM moves by row in one write, skipping rows that already hold that move.</summary>
        public static void SetExtraTmMoves(IReadOnlyDictionary<int, ushort> moves)
        {
            var t = Tms() ?? throw new InvalidOperationException("The Extra TMs patch isn't installed.");
            if (moves.Keys.Any(r => r < 0 || r >= t.Count)) throw new ArgumentOutOfRangeException(nameof(moves));
            var changed = moves.Where(kv => t.MoveIds[kv.Key] != kv.Value).ToList();
            if (changed.Count == 0) return;
            byte[] synth = (byte[])Files().synth.Clone();
            foreach (var (row, move) in changed) BitConverter.GetBytes(move).CopyTo(synth, t.Marker + 0x590 + row * 2);
            File.WriteAllBytes(Filesystem.expArmPath, synth);
            Forget();
        }

        private static string PersonalPath(int personalId)
        {
            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.personalPokeData });
            return Path.Combine(gameDirs[DirNames.personalPokeData].unpackedDir, personalId.ToString("D4"));
        }

        /// <summary>Whether personal file <paramref name="personalId"/> can learn extra TM <paramref name="row"/>.</summary>
        public static bool CanLearn(ExtraTms t, int personalId, int row)
        {
            if (row < PersonalMaskRows)
            {
                byte[] p = File.ReadAllBytes(PersonalPath(personalId));
                return p.Length >= PersonalMaskOffset + 4 && (BitConverter.ToUInt32(p, PersonalMaskOffset) & (1u << (row + PersonalMaskFirstBit))) != 0;
            }
            byte[] synth = Files().synth;
            int o = t.MasksAt + personalId * 4;
            return o + 4 <= synth.Length && (BitConverter.ToUInt32(synth, o) & (1u << (row - PersonalMaskRows))) != 0;
        }

        /// <summary>Sets compatibility for one row across personal files.</summary>
        public static void SetCanLearn(ExtraTms t, int row, IReadOnlyDictionary<int, bool> byPersonalId) =>
            SetCanLearn(t, byPersonalId.Select(kv => (row, kv.Key, kv.Value)));

        /// <summary>
        /// Sets compatibility as (row, personal file, can learn). Rows 0-27 use bits 4-31 of the personal file's fourth
        /// TM word (bits 0-3 are HM05-HM08); later rows use a mask per personal file in the synthetic overlay.
        /// </summary>
        public static void SetCanLearn(ExtraTms t, IEnumerable<(int Row, int PersonalId, bool Can)> changes)
        {
            var list = changes.ToList();
            if (list.Any(c => c.Row < 0 || c.Row >= t.Count)) throw new ArgumentOutOfRangeException(nameof(changes));
            byte[] synth = (byte[])Files().synth.Clone();
            if (list.Any(c => c.Row >= PersonalMaskRows && c.Can && t.MasksAt + c.PersonalId * 4 + 4 > synth.Length))
                throw new InvalidOperationException("A Pokémon is past the Extra TMs compatibility table.");

            bool synthChanged = false;
            foreach (var c in list.Where(c => c.Row >= PersonalMaskRows))
            {
                int o = t.MasksAt + c.PersonalId * 4;
                if (o + 4 > synth.Length) continue;   // only "can't learn" gets here, already true
                uint mask = BitConverter.ToUInt32(synth, o), bit = 1u << (c.Row - PersonalMaskRows);
                uint next = c.Can ? mask | bit : mask & ~bit;
                if (next == mask) continue;
                BitConverter.GetBytes(next).CopyTo(synth, o);
                synthChanged = true;
            }
            foreach (var file in list.Where(c => c.Row < PersonalMaskRows).GroupBy(c => c.PersonalId))
            {
                string path = PersonalPath(file.Key);
                byte[] p = File.ReadAllBytes(path);
                uint mask = BitConverter.ToUInt32(p, PersonalMaskOffset), next = mask;
                foreach (var c in file)
                {
                    uint bit = 1u << (c.Row + PersonalMaskFirstBit);
                    next = c.Can ? next | bit : next & ~bit;
                }
                if (next == mask) continue;
                BitConverter.GetBytes(next).CopyTo(p, PersonalMaskOffset);
                File.WriteAllBytes(path, p);
            }
            if (synthChanged) { File.WriteAllBytes(Filesystem.expArmPath, synth); Forget(); }
        }

        /// <summary>Every (row, personal file) pair that can learn, for rows from <paramref name="firstRow"/>, read in one pass.</summary>
        public static HashSet<(int Row, int PersonalId)> Compatibility(ExtraTms t, IEnumerable<int> personalIds, int firstRow = 0)
        {
            var result = new HashSet<(int, int)>();
            byte[] synth = Files().synth;
            foreach (int id in personalIds)
            {
                uint personalMask = 0;
                if (firstRow < PersonalMaskRows)
                {
                    byte[] p = File.ReadAllBytes(PersonalPath(id));
                    if (p.Length >= PersonalMaskOffset + 4) personalMask = BitConverter.ToUInt32(p, PersonalMaskOffset);
                }
                int o = t.MasksAt + id * 4;
                uint synthMask = o + 4 <= synth.Length ? BitConverter.ToUInt32(synth, o) : 0;
                for (int row = firstRow; row < t.Count; row++)
                {
                    bool can = row < PersonalMaskRows
                        ? (personalMask & (1u << (row + PersonalMaskFirstBit))) != 0
                        : (synthMask & (1u << (row - PersonalMaskRows))) != 0;
                    if (can) result.Add((row, id));
                }
            }
            return result;
        }
    }

    /// <summary>
    /// Item id to item-data, icon and palette members, through PlatPatches' overflow table for expanded items as the game does.
    /// </summary>
    public static class ItemTable
    {
        /// <summary>Vanilla rows in the ARM9 table (items 0 to 467 in DP, Pt and HGSS).</summary>
        public const int VanillaCount = 468;

        public static ItemNarcTableEntry Read(int itemId)
        {
            if (PlatPatches.TryReadItem(itemId, out var e)) return e;
            if (itemId < 0 || itemId >= VanillaCount) throw new InvalidOperationException($"Item {itemId} has no row in the item table.");
            uint o = itemTableOffset + (uint)itemId * 8;
            return new ItemNarcTableEntry
            {
                itemData = ARM9.ReadWordLE(o),
                itemIcon = ARM9.ReadWordLE(o + 2),
                itemPalette = ARM9.ReadWordLE(o + 4),
                itemAGB = ARM9.ReadWordLE(o + 6),
            };
        }

        public static void Write(int itemId, ItemNarcTableEntry e)
        {
            if (PlatPatches.TryWriteItem(itemId, e)) return;
            if (itemId < 0 || itemId >= VanillaCount) throw new InvalidOperationException($"Item {itemId} has no row in the item table.");
            uint o = itemTableOffset + (uint)itemId * 8;
            ARM9.WriteBytes(BitConverter.GetBytes((ushort)e.itemData), o);
            ARM9.WriteBytes(BitConverter.GetBytes((ushort)e.itemIcon), o + 2);
            ARM9.WriteBytes(BitConverter.GetBytes((ushort)e.itemPalette), o + 4);
            ARM9.WriteBytes(BitConverter.GetBytes((ushort)e.itemAGB), o + 6);
            PlatPatches.Forget();
        }

        /// <summary>Whether the id has a row the game reads: vanilla, or covered by the overflow table.</summary>
        public static bool Exists(int itemId) =>
            itemId >= 0 && (itemId < VanillaCount || (PlatPatches.Items()?.Covers(itemId) ?? false));

        /// <summary>Item-data members for items 0 to <paramref name="itemCount"/> - 1, read in one pass; -1 where an item has no row.</summary>
        public static int[] DataMembers(int itemCount)
        {
            var members = new int[itemCount];
            byte[] table = ARM9.ReadBytes(itemTableOffset, VanillaCount * 8);
            var expansion = PlatPatches.Items();
            for (int i = 0; i < itemCount; i++)
            {
                if (i < VanillaCount) members[i] = BitConverter.ToUInt16(table, i * 8);
                else members[i] = expansion != null && expansion.Covers(i) && PlatPatches.TryReadItem(i, out var e) ? (int)e.itemData : -1;
            }
            return members;
        }

        /// <summary>Other items reading the same item-data member, which an edit to it also changes.</summary>
        public static List<int> SharingData(int itemId, int itemCount) =>
            Exists(itemId) ? SharingData(itemId, (int)Read(itemId).itemData, itemCount) : new List<int>();

        /// <summary>Other items whose rows point at item data <paramref name="data"/>.</summary>
        public static List<int> SharingData(int itemId, int data, int itemCount)
        {
            int[] members = DataMembers(itemCount);
            return Enumerable.Range(0, itemCount).Where(i => i != itemId && members[i] == data).ToList();
        }
    }
}
