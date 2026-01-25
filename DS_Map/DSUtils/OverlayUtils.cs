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

        public static void RestoreFromCompressedBackup(int overlayNumber, bool eventEditorIsReady)
        {
            String overlayFilePath = GetPath(overlayNumber);

            if (File.Exists(overlayFilePath + DSUtils.backupSuffix))
            {
                if (new FileInfo(overlayFilePath).Length <= new FileInfo(overlayFilePath + DSUtils.backupSuffix).Length)
                { //if overlay is bigger than its backup
                    AppLogger.Info($"Overlay {overlayNumber} is already compressed.");
                    return;
                }
                else
                {
                    File.Delete(overlayFilePath);
                    File.Move(overlayFilePath + DSUtils.backupSuffix, overlayFilePath);
                }
            }
            else
            {
                string msg = $"Overlay File {overlayFilePath}{DSUtils.backupSuffix} couldn't be found and restored.";
                AppLogger.Debug(msg);

                if (eventEditorIsReady)
                {
                    MessageBox.Show(msg, "Can't restore overlay from backup", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
        public static int Compress(int overlayNumber)
        {
            // ds-rom handles compression automatically during build
            if (RomInfo.IsDsRomProject)
            {
                AppLogger.Info("ds-rom handles overlay compression automatically during ROM build.");
                return 0; // Success - no action needed
            }

            string overlayFilePath = GetPath(overlayNumber);

            if (!File.Exists(overlayFilePath))
            {
                MessageBox.Show("Overlay to decompress #" + overlayNumber + " doesn't exist",
                    "Overlay not found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return ERR_OVERLAY_NOTFOUND;
            }

            Process compress = new Process();
            compress.StartInfo.FileName = @"Tools\blz.exe";
            compress.StartInfo.Arguments = "-en " + '"' + overlayFilePath + '"';
            Application.DoEvents();
            compress.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            compress.StartInfo.CreateNoWindow = true;
            compress.Start();
            compress.WaitForExit();
            return compress.ExitCode;
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

    }
}
