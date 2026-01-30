using Ekona.Images;
using Images;
using LibNDSFormats.NSBMD;
using Microsoft.WindowsAPICodePack.Dialogs;
using NarcAPI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static DSPRE.RomInfo;

namespace DSPRE {
    public static class DSUtils {

        public const int ERR_OVERLAY_NOTFOUND = -1;
        public const int ERR_OVERLAY_ALREADY_UNCOMPRESSED = -2;

        public const string backupSuffix = ".backup";

        public static readonly string NDSRomFilter = "NDS File (*.nds)|*.nds";
        public class EasyReader : BinaryReader {
            public EasyReader(string path, long pos = 0) : base(File.OpenRead(path)) {
                this.BaseStream.Position = pos;
            }
        }
        public class EasyWriter : BinaryWriter {
            public EasyWriter(string path, long pos = 0, FileMode fmode = FileMode.OpenOrCreate) : base(new FileStream(path, fmode, FileAccess.Write, FileShare.None)) {
                this.BaseStream.Position = pos;
            }
            public void EditSize(int increment) {
                this.BaseStream.SetLength(this.BaseStream.Length + increment);
            }
        }

        public static void WriteToFile(string filepath, byte[] toOutput, uint writeAt = 0, int indexFirstByteToWrite = 0, int? indexLastByteToWrite = null, FileMode fmode = FileMode.OpenOrCreate) {
            using (EasyWriter writer = new EasyWriter(filepath, writeAt, fmode)) {
                writer.Write(toOutput, indexFirstByteToWrite, indexLastByteToWrite is null ? toOutput.Length - indexFirstByteToWrite : (int)indexLastByteToWrite);
            }
        }
        public static byte[] ReadFromFile(string filepath, long startOffset = 0, long numberOfBytes = 0) {
            byte[] buffer = null;

            using (EasyReader reader = new EasyReader(filepath, startOffset)) {
                try {
                    buffer = reader.ReadBytes(numberOfBytes == 0 ? (int)(reader.BaseStream.Length - reader.BaseStream.Position) : (int)numberOfBytes);
                } catch (EndOfStreamException) {
                    AppLogger.Error("Stream ended");
                }
            }

            return buffer;
        }
        public static byte[] ReadFromByteArray(byte[] input, long readFrom = 0, long numberOfBytes = 0) {
            byte[] buffer = null;

            using (BinaryReader reader = new BinaryReader(new MemoryStream(input))) {
                reader.BaseStream.Position = readFrom;

                try {
                    if (numberOfBytes == 0) {
                        buffer = reader.ReadBytes((int)(input.Length - reader.BaseStream.Position));
                    } else {
                        buffer = reader.ReadBytes((int)numberOfBytes);
                    }
                } catch (EndOfStreamException) {
                    AppLogger.Error("Stream ended");
                }
            }
            return buffer;
        }
        public static Process CreateDecompressProcess(string path) {
            Process decompress = new Process();
            decompress.StartInfo.FileName = @"Tools\blz.exe";
            decompress.StartInfo.Arguments = @" -d " + '"' + path + '"';
            decompress.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            decompress.StartInfo.CreateNoWindow = true;
            return decompress;

        }

        public static string WorkDirPathFromFile(string filePath)
        {
            filePath = Path.GetFullPath(filePath);
            return Path.Combine(Path.GetDirectoryName(filePath), Path.GetFileNameWithoutExtension(filePath) + RomInfo.folderSuffix);
        }

        public static bool UnpackRom(string ndsFileName, string workDir)
        {
            return UnpackRomDsRom(ndsFileName, workDir);
        }

        public static bool UnpackRomNdstool(string ndsFileName, string workDir)
        {
            Directory.CreateDirectory(workDir);

            string arm9Path = Path.Combine(workDir, "arm9.bin");
            string arm7Path = Path.Combine(workDir, "arm7.bin");
            string y9Path = Path.Combine(workDir, "y9.bin");
            string y7Path = Path.Combine(workDir, "y7.bin");
            string dataPath = Path.Combine(workDir, "data");
            string overlayPath = Path.Combine(workDir, "overlay");
            string bannerPath = Path.Combine(workDir, "banner.bin");
            string headerPath = Path.Combine(workDir, "header.bin");

            Process unpack = new Process();
            unpack.StartInfo.FileName = @"Tools\ndstool.exe";
            unpack.StartInfo.Arguments = "-x " + '"' + ndsFileName + '"'
                + " -9 " + '"' + arm9Path + '"'
                + " -7 " + '"' + arm7Path + '"'
                + " -y9 " + '"' + y9Path + '"'
                + " -y7 " + '"' + y7Path + '"'
                + " -d " + '"' + dataPath + '"'
                + " -y " + '"' + overlayPath + '"'
                + " -t " + '"' + bannerPath + '"'
                + " -h " + '"' + headerPath + '"';

            Application.DoEvents();

            unpack.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            unpack.StartInfo.CreateNoWindow = true;
            unpack.StartInfo.RedirectStandardError = true;
            unpack.StartInfo.UseShellExecute = false;

            string errors = "";

            AppLogger.Info("Unpacking ROM with command: " + unpack.StartInfo.FileName + " " + unpack.StartInfo.Arguments);

            try
            {
                unpack.Start();
                errors = unpack.StandardError.ReadToEnd().Trim();
                unpack.WaitForExit();

                
            }
            catch (System.ComponentModel.Win32Exception)
            {
                MessageBox.Show("Failed to call ndstool.exe" + Environment.NewLine + "Make sure DSPRE's Tools folder is intact.",
                    "Couldn't unpack ROM", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            if (!string.IsNullOrWhiteSpace(errors))
            {
                AppLogger.Error("ndstool returned the following error(s):" + errors);
                MessageBox.Show("An error occurred while unpacking the ROM:" + Environment.NewLine + errors + Environment.NewLine,
                    "Couldn't unpack ROM", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            return true;
        }

        public static bool UnpackRomDsRom(string ndsFileName, string workDir)
        {
            Directory.CreateDirectory(workDir);

            Process unpack = new Process();
            unpack.StartInfo.FileName = @"Tools\dsrom.exe";
            unpack.StartInfo.Arguments = $"extract -r \"{ndsFileName}\" -o \"{workDir}\"";
            unpack.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            unpack.StartInfo.CreateNoWindow = true;
            unpack.StartInfo.RedirectStandardError = true;
            unpack.StartInfo.RedirectStandardOutput = true;
            unpack.StartInfo.UseShellExecute = false;

            AppLogger.Info("Unpacking ROM with command: " + unpack.StartInfo.FileName + " " + unpack.StartInfo.Arguments);

            string output = "";
            string errors = "";

            try
            {
                Application.DoEvents();
                unpack.Start();
                var outputTask = unpack.StandardOutput.ReadToEndAsync();
                var errorTask = unpack.StandardError.ReadToEndAsync();
                unpack.WaitForExit();
                output = outputTask.Result;
                errors = errorTask.Result.Trim();

                if (!string.IsNullOrWhiteSpace(output))
                {
                    AppLogger.Info("dsrom stdout: " + output);
                }
            }
            catch (System.ComponentModel.Win32Exception)
            {
                MessageBox.Show("Failed to call dsrom.exe" + Environment.NewLine + "Make sure DSPRE's Tools folder is intact.",
                    "Couldn't unpack ROM", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            if (unpack.ExitCode != 0)
            {
                AppLogger.Error("dsrom returned the following error(s): " + errors);
                MessageBox.Show("An error occurred while unpacking the ROM:" + Environment.NewLine + errors + Environment.NewLine,
                    "Couldn't unpack ROM", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            if (!string.IsNullOrWhiteSpace(errors))
            {
                AppLogger.Info("dsrom stderr: " + errors);
            }

            if (!File.Exists(Path.Combine(workDir, "config.yaml")))
            {
                AppLogger.Error("Validation failed: config.yaml not found after extraction");
                MessageBox.Show("ROM extraction failed: config.yaml not found in output directory.",
                    "Extraction Validation Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            if (!File.Exists(Path.Combine(workDir, "arm9", "arm9.bin")))
            {
                AppLogger.Error("Validation failed: arm9/arm9.bin not found after extraction");
                MessageBox.Show("ROM extraction failed: arm9/arm9.bin not found in output directory.",
                    "Extraction Validation Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            if (!Directory.Exists(Path.Combine(workDir, "files")))
            {
                AppLogger.Error("Validation failed: files/ directory not found after extraction");
                MessageBox.Show("ROM extraction failed: files/ directory not found in output directory.",
                    "Extraction Validation Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            return true;
        }

        public static bool RepackROMDsRom(string ndsFileName)
        {
            string configPath = Path.Combine(workDir, "config.yaml");

            if (!File.Exists(configPath))
            {
                AppLogger.Error("config.yaml not found, cannot build with ds-rom");
                MessageBox.Show("Cannot build ROM: config.yaml not found in the working directory.",
                    "Couldn't repack ROM", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            Process repack = new Process();
            repack.StartInfo.FileName = @"Tools\dsrom.exe";
            repack.StartInfo.Arguments = $"build -c \"{configPath}\" -o \"{ndsFileName}\"";
            repack.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            repack.StartInfo.CreateNoWindow = true;
            repack.StartInfo.RedirectStandardError = true;
            repack.StartInfo.RedirectStandardOutput = true;
            repack.StartInfo.UseShellExecute = false;

            AppLogger.Info("Repacking ROM with command: " + repack.StartInfo.FileName + " " + repack.StartInfo.Arguments);

            string output = "";
            string errors = "";

            try
            {
                Application.DoEvents();
                repack.Start();
                var outputTask = repack.StandardOutput.ReadToEndAsync();
                var errorTask = repack.StandardError.ReadToEndAsync();
                repack.WaitForExit();
                output = outputTask.Result;
                errors = errorTask.Result.Trim();

                if (!string.IsNullOrWhiteSpace(output))
                {
                    AppLogger.Info("dsrom stdout: " + output);
                }
            }
            catch (System.ComponentModel.Win32Exception)
            {
                MessageBox.Show("Failed to call dsrom.exe" + Environment.NewLine + "Make sure DSPRE's Tools folder is intact.",
                    "Couldn't repack ROM", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            if (repack.ExitCode != 0)
            {
                AppLogger.Error("dsrom returned the following error(s): " + errors);
                MessageBox.Show("An error occurred while repacking the ROM:" + Environment.NewLine + errors + Environment.NewLine,
                    "Couldn't repack ROM", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            if (!string.IsNullOrWhiteSpace(errors))
            {
                AppLogger.Info("dsrom stderr: " + errors);
            }

            return true;
        }

        public static bool RepackROM(string ndsFileName) {
            // Route to ds-rom if this is a ds-rom project
            if (RomInfo.IsDsRomProject)
            {
                return RepackROMDsRom(ndsFileName);
            }

            string arm9Path = Path.Combine(workDir, "arm9.bin");
            string arm7Path = Path.Combine(workDir, "arm7.bin");
            string y9Path = Path.Combine(workDir, "y9.bin");
            string y7Path = Path.Combine(workDir, "y7.bin");
            string dataPath = Path.Combine(workDir, "data");
            string overlayPath = Path.Combine(workDir, "overlay");
            string bannerPath = Path.Combine(workDir, "banner.bin");
            string headerPath = Path.Combine(workDir, "header.bin");

            Process repack = new Process();
            repack.StartInfo.FileName = @"Tools\ndstool.exe";
            repack.StartInfo.Arguments = "-c " + '"' + ndsFileName + '"'
                + " -9 " + '"' + arm9Path + '"'
                + " -7 " + '"' + arm7Path + '"'
                + " -y9 " + '"' + y9Path + '"'
                + " -y7 " + '"' + y7Path + '"'
                + " -d " + '"' + dataPath + '"'
                + " -y " + '"' + overlayPath + '"'
                + " -t " + '"' + bannerPath + '"'
                + " -h " + '"' + headerPath + '"';

            Application.DoEvents();
            repack.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            repack.StartInfo.CreateNoWindow = true;
            repack.StartInfo.RedirectStandardError = true;
            repack.StartInfo.UseShellExecute = false;

            string errors = "";

            AppLogger.Info("Repacking ROM with command: " + repack.StartInfo.FileName + " " + repack.StartInfo.Arguments);

            repack.Start();
            errors = repack.StandardError.ReadToEnd().Trim();
            repack.WaitForExit();

            if (!string.IsNullOrWhiteSpace(errors))
            {
                AppLogger.Error("ndstool returned the following error(s): " + errors);
                MessageBox.Show("An error occurred while repacking the ROM:" + Environment.NewLine + errors + Environment.NewLine,
                    "Couldn't repack ROM", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            return true;

        }

        public static int GetFolderType(string folderPath)
        {
            if (!Directory.Exists(folderPath))
                return -1;

            // Check if the folder contains a config.yaml file
            string configPath = Path.Combine(folderPath, "config.yaml");
            string headerPath = Path.Combine(folderPath, "header.bin");
            if (File.Exists(configPath))
            {
                return 0; // This is a dsrom folder
            }
            else if (File.Exists(headerPath))
            {
                return 1; // This is a ndstool folder
            }

            return -1; // Not a valid dsrom or ndstool folder

        }

        /// <summary>
        /// Converts a project directory from ndstool format to ds-rom format in place, creating a backup of
        /// the original project.
        /// </summary>
        /// <remarks>A ZIP backup of the original ndstool project is created in the same location as the
        /// project directory before any changes are made. If the conversion fails, the project directory remains
        /// unchanged and can be restored from the backup. User interaction may be required during the process to
        /// confirm actions or handle errors. The method displays message boxes to inform the user of progress and
        /// errors.</remarks>
        /// <param name="workDir">The full path to the project directory in ndstool format to be converted. Must be a valid directory path.</param>
        /// <returns>1 if the conversion to ds-rom format succeeds; 2 if the conversion fails but the user chooses to continue
        /// with the original ndstool format; 0 if the conversion fails and no changes are made.</returns>
        public static int ConvertNdstoolToDsRom(string workDir)
        {
            // 1. Verify project is ndstool format
            if (GetFolderType(workDir) != 1)
            {
                MessageBox.Show("This project is not in ndstool format.", "Conversion Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return 0;
            }

            // 2. Create ZIP backup
            string backupPath = workDir + ".ndstool_backup.zip";
            try
            {
                if (File.Exists(backupPath))
                    File.Delete(backupPath);
                ZipFile.CreateFromDirectory(workDir, backupPath);
                AppLogger.Info($"Created ndstool backup at: {backupPath}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to create backup: {ex.Message}", "Conversion Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 0;
            }

            // 3. Build temp ROM using ndstool (the project is still in ndstool format)
            string tempRomPath = Path.Combine(Path.GetDirectoryName(workDir), "temp_conversion.nds");
            string tempDsRomDir = workDir + "_dsrom_temp";

            try
            {
                // Use ndstool directly to build temp ROM
                Process buildTemp = new Process();
                buildTemp.StartInfo.FileName = @"Tools\ndstool.exe";
                buildTemp.StartInfo.Arguments = "-c \"" + tempRomPath + "\""
                    + " -9 \"" + Path.Combine(workDir, "arm9.bin") + "\""
                    + " -7 \"" + Path.Combine(workDir, "arm7.bin") + "\""
                    + " -y9 \"" + Path.Combine(workDir, "y9.bin") + "\""
                    + " -y7 \"" + Path.Combine(workDir, "y7.bin") + "\""
                    + " -d \"" + Path.Combine(workDir, "data") + "\""
                    + " -y \"" + Path.Combine(workDir, "overlay") + "\""
                    + " -t \"" + Path.Combine(workDir, "banner.bin") + "\""
                    + " -h \"" + Path.Combine(workDir, "header.bin") + "\"";
                buildTemp.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                buildTemp.StartInfo.CreateNoWindow = true;
                buildTemp.StartInfo.UseShellExecute = false;
                buildTemp.StartInfo.RedirectStandardError = true;

                AppLogger.Info("Building temp ROM: " + buildTemp.StartInfo.Arguments);
                Application.DoEvents();
                buildTemp.Start();
                var errorTask = buildTemp.StandardError.ReadToEndAsync();
                buildTemp.WaitForExit();
                string errors = errorTask.Result;

                if (buildTemp.ExitCode != 0)
                {
                    AppLogger.Error("ndstool build failed: " + errors);
                    MessageBox.Show("Failed to build temporary ROM: " + errors, "Conversion Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return 0;
                }

                // 4. Extract with ds-rom to get new structure
                if (!UnpackRomDsRom(tempRomPath, tempDsRomDir))
                {
                    AppLogger.Error("ds-rom extraction failed during conversion. This may indicate overlay compression issues.");
                    
                    var result = MessageBox.Show(
                        "Conversion to ds-rom format failed during ROM extraction.\n\n" +
                        "This is usually caused by corrupted or incompatible overlay compression in the ndstool project.\n\n" +
                        "Would you like to:\n" +
                        "• Yes: Continue loading with ndstool format (no conversion)\n" +
                        "• No: Cancel and restore from backup\n" +
                        "• Cancel: Abort loading",
                        "Conversion Failed",
                        MessageBoxButtons.YesNoCancel,
                        MessageBoxIcon.Warning);
                    
                    if (Directory.Exists(tempDsRomDir))
                        Directory.Delete(tempDsRomDir, true);
                    if (File.Exists(tempRomPath))
                        File.Delete(tempRomPath);
                    
                    if (result == DialogResult.Yes)
                    {
                        AppLogger.Info("User chose to continue with ndstool format.");
                        return 2;
                    }
                    else if (result == DialogResult.No)
                    {
                        AppLogger.Info("User chose to restore from backup.");
                        RestoreFromNdstoolBackup(workDir);
                        return 0;
                    }
                    else
                    {

                        MessageBox.Show("Conversion cancelled. Your ndstool project remains unchanged.\n\nBackup available at:\n" + backupPath,
                            "Conversion Cancelled", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return 0;
                    }
                }

                // 5. Validate temp output
                if (!File.Exists(Path.Combine(tempDsRomDir, "config.yaml")))
                {
                    MessageBox.Show("Conversion validation failed: config.yaml not found.", "Conversion Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Directory.Delete(tempDsRomDir, true);
                    File.Delete(tempRomPath);
                    return 0;
                }

                // 6. Delete old ndstool files from workDir
                string[] oldFiles = { "arm9.bin", "arm7.bin", "y9.bin", "y7.bin", "banner.bin", "header.bin" };
                string[] oldDirs = { "data", "overlay" };

                foreach (var f in oldFiles)
                {
                    string path = Path.Combine(workDir, f);
                    if (File.Exists(path)) File.Delete(path);
                }
                foreach (var d in oldDirs)
                {
                    string path = Path.Combine(workDir, d);
                    if (Directory.Exists(path)) Directory.Delete(path, true);
                }

                // 7. Move temp contents to workDir
                foreach (var entry in Directory.GetFileSystemEntries(tempDsRomDir))
                {
                    string destPath = Path.Combine(workDir, Path.GetFileName(entry));
                    if (File.Exists(entry))
                    {
                        if (File.Exists(destPath)) File.Delete(destPath);
                        File.Move(entry, destPath);
                    }
                    else if (Directory.Exists(entry))
                    {
                        if (Directory.Exists(destPath)) Directory.Delete(destPath, true);
                        Directory.Move(entry, destPath);
                    }
                }

                // 8. Cleanup
                Directory.Delete(tempDsRomDir, true);
                File.Delete(tempRomPath);

                AppLogger.Info("Successfully converted project to ds-rom format.");
                MessageBox.Show("Project converted to ds-rom format successfully.\n\nBackup saved at:\n" + backupPath,
                    "Conversion Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return 1;
            }
            catch (Exception ex)
            {
                AppLogger.Error("Conversion failed: " + ex.Message);
                MessageBox.Show($"Conversion failed: {ex.Message}\n\nYour backup is at:\n{backupPath}",
                    "Conversion Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);

                // Cleanup temp files on failure
                if (Directory.Exists(tempDsRomDir))
                    Directory.Delete(tempDsRomDir, true);
                if (File.Exists(tempRomPath))
                    File.Delete(tempRomPath);

            return 0;
        }
    }

    public static bool RestoreFromNdstoolBackup(string workDir)
    {
        string backupPath = workDir + ".ndstool_backup.zip";
        
        if (!File.Exists(backupPath))
        {
            MessageBox.Show("Backup file not found:\n" + backupPath, 
                "Restore Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
        
        try
        {
            // Delete current contents
            foreach (var file in Directory.GetFiles(workDir))
            {
                File.Delete(file);
            }
            foreach (var dir in Directory.GetDirectories(workDir))
            {
                Directory.Delete(dir, true);
            }
            
            // Extract backup
            ZipFile.ExtractToDirectory(backupPath, workDir);
            
            AppLogger.Info("Successfully restored from backup: " + backupPath);
            MessageBox.Show("Project restored from backup successfully.", 
                "Restore Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return true;
        }
        catch (Exception ex)
        {
            AppLogger.Error("Restore failed: " + ex.Message);
            MessageBox.Show($"Restore failed: {ex.Message}", 
                "Restore Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    public static byte[] StringToByteArray(String hex) {
            //Ummm what?
            int NumberChars = hex.Length;
            byte[] bytes = new byte[NumberChars / 2];
            for (int i = 0; i < NumberChars; i += 2)
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            return bytes;
        }
        public static byte[] HexStringToByteArray(string hexString) {
            //FC B5 05 48 C0 46 41 21 
            //09 22 02 4D A8 47 00 20 
            //03 21 FC BD F1 64 00 02 
            //00 80 3C 02
            if (hexString is null)
                return null;

            hexString = hexString.Trim();

            byte[] b = new byte[hexString.Length / 3 + 1];
            for (int i = 0; i < hexString.Length; i += 2) {
                if (hexString[i] == ' ') {
                    hexString = hexString.Substring(1, hexString.Length - 1);
                }

                b[i / 2] = Convert.ToByte(hexString.Substring(i, 2), 16);
            }
            return b;
        }

        public static void TryUnpackNarcs(List<DirNames> IDs) {
            if (gameDirs == null || gameDirs.Count == 0) {
                return;
            }    
            Parallel.ForEach(IDs, id => {
                if (gameDirs.TryGetValue(id, out (string packedPath, string unpackedPath) paths)) {
                    DirectoryInfo di = new DirectoryInfo(paths.unpackedPath);

                    if (di.Exists && di.GetFiles().Length > 0) {
                        return;
                    }

                    if (!File.Exists(paths.packedPath)) {
                        AppLogger.Error($"Tried to unpack NARC at {paths.packedPath}, but file does not exist.");
                        return;
                    }

                    Narc opened = Narc.Open(paths.packedPath) ?? throw new NullReferenceException();
                    opened.ExtractToFolder(paths.unpackedPath);

                }
            });
        }
        public static void ForceUnpackNarcs(List<DirNames> IDs) {
            Parallel.ForEach(IDs, id => {
                if (gameDirs.TryGetValue(id, out (string packedPath, string unpackedPath) paths)) {

                    if (!File.Exists(paths.packedPath))
                    {
                        AppLogger.Error($"Tried to unpack NARC at {paths.packedPath}, but file does not exist.");
                        return;
                    }

                    Narc opened = Narc.Open(paths.packedPath);

                    if (opened is null) {
                        throw new NullReferenceException();
                    }

                    opened.ExtractToFolder(paths.unpackedPath);
                }
            });
        }

        public static Image GetPokePic(int species, int w, int h) {
            PaletteBase paletteBase;
            bool fiveDigits = false; // some extreme future proofing
            string filename = "0000";

            try {
                paletteBase = new NCLR(gameDirs[DirNames.monIcons].unpackedDir + "\\" + filename, 0, filename);
            } catch (FileNotFoundException) {
                filename += '0';
                paletteBase = new NCLR(gameDirs[DirNames.monIcons].unpackedDir + "\\" + filename, 0, filename);
                fiveDigits = true;
            }

            // read arm9 table to grab pal ID
            int paletteId = 0;
            string iconTablePath;

            int iconPalTableOffsetFromFileStart;
            if (RomInfo.isHGE) {
                // if overlay 129 exists, read it from there
                iconPalTableOffsetFromFileStart = (int)(RomInfo.monIconPalTableAddress - OverlayUtils.OverlayTable.GetRAMAddress(129));
                iconTablePath = OverlayUtils.GetPath(129);
            } else if ((int)(RomInfo.monIconPalTableAddress - RomInfo.synthOverlayLoadAddress) >= 0) {
                // if there is a synthetic overlay, read it from there
                iconPalTableOffsetFromFileStart = (int)(RomInfo.monIconPalTableAddress - RomInfo.synthOverlayLoadAddress);
                iconTablePath = Filesystem.expArmPath;
            } else {
                // default handling
                iconPalTableOffsetFromFileStart = (int)(RomInfo.monIconPalTableAddress - ARM9.address);
                iconTablePath = RomInfo.arm9Path;
            }

            using (DSUtils.EasyReader idReader = new DSUtils.EasyReader(iconTablePath, iconPalTableOffsetFromFileStart + species)) {
                paletteId = idReader.ReadByte();
            }

            if (paletteId != 0) {
                paletteBase.Palette[0] = paletteBase.Palette[paletteId]; // update pal 0 to be the new pal
            }

            // grab tiles
            int spriteFileID = species + 7;
            string spriteFilename = spriteFileID.ToString("D" + (fiveDigits ? "5" : "4"));
            ImageBase imageBase = new NCGR(gameDirs[DirNames.monIcons].unpackedDir + "\\" + spriteFilename, spriteFileID, spriteFilename);

            // grab sprite
            int ncerFileId = 2;
            string ncerFileName = ncerFileId.ToString("D" + (fiveDigits ? "5" : "4"));
            SpriteBase spriteBase = new NCER(gameDirs[DirNames.monIcons].unpackedDir + "\\" + ncerFileName, 2, ncerFileName);

            // copy this from the trainer
            int bank0OAMcount = spriteBase.Banks[0].oams.Length;
            int[] OAMenabled = new int[bank0OAMcount];
            for (int i = 0; i < OAMenabled.Length; i++) {
                OAMenabled[i] = i;
            }

            // finally compose image
            try {
                return spriteBase.Get_Image(imageBase, paletteBase, 0, w, h, false, false, false, true, true, -1, OAMenabled);
            } catch (FormatException) {
                return Properties.Resources.IconPokeball;
            }
        }
    }
}
