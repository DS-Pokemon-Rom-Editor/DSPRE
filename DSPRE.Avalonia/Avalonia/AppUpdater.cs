using System;
using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;

namespace DSPRE.Avalonia
{
    /// <summary>
    /// Velopack-based update check for the Avalonia shell, the cross-platform counterpart of the
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
                    AppLogger.Info("Not a Velopack-installed build; skipping update check.");
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
                string velopackVersion = newVersion.TargetFullRelease.Version.ToString();
                string availableVersion = ToDotNetVersion(velopackVersion);

                string notes = await Task.Run(() => FetchReleaseNotes($"v{availableVersion}"));
                bool install = await ShowUpdatePrompt(currentVersion, availableVersion, notes);

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

        /// <summary>Shows the prompt with the release notes. Public so the dev preview can reuse it.</summary>
        public static async Task<bool> ShowUpdatePrompt(string currentVersion, string availableVersion, string notes, bool preview = false)
        {
            var owner = (global::Avalonia.Application.Current?.ApplicationLifetime
                as global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            var dlg = new Views.UpdateAvailableWindow(currentVersion, availableVersion, notes, preview);
            if (owner != null) await dlg.ShowDialog(owner);
            else dlg.Show();
            return dlg.Install;
        }

        // Velopack strips trailing zero parts and turns a revision into "-revN"; the release tag keeps the
        // original 4-part AssemblyVersion, so it has to be rebuilt before the notes can be looked up.
        private static string ToDotNetVersion(string velopackVersion)
        {
            try
            {
                if (velopackVersion.Contains("-rev"))
                {
                    string[] parts = velopackVersion.Split('-');
                    string[] nums = parts[0].Split('.');
                    int revision = int.Parse(parts[1].Replace("rev", ""));
                    int patch = int.Parse(nums[2]) - 1;
                    return $"{nums[0]}.{nums[1]}.{patch}.{revision}";
                }
                string[] v = velopackVersion.Split('.');
                return v.Length >= 3 ? $"{v[0]}.{v[1]}.{v[2]}.0" : velopackVersion;
            }
            catch { return velopackVersion; }
        }

        /// <summary>Reads a release's notes off GitHub. Returns null when there are none to show.</summary>
        private static string FetchReleaseNotes(string tag)
        {
            try
            {
                using var client = new System.Net.Http.HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "DSPRE");
                client.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");

                var response = client.GetAsync(
                    $"https://api.github.com/repos/DS-Pokemon-Rom-Editor/DSPRE/releases/tags/{tag}").Result;
                if (!response.IsSuccessStatusCode)
                {
                    AppLogger.Warn($"Release notes for '{tag}' returned {(int)response.StatusCode}.");
                    return null;
                }

                using var doc = System.Text.Json.JsonDocument.Parse(response.Content.ReadAsStringAsync().Result);
                if (doc.RootElement.TryGetProperty("body", out var body) &&
                    body.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    string text = body.GetString();
                    return string.IsNullOrWhiteSpace(text) ? null : text;
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warn("Couldn't fetch release notes: " + ex.Message);
            }
            return null;
        }
    }
}
