using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace DSPRE.HgEngine
{
    /// <summary>Each species' own overworld walk sprite, data/graphics/sprites/&lt;name&gt;/overworld.png
    /// (same convention as icon.png/front.png/back.png), separate from the newer per-form
    /// MON_OVERWORLD_GFX_START table in <see cref="HgEngineOverworldFollowerSprite"/>.</summary>
    public static class HgEngineOverworldSprite
    {
        private const string PokegraMkRelPath = "data/graphics/pokegra.mk";

        private static Dictionary<int, string> _cache;
        private static string _cachedForRepo;

        public static bool TryGetSpritePngPath(int speciesId, out string absolutePngPath)
        {
            absolutePngPath = null;
            if (!HgEngineProject.IsLinked) return false;

            var map = LoadMap();
            if (map == null) return false;

            int lookupId = HgEngineSpeciesExpansion.AdjustForPokegraMkLookup(speciesId);
            if (lookupId < 0 || !map.TryGetValue(lookupId, out string relPath)) return false;

            string full = Path.Combine(HgEngineProject.RepoPathUnc, relPath.Replace('/', '\\'));
            if (!File.Exists(full)) return false;

            absolutePngPath = full;
            return true;
        }

        private static Dictionary<int, string> LoadMap()
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

        /// <summary>Matches "build/pokemonow/3_0025.btx0: .../overworld.png" rule lines (the "3_" prefix is this build's own bank number for the walk-sprite texture).</summary>
        internal static Dictionary<int, string> ParseMap(string pokegraMkText)
        {
            var map = new Dictionary<int, string>();
            foreach (Match m in Regex.Matches(pokegraMkText,
                @"build/pokemonow/3_(\d+)\.btx0:\s*(data/graphics/sprites/[^\s/]+/overworld\.png)"))
            {
                if (int.TryParse(m.Groups[1].Value, out int id))
                    map[id] = m.Groups[2].Value;
            }
            return map;
        }
    }
}
