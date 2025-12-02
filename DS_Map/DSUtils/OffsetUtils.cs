using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static DSPRE.RomInfo;

namespace DSPRE
{
    using OffsetDict = Dictionary<(GameFamilies, GameLanguages), OffsetUtils.Offset>;

    public static class OffsetUtils
    {
        public struct Offset
        {
            private uint versionA;
            private uint versionB;

            public Offset(uint versionA, uint versionB)
            {
                this.versionA = versionA;
                this.versionB = versionB;
            }

            public static implicit operator Offset(int offset)
            {
                return new Offset((uint)offset, (uint)offset);
            }

            public uint GetOffset()
            {
                if (gameVersion == GameVersions.Pearl || gameVersion == GameVersions.SoulSilver)
                {
                    return versionB;
                }
                else
                {
                    return versionA;
                }
            }
        }

        private static uint GetOffset(this OffsetDict dict)
        {
            return dict[(RomInfo.gameFamily, RomInfo.gameLanguage)].GetOffset();
        }

        public static uint headerTableOffset => headerTableOffsets.GetOffset();
        private static readonly OffsetDict headerTableOffsets = new OffsetDict()
        {
            // D/P
            { (GameFamilies.DP, GameLanguages.English), 0xEEDBC },
            { (GameFamilies.DP, GameLanguages.Spanish), 0xEEE08 },
            { (GameFamilies.DP, GameLanguages.Italian), 0xEED70 },
            { (GameFamilies.DP, GameLanguages.French), 0xEEDFC },
            { (GameFamilies.DP, GameLanguages.German), 0xEEDCC },
            { (GameFamilies.DP, GameLanguages.Japanese), new Offset(0xF0D68, 0xF0D6C) },

            // Plat
            { (GameFamilies.Plat, GameLanguages.English), 0xE601C },
            { (GameFamilies.Plat, GameLanguages.Spanish), 0xE60B0 },
            { (GameFamilies.Plat, GameLanguages.Italian), 0xE6038 },
            { (GameFamilies.Plat, GameLanguages.French), 0xE60A4 },
            { (GameFamilies.Plat, GameLanguages.German), 0xE6074 },
            { (GameFamilies.Plat, GameLanguages.Japanese), 0xE56F0 },

            // HG/SS
            { (GameFamilies.HGSS, GameLanguages.English), 0xF6BE0 },
            { (GameFamilies.HGSS, GameLanguages.Spanish), new Offset(0xF6BC8, 0xF6BD0) },
            { (GameFamilies.HGSS, GameLanguages.Italian), 0xF6B58 },
            { (GameFamilies.HGSS, GameLanguages.French), 0xF6BC4 },
            { (GameFamilies.HGSS, GameLanguages.German), 0xF6B94 },
            { (GameFamilies.HGSS, GameLanguages.Japanese), 0xF6390 },
        };

        public static uint genderTableOffset => genderTableOffsets.GetOffset();
        private static readonly OffsetDict genderTableOffsets = new OffsetDict()
        {
            // D/P
            { (GameFamilies.DP, GameLanguages.English), 0xF8010 },
            { (GameFamilies.DP, GameLanguages.Japanese), 0xF9F7C },
            { (GameFamilies.DP, GameLanguages.French), 0xF8054 },
            { (GameFamilies.DP, GameLanguages.German), 0xF8024 },
            { (GameFamilies.DP, GameLanguages.Italian), 0xF7FC8 },
            { (GameFamilies.DP, GameLanguages.Spanish), 0xF8060 },

            // Plat
            { (GameFamilies.Plat, GameLanguages.English), 0xF0714 },
            { (GameFamilies.Plat, GameLanguages.Japanese), 0xEFDA4 },
            { (GameFamilies.Plat, GameLanguages.French), 0xF079C },
            { (GameFamilies.Plat, GameLanguages.German), 0xF076C },
            { (GameFamilies.Plat, GameLanguages.Italian), 0xF0730 },
            { (GameFamilies.Plat, GameLanguages.Spanish), 0xF07A8 },

            // HG/SS
            { (GameFamilies.HGSS, GameLanguages.English), 0xFFB90 },
            { (GameFamilies.HGSS, GameLanguages.Japanese), 0xFF310 },
            { (GameFamilies.HGSS, GameLanguages.French), 0xFFB74 },
            { (GameFamilies.HGSS, GameLanguages.German), 0xFFB44 },
            { (GameFamilies.HGSS, GameLanguages.Italian), 0xFFB08 },
            { (GameFamilies.HGSS, GameLanguages.Spanish), 0xFFB78 },
        };
    }
}
