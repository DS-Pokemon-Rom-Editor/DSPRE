using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace DSPRE.HgEngine
{
    /// <summary>Resolves a species id straight to its real icon PNG in the linked hg-engine checkout.
    /// hg-engine doesn't store Pokémon icons in personal.narc; each species' icon is a source PNG
    /// (data/graphics/sprites/&lt;name&gt;/icon.png) assembled by data/graphics/pokegra.mk. This bypasses
    /// the NCGR/NCLR/palette-table pipeline entirely and loads the source PNG directly, since
    /// RomInfo.monIconPalTableAddress is a vanilla-only ARM9 offset that doesn't survive hg-engine's
    /// recompile. pokegra.mk is parsed for the id -> PNG path mapping rather than re-deriving a
    /// folder-name slug, since it's hg-engine's own generated ground truth.</summary>
    public static class HgEnginePokemonIcons
    {
        private const string PokegraMkRelPath = "data/graphics/pokegra.mk";

        private static Dictionary<int, string> _cache;
        private static string _cachedForRepo;

        public static bool TryGetIconPath(int speciesId, out string absolutePngPath)
        {
            absolutePngPath = null;
            if (!HgEngineProject.IsLinked) return false;

            var map = LoadMap();
            if (map == null) return false;

            int lookupId = AdjustForFakemonShift(speciesId);
            if (lookupId < 0 || !map.TryGetValue(lookupId, out string relPath)) return false;

            string full = Path.Combine(HgEngineProject.RepoPathUnc, relPath.Replace('/', '\\'));
            if (!File.Exists(full)) return false;

            absolutePngPath = full;
            return true;
        }

        /// <summary>pokegra.mk is static and never regenerated when a fakemon is inserted, even though
        /// every downstream form species shifts up by NUM_OF_FAKEMONS in personal.narc. Maps an id past
        /// the fakemon block back down to what pokegra.mk still calls it by; an id inside the fakemon
        /// block has no dump-time entry and correctly resolves to "no icon".</summary>
        private static int AdjustForFakemonShift(int speciesId)
        {
            if (!HgEngineSpeciesExpansion.TryGetCustomRange(out int firstCustomId, out int fakemonCount) || fakemonCount == 0)
                return speciesId;

            if (speciesId < firstCustomId) return speciesId;
            if (speciesId < firstCustomId + fakemonCount) return -1;
            return speciesId - fakemonCount;
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

        /// <summary>Matches "build/pokemonicon/1_0001.NCGR: .../icon.png" rule lines. The "1_" prefix
        /// distinguishes the icon NCGR from the "0_"-prefixed shared palette banks.</summary>
        internal static Dictionary<int, string> ParseMap(string pokegraMkText)
        {
            var map = new Dictionary<int, string>();
            foreach (Match m in Regex.Matches(pokegraMkText,
                @"build/pokemonicon/1_(\d+)\.NCGR:\s*(data/graphics/sprites/[^\s/]+/icon\.png)"))
            {
                if (int.TryParse(m.Groups[1].Value, out int id))
                    map[id] = m.Groups[2].Value;
            }
            return map;
        }
    }
}
