using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE.Models;
using LibNDSFormats.NSBMD;
using LibNDSFormats.NSBTX;
using static DSPRE.RomInfo;

namespace DSPRE.ROMFiles
{
    /// <summary>What a map brought in from elsewhere lacks here: textures for its model, and its buildings.</summary>
    public static class MapImportCheck
    {
        /// <summary>A texture or palette the model names that a pack does not have.</summary>
        public sealed class Missing
        {
            public string Name { get; init; }
            public bool Palette { get; init; }
            public string Kind => Palette ? "Palette" : "Texture";
        }

        /// <summary>A building the map places that can't be drawn here as it is.</summary>
        public sealed class BuildingTrouble
        {
            public uint Model { get; init; }
            public int Count { get; init; }
            public bool NoSuchModel { get; init; }
            public List<string> Lacking { get; init; } = new List<string>();
            public string Problem => NoSuchModel ? "No such model in this game" : "Lacks " + string.Join(", ", Lacking);
        }

        public static string MapPackPath(int pack) => Path.Combine(GameDirs[DirNames.MapTextures].unpackedDir, pack.ToString("D4"));

        /// <summary>
        /// The model's materials the pack can't dress and the names it lacks, by the renderer's own pairing
        /// (<see cref="NSBMD.MatchTextures"/>), so this counts exactly what shows grey.
        /// </summary>
        public static (int Materials, int Undressed, List<Missing> Names) MapTextures(MapFile map, string packPath)
        {
            NSBMD copy = ParseModel(map);
            if (copy == null) return (0, 0, new List<Missing>());
            List<NSBMDTexture> textures = new List<NSBMDTexture>();
            List<NSBMDPalette> palettes = new List<NSBMDPalette>();
            if (packPath != null && File.Exists(packPath)) ReadPack(packPath, out textures, out palettes);
            return Match(copy, textures, palettes);
        }

        /// <summary>Every map texture pack with how many of the model's materials it would leave grey, best first.</summary>
        public static List<(int Pack, int Undressed)> RankPacks(MapFile map)
        {
            List<(int Pack, int Undressed)> ranked = new List<(int Pack, int Undressed)>();
            NSBMD copy = ParseModel(map);
            if (copy == null) return ranked;
            string dir = GameDirs[DirNames.MapTextures].unpackedDir;
            foreach (string file in Directory.GetFiles(dir))
            {
                if (!int.TryParse(Path.GetFileName(file), out int pack)) continue;
                try
                {
                    ReadPack(file, out List<NSBMDTexture> textures, out List<NSBMDPalette> palettes);
                    ranked.Add((pack, Match(copy, textures, palettes).Undressed));
                }
                catch (Exception ex) when (ex is IOException || ex is EndOfStreamException || ex is ArgumentException || ex is IndexOutOfRangeException) { }   // a pack that can't be read is left out of the ranking
            }
            ranked.Sort((a, b) => a.Undressed != b.Undressed ? a.Undressed.CompareTo(b.Undressed) : a.Pack.CompareTo(b.Pack));
            return ranked;
        }

        /// <summary>A short line saying how many materials the pack leaves grey, or null when it dresses them all.</summary>
        public static string Note(MapFile map, int pack)
        {
            (int Materials, int Undressed, List<Missing> Names) found;
            try { found = MapTextures(map, MapPackPath(pack)); }
            catch (Exception ex) when (ex is IOException || ex is EndOfStreamException || ex is ArgumentException || ex is IndexOutOfRangeException) { return null; }
            if (found.Undressed == 0) return null;
            return $"Map Tileset {pack} has no textures for {found.Undressed} of this model's {found.Materials} materials"
                 + $" ({string.Join(", ", found.Names.Take(4).Select(n => n.Name))}{(found.Names.Count > 4 ? " ..." : "")}), so they show grey.";
        }

        /// <summary>
        /// Buildings whose model this game lacks, or whose textures the area's building pack lacks. HGSS indoor areas
        /// use the interior building models. With no area (a map no header uses) only missing models are reported.
        /// </summary>
        public static List<BuildingTrouble> Buildings(MapFile map, int areaId, GameFamilies family)
        {
            List<BuildingTrouble> trouble = new List<BuildingTrouble>();
            if (map?.Buildings == null || map.Buildings.Count == 0) return trouble;
            AreaData area = areaId >= 0 ? new AreaData((byte)areaId) : null;
            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.BuildingTextures });
            string packPath = area == null ? null
                : Path.Combine(GameDirs[DirNames.BuildingTextures].unpackedDir, area.BuildingsTileset.ToString("D4"));
            // Without the pack file there is nothing to compare against, so only missing models are reported.
            bool hasPack = packPath != null && File.Exists(packPath);
            HashSet<string> have = new HashSet<string>(StringComparer.Ordinal);
            if (hasPack)
            {
                ReadPack(packPath, out List<NSBMDTexture> textures, out List<NSBMDPalette> palettes);
                foreach (NSBMDTexture t in textures) have.Add(t.Texname.TrimEnd('\0'));
                foreach (NSBMDPalette p in palettes) have.Add(p.Palname.TrimEnd('\0'));
            }

            bool interior = area != null && family == GameFamilies.HGSS && GameDirs.ContainsKey(DirNames.InteriorBuildingModels) && area.AreaType == AreaData.TYPE_INDOOR;
            if (interior) DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.InteriorBuildingModels });
            string dir = GameDirs[interior ? DirNames.InteriorBuildingModels : DirNames.ExteriorBuildingModels].unpackedDir;

            foreach (IGrouping<uint, Building> same in map.Buildings.GroupBy(b => b.ModelID).OrderBy(g => g.Key))
            {
                string path = Path.Combine(dir, same.Key.ToString("D4"));
                MapMesh mesh = File.Exists(path) ? MapMesh.Read(File.ReadAllBytes(path), out _) : null;
                if (mesh == null) { trouble.Add(new BuildingTrouble { Model = same.Key, Count = same.Count(), NoSuchModel = true }); continue; }
                List<string> lacking = mesh.Faces.Select(f => f.Material).Distinct()
                    .SelectMany(m => new[] { mesh.PictureFor(m) >= 0 ? mesh.NameOfPicture(mesh.PictureFor(m)) : null,
                                             mesh.ColoursFor(m) >= 0 ? mesh.NameOfColours(mesh.ColoursFor(m)) : null })
                    .Where(n => !string.IsNullOrEmpty(n) && !have.Contains(NitroDictionary.Fit(n))).Distinct().ToList();
                if (hasPack && lacking.Count > 0) trouble.Add(new BuildingTrouble { Model = same.Key, Count = same.Count(), Lacking = lacking });
            }
            return trouble;
        }

        private static NSBMD ParseModel(MapFile map)
        {
            if (map?.MapModelData == null) return null;
            NSBMD copy = NSBMDLoader.LoadNSBMD(new MemoryStream(map.MapModelData));
            return copy?.Models == null || copy.Models.Length == 0 ? null : copy;
        }

        private static void ReadPack(string path, out List<NSBMDTexture> textures, out List<NSBMDPalette> palettes)
        {
            NSBTXLoader.LoadNsbtx(new MemoryStream(File.ReadAllBytes(path)), out textures, out palettes);
            textures ??= new List<NSBMDTexture>();
            palettes ??= new List<NSBMDPalette>();
        }

        private static (int Materials, int Undressed, List<Missing> Names) Match(NSBMD copy, List<NSBMDTexture> textures, List<NSBMDPalette> palettes)
        {
            copy.Textures = textures;
            copy.Palettes = palettes;
            copy.MatchTextures();
            List<Missing> names = new List<Missing>();
            int materials = 0, undressed = 0;
            NSBMDModel model = copy.Models[0];
            for (int i = 0; i < model.Materials.Count; i++)
            {
                NSBMDTexture wanted = model.Textures.Find(t => t.Texmatid.Contains((uint)i));
                if (wanted == null) continue;
                materials++;
                if (!model.Materials[i].MissingExternalTexture) continue;
                undressed++;
                bool hasTexture = textures.Exists(t => string.Equals(t.Texname, wanted.Texname, StringComparison.Ordinal));
                Missing name = hasTexture
                    ? new Missing { Name = model.Palettes.Find(p => p.Palmatid.Contains((uint)i))?.Palname ?? wanted.Texname, Palette = true }
                    : new Missing { Name = wanted.Texname };
                if (!names.Exists(n => n.Name == name.Name && n.Palette == name.Palette)) names.Add(name);
            }
            return (materials, undressed, names);
        }
    }
}
