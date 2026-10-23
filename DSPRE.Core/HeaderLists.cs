using System.Collections.Generic;
using System.Text;
using DSPRE.ROMFiles;

namespace DSPRE
{
    /// <summary>
    /// Header list names and internal names.
    /// </summary>
    public static class HeaderLists
    {
        public static List<string> GetHeaderListBoxNames()
        {
            if (string.IsNullOrWhiteSpace(RomInfo.InternalNamesPath))
            {
                return null;
            }

            List<string> headerListBoxNames = new List<string>();

            using (DSUtils.EasyReader reader = new DSUtils.EasyReader(RomInfo.InternalNamesPath))
            {
                int headerCount = RomInfo.GetHeaderCount();
                for (int i = 0; i < headerCount; i++)
                {
                    byte[] row = reader.ReadBytes(RomInfo.InternalNameLength);
                    string internalName = Encoding.ASCII.GetString(row);
                    headerListBoxNames.Add(MapHeader.BuildName(i, internalName));
                }
            }

            return headerListBoxNames;
        }

        public static List<string> GetInternalNames()
        {
            List<string> internalNames = new List<string>();

            using (DSUtils.EasyReader reader = new DSUtils.EasyReader(RomInfo.InternalNamesPath))
            {
                int headerCount = RomInfo.GetHeaderCount();
                for (int i = 0; i < headerCount; i++)
                {
                    byte[] row = reader.ReadBytes(RomInfo.InternalNameLength);
                    string internalName = Encoding.ASCII.GetString(row);
                    internalNames.Add(internalName.TrimEnd('\0'));
                }
            }

            return internalNames;
        }
    }
}
