using System.Collections.Generic;
using System.Text;
using DSPRE.ROMFiles;

namespace DSPRE
{
    /// <summary>
    /// Header list-box / internal-name readers (extracted from the WinForms <c>Helpers</c> so the
    /// cross-platform shells can build header lists without a WinForms dependency).
    /// </summary>
    public static class HeaderLists
    {
        public static List<string> GetHeaderListBoxNames()
        {
            if (string.IsNullOrWhiteSpace(RomInfo.internalNamesPath))
            {
                return null;
            }

            List<string> headerListBoxNames = new List<string>();

            using (DSUtils.EasyReader reader = new DSUtils.EasyReader(RomInfo.internalNamesPath))
            {
                int headerCount = RomInfo.GetHeaderCount();
                for (int i = 0; i < headerCount; i++)
                {
                    byte[] row = reader.ReadBytes(RomInfo.internalNameLength);
                    string internalName = Encoding.ASCII.GetString(row); //.TrimEnd();
                    headerListBoxNames.Add(MapHeader.BuildName(i, internalName));
                }
            }

            return headerListBoxNames;
        }

        public static List<string> GetInternalNames()
        {
            List<string> internalNames = new List<string>();

            using (DSUtils.EasyReader reader = new DSUtils.EasyReader(RomInfo.internalNamesPath))
            {
                int headerCount = RomInfo.GetHeaderCount();
                for (int i = 0; i < headerCount; i++)
                {
                    byte[] row = reader.ReadBytes(RomInfo.internalNameLength);
                    string internalName = Encoding.ASCII.GetString(row); //.TrimEnd();
                    internalNames.Add(internalName.TrimEnd('\0'));
                }
            }

            return internalNames;
        }
    }
}
