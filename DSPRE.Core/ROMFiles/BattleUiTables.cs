using System;
using System.IO;
using static DSPRE.RomInfo;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// Read-only tables the battle menus and summary icons take from code rather than from archives. Every
    /// reader returns null when the table is not located for this ROM or does not look like the game's.
    /// </summary>
    public static class BattleUiTables
    {
        /// <summary>18 types, then Cool, Beauty, Cute, Smart and Tough.</summary>
        public const int IconCount = 23;
        public const int TypeCount = 18, CategoryCount = 3, PaletteBanks = 3;

        public sealed class IconTables
        {
            public byte[] TypeBanks;      // IconCount entries
            public byte[] CategoryBanks;  // physical, special, status
            public uint[] TypeMembers;    // battle object archive member of each type or contest icon
        }

        public static IconTables ReadIconTables()
        {
            byte[] banks = TryRead(GameTable.TypeIconBanks, IconCount);
            byte[] kinds = TryRead(GameTable.MoveCategoryIconBanks, CategoryCount);
            byte[] members = TryRead(GameTable.TypeIconMembers, IconCount * 4);
            if (banks == null || kinds == null || members == null) return null;
            if (Array.Exists(banks, b => b >= PaletteBanks) || Array.Exists(kinds, b => b >= PaletteBanks)) return null;
            uint[] ids = new uint[IconCount];
            for (int i = 0; i < IconCount; i++) ids[i] = BitConverter.ToUInt32(members, i * 4);
            return new IconTables { TypeBanks = banks, CategoryBanks = kinds, TypeMembers = ids };
        }

        /// <summary>The 16 BGR555 colours the game paints a move button with for a move of this type.</summary>
        public static ushort[] MoveButtonPalette(int type)
        {
            if (type < 0 || type >= TypeCount) return null;
            try
            {
                TableSpot? spot = SpotOf(GameTable.MoveTypeButtonPalettes);
                if (spot == null || GameTableFile.WhyNot(GameTable.MoveTypeButtonPalettes, TypeCount * 4) != null) return null;
                // A preview must not rewrite the project, so a still-compressed overlay is not unpacked here.
                if (spot.Value.Overlay >= 0 && OverlayUtils.IsStillCompressed(spot.Value.Overlay)) return null;

                byte[] file = File.ReadAllBytes(GameTableFile.PathOf(spot.Value));
                uint ramBase = spot.Value.Overlay >= 0 ? OverlayUtils.OverlayTable.GetRAMAddress(spot.Value.Overlay) : ARM9.address;
                uint pointer = BitConverter.ToUInt32(file, spot.Value.Offset + type * 4);
                long at = (long)pointer - ramBase;
                if ((pointer & 1) != 0 || at < 0 || at + 32 > file.Length) return null;

                ushort[] colours = new ushort[16];
                for (int i = 0; i < 16; i++) colours[i] = BitConverter.ToUInt16(file, (int)at + i * 2);
                return colours;
            }
            catch (Exception e) when (e is IOException || e is InvalidOperationException || e is UnauthorizedAccessException)
            {
                return null;
            }
        }

        private static byte[] TryRead(GameTable table, int length)
        {
            try { return GameTableFile.WhyNot(table, length) == null ? GameTableFile.Read(table, length) : null; }
            catch (Exception e) when (e is IOException || e is InvalidOperationException || e is UnauthorizedAccessException)
            {
                return null;
            }
        }
    }
}
