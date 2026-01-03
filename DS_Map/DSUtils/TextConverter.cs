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
            // Ensure all paths are absolute
            inputFilePath = Path.GetFullPath(inputFilePath);
            outputFilePath = Path.GetFullPath(outputFilePath);
            charMapPath = Path.GetFullPath(charMapPath);
            
            string chatotPath = Path.Combine(Application.StartupPath, "Tools", "chatot.exe");
            
            Process chatot = new Process();
            chatot.StartInfo.FileName = chatotPath;
            chatot.StartInfo.Arguments = $"decode -m \"{charMapPath}\" -b \"{inputFilePath}\" -t \"{outputFilePath}\" --json";
            chatot.StartInfo.UseShellExecute = false;
            chatot.StartInfo.CreateNoWindow = true;
            chatot.StartInfo.RedirectStandardError = true;
            chatot.StartInfo.RedirectStandardOutput = true;
            
            // Set working directory to the directory containing chatot.exe
            chatot.StartInfo.WorkingDirectory = Path.GetDirectoryName(chatotPath);

            string errorOutput = "";

            try
            {
                chatot.Start();
                
                // Only read error stream for logging purposes
                errorOutput = chatot.StandardError.ReadToEnd();
                
                // Wait for the process to finish
                chatot.WaitForExit();
            }
            catch (Exception e)
            {
                MessageBox.Show("An error occurred while converting BIN to JSON:\n" + e.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            if (errorOutput.Length > 0)
            {
                AppLogger.Warn($"chatot.exe reported the following warnings/errors while converting BIN to JSON:\n{errorOutput}");
            }
        }

        public static void JSONToBin(string inputFilePath, string outputFilePath, string charMapPath)
        {
            // Ensure all paths are absolute
            inputFilePath = Path.GetFullPath(inputFilePath);
            outputFilePath = Path.GetFullPath(outputFilePath);
            charMapPath = Path.GetFullPath(charMapPath);
            
            string chatotPath = Path.Combine(Application.StartupPath, "Tools", "chatot.exe");
            
            Process chatot = new Process();
            chatot.StartInfo.FileName = chatotPath;
            chatot.StartInfo.Arguments = $"encode -m \"{charMapPath}\" -t \"{inputFilePath}\" -b \"{outputFilePath}\" --json";
            chatot.StartInfo.UseShellExecute = false;
            chatot.StartInfo.CreateNoWindow = true;
            chatot.StartInfo.RedirectStandardError = true;
            chatot.StartInfo.RedirectStandardOutput = true;
            
            // Set working directory to the directory containing chatot.exe
            chatot.StartInfo.WorkingDirectory = Path.GetDirectoryName(chatotPath);

            // Debug
            string commandText = $"{chatot.StartInfo.FileName} {chatot.StartInfo.Arguments}";
            AppLogger.Debug("Executing command: " + commandText);

            string errorOutput = "";
            
            try
            {
                chatot.Start();
                
                // Only read error stream for logging purposes
                errorOutput = chatot.StandardError.ReadToEnd();
                
                // Wait for the process to finish
                chatot.WaitForExit();
            }
            catch (Exception e)
            {
                MessageBox.Show("An error occurred while converting JSON to BIN:\n" + e.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            
            if (errorOutput.Length > 0)
            {
                AppLogger.Warn($"chatot.exe reported the following warnings/errors while converting JSON to BIN:\n{errorOutput}");
            }
        }

        public static void FolderToJSON(string inputFolderPath, string outputFolderPath, string charMapPath)
        {
            // Ensure all paths are absolute
            inputFolderPath = Path.GetFullPath(inputFolderPath);
            outputFolderPath = Path.GetFullPath(outputFolderPath);
            charMapPath = Path.GetFullPath(charMapPath);

            string chatotPath = Path.Combine(Application.StartupPath, "Tools", "chatot.exe");

            Process chatot = new Process();
            chatot.StartInfo.FileName = chatotPath;
            chatot.StartInfo.Arguments = $"decode -m \"{charMapPath}\" -a \"{inputFolderPath}\" -d \"{outputFolderPath}\" --json --newer";
            chatot.StartInfo.UseShellExecute = false;
            chatot.StartInfo.CreateNoWindow = true;
            chatot.StartInfo.RedirectStandardError = true;
            chatot.StartInfo.RedirectStandardOutput = true;

            // Set working directory to the directory containing chatot.exe
            chatot.StartInfo.WorkingDirectory = Path.GetDirectoryName(chatotPath);

            // Debug
            string commandText = $"{chatot.StartInfo.FileName} {chatot.StartInfo.Arguments}";
            AppLogger.Debug("Executing command: " + commandText);

            string errorOutput = "";

            try
            {
                chatot.Start();

                // Only read error stream for logging purposes
                errorOutput = chatot.StandardError.ReadToEnd();

                // Wait for the process to finish
                chatot.WaitForExit();
            }
            catch (Exception e)
            {
                MessageBox.Show("An error occurred while converting JSON to BIN:\n" + e.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            if (errorOutput.Length > 0)
            {
                AppLogger.Warn($"chatot.exe reported the following warnings/errors while converting JSON to BIN:\n{errorOutput}");
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
