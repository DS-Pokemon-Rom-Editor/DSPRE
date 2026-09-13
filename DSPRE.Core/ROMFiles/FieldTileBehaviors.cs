using System.Collections.Generic;

namespace DSPRE.ROMFiles
{
    /// <summary>What a tile's behaviour byte means for somebody standing on it.</summary>
    public static class FieldTileBehaviors
    {
        // The behaviours flagged surfable in sTileBehaviorFlags, less the bridges, which carry the flag but
        // are walked over. HGSS numbers them the same and leaves out 34.
        private static readonly HashSet<byte> Water = new HashSet<byte> { 16, 17, 18, 19, 20, 21, 25, 42, 80, 81, 82, 83 };
        private const byte PlatinumOnlyWater = 34;

        /// <summary>Whether this is water, which needs Surf rather than feet.</summary>
        public static bool IsWater(byte behavior, RomInfo.GameFamilies family) =>
            Water.Contains(behavior) || (family != RomInfo.GameFamilies.HGSS && behavior == PlatinumOnlyWater);
    }
}
