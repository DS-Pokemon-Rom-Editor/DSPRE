using System;
using System.IO;

namespace DSPRE
{
    /// <summary>
    /// First-run copy of the bundled script databases from the install folder into the per-user
    /// data folder.
    /// </summary>
    public static class DatabaseSetup
    {
        public static void CopyBundledDatabases()
        {
            // needs to be this verbose (copy instead of move) so this works across drives
            try
            {
                string sourceDbPath = Path.Combine(AppContext.BaseDirectory, "databases");
                if (Directory.Exists(sourceDbPath) && !SettingsManager.Settings.DatabasesPulled)
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
                        PlainFiles.Copy(filePath, filePath.Replace(sourceDbPath, AppPaths.DatabasePath), true);
                    }
                    // After a successful copy, remove the bundled source (best-effort: the install
                    // dir may be read-only, e.g. a system-wide install on Linux) and update settings.
                    try { Directory.Delete(sourceDbPath, true); }
                    catch (Exception delEx) { AppLogger.Warn($"Couldn't remove bundled databases folder: {delEx.Message}"); }
                    SettingsManager.Settings.DatabasesPulled = true;
                }
            }
            catch (Exception ex)
            {
                AppMessages.Error($"The script databases couldn't be copied: {ex.Message}", "Couldn't set up the databases");
            }
        }
    }
}
