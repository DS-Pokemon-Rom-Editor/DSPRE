using System;
using System.Collections.Generic;

namespace DSPRE.Avalonia
{
    /// <summary>
    /// Hooks the app installs into the UI layer at startup; today only the update check.
    /// </summary>
    public static class ShellIntegration
    {
        /// <summary>App-update check (Velopack lives in the app exe, not this UI layer).</summary>
        public static Action<bool> CheckForUpdatesHook = null;
        public static void CheckForUpdates(bool silent)
        {
            if (CheckForUpdatesHook != null) CheckForUpdatesHook(silent);
            else if (!silent) _ = DialogHelper.ShowInfoAsync("Update checking isn't available in this build.", "Check for updates");
        }
    }
}
