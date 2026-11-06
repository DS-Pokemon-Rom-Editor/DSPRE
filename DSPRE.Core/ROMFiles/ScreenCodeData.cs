using System;
using System.Collections.Generic;
using System.IO;

namespace DSPRE.ROMFiles
{
    /// <summary>Screen data the game keeps in its code, read for previews. Nothing here unpacks or changes the project.</summary>
    public static class ScreenCodeData
    {
        public const int NamingRows = 5, NamingRowLength = 20;

        /// <summary>A naming screen page's key rows as character codes, read through the arm9 pointer table. Control codes (0xD001 and
        /// up) read as 0. Null when the table isn't known here.</summary>
        public static ushort[][] NamingKeys(int page)
        {
            if (RomLocator.NamingKeyTable is not uint table || !File.Exists(RomInfo.Arm9Path)) return null;
            byte[] arm9 = File.ReadAllBytes(RomInfo.Arm9Path);
            ushort[][] rows = new ushort[NamingRows][];
            for (int r = 0; r < NamingRows; r++)
            {
                int at = (int)table + (page * NamingRows + r) * 4;
                if (at < 0 || at + 4 > arm9.Length) return null;
                long p = (long)BitConverter.ToUInt32(arm9, at) - ARM9.Address;
                List<ushort> codes = new List<ushort>();
                for (long o = p; o >= 0 && o + 2 <= arm9.Length && codes.Count < NamingRowLength; o += 2)
                {
                    ushort code = BitConverter.ToUInt16(arm9, (int)o);
                    if (code == 0xFFFF) break;
                    codes.Add(code >= 0xD001 ? (ushort)0 : code);
                }
                rows[r] = codes.ToArray();
            }
            return rows;
        }

        public const int ItemBoxSide = 5;

        /// <summary>Platinum's 5 by 5 item box entries as the bag writes them to BG1, shown or hidden, or null when unknown or the
        /// overlay is still compressed.</summary>
        public static ushort[] PtItemBox(bool shown)
        {
            if (RomLocator.PtBagItemBox is not { } box || OverlayUtils.IsStillCompressed(box.Overlay)) return null;
            string path = OverlayUtils.GetPath(box.Overlay);
            if (!File.Exists(path)) return null;
            byte[] code = File.ReadAllBytes(path);
            uint at = shown ? box.Shown : box.Hidden;
            ushort[] entries = new ushort[ItemBoxSide * ItemBoxSide];
            if (at + entries.Length * 2 > code.Length) return null;
            for (int i = 0; i < entries.Length; i++) entries[i] = BitConverter.ToUInt16(code, (int)at + 2 * i);
            return entries;
        }
    }
}
