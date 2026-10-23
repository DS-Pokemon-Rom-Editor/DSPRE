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
                // Nobody was waiting on this one, so there is nothing to interrupt and nowhere to put a
                // dialog: it arrives on the finalizer thread, where building a window throws and takes the
                // whole application down with it.
                e.SetObserved();
                LogHandled(e.Exception);
            };
        }

        /// <summary>
        /// Writes a crash report for an error DSPRE recovered from, without the crash dialog, and returns its
        /// path. Null when the report can't be written.
        /// </summary>
        public static string LogHandled(Exception ex)
        {
            try
            {
                string filePath = GetCrashReportFilePath();
                PlainFiles.WriteText(filePath, BuildCrashReport(ex), Encoding.UTF8);
                return filePath;
            }
            catch (Exception logError)
            {
                AppLogger.WarnOnce("CrashReporter.LogHandled", logError);
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
                PlainFiles.WriteText(filePath, crashReport, Encoding.UTF8);
            }
            catch
            {
                // The crash report is best effort; there is nowhere left to report this failure.
            }

            if (AppMessages.Confirm(
                    $"DSPRE crashed. A crash report was saved here:\n{filePath}\n\nOpen the folder?",
                    "DSPRE crashed"))
            {
                SystemShell.RevealInFileManager(filePath);
            }
        }

        private static string BuildCrashReport(Exception ex)
        {
            string romPath;

            StringBuilder sb = new StringBuilder();

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
                romPath = $"Couldn't retrieve ROM path: {romEx.Message}";
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
