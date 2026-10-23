using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Linq;

namespace DSPRE.CharMaps
{
    #region Charmap Data Structures
    
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
        
        public CharMapEntry GetEntry(ushort code)
        {
            string hexKey = code.ToString("X4");
            return CharacterMap.TryGetValue(hexKey, out CharMapEntry entry) ? entry : null;
        }
        
        public CharMapEntry GetEntry(int code)
        {
            return GetEntry((ushort)code);
        }
        
        public void SetEntry(ushort code, CharMapEntry entry)
        {
            string hexKey = code.ToString("X4");
            CharacterMap[hexKey] = entry;
        }
        
        public void SetEntry(int code, CharMapEntry entry)
        {
            SetEntry((ushort)code, entry);
        }
        
        public bool RemoveEntry(ushort code)
        {
            string hexKey = code.ToString("X4");
            return CharacterMap.Remove(hexKey);
        }
        
        public bool RemoveEntry(int code)
        {
            return RemoveEntry((ushort)code);
        }
        
        public bool HasEntry(ushort code)
        {
            string hexKey = code.ToString("X4");
            return CharacterMap.ContainsKey(hexKey);
        }
        
        public bool HasEntry(int code)
        {
            return HasEntry((ushort)code);
        }
        
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
        
        public ushort? FindCode(string characterOrAlias)
        {
            foreach (KeyValuePair<string, CharMapEntry> kvp in CharacterMap)
            {
                if (kvp.Value.Character == characterOrAlias)
                {
                    if (ushort.TryParse(kvp.Key, System.Globalization.NumberStyles.HexNumber, null, out ushort code))
                    {
                        return code;
                    }
                }
            }
            
            foreach (KeyValuePair<string, CharMapEntry> kvp in CharacterMap)
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
        
        public CharMap Clone()
        {
            CharMap cloned = new CharMap();
            
            cloned.Metadata = new CharMapMetadata
            {
                Description = this.Metadata.Description,
                Version = this.Metadata.Version
            };
            
            foreach (KeyValuePair<string, CharMapEntry> kvp in this.CharacterMap)
            {
                CharMapEntry clonedEntry = new CharMapEntry(
                    kvp.Value.Character,
                    kvp.Value.Aliases != null ? new List<string>(kvp.Value.Aliases) : null
                );
                cloned.CharacterMap[kvp.Key] = clonedEntry;
            }
            
            foreach (KeyValuePair<string, string> kvp in this.CommandMap)
            {
                cloned.CommandMap[kvp.Key] = kvp.Value;
            }
            
            return cloned;
        }
    }
    
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
        
        public bool RemoveAlias(string alias)
        {
            if (Aliases == null)
            {
                return false;
            }
            
            bool removed = Aliases.Remove(alias);
            
            if (Aliases.Count == 0)
            {
                Aliases = null;
            }
            
            return removed;
        }
        
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
        public static readonly string CharmapFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools", "charmap.json");
        public static readonly string CustomCharmapFilePath = Path.Combine(AppPaths.DspreDataPath, "charmap.json");
        private static string _loadedCharmapPath = null;
        
        private static CharMap _cachedCharMap = null;

        #region Serialization / Deserialization

        public static CharMap DeserializeCharMap(string filePath)
        {
            try
            {
                string jsonContent = File.ReadAllText(filePath, System.Text.Encoding.UTF8);

                JsonSerializerOptions options = new JsonSerializerOptions
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
                    throw new InvalidDataException("The character map file is empty.");
                }
                
                AppLogger.Debug($"Successfully deserialized charmap from {filePath} with {charMap.CharacterMap.Count} character entries and {charMap.CommandMap.Count} command entries");
                
                return charMap;
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Couldn't deserialize charmap from {filePath}: {ex.Message}");
                throw;
            }
        }
        
        public static void SerializeCharMap(CharMap charMap, string filePath)
        {
            try
            {
                JsonSerializerOptions options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                    DefaultIgnoreCondition = JsonIgnoreCondition.Never
                };
                
                string jsonString = JsonSerializer.Serialize(charMap, options);
                
                string directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                PlainFiles.WriteText(filePath, jsonString, new System.Text.UTF8Encoding(false));
                
                AppLogger.Debug($"Successfully serialized charmap to {filePath} with {charMap.CharacterMap.Count} character entries and {charMap.CommandMap.Count} command entries");
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Couldn't serialize charmap to {filePath}: {ex.Message}");
                throw;
            }
        }
        
        public static CharMap LoadCharMap(bool forceReload = false)
        {
            if (_cachedCharMap != null && !forceReload)
            {
                return _cachedCharMap;
            }
            
            string charmapPath = GetCharMapPath();
            _cachedCharMap = DeserializeCharMap(charmapPath);
            
            return _cachedCharMap;
        }
        
        /// <summary>Saves the charmap to the custom path, or to the path it was loaded from.</summary>
        public static void SaveCharMap(CharMap charMap, bool saveToCustomPath = true)
        {
            string savePath = saveToCustomPath ? CustomCharmapFilePath : GetCharMapPath();
            SerializeCharMap(charMap, savePath);
            
            _cachedCharMap = charMap;
            
            if (saveToCustomPath)
            {
                _loadedCharmapPath = CustomCharmapFilePath;
            }
        }
        
        public static CharMap GetCurrentCharMap()
        {
            return LoadCharMap();
        }
        
        public static void ClearCache()
        {
            _cachedCharMap = null;
            _loadedCharmapPath = null;
            AppLogger.Debug("Cleared charmap cache");
        }

        #endregion

        #region Merge Operations

        /// <summary>Merges a custom charmap onto a newer built-in one, resolving clashes by <paramref name="strategy"/>.</summary>
        public static MergeResult MergeCharMaps(CharMap baseMap, CharMap customMap, MergeStrategy strategy = MergeStrategy.PreferCustom)
        {
            MergeResult result = new MergeResult();

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
            
            foreach (KeyValuePair<string, CharMapEntry> customEntry in customMap.CharacterMap)
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
            
            foreach (KeyValuePair<string, string> customCommand in customMap.CommandMap)
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
        
        /// <summary>Both alias lists, without duplicates.</summary>
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
        
        /// <summary>Merges the custom charmap onto the built-in one.</summary>
        public static MergeResult MergeCustomWithDefault(MergeStrategy strategy = MergeStrategy.PreferCustom)
        {
            if (!File.Exists(CustomCharmapFilePath))
            {
                throw new FileNotFoundException("Custom charmap doesn't exist. Can't merge.");
            }
            
            CharMap defaultMap = DeserializeCharMap(CharmapFilePath);
            CharMap customMap = DeserializeCharMap(CustomCharmapFilePath);
            
            return MergeCharMaps(defaultMap, customMap, strategy);
        }
        
        #endregion

        /// <summary>The custom charmap if there is one, else the built-in one; throws when neither exists.</summary>
        public static string GetCharMapPath()
        {
            if (_loadedCharmapPath != null)
            {
                return _loadedCharmapPath;
            }

            if (File.Exists(CustomCharmapFilePath))
            {
                AppLogger.Info("Loading custom charmap from user data directory.");
                _loadedCharmapPath = CustomCharmapFilePath;
                return CustomCharmapFilePath;
            }
            else if (File.Exists(CharmapFilePath))
            {
                AppLogger.Info("Loading default charmap from application directory.");
                _loadedCharmapPath = CharmapFilePath;
                return CharmapFilePath;
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

        /// <summary>Whether the custom charmap is older than the built-in one.</summary>
        public static bool IsCustomMapOutdated()
        {
            if (File.Exists(CustomCharmapFilePath))
            {
                try
                {
                    Version defaultVersion = GetCharMapVersion(CharmapFilePath);
                    Version customVersion = GetCharMapVersion(CustomCharmapFilePath);

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

        /// <summary>Copies the built-in charmap to the custom path, replacing any file there.</summary>
        public static bool CreateCustomCharMapFile()
        {
            try
            {
                if (!Directory.Exists(Path.GetDirectoryName(CustomCharmapFilePath)))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(CustomCharmapFilePath));
                }
                PlainFiles.Copy(CharmapFilePath, CustomCharmapFilePath, overwrite: true);
                ClearCache(); // Clear cache to force reload with new custom file
                AppLogger.Info("Custom charmap file created successfully.");
                return true;
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Couldn't create custom charmap file: {ex.Message}");
                return false;
            }
        }

        /// <summary>Deletes the custom charmap if there is one; false only on an error.</summary>
        public static bool DeleteCustomCharMapFile()
        {
            try
            {
                if (File.Exists(CustomCharmapFilePath))
                {
                    File.Delete(CustomCharmapFilePath);
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
                AppLogger.Error($"Couldn't delete custom charmap file: {ex.Message}");
                return false;
            }
        }

    }
}
