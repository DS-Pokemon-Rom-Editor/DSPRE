using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using YamlDotNet.Serialization;
using static DSPRE.DSUtils;
using static DSPRE.RomInfo;

namespace DSPRE
{
    public static class OverlayUtils
    {
        private class OverlayYaml
        {
            public bool table_signed { get; set; }
            public List<OverlayEntry> overlays { get; set; }
        }

        private class OverlayEntry
        {
            public int id { get; set; }
            public uint base_address { get; set; }
            public uint code_size { get; set; }
            public uint bss_size { get; set; }
            public uint ctor_start { get; set; }
            public uint ctor_end { get; set; }
            public int file_id { get; set; }
            public bool compressed { get; set; }
            public bool signed { get; set; }
            public string file_name { get; set; }
        }

        private static OverlayYaml _cachedOverlayYaml;

        private static OverlayYaml LoadOverlayYaml()
        {
            if (_cachedOverlayYaml != null)
                return _cachedOverlayYaml;

            try
            {
                string yamlContent = File.ReadAllText(RomInfo.overlayTablePath);
                var deserializer = new DeserializerBuilder()
                    .IgnoreUnmatchedProperties()
                    .Build();
                _cachedOverlayYaml = deserializer.Deserialize<OverlayYaml>(yamlContent);
                return _cachedOverlayYaml;
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Failed to load overlays.yaml: {ex.Message}");
                return null;
            }
        }

        public static class OverlayTable
        {
            private const int ENTRY_LEN = 32;

            /**
            * Only checks if the overlay is CONFIGURED as compressed
            **/
            public static bool IsDefaultCompressed(int ovNumber)
            {
                if (RomInfo.IsDsRomProject)
                {
                    var yaml = LoadOverlayYaml();
                    if (yaml?.overlays == null || ovNumber >= yaml.overlays.Count)
                        return false;
                    return yaml.overlays[ovNumber].compressed;
                }

                using (DSUtils.EasyReader f = new EasyReader(RomInfo.overlayTablePath, ovNumber * ENTRY_LEN + 31))
                {
                    return (f.ReadByte() & 1) == 1;
                }
            }

            public static void SetDefaultCompressed(int ovNumber, bool compressStatus)
            {
                if (RomInfo.IsDsRomProject)
                {
                    AppLogger.Warn("Cannot modify overlay compression flag in ds-rom format (compression is automatic)");
                    return;
                }

                DSUtils.WriteToFile(RomInfo.overlayTablePath, new byte[] { compressStatus ? (byte)1 : (byte)0 }, (uint)(ovNumber * ENTRY_LEN + 31));
            }

            public static uint GetRAMAddress(int ovNumber)
            {
                if (RomInfo.IsDsRomProject)
                {
                    var yaml = LoadOverlayYaml();
                    if (yaml?.overlays == null || ovNumber >= yaml.overlays.Count)
                        return 0;
                    return yaml.overlays[ovNumber].base_address;
                }

                using (DSUtils.EasyReader f = new EasyReader(RomInfo.overlayTablePath, ovNumber * ENTRY_LEN + 4))
                {
                    return f.ReadUInt32();
                }
            }

            public static uint GetUncompressedSize(int ovNumber)
            {
                if (RomInfo.IsDsRomProject)
                {
                    var yaml = LoadOverlayYaml();
                    if (yaml?.overlays == null || ovNumber >= yaml.overlays.Count)
                        return 0;
                    return yaml.overlays[ovNumber].code_size + yaml.overlays[ovNumber].bss_size;
                }

                using (DSUtils.EasyReader f = new EasyReader(RomInfo.overlayTablePath, ovNumber * ENTRY_LEN + 8))
                {
                    return f.ReadUInt32();
                }
            }

            public static int GetNumberOfOverlays()
            {
                if (RomInfo.IsDsRomProject)
                {
                    var yaml = LoadOverlayYaml();
                    return yaml?.overlays?.Count ?? 0;
                }

                using (FileStream fileStream = File.OpenRead(RomInfo.overlayTablePath))
                {
                    // Get the length of the file in bytes
                    return (int)(fileStream.Length / ENTRY_LEN);
                }
            }
        }


        public static string GetPath(int overlayNumber)
        {
            if (RomInfo.IsDsRomProject)
            {
                return $"{workDir}arm9_overlays\\ov{overlayNumber:D3}.bin";
            }
            return $"{workDir}overlay\\overlay_{overlayNumber:D4}.bin";
        }

        /**
         * Checks the actual size of the overlay file
         **/
        public static bool IsCompressed(int ovNumber)
        {
            if (RomInfo.IsDsRomProject) { return false; }

            string overlayPath = GetPath(ovNumber);

            if (!File.Exists(overlayPath))
            {
                AppLogger.Warn($"Overlay file not found: {overlayPath}");
                return false;
            }

            try
            {
                long fileSize = new FileInfo(overlayPath).Length;
                uint uncompressedSize = OverlayTable.GetUncompressedSize(ovNumber);
                return fileSize < uncompressedSize;
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Error checking compression status for overlay {ovNumber}: {ex.Message}");
                return false;
            }
        }

        public static int Decompress(string overlayFilePath, bool makeBackup = true)
        {
            // ds-rom overlays are always decompressed on disk
            if (RomInfo.IsDsRomProject)
            {
                AppLogger.Info("ds-rom overlays are always stored decompressed on disk.");
                return 0; // Success - already decompressed
            }

            if (!File.Exists(overlayFilePath))
            {
                MessageBox.Show($"File to decompress \"{overlayFilePath}\" doesn't exist",
                    "Overlay not found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return ERR_OVERLAY_NOTFOUND;
            }

            if (makeBackup)
            {
                if (File.Exists(overlayFilePath + backupSuffix))
                {
                    File.Delete(overlayFilePath + backupSuffix);
                }
                File.Copy(overlayFilePath, overlayFilePath + backupSuffix);
            }

            Process decompress = DSUtils.CreateDecompressProcess(overlayFilePath);
            decompress.Start();
            decompress.WaitForExit();
            return decompress.ExitCode;
        }
        public static int Decompress(int overlayNumber, bool makeBackup = true)
        {
            return Decompress(GetPath(overlayNumber), makeBackup);
        }

        public static byte[] ReadBytes(int overlayNumber, uint offset, uint length)
        {
            string path = GetPath(overlayNumber);
            if (!File.Exists(path))
            {
                AppLogger.Error($"Overlay file not found: {path}");
                return new byte[length]; // Return empty data to avoid crashes
            }

            if (IsCompressed(overlayNumber)) {
                Decompress(overlayNumber, makeBackup: false);
            }

            try
            {
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                {
                    if (offset + length > fs.Length)
                    {
                        AppLogger.Warn($"Attempt to read beyond end of overlay file: {path} (offset={offset}, length={length})");
                        return new byte[length]; // Return empty data to avoid crashes
                    }
                    byte[] buffer = new byte[length];
                    fs.Seek(offset, SeekOrigin.Begin);
                    fs.Read(buffer, 0, (int)length);
                    return buffer;
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Error reading bytes from overlay {overlayNumber}: {ex.Message}");
                return new byte[length]; // Return empty data to avoid crashes
            }

        }

        public static void WriteBytes(int overlayNumber, uint offset, byte[] data)
        {
            string path = GetPath(overlayNumber);
            if (!File.Exists(path))
            {
                AppLogger.Error($"Overlay file not found: {path}");
                return;
            }
            try
            {
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Write))
                {
                    if (offset + data.Length > fs.Length)
                    {
                        AppLogger.Warn($"Attempt to write beyond end of overlay file: {path} (offset={offset}, data length={data.Length})");
                        return;
                    }
                    fs.Seek(offset, SeekOrigin.Begin);
                    fs.Write(data, 0, data.Length);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Error writing bytes to overlay {overlayNumber}: {ex.Message}");
            }

        }
}
}
