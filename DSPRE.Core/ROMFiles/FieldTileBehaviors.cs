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

        private static readonly Dictionary<byte, (int dx, int dz, int tiles)> Jumps
            = new Dictionary<byte, (int, int, int)>
            {
                [56] = (1, 0, 1),    // east
                [57] = (-1, 0, 1),   // west
                [58] = (0, -1, 1),   // north
                [59] = (0, 1, 1),    // south
                [90] = (0, -1, 2),
                [91] = (0, 1, 2),
                [92] = (-1, 0, 2),
                [93] = (1, 0, 2),
            };

        public static bool TryJump(byte behavior, out int dx, out int dz, out int tiles)
        {
            if (Jumps.TryGetValue(behavior, out var jump))
            {
                (dx, dz, tiles) = jump;
                return true;
            }
            dx = dz = tiles = 0;
            return false;
        }

        public static bool JumpsWith(byte behavior, int dx, int dz)
            => TryJump(behavior, out int jx, out int jz, out _) && jx == dx && jz == dz;
    }
}
