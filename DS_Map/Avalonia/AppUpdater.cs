using System;
using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;

namespace DSPRE.Avalonia
{
    /// <summary>
    /// Velopack-based update check for the Avalonia shell — the cross-platform counterpart of the
    /// WinForms <c>Helpers.CheckForUpdates</c> (Velopack ships Windows installers and Linux
    /// AppImages from the same release feed). Installed as the default
    /// <see cref="ShellIntegration.CheckForUpdatesHook"/> when no host provides one.
    /// </summary>
    public static class AppUpdater
    {
        private const string RepoUrl = "https://github.com/DS-Pokemon-Rom-Editor/DSPRE";

        public static void CheckForUpdates(bool silent) => _ = CheckForUpdatesAsync(silent);

        private static async Task CheckForUpdatesAsync(bool silent)
        {
            AppLogger.Info("Checking for updates...");
            try
            {
                var mgr = new UpdateManager(new GithubSource(RepoUrl, "", prerelease: false));

                if (!mgr.IsInstalled)
                {
                    AppLogger.Info("Not a Velopack-installed build — skipping update check.");
                    if (!silent)
                        await DialogHelper.ShowInfo("Update checks are only available in installed builds (not portable/dev runs).", "Check for updates");
                    return;
                }

                var newVersion = await Task.Run(() => mgr.CheckForUpdates());
                if (newVersion == null)
                {
                    AppLogger.Info("No updates available.");
                    if (!silent)
                        await DialogHelper.ShowInfo("No update is available.", "Information");
                    return;
                }

                string currentVersion = AppInfo.GetDSPREVersion();
                string availableVersion = newVersion.TargetFullRelease.Version.ToString();

                bool install = await DialogHelper.AskYesNo(
                    "A new DSPRE version is available!\n\n" +
                    $"Current: {currentVersion}\n" +
                    $"Available: {availableVersion}\n\n" +
                    "Do you want to install it now?",
                    "New Update Available");

                if (install)
                {
                    AppLogger.Info($"Downloading and installing update {availableVersion}...");
                    await Task.Run(() =>
                    {
                        mgr.DownloadUpdates(newVersion);
                        mgr.ApplyUpdatesAndRestart(newVersion);
                    });
                }
                else
                {
                    AppLogger.Info("User declined to update the application.");
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Error checking for updates: {ex.Message}");
                if (!silent)
                    await DialogHelper.ShowError($"Error checking for updates: {ex.Message}", "Error");
            }
        }
    }
}
