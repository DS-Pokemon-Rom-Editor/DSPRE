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
                (byte[] arm9, byte[] synth) = Files();
                if (arm9 == null) return null;
                int i = FollowHook(arm9, synth, ItemFileIdHook, "ITEMEXPV2");
                if (i < 0 || i + 0x298 > synth.Length) return null;
                ItemExpansion e = new ItemExpansion
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
            ItemExpansion e = Items();
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
            ItemExpansion e = Items();
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
                (byte[] arm9, byte[] synth) = Files();
                if (arm9 == null) return null;
                int m = FollowHook(arm9, synth, ItemIsTmHmHook, "EXTRATMSV1");
                if (m < 0 || m + 0x608 > synth.Length) return null;
                uint table = BitConverter.ToUInt32(synth, m + 0x5C);
                if (table != synthOverlayLoadAddress + (uint)m + 0x510) return null;
                int count = (int)BitConverter.ToUInt32(synth, m + 0x510);
                if (count < 0 || count > MaxExtraTms) return null;
                ExtraTms t = new ExtraTms { Marker = m, Count = count };
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
            ExtraTms t = Tms() ?? throw new InvalidOperationException("The Extra TMs patch isn't installed.");
            if (moves.Keys.Any(r => r < 0 || r >= t.Count)) throw new ArgumentOutOfRangeException(nameof(moves));
            List<KeyValuePair<int, ushort>> changed = moves.Where(kv => t.MoveIds[kv.Key] != kv.Value).ToList();
            if (changed.Count == 0) return;
            byte[] synth = (byte[])Files().synth.Clone();
            foreach ((int row, ushort move) in changed) BitConverter.GetBytes(move).CopyTo(synth, t.Marker + 0x590 + row * 2);
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
            List<(int Row, int PersonalId, bool Can)> list = changes.ToList();
            if (list.Any(c => c.Row < 0 || c.Row >= t.Count)) throw new ArgumentOutOfRangeException(nameof(changes));
            byte[] synth = (byte[])Files().synth.Clone();
            if (list.Any(c => c.Row >= PersonalMaskRows && c.Can && t.MasksAt + c.PersonalId * 4 + 4 > synth.Length))
                throw new InvalidOperationException("A Pokémon is past the Extra TMs compatibility table.");

            bool synthChanged = false;
            foreach ((int Row, int PersonalId, bool Can) c in list.Where(c => c.Row >= PersonalMaskRows))
            {
                int o = t.MasksAt + c.PersonalId * 4;
                if (o + 4 > synth.Length) continue;   // only "can't learn" gets here, already true
                uint mask = BitConverter.ToUInt32(synth, o), bit = 1u << (c.Row - PersonalMaskRows);
                uint next = c.Can ? mask | bit : mask & ~bit;
                if (next == mask) continue;
                BitConverter.GetBytes(next).CopyTo(synth, o);
                synthChanged = true;
            }
            foreach (IGrouping<int, (int Row, int PersonalId, bool Can)> file in list.Where(c => c.Row < PersonalMaskRows).GroupBy(c => c.PersonalId))
            {
                string path = PersonalPath(file.Key);
                byte[] p = File.ReadAllBytes(path);
                uint mask = BitConverter.ToUInt32(p, PersonalMaskOffset), next = mask;
                foreach ((int Row, int PersonalId, bool Can) c in file)
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
            HashSet<(int, int)> result = new HashSet<(int, int)>();
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
        /// <summary>How far the ARM9 table runs, and why.</summary>
        public sealed class TableLimits
        {
            /// <summary>Rows <see cref="Read"/> and <see cref="Write"/> accept.</summary>
            public int Count { get; init; }
            /// <summary>The highest id the game's item loader lets through, or -1 when its code didn't match.</summary>
            public int CodeLimit { get; init; } = -1;
            /// <summary>The first row within the game's limit that can't be an item row, or -1.</summary>
            public int FirstBadRow { get; init; } = -1;
        }

        private static (string key, TableLimits limits) _limits;

        /// <summary>Drops the cached limits, for callers that changed the item archives.</summary>
        public static void Forget() => _limits = default;

        /// <summary>Vanilla rows in the ARM9 table: the game's own item limit, cut short at the first row that can't be real.</summary>
        public static int VanillaCount => Limits().Count;

        public static TableLimits Limits()
        {
            if (string.IsNullOrEmpty(arm9Path) || !File.Exists(arm9Path)) return new TableLimits();
            string key = arm9Path + "|" + File.GetLastWriteTimeUtc(arm9Path).Ticks + "|" + itemTableOffset + "|" + isHGE;
            if (_limits.key == key && _limits.limits != null) return _limits.limits;
            TableLimits limits = isHGE ? ComputedLimits() : ComputeLimits(File.ReadAllBytes(arm9Path));
            _limits = (key, limits);
            return limits;
        }

        private static TableLimits ComputeLimits(byte[] arm9)
        {
            int codeLimit = ReadCodeLimit(arm9);
            int cap = codeLimit >= 0 ? codeLimit + 1 : ItemNameCount();
            int dataCount = MemberCount(DirNames.itemData);
            int iconCount = MemberCount(DirNames.itemIcons);

            int rows = 0;
            while (rows < cap && itemTableOffset + (rows + 1) * 8L <= arm9.Length)
            {
                int o = (int)itemTableOffset + rows * 8;
                int data = BitConverter.ToUInt16(arm9, o), icon = BitConverter.ToUInt16(arm9, o + 2), palette = BitConverter.ToUInt16(arm9, o + 4);
                if (data >= dataCount || icon >= iconCount || palette >= iconCount) break;
                rows++;
            }
            if (codeLimit >= 0 && rows < cap)
                AppLogger.Error($"Item table: row {rows} points past the item archives, below the game's limit of {codeLimit}; items from {rows} on are left alone.");
            return new TableLimits { Count = rows, CodeLimit = codeLimit, FirstBadRow = codeLimit >= 0 && rows < cap ? rows : -1 };
        }

        // hg-engine's GetItemIndex ignores the ARM9 table: an item's data is the member at its id, and the icon archive holds
        // each item's drawing and colours at 2 * id + 2 and 2 * id + 3, after its shared NANR and NCER.
        private static TableLimits ComputedLimits()
        {
            int dataCount = MemberCount(DirNames.itemData), iconCount = MemberCount(DirNames.itemIcons);
            return new TableLimits { Count = Math.Max(0, Math.Min(dataCount, (iconCount - 2) / 2)) };
        }

        private static ItemNarcTableEntry ComputedRow(int itemId) => new ItemNarcTableEntry
        {
            itemData = (uint)itemId,
            itemIcon = (uint)(itemId * 2 + 2),
            itemPalette = (uint)(itemId * 2 + 3),
        };

        /// <summary>The limit the game's item loader clamps ids to, when its code is where RomInfo expects; -1 otherwise.</summary>
        private static int ReadCodeLimit(byte[] arm9)
        {
            uint site = itemLimitSite;
            string[] parts = (itemLimitSignature ?? "").Replace(" ", "") is string hex && hex.Length % 2 == 0
                ? Enumerable.Range(0, hex.Length / 2).Select(i => hex.Substring(i * 2, 2)).ToArray()
                : Array.Empty<string>();
            if (site == 0 || parts.Length == 0 || site + parts.Length > arm9.Length) return -1;
            for (int i = 0; i < parts.Length; i++)
                if (parts[i] != "??" && arm9[site + i] != Convert.ToByte(parts[i], 16)) return -1;

            ushort load = BitConverter.ToUInt16(arm9, (int)site + 4);
            if ((load & 0xFF00) == 0x2000)
            {
                // mov r0, #imm8; lsl r0, r0, #n
                ushort shift = BitConverter.ToUInt16(arm9, (int)site + 6);
                if ((shift & 0xF83F) != 0) return -1;
                return (load & 0xFF) << ((shift >> 6) & 0x1F);
            }
            if ((load & 0xFF00) == 0x4800)
            {
                // ldr r0, [pc, #imm8 * 4]
                long literal = ((site + 4 + 4) & ~3u) + (load & 0xFF) * 4L;
                if (literal + 4 > arm9.Length) return -1;
                uint value = BitConverter.ToUInt32(arm9, (int)literal);
                return value <= ushort.MaxValue ? (int)value : -1;
            }
            return -1;
        }

        /// <summary>Members in an archive, from the unpacked copy DSPRE edits, else the packed NARC; int.MaxValue when neither can be read.</summary>
        private static int MemberCount(DirNames dir)
        {
            try
            {
                if (gameDirs == null || !gameDirs.TryGetValue(dir, out (string packedDir, string unpackedDir) dirs)) return int.MaxValue;
                if (Directory.Exists(dirs.unpackedDir))
                {
                    int n = Directory.GetFiles(dirs.unpackedDir).Length;
                    if (n > 0) return n;
                }
                if (File.Exists(dirs.packedDir)) return new Editors.Utils.NarcReader(dirs.packedDir).Entrys;
            }
            catch (Exception ex) { AppLogger.Error($"Item table: {dir} member count: {ex.Message}"); }
            return int.MaxValue;
        }

        private static int ItemNameCount()
        {
            try { return GetItemNames().Length; }
            catch (Exception ex) { AppLogger.Error("Item table: item names: " + ex.Message); return 0; }
        }

        /// <summary>An item's row. On hg-engine it follows from the id, so an item added since the last build has one too.</summary>
        public static ItemNarcTableEntry Read(int itemId)
        {
            if (isHGE)
            {
                if (itemId < 0) throw new InvalidOperationException($"Item {itemId} has no row in the item table.");
                return ComputedRow(itemId);
            }
            if (PlatPatches.TryReadItem(itemId, out ItemNarcTableEntry e)) return e;
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
            if (isHGE) throw new InvalidOperationException("hg-engine finds an item's data and icon by its id, so there is no item table to edit.");
            if (PlatPatches.TryWriteItem(itemId, e)) return;
            if (itemId < 0 || itemId >= VanillaCount) throw new InvalidOperationException($"Item {itemId} has no row in the item table.");
            uint o = itemTableOffset + (uint)itemId * 8;
            ARM9.WriteBytes(BitConverter.GetBytes((ushort)e.itemData), o);
            ARM9.WriteBytes(BitConverter.GetBytes((ushort)e.itemIcon), o + 2);
            ARM9.WriteBytes(BitConverter.GetBytes((ushort)e.itemPalette), o + 4);
            ARM9.WriteBytes(BitConverter.GetBytes((ushort)e.itemAGB), o + 6);
            PlatPatches.Forget();
            Forget();
        }

        /// <summary>Whether the id has a row the game reads: vanilla, or covered by the overflow table.</summary>
        public static bool Exists(int itemId) =>
            itemId >= 0 && (itemId < VanillaCount || (PlatPatches.Items()?.Covers(itemId) ?? false));

        /// <summary>Item-data members for items 0 to <paramref name="itemCount"/> - 1, read in one pass; -1 where an item has no row.</summary>
        public static int[] DataMembers(int itemCount)
        {
            int[] members = new int[itemCount];
            int vanilla = VanillaCount;
            if (isHGE)
            {
                for (int i = 0; i < itemCount; i++) members[i] = i < vanilla ? i : -1;
                return members;
            }
            byte[] table = ARM9.ReadBytes(itemTableOffset, vanilla * 8);
            PlatPatches.ItemExpansion expansion = PlatPatches.Items();
            for (int i = 0; i < itemCount; i++)
            {
                if (i < vanilla) members[i] = BitConverter.ToUInt16(table, i * 8);
                else members[i] = expansion != null && expansion.Covers(i) && PlatPatches.TryReadItem(i, out ItemNarcTableEntry e) ? (int)e.itemData : -1;
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
