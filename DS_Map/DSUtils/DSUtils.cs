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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using YamlDotNet.RepresentationModel;
using static DSPRE.RomInfo;

namespace DSPRE
{
    public static class DSUtils
    {
        public const int ERR_OVERLAY_NOTFOUND = -1;
        public const int ERR_OVERLAY_ALREADY_UNCOMPRESSED = -2;

        public const string backupSuffix = ".backup";

        public static readonly string NDSRomFilter = "NDS File (*.nds)|*.nds";

        public static bool legacyMode = false; // true if using legacy ndstool, false if using dsrom

        public class EasyReader : BinaryReader
        {
            public EasyReader(string path, long pos = 0) : base(File.OpenRead(path))
            {
                this.BaseStream.Position = pos;
            }
        }
        public class EasyWriter : BinaryWriter
        {
            public EasyWriter(string path, long pos = 0, FileMode fmode = FileMode.OpenOrCreate) : base(new FileStream(path, fmode, FileAccess.Write, FileShare.None))
            {
                this.BaseStream.Position = pos;
            }
            public void EditSize(int increment)
            {
                this.BaseStream.SetLength(this.BaseStream.Length + increment);
            }
        }

        public static void WriteToFile(string filepath, byte[] toOutput, uint writeAt = 0, int indexFirstByteToWrite = 0, int? indexLastByteToWrite = null, FileMode fmode = FileMode.OpenOrCreate)
        {
            using (EasyWriter writer = new EasyWriter(filepath, writeAt, fmode))
            {
                writer.Write(toOutput, indexFirstByteToWrite, indexLastByteToWrite is null ? toOutput.Length - indexFirstByteToWrite : (int)indexLastByteToWrite);
            }
        }
        public static byte[] ReadFromFile(string filepath, long startOffset = 0, long numberOfBytes = 0)
        {
            byte[] buffer = null;

            using (EasyReader reader = new EasyReader(filepath, startOffset))
            {
                try
                {
                    buffer = reader.ReadBytes(numberOfBytes == 0 ? (int)(reader.BaseStream.Length - reader.BaseStream.Position) : (int)numberOfBytes);
                }
                catch (EndOfStreamException)
                {
                    Console.WriteLine("Stream ended");
                }
            }

            return buffer;
        }
        public static byte[] ReadFromByteArray(byte[] input, long readFrom = 0, long numberOfBytes = 0)
        {
            byte[] buffer = null;

            using (BinaryReader reader = new BinaryReader(new MemoryStream(input)))
            {
                reader.BaseStream.Position = readFrom;

                try
                {
                    if (numberOfBytes == 0)
                    {
                        buffer = reader.ReadBytes((int)(input.Length - reader.BaseStream.Position));
                    }
                    else
                    {
                        buffer = reader.ReadBytes((int)numberOfBytes);
                    }
                }
                catch (EndOfStreamException)
                {
                    Console.WriteLine("Stream ended");
                }
            }
            return buffer;
        }
        public static Process CreateDecompressProcess(string path)
        {
            Process decompress = new Process();
            decompress.StartInfo.FileName = @"Tools\blz.exe";
            decompress.StartInfo.Arguments = @" -d " + '"' + path + '"';
            decompress.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            decompress.StartInfo.CreateNoWindow = true;
            return decompress;

        }

        public static void RepackROMLegacy(string ndsFileName)
        {
            Process repack = new Process();
            repack.StartInfo.FileName = @"Tools\ndstool.exe";
            repack.StartInfo.Arguments = "-c " + '"' + ndsFileName + '"'
                + " -9 " + '"' + RomInfo.arm9Path + '"'
                + " -7 " + '"' + RomInfo.arm7Path + '"'
                + " -y9 " + '"' + RomInfo.overlayTablePath + '"'
                + " -y7 " + '"' + RomInfo.y7Path + '"'
                + " -d " + '"' + RomInfo.dataPath + '"'
                + " -y " + '"' + RomInfo.overlayPath + '"'
                + " -t " + '"' + RomInfo.bannerPath + '"'
                + " -h " + '"' + RomInfo.headerPath + '"';

            repack.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            repack.StartInfo.CreateNoWindow = true;
            repack.StartInfo.UseShellExecute = false;
            repack.StartInfo.RedirectStandardError = true;

            AppLogger.Debug("Repacking ROM (legacy) with command: " + repack.StartInfo.FileName + " " + repack.StartInfo.Arguments);
            Application.DoEvents();

            string errors = "";

            try
            {
                repack.Start();
                errors = repack.StandardError.ReadToEnd().Trim();
                repack.WaitForExit();
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                AppLogger.Error("Failed to call ndstool.exe: " + ex.Message);
                MessageBox.Show("Failed to call ndstool.exe" + Environment.NewLine + "Make sure DSPRE's Tools folder is intact." + Environment.NewLine + Environment.NewLine + "Error: " + ex.Message,
                    "Couldn't repack ROM", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            AppLogger.Debug("ndstool.exe returned: " + repack.ExitCode);

            if (!string.IsNullOrWhiteSpace(errors))
            {
                AppLogger.Error("ndstool.exe returned the following error(s): " + errors);
                MessageBox.Show("An error occurred while repacking the ROM:" + Environment.NewLine + errors + Environment.NewLine,
                    "ndstool Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public static string WorkDirPathFromFile(string filePath)
        {
            filePath = Path.GetFullPath(filePath);
            return Path.Combine(Path.GetDirectoryName(filePath), Path.GetFileNameWithoutExtension(filePath) + "_DSPRE_contents");
        }

        public static void RepackROM(string ndsFileName)
        {
            string dsromPath = Path.Combine(Application.StartupPath, "Tools", "dsrom.exe");
            string ndsFileAbs = Path.GetFullPath(ndsFileName);
            string configFileAbs = Path.GetFullPath(RomInfo.workDir + "config.yaml");

            Process repack = new Process();
            repack.StartInfo.FileName = dsromPath;
            repack.StartInfo.Arguments = $"build --config \"{configFileAbs}\" --rom \"{ndsFileAbs}\"";
            repack.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            repack.StartInfo.CreateNoWindow = true;
            repack.StartInfo.UseShellExecute = false;

            AppLogger.Debug("Repacking ROM with command: " + repack.StartInfo.FileName + " " + repack.StartInfo.Arguments);
            Application.DoEvents();

            try
            {
                repack.Start();
                repack.WaitForExit();
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                AppLogger.Error("Failed to call dsrom.exe: " + ex.Message);
                MessageBox.Show("Failed to call dsrom.exe" + Environment.NewLine + "Make sure DSPRE's Tools folder is intact and dsrom.exe is present." + Environment.NewLine + Environment.NewLine + "Error: " + ex.Message,
                    "Couldn't repack ROM", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            AppLogger.Debug("dsrom.exe returned: " + repack.ExitCode);
        }

        // Unpack ROM using ndstool
        public static bool UnpackROMLegacy(string ndsFileName)
        {
            Directory.CreateDirectory(RomInfo.workDir);
            Process unpack = new Process();
            unpack.StartInfo.FileName = @"Tools\ndstool.exe";
            unpack.StartInfo.Arguments = "-x " + '"' + ndsFileName + '"'
                + " -9 " + '"' + RomInfo.arm9Path + '"'
                + " -7 " + '"' + RomInfo.workDir + "arm7.bin" + '"'
                + " -y9 " + '"' + RomInfo.workDir + "y9.bin" + '"'
                + " -y7 " + '"' + RomInfo.workDir + "y7.bin" + '"'
                + " -d " + '"' + RomInfo.workDir + "data" + '"'
                + " -y " + '"' + RomInfo.workDir + "overlay" + '"'
                + " -t " + '"' + RomInfo.workDir + "banner.bin" + '"'
                + " -h " + '"' + RomInfo.workDir + "header.bin" + '"';

            AppLogger.Debug("Unpacking ROM with command: " + unpack.StartInfo.FileName + " " + unpack.StartInfo.Arguments);
            Application.DoEvents();
            unpack.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            unpack.StartInfo.CreateNoWindow = true;
            try
            {
                unpack.Start();
                unpack.WaitForExit();
            }
            catch (System.ComponentModel.Win32Exception)
            {
                AppLogger.Error("Failed to call ndstool.exe");
                MessageBox.Show("Failed to call ndstool.exe" + Environment.NewLine + "Make sure DSPRE's Tools folder is intact.",
                    "Couldn't unpack ROM", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            AppLogger.Debug("ndstool.exe returned: " + unpack.ExitCode);

            return true;
        }

        public static bool UnpackROM(string ndsFileName, string customFolderPath = "")
        {
            string dsromPath = Path.Combine(Application.StartupPath, "Tools", "dsrom.exe");
            string ndsFileAbs = Path.GetFullPath(ndsFileName);
            string workDirAbs = "";

            if (!customFolderPath.Equals(string.Empty))
            {
                // If a custom folder path is provided, use it instead of the default work directory
                workDirAbs = Path.GetFullPath(customFolderPath);
            }
            else
            {
                workDirAbs = Path.GetFullPath(RomInfo.workDir);
            }

            workDirAbs = workDirAbs.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            Process unpack = new Process();
            unpack.StartInfo.FileName = $"{dsromPath}";
            unpack.StartInfo.Arguments = $"extract -r \"{ndsFileAbs}\" -o \"{workDirAbs}\"";
            unpack.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            unpack.StartInfo.CreateNoWindow = true;
            unpack.StartInfo.UseShellExecute = false;

            AppLogger.Debug("Unpacking ROM with command: " + unpack.StartInfo.FileName + " " + unpack.StartInfo.Arguments);
            Application.DoEvents();

            try
            {
                unpack.Start();
                unpack.WaitForExit();
            }
            catch (System.ComponentModel.Win32Exception)
            {
                AppLogger.Error("Failed to call dsrom.exe");
                MessageBox.Show("Failed to call dsrom.exe" + Environment.NewLine + "Make sure DSPRE's Tools folder is intact.",
                    "Couldn't unpack ROM", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            AppLogger.Debug("dsrom.exe returned: " + unpack.ExitCode);

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

        public static bool IsROMPackedByNdstool(string filePath)
        {
            if (!File.Exists(filePath))
                return false;

            // Check if file is packed by ndstool via file size
            FileInfo fileInfo = new FileInfo(filePath);

            // Correctly packed ROMs have file sizes that are powers of two
            // Due to ndstool's trimming, the file size will likely not be a power of two
            // We therefore check if the file size is a power of two and assume that if it is not, it is packed by ndstool
            return !((fileInfo.Length & (fileInfo.Length - 1)) == 0);
        }

        public static bool ResetPathOrder(string folderPath)
        {
            if (!Directory.Exists(folderPath))
                return false;
            // Reset the path order by writing a single slash to the path_order.txt file
            string pathOrderFile = Path.Combine(folderPath, "path_order.txt");
            try
            {
                File.WriteAllText(pathOrderFile, "/");
            }
            catch (Exception ex)
            {
                AppLogger.Error("Failed to reset path order: " + ex.Message);
                MessageBox.Show("Failed to reset path order: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            return true;
        }

        public static bool MarkARM9Recompression(string folderPath)
        {
            try
            {
                string arm9YamlPath = Path.Combine(folderPath, "arm9/arm9.yaml");
                StreamReader reader = new StreamReader(arm9YamlPath);
                YamlStream yamlStream = new YamlStream();
                yamlStream.Load(reader);
                reader.Close();

                YamlMappingNode rootNode = (YamlMappingNode)yamlStream.Documents[0].RootNode;

                if (rootNode.Children.ContainsKey("compressed"))
                {
                    rootNode.Children["compressed"] = new YamlScalarNode("true");
                    // Save the changes back to the YAML file
                    using (StreamWriter writer = new StreamWriter(arm9YamlPath))
                    {
                        yamlStream.Save(writer, false);
                        writer.Close();
                    }
                }
                else
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error("Failed to mark ARM9 for recompression: " + ex.Message);
                MessageBox.Show("Failed to mark ARM9 for recompression: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            return true;
        }

        public static bool ConvertLegacyROMFolder(string folderPath)
        {
            if (!Directory.Exists(folderPath))
                return false;

            // Pack all NARCs
            foreach (KeyValuePair<DirNames, (string packedDir, string unpackedDir)> kvp in RomInfo.gameDirs)
            {
                DirectoryInfo di = new DirectoryInfo(kvp.Value.unpackedDir);
                if (di.Exists)
                {
                    Narc.FromFolder(kvp.Value.unpackedDir).Save(kvp.Value.packedDir); // Make new NARC from folder
                }
            }

            // If there is a compression mismatch conversion will fail so this is required
            if (ARM9.CheckCompressionMark())
            {
                ARM9.WriteBytes(new byte[4] { 0, 0, 0, 0 }, (uint)(RomInfo.gameFamily == GameFamilies.DP ? 0xB7C : 0xBB4));
            }

            uint hashcode = (uint)DateTime.Now.GetHashCode();
            string parentDir = Directory.GetParent(folderPath).FullName;
            string tempRomPath = Path.Combine(parentDir, "temp_" + hashcode + ".nds");
            string backupFolderPath = Path.Combine(parentDir, "backup_" + hashcode);
            string newUnpackedFolderPath = Path.Combine(parentDir, "temp_" + hashcode);

            // Pack the ROM using ndstool
            RepackROMLegacy(tempRomPath);
            if (!File.Exists(tempRomPath))
            {
                MessageBox.Show("Failed to create new ROM file.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            // Unpack the new ROM
            if (!UnpackROM(tempRomPath, newUnpackedFolderPath))
            {
                return false;
            }

            // Delete the temporary ROM file
            try
            {
                File.Delete(tempRomPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to delete temporary ROM file: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            if (!Directory.Exists(newUnpackedFolderPath))
            {
                return false;
            }

            // Rename old folder to backup
            try
            {
                Directory.Move(folderPath, backupFolderPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to rename old folder: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            // Move the new unpacked folder to the original folder path
            try
            {
                Directory.Move(newUnpackedFolderPath, folderPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to move new unpacked folder: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            // Reset the path order
            if (!ResetPathOrder(folderPath))
            {
                return false;
            }

            // Mark ARM9 for recompression
            if (!MarkARM9Recompression(folderPath))
            {
                return false;
            }

            return true;
        }

        public static byte[] StringToByteArray(String hex)
        {
            //Ummm what?
            int NumberChars = hex.Length;
            byte[] bytes = new byte[NumberChars / 2];
            for (int i = 0; i < NumberChars; i += 2)
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            return bytes;
        }
        public static byte[] HexStringToByteArray(string hexString)
        {
            //FC B5 05 48 C0 46 41 21
            //09 22 02 4D A8 47 00 20
            //03 21 FC BD F1 64 00 02
            //00 80 3C 02
            if (hexString is null)
                return null;

            hexString = hexString.Trim();

            byte[] b = new byte[hexString.Length / 3 + 1];
            for (int i = 0; i < hexString.Length; i += 2)
            {
                if (hexString[i] == ' ')
                {
                    hexString = hexString.Substring(1, hexString.Length - 1);
                }

                b[i / 2] = Convert.ToByte(hexString.Substring(i, 2), 16);
            }
            return b;
        }

        public static void TryUnpackNarcs(List<DirNames> IDs)
        {
            if (gameDirs == null || gameDirs.Count == 0)
            {
                return;
            }
            Parallel.ForEach(IDs, id =>
            {
                if (gameDirs.TryGetValue(id, out (string packedPath, string unpackedPath) paths))
                {
                    DirectoryInfo di = new DirectoryInfo(paths.unpackedPath);

                    if (!di.Exists || di.GetFiles().Length == 0)
                    {
                        Narc opened = Narc.Open(paths.packedPath) ?? throw new NullReferenceException();
                        opened.ExtractToFolder(paths.unpackedPath);
                    }
                }
            });
        }
        public static void ForceUnpackNarcs(List<DirNames> IDs)
        {
            Parallel.ForEach(IDs, id =>
            {
                if (gameDirs.TryGetValue(id, out (string packedPath, string unpackedPath) paths))
                {
                    Narc opened = Narc.Open(paths.packedPath);

                    if (opened is null)
                    {
                        throw new NullReferenceException();
                    }

                    opened.ExtractToFolder(paths.unpackedPath);
                }
            });
        }

        public static Image GetPokePic(int species, int w, int h)
        {
            PaletteBase paletteBase;
            bool fiveDigits = false; // some extreme future proofing
            string filename = "0000";

            try
            {
                paletteBase = new NCLR(gameDirs[DirNames.monIcons].unpackedDir + "\\" + filename, 0, filename);
            }
            catch (FileNotFoundException)
            {
                filename += '0';
                paletteBase = new NCLR(gameDirs[DirNames.monIcons].unpackedDir + "\\" + filename, 0, filename);
                fiveDigits = true;
            }

            // read arm9 table to grab pal ID
            int paletteId = 0;
            string iconTablePath;

            int iconPalTableOffsetFromFileStart;
            string ov129path = LegacyOverlayUtils.GetPath(129);
            if (File.Exists(ov129path))
            {
                // if overlay 129 exists, read it from there
                iconPalTableOffsetFromFileStart = (int)(RomInfo.monIconPalTableAddress - LegacyOverlayUtils.OverlayTable.GetRAMAddress(129));
                iconTablePath = ov129path;
            }
            else if ((int)(RomInfo.monIconPalTableAddress - RomInfo.synthOverlayLoadAddress) >= 0)
            {
                // if there is a synthetic overlay, read it from there
                iconPalTableOffsetFromFileStart = (int)(RomInfo.monIconPalTableAddress - RomInfo.synthOverlayLoadAddress);
                iconTablePath = gameDirs[DirNames.synthOverlay].unpackedDir + "\\" + PatchToolboxDialog.expandedARMfileID.ToString("D4");
            }
            else
            {
                // default handling
                iconPalTableOffsetFromFileStart = (int)(RomInfo.monIconPalTableAddress - ARM9.address);
                iconTablePath = RomInfo.arm9Path;
            }

            using (DSUtils.EasyReader idReader = new DSUtils.EasyReader(iconTablePath, iconPalTableOffsetFromFileStart + species))
            {
                paletteId = idReader.ReadByte();
            }

            if (paletteId != 0)
            {
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
            for (int i = 0; i < OAMenabled.Length; i++)
            {
                OAMenabled[i] = i;
            }

            // finally compose image
            try
            {
                return spriteBase.Get_Image(imageBase, paletteBase, 0, w, h, false, false, false, true, true, -1, OAMenabled);
            }
            catch (FormatException)
            {
                return Properties.Resources.IconPokeball;
            }
        }
    }
}
