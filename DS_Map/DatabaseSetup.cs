using System;
using System.IO;

namespace DSPRE
{
    /// <summary>
    /// First-run copy of the bundled script databases from the install folder into the per-user
    /// data folder (extracted from the WinForms <c>Program.SetupDatabase</c>; core, cross-platform).
    /// </summary>
    public static class DatabaseSetup
    {
        public static void CopyBundledDatabases()
        {
            // needs to be this verbose (copy instead of move) so this works across drives
            try
            {
                string sourceDbPath = Path.Combine(AppContext.BaseDirectory, "databases");
                if (Directory.Exists(sourceDbPath) && !SettingsManager.Settings.databasesPulled)
                {
                    if (!Directory.Exists(AppPaths.DatabasePath))
                    {
                        Directory.CreateDirectory(AppPaths.DatabasePath);
                    }
                    foreach (string dirPath in Directory.GetDirectories(sourceDbPath, "*", SearchOption.AllDirectories))
                    {
                        Directory.CreateDirectory(dirPath.Replace(sourceDbPath, AppPaths.DatabasePath));
                    }
                    foreach (string filePath in Directory.GetFiles(sourceDbPath, "*.*", SearchOption.AllDirectories))
                    {
                        File.Copy(filePath, filePath.Replace(sourceDbPath, AppPaths.DatabasePath), true);
                    }
                    // After a successful copy, remove the bundled source (best-effort: the install
                    // dir may be read-only, e.g. a system-wide install on Linux) and update settings.
                    try { Directory.Delete(sourceDbPath, true); }
                    catch (Exception delEx) { AppLogger.Warn($"Could not remove bundled databases folder: {delEx.Message}"); }
                    SettingsManager.Settings.databasesPulled = true;
                }
            }
            catch (Exception ex)
            {
                AppMessages.Error($"Failed to copy databases: {ex.Message}", "Database Setup Error");
            }
        }
    }
}
