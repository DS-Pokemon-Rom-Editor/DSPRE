using System.Collections.Generic;
using System.IO;
using System;

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

        /// <param name="mustExist">False to get the path pokegra.mk builds from even before the PNG is there.</param>
        public static bool TryGetSpritePngPath(int speciesId, out string absolutePngPath, bool mustExist = true)
        {
            absolutePngPath = null;
            if (!HgEngineProject.IsLinked) return false;

            Dictionary<int, string> map = LoadMap();
            if (map == null) return false;

            int lookupId = HgEngineSpeciesExpansion.PokegraMkNumber(speciesId, n => map.GetValueOrDefault(n));
            if (lookupId < 0 || !map.TryGetValue(lookupId, out string relPath)) return false;

            string full = Path.Combine(HgEngineProject.RepoPathUnc, relPath.Replace('/', Path.DirectorySeparatorChar));
            if (mustExist && !File.Exists(full)) return false;

            absolutePngPath = full;
            return true;
        }

        /// <summary>True when pokegra.mk hasn't been generated again since a species was added.</summary>
        public static bool MakefileIsBehind()
        {
            if (!HgEngineProject.IsLinked) return false;
            if (!HgEngineSpeciesExpansion.TryGetCustomRange(out int first, out int count) || count == 0) return false;
            Dictionary<int, string> map = LoadMap();
            if (map == null) return false;
            int firstForm = first + count;
            return HgEngineSpeciesExpansion.PokegraMkNumber(firstForm, n => map.GetValueOrDefault(n)) != firstForm;
        }

        private static DateTime _cachedAt;

        private static Dictionary<int, string> LoadMap()
        {
            string repo = HgEngineProject.RepoPathUnc;
            string path = Path.Combine(repo, PokegraMkRelPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path)) return null;
            DateTime written = File.GetLastWriteTimeUtc(path);
            if (_cache != null && _cachedForRepo == repo && _cachedAt == written) return _cache;

            Dictionary<int, string> map = ParseMap(File.ReadAllText(path));
            _cache = map;
            _cachedForRepo = repo;
            _cachedAt = written;
            return map;
        }

        /// <summary>Matches "build/pokemonow/3_0025.btx0: .../overworld.png" rule lines (the "3_" prefix is this build's own bank number for the walk-sprite texture).</summary>
        internal static Dictionary<int, string> ParseMap(string pokegraMkText)
        {
            Dictionary<int, string> map = new Dictionary<int, string>();
            foreach (HgEngineMakeFile.Rule rule in HgEngineMakeFile.For(pokegraMkText).Rules)
            {
                int id = HgEngineMakeFile.NumberBetween(rule.Target, "build/pokemonow/3_", ".btx0");
                if (id >= 0 && rule.Prerequisites.StartsWith("data/graphics/sprites/", StringComparison.Ordinal)
                    && HgEngineMakeFile.Words(rule.Prerequisites)[0] is string png && png.EndsWith("/overworld.png", StringComparison.Ordinal))
                    map[id] = png;
            }
            return map;
        }
    }
}
