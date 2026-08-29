using System;
using System.Collections.Generic;
using System.IO;
using static DSPRE.RomInfo;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// The terrain animations an area can play. Each area's RESOURCE_PARAM names one by index, and the
    /// game loops it forever while you are on the map. HeartGold and SoulSilver are the only games that
    /// have any: two animations covering the moving water. Diamond, Pearl and Platinum have none.
    /// </summary>
    public static class GroundAnimationSet
    {
        /// <summary>An area with this in its terrain animation slot plays nothing.</summary>
        public const ushort None = 0xFFFF;

        private static readonly Dictionary<int, TextureSrtAnimation> Cache = new Dictionary<int, TextureSrtAnimation>();
        private static string _cacheDir;

        /// <summary>True when the loaded game has terrain animations at all.</summary>
        public static bool Available => gameDirs != null && gameDirs.ContainsKey(DirNames.groundAnimations);

        /// <summary>The animation an area plays, or null when it has none or the game has none.</summary>
        public static TextureSrtAnimation ForArea(AreaData area) =>
            area == null ? null : Load(area.groundAnimation);

        /// <summary>Loads one animation by index. Null for <see cref="None"/> or anything missing.</summary>
        public static TextureSrtAnimation Load(int index)
        {
            if (index == None || index < 0 || !Available) return null;

            string dir = gameDirs[DirNames.groundAnimations].unpackedDir;
            if (dir != _cacheDir) { Cache.Clear(); _cacheDir = dir; }
            if (Cache.TryGetValue(index, out var hit)) return hit;

            TextureSrtAnimation anim = null;
            try
            {
                // Nothing else in DSPRE reads this archive, so it is usually still packed.
                DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.groundAnimations });
                string path = Path.Combine(dir, index.ToString("D4"));
                if (File.Exists(path)) anim = TextureSrtAnimation.Load(File.ReadAllBytes(path));
            }
            catch (Exception ex) { AppLogger.Error($"Terrain animation {index} failed to load: {ex.Message}"); }

            Cache[index] = anim;
            return anim;
        }
    }
}
