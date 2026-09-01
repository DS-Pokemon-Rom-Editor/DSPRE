using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static DSPRE.RomInfo;

namespace DSPRE.ROMFiles
{
    /// <summary>The animations a building model plays. </summary>
    public static class BuildingAnimationSet
    {
        private static readonly Dictionary<(bool indoor, int model), BuildingAnimationInfo> InfoCache
            = new Dictionary<(bool, int), BuildingAnimationInfo>();
        private static readonly Dictionary<int, TextureSrtAnimation> ScrollCache
            = new Dictionary<int, TextureSrtAnimation>();
        private static readonly Dictionary<int, TexturePatternAnimation> PatternCache
            = new Dictionary<int, TexturePatternAnimation>();
        private static readonly Dictionary<int, MaterialColourAnimation> FadeCache = new Dictionary<int, MaterialColourAnimation>();
        private static readonly Dictionary<int, JointAnimation> JointCache = new Dictionary<int, JointAnimation>();
        private static readonly Dictionary<int, byte[]> RawCache = new Dictionary<int, byte[]>();
        private static string _cacheDir;

        public static bool Available =>
            gameDirs != null
            && gameDirs.ContainsKey(DirNames.buildingAnimations)
            && gameDirs.ContainsKey(DirNames.buildingAnimListOut);

        /// <summary>The list entry for one building model, or null when it has none.</summary>
        public static BuildingAnimationInfo InfoFor(int modelId, bool indoor)
        {
            if (!Available || modelId < 0) return null;
            Refresh();
            var key = (indoor, modelId);
            if (InfoCache.TryGetValue(key, out var hit)) return hit;

            BuildingAnimationInfo info = null;
            try
            {
                DirNames list = indoor && gameDirs.ContainsKey(DirNames.buildingAnimListIn)
                    ? DirNames.buildingAnimListIn : DirNames.buildingAnimListOut;
                DSUtils.TryUnpackNarcs(new List<DirNames> { list });
                string path = Path.Combine(gameDirs[list].unpackedDir, modelId.ToString("D4"));
                if (File.Exists(path))
                {
                    byte[] b = File.ReadAllBytes(path);
                    // Diamond, Pearl and Platinum write a shorter record than HeartGold does; both are read.
                    if (b.Length >= BuildingAnimationInfo.ShortSize) info = new BuildingAnimationInfo(b);
                }
            }
            catch (Exception ex) { AppLogger.Error($"Building animation list entry {modelId} failed: {ex.Message}"); }

            InfoCache[key] = info;
            return info;
        }

        /// <summary>The texture-scrolling animations a building model plays. </summary>
        public static IReadOnlyList<TextureSrtAnimation> ScrollingFor(int modelId, bool indoor, FieldTimeZone? timeOfDay = null)
        {
            var result = new List<TextureSrtAnimation>();
            foreach (int code in CodesToPlay(modelId, indoor, timeOfDay))
            {
                var anim = LoadScrolling(code);
                if (anim != null) result.Add(anim);
            }
            return result;
        }

        /// <summary>
        /// The texture-swapping animations a building model plays, which is what makes signs flash.
        /// </summary>
        public static IReadOnlyList<TexturePatternAnimation> PatternsFor(int modelId, bool indoor, FieldTimeZone? timeOfDay = null)
        {
            var result = new List<TexturePatternAnimation>();
            foreach (int code in CodesToPlay(modelId, indoor, timeOfDay))
            {
                var anim = LoadPattern(code);
                if (anim != null) result.Add(anim);
            }
            return result;
        }

        /// <summary>The animations that move a building model's separate parts about.</summary>
        public static IReadOnlyList<JointAnimation> JointsFor(int modelId, bool indoor, FieldTimeZone? timeOfDay = null)
        {
            var result = new List<JointAnimation>();
            foreach (int code in CodesToPlay(modelId, indoor, timeOfDay))
            {
                var anim = LoadJoint(code);
                if (anim != null && anim.Moves) result.Add(anim);
            }
            return result;
        }

        /// <summary>Which of a model's animation slots actually run. </summary>
        public static IEnumerable<int> CodesToPlay(int modelId, bool indoor, FieldTimeZone? timeOfDay)
        {
            var info = InfoFor(modelId, indoor);
            if (info == null || !info.Animates) yield break;

            if (info.IsTimeOfDay)
            {
                if (timeOfDay == null) yield break;      // no clock given, so nothing to choose with
                int slot = FieldTimeOfDay.AnimationIndexForZone(timeOfDay.Value);
                if (slot < 0 || slot >= BuildingAnimationInfo.MaxAnimations) yield break;

                int code = info.Codes[slot];
                if (unchecked((uint)code) != BuildingAnimationInfo.NoAnimation) yield return code;
                yield break;
            }

            // A door, or anything else that waits to be set off, stays still until it is.
            if (!info.PlaysUnprompted) yield break;
            foreach (int code in info.UsedCodes) yield return code;
        }

        /// <summary>The animations that fade a building model's materials in and out.</summary>
        public static IReadOnlyList<MaterialColourAnimation> FadesFor(int modelId, bool indoor, FieldTimeZone? timeOfDay = null)
        {
            var result = new List<MaterialColourAnimation>();
            foreach (int code in CodesToPlay(modelId, indoor, timeOfDay))
            {
                var anim = LoadFade(code);
                if (anim != null && anim.Fades) result.Add(anim);
            }
            return result;
        }

        private static MaterialColourAnimation LoadFade(int code)
        {
            if (FadeCache.TryGetValue(code, out var hit)) return hit;
            var anim = MaterialColourAnimation.Load(Raw(code));
            FadeCache[code] = anim;
            return anim;
        }

        private static JointAnimation LoadJoint(int code)
        {
            if (JointCache.TryGetValue(code, out var hit)) return hit;
            var anim = JointAnimation.Load(Raw(code));
            JointCache[code] = anim;
            return anim;
        }

        /// <summary>
        /// The sound a door makes, which is what the Door field picks (GetDoorSE in field_3d_anime_ev.c).
        /// </summary>
        public static string DoorSound(int modelId, bool indoor, bool opening)
        {
            var info = InfoFor(modelId, indoor);
            switch (info?.Door ?? 0)
            {
                case 1: return opening ? "a door opening" : "a door closing";
                case 2: return opening ? "an automatic door opening" : "an automatic door closing";
                case 3: return opening ? "a glass door opening" : "a glass door closing";
                case 4: return opening ? "a sliding door opening" : "a sliding door closing";
                default: return null;
            }
        }

        /// <summary>A door's animations, which the games only play when somebody goes through. </summary>
        public static (IReadOnlyList<JointAnimation> Joints, IReadOnlyList<TexturePatternAnimation> Patterns)
            DoorAnimations(int modelId, bool indoor)
        {
            var info = InfoFor(modelId, indoor);
            if (info == null || !info.Animates || !info.IsDoor)
                return (Array.Empty<JointAnimation>(), Array.Empty<TexturePatternAnimation>());

            var joints = new List<JointAnimation>();
            var patterns = new List<TexturePatternAnimation>();
            foreach (int code in info.UsedCodes)
            {
                var j = LoadJoint(code);
                if (j != null && j.Moves) joints.Add(j);
                var t = LoadPattern(code);
                if (t != null) patterns.Add(t);
            }
            return (joints, patterns);
        }

        /// <summary>What a building model's animations wait for, when they do not just run on their own.</summary>
        public static (bool Door, bool TimeOfDay) WaitsFor(int modelId, bool indoor)
        {
            var info = InfoFor(modelId, indoor);
            if (info == null || !info.Animates) return (false, false);
            return (info.IsDoor, info.IsTimeOfDay);
        }

        private static TextureSrtAnimation LoadScrolling(int code)
        {
            if (ScrollCache.TryGetValue(code, out var hit)) return hit;
            // Load returns null for anything that isn't a scrolling animation, which is most of the
            // archive, so the other kinds simply fall out here.
            var anim = TextureSrtAnimation.Load(Raw(code));
            ScrollCache[code] = anim;
            return anim;
        }

        private static TexturePatternAnimation LoadPattern(int code)
        {
            if (PatternCache.TryGetValue(code, out var hit)) return hit;
            var anim = TexturePatternAnimation.Load(Raw(code));
            PatternCache[code] = anim;
            return anim;
        }

        private static byte[] Raw(int code)
        {
            if (code < 0) return null;
            Refresh();
            if (RawCache.TryGetValue(code, out var hit)) return hit;

            byte[] data = null;
            try
            {
                DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.buildingAnimations });
                string path = Path.Combine(gameDirs[DirNames.buildingAnimations].unpackedDir, code.ToString("D4"));
                if (File.Exists(path)) data = File.ReadAllBytes(path);
            }
            catch (Exception ex) { AppLogger.Error($"Building animation {code} failed to load: {ex.Message}"); }

            RawCache[code] = data;
            return data;
        }

        private static void Refresh()
        {
            string dir = gameDirs[DirNames.buildingAnimations].unpackedDir;
            if (dir == _cacheDir) return;
            InfoCache.Clear();
            ScrollCache.Clear();
            PatternCache.Clear();
            JointCache.Clear();
            FadeCache.Clear();
            RawCache.Clear();
            _cacheDir = dir;
        }
    }
}
