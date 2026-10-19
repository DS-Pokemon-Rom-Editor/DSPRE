﻿using System.IO;

namespace DSPRE
{
    public static class Filesystem
    {
        public static string EventFiles => RomInfo.GameDirs[RomInfo.DirNames.EventFiles].unpackedDir;
        public static string OWSprites => RomInfo.GameDirs[RomInfo.DirNames.OWSprites].unpackedDir;
        public static string MapTextures => RomInfo.GameDirs[RomInfo.DirNames.MapTextures].unpackedDir;
        public static string BuildingTextures => RomInfo.GameDirs[RomInfo.DirNames.BuildingTextures].unpackedDir;
        public static string DynamicHeaders => RomInfo.GameDirs[RomInfo.DirNames.DynamicHeaders].unpackedDir;
        public static string DynamicHeadersPacked => RomInfo.GameDirs[RomInfo.DirNames.DynamicHeaders].packedDir;
        public static string Scripts => RomInfo.GameDirs[RomInfo.DirNames.Scripts].unpackedDir;
        public static string Maps => RomInfo.GameDirs[RomInfo.DirNames.Maps].unpackedDir;
        public static string Matrices => RomInfo.GameDirs[RomInfo.DirNames.Matrices].unpackedDir;
        public static string BuildingConfigFiles => RomInfo.GameDirs[RomInfo.DirNames.BuildingConfigFiles].unpackedDir;
        public static string AreaData => RomInfo.GameDirs[RomInfo.DirNames.AreaData].unpackedDir;
        public static string TextArchives => RomInfo.GameDirs[RomInfo.DirNames.TextArchives].unpackedDir;
        public static string TrainerProperties => RomInfo.GameDirs[RomInfo.DirNames.TrainerProperties].unpackedDir;
        public static string TrainerParty => RomInfo.GameDirs[RomInfo.DirNames.TrainerParty].unpackedDir;
        public static string TrainerGraphics => RomInfo.GameDirs[RomInfo.DirNames.TrainerGraphics].unpackedDir;
        public static string Encounters => RomInfo.GameDirs[RomInfo.DirNames.Encounters].unpackedDir;
        public static string EncounterExtended => RomInfo.GameDirs.ContainsKey(RomInfo.DirNames.EncounterExtended) 
            ? RomInfo.GameDirs[RomInfo.DirNames.EncounterExtended].unpackedDir 
            : null;
        public static string Headbutt => RomInfo.GameDirs.ContainsKey(RomInfo.DirNames.Headbutt)
            ? RomInfo.GameDirs[RomInfo.DirNames.Headbutt].unpackedDir
            : null;
        public static string RockSmash => RomInfo.GameDirs.ContainsKey(RomInfo.DirNames.RockSmash)
            ? RomInfo.GameDirs[RomInfo.DirNames.RockSmash].unpackedDir
            : null;
        public static string SafariZone => RomInfo.GameDirs.ContainsKey(RomInfo.DirNames.SafariZone)
            ? RomInfo.GameDirs[RomInfo.DirNames.SafariZone].unpackedDir
            : null;
        public static string BattleTowerTrainers => RomInfo.GameDirs.ContainsKey(RomInfo.DirNames.BattleTowerTrainers)
            ? RomInfo.GameDirs[RomInfo.DirNames.BattleTowerTrainers].unpackedDir
            : null;
        public static string BattleTowerPokemon => RomInfo.GameDirs.ContainsKey(RomInfo.DirNames.BattleTowerPokemon)
            ? RomInfo.GameDirs[RomInfo.DirNames.BattleTowerPokemon].unpackedDir
            : null;
        public static string MonIcons => RomInfo.GameDirs[RomInfo.DirNames.MonIcons].unpackedDir;
        public static string SynthOverlay => RomInfo.GameDirs[RomInfo.DirNames.SynthOverlay].unpackedDir;
        public static string InteriorBuildingModels => RomInfo.GameDirs[RomInfo.DirNames.InteriorBuildingModels].unpackedDir;
        public static string ExteriorBuildingModels => RomInfo.GameDirs[RomInfo.DirNames.ExteriorBuildingModels].unpackedDir;

        /// <summary>
        /// Gets the path to the Bug Contest encounter file (HGSS only).
        /// This is a single file, not a NARC.
        /// Path in ROM filesystem: data/mushi/mushi_encount.bin
        /// </summary>
        public static string GetBugContestEncounterPath()
        {
            return Path.Combine(RomInfo.DataPath, "data", "mushi", "mushi_encount.bin");
        }

        /// <summary>
        /// Checks if the Bug Contest encounter file exists.
        /// </summary>
        public static bool BugContestEncounterFileExists()
        {
            return File.Exists(GetBugContestEncounterPath());
        }

        /// <summary>data/mushi/mushi_trainer.bin (HGSS only): the Bug-Catching Contest opponents.</summary>
        public static string GetBugContestTrainerPath()
        {
            return Path.Combine(RomInfo.DataPath, "data", "mushi", "mushi_trainer.bin");
        }

        public static string GetBuildingModelsDirPath(bool interior)
        {
            if (interior)
            {
                return InteriorBuildingModels;
            }
            else
            {
                return ExteriorBuildingModels;
            }
        }

        public static string ExpArmPath => Path.Combine(SynthOverlay, RomPatchState.ExpandedARMfileID.ToString("D4"));

        public static string GetPath(string path, int id, string format = "D4")
        {
            return Path.Combine(path, id.ToString(format));
        }

        public static string GetPath(string path, string prefix, int id, string ext, string format = "D4")
        {
            return Path.Combine(path, prefix + id.ToString(format) + "." + ext);
        }

        static string[] GetBuildingModelFiles(bool interior)
        {
            return string.IsNullOrWhiteSpace(Filesystem.GetBuildingModelsDirPath(interior)) ? null : Directory.GetFiles(Filesystem.GetBuildingModelsDirPath(interior));
        }

        public static string GetBuildingModelPath(bool interior, int id)
        {
            return GetPath(Filesystem.GetBuildingModelsDirPath(interior), id);
        }

        public static int GetBuildingCount(bool interior)
        {
            return GetBuildingModelFiles(interior).Length;
        }

        public static string[] GetAreaDataFiles()
        {
            return string.IsNullOrWhiteSpace(Filesystem.AreaData) ? null : Directory.GetFiles(Filesystem.AreaData);
        }

        public static string GetAreaDataPath(int id)
        {
            return GetPath(Filesystem.AreaData, id);
        }

        public static int GetAreaDataCount()
        {
            return GetAreaDataFiles().Length;
        }

        public static string GetTexturePath(bool useMapTiles, int textureID)
        {
            string path = Filesystem.GetMapTexturePath(textureID);
            string path2 = Filesystem.GetBuildingTexturePath(textureID);
            string tilesetPath = useMapTiles ? path : path2;
            return tilesetPath;
        }

        static string[] GetMapTextureFiles()
        {
            return string.IsNullOrWhiteSpace(Filesystem.MapTextures) ? null : Directory.GetFiles(Filesystem.MapTextures);
        }

        public static string GetMapTexturePath(int id)
        {
            return GetPath(Filesystem.MapTextures, id);
        }

        public static int GetMapTexturesCount()
        {
            return GetMapTextureFiles().Length;
        }

        static string[] GetBuildingTextureFiles()
        {
            return string.IsNullOrWhiteSpace(Filesystem.BuildingTextures) ? null : Directory.GetFiles(Filesystem.BuildingTextures);
        }

        public static string GetBuildingTexturePath(int id)
        {
            return GetPath(Filesystem.BuildingTextures, id);
        }

        public static int GetBuildingTexturesCount()
        {
            return GetBuildingTextureFiles().Length;
        }

        static string[] GetMatrixFiles()
        {
            return string.IsNullOrWhiteSpace(Filesystem.Matrices) ? null : Directory.GetFiles(Filesystem.Matrices);
        }

        public static string GetMatrixPath(int id)
        {
            return GetPath(Filesystem.Matrices, id);
        }

        public static int GetMatrixCount()
        {
            return GetMatrixFiles().Length;
        }

        static string[] GetTextArchiveFiles()
        {
            return string.IsNullOrWhiteSpace(Filesystem.TextArchives) ? null : Directory.GetFiles(Filesystem.TextArchives);
        }

        public static string GetTextArchivePath(int id)
        {
            return GetPath(Filesystem.TextArchives, id);
        }

        public static int GetTextArchivesCount()
        {
            return GetTextArchiveFiles().Length;
        }

        static string[] GetMapFiles()
        {
            return string.IsNullOrWhiteSpace(Filesystem.Maps) ? null : Directory.GetFiles(Filesystem.Maps);
        }

        public static string GetMapPath(int id)
        {
            return GetPath(Filesystem.Maps, id);
        }

        public static int GetMapCount()
        {
            return GetMapFiles().Length;
        }

        static string[] GetScriptFiles()
        {
            return string.IsNullOrWhiteSpace(Filesystem.Scripts) ? null : Directory.GetFiles(Filesystem.Scripts);
        }

        public static string GetScriptPath(int id)
        {
            return GetPath(Filesystem.Scripts, id);
        }

        public static int GetScriptCount()
        {
            return GetScriptFiles().Length;
        }

        static string[] GetEventFiles()
        {
            return string.IsNullOrWhiteSpace(Filesystem.EventFiles) ? null : Directory.GetFiles(Filesystem.EventFiles);
        }

        public static string GetEventPath(int id)
        {
            return GetPath(Filesystem.EventFiles, id);
        }

        public static int GetEventFileCount()
        {
            return GetEventFiles().Length;
        }

        static string[] GetTrainerPropertiesFiles()
        {
            return string.IsNullOrWhiteSpace(Filesystem.TrainerProperties) ? null : Directory.GetFiles(Filesystem.TrainerProperties);
        }

        public static string GetTrainerPropertiesPath(int id)
        {
            return GetPath(Filesystem.TrainerProperties, id);
        }

        public static int GetTrainerPropertiesCount()
        {
            return GetTrainerPropertiesFiles().Length;
        }

        static string[] GetDynamicHeaderFiles()
        {
            return string.IsNullOrWhiteSpace(Filesystem.DynamicHeaders) ? null : Directory.GetFiles(Filesystem.DynamicHeaders);
        }

        public static string GetDynamicHeaderPath(int id)
        {
            return GetPath(Filesystem.DynamicHeaders, id);
        }

        public static int GetDynamicHeadersCount()
        {
            return GetDynamicHeaderFiles().Length;
        }

        static string[] GetEncounterFiles()
        {
            return string.IsNullOrWhiteSpace(Filesystem.Encounters) ? null : Directory.GetFiles(Filesystem.Encounters);
        }

        public static string GetEncounterPath(int id)
        {
            return GetPath(Filesystem.Encounters, id);
        }

        public static int GetEncountersCount()
        {
            return GetEncounterFiles().Length;
        }

        static string[] GetSafariZoneFiles()
        {
            return string.IsNullOrWhiteSpace(Filesystem.SafariZone) ? null : Directory.GetFiles(Filesystem.SafariZone);
        }

        public static string GetSafariZonePath(int id)
        {
            return GetPath(Filesystem.SafariZone, id);
        }

        public static int GetSafariZoneCount()
        {
            string[] files = GetSafariZoneFiles();
            return files?.Length ?? 0;
        }

        static string[] GetHeadbuttFiles()
        {
            return string.IsNullOrWhiteSpace(Filesystem.Headbutt) ? null : Directory.GetFiles(Filesystem.Headbutt);
        }

        public static string GetHeadbuttPath(int id)
        {
            return GetPath(Filesystem.Headbutt, id);
        }

        public static int GetHeadbuttCount()
        {
            string[] files = GetHeadbuttFiles();
            return files?.Length ?? 0;
        }

        static string[] GetRockSmashFiles()
        {
            return string.IsNullOrWhiteSpace(Filesystem.RockSmash) ? null : Directory.GetFiles(Filesystem.RockSmash);
        }

        public static string GetRockSmashPath(int id)
        {
            return GetPath(Filesystem.RockSmash, id);
        }

        public static int GetRockSmashCount()
        {
            string[] files = GetRockSmashFiles();
            return files?.Length ?? 0;
        }

        public static string GetOWSpritePath(int id)
        {
            return GetPath(Filesystem.OWSprites, id);
        }

        public static string GetBuildingConfigPath(int id)
        {
            return GetPath(Filesystem.BuildingConfigFiles, id);
        }

        public static string GetTrainerPartyPath(int id)
        {
            return GetPath(Filesystem.TrainerParty, id);
        }

        public static string GetTrainerGraphicsPath(int id)
        {
            return GetPath(Filesystem.TrainerGraphics, id);
        }

        public static string GetSynthOerlayPath(int id)
        {
            return GetPath(Filesystem.SynthOverlay, id);
        }
    }
}
