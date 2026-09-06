using System;
using System.Collections.Generic;
using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests.Items
{
    public class MartDataTests
    {
        private const uint CommonCountOffset = 0x10;
        private const uint CommonPointerOffset = 0x20;
        private const uint SpecialtyPointerOffset = 0x30;

        [Fact]
        public void ParsesCommonAndSpecialtyInventories()
        {
            byte[] arm9 = CreateArm9();

            var marts = new MartData(
                arm9, CommonCountOffset, CommonPointerOffset, SpecialtyPointerOffset,
                2, new[] { "First", "Second" });

            Assert.Collection(marts.CommonItems,
                row => { Assert.Equal((ushort)4, row.ItemId); Assert.Equal((ushort)1, row.RequiredTier); },
                row => { Assert.Equal((ushort)3, row.ItemId); Assert.Equal((ushort)3, row.RequiredTier); });
            Assert.Collection(marts.SpecialtyShops,
                shop => Assert.Equal(new ushort[] { 17, 18 }, shop.Items),
                shop => Assert.Equal(new ushort[] { 25 }, shop.Items));
            Assert.Equal("First", marts.SpecialtyShops[0].Name);
        }

        [Fact]
        public void UnchangedRoundTripPreservesEveryArm9Byte()
        {
            byte[] arm9 = CreateArm9();
            var marts = CreateMarts(arm9);

            Assert.Equal(arm9, marts.ToByteArray());
        }

        [Fact]
        public void WritesEditedEntriesWithoutMovingTables()
        {
            var marts = CreateMarts(CreateArm9());
            marts.CommonItems[0].ItemId = 99;
            marts.CommonItems[0].RequiredTier = 6;
            marts.SpecialtyShops[1].Items[0] = 77;

            byte[] saved = marts.ToByteArray();

            Assert.Equal((ushort)99, BitConverter.ToUInt16(saved, 0x100));
            Assert.Equal((ushort)6, BitConverter.ToUInt16(saved, 0x102));
            Assert.Equal((ushort)77, BitConverter.ToUInt16(saved, 0x320));
            Assert.Equal(ushort.MaxValue, BitConverter.ToUInt16(saved, 0x322));
        }

        [Fact]
        public void RejectsSpecialtyListSizeChanges()
        {
            var marts = CreateMarts(CreateArm9());
            marts.SpecialtyShops[0].Items.Add(19);

            Assert.Throws<InvalidOperationException>(() => marts.ToByteArray());
        }

        [Fact]
        public void RejectsPointersOutsideArm9()
        {
            byte[] arm9 = CreateArm9();
            WriteUInt32(arm9, CommonPointerOffset, 0x01000000);

            Assert.Throws<InvalidOperationException>(() => CreateMarts(arm9));
        }

        [Fact]
        public void ExpandedBlockRoundTripsResizedInventoriesAndCustomMartIds()
        {
            var marts = CreateExpandableMarts();
            marts.CommonItems.Add(new MartData.CommonEntry { ItemId = 50, RequiredTier = 4 });
            marts.SpecialtyShops[0].Items.Add(19);
            MartData.SpecialtyShop custom = marts.AddSpecialtyShop();
            custom.Items[0] = 77;

            MartData.ExpandedFiles files = marts.BuildExpandedFiles(new byte[0x1000], RomInfo.synthOverlayLoadAddress);
            var reloaded = new MartData(files.Arm9, files.SyntheticOverlay,
                CommonCountOffset, CommonPointerOffset, SpecialtyPointerOffset,
                2, new[] { "First", "Second" }, true);

            Assert.Equal(3, reloaded.CommonItems.Count);
            Assert.Equal((ushort)50, reloaded.CommonItems[2].ItemId);
            Assert.Equal(new ushort[] { 17, 18, 19 }, reloaded.SpecialtyShops[0].Items);
            Assert.Equal(3, reloaded.SpecialtyShops.Count);
            Assert.Equal(2, reloaded.SpecialtyShops[2].Id);
            Assert.True(reloaded.SpecialtyShops[2].IsCustom);
            Assert.Equal((ushort)77, reloaded.SpecialtyShops[2].Items[0]);
        }

        [Fact]
        public void ExpandedBlockAvoidsReservedSyntheticOverlayRanges()
        {
            var marts = CreateExpandableMarts();
            var reserved = new List<(long Start, long End)> { (0, 0x300) };

            MartData.ExpandedFiles files = marts.BuildExpandedFiles(
                new byte[0x1000], RomInfo.synthOverlayLoadAddress, reserved);

            Assert.True(files.BlockOffset >= 0x300);
        }

        [Fact]
        public void CommonMartExpansionIsCappedForTheGamesFixedRuntimeBuffer()
        {
            var marts = CreateExpandableMarts();
            while (marts.CommonItems.Count < 64)
                marts.CommonItems.Add(new MartData.CommonEntry { ItemId = 1, RequiredTier = 1 });

            Assert.Throws<InvalidOperationException>(() =>
                marts.BuildExpandedFiles(new byte[0x1000], RomInfo.synthOverlayLoadAddress));
        }

        [Fact]
        public void AddingCustomMartRequiresArm9Expansion()
        {
            MartData marts = CreateMarts(CreateArm9());

            Assert.Throws<InvalidOperationException>(() => marts.AddSpecialtyShop());
        }

        private static MartData CreateMarts(byte[] arm9) => new(
            arm9, CommonCountOffset, CommonPointerOffset, SpecialtyPointerOffset,
            2, new[] { "First", "Second" });

        private static MartData CreateExpandableMarts() => new(
            CreateArm9(), new byte[0x1000], CommonCountOffset, CommonPointerOffset,
            SpecialtyPointerOffset, 2, new[] { "First", "Second" }, true);

        private static byte[] CreateArm9()
        {
            byte[] data = new byte[0x400];
            data[CommonCountOffset] = 2;
            WriteUInt32(data, CommonPointerOffset, ARM9.address + 0x100);
            WriteUInt32(data, SpecialtyPointerOffset, ARM9.address + 0x200);

            WriteUInt16(data, 0x100, 4);
            WriteUInt16(data, 0x102, 1);
            WriteUInt16(data, 0x104, 3);
            WriteUInt16(data, 0x106, 3);

            WriteUInt32(data, 0x200, ARM9.address + 0x300);
            WriteUInt32(data, 0x204, ARM9.address + 0x320);
            WriteUInt16(data, 0x300, 17);
            WriteUInt16(data, 0x302, 18);
            WriteUInt16(data, 0x304, ushort.MaxValue);
            WriteUInt16(data, 0x320, 25);
            WriteUInt16(data, 0x322, ushort.MaxValue);
            return data;
        }

        private static void WriteUInt16(byte[] data, int offset, ushort value) =>
            Array.Copy(BitConverter.GetBytes(value), 0, data, offset, 2);

        private static void WriteUInt32(byte[] data, uint offset, uint value) =>
            Array.Copy(BitConverter.GetBytes(value), 0, data, offset, 4);
    }
}
