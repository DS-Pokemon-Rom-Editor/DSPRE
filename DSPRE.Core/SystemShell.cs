using System;
using System.Diagnostics;
using System.IO;

namespace DSPRE
{
    /// <summary>
    /// Cross-platform "show me this file" helpers (extracted from the WinForms <c>Helpers</c>).
    /// Windows opens Explorer; Linux falls back to xdg-open on the containing folder.
    /// </summary>
    public static class SystemShell
    {
        /// <summary>Reveal the file in the OS file manager (selected where the platform supports it).</summary>
        public static void RevealInFileManager(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return;
            }
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    Process.Start("explorer.exe", "/select," + "\"" + path + "\"");
                }
                else
                {
                    Process.Start(new ProcessStartInfo("xdg-open", "\"" + Path.GetDirectoryName(path) + "\""));
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Failed to reveal '{path}' in the file manager: {ex.Message}");
            }
        }

        /// <summary>Open the file (or folder) with the system default application.</summary>
        public static void OpenWithDefaultApp(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    AppMessages.Error("Path is empty.", "Error");
                    return;
                }
                if (OperatingSystem.IsWindows())
                {
                    Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                }
                else
                {
                    Process.Start(new ProcessStartInfo("xdg-open", "\"" + path + "\""));
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Failed to open '{path}' with default app: {ex.Message}");
                AppMessages.Error($"Unable to open file with the default application:\n{ex.Message}", "Error");
            }
        }
    }
}
