using System;

namespace DSPRE
{
    /// <summary>Where a new game starts: the header and tile in ARM9, and the starting money in its overlay.</summary>
    public sealed class SpawnPoint
    {
        public ushort Header;

        /// <summary>Global tile coordinates, 32 tiles to a matrix cell.</summary>
        public ushort GlobalX, GlobalY;

        public ushort Direction;
        public uint Money;

        public static SpawnPoint Read() => new SpawnPoint
        {
            Header = BitConverter.ToUInt16(ARM9.ReadBytes(RomInfo.arm9spawnOffset, 2), 0),
            GlobalX = BitConverter.ToUInt16(ARM9.ReadBytes(RomInfo.arm9spawnOffset + 8, 2), 0),
            GlobalY = BitConverter.ToUInt16(ARM9.ReadBytes(RomInfo.arm9spawnOffset + 12, 2), 0),
            Direction = BitConverter.ToUInt16(ARM9.ReadBytes(RomInfo.arm9spawnOffset + 16, 2), 0),
            Money = ReadMoney(),
        };

        public static uint ReadMoney() =>
            BitConverter.ToUInt32(DSUtils.ReadFromFile(MoneyOverlay(), RomInfo.initialMoneyOverlayOffset, 4), 0);

        public void Write()
        {
            ARM9.WriteBytes(BitConverter.GetBytes(Header), RomInfo.arm9spawnOffset);
            ARM9.WriteBytes(BitConverter.GetBytes(GlobalX), RomInfo.arm9spawnOffset + 8);
            ARM9.WriteBytes(BitConverter.GetBytes(GlobalY), RomInfo.arm9spawnOffset + 12);
            ARM9.WriteBytes(BitConverter.GetBytes(Direction), RomInfo.arm9spawnOffset + 16);
            DSUtils.WriteToFile(MoneyOverlay(), BitConverter.GetBytes(Money), RomInfo.initialMoneyOverlayOffset);
        }

        private static string MoneyOverlay()
        {
            if (OverlayUtils.IsStillCompressed(RomInfo.initialMoneyOverlayNumber))
                OverlayUtils.Decompress(RomInfo.initialMoneyOverlayNumber);
            return OverlayUtils.GetPath(RomInfo.initialMoneyOverlayNumber);
        }
    }
}
