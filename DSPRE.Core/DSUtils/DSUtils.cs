using DSPRE.HgEngine;
using Ekona.Images;
using Images;
using LibNDSFormats.NSBMD;
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
using static DSPRE.RomInfo;

namespace DSPRE {
    public static class DSUtils {

        public const int ERR_OVERLAY_NOTFOUND = -1;
        public const int ERR_OVERLAY_ALREADY_UNCOMPRESSED = -2;
        public const int ERR_TOOL_UNAVAILABLE = -3;

        public static int ReplaceTextEverywhere(string searchString, string replaceString, bool caseSensitive) {
            return ReplaceTextEverywhere(new[] { (searchString, replaceString, caseSensitive) });
        }

        // Advancing past each replacement instead of rescanning from 0 avoids looping forever when a
        // replacement text itself matches its own search text, e.g. renaming "PIKABLU" to "Pikablu".
        public static int ReplaceTextEverywhere(IEnumerable<(string searchString, string replaceString, bool caseSensitive)> replacements) {
            var pairs = replacements.Where(r => !string.IsNullOrEmpty(r.searchString) && r.searchString != r.replaceString).ToList();
            if (pairs.Count == 0) {
                return 0;
            }

            int archiveCount = Filesystem.GetTextArchivesCount();
            int archivesChanged = 0;

            for (int i = 0; i < archiveCount; i++) {
                var archive = new DSPRE.ROMFiles.TextArchive(i);
                bool changed = false;

                for (int j = 0; j < archive.messages.Count; j++) {
                    string text = archive.messages[j];
                    foreach (var pair in pairs) {
                        StringComparison comparison = pair.caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                        int searchFrom = 0;
                        int posFound;
                        while ((posFound = text.IndexOf(pair.searchString, searchFrom, comparison)) >= 0) {
                            text = text.Substring(0, posFound) + pair.replaceString + text.Substring(posFound + pair.searchString.Length);
                            searchFrom = posFound + pair.replaceString.Length;
                            changed = true;
                        }
                    }
                    archive.messages[j] = text;
                }

                if (changed) {
                    archive.SaveToExpandedDir(i, showSuccessMessage: false);
                    archivesChanged++;
                }
            }

            return archivesChanged;
        }

        // Anything longer than the 3-command "give item" template is the shared execution routine, not a pickable entry.
        public static bool IsGroundItemScriptEntry(DSPRE.ROMFiles.ScriptCommandContainer container) {
            return container.commands != null && container.commands.Count <= 4
                && container.commands.Count >= 2
                && container.commands[0].cmdParams != null && container.commands[0].cmdParams.Count >= 2
                && container.commands[1].cmdParams != null && container.commands[1].cmdParams.Count >= 2;
        }

        public static List<(int scriptIndex, int itemId, int quantity)> GetGroundItemScriptEntries(DSPRE.ROMFiles.ScriptFile itemScript) {
            var result = new List<(int scriptIndex, int itemId, int quantity)>();

            for (int i = 0; i < itemScript.allScripts.Count; i++) {
                var container = itemScript.allScripts[i];
                if (!IsGroundItemScriptEntry(container)) {
                    continue;
                }

                int itemId = BitConverter.ToUInt16(container.commands[0].cmdParams[1], 0);
                int quantity = BitConverter.ToUInt16(container.commands[1].cmdParams[1], 0);
                result.Add((i, itemId, quantity));
            }

            return result;
        }

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
        // ── Mon party-icon palette table (1 byte per species: 0/1/2) ──────────────────────────────
        // The table lives at RomInfo.monIconPalTableAddress; which file + offset depends on the overlay
        // setup, exactly as GetPokePic resolves it when rendering icons. Reused here for the editor.
        // Requires RomInfo.SetMonIconsPalTableAddress() to have been called first.
        public static bool TryResolveMonIconPalTable(out string path, out int baseOffset) {
            path = null; baseOffset = 0;
            if (RomInfo.isHGE) {
                baseOffset = (int)(RomInfo.monIconPalTableAddress - OverlayUtils.OverlayTable.GetRAMAddress(129));
                path = OverlayUtils.GetPath(129);
            } else if ((int)(RomInfo.monIconPalTableAddress - RomInfo.synthOverlayLoadAddress) >= 0) {
                baseOffset = (int)(RomInfo.monIconPalTableAddress - RomInfo.synthOverlayLoadAddress);
                path = Filesystem.expArmPath;
            } else {
                baseOffset = (int)(RomInfo.monIconPalTableAddress - ARM9.address);
                path = RomInfo.arm9Path;
            }
            return path != null && baseOffset >= 0;
        }

        /// <summary>Reads the party-icon palette id (0/1/2) for a species, or 0 if unavailable.</summary>
        public static int GetMonIconPaletteId(int species) {
            if (!TryResolveMonIconPalTable(out string path, out int baseOff)) return 0;
            using (EasyReader r = new EasyReader(path, baseOff + species)) return r.ReadByte();
        }

        /// <summary>Writes the party-icon palette id (0/1/2) for a species into the resolved table file.</summary>
        public static void SetMonIconPaletteId(int species, byte palId) {
            if (!TryResolveMonIconPalTable(out string path, out int baseOff)) return;
            using (EasyWriter w = new EasyWriter(path, baseOff + species)) w.Write(palId);
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

        /// <summary>Returns every offset in <paramref name="haystack"/> where <paramref name="needle"/> occurs
        /// (naive scan; needles here are short fixed byte patterns, not large enough to need Boyer-Moore).</summary>
        public static List<int> SearchBytes(byte[] haystack, byte[] needle) {
            var matches = new List<int>();
            if (haystack == null || needle == null || needle.Length == 0 || needle.Length > haystack.Length) {
                return matches;
            }

            for (int i = 0; i <= haystack.Length - needle.Length; i++) {
                bool isMatch = true;
                for (int j = 0; j < needle.Length; j++) {
                    if (haystack[i + j] != needle[j]) {
                        isMatch = false;
                        break;
                    }
                }
                if (isMatch) matches.Add(i);
            }
            return matches;
        }

        /// <summary>
        /// Resolves a bundled tool. Windows uses the .exe file; other platforms prefer an
        /// extensionless native binary and fall back to the .exe when no native binary exists.
        /// </summary>
        public static string ToolPath(string name)
        {
            string toolsDirectory = Path.Combine(System.AppContext.BaseDirectory, "Tools");
            string nativePath = Path.Combine(toolsDirectory, name);
            if (OperatingSystem.IsWindows()) return nativePath + ".exe";

            // TODO(macOS): distinguish macOS-native tools from the Linux binaries currently bundled
            // under extensionless names. macOS will likely use the .exe files through Wine instead.
            if (File.Exists(nativePath)) return nativePath;

            string windowsPath = nativePath + ".exe";
            return File.Exists(windowsPath) ? windowsPath : nativePath;
        }

        /// <summary>
        /// Configures a process to run a bundled tool directly, through WSL's own interop when only
        /// the Windows executable is available, or through Wine as the last resort on real Linux.
        /// Returns false when nothing can run the tool, allowing callers to report the failure without
        /// reaching Process.Start(). Call this after setting any tool arguments.
        /// </summary>
        public static bool ConfigureToolStartInfo(ProcessStartInfo startInfo, string name)
        {
            if (startInfo == null) throw new ArgumentNullException(nameof(startInfo));

            string toolPath = ToolPath(name);
            if (!File.Exists(toolPath)) return false;

            if (OperatingSystem.IsWindows())
            {
                startInfo.FileName = toolPath;
                return true;
            }

            if (!toolPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                if (TryEnsureUnixExecutable(toolPath))
                {
                    startInfo.FileName = toolPath;
                    return true;
                }

                string windowsPath = toolPath + ".exe";
                if (!File.Exists(windowsPath)) return false;
                toolPath = windowsPath;
            }

            if (IsWsl())
            {
                if (!TryEnsureUnixExecutable(toolPath)) return false;
                ConfigureWslStartInfo(startInfo, toolPath);
                return true;
            }

            if (!IsCommandAvailable("wine")) return false;

            ConfigureWineStartInfo(startInfo, toolPath);
            return true;
        }

        /// <summary>True when running inside WSL, where the kernel can execute a Windows .exe directly
        /// (no Wine needed). WSL2 sets this env var by default.</summary>
        private static bool IsWsl() =>
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WSL_DISTRO_NAME"));

        /// <summary>No path rewriting needed here: WSL interop already resolves plain Linux paths in a
        /// launched .exe's arguments on its own (confirmed directly against ndstool.exe; a \\wsl.localhost
        /// UNC rewrite, the Wine-style fix, actually broke it).</summary>
        private static void ConfigureWslStartInfo(ProcessStartInfo startInfo, string toolPath)
        {
            startInfo.FileName = toolPath;
        }

        private static bool TryEnsureUnixExecutable(string toolPath)
        {
            const UnixFileMode executeBits = UnixFileMode.UserExecute
                | UnixFileMode.GroupExecute
                | UnixFileMode.OtherExecute;

            try
            {
                UnixFileMode mode = File.GetUnixFileMode(toolPath);
                if ((mode & executeBits) != executeBits)
                    File.SetUnixFileMode(toolPath, mode | executeBits);
                return true;
            }
            catch (Exception ex) when (ex is IOException
                || ex is UnauthorizedAccessException
                || ex is PlatformNotSupportedException)
            {
                AppLogger.Error($"Unable to make native tool executable: {toolPath}: {ex.Message}");
                return false;
            }
        }

        private static void ConfigureWineStartInfo(ProcessStartInfo startInfo, string toolPath)
        {
            startInfo.FileName = "wine";
            // Wine's own diagnostics would otherwise be mixed into tool stderr; legacy callers use
            // non-empty stderr as a failure signal.
            startInfo.Environment["WINEDEBUG"] = "-all";

            for (int i = 0; i < startInfo.ArgumentList.Count; i++)
                startInfo.ArgumentList[i] = ToWinePath(startInfo.ArgumentList[i]);

            if (startInfo.ArgumentList.Count > 0)
            {
                startInfo.ArgumentList.Insert(0, toolPath);
                return;
            }

            startInfo.Arguments = ConvertUnixPathsToWine(startInfo.Arguments);
            string toolArgument = '"' + toolPath.Replace("\"", "\\\"") + '"';
            startInfo.Arguments = string.IsNullOrWhiteSpace(startInfo.Arguments)
                ? toolArgument
                : toolArgument + " " + startInfo.Arguments;
        }

        /// <summary>True on real (non-WSL) Linux/macOS with no Wine on PATH: none of the bundled
        /// .exe-only tools (ndstool, blz, apicula) can run at all in that case.</summary>
        public static bool RequiresWineButUnavailable() =>
            !OperatingSystem.IsWindows() && !IsWsl() && !IsCommandAvailable("wine");

        /// <summary>Returns a user-facing explanation for a tool that could not be launched.</summary>
        public static string ToolAvailabilityError(string name)
        {
            string toolPath = ToolPath(name);
            if (!File.Exists(toolPath))
                return $"{name} was not found in DSPRE's Tools folder.";

            if (toolPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && RequiresWineButUnavailable())
            {
                return $"Wine is required to run {Path.GetFileName(toolPath)} on this platform, "
                    + "but Wine was not found on PATH.";
            }

            return $"Unable to launch {Path.GetFileName(toolPath)}.";
        }

        /// <summary>Logs and displays a tool-launch failure without throwing from the caller.</summary>
        public static void ReportToolUnavailable(string name)
        {
            string message = ToolAvailabilityError(name);
            AppLogger.Error(message);
            AppMessages.Error(message, "Tool unavailable");
        }

        private static bool IsCommandAvailable(string command)
        {
            string path = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrWhiteSpace(path)) return false;

            foreach (string directory in path.Split(Path.PathSeparator))
            {
                string candidate = Path.Combine(directory, command);
                if (File.Exists(candidate)) return true;
            }

            return false;
        }

        private static string ConvertUnixPathsToWine(string arguments)
        {
            if (string.IsNullOrEmpty(arguments)) return arguments;

            var converted = new StringBuilder(arguments.Length);
            int index = 0;
            while (index < arguments.Length)
            {
                if (char.IsWhiteSpace(arguments[index]))
                {
                    converted.Append(arguments[index++]);
                    continue;
                }

                if (arguments[index] == '"')
                {
                    converted.Append(arguments[index++]);
                    int valueStart = index;
                    while (index < arguments.Length && arguments[index] != '"') index++;
                    converted.Append(ToWinePath(arguments.Substring(valueStart, index - valueStart)));
                    if (index < arguments.Length) converted.Append(arguments[index++]);
                    continue;
                }

                int tokenStart = index;
                while (index < arguments.Length && !char.IsWhiteSpace(arguments[index])) index++;
                converted.Append(ToWinePath(arguments.Substring(tokenStart, index - tokenStart)));
            }

            return converted.ToString();
        }

        private static string ToWinePath(string value)
        {
            if (string.IsNullOrEmpty(value) || value[0] != '/') return value;
            return "Z:" + value.Replace('/', '\\');
        }

        public static Process CreateDecompressProcess(string path) {
            Process decompress = new Process();
            decompress.StartInfo.Arguments = @" -d " + '"' + path + '"';
            if (!ConfigureToolStartInfo(decompress.StartInfo, "blz"))
            {
                ReportToolUnavailable("blz");
                decompress.Dispose();
                return null;
            }
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
            unpack.StartInfo.Arguments = "-x " + '"' + ndsFileName + '"'
                + " -9 " + '"' + arm9Path + '"'
                + " -7 " + '"' + arm7Path + '"'
                + " -y9 " + '"' + y9Path + '"'
                + " -y7 " + '"' + y7Path + '"'
                + " -d " + '"' + dataPath + '"'
                + " -y " + '"' + overlayPath + '"'
                + " -t " + '"' + bannerPath + '"'
                + " -h " + '"' + headerPath + '"';
            if (!ConfigureToolStartInfo(unpack.StartInfo, "ndstool"))
            {
                ReportToolUnavailable("ndstool");
                unpack.Dispose();
                return false;
            }

            AppMessages.PumpEvents();

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
                AppMessages.Error("Failed to call ndstool.exe" + Environment.NewLine + "Make sure DSPRE's Tools folder is intact.",
                    "Couldn't unpack ROM");
                return false;
            }

            if (!string.IsNullOrWhiteSpace(errors))
            {
                AppLogger.Error("ndstool returned the following error(s):" + errors);
                AppMessages.Error("An error occurred while unpacking the ROM:" + Environment.NewLine + errors + Environment.NewLine,
                    "Couldn't unpack ROM");
                return false;
            }

            return true;
        }

        public static bool UnpackRomDsRom(string ndsFileName, string workDir)
        {
            Directory.CreateDirectory(workDir);

            Process unpack = new Process();
            unpack.StartInfo.Arguments = $"extract -r \"{ndsFileName}\" -o \"{workDir}\"";
            if (!ConfigureToolStartInfo(unpack.StartInfo, "dsrom"))
            {
                ReportToolUnavailable("dsrom");
                unpack.Dispose();
                return false;
            }
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
                AppMessages.PumpEvents();
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
            catch (System.ComponentModel.Win32Exception ex)
            {
                string message = "Failed to start dsrom: " + ex.Message;
                AppLogger.Error(message);
                AppMessages.Error(message + Environment.NewLine + "Make sure DSPRE's Tools folder is intact.",
                    "Couldn't unpack ROM");
                return false;
            }

            if (unpack.ExitCode != 0)
            {
                AppLogger.Error("dsrom returned the following error(s): " + errors);
                AppMessages.Error("An error occurred while unpacking the ROM:" + Environment.NewLine + errors + Environment.NewLine,
                    "Couldn't unpack ROM");
                return false;
            }

            if (!string.IsNullOrWhiteSpace(errors))
            {
                AppLogger.Info("dsrom stderr: " + errors);
            }

            if (!File.Exists(Path.Combine(workDir, "config.yaml")))
            {
                AppLogger.Error("Validation failed: config.yaml not found after extraction");
                AppMessages.Error("ROM extraction failed: config.yaml not found in output directory.",
                    "Extraction Validation Failed");
                return false;
            }

            if (!File.Exists(Path.Combine(workDir, "arm9", "arm9.bin")))
            {
                AppLogger.Error("Validation failed: arm9/arm9.bin not found after extraction");
                AppMessages.Error("ROM extraction failed: arm9/arm9.bin not found in output directory.",
                    "Extraction Validation Failed");
                return false;
            }

            if (!Directory.Exists(Path.Combine(workDir, "files")))
            {
                AppLogger.Error("Validation failed: files/ directory not found after extraction");
                AppMessages.Error("ROM extraction failed: files/ directory not found in output directory.",
                    "Extraction Validation Failed");
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
                AppMessages.Error("Cannot build ROM: config.yaml not found in the working directory.",
                    "Couldn't repack ROM");
                return false;
            }

            Process repack = new Process();
            repack.StartInfo.Arguments = $"build -c \"{configPath}\" -o \"{ndsFileName}\"";
            if (!ConfigureToolStartInfo(repack.StartInfo, "dsrom"))
            {
                ReportToolUnavailable("dsrom");
                repack.Dispose();
                return false;
            }
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
                AppMessages.PumpEvents();
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
            catch (System.ComponentModel.Win32Exception ex)
            {
                string message = "Failed to start dsrom: " + ex.Message;
                AppLogger.Error(message);
                AppMessages.Error(message + Environment.NewLine + "Make sure DSPRE's Tools folder is intact.",
                    "Couldn't repack ROM");
                return false;
            }

            if (repack.ExitCode != 0)
            {
                AppLogger.Error("dsrom returned the following error(s): " + errors);
                AppMessages.Error("An error occurred while repacking the ROM:" + Environment.NewLine + errors + Environment.NewLine,
                    "Couldn't repack ROM");
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
            repack.StartInfo.Arguments = "-c " + '"' + ndsFileName + '"'
                + " -9 " + '"' + arm9Path + '"'
                + " -7 " + '"' + arm7Path + '"'
                + " -y9 " + '"' + y9Path + '"'
                + " -y7 " + '"' + y7Path + '"'
                + " -d " + '"' + dataPath + '"'
                + " -y " + '"' + overlayPath + '"'
                + " -t " + '"' + bannerPath + '"'
                + " -h " + '"' + headerPath + '"';
            if (!ConfigureToolStartInfo(repack.StartInfo, "ndstool"))
            {
                ReportToolUnavailable("ndstool");
                repack.Dispose();
                return false;
            }

            AppMessages.PumpEvents();
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
                AppMessages.Error("An error occurred while repacking the ROM:" + Environment.NewLine + errors + Environment.NewLine,
                    "Couldn't repack ROM");
                return false;
            }

            return true;

        }
        
        /// <summary>
        /// Determines the type of folder based on the presence of specific configuration files.
        /// </summary>
        /// <remarks>This method checks for the existence of specific files to classify the folder type.
        /// It is important to ensure that the provided path is valid and accessible.</remarks>
        /// <param name="folderPath">The path to the folder to be checked. Must be a valid directory path.</param>
        /// <returns>Returns 0 if the folder contains a 'config.yaml' file, indicating it is a dsrom folder; returns 1 if it
        /// contains a 'header.bin' file, indicating it is a ndstool folder; returns -1 if the folder does not exist or
        /// does not match either type.</returns>
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
                AppMessages.Warning("This project is not in ndstool format.", "Conversion Failed");
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
                AppMessages.Error($"Failed to create backup: {ex.Message}", "Conversion Failed");
                return 0;
            }

            // 3. Build temp ROM using ndstool (the project is still in ndstool format)
            string tempRomPath = Path.Combine(Path.GetDirectoryName(workDir), "temp_conversion.nds");
            string tempDsRomDir = workDir + "_dsrom_temp";

            try
            {
                // Use ndstool directly to build temp ROM
                Process buildTemp = new Process();
                buildTemp.StartInfo.Arguments = "-c \"" + tempRomPath + "\""
                    + " -9 \"" + Path.Combine(workDir, "arm9.bin") + "\""
                    + " -7 \"" + Path.Combine(workDir, "arm7.bin") + "\""
                    + " -y9 \"" + Path.Combine(workDir, "y9.bin") + "\""
                    + " -y7 \"" + Path.Combine(workDir, "y7.bin") + "\""
                    + " -d \"" + Path.Combine(workDir, "data") + "\""
                    + " -y \"" + Path.Combine(workDir, "overlay") + "\""
                    + " -t \"" + Path.Combine(workDir, "banner.bin") + "\""
                    + " -h \"" + Path.Combine(workDir, "header.bin") + "\"";
                if (!ConfigureToolStartInfo(buildTemp.StartInfo, "ndstool"))
                {
                    ReportToolUnavailable("ndstool");
                    buildTemp.Dispose();
                    return 0;
                }
                buildTemp.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                buildTemp.StartInfo.CreateNoWindow = true;
                buildTemp.StartInfo.UseShellExecute = false;
                buildTemp.StartInfo.RedirectStandardError = true;

                AppLogger.Info("Building temp ROM: " + buildTemp.StartInfo.Arguments);
                AppMessages.PumpEvents();
                buildTemp.Start();
                var errorTask = buildTemp.StandardError.ReadToEndAsync();
                buildTemp.WaitForExit();
                string errors = errorTask.Result;

                if (buildTemp.ExitCode != 0)
                {
                    AppLogger.Error("ndstool build failed: " + errors);
                    AppMessages.Error("Failed to build temporary ROM: " + errors, "Conversion Failed");
                    return 0;
                }

                // 4. Extract with ds-rom to get new structure
                if (!UnpackRomDsRom(tempRomPath, tempDsRomDir))
                {
                    AppLogger.Error("ds-rom extraction failed during conversion. This may indicate overlay compression issues.");
                    
                    var result = AppMessages.ConfirmYesNoCancel(
                        "Conversion to ds-rom format failed during ROM extraction.\n\n" +
                        "This is usually caused by corrupted or incompatible overlay compression in the ndstool project.\n\n" +
                        "Would you like to:\n" +
                        "• Yes: Continue loading with ndstool format (no conversion)\n" +
                        "• No: Cancel and restore from backup\n" +
                        "• Cancel: Abort loading",
                        "Conversion Failed");

                    if (Directory.Exists(tempDsRomDir))
                        Directory.Delete(tempDsRomDir, true);
                    if (File.Exists(tempRomPath))
                        File.Delete(tempRomPath);

                    if (result == AppMessages.ConfirmResult.Yes)
                    {
                        AppLogger.Info("User chose to continue with ndstool format.");
                        return 2;
                    }
                    else if (result == AppMessages.ConfirmResult.No)
                    {
                        AppLogger.Info("User chose to restore from backup.");
                        RestoreFromNdstoolBackup(workDir);
                        return 0;
                    }
                    else
                    {

                        AppMessages.Info("Conversion cancelled. Your ndstool project remains unchanged.\n\nBackup available at:\n" + backupPath,
                            "Conversion Cancelled");
                        return 0;
                    }
                }

                // 5. Validate temp output
                if (!File.Exists(Path.Combine(tempDsRomDir, "config.yaml")))
                {
                    AppMessages.Error("Conversion validation failed: config.yaml not found.", "Conversion Failed");
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
                AppMessages.Info("Project converted to ds-rom format successfully.\n\nBackup saved at:\n" + backupPath,
                    "Conversion Complete");
                return 1;
            }
            catch (Exception ex)
            {
                AppLogger.Error("Conversion failed: " + ex.Message);
                AppMessages.Error($"Conversion failed: {ex.Message}\n\nYour backup is at:\n{backupPath}",
                    "Conversion Failed");

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
            AppMessages.Error("Backup file not found:\n" + backupPath, 
                "Restore Failed");
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
            AppMessages.Info("Project restored from backup successfully.", 
                "Restore Complete");
            return true;
        }
        catch (Exception ex)
        {
            AppLogger.Error("Restore failed: " + ex.Message);
            AppMessages.Error($"Restore failed: {ex.Message}", 
                "Restore Failed");
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
            // hg-engine-owned domains are always rebuilt fresh from source (cheap: 0.5-3s each), never
            // read from the packed ROM's NARC — see HgEngineSync.
            IDs = HgEngineSync.SyncOwnedAndReturnRemaining(IDs);
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
            IDs = HgEngineSync.SyncOwnedAndReturnRemaining(IDs);
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

        /// <summary>GDI twin of <see cref="MonIconFallbackHook"/> for the WinForms-only <see cref="GetPokePic"/>
        /// path — installed by the WinForms shell (resx Pokéball); never runs off-Windows.</summary>
        public static Func<Image> MonIconFallbackGdiHook = null;

        /// <param name="paletteIdOverride">When &gt;= 0, render with this icon-palette id (0/1/2) instead of
        /// the one stored in the ARM9 table — used to PREVIEW a palette change before it is saved.</param>
        public static Image GetPokePic(int species, int w, int h, int paletteIdOverride = -1) {
            LoadMonIconParts(species, paletteIdOverride, out ImageBase imageBase, out PaletteBase paletteBase, out SpriteBase spriteBase, out int[] OAMenabled);
            try {
                return spriteBase.Get_Image(imageBase, paletteBase, 0, w, h, false, false, false, true, true, -1, OAMenabled);
            } catch (FormatException) {
                return MonIconFallbackGdiHook?.Invoke();
            }
        }

        /// <summary>
        /// Placeholder mon icon returned by <see cref="GetPokePicRaw"/> when the real icon can't be
        /// decoded. Installed by each shell (WinForms → resx Pokéball via the GDI bridge, Avalonia →
        /// avares asset) so this core code stays free of System.Drawing and WinForms resources.
        /// </summary>
        public static Func<RawImage> MonIconFallbackHook = null;

        /// <summary>Alt-form pseudo-ids (personalExtraFiles list position, vanilla-only) aren't icon indices;
        /// <summary>Alt-form pseudo-ids aren't icon indices; resolves one to the real icon id.</summary>
        public static int ResolveIconId(int id) {
            string[] names = GetPokemonNames();
            // Bad Egg has no icon of its own; shows the plain egg icon.
            if (id == names.Length - 1) id = names.Length - 2;
            int excess = id - names.Length;
            var extras = DSPRE.Resources.PokeDatabase.PersonalData.personalExtraFiles;
            return (excess >= 0 && excess < extras.Length) ? extras[excess].iconId : id;
        }

        /// <summary>Sprite-offset and animation NARCs hold one record per real species; alt-form pseudo-ids share their base species' record.</summary>
        public static int ResolveBaseSpeciesId(int id) {
            int excess = id - GetPokemonNames().Length;
            var extras = DSPRE.Resources.PokeDatabase.PersonalData.personalExtraFiles;
            return (excess >= 0 && excess < extras.Length) ? extras[excess].monId : id;
        }

        /// <summary>GDI-free twin of <see cref="GetPokePic"/> — composes the mon icon straight into a <see cref="RawImage"/> (no System.Drawing).</summary>
        public static RawImage GetPokePicRaw(int species, int w, int h, int paletteIdOverride = -1) {
            LoadMonIconParts(species, paletteIdOverride, out ImageBase imageBase, out PaletteBase paletteBase, out SpriteBase spriteBase, out int[] OAMenabled);
            try {
                return spriteBase.Get_RawImage(imageBase, paletteBase, 0, w, h, trans: true, currOAM: -1, draw_index: OAMenabled);
            } catch (FormatException) {
                return MonIconFallbackHook?.Invoke();
            }
        }

        /// <summary>GDI-free item icon lookup, same NCLR/NCGR/NCER decode as the WinForms <c>GetItemPic</c>
        /// (item icon table entry → palette/sprite/cell files in the itemIcons NARC), returned as a
        /// <see cref="RawImage"/> instead of a System.Drawing Image. Returns null (not a placeholder) on
        /// failure — callers that want a fallback icon supply their own, same as PokemonIconCache.</summary>
        public static RawImage GetItemPicRaw(int itemId, int w, int h) {
            try {
                uint entryOffset = (uint)(RomInfo.itemTableOffset + itemId * 8);
                int itemIconId = ARM9.ReadWordLE(entryOffset + 2);
                int itemPaletteId = ARM9.ReadWordLE(entryOffset + 4);
                string itemIconsDir = gameDirs[DirNames.itemIcons].unpackedDir;

                string paletteFilename = itemPaletteId.ToString("D4");
                var itemPalette = new NCLR(Path.Combine(itemIconsDir, paletteFilename), itemPaletteId, paletteFilename);

                string spriteFilename = itemIconId.ToString("D4");
                ImageBase imageBase = new NCGR(Path.Combine(itemIconsDir, spriteFilename), itemIconId, spriteFilename);

                string ncerFileName = "0001"; // the only NCER in the itemIcons NARC
                SpriteBase spriteBase = new NCER(Path.Combine(itemIconsDir, ncerFileName), 2, ncerFileName);

                return spriteBase.Get_RawImage(imageBase, itemPalette, 0, w, h, trans: true, currOAM: -1, draw_index: null);
            } catch (Exception) {
                return null;
            }
        }

        // Icon NCGRs store nTilesX/nTilesY as the 0xFFFF "unspecified" sentinel, so ImageBase.Read falls
        // back to Actions.Get_Size's generic square/0x100-wide guess — which produces a nonsensical shape
        // (e.g. 256×8 for a real 32×64 icon) totally unrelated to how the game actually reads this data.
        // The OAM-based renderer used everywhere else (SpriteBase.Get_RawImage, via GetPokePicRaw) never
        // hits this bug because it addresses tiles directly by index and ignores ImageBase.Width/Height
        // entirely. So icon-graphic editing must do the same: read/write raw 8×8 tile blocks directly,
        // fixed at the real, universal Gen4 icon width (32px = 4 tiles); height comes from the tile count
        // (normally 64px = 2 stacked 32×32 animation frames, frame 0 on top).
        private const int MonIconTileSize = 8;
        private const int MonIconTilesWide = 4;
        private const int MonIconWidth = MonIconTilesWide * MonIconTileSize;   // 32

        /// <summary>
        /// The icon's raw graphic, decoded directly from tile data at its real dimensions (32px wide;
        /// height from tile count) — the exact bitmap an icon-graphic editor should export/reimport.
        /// Rendered with the currently-assigned (or overridden) palette bank.
        /// </summary>
        public static RawImage GetMonIconGraphicRaw(int species, int paletteIdOverride = -1) {
            LoadMonIconParts(species, paletteIdOverride, out ImageBase imageBase, out PaletteBase paletteBase, out _, out _);
            return DecodeMonIconTiles(imageBase, paletteBase.Palette[0]);
        }

        /// <summary>Same tile walk as <see cref="DecodeMonIconTiles"/>, but returns the raw 0-15 palette
        /// indices instead of resolving them to colors, plus the palette itself. RawImage is always
        /// flattened BGRA (see its own doc comment) so it can't carry an indexed image; callers that want
        /// a genuine indexed PNG (preserving the real 16-color palette table, not just its resolved
        /// pixels) need this instead.</summary>
        public static bool TryGetMonIconIndexedPixels(int species, int paletteIdOverride, out byte[] indices, out int width, out int height, out Color[] palette) {
            LoadMonIconParts(species, paletteIdOverride, out ImageBase imageBase, out PaletteBase paletteBase, out _, out _);
            indices = null; width = 0; height = 0; palette = null;
            if (imageBase.FormatColor != Ekona.Images.ColorFormat.colors16) return false;

            byte[] tiles = imageBase.Tiles;
            int tileBytes = MonIconTileSize * MonIconTileSize * imageBase.BPP / 8;
            int totalTiles = tileBytes > 0 ? tiles.Length / tileBytes : 0;
            int tilesTall = Math.Max(1, totalTiles / MonIconTilesWide);
            height = tilesTall * MonIconTileSize;
            width = MonIconWidth;
            indices = new byte[width * height];

            int pos = 0;
            for (int ty = 0; ty < tilesTall; ty++)
                for (int tx = 0; tx < MonIconTilesWide; tx++)
                    for (int y = 0; y < MonIconTileSize; y++)
                        for (int x = 0; x < MonIconTileSize; x++)
                        {
                            int byteIndex = pos / 2;
                            int idx = 0;
                            if (byteIndex < tiles.Length)
                            {
                                byte packed = tiles[byteIndex];
                                idx = (pos % 2 == 0) ? (packed & 0x0F) : ((packed & 0xF0) >> 4);
                            }
                            pos++;
                            indices[(ty * MonIconTileSize + y) * width + (tx * MonIconTileSize + x)] = (byte)idx;
                        }

            palette = paletteBase.Palette[0];
            return true;
        }

        private static RawImage DecodeMonIconTiles(ImageBase imageBase, Color[] palette) {
            byte[] tiles = imageBase.Tiles;
            int tileBytes = MonIconTileSize * MonIconTileSize * imageBase.BPP / 8;   // 32 bytes/tile @ 4bpp
            int totalTiles = tileBytes > 0 ? tiles.Length / tileBytes : 0;
            int tilesTall = Math.Max(1, totalTiles / MonIconTilesWide);
            int h = tilesTall * MonIconTileSize;

            var raw = new RawImage(MonIconWidth, h);
            int pos = 0;   // increments once per pixel, tile-sequential order (matches how bytes are laid out)
            for (int ty = 0; ty < tilesTall; ty++)
                for (int tx = 0; tx < MonIconTilesWide; tx++)
                    for (int y = 0; y < MonIconTileSize; y++)
                        for (int x = 0; x < MonIconTileSize; x++)
                        {
                            int byteIndex = pos / 2;
                            int idx = 0;
                            if (byteIndex < tiles.Length)
                            {
                                byte packed = tiles[byteIndex];
                                idx = (pos % 2 == 0) ? (packed & 0x0F) : ((packed & 0xF0) >> 4);
                            }
                            pos++;
                            Color c = idx < palette.Length ? palette[idx] : Color.Black;
                            raw.SetPixel(tx * MonIconTileSize + x, ty * MonIconTileSize + y, c.R, c.G, c.B, idx == 0 ? (byte)0 : (byte)255);
                        }
            return raw;
        }

        private static string ValidateMonIconGraphicCore(ImageBase imageBase, RawImage newImage) {
            if (newImage == null || newImage.IsEmpty) return "The image is empty.";
            if (imageBase.FormatColor != Ekona.Images.ColorFormat.colors16)
                return "This icon isn't a 16-color image; graphic editing isn't supported for this format.";

            int tileBytes = MonIconTileSize * MonIconTileSize * imageBase.BPP / 8;
            int totalTiles = tileBytes > 0 ? imageBase.Tiles.Length / tileBytes : 0;
            int tilesTall = Math.Max(1, totalTiles / MonIconTilesWide);
            int expectedH = tilesTall * MonIconTileSize;
            if (newImage.Width != MonIconWidth || newImage.Height != expectedH)
                return $"Size mismatch: this icon is {MonIconWidth}×{expectedH}, the image is {newImage.Width}×{newImage.Height}.";
            return null;
        }

        /// <summary>Checks whether <paramref name="newImage"/> could replace a species' icon graphic
        /// (right pixel dimensions, 16-color format) without writing anything. Returns null if OK, else
        /// a user-facing error string.</summary>
        public static string ValidateMonIconGraphic(int species, RawImage newImage) {
            LoadMonIconParts(species, -1, out ImageBase imageBase, out _, out _, out _);
            return ValidateMonIconGraphicCore(imageBase, newImage);
        }

        /// <summary>
        /// Replaces a species' icon graphic (NCGR tile data) with <paramref name="newImage"/>, which must
        /// be the exact pixel dimensions the icon already is (see <see cref="ValidateMonIconGraphic"/>).
        /// Quantizes onto the icon's currently-assigned (or overridden) 16-color palette bank via
        /// nearest-RGB match; pixels with alpha &lt; 128 map to palette index 0 (the conventional
        /// transparent slot). Writes the NCGR back to disk immediately. Returns null on success, or a
        /// user-facing error string.
        /// </summary>
        public static string SetMonIconGraphic(int species, int paletteIdOverride, RawImage newImage) {
            LoadMonIconParts(species, paletteIdOverride, out ImageBase imageBase, out PaletteBase paletteBase, out _, out _);

            string error = ValidateMonIconGraphicCore(imageBase, newImage);
            if (error != null) return error;

            // LoadMonIconParts already swapped the selected bank into slot 0 for rendering.
            Color[] palette = paletteBase.Palette[0];
            byte[] newTiles = EncodeMonIconTiles(imageBase, palette, newImage);

            // The no-side-effect overload: swaps the tile bytes only, leaving the (already-wrong, but
            // irrelevant here — nothing downstream uses them) auto-detected Width/Height/FormTile alone.
            imageBase.Set_Tiles(newTiles);

            string path = Path.Combine(gameDirs[DirNames.monIcons].unpackedDir, imageBase.FileName);
            imageBase.Write(path, paletteBase);
            return null;
        }

        private static byte[] EncodeMonIconTiles(ImageBase imageBase, Color[] palette, RawImage newImage) {
            byte[] newTiles = new byte[imageBase.Tiles.Length];
            int w = newImage.Width;
            int tilesTall = newImage.Height / MonIconTileSize;

            int pos = 0;   // exact inverse traversal of DecodeMonIconTiles
            for (int ty = 0; ty < tilesTall; ty++)
                for (int tx = 0; tx < MonIconTilesWide; tx++)
                    for (int y = 0; y < MonIconTileSize; y++)
                        for (int x = 0; x < MonIconTileSize; x++)
                        {
                            int px = tx * MonIconTileSize + x, py = ty * MonIconTileSize + y;
                            int i = (py * w + px) * 4;
                            byte b = newImage.Bgra[i], g = newImage.Bgra[i + 1], r = newImage.Bgra[i + 2], a = newImage.Bgra[i + 3];
                            byte idx = a < 128 ? (byte)0 : NearestPaletteIndex(palette, r, g, b);

                            int byteIndex = pos / 2;
                            if (byteIndex < newTiles.Length)
                            {
                                if (pos % 2 == 0) newTiles[byteIndex] = (byte)((newTiles[byteIndex] & 0xF0) | (idx & 0x0F));
                                else newTiles[byteIndex] = (byte)((newTiles[byteIndex] & 0x0F) | ((idx & 0x0F) << 4));
                            }
                            pos++;
                        }
            return newTiles;
        }

        private static byte NearestPaletteIndex(Color[] palette, byte r, byte g, byte b) {
            int best = 0, bestDist = int.MaxValue;
            for (int i = 0; i < palette.Length; i++) {
                int dr = palette[i].R - r, dg = palette[i].G - g, db = palette[i].B - b;
                int dist = dr * dr + dg * dg + db * db;
                if (dist < bestDist) { bestDist = dist; best = i; }
            }
            return (byte)best;
        }

        // Loads the mon-icon NCLR/NCGR/NCER + enabled-OAM list. Shared by GetPokePic (GDI) and GetPokePicRaw.
        private static void LoadMonIconParts(int species, int paletteIdOverride,
            out ImageBase imageBase, out PaletteBase paletteBase, out SpriteBase spriteBase, out int[] OAMenabled) {
            bool fiveDigits = false; // some extreme future proofing
            string filename = "0000";

            try {
                paletteBase = new NCLR(Path.Combine(gameDirs[DirNames.monIcons].unpackedDir, filename), 0, filename);
            } catch (FileNotFoundException) {
                filename += '0';
                paletteBase = new NCLR(Path.Combine(gameDirs[DirNames.monIcons].unpackedDir, filename), 0, filename);
                fiveDigits = true;
            }

            // Palette id: caller override (preview) or the value stored in the ARM9 table.
            int paletteId = 0;
            if (paletteIdOverride >= 0) {
                paletteId = paletteIdOverride;
            } else {
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
            }

            if (paletteId != 0 && paletteId < paletteBase.Palette.Length) {
                paletteBase.Palette[0] = paletteBase.Palette[paletteId]; // update pal 0 to be the new pal
            }

            // grab tiles
            int spriteFileID = species + 7;
            string spriteFilename = spriteFileID.ToString("D" + (fiveDigits ? "5" : "4"));
            imageBase = new NCGR(Path.Combine(gameDirs[DirNames.monIcons].unpackedDir, spriteFilename), spriteFileID, spriteFilename);

            // grab sprite
            int ncerFileId = 2;
            string ncerFileName = ncerFileId.ToString("D" + (fiveDigits ? "5" : "4"));
            spriteBase = new NCER(Path.Combine(gameDirs[DirNames.monIcons].unpackedDir, ncerFileName), 2, ncerFileName);

            // copy this from the trainer
            int bank0OAMcount = spriteBase.Banks[0].oams.Length;
            OAMenabled = new int[bank0OAMcount];
            for (int i = 0; i < OAMenabled.Length; i++) {
                OAMenabled[i] = i;
            }
        }
    }
}
