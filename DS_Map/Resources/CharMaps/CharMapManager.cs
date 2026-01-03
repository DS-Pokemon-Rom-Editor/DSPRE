using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace DSPRE.CharMaps
{
    internal static class CharMapManager
    {

        private static Dictionary<ushort, string> decodeMap;
        private static Dictionary<string, ushort> encodeMap;
        private static Dictionary<ushort, string> commandMap;

        public static readonly string charmapFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools", "charmap.json");
        public static readonly string customCharmapFilePath = Path.Combine(Program.DspreDataPath, "charmap.json");
        private static string loadedCharmapPath = null;

        /// <summary>
        /// Retrieves the file path of the character map JSON file to be used by the application.
        /// </summary>
        /// <remarks>This method checks for the existence of a custom character map file in the user data
        /// directory. If the custom file is not found, it falls back to the default character map file in the
        /// application directory. If neither file is found, an exception is thrown.</remarks>
        /// <returns>The file path of the character map JSON file. This will be either the custom file path or the default file
        /// path, depending on which file is available.</returns>
        /// <exception cref="FileNotFoundException">Thrown if neither the custom character map file nor the default character map file exists.</exception>
        public static string GetCharMapPath()
        {
            if (loadedCharmapPath != null)
            {
                return loadedCharmapPath;
            }

            if (File.Exists(customCharmapFilePath))
            {
                AppLogger.Info("Loading custom charmap from user data directory.");
                loadedCharmapPath = customCharmapFilePath;
                return customCharmapFilePath;
            }
            else if (File.Exists(charmapFilePath))
            {
                AppLogger.Info("Loading default charmap from application directory.");
                loadedCharmapPath = charmapFilePath;
                return charmapFilePath;
            }
            else
            {
                throw new FileNotFoundException("No charmap JSON file found in application or user data directory.");
            }
        }

        
        public static Version GetCharMapVersion(string path)
        {   
            string charmapContent = File.ReadAllText(path);
            JsonDocument charmap = JsonDocument.Parse(charmapContent);

            string version = null;

            // Get the metadata object and extract the version property
            if (charmap.RootElement.TryGetProperty("metadata", out JsonElement metadataElement))
            {
                if (metadataElement.TryGetProperty("version", out JsonElement versionElement))
                {
                    version = versionElement.GetString();
                }
            }

            if (version != null && Version.TryParse(version, out Version ver))
            {
                return ver;
            }
            else
            {
                AppLogger.Warn("Charmap version not found or invalid, defaulting to 1.0");
                return new Version(1, 0);
            }
        }

        /// <summary>
        /// Determines whether the custom character map is outdated compared to the default character map.
        /// </summary>
        /// <remarks>This method compares the versions of the default and custom character maps to
        /// determine if the custom map needs to be updated.</remarks>
        /// <returns><see langword="true"/> if the version of the custom character map is older than the version of the default
        /// character map; otherwise, <see langword="false"/>.</returns>
        public static bool IsCustomMapOutdated()
        {
            // Check if custom charmap file exists
            if (File.Exists(customCharmapFilePath))
            {
                // Both files exist, compare versions
                try
                {
                    var defaultVersion = GetCharMapVersion(charmapFilePath);
                    var customVersion = GetCharMapVersion(customCharmapFilePath);

                    return customVersion < defaultVersion;
                }
                catch (Exception ex)
                {
                    AppLogger.Error($"Error comparing charmap versions: {ex.Message}");
                    return false; // Don't treat as outdated if comparison failed
                }
            }

            return false; // Custom charmap does not exist, so not outdated

        }

        /// <summary>
        /// Creates a custom character map file by copying the default character map file to a specified location.
        /// </summary>
        /// <remarks>This method ensures that the directory for the custom character map file exists
        /// before attempting to copy the file. If the operation succeeds, the custom character map file will overwrite
        /// any existing file at the target location.</remarks>
        /// <returns><see langword="true"/> if the custom character map file is successfully created; otherwise, <see
        /// langword="false"/>.</returns>
        public static bool CreateCustomCharMapFile()
        {
            try
            {
                if (!Directory.Exists(Path.GetDirectoryName(customCharmapFilePath)))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(customCharmapFilePath));
                }
                File.Copy(charmapFilePath, customCharmapFilePath, overwrite: true);
                AppLogger.Info("Custom charmap file created successfully.");
                return true;
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Failed to create custom charmap file: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Deletes the custom character map file if it exists.
        /// </summary>
        /// <remarks>This method checks for the existence of the custom character map file at the
        /// predefined path and deletes it if found.</remarks>
        /// <returns><see langword="true"/> if the operation completes successfully, regardless of whether the file existed;
        /// otherwise, <see langword="false"/> if an error occurs during the deletion process.</returns>
        public static bool DeleteCustomCharMapFile()
        {
            try
            {
                if (File.Exists(customCharmapFilePath))
                {
                    File.Delete(customCharmapFilePath);
                    AppLogger.Info("Custom charmap file deleted successfully.");
                }
                else
                {
                    AppLogger.Info("No custom charmap file to delete.");
                }
                return true;
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Failed to delete custom charmap file: {ex.Message}");
                return false;
            }
        }

    }
}
