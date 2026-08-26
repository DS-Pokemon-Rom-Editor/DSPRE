using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace DSPRE.HgEngine
{
    /// <summary>Resolves a species id to its 4 real battle-sprite source PNGs (data/graphics/sprites/&lt;name&gt;/{female,male}/{back,front}.png) via pokegra.mk, bypassing pokegra.narc. Slot order 0-3 = FemaleBack/MaleBack/FemaleFront/MaleFront, matching DSPRE's own NARC convention.</summary>
    public static class HgEnginePokemonBattleSprites
    {
        private const string PokegraMkRelPath = "data/graphics/pokegra.mk";

        private static Dictionary<int, string[]> _cache;
        private static string _cachedForRepo;

        /// <summary>4 absolute paths in slot order (FemaleBack, MaleBack, FemaleFront, MaleFront), or null if this species has no pokegra.mk entry.</summary>
        public static string[] TryGetPosePaths(int speciesId)
        {
            if (!HgEngineProject.IsLinked) return null;

            var map = LoadMap();
            if (map == null) return null;

            int lookupId = HgEngineSpeciesExpansion.AdjustForPokegraMkLookup(speciesId);
            if (lookupId < 0 || !map.TryGetValue(lookupId, out string[] relPaths)) return null;

            var full = new string[4];
            for (int i = 0; i < 4; i++)
            {
                full[i] = Path.Combine(HgEngineProject.RepoPathUnc, relPaths[i].Replace('/', '\\'));
                if (!File.Exists(full[i])) return null;
            }
            return full;
        }

        private static Dictionary<int, string[]> LoadMap()
        {
            string repo = HgEngineProject.RepoPathUnc;
            if (_cache != null && _cachedForRepo == repo) return _cache;

            string path = Path.Combine(repo, PokegraMkRelPath.Replace('/', '\\'));
            if (!File.Exists(path)) return null;

            var map = ParseMap(File.ReadAllText(path));
            _cache = map;
            _cachedForRepo = repo;
            return map;
        }

        /// <summary>Matches "build/pokemonpic/0001-0N.NCGR: .../female|male/back|front.png" rule lines (N = 0..3).</summary>
        internal static Dictionary<int, string[]> ParseMap(string pokegraMkText)
        {
            var map = new Dictionary<int, string[]>();
            foreach (Match m in Regex.Matches(pokegraMkText,
                @"build/pokemonpic/(\d+)-0([0-3])\.NCGR:\s*(data/graphics/sprites/[^\s]+\.png)"))
            {
                if (!int.TryParse(m.Groups[1].Value, out int id)) continue;
                int slot = m.Groups[2].Value[0] - '0';
                if (!map.TryGetValue(id, out string[] paths)) map[id] = paths = new string[4];
                paths[slot] = m.Groups[3].Value;
            }
            return map;
        }
    }
}
