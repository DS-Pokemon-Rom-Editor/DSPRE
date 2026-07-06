using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DSPRE
{
    /// <summary>
    /// Global crash handlers + crash-report files. Core (no UI-toolkit dependency): the crash dialog
    /// goes through <see cref="AppMessages"/> and folder reveal through <see cref="SystemShell"/>.
    /// The WinForms host additionally routes <c>Application.ThreadException</c> to
    /// <see cref="ReportCrash"/> and installs <see cref="RomPathProvider"/> for report context.
    /// </summary>
    public static class CrashReporter
    {
        /// <summary>Optional: supplies the "Opened ROM Path" line of the report.</summary>
        public static Func<string> RomPathProvider = null;

        public static void Initialize()
        {
            AppDomain.CurrentDomain.UnhandledException += (s, e) => ReportCrash(e.ExceptionObject as Exception);
            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                e.SetObserved(); // Prevents app from crashing
                ReportCrash(e.Exception);
            };
        }

        /// <summary>
        /// Writes a crash-report file for a NON-fatal, already-handled exception (e.g. one caught by the
        /// Avalonia UI-thread net) and returns the file path. Unlike <see cref="ReportCrash"/> it does
        /// NOT show the "application crashed" dialog — the caller surfaces a friendlier message and keeps
        /// the app running. Returns null if the report could not be written.
        /// </summary>
        public static string LogHandled(Exception ex)
        {
            try
            {
                string filePath = GetCrashReportFilePath();
                File.WriteAllText(filePath, BuildCrashReport(ex), Encoding.UTF8);
                return filePath;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Writes the crash report and shows the "application crashed" prompt.</summary>
        public static void ReportCrash(Exception ex)
        {
            string crashReport = BuildCrashReport(ex);
            string filePath = GetCrashReportFilePath();

            try
            {
                File.WriteAllText(filePath, crashReport, Encoding.UTF8);
            }
            catch
            {

            }

            if (AppMessages.Confirm(
                    $"An unexpected error occurred and the application crashed.\n\nA crash report was saved here:\n{filePath}\n\nOpen the folder?",
                    "Application Error"))
            {
                SystemShell.RevealInFileManager(filePath);
            }
        }

        private static string BuildCrashReport(Exception ex)
        {
            string romPath;

            var sb = new StringBuilder();

            sb.AppendLine("===== Crash Report =====");
            sb.AppendLine($"Timestamp: {DateTime.Now}");
            sb.AppendLine($"App Version: {AppInfo.GetDSPREVersion()}");
            sb.AppendLine($"App Path: {AppDomain.CurrentDomain.BaseDirectory}");
            sb.AppendLine($".NET Version: {Environment.Version}");
            sb.AppendLine($"OS: {Environment.OSVersion}");
            sb.AppendLine($"Is 64-bit OS: {Environment.Is64BitOperatingSystem}");
            try
            {
                romPath = RomPathProvider?.Invoke() ?? "Unknown";
            }
            catch (Exception romEx)
            {
                romPath = $"Failed to retrieve ROM path: {romEx.Message}";
            }
            sb.AppendLine($"Opened ROM Path: {romPath}");
            sb.AppendLine();
            sb.AppendLine("===== Recent Logs =====");
            sb.AppendLine(AppLogger.GetRecentLogs());


            if (ex != null)
            {
                sb.AppendLine("Exception:");
                sb.AppendLine(ex.ToString());
            }
            else
            {
                sb.AppendLine("Exception: Unknown");
            }

            return sb.ToString();
        }

        private static string GetCrashReportFilePath()
        {
            string crashDir = Path.Combine(AppPaths.DspreDataPath, "CrashReports");
            Directory.CreateDirectory(crashDir);

            string filename = $"Crash_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
            return Path.Combine(crashDir, filename);
        }
    }
}
