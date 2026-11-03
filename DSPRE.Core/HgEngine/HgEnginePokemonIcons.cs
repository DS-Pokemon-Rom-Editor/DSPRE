using System.Collections.Generic;
using System.IO;
using System;

namespace DSPRE.HgEngine
{
    /// <summary>
    /// A species' icon PNG in the checkout (data/graphics/sprites/&lt;name&gt;/icon.png), found through pokegra.mk,
    /// since the vanilla icon palette table doesn't survive hg-engine's build.
    /// </summary>
    public static class HgEnginePokemonIcons
    {
        private const string PokegraMkRelPath = "data/graphics/pokegra.mk";

        private static Dictionary<int, string> _cache;
        private static string _cachedForRepo;

        public static bool TryGetIconPath(int speciesId, out string absolutePngPath)
        {
            absolutePngPath = null;
            if (!HgEngineProject.IsLinked) return false;

            Dictionary<int, string> map = LoadMap();
            if (map == null) return false;

            int lookupId = HgEngineSpeciesExpansion.AdjustForPokegraMkLookup(speciesId);
            if (lookupId < 0 || !map.TryGetValue(lookupId, out string relPath)) return false;

            string full = Path.Combine(HgEngineProject.RepoPathUnc, relPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(full)) return false;

            absolutePngPath = full;
            return true;
        }

        private static Dictionary<int, string> LoadMap()
        {
            string repo = HgEngineProject.RepoPathUnc;
            if (_cache != null && _cachedForRepo == repo) return _cache;

            string path = Path.Combine(repo, PokegraMkRelPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path)) return null;

            Dictionary<int, string> map = ParseMap(File.ReadAllText(path));
            _cache = map;
            _cachedForRepo = repo;
            return map;
        }

        /// <summary>Matches "build/pokemonicon/1_0001.NCGR: .../icon.png" rule lines. The "1_" prefix
        /// distinguishes the icon NCGR from the "0_"-prefixed shared palette banks.</summary>
        internal static Dictionary<int, string> ParseMap(string pokegraMkText)
        {
            Dictionary<int, string> map = new Dictionary<int, string>();
            foreach (HgEngineMakeFile.Rule rule in HgEngineMakeFile.For(pokegraMkText).Rules)
            {
                int id = HgEngineMakeFile.NumberBetween(rule.Target, "build/pokemonicon/1_", ".NCGR");
                if (id >= 0 && rule.Prerequisites.StartsWith("data/graphics/sprites/", StringComparison.Ordinal)
                    && HgEngineMakeFile.Words(rule.Prerequisites)[0] is string png && png.EndsWith("/icon.png", StringComparison.Ordinal))
                    map[id] = png;
            }
            return map;
        }
    }
}
