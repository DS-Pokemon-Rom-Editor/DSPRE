using System;
using System.Collections.Generic;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// Where a new game begins, so an editor can open somewhere useful instead of whatever header
    /// happens to come first.
    ///
    /// The games keep these in location.c. HeartGold and SoulSilver start indoors at T20R0202, the
    /// player's bedroom, and outdoors at T20, which is New Bark Town. Platinum starts at T01R0202 and
    /// T01, which is Twinleaf Town. Diamond and Pearl share Twinleaf with Platinum; their own leak was
    /// not to hand, so that pair is taken from Platinum rather than read directly.
    /// </summary>
    public static class FieldStartLocation
    {
        /// <summary>The internal name of the town a new game starts in, or null when it is not known.</summary>
        public static string TownFor(RomInfo.GameFamilies family)
        {
            switch (family)
            {
                case RomInfo.GameFamilies.HGSS: return "T20";
                case RomInfo.GameFamilies.Plat:
                case RomInfo.GameFamilies.DP: return "T01";
                default: return null;
            }
        }

        /// <summary>The internal name of the room a new game actually opens in.</summary>
        public static string RoomFor(RomInfo.GameFamilies family)
        {
            switch (family)
            {
                case RomInfo.GameFamilies.HGSS: return "T20R0202";
                case RomInfo.GameFamilies.Plat:
                case RomInfo.GameFamilies.DP: return "T01R0202";
                default: return null;
            }
        }

        /// <summary>
        /// The header to open on, given every header's internal name. The starting town comes first,
        /// then the room it begins in, and failing both it says so by handing back -1 so the caller can
        /// fall back to whatever it did before.
        /// </summary>
        public static int HeaderFor(RomInfo.GameFamilies family, IReadOnlyList<string> internalNames)
        {
            if (internalNames == null || internalNames.Count == 0) return -1;

            foreach (string want in new[] { TownFor(family), RoomFor(family) })
            {
                if (string.IsNullOrEmpty(want)) continue;
                for (int id = 0; id < internalNames.Count; id++)
                    if (string.Equals(InternalPart(internalNames[id]), want, StringComparison.OrdinalIgnoreCase))
                        return id;
            }
            return -1;
        }

        /// <summary>
        /// The internal name out of whatever the caller had to hand. A list meant for showing people
        /// reads like "060 -   T20", and a name read straight out of the ROM is padded with zero bytes,
        /// so both are reduced to the last real word.
        /// </summary>
        private static string InternalPart(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            name = name.Replace('\0', ' ').Trim();
            int at = name.LastIndexOf(' ');
            return at >= 0 ? name.Substring(at + 1) : name;
        }
    }
}
