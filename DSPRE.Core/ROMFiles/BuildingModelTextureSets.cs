using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static DSPRE.RomInfo;

namespace DSPRE.ROMFiles
{
    /// <summary>One building texture set, the model ids it contains pictures for, and the areas that use it.</summary>
    public sealed class BuildingModelTextureSet
    {
        public int TextureSetId { get; init; }
        public bool Indoor { get; init; }
        public IReadOnlyList<int> ModelIds { get; init; }
        public IReadOnlyList<int> AreaIds { get; init; }
    }

    /// <summary>
    /// Reads the ROM's area and area-build tables. The texture pack and area-build entry share an id;
    /// the latter starts with a 16-bit count followed by that many 16-bit building model ids.
    /// </summary>
    public static class BuildingModelTextureSets
    {
        public static IReadOnlyList<int> ParseModelIds(ReadOnlySpan<byte> data)
        {
            if (data.Length < 2) throw new InvalidDataException("The building model list has no count.");
            int count = data[0] | data[1] << 8;
            int required = 2 + count * 2;
            if (required > data.Length)
                throw new InvalidDataException($"The building model list says it has {count} entries, but only {data.Length} bytes exist.");

            var result = new int[count];
            for (int i = 0; i < count; i++)
                result[i] = data[2 + i * 2] | data[3 + i * 2] << 8;
            return result;
        }

        public static IReadOnlyList<BuildingModelTextureSet> ReadCurrentRom()
        {
            if (gameDirs == null
                || !gameDirs.ContainsKey(DirNames.areaData)
                || !gameDirs.ContainsKey(DirNames.buildingConfigFiles)
                || !gameDirs.ContainsKey(DirNames.buildingTextures))
                return Array.Empty<BuildingModelTextureSet>();

            DSUtils.TryUnpackNarcs(new List<DirNames>
            {
                DirNames.areaData, DirNames.buildingConfigFiles, DirNames.buildingTextures,
            });

            var areas = new Dictionary<(int set, bool indoor), List<int>>();
            foreach (string path in NumberedFiles(gameDirs[DirNames.areaData].unpackedDir))
            {
                if (!TryFileId(path, out int areaId)) continue;
                try
                {
                    using var input = File.OpenRead(path);
                    var area = new AreaData(input);
                    var key = ((int)area.buildingsTileset, area.IsIndoor);
                    if (!areas.TryGetValue(key, out var ids)) areas[key] = ids = new List<int>();
                    ids.Add(areaId);
                }
                catch (Exception ex)
                {
                    AppLogger.Error($"Building texture association skipped area {areaId}: {ex.Message}");
                }
            }

            var result = new List<BuildingModelTextureSet>();
            foreach (string path in NumberedFiles(gameDirs[DirNames.buildingConfigFiles].unpackedDir))
            {
                if (!TryFileId(path, out int setId)) continue;
                IReadOnlyList<int> models;
                try { models = ParseModelIds(File.ReadAllBytes(path)); }
                catch (Exception ex)
                {
                    AppLogger.Error($"Building texture association skipped set {setId}: {ex.Message}");
                    continue;
                }

                foreach (var use in areas.Where(a => a.Key.set == setId))
                    result.Add(new BuildingModelTextureSet
                    {
                        TextureSetId = setId,
                        Indoor = use.Key.indoor,
                        ModelIds = models,
                        AreaIds = use.Value.OrderBy(x => x).ToArray(),
                    });
            }
            return result;
        }

        private static IEnumerable<string> NumberedFiles(string directory) =>
            Directory.Exists(directory)
                ? Directory.EnumerateFiles(directory).OrderBy(x => x, StringComparer.Ordinal)
                : Enumerable.Empty<string>();

        private static bool TryFileId(string path, out int id) =>
            Int32.TryParse(Path.GetFileName(path), out id);
    }
}
