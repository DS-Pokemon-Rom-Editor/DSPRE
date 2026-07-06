using System;
using System.Collections.Generic;

namespace DSPRE.Avalonia
{
    /// <summary>
    /// Optional hooks the HOST application installs into the Avalonia UI layer for features that
    /// live outside it (updater, WinForms-only tools, legacy bridges). The pure-Avalonia shell
    /// runs with the defaults; the Windows DSPRE exe installs implementations at startup.
    /// </summary>
    public static class ShellIntegration
    {
        /// <summary>App-update check (Velopack lives in the app exe, not this UI layer).</summary>
        public static Action<bool> CheckForUpdatesHook = null;
        public static void CheckForUpdates(bool silent)
        {
            if (CheckForUpdatesHook != null) CheckForUpdatesHook(silent);
            else if (!silent) _ = DialogHelper.ShowInfo("Update checking isn't available in this build.", "Check for updates");
        }

        /// <summary>Legacy bridge: refresh the WinForms header editor's location combo after text edits.</summary>
        public static Action<IEnumerable<string>> ReloadWinFormsHeaderLocationsHook = null;
        public static void ReloadWinFormsHeaderLocations(IEnumerable<string> contents)
            => ReloadWinFormsHeaderLocationsHook?.Invoke(contents);
    }
}
