using System;
using System.Collections.Generic;
using System.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Core;
using static DSPRE.Anchor;
using static DSPRE.RomInfo;
using System.Windows;

namespace DSPRE
{
    /// <summary>
    /// Manages offset database loading from YAML files with support for project-level overrides.
    /// Follows the same pattern as ScriptDatabase for consistency.
    /// </summary>
    public static class OffsetDatabase
    {
        private static Dictionary<string, Anchor> defaultAnchors;
        private static Dictionary<string, Anchor> projectAnchors;

        public static GameFamilies CurrentGameFamily { get; private set; }
        public static GameLanguages CurrentLanguage { get; private set; }
        public static GameVersions CurrentVersion { get; private set; }

        /// <summary>
        /// Initialize the offset database from default and project-specific YAML files.
        /// Should be called once at startup.
        /// </summary>
        public static void Initialize(string defaultPath, string projectPath, 
            GameFamilies gameFamily, GameLanguages language, GameVersions version)
        {
            try
            {
                // Load default anchors
                if (!File.Exists(defaultPath))
                    throw new FileNotFoundException($"Default offset database not found: {defaultPath}");

                AppLogger.Debug($"Loading default offset database from: {Path.GetFileName(defaultPath)}");
                defaultAnchors = LoadYaml(defaultPath);

                // Load project-specific overrides if they exist
                if (File.Exists(projectPath))
                {
                    AppLogger.Debug($"Loading project-specific offset overrides from: {Path.GetFileName(projectPath)}");
                    projectAnchors = LoadYaml(projectPath);
                }
                else
                {
                    projectAnchors = new Dictionary<string, Anchor>();
                    AppLogger.Debug("No project-specific offset overrides found.");
                }

                CurrentGameFamily = gameFamily;
                CurrentLanguage = language;
                CurrentVersion = version;

                AppLogger.Debug($"OffsetDatabase initialized: {defaultAnchors.Count} default anchors, {projectAnchors.Count} project overrides");
            }
            catch (InvalidOperationException ex)
            {
                // Re-throw InvalidOperationException (these have our custom error messages)
                AppLogger.Error($"Failed to initialize OffsetDatabase:\n{ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                string errorMsg = $"Failed to initialize OffsetDatabase: {ex.Message}";
                AppLogger.Error(errorMsg);
                throw new InvalidOperationException(errorMsg, ex);
            }
        }

        /// <summary>
        /// Get an offset by anchor name, automatically resolving language and version.
        /// </summary>
        public static uint GetOffset(string anchorName)
        {
            var anchor = GetAnchor(anchorName);
            return anchor.GetOffset(CurrentLanguage, CurrentVersion);
        }

        /// <summary>
        /// Get the length (in bytes) of data to read for an anchor.
        /// </summary>
        public static uint GetLength(string anchorName)
        {
            var anchor = GetAnchor(anchorName);
            return anchor.Length;
        }

        /// <summary>
        /// Get the source information for an anchor, which includes the type (ARM9, Overlay, SyntheticOverlay) and overlay number if applicable.
        /// </summary>
        /// <param name="anchorName"></param>
        /// <returns></returns>
        public static AnchorSource GetSource(string anchorName)
        {
            var anchor = GetAnchor(anchorName);
            return anchor.Source;
        }

        public static uint GetOverlayID(string anchorName)
        {
            var anchor = GetAnchor(anchorName);
            if (anchor.Source.Type != SourceType.Overlay)
                throw new InvalidOperationException($"Anchor '{anchorName}' does not have an overlay source");
            if (!anchor.Source.OverlayNumber.HasValue)
                throw new InvalidOperationException($"Anchor '{anchorName}' overlay source does not specify an overlay number");
            return (uint)anchor.Source.OverlayNumber.Value;
        }

        /// <summary>
        /// Get the full anchor object. Tries project overrides first, then defaults.
        /// </summary>
        public static Anchor GetAnchor(string anchorName)
        {
            // Try project override first
            if (projectAnchors != null && projectAnchors.TryGetValue(anchorName, out var projectAnchor))
                return projectAnchor;

            // Fall back to default
            if (defaultAnchors.TryGetValue(anchorName, out var defaultAnchor))
                return defaultAnchor;

            throw new KeyNotFoundException($"Anchor '{anchorName}' not found in offset database");
        }

        /// <summary>
        /// Read data directly using an anchor, handling all source type conversions.
        /// </summary>
        public static byte[] ReadAnchorData(string anchorName)
        {
            var anchor = GetAnchor(anchorName);
            return anchor.ReadData(anchor.Length);
        }

        /// <summary>
        /// Write data directly using an anchor, handling all source type conversions.
        /// </summary>
        /// <param name="anchorName"></param>
        /// <param name="data"></param>
        public static void WriteAnchorData(string anchorName, byte[] data)
        {
            var anchor = GetAnchor(anchorName);
            anchor.WriteData(data, anchor.Length);
        }

        /// <summary>
        /// Validate a YAML file without loading it into the active database.
        /// Useful for testing/previewing offset files before use.
        /// Returns validation result with detailed error information.
        /// </summary>
        public static (bool isValid, string message) ValidateYamlFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return (false, $"File not found: {filePath}");

                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .Build();

                string content = File.ReadAllText(filePath);
                string filename = Path.GetFileName(filePath);

                try
                {
                    var root = deserializer.Deserialize<OffsetDatabaseRoot>(content);

                    if (root?.Anchors == null || root.Anchors.Count == 0)
                        return (false, $"No anchors found in {filename}");

                    var errors = new List<string>();
                    int anchorCount = 0;

                    foreach (var kvp in root.Anchors)
                    {
                        try
                        {
                            ValidateAnchorStructure(kvp.Key, kvp.Value);
                            anchorCount++;
                        }
                        catch (InvalidOperationException valEx)
                        {
                            errors.Add($"Anchor '{kvp.Key}': {valEx.Message}");
                        }
                    }

                    if (errors.Count > 0)
                    {
                        string errorSummary = string.Join("\n\n", errors);
                        return (false, $"Validation failed for {filename}:\n\n{errorSummary}");
                    }

                    string successMsg = $"✓ Validation successful for {filename}\n" +
                                       $"  Found {anchorCount} valid anchor(s)";
                    return (true, successMsg);
                }
                catch (YamlException yamlEx)
                {
                    string errorMsg = BuildYamlErrorMessage(yamlEx, filePath, content);
                    return (false, errorMsg);
                }
            }
            catch (Exception ex)
            {
                return (false, $"Validation error: {ex.Message}");
            }
        }

        /// <summary>
        /// Load YAML file and deserialize into anchors dictionary with comprehensive error handling.
        /// </summary>
        private static Dictionary<string, Anchor> LoadYaml(string filePath)
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            try
            {
                string content = File.ReadAllText(filePath);

                OffsetDatabaseRoot root;
                try
                {
                    root = deserializer.Deserialize<OffsetDatabaseRoot>(content);
                }
                catch (YamlException yamlEx)
                {
                    // Provide user-friendly error message for common YAML mistakes
                    string errorMessage = BuildYamlErrorMessage(yamlEx, filePath, content);
                    throw new InvalidOperationException(errorMessage, yamlEx);
                }

                var anchors = new Dictionary<string, Anchor>();
                if (root?.Anchors != null)
                {
                    foreach (var kvp in root.Anchors)
                    {
                        try
                        {
                            // Validate anchor structure before adding
                            ValidateAnchorStructure(kvp.Key, kvp.Value);

                            anchors[kvp.Key] = kvp.Value;
                            anchors[kvp.Key].Name = kvp.Key; // Set anchor name from key
                        }
                        catch (InvalidOperationException valEx)
                        {
                            throw new InvalidOperationException(
                                $"Error in anchor '{kvp.Key}' in file '{Path.GetFileName(filePath)}': {valEx.Message}", 
                                valEx);
                        }
                    }
                }

                if (anchors.Count == 0)
                {
                    AppLogger.Warn($"No anchors found in {Path.GetFileName(filePath)}. File may be empty or malformed.");
                }

                return anchors;
            }
            catch (FileNotFoundException)
            {
                throw;
            }
            catch (Exception ex)
            {
                string filename = Path.GetFileName(filePath);
                string errorMsg = $"Failed to load offset database from '{filename}': {ex.Message}";
                AppLogger.Error(errorMsg);
                MessageBox.Show(errorMsg, "Offset Database Load Error", MessageBoxButton.OK, MessageBoxImage.Error);
                throw new InvalidOperationException(errorMsg, ex);
            }
        }

        /// <summary>
        /// Validates the structure of an anchor and provides helpful error messages for common mistakes.
        /// </summary>
        private static void ValidateAnchorStructure(string anchorName, Anchor anchor)
        {
            if (anchor == null)
                throw new InvalidOperationException("Anchor is null");

            if (anchor.Source == null)
                throw new InvalidOperationException(
                    "Missing 'source' property. Required format:\n" +
                    "  " + anchorName + ":\n" +
                    "    source:\n" +
                    "      type: arm9  # or 'overlay' or 'syntheticOverlay'\n" +
                    "    length: 4\n" +
                    "    offsets:\n" +
                    "      English: 0xF85B4");

            // For overlay type, overlayNumber is required
            if (anchor.Source.Type == SourceType.Overlay && !anchor.Source.OverlayNumber.HasValue)
                throw new InvalidOperationException(
                    "Source type is 'overlay' but 'overlayNumber' is not specified. Example:\n" +
                    "  " + anchorName + ":\n" +
                    "    source:\n" +
                    "      type: overlay\n" +
                    "      overlayNumber: 5");

            if (anchor.Offsets == null || anchor.Offsets.Count == 0)
                throw new InvalidOperationException(
                    "Missing 'offsets' dictionary. Must have at least one language entry (e.g., English, Japanese)");
        }

        /// <summary>
        /// Builds a helpful error message for common YAML deserialization errors.
        /// </summary>
        private static string BuildYamlErrorMessage(YamlException yamlEx, string filePath, string content)
        {
            string filename = Path.GetFileName(filePath);
            string baseMessage = $"YAML format error in '{filename}':\n{yamlEx.Message}\n\n";

            // Detect common mistakes
            if (yamlEx.Message.Contains("Property 'type' not found"))
            {
                return baseMessage + 
                    "ERROR: 'type' property found at the wrong level.\n\n" +
                    "INCORRECT:\n" +
                    "  ItemTableOffset:\n" +
                    "    type: arm9  ❌ (type should be under 'source')\n" +
                    "    offsets:\n" +
                    "      English: 0xF85B4\n\n" +
                    "CORRECT:\n" +
                    "  ItemTableOffset:\n" +
                    "    source:\n" +
                    "      type: arm9  ✓\n" +
                    "    length: 4\n" +
                    "    offsets:\n" +
                    "      English: 0xF85B4";
            }

            if (yamlEx.Message.Contains("Property 'offsets' not found"))
            {
                return baseMessage +
                    "ERROR: Missing required 'offsets' property.\n\n" +
                    "Every anchor must have an 'offsets' section with language-specific values:\n" +
                    "  ItemTableOffset:\n" +
                    "    source:\n" +
                    "      type: arm9\n" +
                    "    length: 4\n" +
                    "    offsets:\n" +
                    "      English: 0xF85B4\n" +
                    "      French: 0xF85B8\n" +
                    "      German: 0xF85BC";
            }

            if (yamlEx.Message.Contains("Property 'source' not found"))
            {
                return baseMessage +
                    "ERROR: Missing required 'source' property.\n\n" +
                    "Correct structure:\n" +
                    "  ItemTableOffset:\n" +
                    "    source:\n" +
                    "      type: arm9  # Required: arm9, overlay, or syntheticOverlay\n" +
                    "      overlayNumber: 5  # Only for overlay type\n" +
                    "    length: 4\n" +
                    "    offsets:\n" +
                    "      English: 0xF85B4";
            }

            if (yamlEx.Message.Contains("mapping values are not allowed"))
            {
                return baseMessage +
                    "ERROR: Indentation or syntax error detected.\n\n" +
                    "Common issues:\n" +
                    "  • Inconsistent indentation (use 2 or 4 spaces, not tabs)\n" +
                    "  • Missing colon (:) after property name\n" +
                    "  • Invalid characters or quotes\n\n" +
                    "Example of correct indentation:\n" +
                    "anchors:\n" +
                    "  ItemTableOffset:\n" +
                    "    source:\n" +
                    "      type: arm9\n" +
                    "    length: 4";
            }

            // Generic fallback with format template
            return baseMessage +
                "Please check your YAML syntax. Expected structure:\n\n" +
                "anchors:\n" +
                "  AnchorName:\n" +
                "    source:\n" +
                "      type: arm9           # arm9, overlay, or syntheticOverlay\n" +
                "      overlayNumber: 5     # Only required for overlay type\n" +
                "    length: 4              # Bytes to read\n" +
                "    offsets:\n" +
                "      English: 0xF85B4     # Hex offset for English version\n" +
                "      Japanese: 0xF85C0";
        }
    }

    /// <summary>
    /// Root structure for YAML deserialization.
    /// </summary>
    public class OffsetDatabaseRoot
    {
        public Dictionary<string, Anchor> Anchors { get; set; }
    }

    public class Anchor
    {
        public string Name { get; set; }
        public AnchorSource Source { get; set; }
        public uint Length { get; set; }
        public DecompReference DecompRef { get; set; }

        // The nested structure: Language -> (Offset OR Version -> Offset)
        public Dictionary<GameLanguages, object> Offsets { get; set; }

        /// <summary>
        /// Smart resolution: language with version fallback, then English fallback
        /// </summary>
        public uint GetOffset(GameLanguages language, GameVersions version)
        {
            if (Offsets == null || Offsets.Count == 0)
                throw new InvalidOperationException(
                    $"Anchor '{Name}' has no offsets defined. Check your YAML file.");

            // Try exact language match
            if (Offsets.TryGetValue(language, out var value))
            {
                var offset = ResolveOffsetValue(value, version);
                if (offset.HasValue)
                    return offset.Value;
            }

            // Fall back to English
            if (language != GameLanguages.English && 
                Offsets.TryGetValue(GameLanguages.English, out var englishValue))
            {
                var offset = ResolveOffsetValue(englishValue, version);
                if (offset.HasValue)
                    return offset.Value;
            }

            throw new InvalidOperationException(
                $"No offset found for anchor '{Name}' (language={language}, version={version}). " +
                $"Available languages: {string.Join(", ", Offsets.Keys)}");
        }

        /// <summary>
        /// Helper to resolve offset value which can be either a direct uint or a version-split dictionary.
        /// </summary>
        private uint? ResolveOffsetValue(object value, GameVersions version)
        {
            // Check if it's a version-split structure (dictionary)
            if (value is Dictionary<object, object> versionMapObj)
            {
                var versionMap = ConvertToVersionMap(versionMapObj);
                if (versionMap.TryGetValue(version, out var versionOffset))
                    return versionOffset;
            }
            // Direct offset
            else if (value is uint directOffset)
            {
                return directOffset;
            }
            // Handle case where YAML deserializes as long
            else if (value is long longOffset)
            {
                return (uint)longOffset;
            }
            // Handle case where YAML desrializes as string (most common case)
            else if (value is string stringOffset)
            {
                // Parse as hex or decimal
                if (stringOffset.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                {
                    if (uint.TryParse(stringOffset.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out uint parsedOffset))
                        return parsedOffset;
                }
                else
                {
                    if (uint.TryParse(stringOffset, out uint parsedOffset))
                        return parsedOffset;
                }
            }

            return null;
        }

        /// <summary>
        /// Convert a deserialized dictionary with GameVersions keys to proper types.
        /// </summary>
        private Dictionary<GameVersions, uint> ConvertToVersionMap(Dictionary<object, object> rawMap)
        {
            var versionMap = new Dictionary<GameVersions, uint>();

            foreach (var kvp in rawMap)
            {
                if (Enum.TryParse<GameVersions>(kvp.Key.ToString(), out var version))
                {
                    uint offset = 0;
                    if (kvp.Value is uint uintVal)
                        offset = uintVal;
                    else if (kvp.Value is long longVal)
                        offset = (uint)longVal;
                    else if (kvp.Value is string strVal && uint.TryParse(strVal, System.Globalization.NumberStyles.HexNumber, null, out var parsed))
                        offset = parsed;

                    versionMap[version] = offset;
                }
            }

            return versionMap;
        }

        /// <summary>
        /// Read data from ROM using this anchor's configuration.
        /// Automatically handles source type and offset conversions.
        /// </summary>
        public byte[] ReadData(uint length, uint displacement=0)
        {
            try
            {
                uint offset = GetOffset(RomInfo.gameLanguage, RomInfo.gameVersion);

                // Auto-detect synthetic overlay if offset is in that range
                if (offset >= RomInfo.synthOverlayLoadAddress && Source.Type != SourceType.SyntheticOverlay)
                {
                    Source.Type = SourceType.SyntheticOverlay;
                }

                switch (Source.Type)
                {
                    case SourceType.ARM9:
                        if (offset >= ARM9.address)
                        {
                            offset -= ARM9.address; // Convert to file offset
                        }
                        return ARM9.ReadBytes(offset + displacement, length);

                    case SourceType.Overlay:
                        if (!Source.OverlayNumber.HasValue)
                            throw new InvalidOperationException(
                                $"Anchor '{Name}' is configured as overlay type but has no overlayNumber. " +
                                $"Check YAML: source.overlayNumber is required for overlay type.");

                        uint ramAddress = OverlayUtils.OverlayTable.GetRAMAddress(Source.OverlayNumber.Value);
                        if (offset >= ramAddress)
                        {
                            offset -= ramAddress; // Convert to overlay file offset
                        }
                        return OverlayUtils.ReadBytes(Source.OverlayNumber.Value, offset + displacement, length);

                    case SourceType.SyntheticOverlay:
                        if (offset >= RomInfo.synthOverlayLoadAddress)
                        {
                            offset -= RomInfo.synthOverlayLoadAddress; // Convert to file offset
                        }
                        using (DSUtils.EasyReader ovReader = new DSUtils.EasyReader(Filesystem.expArmPath, offset + displacement))
                        {
                            return ovReader.ReadBytes((int) length);
                        }

                    default:
                        throw new InvalidOperationException(
                            $"Anchor '{Name}' has unknown source type: {Source.Type}. " +
                            $"Allowed values: arm9, overlay, syntheticOverlay");
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to read data from anchor '{Name}': {ex.Message}", ex);
            }
        }

        public void WriteData(byte[] data, uint length, uint displacement=0)
        {
            try
            {
                uint offset = GetOffset(RomInfo.gameLanguage, RomInfo.gameVersion);

                // Auto-detect synthetic overlay if offset is in that range
                if (offset >= RomInfo.synthOverlayLoadAddress && Source.Type != SourceType.SyntheticOverlay)
                {
                    Source.Type = SourceType.SyntheticOverlay;
                }

                switch (Source.Type)
                {
                    case SourceType.ARM9:
                        if (offset >= ARM9.address)
                        {
                            offset -= ARM9.address; // Convert to file offset
                        }
                        ARM9.WriteBytes(data, offset + displacement);
                        break;

                    case SourceType.Overlay:
                        if (!Source.OverlayNumber.HasValue)
                            throw new InvalidOperationException(
                                $"Anchor '{Name}' is configured as overlay type but has no overlayNumber. " +
                                $"Check YAML: source.overlayNumber is required for overlay type.");

                        uint ramAddress = OverlayUtils.OverlayTable.GetRAMAddress(Source.OverlayNumber.Value);
                        if (offset >= ramAddress)
                        {
                            offset -= ramAddress; // Convert to overlay file offset
                        }
                        OverlayUtils.WriteBytes(Source.OverlayNumber.Value, offset + displacement, data);
                        break;

                    case SourceType.SyntheticOverlay:
                        if (offset >= RomInfo.synthOverlayLoadAddress)
                        {
                            offset -= RomInfo.synthOverlayLoadAddress; // Convert to file offset
                        }
                        DSUtils.WriteToFile(Filesystem.expArmPath, data, offset + displacement);
                        break;

                    default:
                        throw new InvalidOperationException(
                            $"Anchor '{Name}' has unknown source type: {Source.Type}. " +
                            $"Allowed values: arm9, overlay, syntheticOverlay");
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to write data to anchor '{Name}': {ex.Message}", ex);
            }
        }

    public class AnchorSource
    {
        public SourceType Type { get; set; }
        public int? OverlayNumber { get; set; }  // Only for overlay type
    }

    public enum SourceType
    {
        ARM9,
        Overlay,
        SyntheticOverlay
    }

    public class DecompReference
    {
        public string Symbol { get; set; }
        public int Offset { get; set; }
    }
}
}
