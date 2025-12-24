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
        public static string GetExpandedFolderPath()
        {
            // ToDo: Don't hardcode "expanded" and "textArchives" folders
            return Path.Combine(RomInfo.workDir, "expanded", "textArchives");
        }

        public static void BinToJSON(string inputFilePath, string outputFilePath, string charMapPath)
        {
            Process chatot = new Process();
            chatot.StartInfo.FileName = Path.Combine(Application.StartupPath, "Tools", "chatot.exe");
            chatot.StartInfo.Arguments = $"decode -c \"{charMapPath}\" -b \"{inputFilePath}\" -t \"{outputFilePath}\" --json";
            chatot.StartInfo.UseShellExecute = false;
            chatot.StartInfo.CreateNoWindow = true;
            chatot.StartInfo.RedirectStandardError = true;

            string errorOutput = "";

            try
            {
                chatot.Start();
                chatot.WaitForExit();
                errorOutput = chatot.StandardError.ReadToEnd();
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
            Process chatot = new Process();
            chatot.StartInfo.FileName = Path.Combine(Application.StartupPath, "Tools", "chatot.exe");
            chatot.StartInfo.Arguments = $"encode -c \"{charMapPath}\" -t \"{inputFilePath}\" -b \"{outputFilePath}\" --json";
            chatot.StartInfo.UseShellExecute = false;
            chatot.StartInfo.CreateNoWindow = true;
            chatot.StartInfo.RedirectStandardError = true;
            string errorOutput = "";
            try
            {
                chatot.Start();
                chatot.WaitForExit();
                errorOutput = chatot.StandardError.ReadToEnd();
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
