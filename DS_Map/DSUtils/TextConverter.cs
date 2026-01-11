using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Forms;
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
            return Path.Combine(RomInfo.workDir, "expanded", "textArchives");
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
            ChatotWrapperDirectory(outputFolderPath, inputFolderPath, charMapPath, "decode", true, "--newer");
        }

        public static void FolderToBin(string inputFolderPath, string outputFolderPath, string charMapPath)
        {
            ChatotWrapperDirectory(inputFolderPath, outputFolderPath, charMapPath, "encode", true, "--newer");
        }

        private static void ChatotWrapperDirectory(string plainTextPath, string binaryPath, string charMapPath, string mode, bool json, string extraArgs = "")
        {
            ChatotWrapper(plainTextPath, binaryPath, charMapPath, mode, true, json, extraArgs);
        }

        private static void ChatotWrapper(string plainTextPath, string binaryPath, string charMapPath, string mode, bool isDirectory, bool isJson, string extraArgs = "")
        {
            // Ensure all paths are absolute
            plainTextPath = Path.GetFullPath(plainTextPath);
            binaryPath = Path.GetFullPath(binaryPath);
            charMapPath = Path.GetFullPath(charMapPath);

            string chatotPath = Path.Combine(Application.StartupPath, "Tools", "chatot.exe");
            string plainTextArg = "";
            string binaryArg = "";

            if (!File.Exists(chatotPath))
            {
                MessageBox.Show("chatot.exe not found in Tools folder.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
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
            chatot.StartInfo.FileName = chatotPath;
            chatot.StartInfo.Arguments = $"{mode} -m \"{charMapPath}\" {plainTextArg} {binaryArg}";
            chatot.StartInfo.UseShellExecute = false;
            chatot.StartInfo.CreateNoWindow = true;
            chatot.StartInfo.RedirectStandardError = true;
            chatot.StartInfo.RedirectStandardOutput = true;

            if (isJson)
            {
                chatot.StartInfo.Arguments += " --json";
            }

            if (!string.IsNullOrEmpty(extraArgs))
            {
                chatot.StartInfo.Arguments += " " + extraArgs;
            }

            // Set working directory to the directory containing chatot.exe
            chatot.StartInfo.WorkingDirectory = Path.GetDirectoryName(chatotPath);

            // Debug
            string commandText = $"{chatot.StartInfo.FileName} {chatot.StartInfo.Arguments}";
            AppLogger.Debug("Executing command: " + commandText);

            string errorOutput = "";
            string standardOutput = "";

            try
            {
                chatot.Start();

                // Only read error stream for logging purposes
                errorOutput = chatot.StandardError.ReadToEnd();
                standardOutput = chatot.StandardOutput.ReadToEnd();

                // Wait for the process to finish
                chatot.WaitForExit();
            }
            catch (Exception e)
            {
                MessageBox.Show("An error occurred while converting JSON/TXT to BIN:\n" + e.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            if (errorOutput.Length > 0)
            {
                AppLogger.Warn($"chatot.exe reported the following warnings/errors while converting JSON/TXT to BIN:\n{errorOutput}");
            }

            if (standardOutput.Length > 0)
            {
                AppLogger.Info($"chatot.exe output:\n{standardOutput}");
            }
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
