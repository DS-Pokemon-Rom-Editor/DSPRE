using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using YamlDotNet.Serialization;
using static DSPRE.DSUtils;
using static DSPRE.RomInfo;

namespace DSPRE
{
    public static class OverlayUtils
    {
        private class OverlayYaml
        {
            [YamlMember(Alias = "table_signed")]
            public bool TableSigned { get; set; }
            [YamlMember(Alias = "overlays")]
            public List<OverlayEntry> Overlays { get; set; }
        }

        private class OverlayEntry
        {
            [YamlMember(Alias = "id")]
            public int Id { get; set; }
            [YamlMember(Alias = "base_address")]
            public uint BaseAddress { get; set; }
            [YamlMember(Alias = "code_size")]
            public uint CodeSize { get; set; }
            [YamlMember(Alias = "bss_size")]
            public uint BssSize { get; set; }
            [YamlMember(Alias = "ctor_start")]
            public uint CtorStart { get; set; }
            [YamlMember(Alias = "ctor_end")]
            public uint CtorEnd { get; set; }
            [YamlMember(Alias = "file_id")]
            public int FileId { get; set; }
            [YamlMember(Alias = "compressed")]
            public bool Compressed { get; set; }
            [YamlMember(Alias = "signed")]
            public bool Signed { get; set; }
            [YamlMember(Alias = "file_name")]
            public string FileName { get; set; }
        }

        private static OverlayYaml _cachedOverlayYaml;

        /// <summary>
        /// Forgets the overlay table, which belongs to one ROM. Loading a second ROM without this leaves
        /// every overlay address and size pointing at the first one, and reads land in the wrong place.
        /// </summary>
        public static void ForgetOverlayTable() => _cachedOverlayYaml = null;

        private static OverlayYaml LoadOverlayYaml()
        {
            if (_cachedOverlayYaml != null)
                return _cachedOverlayYaml;

            try
            {
                string yamlContent = File.ReadAllText(RomInfo.OverlayTablePath);
                IDeserializer deserializer = new DeserializerBuilder()
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
            private const int EntryLen = 32;

            /**
            * Only checks if the overlay is CONFIGURED as compressed
            **/
            public static bool IsDefaultCompressed(int ovNumber)
            {
                if (RomInfo.IsDsRomProject)
                {
                    OverlayYaml yaml = LoadOverlayYaml();
                    if (yaml?.Overlays == null || ovNumber >= yaml.Overlays.Count)
                        return false;
                    return yaml.Overlays[ovNumber].Compressed;
                }

                using (DSUtils.EasyReader f = new EasyReader(RomInfo.OverlayTablePath, ovNumber * EntryLen + 31))
                {
                    return (f.ReadByte() & 1) == 1;
                }
            }

            public static void SetDefaultCompressed(int ovNumber, bool compressStatus)
            {
                if (RomInfo.OverlaysStayFlat)
                {
                    AppLogger.Warn("Cannot modify overlay compression flag in ds-rom format (compression is automatic)");
                    return;
                }

                DSUtils.WriteToFile(RomInfo.OverlayTablePath, new byte[] { compressStatus ? (byte)1 : (byte)0 }, (uint)(ovNumber * EntryLen + 31));
            }

            public static uint GetRAMAddress(int ovNumber)
            {
                if (RomInfo.IsDsRomProject)
                {
                    OverlayYaml yaml = LoadOverlayYaml();
                    if (yaml?.Overlays == null || ovNumber >= yaml.Overlays.Count)
                        return 0;
                    return yaml.Overlays[ovNumber].BaseAddress;
                }

                using (DSUtils.EasyReader f = new EasyReader(RomInfo.OverlayTablePath, ovNumber * EntryLen + 4))
                {
                    return f.ReadUInt32();
                }
            }

            public static uint GetUncompressedSize(int ovNumber)
            {
                if (RomInfo.IsDsRomProject)
                {
                    OverlayYaml yaml = LoadOverlayYaml();
                    if (yaml?.Overlays == null || ovNumber >= yaml.Overlays.Count)
                        return 0;
                    return yaml.Overlays[ovNumber].CodeSize + yaml.Overlays[ovNumber].BssSize;
                }

                using (DSUtils.EasyReader f = new EasyReader(RomInfo.OverlayTablePath, ovNumber * EntryLen + 8))
                {
                    return f.ReadUInt32();
                }
            }

            /// <summary>RAM address of the overlay's static initialiser list, which bounds its data.</summary>
            public static uint GetStaticInitStart(int ovNumber)
            {
                if (RomInfo.IsDsRomProject)
                {
                    OverlayYaml yaml = LoadOverlayYaml();
                    if (yaml?.Overlays == null || ovNumber >= yaml.Overlays.Count)
                        return 0;
                    return yaml.Overlays[ovNumber].CtorStart;
                }

                using (DSUtils.EasyReader f = new EasyReader(RomInfo.OverlayTablePath, ovNumber * EntryLen + 16))
                {
                    return f.ReadUInt32();
                }
            }

            public static int GetNumberOfOverlays()
            {
                if (RomInfo.IsDsRomProject)
                {
                    OverlayYaml yaml = LoadOverlayYaml();
                    return yaml?.Overlays?.Count ?? 0;
                }

                using (FileStream fileStream = File.OpenRead(RomInfo.OverlayTablePath))
                {
                    // Get the length of the file in bytes
                    return (int)(fileStream.Length / EntryLen);
                }
            }
        }


        public static string GetPath(int overlayNumber)
        {
            if (RomInfo.IsDsRomProject)
            {
                return Path.Combine(WorkDir, "arm9_overlays", $"ov{overlayNumber:D3}.bin");
            }
            return Path.Combine(WorkDir, "overlay", $"overlay_{overlayNumber:D4}.bin");
        }

        /**
         * Checks the actual size of the overlay file
         **/
        /// <summary>Whether the overlay on disk is still compressed. ds-rom projects keep overlays flat until build.</summary>
        public static bool IsStillCompressed(int ovNumber) =>
            !RomInfo.OverlaysStayFlat && OverlayTable.IsDefaultCompressed(ovNumber) && IsCompressed(ovNumber);

        public static bool IsCompressed(int ovNumber)
        {
            // ds-rom extracts every overlay flat; its size there leaves out the BSS the table's size counts.
            if (RomInfo.OverlaysStayFlat) return false;

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

            if (File.Exists(overlayFilePath + DSUtils.BackupSuffix))
            {
                if (new FileInfo(overlayFilePath).Length <= new FileInfo(overlayFilePath + DSUtils.BackupSuffix).Length)
                { //if overlay is bigger than its backup
                    AppLogger.Info($"Overlay {overlayNumber} is already compressed.");
                    return;
                }
                else
                {
                    try
                    {
                        DSUtils.WriteWholeFile(overlayFilePath, File.ReadAllBytes(overlayFilePath + DSUtils.BackupSuffix));
                        File.Delete(overlayFilePath + DSUtils.BackupSuffix);
                    }
                    catch (InvalidOperationException refused)
                    {
                        AppMessages.Error(refused.Message, "Can't restore overlay from backup");
                    }
                }
            }
            else
            {
                string msg = $"Overlay File {overlayFilePath}{DSUtils.BackupSuffix} couldn't be found and restored.";
                AppLogger.Debug(msg);

                if (eventEditorIsReady)
                {
                    AppMessages.Error(msg, "Can't restore overlay from backup");
                }
            }
        }
        public static int Compress(int overlayNumber)
        {
            // ds-rom handles compression automatically during build
            if (RomInfo.OverlaysStayFlat)
            {
                AppLogger.Info("ds-rom handles overlay compression automatically during ROM build.");
                return 0; // Success - no action needed
            }

            string overlayFilePath = GetPath(overlayNumber);

            if (!File.Exists(overlayFilePath))
            {
                AppMessages.Error("Overlay to decompress #" + overlayNumber + " doesn't exist",
                    "Overlay not found");
                return ErrOverlayNotfound;
            }

            Process compress = new Process();
            compress.StartInfo.Arguments = "-en " + '"' + overlayFilePath + '"';
            if (!DSUtils.ConfigureToolStartInfo(compress.StartInfo, "blz"))
            {
                compress.Dispose();
                DSUtils.ReportToolUnavailable("blz");
                return DSUtils.ErrToolUnavailable;
            }
            AppMessages.PumpEvents();
            compress.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            compress.StartInfo.CreateNoWindow = true;
            compress.Start();
            compress.WaitForExit();
            return compress.ExitCode;
        }

        public static int Decompress(string overlayFilePath, bool makeBackup = true)
        {
            // ds-rom overlays are always decompressed on disk
            if (RomInfo.OverlaysStayFlat)
            {
                AppLogger.Info("ds-rom overlays are always stored decompressed on disk.");
                return 0; // Success - already decompressed
            }

            if (!File.Exists(overlayFilePath))
            {
                AppMessages.Error($"File to decompress \"{overlayFilePath}\" doesn't exist",
                    "Overlay not found");
                return ErrOverlayNotfound;
            }

            if (makeBackup)
            {
                if (File.Exists(overlayFilePath + BackupSuffix))
                {
                    File.Delete(overlayFilePath + BackupSuffix);
                }
                PlainFiles.Copy(overlayFilePath, overlayFilePath + BackupSuffix);
            }

            Process decompress = DSUtils.CreateDecompressProcess(overlayFilePath);
            if (decompress == null) return DSUtils.ErrToolUnavailable;
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
