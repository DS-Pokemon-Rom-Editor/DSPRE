using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using DSPRE.CharMaps;

namespace DSPRE
{
    internal class TextConverter
    {   
        public static readonly Dictionary<RomInfo.GameLanguages, string> langCodes = new Dictionary<RomInfo.GameLanguages, string>
        {
            { RomInfo.GameLanguages.English, "en_US" },
            { RomInfo.GameLanguages.French, "fr_FR" },
            { RomInfo.GameLanguages.Italian, "it_IT" },
            { RomInfo.GameLanguages.German, "de_DE" },
            { RomInfo.GameLanguages.Spanish, "es_ES" },
            { RomInfo.GameLanguages.Japanese, "ja_JP" },
        };

        public static string GetExpandedFolderPath()
        {
            // ToDo: Don't hardcode "expanded" and "textArchives" folders
            return Path.Combine(RomInfo.dspreDir, "expanded", "textArchives");
        }

        public static void BinToJSON(string inputFilePath, string outputFilePath, string charMapPath)
        {
            ChatotWrapper(outputFilePath, inputFilePath, charMapPath, "decode", false, true);
        }

        public static void JSONToBin(string inputFilePath, string outputFilePath, string charMapPath)
        {
            ChatotWrapper(inputFilePath, outputFilePath, charMapPath, "encode", false, true);
        }

        public static void BinToPlainText(string inputFilePath, string outputFilePath, string charMapPath)
        {
            ChatotWrapper(outputFilePath, inputFilePath, charMapPath, "decode", false, false);
        }

        public static void PlainTextToBin(string inputFilePath, string outputFilePath, string charMapPath)
        {
            ChatotWrapper(inputFilePath, outputFilePath, charMapPath, "encode", false, false);
        }

        public static void FolderToJSON(string inputFolderPath, string outputFolderPath, string charMapPath)
        {
            ChatotWrapperDirectory(outputFolderPath, inputFolderPath, charMapPath, "decode", true, extraArgs: "--newer");
        }

        public static void FolderToBin(string inputFolderPath, string outputFolderPath, string charMapPath)
            => FolderToBin(inputFolderPath, outputFolderPath, charMapPath, out _);

        /// <summary>
        /// Encodes every JSON newer than its archive. False when chatot failed or replaced a control code
        /// it did not know with a null code; <paramref name="error"/> then names the archive, line and code.
        /// </summary>
        public static bool FolderToBin(string inputFolderPath, string outputFolderPath, string charMapPath, out string error)
        {
            error = null;
            List<string> candidates = NewerJsonFiles(inputFolderPath, outputFolderPath);
            ChatotResult result = ChatotWrapperDirectory(inputFolderPath, outputFolderPath, charMapPath, "encode", true, extraArgs: "--newer");
            if (result == null) { error = "chatot could not be run."; return false; }

            var unknown = UnknownCodeWarnings(result.Stderr);
            if (unknown.Count == 0 && result.ExitCode == 0) return true;

            var problems = new List<string>();
            var offending = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string token in unknown)
            {
                var where = FindToken(candidates, token);
                if (where.Count == 0) problems.Add($"unknown control code {token}");
                foreach (var (path, line) in where)
                {
                    offending.Add(path);
                    problems.Add($"archive {Path.GetFileNameWithoutExtension(path)}, line {line} (0x{line:X}): unknown control code {token}");
                }
            }
            if (result.ExitCode != 0)
                problems.Add($"chatot exited with code {result.ExitCode}: {result.Stderr?.Trim()}");

            // chatot already wrote a null code into these archives; newer JSON makes the next build retry them.
            foreach (string path in offending)
            {
                try { File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(2)); } catch { }
            }

            error = string.Join("\n", problems.Distinct());
            AppLogger.Error("Text archives could not be encoded:\n" + error);
            return false;
        }

        private sealed class ChatotResult
        {
            public int ExitCode;
            public string Stderr;
        }

        private static List<string> NewerJsonFiles(string jsonFolder, string binFolder)
        {
            var found = new List<string>();
            if (!Directory.Exists(jsonFolder)) return found;
            foreach (string json in Directory.GetFiles(jsonFolder, "*.json"))
            {
                string bin = Path.Combine(binFolder, Path.GetFileNameWithoutExtension(json));
                if (!File.Exists(bin) || File.GetLastWriteTimeUtc(json) >= File.GetLastWriteTimeUtc(bin))
                    found.Add(json);
            }
            return found;
        }

        // chatot: "Warning: invalid command format 'X'. Inserting null code." and the like.
        private static readonly System.Text.RegularExpressions.Regex UnknownCodeWarning =
            new System.Text.RegularExpressions.Regex(@"^\s*Warning:.*?'(?<token>.+)'.*(null code|code 0x0000)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        private static List<string> UnknownCodeWarnings(string stderr)
        {
            var tokens = new List<string>();
            if (string.IsNullOrEmpty(stderr)) return tokens;
            foreach (string line in stderr.Split('\n'))
            {
                var m = UnknownCodeWarning.Match(line.TrimEnd('\r'));
                if (m.Success && !tokens.Contains(m.Groups["token"].Value)) tokens.Add(m.Groups["token"].Value);
            }
            return tokens;
        }

        /// <summary>Which message of which archive holds <paramref name="token"/>, as 0-based lines.</summary>
        private static List<(string path, int line)> FindToken(List<string> jsonFiles, string token)
        {
            var hits = new List<(string, int)>();
            // The code sits in braces, so a word that happens to share its name in plain text is not it.
            string code = "{" + token;
            string lang = langCodes.TryGetValue(RomInfo.gameLanguage, out string l) ? l : "en_US";
            foreach (string path in jsonFiles)
            {
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path, Encoding.UTF8));
                    if (!doc.RootElement.TryGetProperty("messages", out var messages)) continue;
                    int index = 0;
                    foreach (var message in messages.EnumerateArray())
                    {
                        if (message.TryGetProperty(lang, out var text) || message.TryGetProperty("en_US", out text))
                        {
                            string value = text.ValueKind == System.Text.Json.JsonValueKind.Array
                                ? string.Concat(text.EnumerateArray().Select(e => e.GetString()))
                                : text.GetString();
                            if (value != null && value.Contains(code)) hits.Add((path, index));
                        }
                        index++;
                    }
                }
                catch { }
            }
            return hits;
        }

        private static ChatotResult ChatotWrapperDirectory(string plainTextPath, string binaryPath, string charMapPath,
            string mode, bool json, string lang = "", string extraArgs = "")
        {
            return ChatotWrapper(plainTextPath, binaryPath, charMapPath, mode, true, json, "", extraArgs);
        }

        private static ChatotResult ChatotWrapper(string plainTextPath, string binaryPath, string charMapPath,
            string mode, bool isDirectory, bool isJson, string lang = "", string extraArgs = "")
        {
            // Ensure all paths are absolute
            plainTextPath = Path.GetFullPath(plainTextPath);
            binaryPath = Path.GetFullPath(binaryPath);
            charMapPath = Path.GetFullPath(charMapPath);

            string chatotPath = DSUtils.ToolPath("chatot");
            string plainTextArg = "";
            string binaryArg = "";

            if (!File.Exists(chatotPath))
            {
                AppMessages.Error($"{Path.GetFileName(chatotPath)} not found in Tools folder.", "Error");
                return null;
            }

            if (isDirectory)
            {
                plainTextArg = $"-d \"{plainTextPath}\"";
                binaryArg = $"-a \"{binaryPath}\"";
            }
            else
            {
                plainTextArg = $"-t \"{plainTextPath}\"";
                binaryArg = $"-b \"{binaryPath}\"";
            }

            Process chatot = new Process();
            chatot.StartInfo.Arguments = $"{mode} -m \"{charMapPath}\" {plainTextArg} {binaryArg}";
            chatot.StartInfo.UseShellExecute = false;
            chatot.StartInfo.CreateNoWindow = true;
            chatot.StartInfo.RedirectStandardError = true;
            chatot.StartInfo.RedirectStandardOutput = true;
            chatot.StartInfo.StandardErrorEncoding = Encoding.UTF8;
            chatot.StartInfo.StandardOutputEncoding = Encoding.UTF8;

            if (isJson)
            {
                chatot.StartInfo.Arguments += " --json";
                
                // If no language was specified read from the langcodes dictionary
                if (string.IsNullOrEmpty(lang))
                {
                    lang = langCodes[RomInfo.gameLanguage];
                }

                chatot.StartInfo.Arguments += $" --lang {lang}";
            }

            if (!string.IsNullOrEmpty(extraArgs))
            {
                chatot.StartInfo.Arguments += " " + extraArgs;
            }

            if (!DSUtils.ConfigureToolStartInfo(chatot.StartInfo, "chatot"))
            {
                chatot.Dispose();
                DSUtils.ReportToolUnavailable("chatot");
                return null;
            }

            // Set working directory to the directory containing the chatot tool
            chatot.StartInfo.WorkingDirectory = Path.GetDirectoryName(chatotPath);

            // Debug
            string commandText = $"{chatot.StartInfo.FileName} {chatot.StartInfo.Arguments}";
            AppLogger.Debug("Executing command: " + commandText);

            string errorOutput = "";
            string standardOutput = "";
            int exitCode = -1;

            try
            {
                chatot.Start();

                // Both streams at once: reading one to the end first can deadlock on a full other pipe.
                var stderrTask = chatot.StandardError.ReadToEndAsync();
                standardOutput = chatot.StandardOutput.ReadToEnd();
                errorOutput = stderrTask.Result;

                // Wait for the process to finish
                chatot.WaitForExit();
                exitCode = chatot.ExitCode;
            }
            catch (Exception e)
            {
                AppMessages.Error("An error occurred while converting JSON/TXT to BIN:\n" + e.Message, "Error");
                errorOutput += e.Message;
            }
            finally
            {
                chatot.Dispose();
            }

            if (errorOutput.Length > 0)
            {
                AppLogger.Warn($"chatot.exe reported the following warnings/errors while converting JSON/TXT to BIN:\n{errorOutput}");
            }

            if (standardOutput.Length > 0)
            {
                AppLogger.Info($"chatot.exe output:\n{standardOutput}");
            }

            return new ChatotResult { ExitCode = exitCode, Stderr = errorOutput };
        }

        public static string GetSimpleTrainerName(string message)
        {
            if (string.IsNullOrEmpty(message))
                return "";

            if (message.StartsWith("{TRAINER_NAME:") && message.EndsWith("}"))
            {
                return message.Substring(14, message.Length - 15);
            }

            return message;
        }

        public static string ReplaceTrainerName(string message, string simpleName)
        {
            if (string.IsNullOrEmpty(message))
                return message;

            if (message.StartsWith("{TRAINER_NAME:") && message.EndsWith("}"))
            {
                return "{TRAINER_NAME:" + simpleName + "}";
            }

            return message;
        }

    }
}
