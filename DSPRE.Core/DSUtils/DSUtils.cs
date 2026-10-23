using DSPRE.Editors;
using DSPRE.HgEngine;
using DSPRE.Resources;
using DSPRE.ROMFiles;
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

namespace DSPRE
{
    public static class DSUtils
    {

        public const int ErrOverlayNotfound = -1;
        public const int ErrOverlayAlreadyUncompressed = -2;
        public const int ErrToolUnavailable = -3;

        public static int ReplaceTextEverywhere(string searchString, string replaceString, bool caseSensitive)
        {
            return ReplaceTextEverywhere(new[] { (searchString, replaceString, caseSensitive) });
        }

        // Advancing past each replacement instead of rescanning from 0 avoids looping forever when a
        // replacement text itself matches its own search text, e.g. renaming "PIKABLU" to "Pikablu".
        public static int ReplaceTextEverywhere(IEnumerable<(string searchString, string replaceString, bool caseSensitive)> replacements,
            bool wholeWord = false)
        {
            return ReplaceTextInArchives(replacements, 0, Filesystem.GetTextArchivesCount(), wholeWord: wholeWord).Count;
        }

        /// <summary>
        /// Replaces in archives <paramref name="first"/> up to, not including, <paramref name="last"/>, and
        /// returns the ids it saved. <paramref name="skip"/> leaves an archive untouched.
        /// </summary>
        public static List<int> ReplaceTextInArchives(IEnumerable<(string searchString, string replaceString, bool caseSensitive)> replacements,
            int first, int last, Func<int, bool> skip = null, object sender = null, bool wholeWord = false)
        {
            List<int> edited = new List<int>();
            List<(string searchString, string replaceString, bool caseSensitive)> pairs = replacements.Where(r => !string.IsNullOrEmpty(r.searchString) && r.searchString != r.replaceString).ToList();
            if (pairs.Count == 0)
            {
                return edited;
            }

            last = Math.Min(last, Filesystem.GetTextArchivesCount());
            for (int i = Math.Max(0, first); i < last; i++)
            {
                if (skip != null && skip(i))
                {
                    continue;
                }

                TextArchive archive = new DSPRE.ROMFiles.TextArchive(i);
                bool changed = false;

                for (int j = 0; j < archive.Messages.Count; j++)
                {
                    string text = archive.Messages[j];
                    foreach ((string searchString, string replaceString, bool caseSensitive) pair in pairs)
                    {
                        text = ReplaceInText(text, pair.searchString, pair.replaceString, pair.caseSensitive, ref changed, wholeWord);
                    }
                    archive.Messages[j] = text;
                }

                if (changed)
                {
                    archive.SaveToExpandedDir(i, showSuccessMessage: false, sender: sender);
                    edited.Add(i);
                }
            }

            return edited;
        }

        public static string ReplaceInText(string text, string searchString, string replaceString, bool caseSensitive, ref bool changed,
            bool wholeWord = false)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(searchString))
            {
                return text;
            }
            StringComparison comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            int searchFrom = 0;
            int posFound;
            while ((posFound = text.IndexOf(searchString, searchFrom, comparison)) >= 0)
            {
                if (wholeWord && !IsWordAt(text, posFound, searchString.Length))
                {
                    searchFrom = posFound + 1;
                    continue;
                }
                text = text.Substring(0, posFound) + replaceString + text.Substring(posFound + searchString.Length);
                searchFrom = posFound + (replaceString ?? "").Length;
                changed = true;
            }
            return text;
        }

        // A letter right after a backslash is an escape such as \n, so a name may start straight after it.
        private static bool IsWordAt(string text, int start, int length)
        {
            int end = start + length;
            bool before = start == 0 || !char.IsLetterOrDigit(text[start - 1])
                || (start >= 2 && text[start - 2] == '\\');
            bool after = end >= text.Length || !char.IsLetterOrDigit(text[end]);
            return before && after;
        }

        // Anything longer than the 3-command "give item" template is the shared execution routine, not a pickable entry.
        public static bool IsGroundItemScriptEntry(DSPRE.ROMFiles.ScriptCommandContainer container)
        {
            return container.Commands != null && container.Commands.Count <= 4
                && container.Commands.Count >= 2
                && container.Commands[0].CmdParams != null && container.Commands[0].CmdParams.Count >= 2
                && container.Commands[1].CmdParams != null && container.Commands[1].CmdParams.Count >= 2;
        }

        public static List<(int scriptIndex, int itemId, int quantity)> GetGroundItemScriptEntries(DSPRE.ROMFiles.ScriptFile itemScript)
        {
            List<(int scriptIndex, int itemId, int quantity)> result = new List<(int scriptIndex, int itemId, int quantity)>();

            for (int i = 0; i < itemScript.AllScripts.Count; i++)
            {
                ScriptCommandContainer container = itemScript.AllScripts[i];
                if (!IsGroundItemScriptEntry(container))
                {
                    continue;
                }

                int itemId = BitConverter.ToUInt16(container.Commands[0].CmdParams[1], 0);
                int quantity = BitConverter.ToUInt16(container.Commands[1].CmdParams[1], 0);
                result.Add((i, itemId, quantity));
            }

            return result;
        }

        public const string BackupSuffix = ".backup";

        public static readonly string NDSRomFilter = "NDS File (*.nds)|*.nds";
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

        /// <summary>Writes <paramref name="byteCount"/> bytes of <paramref name="toOutput"/> from <paramref name="firstByte"/> (the rest when null) at <paramref name="writeAt"/>.</summary>
        public static void WriteToFile(string filepath, byte[] toOutput, uint writeAt = 0, int firstByte = 0, int? byteCount = null, FileMode fmode = FileMode.OpenOrCreate)
        {
            int length = byteCount ?? toOutput.Length - firstByte;
            if (HgEngineWriteGuard.Refuses(filepath, writeAt, length)) return;

            using (EasyWriter writer = new EasyWriter(filepath, writeAt, fmode))
            {
                writer.Write(toOutput, firstByte, length);
            }
        }
        /// <summary>
        /// Replaces a whole arm9, overlay or synthetic overlay file. The hg-engine write guard is asked about the span
        /// that changes, and a refusal throws so a caller writing several files can put the others back. Writing goes
        /// through a temporary file, so a failed write never leaves half a file.
        /// </summary>
        public static void WriteWholeFile(string filepath, byte[] bytes)
        {
            byte[] now = File.Exists(filepath) ? File.ReadAllBytes(filepath) : Array.Empty<byte>();
            int first = 0;
            while (first < bytes.Length && first < now.Length && bytes[first] == now[first]) first++;
            if (first == bytes.Length && now.Length == bytes.Length) return;
            int last = bytes.Length - 1;
            if (now.Length == bytes.Length) while (last > first && bytes[last] == now[last]) last--;
            if (HgEngineWriteGuard.Refuses(filepath, first, Math.Max(1, last - first + 1)))
                throw new InvalidOperationException($"hg-engine owns part of {Path.GetFileName(filepath)}, so nothing was written.");

            string temp = filepath + ".dspre.tmp";
            try
            {
                File.WriteAllBytes(temp, bytes);
                File.Move(temp, filepath, true);
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
        }

        // ── Mon party-icon palette table (1 byte per species: 0/1/2) ──────────────────────────────
        // Resolved as GetPokePicRaw does; needs RomInfo.SetMonIconsPalTableAddress() first.
        public static bool TryResolveMonIconPalTable(out string path, out int baseOffset)
        {
            path = null; baseOffset = 0;
            if ((int)(RomInfo.MonIconPalTableAddress - RomInfo.SynthOverlayLoadAddress) >= 0)
            {
                baseOffset = (int)(RomInfo.MonIconPalTableAddress - RomInfo.SynthOverlayLoadAddress);
                path = Filesystem.ExpArmPath;
            }
            else
            {
                baseOffset = (int)(RomInfo.MonIconPalTableAddress - ARM9.Address);
                path = RomInfo.Arm9Path;
            }
            return path != null && baseOffset >= 0;
        }

        /// <summary>Reads the party-icon palette id (0/1/2) for a species, or 0 if unavailable.</summary>
        public static int GetMonIconPaletteId(int species)
        {
            // A linked checkout owns this table, so its source is what the next build will use. Failing
            // that, an hg-engine build keeps it in a/0/2/8 and never writes the vanilla ARM9 one, so
            // reading the member its code reads is the only way to show what the game shows.
            if (RomInfo.IsHGE)
            {
                if (HgEngine.HgEngineIconPalette.TryGetPaletteId(species, out int fromSource)) return fromSource;
                return HgEngine.HgEngineCodeAddons.ReadIconPaletteId(species, out int hgeId)
                    == HgEngine.HgEngineCodeAddons.PaletteStatus.Ok ? hgeId : 0;
            }
            if (!TryResolveMonIconPalTable(out string path, out int baseOff)) return 0;
            using (EasyReader r = new EasyReader(path, baseOff + species)) return r.ReadByte();
        }

        /// <summary>Writes the party-icon palette id (0/1/2) for a species into the resolved table file.</summary>
        public static void SetMonIconPaletteId(int species, byte palId)
        {
            // hg-engine owns its copy of this table, so the vanilla one is not the place to write it.
            if (RomInfo.IsHGE) return;
            if (!TryResolveMonIconPalTable(out string path, out int baseOff)) return;
            WriteToFile(path, new[] { palId }, (uint)(baseOff + species));
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
                    AppLogger.Error("Stream ended");
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
                    AppLogger.Error("Stream ended");
                }
            }
            return buffer;
        }

        /// <summary>Returns every offset in <paramref name="haystack"/> where <paramref name="needle"/> occurs
        /// (naive scan; needles here are short fixed byte patterns, not large enough to need Boyer-Moore).</summary>
        public static List<int> SearchBytes(byte[] haystack, byte[] needle)
        {
            List<int> matches = new List<int>();
            if (haystack == null || needle == null || needle.Length == 0 || needle.Length > haystack.Length)
            {
                return matches;
            }

            for (int i = 0; i <= haystack.Length - needle.Length; i++)
            {
                bool isMatch = true;
                for (int j = 0; j < needle.Length; j++)
                {
                    if (haystack[i + j] != needle[j])
                    {
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
        public static bool IsWsl() =>
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WSL_DISTRO_NAME"));

        /// <summary>WSL interop already resolves Linux paths in an .exe's arguments; a \\wsl.localhost rewrite broke ndstool.</summary>
        private static void ConfigureWslStartInfo(ProcessStartInfo startInfo, string toolPath)
        {
            startInfo.FileName = toolPath;
        }

        [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
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
                return $"{name} wasn't found in DSPRE's Tools folder.";

            if (toolPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && RequiresWineButUnavailable())
            {
                return $"Wine is required to run {Path.GetFileName(toolPath)} on this platform, "
                    + "but Wine wasn't found on PATH.";
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

            StringBuilder converted = new StringBuilder(arguments.Length);
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

        public static Process CreateDecompressProcess(string path)
        {
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
            return Path.Combine(Path.GetDirectoryName(filePath), Path.GetFileNameWithoutExtension(filePath) + RomInfo.FolderSuffix);
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
                AppMessages.Error("ndstool couldn't be started." + Environment.NewLine + "Make sure DSPRE's Tools folder is intact.",
                    "Couldn't unpack ROM");
                return false;
            }

            if (!string.IsNullOrWhiteSpace(errors))
            {
                AppLogger.Error("ndstool failed:" + errors);
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
                Task<string> outputTask = unpack.StandardOutput.ReadToEndAsync();
                Task<string> errorTask = unpack.StandardError.ReadToEndAsync();
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
                string message = "Couldn't start dsrom: " + ex.Message;
                AppLogger.Error(message);
                AppMessages.Error(message + Environment.NewLine + "Make sure DSPRE's Tools folder is intact.",
                    "Couldn't unpack ROM");
                return false;
            }

            if (unpack.ExitCode != 0)
            {
                AppLogger.Error("dsrom failed: " + errors);
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
                AppMessages.Error("The ROM was unpacked but config.yaml is missing.",
                    "Couldn't unpack ROM");
                return false;
            }

            YamlUtils.EnsureConfigPaddingFields(Path.Combine(workDir, "config.yaml"));

            if (!File.Exists(Path.Combine(workDir, "arm9", "arm9.bin")))
            {
                AppLogger.Error("Validation failed: arm9/arm9.bin not found after extraction");
                AppMessages.Error("The ROM was unpacked but arm9/arm9.bin is missing.",
                    "Couldn't unpack ROM");
                return false;
            }

            if (!Directory.Exists(Path.Combine(workDir, "files")))
            {
                AppLogger.Error("Validation failed: files/ directory not found after extraction");
                AppMessages.Error("The ROM was unpacked but the files folder is missing.",
                    "Couldn't unpack ROM");
                return false;
            }

            return true;
        }

        public static bool RepackROMDsRom(string ndsFileName)
        {
            string configPath = Path.Combine(WorkDir, "config.yaml");

            if (!File.Exists(configPath))
            {
                AppLogger.Error("config.yaml not found, can't build with ds-rom");
                AppMessages.Error("The ROM can't be built because config.yaml is missing from the project folder.",
                    "Couldn't repack ROM");
                return false;
            }

            YamlUtils.EnsureConfigPaddingFields(configPath);

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
                Task<string> outputTask = repack.StandardOutput.ReadToEndAsync();
                Task<string> errorTask = repack.StandardError.ReadToEndAsync();
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
                string message = "Couldn't start dsrom: " + ex.Message;
                AppLogger.Error(message);
                AppMessages.Error(message + Environment.NewLine + "Make sure DSPRE's Tools folder is intact.",
                    "Couldn't repack ROM");
                return false;
            }

            if (repack.ExitCode != 0)
            {
                AppLogger.Error("dsrom failed: " + errors);
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

        public static bool RepackROM(string ndsFileName)
        {
            // Route to ds-rom if this is a ds-rom project
            if (RomInfo.IsDsRomProject)
            {
                return RepackROMDsRom(ndsFileName);
            }

            // RomInfo's paths, so an hg-engine checkout's own tree (root/, overarm9.bin) packs too.
            Process repack = new Process();
            repack.StartInfo.Arguments = "-c " + '"' + ndsFileName + '"'
                + " -9 " + '"' + Arm9Path + '"'
                + " -7 " + '"' + Arm7Path + '"'
                + " -y9 " + '"' + OverlayTablePath + '"'
                + " -y7 " + '"' + Y7Path + '"'
                + " -d " + '"' + DataPath + '"'
                + " -y " + '"' + OverlayPath + '"'
                + " -t " + '"' + BannerPath + '"'
                + " -h " + '"' + HeaderPath + '"';
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
        
        /// <summary>0 for a ds-rom project, 1 for ndstool, 2 for an hg-engine tree, -1 for anything else.</summary>
        public static int GetFolderType(string folderPath)
        {
            if (!Directory.Exists(folderPath))
                return -1;

            string configPath = Path.Combine(folderPath, "config.yaml");
            string headerPath = Path.Combine(folderPath, "header.bin");
            if (File.Exists(configPath))
            {
                return 0; // This is a dsrom folder
            }
            else if (File.Exists(headerPath))
            {
                // hg-engine extracts to the same shape, but calls the filesystem root/ instead of data/ and the
                // overlay tables overarm9/overarm7 instead of y9/y7.
                return Directory.Exists(Path.Combine(folderPath, "root")) ? 2 : 1;
            }
            else if (HgEngineDsRomMetaDir(folderPath) != null)
            {
                return 2; // hg-engine's ds-rom bridge: the same base/ tree, header and banner kept in base_dsrom/
            }

            return -1; // Not a valid dsrom or ndstool folder

        }

        /// <summary>
        /// For an hg-engine base/ tree built through ds-rom, the base_dsrom/ project beside it, which holds the header,
        /// banner and overlay metadata the flat tree has no files for. Null for any other folder.
        /// </summary>
        public static string HgEngineDsRomMetaDir(string baseFolder)
        {
            try
            {
                string folder = baseFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (!Directory.Exists(Path.Combine(folder, "root")) || !File.Exists(Path.Combine(folder, "overarm9.bin"))) return null;
                string parent = Directory.GetParent(folder)?.FullName;
                string meta = parent == null ? null : Path.Combine(parent, "base_dsrom");
                return meta != null && File.Exists(Path.Combine(meta, "config.yaml")) ? meta : null;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException)
            {
                return null;
            }
        }

        /// <summary>Converts an ndstool project to ds-rom in place, after zipping a backup beside it. Returns 1 on
        /// success, 2 when it failed and the user kept ndstool, 0 when it failed and nothing changed.</summary>
        public static int ConvertNdstoolToDsRom(string workDir)
        {
            // RomInfo.workDir ends in a separator, which would put the backup and temp files inside the folder being zipped.
            workDir = ProjectFolderPath(workDir);


            if (GetFolderType(workDir) != 1)
            {
                AppMessages.Warning("This project isn't in ndstool format.", "Conversion failed");
                return 0;
            }


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
                AppMessages.Error($"Couldn't create backup: {ex.Message}", "Conversion failed");
                return 0;
            }


            string tempRomPath = Path.Combine(Path.GetDirectoryName(workDir), "temp_conversion.nds");
            string tempDsRomDir = workDir + "_dsrom_temp";

            try
            {

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
                Task<string> errorTask = buildTemp.StandardError.ReadToEndAsync();
                buildTemp.WaitForExit();
                string errors = errorTask.Result;

                if (buildTemp.ExitCode != 0)
                {
                    AppLogger.Error("ndstool build failed: " + errors);
                    AppMessages.Error("The temporary ROM couldn't be built: " + errors, "Conversion failed");
                    return 0;
                }


                if (!UnpackRomDsRom(tempRomPath, tempDsRomDir))
                {
                    AppLogger.Error("ds-rom extraction failed during conversion. This may indicate overlay compression issues.");

                    AppMessages.ConfirmResult result = AppMessages.ConfirmYesNoCancel(
                        "The project couldn't be converted to ds-rom. This usually means its overlay compression is broken.\n\n" +
                        "Yes keeps loading it as ndstool, No restores the backup, and Cancel stops loading.",
                        "Conversion failed");

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
                            "Conversion cancelled");
                        return 0;
                    }
                }


                if (!File.Exists(Path.Combine(tempDsRomDir, "config.yaml")))
                {
                    AppMessages.Error("The converted project has no config.yaml.", "Conversion failed");
                    Directory.Delete(tempDsRomDir, true);
                    File.Delete(tempRomPath);
                    return 0;
                }


                string[] oldFiles = { "arm9.bin", "arm7.bin", "y9.bin", "y7.bin", "banner.bin", "header.bin" };
                string[] oldDirs = { "data", "overlay" };

                foreach (string f in oldFiles)
                {
                    string path = Path.Combine(workDir, f);
                    if (File.Exists(path)) File.Delete(path);
                }
                foreach (string d in oldDirs)
                {
                    string path = Path.Combine(workDir, d);
                    if (Directory.Exists(path)) Directory.Delete(path, true);
                }


                foreach (string entry in Directory.GetFileSystemEntries(tempDsRomDir))
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


                Directory.Delete(tempDsRomDir, true);
                File.Delete(tempRomPath);

                AppLogger.Info("Successfully converted project to ds-rom format.");
                AppMessages.Info("The project is now in ds-rom format.\n\nThe backup is at:\n" + backupPath,
                    "Project converted");
                return 1;
            }
            catch (Exception ex)
            {
                AppLogger.Error("Conversion failed: " + ex.Message);
                AppMessages.Error($"Conversion failed: {ex.Message}\n\nYour backup is at:\n{backupPath}",
                    "Conversion failed");


                if (Directory.Exists(tempDsRomDir))
                    Directory.Delete(tempDsRomDir, true);
                if (File.Exists(tempRomPath))
                    File.Delete(tempRomPath);

            return 0;
        }
    }

    public static bool RestoreFromNdstoolBackup(string workDir)
    {
        workDir = ProjectFolderPath(workDir);
        string backupPath = workDir + ".ndstool_backup.zip";
        
        if (!File.Exists(backupPath))
        {
            AppMessages.Error("The backup is missing:\n" + backupPath, 
                "Restore failed");
            return false;
        }
        
        try
        {
            // Delete current contents, never the backup being restored
            foreach (string file in Directory.GetFiles(workDir))
            {
                if (string.Equals(Path.GetFullPath(file), Path.GetFullPath(backupPath), StringComparison.OrdinalIgnoreCase)) continue;
                File.Delete(file);
            }
            foreach (string dir in Directory.GetDirectories(workDir))
            {
                Directory.Delete(dir, true);
            }
            

            ZipFile.ExtractToDirectory(backupPath, workDir);
            
            AppLogger.Info("Successfully restored from backup: " + backupPath);
            AppMessages.Info("The project was restored from the backup.", 
                "Backup restored");
            return true;
        }
        catch (Exception ex)
        {
            AppLogger.Error("Restore failed: " + ex.Message);
            AppMessages.Error($"Restore failed: {ex.Message}", 
                "Restore failed");
            return false;
        }
    }

    private static string ProjectFolderPath(string workDir) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(workDir));

    public static byte[] StringToByteArray(String hex)
    {
            int NumberChars = hex.Length;
            byte[] bytes = new byte[NumberChars / 2];
            for (int i = 0; i < NumberChars; i += 2)
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            return bytes;
        }
        public static byte[] HexStringToByteArray(string hexString)
        {
            // Takes hex such as "FC B5 05 48 C0 46", spaces allowed.
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

        // A second caller waits for an extraction in progress instead of reading a half-filled folder.
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<DirNames, object> UnpackLocks =
            new System.Collections.Concurrent.ConcurrentDictionary<DirNames, object>();

        /// <summary>The lock an archive's extraction holds, for readers that must not see a half-written folder.</summary>
        internal static object UnpackLockFor(DirNames id) => UnpackLocks.GetOrAdd(id, _ => new object());

        public static void TryUnpackNarcs(List<DirNames> IDs)
        {
            if (GameDirs == null || GameDirs.Count == 0)
            {
                return;
            }
            // hg-engine-owned domains are always rebuilt fresh from source (cheap: 0.5-3s each), never
            // read from the packed ROM's NARC, see HgEngineSync.
            IDs = HgEngineSync.SyncOwnedAndReturnRemaining(IDs);
            // An existing folder wins below, so an archive changed outside DSPRE is settled first.
            NarcSync.SettleIfFree(IDs);
            Parallel.ForEach(IDs, id => {
                lock (UnpackLocks.GetOrAdd(id, _ => new object()))
                if (GameDirs.TryGetValue(id, out (string packedPath, string unpackedPath) paths))
                {
                    DirectoryInfo di = new DirectoryInfo(paths.unpackedPath);

                    if (di.Exists && di.GetFiles().Length > 0)
                    {
                        return;
                    }

                    if (!File.Exists(paths.packedPath))
                    {
                        if (IsOptionalNarc(id, GameFamily))
                        {
                            AppLogger.Debug($"Skipping optional NARC at {paths.packedPath}; it isn't present in this project.");
                            return;
                        }
                        AppLogger.Error($"Tried to unpack NARC at {paths.packedPath}, but file doesn't exist.");
                        return;
                    }

                    Narc opened = Narc.Open(paths.packedPath) ?? throw new NullReferenceException();
                    // Unpacked beside the folder and moved into place whole, so a folder with files in it is always
                    // complete: an interrupted unpack leaves only the side folder, which the next attempt replaces.
                    string unpacking = paths.unpackedPath + ".unpacking";
                    if (Directory.Exists(unpacking)) Directory.Delete(unpacking, true);
                    opened.ExtractToFolder(unpacking);
                    if (di.Exists) Directory.Delete(paths.unpackedPath, true);
                    Directory.Move(unpacking, paths.unpackedPath);
                    // The side folder is not the archive's own, so the extract above left no fingerprint.
                    NarcSync.NoteExtracted(paths.packedPath, paths.unpackedPath);
                }
            });
        }
        public static void ForceUnpackNarcs(List<DirNames> IDs)
        {
            IDs = HgEngineSync.SyncOwnedAndReturnRemaining(IDs);
            Parallel.ForEach(IDs, id => {
                lock (UnpackLocks.GetOrAdd(id, _ => new object()))
                if (GameDirs.TryGetValue(id, out (string packedPath, string unpackedPath) paths))
                {

                    if (!File.Exists(paths.packedPath))
                    {
                        if (IsOptionalNarc(id, GameFamily))
                        {
                            AppLogger.Debug($"Skipping optional NARC at {paths.packedPath}; it isn't present in this project.");
                            return;
                        }
                        AppLogger.Error($"Tried to unpack NARC at {paths.packedPath}, but file doesn't exist.");
                        return;
                    }

                    Narc opened = Narc.Open(paths.packedPath);

                    if (opened is null)
                    {
                        throw new NullReferenceException();
                    }

                    opened.ExtractToFolder(paths.unpackedPath);
                }
            });
        }

        internal static bool IsOptionalNarc(DirNames id, GameFamilies family) =>
            id == DirNames.EggMoves && family != GameFamilies.HGSS;

        /// <summary>
        /// Placeholder mon icon returned by <see cref="GetPokePicRaw"/> when the real icon can't be decoded. The app
        /// installs it (an avares asset) so this core code stays free of UI resources.
        /// </summary>
        public static Func<RawImage> MonIconFallbackHook = null;

        /// <summary>Alt-form pseudo-ids aren't icon indices; resolves one to the real icon id.</summary>
        public static int ResolveIconId(int id)
        {
            string[] names = GetPokemonNames();
            // Bad Egg has no icon, so it shows the plain egg icon.
            if (id == names.Length - 1) id = names.Length - 2;
            int excess = id - names.Length;
            PokeDatabase.PersonalData.PersonalExtraFiles[] extras = DSPRE.Resources.PokeDatabase.PersonalData.ExtraFiles;
            return (excess >= 0 && excess < extras.Length) ? extras[excess].IconId : id;
        }

        /// <summary>Sprite-offset and animation NARCs hold one record per real species; alt-form pseudo-ids share their base species' record.</summary>
        public static int ResolveBaseSpeciesId(int id)
        {
            int excess = id - GetPokemonNames().Length;
            PokeDatabase.PersonalData.PersonalExtraFiles[] extras = DSPRE.Resources.PokeDatabase.PersonalData.ExtraFiles;
            return (excess >= 0 && excess < extras.Length) ? extras[excess].MonId : id;
        }

        /// <summary>
        /// The party icon a form shows, as the game picks it: the extra icons after the two eggs, one run per species
        /// with form icons, in the same order in DP, Pt and HGSS (DP stops after Gastrodon). The species itself when
        /// the form has no icon or the ROM lacks it.
        /// </summary>
        public static int MonIconIndex(int species, int form, out int paletteIndex)
        {
            paletteIndex = species;
            if (form <= 0) return species;
            (int first, int forms) = species switch {
                386 => (496, 3),    // Deoxys: Attack, Defense, Speed
                201 => (500, 27),   // Unown: B to ?
                412 => (527, 2),    // Burmy: Sandy, Trash
                413 => (529, 2),    // Wormadam: Sandy, Trash
                422 => (531, 1),    // Shellos: East Sea
                423 => (532, 1),    // Gastrodon: East Sea
                487 => (533, 1),    // Giratina: Origin
                492 => (534, 1),    // Shaymin: Sky
                479 => (535, 5),    // Rotom: Heat, Wash, Frost, Fan, Mow
                _ => (-1, 0),
            };
            if (first < 0 || form > forms) return species;
            int index = first + form - 1;
            string dir = GameDirs[DirNames.MonIcons].unpackedDir;
            if (!File.Exists(Path.Combine(dir, (index + 7).ToString("D4"))) && !File.Exists(Path.Combine(dir, (index + 7).ToString("D5"))))
                return species;
            // The game reads Unown's palette one slot back, from the unused Unown A icon's; they all match anyway.
            paletteIndex = species == 201 ? index - 1 : index;
            return index;
        }

        /// <summary>A species' party icon in the given form.</summary>
        public static RawImage GetFormPokePicRaw(int species, int form, int w, int h)
        {
            int index = MonIconIndex(species, form, out int palette);
            return index == species ? GetPokePicRaw(species, w, h) : GetPokePicRaw(index, w, h, GetMonIconPaletteId(palette));
        }

        /// <summary>The mon icon composed into a <see cref="RawImage"/>.</summary>
        public static RawImage GetPokePicRaw(int species, int w, int h, int paletteIdOverride = -1)
        {
            MonIcon icon = LoadMonIconParts(species, paletteIdOverride);
            if (icon.Header == null) return MonIconFallbackHook?.Invoke();
            return CellRenderer.Draw(icon.Cells, 0, icon.Drawing, icon.Colours, w, h);
        }

        /// <summary>An item's icon, or null when it can't be decoded; callers supply their own fallback.</summary>
        public static RawImage GetItemPicRaw(int itemId, int w, int h)
        {
            try
            {
                if (!DSPRE.ROMFiles.ItemTable.Exists(itemId)) return null;
                ItemNarcTableEntry entry = DSPRE.ROMFiles.ItemTable.Read(itemId);   // PlatPatches' expanded items resolve through its overflow table
                int itemIconId = (int)entry.ItemIcon;
                int itemPaletteId = (int)entry.ItemPalette;
                string itemIconsDir = GameDirs[DirNames.ItemIcons].unpackedDir;

                ushort[] colours = NitroBgCodec.ReadColours(File.ReadAllBytes(Path.Combine(itemIconsDir, itemPaletteId.ToString("D4"))));
                byte[] drawing = File.ReadAllBytes(Path.Combine(itemIconsDir, itemIconId.ToString("D4")));
                NcerFile cells = NcerFile.Read(File.ReadAllBytes(Path.Combine(itemIconsDir, "0001")));   // the only NCER in the itemIcons NARC
                if (cells == null || NitroBgCodec.ReadDrawingHeader(drawing) == null) return null;

                return CellRenderer.Draw(cells, 0, drawing, colours, w, h);
            }
            catch (Exception ex)
            {
                AppLogger.WarnOnce("DSUtils.GetItemPicRaw", ex);
                return null;
            }
        }

        // Icon NCGRs leave their size unset (0xFFFF), so icons are read 32 px wide with the height from the tile count.
        private const int MonIconTileSize = 8;
        private const int MonIconTilesWide = 4;
        private const int MonIconWidth = MonIconTilesWide * MonIconTileSize;   // 32

        /// <summary>
        /// The icon's raw graphic, decoded directly from tile data at its real dimensions (32px wide;
        /// height from tile count), the exact bitmap an icon-graphic editor should export/reimport.
        /// Rendered with the currently-assigned (or overridden) palette bank.
        /// </summary>
        public static RawImage GetMonIconGraphicRaw(int species, int paletteIdOverride = -1)
        {
            MonIcon icon = LoadMonIconParts(species, paletteIdOverride);
            return DecodeMonIconTiles(icon.Tiles(), icon.FirstRow());
        }

        /// <summary><see cref="DecodeMonIconTiles"/> as palette indices plus the palette, for exporting an indexed PNG.</summary>
        public static bool TryGetMonIconIndexedPixels(int species, int paletteIdOverride, out byte[] indices, out int width, out int height, out Color[] palette)
        {
            MonIcon icon = LoadMonIconParts(species, paletteIdOverride);
            indices = null; width = 0; height = 0; palette = null;
            if (!icon.SixteenColour) return false;

            byte[] tiles = icon.Tiles();
            int tileBytes = MonIconTileSize * MonIconTileSize * 4 / 8;
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

            palette = icon.FirstRow();
            return true;
        }

        private static RawImage DecodeMonIconTiles(byte[] tiles, Color[] palette)
        {
            int tileBytes = MonIconTileSize * MonIconTileSize * 4 / 8;   // 32 bytes/tile @ 4bpp
            int totalTiles = tileBytes > 0 ? tiles.Length / tileBytes : 0;
            int tilesTall = Math.Max(1, totalTiles / MonIconTilesWide);
            int h = tilesTall * MonIconTileSize;

            RawImage raw = new RawImage(MonIconWidth, h);
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

        private static string ValidateMonIconGraphicCore(MonIcon icon, RawImage newImage)
        {
            if (newImage == null || newImage.IsEmpty) return "The image is empty.";
            if (!icon.SixteenColour)
                return "Only 16-colour icons can be replaced.";

            int tileBytes = MonIconTileSize * MonIconTileSize * 4 / 8;
            int totalTiles = icon.Header.Value.DataSize / tileBytes;
            int tilesTall = Math.Max(1, totalTiles / MonIconTilesWide);
            int expectedH = tilesTall * MonIconTileSize;
            if (newImage.Width != MonIconWidth || newImage.Height != expectedH)
                return $"This icon is {MonIconWidth}×{expectedH} but the image is {newImage.Width}×{newImage.Height}.";
            return null;
        }

        /// <summary>Checks whether <paramref name="newImage"/> could replace a species' icon graphic
        /// (right pixel dimensions, 16-color format) without writing anything. Returns null if OK, else
        /// a user-facing error string.</summary>
        public static string ValidateMonIconGraphic(int species, RawImage newImage)
        {
            return ValidateMonIconGraphicCore(LoadMonIconParts(species, -1), newImage);
        }

        /// <summary>
        /// Writes a new icon graphic matched to the icon's palette (alpha under 128 is transparent) straight to disk.
        /// Null on success, otherwise the error.
        /// </summary>
        public static string SetMonIconGraphic(int species, int paletteIdOverride, RawImage newImage)
        {
            MonIcon icon = LoadMonIconParts(species, paletteIdOverride);

            string error = ValidateMonIconGraphicCore(icon, newImage);
            if (error != null) return error;

            // LoadMonIconParts already swapped the selected bank into slot 0 for rendering.
            byte[] newTiles = EncodeMonIconTiles(icon.Header.Value.DataSize, icon.FirstRow(), newImage);

            // Only the pixel bytes change; the rest of the file stays as it was.
            byte[] file = (byte[])icon.Drawing.Clone();
            Array.Copy(newTiles, 0, file, icon.Header.Value.DataAt, newTiles.Length);
            ArchiveMembers.Write(icon.DrawingPath, file);
            return null;
        }

        private static byte[] EncodeMonIconTiles(int length, Color[] palette, RawImage newImage)
        {
            byte[] newTiles = new byte[length];
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

        private static byte NearestPaletteIndex(Color[] palette, byte r, byte g, byte b)
        {
            int best = 0, bestDist = int.MaxValue;
            for (int i = 0; i < palette.Length; i++)
            {
                int dr = palette[i].R - r, dg = palette[i].G - g, db = palette[i].B - b;
                int dist = dr * dr + dg * dg + db * db;
                if (dist < bestDist) { bestDist = dist; best = i; }
            }
            return (byte)best;
        }

        /// <summary>A party icon's drawing, colours with the chosen row moved to the front, and the shared cells.</summary>
        private sealed class MonIcon
        {
            public byte[] Drawing;
            public string DrawingPath;
            public NitroBgCodec.DrawingHeader? Header;
            public ushort[] Colours;
            public NcerFile Cells;

            public bool SixteenColour => Header is NitroBgCodec.DrawingHeader h && !h.EightBit;

            public byte[] Tiles() => Header is NitroBgCodec.DrawingHeader h ? Drawing.AsSpan(h.DataAt, h.DataSize).ToArray() : Array.Empty<byte>();

            public Color[] FirstRow()
            {
                Color[] row = new Color[Math.Min(16, Colours.Length)];
                for (int i = 0; i < row.Length; i++)
                {
                    (byte r, byte g, byte b) = NitroBgCodec.Colour(Colours[i]);
                    row[i] = Color.FromArgb(r, g, b);
                }
                return row;
            }
        }

        // Loads the mon-icon palette, drawing and cells for GetPokePicRaw.
        private static MonIcon LoadMonIconParts(int species, int paletteIdOverride)
        {
            string dir = GameDirs[DirNames.MonIcons].unpackedDir;
            string digits = File.Exists(Path.Combine(dir, "0000")) || !File.Exists(Path.Combine(dir, "00000")) ? "D4" : "D5";   // an archive of 10000 or more members uses five digits
            ushort[] colours = NitroBgCodec.ReadColours(File.ReadAllBytes(Path.Combine(dir, 0.ToString(digits))));

            // Palette id: caller override (preview) or the value stored in the ARM9 table.
            int paletteId = paletteIdOverride >= 0 ? paletteIdOverride : GetMonIconPaletteId(species);
            if (paletteId != 0 && paletteId < colours.Length / 16)
            {
                Array.Copy(colours, paletteId * 16, colours, 0, 16); // update pal 0 to be the new pal
            }

            string drawingPath = Path.Combine(dir, (species + 7).ToString(digits));
            byte[] drawing = File.ReadAllBytes(drawingPath);
            return new MonIcon {
                Drawing = drawing,
                DrawingPath = drawingPath,
                Header = NitroBgCodec.ReadDrawingHeader(drawing),
                Colours = colours,
                Cells = NcerFile.Read(File.ReadAllBytes(Path.Combine(dir, 2.ToString(digits)))),
            };
        }
    }
}
