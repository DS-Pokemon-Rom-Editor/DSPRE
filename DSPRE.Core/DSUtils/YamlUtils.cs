using System;
using System.Collections.Generic;
using System.IO;
using YamlDotNet.Serialization;

namespace DSPRE {
    public static class YamlUtils {
        private class HeaderYaml {
            public string title { get; set; }
            public string gamecode { get; set; }
            public string makercode { get; set; }
            public int rom_version { get; set; }
        }

        /// <summary>
        /// Reads gamecode and rom_version from a ds-rom header.yaml file.
        /// </summary>
        /// <param name="yamlPath">Path to header.yaml file</param>
        /// <returns>Tuple of (gamecode, revision) or null if parsing fails</returns>
        public static (string gamecode, byte revision)? ReadGameCodeFromHeaderYaml(string yamlPath) {
            try {
                if (!File.Exists(yamlPath)) {
                    AppLogger.Warn($"Header YAML file does not exist at path: {yamlPath}");
                    return null;
                }

                AppLogger.Debug($"Reading header.yaml from: {yamlPath}");
                string yamlContent = File.ReadAllText(yamlPath);
                AppLogger.Debug($"YAML content length: {yamlContent.Length} bytes");
                
                var deserializer = new DeserializerBuilder()
                    .IgnoreUnmatchedProperties()
                    .Build();
                var header = deserializer.Deserialize<HeaderYaml>(yamlContent);

                if (header == null) {
                    AppLogger.Warn("Deserialized header object is null");
                    return null;
                }

                AppLogger.Debug($"Parsed YAML - gamecode: '{header.gamecode}', rom_version: {header.rom_version}");

                if (string.IsNullOrEmpty(header.gamecode)) {
                    AppLogger.Warn("gamecode field is null or empty in header.yaml");
                    return null;
                }

                byte revision = (byte)header.rom_version;
                AppLogger.Info($"Successfully read header.yaml: gamecode={header.gamecode}, revision={revision}");
                return (header.gamecode, revision);
            } catch (Exception ex) {
                AppLogger.Error($"Exception while parsing header.yaml: {ex.Message}");
                AppLogger.Error($"Stack trace: {ex.StackTrace}");
                return null;
            }
        }
    }
}
