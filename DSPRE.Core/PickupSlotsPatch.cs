using System;
using System.IO;
using DSPRE.ROMFiles;

namespace DSPRE
{
    /// <summary>
    /// Gives every Pickup level range its own items. The game reads range r's nine common items from one sliding list of
    /// 18 (index r + slot) and its two rare items from a sliding list of 11. The patch turns the range clamp, which a level
    /// of 1 to 100 never reaches, into r * 9 and points both reads at 10 x 9 tables in the synthetic overlay, filled from
    /// the sliding lists so the game gives the same items until a cell is edited.
    /// </summary>
    public static class PickupSlotsPatch
    {
        public const string Marker = "PICKUPSLOTS1";
        public const int Stride = 9;
        /// <summary>One table's length in bytes: ten ranges of nine u16 ids.</summary>
        public const int TableLength = PickupTable.Ranges * Stride * 2;
        public const int BlockLength = SyntheticOverlaySpace.HeaderSize + TableLength * 2;

        public static bool SupportsCurrentRom => RomLocator.PickupSlotSites != null && RomInfo.PickupTableOverlayNumber >= 0;

        private static int OverlayNumber => RomInfo.PickupTableOverlayNumber;
        private static uint OverlayAddress => OverlayUtils.OverlayTable.GetRAMAddress(OverlayNumber);

        // cmp rN, #10; bcc +0; movs rN, #9
        private static byte[] Clamp(int reg) => new byte[] { 0x0A, (byte)(0x28 | reg), 0x00, 0xD3, 0x09, (byte)(0x20 | reg) };
        // movs r0, #9; muls rN, r0; nop. r0 is overwritten before it is next read.
        private static byte[] Multiply(int reg) => new byte[] { 0x09, 0x20, (byte)(0x40 | reg), 0x43, 0xC0, 0x46 };

        private static byte[] ReadOverlay()
        {
            if (OverlayUtils.IsCompressed(OverlayNumber)) OverlayUtils.Decompress(OverlayNumber);
            return File.ReadAllBytes(OverlayUtils.GetPath(OverlayNumber));
        }

        /// <summary>Where the patch's common table sits in the synthetic overlay, or -1 when the patch is not applied.</summary>
        public static int CommonTableOffset()
        {
            if (!SupportsCurrentRom || !File.Exists(Filesystem.ExpArmPath)) return -1;
            try
            {
                RomInfo.PickupSlotSites s = RomLocator.PickupSlotSites;
                byte[] ov = ReadOverlay();
                if (!ov.AsSpan((int)s.Clamp, 6).SequenceEqual(Multiply(s.Register))) return -1;
                uint common = BitConverter.ToUInt32(ov, (int)s.CommonLiteral), rare = BitConverter.ToUInt32(ov, (int)s.RareLiteral);
                long at = (long)common - RomInfo.SynthOverlayLoadAddress;
                if (at < 0 || rare != common + TableLength || at + TableLength * 2 > new FileInfo(Filesystem.ExpArmPath).Length) return -1;
                return (int)at;
            }
            catch (Exception ex) when (ex is IOException || ex is ArgumentException) { return -1; }
        }

        public static bool IsApplied => CommonTableOffset() >= 0;

        /// <summary>Why the patch can't go in now, or null.</summary>
        public static string WhyNotApplicable()
        {
            if (!SupportsCurrentRom) return "DSPRE doesn't know where this ROM's Pickup code is.";
            if (!SyntheticOverlaySpace.Available()) return "Apply the ARM9 expansion first.";
            RomInfo.PickupSlotSites s = RomLocator.PickupSlotSites;
            byte[] ov = ReadOverlay();
            bool vanilla = ov.AsSpan((int)s.Clamp, 6).SequenceEqual(Clamp(s.Register))
                && BitConverter.ToUInt32(ov, (int)s.CommonLiteral) == OverlayAddress + RomInfo.PickupCommonItemsOffset
                && BitConverter.ToUInt32(ov, (int)s.RareLiteral) == OverlayAddress + RomInfo.PickupRareItemsOffset;
            return vanilla ? null : "Something else has already changed the Pickup code, so DSPRE leaves it alone.";
        }

        /// <summary>Writes the tables, filled from the sliding lists, into a marked block at <paramref name="blockOffset"/> and points the code at them.</summary>
        public static void Apply(int blockOffset)
        {
            PickupTable now = PickupTable.Read();
            ushort[] common = new ushort[TableLength / 2], rare = new ushort[TableLength / 2];
            for (int r = 0; r < PickupTable.Ranges; r++)
            {
                for (int j = 0; j < PickupTable.CommonSlots; j++) common[r * Stride + j] = now.Common[r + j];
                for (int k = 0; k < PickupTable.RareSlots; k++) rare[r * Stride + k] = now.Rare[r + k];
            }

            byte[] block = new byte[BlockLength];
            System.Text.Encoding.ASCII.GetBytes(Marker).CopyTo(block, 0);
            BitConverter.GetBytes(1u).CopyTo(block, 0x0C);
            BitConverter.GetBytes((uint)BlockLength).CopyTo(block, 0x10);
            PickupTable.Words(common, common.Length).CopyTo(block, SyntheticOverlaySpace.HeaderSize);
            PickupTable.Words(rare, rare.Length).CopyTo(block, SyntheticOverlaySpace.HeaderSize + TableLength);
            DSUtils.WriteToFile(Filesystem.ExpArmPath, block, (uint)blockOffset);

            RomInfo.PickupSlotSites s = RomLocator.PickupSlotSites;
            uint tables = RomInfo.SynthOverlayLoadAddress + (uint)blockOffset + SyntheticOverlaySpace.HeaderSize;
            string path = OverlayUtils.GetPath(OverlayNumber);
            DSUtils.WriteToFile(path, BitConverter.GetBytes(tables), s.CommonLiteral);
            DSUtils.WriteToFile(path, BitConverter.GetBytes(tables + TableLength), s.RareLiteral);
            DSUtils.WriteToFile(path, Multiply(s.Register), s.Clamp);
        }
    }
}
