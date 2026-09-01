using System;
using System.Collections.Generic;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// Where a new game begins, so an editor can open somewhere useful instead of whatever header happens
    /// to come first.
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

        /// <summary>The header to open on, given every header's internal name. </summary>
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

        /// <summary>The internal name out of whatever the caller had to hand. </summary>
        private static string InternalPart(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            name = name.Replace('\0', ' ').Trim();
            int at = name.LastIndexOf(' ');
            return at >= 0 ? name.Substring(at + 1) : name;
        }
    }
}
