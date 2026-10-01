using System;
using System.Collections.Generic;
using System.IO;
using NarcAPI;
using static DSPRE.RomInfo;

namespace DSPRE
{
    /// <summary>
    /// Whether a Platinum map has heap room for its weather background when the field loads in full
    /// (door, warp, Fly, a loaded save). That load reads each background file whole into the field heap
    /// after the area data, which stays resident; a file that does not fit loads as nothing and the game
    /// black-screens. Walking in across a map edge loads the weather later and is not affected.
    /// </summary>
    public static class FieldWeatherRoom
    {
        public sealed record Result(long Needed, long Free)
        {
            public bool Fits => Free >= Needed;
        }

        // The field heap's largest free block when the background loads plus what the area data took, measured
        // on a Platinum ROM: 258,788 on Twinleaf and Route 201 (area 6), 266,840 with area 14, 340,988 in an
        // interior. The lowest is used so the estimate errs towards warning.
        private const long LeftoverWithoutArea = 258_788;

        // Each block in an NNS expanded heap carries a 16-byte header and is rounded to 4 bytes.
        private static long Block(long size) => ((size + 3) & ~3L) + 16;

        /// <summary>The check for a header's weather and area, or null where it has not been measured.</summary>
        public static Result Check(int weather, int areaDataId)
        {
            if (gameFamily != GameFamilies.Plat) return null;
            var spec = FieldWeather.For(gameFamily, weather);
            long needed = 0;
            try
            {
                var sizes = BackgroundSizes();
                if (sizes == null) return null;
                foreach (int set in new[] { spec.Background, spec.Companion?.Background ?? -1 })
                {
                    if (set < 0) continue;
                    var (nclr, ncgr, nscr) = FieldWeather.BackgroundSets[set];
                    foreach (int member in new[] { nclr, ncgr, nscr })
                        if (member < sizes.Length) needed = Math.Max(needed, Block(sizes[member]));
                }
                if (needed == 0) return null;
                long cost = AreaCost(areaDataId);
                return new Result(needed, LeftoverWithoutArea - cost);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is IndexOutOfRangeException)
            {
                AppLogger.Warn($"Weather room check for weather {weather}, area {areaDataId} failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// What AreaDataManager_Load keeps in the field heap: the building list, every listed building model,
        /// both texture sets cut down to the part before their texture data, and the listed buildings' animations.
        /// </summary>
        public static long AreaCost(int areaDataId)
        {
            DSUtils.TryUnpackNarcs(new List<DirNames>
            {
                DirNames.areaData, DirNames.mapTextures, DirNames.buildingTextures, DirNames.buildingConfigFiles,
                DirNames.exteriorBuildingModels, DirNames.buildingAnimations, DirNames.buildingAnimListOut,
            });
            var area = new ROMFiles.AreaData((byte)areaDataId);

            byte[] list = File.ReadAllBytes(Member(DirNames.buildingConfigFiles, area.buildingsTileset));
            int count = BitConverter.ToUInt16(list, 0);
            int animLists = Directory.GetFiles(gameDirs[DirNames.buildingAnimListOut].unpackedDir).Length;
            long cost = Block(list.Length);
            bool dummyListed = false;
            for (int i = 0; i < count; i++)
            {
                int model = BitConverter.ToUInt16(list, 2 + 2 * i);
                dummyListed |= model == 0;
                cost += Block(Length(DirNames.exteriorBuildingModels, model));
                if (model < animLists) cost += AnimationCost(model);
            }
            // The dummy box is always loaded.
            if (!dummyListed) cost += Block(Length(DirNames.exteriorBuildingModels, 0));

            cost += Block(KeptTextureSize(Member(DirNames.mapTextures, area.mapTileset)));
            string buildingTextures = Member(DirNames.buildingTextures, area.buildingsTileset);
            cost += Block(count > 0 ? KeptTextureSize(buildingTextures) : new FileInfo(buildingTextures).Length);
            return cost;
        }

        // bm_anime_list: has-animations, flags, bicycle-slope, pad, then four animation ids ending at -1.
        private static long AnimationCost(int model)
        {
            byte[] entry = File.ReadAllBytes(Member(DirNames.buildingAnimListOut, model));
            if (entry.Length < 20 || entry[0] == 0 || (entry[1] & 1) != 0) return 0;
            long cost = 0;
            for (int i = 0; i < 4; i++)
            {
                int id = BitConverter.ToInt32(entry, 4 + 4 * i);
                if (id == -1) break;
                cost += Block(Length(DirNames.buildingAnimations, id));
            }
            return cost;
        }

        // A texture file is cut back to its TEX0 texture data offset once the textures are in VRAM.
        private static long KeptTextureSize(string path)
        {
            using var reader = new BinaryReader(File.OpenRead(path));
            if (reader.BaseStream.Length < 0x14) return reader.BaseStream.Length;
            reader.BaseStream.Position = 0x10;
            uint tex0 = reader.ReadUInt32();
            if (tex0 + 0x18 > reader.BaseStream.Length) return reader.BaseStream.Length;
            reader.BaseStream.Position = tex0 + 0x14;
            return tex0 + reader.ReadUInt32();
        }

        private static string _sizesPath;
        private static long[] _sizes;

        private static long[] BackgroundSizes()
        {
            string path = WeatherSysNarcPath;
            if (_sizes != null && _sizesPath == path) return _sizes;
            if (!File.Exists(path)) return null;
            var narc = Narc.Open(path);
            if (narc == null) return null;
            try
            {
                var sizes = new long[narc.ElementCount];
                for (int i = 0; i < sizes.Length; i++) sizes[i] = narc.GetElementBytes(i).Length;
                _sizesPath = path;
                return _sizes = sizes;
            }
            finally { narc.Free(); }
        }

        private static string Member(DirNames dir, int id) => Path.Combine(gameDirs[dir].unpackedDir, id.ToString("D4"));

        private static long Length(DirNames dir, int id) => new FileInfo(Member(dir, id)).Length;
    }
}
