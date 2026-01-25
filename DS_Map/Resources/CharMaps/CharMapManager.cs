using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Linq;

namespace DSPRE.CharMaps
{
    #region Charmap Data Structures
    
    /// <summary>
    /// Represents the complete charmap JSON structure
    /// </summary>
    public class CharMap
    {
        [JsonPropertyName("metadata")]
        public CharMapMetadata Metadata { get; set; }
        
        [JsonPropertyName("char_map")]
        public Dictionary<string, CharMapEntry> CharacterMap { get; set; }
        
        [JsonPropertyName("command_map")]
        public Dictionary<string, string> CommandMap { get; set; }
        
        public CharMap()
        {
            Metadata = new CharMapMetadata();
            CharacterMap = new Dictionary<string, CharMapEntry>();
            CommandMap = new Dictionary<string, string>();
        }
        
        /// <summary>
        /// Get a character map entry by hex code (supports both ushort and int)
        /// </summary>
        public CharMapEntry GetEntry(ushort code)
        {
            string hexKey = code.ToString("X4");
            return CharacterMap.TryGetValue(hexKey, out CharMapEntry entry) ? entry : null;
        }
        
        /// <summary>
        /// Get a character map entry by hex code (supports both ushort and int)
        /// </summary>
        public CharMapEntry GetEntry(int code)
        {
            return GetEntry((ushort)code);
        }
        
        /// <summary>
        /// Set or update a character map entry by hex code
        /// </summary>
        public void SetEntry(ushort code, CharMapEntry entry)
        {
            string hexKey = code.ToString("X4");
            CharacterMap[hexKey] = entry;
        }
        
        /// <summary>
        /// Set or update a character map entry by hex code
        /// </summary>
        public void SetEntry(int code, CharMapEntry entry)
        {
            SetEntry((ushort)code, entry);
        }
        
        /// <summary>
        /// Remove a character map entry by hex code
        /// </summary>
        public bool RemoveEntry(ushort code)
        {
            string hexKey = code.ToString("X4");
            return CharacterMap.Remove(hexKey);
        }
        
        /// <summary>
        /// Remove a character map entry by hex code
        /// </summary>
        public bool RemoveEntry(int code)
        {
            return RemoveEntry((ushort)code);
        }
        
        /// <summary>
        /// Check if a character map entry exists for the given code
        /// </summary>
        public bool HasEntry(ushort code)
        {
            string hexKey = code.ToString("X4");
            return CharacterMap.ContainsKey(hexKey);
        }
        
        /// <summary>
        /// Check if a character map entry exists for the given code
        /// </summary>
        public bool HasEntry(int code)
        {
            return HasEntry((ushort)code);
        }
        
        /// <summary>
        /// Get all character codes as ushort values
        /// </summary>
        public IEnumerable<ushort> GetAllCodes()
        {
            foreach (string hexKey in CharacterMap.Keys)
            {
                if (ushort.TryParse(hexKey, System.Globalization.NumberStyles.HexNumber, null, out ushort code))
                {
                    yield return code;
                }
            }
        }
        
        /// <summary>
        /// Find the code for a given character or alias
        /// </summary>
        public ushort? FindCode(string characterOrAlias)
        {
            // First check if it's a direct character match
            foreach (var kvp in CharacterMap)
            {
                if (kvp.Value.Character == characterOrAlias)
                {
                    if (ushort.TryParse(kvp.Key, System.Globalization.NumberStyles.HexNumber, null, out ushort code))
                    {
                        return code;
                    }
                }
            }
            
            // Then check aliases
            foreach (var kvp in CharacterMap)
            {
                if (kvp.Value.Aliases != null && kvp.Value.Aliases.Contains(characterOrAlias))
                {
                    if (ushort.TryParse(kvp.Key, System.Globalization.NumberStyles.HexNumber, null, out ushort code))
                    {
                        return code;
                    }
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// Create a deep clone of this CharMap
        /// </summary>
        public CharMap Clone()
        {
            CharMap cloned = new CharMap();
            
            // Clone metadata
            cloned.Metadata = new CharMapMetadata
            {
                Description = this.Metadata.Description,
                Version = this.Metadata.Version
            };
            
            // Clone character map
            foreach (var kvp in this.CharacterMap)
            {
                CharMapEntry clonedEntry = new CharMapEntry(
                    kvp.Value.Character,
                    kvp.Value.Aliases != null ? new List<string>(kvp.Value.Aliases) : null
                );
                cloned.CharacterMap[kvp.Key] = clonedEntry;
            }
            
            // Clone command map
            foreach (var kvp in this.CommandMap)
            {
                cloned.CommandMap[kvp.Key] = kvp.Value;
            }
            
            return cloned;
        }
    }
    
    /// <summary>
    /// Represents metadata information about the charmap
    /// </summary>
    public class CharMapMetadata
    {
        [JsonPropertyName("description")]
        public string Description { get; set; }
        
        [JsonPropertyName("version")]
        public string Version { get; set; }
        
        public CharMapMetadata()
        {
            Description = "";
            Version = "1.0.0";
        }
    }
    
    /// <summary>
    /// Represents a single character mapping entry
    /// </summary>
    public class CharMapEntry
    {
        [JsonPropertyName("char")]
        public string Character { get; set; }
        
        [JsonPropertyName("aliases")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string> Aliases { get; set; }
        
        public CharMapEntry()
        {
            Character = "";
        }
        
        public CharMapEntry(string character, List<string> aliases = null)
        {
            Character = character;
            Aliases = aliases;
        }
        
        /// <summary>
        /// Add an alias to this entry
        /// </summary>
        public void AddAlias(string alias)
        {
            if (Aliases == null)
            {
                Aliases = new List<string>();
            }
            
            if (!Aliases.Contains(alias))
            {
                Aliases.Add(alias);
            }
        }
        
        /// <summary>
        /// Remove an alias from this entry
        /// </summary>
        public bool RemoveAlias(string alias)
        {
            if (Aliases == null)
            {
                return false;
            }
            
            bool removed = Aliases.Remove(alias);
            
            // Clean up the list if it's now empty
            if (Aliases.Count == 0)
            {
                Aliases = null;
            }
            
            return removed;
        }
        
        /// <summary>
        /// Check if this entry has a specific alias
        /// </summary>
        public bool HasAlias(string alias)
        {
            return Aliases != null && Aliases.Contains(alias);
        }
    }
    
    /// <summary>
    /// Strategy for handling conflicts during charmap merge
    /// </summary>
    public enum MergeStrategy
    {
        /// <summary>
        /// Keep the custom (target) value when there's a conflict
        /// </summary>
        PreferCustom,
        
        /// <summary>
        /// Take the base (source) value when there's a conflict
        /// </summary>
        PreferBase,

        /// <summary>
        /// Replace the entire base map with the custom map
        /// </summary>
        ReplaceBase
    }
    
    /// <summary>
    /// Result of a charmap merge operation
    /// </summary>
    public class MergeResult
    {
        public CharMap MergedMap { get; set; }
        public int AddedEntries { get; set; }
        public int UpdatedEntries { get; set; }
        public int ConflictingEntries { get; set; }
        public int AddedAliases { get; set; }
        public List<string> Conflicts { get; set; }
        
        public MergeResult()
        {
            Conflicts = new List<string>();
        }
        
        public string GetSummary()
        {
            return $"Summary:\n" +
                   $"- Added entries: {AddedEntries}\n" +
                   $"- Updated entries: {UpdatedEntries}\n" +
                   $"- Conflicting entries: {ConflictingEntries}\n" +
                   $"- Added aliases: {AddedAliases}\n" +
                   $"- Total conflicts: {Conflicts.Count}";
        }

        public string GetConflictDetails()
        {
            return string.Join("\n", Conflicts);
        }
    }
    
    #endregion
    
    internal static class CharMapManager
    {
        public static readonly string charmapFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools", "charmap.json");
        public static readonly string customCharmapFilePath = Path.Combine(Program.DspreDataPath, "charmap.json");
        private static string loadedCharmapPath = null;
        
        // Cache the loaded charmap for quick access
        private static CharMap cachedCharMap = null;

        #region Serialization / Deserialization

        /// <summary>
        /// Deserialize a charmap from a JSON file
        /// </summary>
        /// <param name="filePath">Path to the charmap JSON file</param>
        /// <returns>Deserialized CharMap object</returns>
        public static CharMap DeserializeCharMap(string filePath)
        {
            try
            {
                string jsonContent = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
                
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = false,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true,
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };
                
                CharMap charMap = JsonSerializer.Deserialize<CharMap>(jsonContent, options);
                
                if (charMap == null)
                {
                    throw new InvalidDataException("Failed to deserialize charmap - result was null");
                }
                
                AppLogger.Debug($"Successfully deserialized charmap from {filePath} with {charMap.CharacterMap.Count} character entries and {charMap.CommandMap.Count} command entries");
                
                return charMap;
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Failed to deserialize charmap from {filePath}: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// Serialize a CharMap object to a JSON file
        /// </summary>
        /// <param name="charMap">CharMap object to serialize</param>
        /// <param name="filePath">Path where the JSON file will be saved</param>
        public static void SerializeCharMap(CharMap charMap, string filePath)
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                    DefaultIgnoreCondition = JsonIgnoreCondition.Never
                };
                
                string jsonString = JsonSerializer.Serialize(charMap, options);
                
                // Ensure directory exists
                string directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                // Write without BOM for consistency
                File.WriteAllText(filePath, jsonString, new System.Text.UTF8Encoding(false));
                
                AppLogger.Debug($"Successfully serialized charmap to {filePath} with {charMap.CharacterMap.Count} character entries and {charMap.CommandMap.Count} command entries");
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Failed to serialize charmap to {filePath}: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// Load and cache the current charmap
        /// </summary>
        /// <param name="forceReload">If true, forces reloading even if already cached</param>
        /// <returns>The loaded CharMap object</returns>
        public static CharMap LoadCharMap(bool forceReload = false)
        {
            if (cachedCharMap != null && !forceReload)
            {
                return cachedCharMap;
            }
            
            string charmapPath = GetCharMapPath();
            cachedCharMap = DeserializeCharMap(charmapPath);
            
            return cachedCharMap;
        }
        
        /// <summary>
        /// Save the current charmap to the appropriate file
        /// </summary>
        /// <param name="charMap">CharMap object to save</param>
        /// <param name="saveToCustomPath">If true, saves to custom charmap path; otherwise uses current loaded path</param>
        public static void SaveCharMap(CharMap charMap, bool saveToCustomPath = true)
        {
            string savePath = saveToCustomPath ? customCharmapFilePath : GetCharMapPath();
            SerializeCharMap(charMap, savePath);
            
            // Update cache
            cachedCharMap = charMap;
            
            // Update loaded path if saving to custom
            if (saveToCustomPath)
            {
                loadedCharmapPath = customCharmapFilePath;
            }
        }
        
        /// <summary>
        /// Get the currently loaded or cached charmap
        /// </summary>
        public static CharMap GetCurrentCharMap()
        {
            return LoadCharMap();
        }
        
        /// <summary>
        /// Clear the cached charmap, forcing a reload on next access
        /// </summary>
        public static void ClearCache()
        {
            cachedCharMap = null;
            loadedCharmapPath = null;
            AppLogger.Debug("Cleared charmap cache");
        }

        #endregion

        #region Merge Operations

        /// <summary>
        /// Merge two charmaps together, typically used to rebase custom map onto updated default map
        /// </summary>
        /// <param name="baseMap">The base charmap (usually the updated default)</param>
        /// <param name="customMap">The custom charmap to merge onto base</param>
        /// <param name="strategy">Strategy for handling conflicts</param>
        /// <returns>Result of the merge operation including the merged map and statistics</returns>
        public static MergeResult MergeCharMaps(CharMap baseMap, CharMap customMap, MergeStrategy strategy = MergeStrategy.PreferCustom)
        {
            MergeResult result = new MergeResult();

            // If strategy is ReplaceBase, just return the custom map
            if (strategy == MergeStrategy.ReplaceBase)
            {
                result.MergedMap = customMap.Clone();
                result.MergedMap.Metadata.Version = baseMap.Metadata.Version; // Update version to base
                result.AddedEntries = customMap.CharacterMap.Count;
                AppLogger.Info("Charmap merge completed with ReplaceBase strategy.");
                return result;
            }

            result.MergedMap = baseMap.Clone();
            
            // Update metadata from custom map
            result.MergedMap.Metadata.Description = customMap.Metadata.Description;
            result.MergedMap.Metadata.Version = baseMap.Metadata.Version; // Keep base version
            
            // Merge character maps
            foreach (var customEntry in customMap.CharacterMap)
            {
                string hexCode = customEntry.Key;
                CharMapEntry customValue = customEntry.Value;
                
                if (result.MergedMap.CharacterMap.ContainsKey(hexCode))
                {
                    // Entry exists in base - check for conflicts
                    CharMapEntry baseValue = result.MergedMap.CharacterMap[hexCode];
                    
                    bool hasConflict = baseValue.Character != customValue.Character;
                    
                    if (hasConflict)
                    {
                        result.ConflictingEntries++;
                        result.Conflicts.Add($"0x{hexCode}: Base='{baseValue.Character}' vs Custom='{customValue.Character}'");
                        
                        switch (strategy)
                        {
                            case MergeStrategy.PreferCustom:
                                // Keep custom character, merge aliases
                                result.MergedMap.CharacterMap[hexCode] = new CharMapEntry(
                                    customValue.Character,
                                    MergeAliasLists(baseValue.Aliases, customValue.Aliases)
                                );
                                result.UpdatedEntries++;
                                break;
                                
                            case MergeStrategy.PreferBase:
                                // Keep base character, add custom aliases if they don't conflict
                                if (customValue.Aliases != null)
                                {
                                    foreach (string alias in customValue.Aliases)
                                    {
                                        if (baseValue.Aliases == null || !baseValue.Aliases.Contains(alias))
                                        {
                                            baseValue.AddAlias(alias);
                                            result.AddedAliases++;
                                        }
                                    }
                                }
                                break;
                        }
                    }
                    else
                    {
                        // No conflict in character, just merge aliases
                        List<string> mergedAliases = MergeAliasLists(baseValue.Aliases, customValue.Aliases);
                        if (mergedAliases != null && mergedAliases.Count > (baseValue.Aliases?.Count ?? 0))
                        {
                            result.MergedMap.CharacterMap[hexCode] = new CharMapEntry(
                                baseValue.Character,
                                mergedAliases
                            );
                            result.AddedAliases += mergedAliases.Count - (baseValue.Aliases?.Count ?? 0);
                        }
                    }
                }
                else
                {
                    // New entry from custom map
                    result.MergedMap.CharacterMap[hexCode] = new CharMapEntry(
                        customValue.Character,
                        customValue.Aliases != null ? new List<string>(customValue.Aliases) : null
                    );
                    result.AddedEntries++;
                }
            }
            
            // Merge command maps 
            foreach (var customCommand in customMap.CommandMap)
            {
                if (!result.MergedMap.CommandMap.ContainsKey(customCommand.Key))
                {
                    result.MergedMap.CommandMap[customCommand.Key] = customCommand.Value;
                }
                else if (result.MergedMap.CommandMap[customCommand.Key] != customCommand.Value)
                {
                    // Command conflict
                    result.Conflicts.Add($"Command 0x{customCommand.Key}: Base='{result.MergedMap.CommandMap[customCommand.Key]}' vs Custom='{customCommand.Value}'");

                    if (strategy == MergeStrategy.PreferCustom)
                    {
                        result.MergedMap.CommandMap[customCommand.Key] = customCommand.Value;
                    }                    
                }
            }
            
            AppLogger.Info($"Charmap merge completed: {result.GetSummary()}");
            
            return result;
        }
        
        /// <summary>
        /// Merge two alias lists, removing duplicates
        /// </summary>
        private static List<string> MergeAliasLists(List<string> list1, List<string> list2)
        {
            if (list1 == null && list2 == null)
                return null;
                
            if (list1 == null)
                return new List<string>(list2);
                
            if (list2 == null)
                return new List<string>(list1);
            
            // Combine and remove duplicates while preserving order
            HashSet<string> seen = new HashSet<string>();
            List<string> merged = new List<string>();
            
            foreach (string alias in list1)
            {
                if (seen.Add(alias))
                {
                    merged.Add(alias);
                }
            }
            
            foreach (string alias in list2)
            {
                if (seen.Add(alias))
                {
                    merged.Add(alias);
                }
            }
            
            return merged.Count > 0 ? merged : null;
        }
        
        /// <summary>
        /// Merge the custom charmap with the default charmap (rebase operation)
        /// </summary>
        /// <param name="strategy">Strategy for handling conflicts</param>
        /// <returns>Result of the merge operation</returns>
        public static MergeResult MergeCustomWithDefault(MergeStrategy strategy = MergeStrategy.PreferCustom)
        {
            if (!File.Exists(customCharmapFilePath))
            {
                throw new FileNotFoundException("Custom charmap does not exist. Cannot merge.");
            }
            
            CharMap defaultMap = DeserializeCharMap(charmapFilePath);
            CharMap customMap = DeserializeCharMap(customCharmapFilePath);
            
            return MergeCharMaps(defaultMap, customMap, strategy);
        }
        
        #endregion

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
                ClearCache(); // Clear cache to force reload with new custom file
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
                    ClearCache(); // Clear cache to force reload with default file
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
