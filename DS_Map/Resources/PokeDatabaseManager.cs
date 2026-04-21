using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DSPRE.Resources
{
    /// <summary>
    /// Manages custom PokeDatabase configurations, allowing users to create and load
    /// custom versions of game data (Weather, Camera, Music, Pokemon names, etc.)
    /// </summary>
    public static class PokeDatabaseManager
    {
        private static readonly string CustomDatabasePath = Path.Combine(Program.DspreDataPath, "customPokedatabase");

        /// <summary>
        /// Event raised when a custom database is loaded, signaling editors to refresh
        /// </summary>
        public static event EventHandler DatabaseReloaded;

        /// <summary>
        /// Notifies all open editors that implement IPokedatabaseDependent to reload
        /// </summary>
        private static void NotifyEditorsToReload()
        {
            // Call ReloadPokeDatabase on all editors that implement IPokedatabaseDependent
            if (EditorPanels.MainProgram == null)
                return;

            var editors = new List<object>
            {
                EditorPanels.headerEditor,
                EditorPanels.cameraEditor,
                EditorPanels.tableEditor,
                // Add other editors here as they implement IPokedatabaseDependent
            };

            foreach (var editor in editors)
            {
                if (editor is IPokedatabaseDependent dependent)
                {
                    try
                    {
                        dependent.ReloadPokeDatabase();
                        AppLogger.Debug($"Reloaded PokeDatabase for {editor.GetType().Name}");
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Error($"Failed to reload PokeDatabase for {editor.GetType().Name}: {ex.Message}");
                    }
                }
            }

            // Raise the event for any other subscribers
            DatabaseReloaded?.Invoke(null, EventArgs.Empty);
        }

        static PokeDatabaseManager()
        {
            // Ensure custom database directory exists
            if (!Directory.Exists(CustomDatabasePath))
            {
                Directory.CreateDirectory(CustomDatabasePath);
            }
        }

        #region Data Classes for Serialization

        public class CustomPokeDatabaseData
        {
            // Weather dictionaries
            public Dictionary<int, string> DPWeatherDict { get; set; }
            public Dictionary<int, string> PtWeatherDict { get; set; }
            public Dictionary<int, string> HGSSWeatherDict { get; set; }

            // Camera dictionaries
            public Dictionary<int, string> DPPtCameraDict { get; set; }
            public Dictionary<int, string> HGSSCameraDict { get; set; }

            // Music dictionaries
            public Dictionary<ushort, string> DPMusicDict { get; set; }
            public Dictionary<ushort, string> PtMusicDict { get; set; }
            public Dictionary<ushort, string> HGSSMusicDict { get; set; }

            // Pokemon dictionary
            public Dictionary<int, string> PokemonDict { get; set; }

            // Area data
            public string[] PtAreaIconValues { get; set; }
            public Dictionary<byte, string> HGSSAreaIconsDict { get; set; }
            public string[] HGSSAreaProperties { get; set; }

            // Show name values
            public string[] DPShowNameValues { get; set; }
            public string[] PtShowNameValues { get; set; }

            public CustomPokeDatabaseData()
            {
                // Initialize with current PokeDatabase values
                DPWeatherDict = new Dictionary<int, string>(PokeDatabase.Weather.DPWeatherDict);
                PtWeatherDict = new Dictionary<int, string>(PokeDatabase.Weather.PtWeatherDict);
                HGSSWeatherDict = new Dictionary<int, string>(PokeDatabase.Weather.HGSSWeatherDict);

                DPPtCameraDict = new Dictionary<int, string>(PokeDatabase.CameraAngles.DPPtCameraDict);
                HGSSCameraDict = new Dictionary<int, string>(PokeDatabase.CameraAngles.HGSSCameraDict);

                DPMusicDict = new Dictionary<ushort, string>(PokeDatabase.MusicDB.DPMusicDict);
                PtMusicDict = new Dictionary<ushort, string>(PokeDatabase.MusicDB.PtMusicDict);
                HGSSMusicDict = new Dictionary<ushort, string>(PokeDatabase.MusicDB.HGSSMusicDict);

                PokemonDict = new Dictionary<int, string>(PokeDatabase.System.pokeNames.ToDictionary(k => (int)k.Key, v => v.Value));

                PtAreaIconValues = (string[])PokeDatabase.Area.PtAreaIconValues.Clone();
                HGSSAreaIconsDict = new Dictionary<byte, string>(PokeDatabase.Area.HGSSAreaIconsDict);
                HGSSAreaProperties = (string[])PokeDatabase.Area.HGSSAreaProperties.Clone();

                DPShowNameValues = (string[])PokeDatabase.ShowName.DPShowNameValues.Clone();
                PtShowNameValues = (string[])PokeDatabase.ShowName.PtShowNameValues.Clone();
            }
        }

        #endregion

        #region Save/Load Operations

        /// <summary>
        /// Saves a custom PokeDatabase to a JSON file
        /// </summary>
        public static bool SaveCustomDatabase(string databaseName, CustomPokeDatabaseData data)
        {
            try
            {
                string filePath = Path.Combine(CustomDatabasePath, $"{databaseName}.json");
                string json = JsonConvert.SerializeObject(data, Formatting.Indented);
                File.WriteAllText(filePath, json);

                AppLogger.Info($"Saved custom PokeDatabase: {databaseName}");
                return true;
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Failed to save custom PokeDatabase '{databaseName}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Loads a custom PokeDatabase from a JSON file and applies it to PokeDatabase
        /// </summary>
        public static bool LoadCustomDatabase(string databaseName)
        {
            try
            {
                string filePath = Path.Combine(CustomDatabasePath, $"{databaseName}.json");
                if (!File.Exists(filePath))
                {
                    AppLogger.Warn($"Custom PokeDatabase file not found: {databaseName}");
                    return false;
                }

                string json = File.ReadAllText(filePath);
                CustomPokeDatabaseData data = JsonConvert.DeserializeObject<CustomPokeDatabaseData>(json);

                ApplyCustomDatabase(data);

                AppLogger.Info($"Loaded custom PokeDatabase: {databaseName}");

                // Notify editors to reload
                NotifyEditorsToReload();

                return true;
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Failed to load custom PokeDatabase '{databaseName}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Resets PokeDatabase to default values (reloads from code defaults)
        /// </summary>
        public static void ResetToDefaults()
        {
            AppLogger.Info("Resetting PokeDatabase to default values - not implemented yet as defaults are static");
            // Note: This would require storing original values or reloading from a backup
            // For now, this is a placeholder

            // Notify editors to reload
            NotifyEditorsToReload();
        }

        /// <summary>
        /// Applies custom database data to the static PokeDatabase class
        /// </summary>
        private static void ApplyCustomDatabase(CustomPokeDatabaseData data)
        {
            // Note: Since PokeDatabase uses static dictionaries, we need to clear and repopulate them
            // This is a limitation of the current design, but works for runtime editing

            // Weather
            PokeDatabase.Weather.DPWeatherDict.Clear();
            foreach (var kvp in data.DPWeatherDict)
                PokeDatabase.Weather.DPWeatherDict[kvp.Key] = kvp.Value;

            PokeDatabase.Weather.PtWeatherDict.Clear();
            foreach (var kvp in data.PtWeatherDict)
                PokeDatabase.Weather.PtWeatherDict[kvp.Key] = kvp.Value;

            PokeDatabase.Weather.HGSSWeatherDict.Clear();
            foreach (var kvp in data.HGSSWeatherDict)
                PokeDatabase.Weather.HGSSWeatherDict[kvp.Key] = kvp.Value;

            // Camera
            PokeDatabase.CameraAngles.DPPtCameraDict.Clear();
            foreach (var kvp in data.DPPtCameraDict)
                PokeDatabase.CameraAngles.DPPtCameraDict[kvp.Key] = kvp.Value;

            PokeDatabase.CameraAngles.HGSSCameraDict.Clear();
            foreach (var kvp in data.HGSSCameraDict)
                PokeDatabase.CameraAngles.HGSSCameraDict[kvp.Key] = kvp.Value;

            // Music
            PokeDatabase.MusicDB.DPMusicDict.Clear();
            foreach (var kvp in data.DPMusicDict)
                PokeDatabase.MusicDB.DPMusicDict[kvp.Key] = kvp.Value;

            PokeDatabase.MusicDB.PtMusicDict.Clear();
            foreach (var kvp in data.PtMusicDict)
                PokeDatabase.MusicDB.PtMusicDict[kvp.Key] = kvp.Value;

            PokeDatabase.MusicDB.HGSSMusicDict.Clear();
            foreach (var kvp in data.HGSSMusicDict)
                PokeDatabase.MusicDB.HGSSMusicDict[kvp.Key] = kvp.Value;

            // Pokemon
            PokeDatabase.System.pokeNames.Clear();
            foreach (var kvp in data.PokemonDict)
                PokeDatabase.System.pokeNames[(ushort)kvp.Key] = kvp.Value;

            // Arrays (need to replace references)
            PokeDatabase.Area.PtAreaIconValues = data.PtAreaIconValues;

            PokeDatabase.Area.HGSSAreaIconsDict.Clear();
            foreach (var kvp in data.HGSSAreaIconsDict)
                PokeDatabase.Area.HGSSAreaIconsDict[kvp.Key] = kvp.Value;

            PokeDatabase.Area.HGSSAreaProperties = data.HGSSAreaProperties;

            PokeDatabase.ShowName.DPShowNameValues = data.DPShowNameValues;
            PokeDatabase.ShowName.PtShowNameValues = data.PtShowNameValues;
        }

        /// <summary>
        /// Gets a list of all available custom database files
        /// </summary>
        public static List<string> GetAvailableDatabases()
        {
            try
            {
                if (!Directory.Exists(CustomDatabasePath))
                    return new List<string>();

                return Directory.GetFiles(CustomDatabasePath, "*.json")
                    .Select(f => Path.GetFileNameWithoutExtension(f))
                    .OrderBy(name => name)
                    .ToList();
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Failed to get available databases: {ex.Message}");
                return new List<string>();
            }
        }

        /// <summary>
        /// Creates a snapshot of the current PokeDatabase state
        /// </summary>
        public static CustomPokeDatabaseData CreateSnapshot()
        {
            return new CustomPokeDatabaseData();
        }

        #endregion
    }
}
