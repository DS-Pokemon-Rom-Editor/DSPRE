using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DSPRE.ROMFiles
{
    /// <summary>The common and specialty item marts read by the standard field mart commands.</summary>
    public sealed class MartData : RomFile
    {
        private const string ExpansionMarker = "MARTEXPANDV1";
        private const int ExpansionHeaderSize = 0x20;
        private const int MaxCommonItems = 63;

        public sealed class CommonEntry
        {
            public ushort ItemId { get; set; }
            public ushort RequiredTier { get; set; }
        }

        public sealed class SpecialtyShop
        {
            internal SpecialtyShop(int id, string name, List<ushort> items, bool isCustom = false)
            {
                Id = id;
                Name = name;
                Items = items;
                OriginalCapacity = items.Count;
                IsCustom = isCustom;
            }

            public int Id { get; }
            public string Name { get; }
            public List<ushort> Items { get; }
            internal int OriginalCapacity { get; }
            public bool IsCustom { get; }
        }

        private readonly byte[] _arm9;
        private readonly uint _commonCountOffset;
        private readonly uint _commonPointerOffset;
        private readonly uint _specialtyPointerOffset;
        private readonly int _vanillaSpecialtyShopCount;
        private readonly int _commonDataOffset;
        private readonly int[] _specialtyDataOffsets;
        private readonly int _originalCommonCount;
        private int _existingExpansionStart = -1;
        private int _existingExpansionLength;

        public List<CommonEntry> CommonItems { get; } = new();
        public List<SpecialtyShop> SpecialtyShops { get; } = new();
        public bool ExpansionAvailable { get; }
        public bool HasSizeChanges => CommonItems.Count != _originalCommonCount
            || SpecialtyShops.Count != _specialtyDataOffsets.Length
            || SpecialtyShops.Where((shop, i) => i < _specialtyDataOffsets.Length)
                .Any(shop => shop.Items.Count != shop.OriginalCapacity);

        public static MartData LoadCurrent()
        {
            if (!RomInfo.IsMartEditorAvailable())
                throw new InvalidOperationException("The Mart Editor is not available for this ROM.");

            if (ARM9.CheckCompressionMark() && !ARM9.Decompress(RomInfo.arm9Path))
                throw new InvalidOperationException("The ARM9 could not be decompressed.");

            DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.synthOverlay });
            bool expanded = (RomPatchState.flag_arm9Expanded || PatchToolboxLogic.CheckFilesArm9ExpansionApplied())
                && File.Exists(Filesystem.expArmPath)
                && new FileInfo(Filesystem.expArmPath).Length >= 0x16000;

            return new MartData(
                File.ReadAllBytes(RomInfo.arm9Path), expanded ? File.ReadAllBytes(Filesystem.expArmPath) : null,
                RomInfo.martCommonCountOffset,
                RomInfo.martCommonPointerOffset,
                RomInfo.martSpecialtyPointerOffset,
                RomInfo.martSpecialtyShopCount,
                MartShopNames.ForFamily(RomInfo.gameFamily), expanded);
        }

        internal MartData(byte[] arm9, uint commonCountOffset, uint commonPointerOffset,
            uint specialtyPointerOffset, int specialtyShopCount, IReadOnlyList<string> specialtyNames)
            : this(arm9, null, commonCountOffset, commonPointerOffset, specialtyPointerOffset,
                specialtyShopCount, specialtyNames, false)
        {
        }

        internal MartData(byte[] arm9, byte[] syntheticOverlay, uint commonCountOffset, uint commonPointerOffset,
            uint specialtyPointerOffset, int specialtyShopCount, IReadOnlyList<string> specialtyNames,
            bool expansionAvailable)
        {
            _arm9 = arm9 != null ? (byte[])arm9.Clone() : throw new ArgumentNullException(nameof(arm9));
            syntheticOverlay = syntheticOverlay != null ? (byte[])syntheticOverlay.Clone() : null;
            _commonCountOffset = commonCountOffset;
            _commonPointerOffset = commonPointerOffset;
            _specialtyPointerOffset = specialtyPointerOffset;
            _vanillaSpecialtyShopCount = specialtyShopCount;
            ExpansionAvailable = expansionAvailable;
            if (specialtyShopCount <= 0)
                throw new InvalidOperationException("The mart layout has no specialty shops.");
            if (specialtyNames == null || specialtyNames.Count != specialtyShopCount)
                throw new InvalidOperationException("The mart shop-name table does not match the ROM layout.");

            EnsureRange(_arm9, commonCountOffset, 1, "common mart count");
            _originalCommonCount = _arm9[commonCountOffset];
            if (_originalCommonCount <= 0)
                throw new InvalidOperationException("The common mart table is empty.");

            DataLocation commonLocation = ReadPointer(_arm9, _arm9, syntheticOverlay, commonPointerOffset,
                checked(_originalCommonCount * 4), "common mart table");
            _commonDataOffset = commonLocation.IsArm9 ? commonLocation.Offset : -1;
            for (int i = 0; i < _originalCommonCount; i++)
            {
                int offset = commonLocation.Offset + i * 4;
                CommonItems.Add(new CommonEntry
                {
                    ItemId = BitConverter.ToUInt16(commonLocation.Data, offset),
                    RequiredTier = BitConverter.ToUInt16(commonLocation.Data, offset + 2),
                });
            }

            int actualSpecialtyCount = TryReadExpansionCount(commonLocation, specialtyShopCount);
            DataLocation pointerTable = ReadPointer(_arm9, _arm9, syntheticOverlay, specialtyPointerOffset,
                checked(actualSpecialtyCount * 4), "specialty mart pointer table");
            _specialtyDataOffsets = new int[actualSpecialtyCount];
            for (int i = 0; i < actualSpecialtyCount; i++)
            {
                DataLocation list = ReadPointer(pointerTable.Data, _arm9, syntheticOverlay,
                    checked((uint)(pointerTable.Offset + i * 4)), 2, $"specialty mart {i}");
                _specialtyDataOffsets[i] = list.IsArm9 ? list.Offset : -1;

                var items = new List<ushort>();
                for (int offset = list.Offset; ; offset += 2)
                {
                    EnsureRange(list.Data, (uint)offset, 2, $"specialty mart {i}");
                    ushort item = BitConverter.ToUInt16(list.Data, offset);
                    if (item == ushort.MaxValue) break;
                    items.Add(item);
                }
                string name = i < specialtyNames.Count ? specialtyNames[i] : $"Custom Mart {i}";
                SpecialtyShops.Add(new SpecialtyShop(i, name, items, i >= specialtyShopCount));
            }
        }

        public SpecialtyShop AddSpecialtyShop()
        {
            if (!ExpansionAvailable)
                throw new InvalidOperationException("Apply the ARM9 expansion patch before adding a mart.");
            int id = SpecialtyShops.Count;
            var shop = new SpecialtyShop(id, $"Custom Mart {id}", new List<ushort> { 1 }, true);
            SpecialtyShops.Add(shop);
            return shop;
        }

        public void RemoveLastSpecialtyShop()
        {
            if (SpecialtyShops.Count <= _vanillaSpecialtyShopCount)
                throw new InvalidOperationException("Only custom marts appended by DSPRE can be removed.");
            SpecialtyShops.RemoveAt(SpecialtyShops.Count - 1);
        }

        public bool SaveCurrent()
        {
            if (!HasSizeChanges && _commonDataOffset >= 0 && _specialtyDataOffsets.All(offset => offset >= 0))
                return SaveToFile(RomInfo.arm9Path, showSuccessMessage: false);
            if (!ExpansionAvailable)
                throw new InvalidOperationException("Apply the ARM9 expansion patch before changing mart sizes.");

            byte[] originalOverlay = File.ReadAllBytes(Filesystem.expArmPath);
            ExpandedFiles files = BuildExpandedFiles(originalOverlay,
                RomInfo.synthOverlayLoadAddress, GetRuntimeReservedRanges());
            WriteReplacementFile(Filesystem.expArmPath, files.SyntheticOverlay);
            try
            {
                WriteReplacementFile(RomInfo.arm9Path, files.Arm9);
            }
            catch
            {
                WriteReplacementFile(Filesystem.expArmPath, originalOverlay);
                throw;
            }
            return true;
        }

        internal sealed class ExpandedFiles
        {
            public byte[] Arm9 { get; init; }
            public byte[] SyntheticOverlay { get; init; }
            public int BlockOffset { get; init; }
        }

        internal ExpandedFiles BuildExpandedFiles(byte[] syntheticOverlay, uint loadAddress,
            IReadOnlyList<(long Start, long End)> reservedRanges = null)
        {
            ValidateInventories();
            if (syntheticOverlay == null) throw new ArgumentNullException(nameof(syntheticOverlay));

            int commonOffset = ExpansionHeaderSize;
            int pointerTableOffset = Align(commonOffset + CommonItems.Count * 4, 4);
            int cursor = pointerTableOffset + SpecialtyShops.Count * 4;
            int[] listOffsets = new int[SpecialtyShops.Count];
            for (int i = 0; i < SpecialtyShops.Count; i++)
            {
                cursor = Align(cursor, 2);
                listOffsets[i] = cursor;
                cursor += (SpecialtyShops[i].Items.Count + 1) * 2;
            }

            byte[] block = new byte[cursor];
            Encoding.ASCII.GetBytes(ExpansionMarker).CopyTo(block, 0);
            WriteUInt32(block, 0x0C, 1);
            WriteUInt32(block, 0x10, (uint)block.Length);
            WriteUInt32(block, 0x14, (uint)SpecialtyShops.Count);
            WriteUInt32(block, 0x18, (uint)CommonItems.Count);

            for (int i = 0; i < CommonItems.Count; i++)
            {
                WriteUInt16(block, commonOffset + i * 4, CommonItems[i].ItemId);
                WriteUInt16(block, commonOffset + i * 4 + 2, CommonItems[i].RequiredTier);
            }

            var excluded = new List<(long Start, long End)>(reservedRanges ?? Array.Empty<(long, long)>());
            AddExistingExpansionRanges(syntheticOverlay, excluded);
            int blockOffset = _existingExpansionStart >= 0 && block.Length <= _existingExpansionLength
                ? _existingExpansionStart
                : FindFreeRegion(syntheticOverlay, block.Length, 4, excluded);
            if (blockOffset < 0)
                throw new InvalidOperationException("No safe free space was found in the synthetic overlay for the mart tables.");

            for (int i = 0; i < SpecialtyShops.Count; i++)
            {
                WriteUInt32(block, pointerTableOffset + i * 4,
                    checked(loadAddress + (uint)blockOffset + (uint)listOffsets[i]));
                for (int j = 0; j < SpecialtyShops[i].Items.Count; j++)
                    WriteUInt16(block, listOffsets[i] + j * 2, SpecialtyShops[i].Items[j]);
                WriteUInt16(block, listOffsets[i] + SpecialtyShops[i].Items.Count * 2, ushort.MaxValue);
            }

            byte[] newOverlay = (byte[])syntheticOverlay.Clone();
            if (blockOffset == _existingExpansionStart && _existingExpansionLength > 0)
                Array.Clear(newOverlay, blockOffset, _existingExpansionLength);
            block.CopyTo(newOverlay, blockOffset);
            byte[] newArm9 = (byte[])_arm9.Clone();
            newArm9[_commonCountOffset] = (byte)CommonItems.Count;
            WriteUInt32(newArm9, checked((int)_commonPointerOffset),
                checked(loadAddress + (uint)blockOffset + (uint)commonOffset));
            WriteUInt32(newArm9, checked((int)_specialtyPointerOffset),
                checked(loadAddress + (uint)blockOffset + (uint)pointerTableOffset));
            return new ExpandedFiles { Arm9 = newArm9, SyntheticOverlay = newOverlay, BlockOffset = blockOffset };
        }

        public override byte[] ToByteArray()
        {
            ValidateInventories();
            if (CommonItems.Count != _originalCommonCount)
                throw new InvalidOperationException("Changing the common mart table size is not supported yet.");

            byte[] result = (byte[])_arm9.Clone();
            for (int i = 0; i < CommonItems.Count; i++)
            {
                CommonEntry entry = CommonItems[i];
                WriteUInt16(result, _commonDataOffset + i * 4, entry.ItemId);
                WriteUInt16(result, _commonDataOffset + i * 4 + 2, entry.RequiredTier);
            }

            for (int shopIndex = 0; shopIndex < SpecialtyShops.Count; shopIndex++)
            {
                SpecialtyShop shop = SpecialtyShops[shopIndex];
                if (shop.Items.Count != shop.OriginalCapacity)
                    throw new InvalidOperationException(
                        $"Changing the size of {shop.Name} is not supported yet.");

                int offset = _specialtyDataOffsets[shopIndex];
                for (int itemIndex = 0; itemIndex < shop.Items.Count; itemIndex++)
                {
                    ushort item = shop.Items[itemIndex];
                    WriteUInt16(result, offset + itemIndex * 2, item);
                }
                WriteUInt16(result, offset + shop.Items.Count * 2, ushort.MaxValue);
            }

            return result;
        }

        private void ValidateInventories()
        {
            if (CommonItems.Count < 1 || CommonItems.Count > MaxCommonItems)
                throw new InvalidOperationException($"The common mart must contain between 1 and {MaxCommonItems} items.");
            if (SpecialtyShops.Count < _vanillaSpecialtyShopCount)
                throw new InvalidOperationException("Vanilla specialty marts cannot be removed.");
            foreach (CommonEntry entry in CommonItems)
            {
                if (entry.ItemId == 0 || entry.ItemId == ushort.MaxValue)
                    throw new InvalidOperationException("Mart item IDs must be between 1 and 0xFFFE.");
                if (entry.RequiredTier < 1 || entry.RequiredTier > 6)
                    throw new InvalidOperationException("Common mart stock tiers must be between 1 and 6.");
            }
            foreach (SpecialtyShop shop in SpecialtyShops)
            {
                if (shop.Items.Count < 1)
                    throw new InvalidOperationException($"{shop.Name} must contain at least one item.");
                if (shop.Items.Any(item => item == 0 || item == ushort.MaxValue))
                    throw new InvalidOperationException("Mart item IDs must be between 1 and 0xFFFE.");
            }
        }

        private int TryReadExpansionCount(DataLocation commonLocation, int fallback)
        {
            if (commonLocation.IsArm9 || commonLocation.Offset < ExpansionHeaderSize) return fallback;
            int header = commonLocation.Offset - ExpansionHeaderSize;
            if (!HasMarker(commonLocation.Data, header)) return fallback;
            EnsureRange(commonLocation.Data, (uint)header, ExpansionHeaderSize, "expanded mart header");
            uint version = BitConverter.ToUInt32(commonLocation.Data, header + 0x0C);
            uint storedLength = BitConverter.ToUInt32(commonLocation.Data, header + 0x10);
            uint storedCommonCount = BitConverter.ToUInt32(commonLocation.Data, header + 0x18);
            uint storedSpecialtyCount = BitConverter.ToUInt32(commonLocation.Data, header + 0x14);
            if (version != 1 || storedLength < ExpansionHeaderSize
                || (long)header + storedLength > commonLocation.Data.Length
                || storedCommonCount != _originalCommonCount
                || storedSpecialtyCount < fallback || storedSpecialtyCount > int.MaxValue)
                throw new InvalidOperationException("The expanded mart metadata is invalid.");
            _existingExpansionStart = header;
            _existingExpansionLength = (int)storedLength;
            return (int)storedSpecialtyCount;
        }

        private static DataLocation ReadPointer(byte[] pointerData, byte[] arm9, byte[] syntheticOverlay,
            uint pointerOffset, int requiredLength, string label)
        {
            EnsureRange(pointerData, pointerOffset, 4, label + " pointer");
            uint address = BitConverter.ToUInt32(pointerData, checked((int)pointerOffset));
            if (address >= RomInfo.synthOverlayLoadAddress && syntheticOverlay != null)
            {
                ulong offset = address - RomInfo.synthOverlayLoadAddress;
                if (offset + (ulong)requiredLength <= (ulong)syntheticOverlay.Length)
                    return new DataLocation(syntheticOverlay, (int)offset, false);
            }
            if (address >= ARM9.address)
            {
                ulong offset = address - ARM9.address;
                if (offset + (ulong)requiredLength <= (ulong)arm9.Length)
                    return new DataLocation(arm9, (int)offset, true);
            }
            throw new InvalidOperationException($"The {label} pointer is outside supported ARM9 memory.");
        }

        private readonly struct DataLocation
        {
            public DataLocation(byte[] data, int offset, bool isArm9) { Data = data; Offset = offset; IsArm9 = isArm9; }
            public byte[] Data { get; }
            public int Offset { get; }
            public bool IsArm9 { get; }
        }

        private static IReadOnlyList<(long Start, long End)> GetRuntimeReservedRanges()
        {
            var ranges = new List<(long, long)>();
            OverworldSpriteTableExpansion.Detect();
            var ow = OverworldSpriteTableExpansion.GetReservedByteRange();
            if (ow.HasValue) ranges.Add(ow.Value);
            return ranges;
        }

        private static void AddExistingExpansionRanges(byte[] data, List<(long Start, long End)> ranges)
        {
            byte[] marker = Encoding.ASCII.GetBytes(ExpansionMarker);
            foreach (int hit in DSUtils.SearchBytes(data, marker))
            {
                if (hit + ExpansionHeaderSize > data.Length) continue;
                uint length = BitConverter.ToUInt32(data, hit + 0x10);
                if (length >= ExpansionHeaderSize && (long)hit + length <= data.Length)
                    ranges.Add((hit, hit + length));
            }
        }

        private static int FindFreeRegion(byte[] data, int length, int alignment,
            IReadOnlyList<(long Start, long End)> excluded)
        {
            for (int offset = 0; offset + length <= data.Length; offset += alignment)
            {
                if (excluded.Any(range => offset + length > range.Start && offset < range.End)) continue;
                bool clear = true;
                for (int i = 0; i < length; i++)
                {
                    if (data[offset + i] != 0) { clear = false; break; }
                }
                if (clear) return offset;
            }
            return -1;
        }

        private static bool HasMarker(byte[] data, int offset)
        {
            byte[] marker = Encoding.ASCII.GetBytes(ExpansionMarker);
            return offset >= 0 && offset + marker.Length <= data.Length
                && marker.SequenceEqual(data.Skip(offset).Take(marker.Length));
        }

        private static int Align(int value, int alignment) => (value + alignment - 1) & ~(alignment - 1);

        private static void WriteReplacementFile(string path, byte[] data)
        {
            string temp = path + ".mart.tmp";
            try
            {
                File.WriteAllBytes(temp, data);
                File.Move(temp, path, true);
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
        }

        private static void EnsureRange(byte[] data, uint offset, int length, string label)
        {
            if ((ulong)offset + (ulong)length > (ulong)data.Length)
                throw new InvalidOperationException($"The {label} is outside its data file.");
        }

        private static void WriteUInt16(byte[] data, int offset, ushort value)
        {
            data[offset] = (byte)value;
            data[offset + 1] = (byte)(value >> 8);
        }

        private static void WriteUInt32(byte[] data, int offset, uint value)
        {
            data[offset] = (byte)value;
            data[offset + 1] = (byte)(value >> 8);
            data[offset + 2] = (byte)(value >> 16);
            data[offset + 3] = (byte)(value >> 24);
        }
    }

    internal static class MartShopNames
    {
        private static readonly string[] Dp =
        {
            "Jubilife Secondary", "Oreburgh Secondary", "Floaroma Secondary",
            "Eterna Secondary", "Eterna Herbs", "Hearthome Secondary",
            "Solaceon Secondary", "Pastoria Secondary",
            "Veilstone Department Store 1F (Right)", "Veilstone Department Store 1F (Left)",
            "Veilstone Department Store 2F (Top)", "Veilstone Department Store 2F (Middle)",
            "Veilstone Department Store 3F (Top)", "Veilstone Department Store 3F (Bottom)",
            "Celestic Secondary", "Snowpoint Secondary", "Canalave Secondary",
            "Sunyshore Secondary", "Pokémon League Secondary",
        };

        private static readonly string[] Hgss =
        {
            "Cherrygrove Secondary", "Violet Secondary", "Azalea Secondary",
            "Goldenrod Department Store 2F (Lower)", "Goldenrod Department Store 2F (Upper)",
            "Goldenrod Department Store 3F", "Goldenrod Department Store 4F",
            "Goldenrod Department Store 5F", "Goldenrod Herb Shop", "Ecruteak Secondary",
            "Olivine Secondary", "Cianwood Pharmacy", "Blackthorn Secondary", "Unused Secondary",
            "Safari Zone Gate Southwest", "Saffron Secondary", "Lavender Secondary",
            "Cerulean Secondary", "Celadon Department Store 2F (Left)",
            "Celadon Department Store 2F (Right)", "Celadon Department Store 3F",
            "Celadon Department Store 4F", "Celadon Department Store 5F (Left)",
            "Celadon Department Store 5F (Right)", "Fuchsia Secondary", "Pewter Secondary",
            "Viridian Secondary", "Mt. Moon Square", "Mahogany Before Hideout",
            "Mahogany After Hideout",
        };

        public static IReadOnlyList<string> ForFamily(RomInfo.GameFamilies family)
        {
            if (family == RomInfo.GameFamilies.DP) return Dp;
            if (family == RomInfo.GameFamilies.Plat)
            {
                var names = new string[Dp.Length + 1];
                Array.Copy(Dp, names, Dp.Length);
                names[^1] = "Veilstone Department Store B1F Berries";
                return names;
            }
            if (family == RomInfo.GameFamilies.HGSS) return Hgss;
            throw new InvalidOperationException("This game does not use the supported mart layout.");
        }
    }
}
