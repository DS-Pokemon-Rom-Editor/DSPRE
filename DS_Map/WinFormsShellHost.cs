using System.Linq;
using Avalonia.Controls.ApplicationLifetimes;
using WinForms = System.Windows.Forms;

namespace DSPRE
{
    /// <summary>
    /// Hosts the legacy WinForms MainProgram inside the Avalonia application lifetime (both toolkits
    /// share the same Win32 message pump / STA thread) and installs the WinForms implementations of
    /// the UI-layer hooks. This file belongs to the Windows DSPRE exe only — the cross-platform
    /// Avalonia exe never references it.
    /// </summary>
    internal static class WinFormsShellHost
    {
        /// <summary>Install the WinForms implementations of the shell hooks (call before Avalonia starts).</summary>
        public static void InstallHooks()
        {
            AvaloniaApp.WinFormsHostHook = Attach;

            DSPRE.Avalonia.ShellIntegration.CheckForUpdatesHook = silent => Helpers.CheckForUpdates(silent);
            DSPRE.Avalonia.ShellIntegration.ReloadWinFormsHeaderLocationsHook = contents =>
            {
                var headerEditor = EditorPanels.headerEditor;
                if (headerEditor == null) return;
                var combo = headerEditor.locationNameComboBox;
                int selection = combo.SelectedIndex;
                combo.Items.Clear();
                combo.Items.AddRange(contents.ToArray());
                if (selection >= 0 && selection < combo.Items.Count)
                    combo.SelectedIndex = selection;
            };
        }

        /// <summary>Build and show the WinForms main form; tie its lifetime to the Avalonia lifetime.</summary>
        private static void Attach(IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Build and show the WinForms main form.
            // Velopack and directory setup were already run before Avalonia started.
            var mainProgram = new MainProgram();
            CrashReporter.Initialize();
            CrashReporter.RomPathProvider = () => mainProgram.romInfo?.GetRomNameFromWorkdir();
            WinForms.Application.ThreadException += (_, e) => CrashReporter.ReportCrash(e.Exception);

            // Show the WinForms form independently — it owns its own Win32 HWND.
            // Avalonia's Win32 backend pumps the same message loop so both stay alive.
            mainProgram.Show();

            // Before the app quits, warn if any open Avalonia editor still holds unsaved changes —
            // quitting force-closes every editor window, bypassing their individual close guards.
            mainProgram.FormClosing += (_, e) =>
            {
                if (e.Cancel) return;   // already cancelled by other WinForms logic
                var unsaved = DSPRE.Avalonia.OpenEditors.UnsavedDescriptions();
                if (unsaved.Count == 0) return;
                var result = WinForms.MessageBox.Show(
                    "The following editor(s) have unsaved changes that will be lost if you quit now:\n\n" +
                    "  • " + string.Join("\n  • ", unsaved) +
                    "\n\nQuit anyway and discard them?",
                    "Unsaved Changes",
                    WinForms.MessageBoxButtons.YesNo,
                    WinForms.MessageBoxIcon.Warning,
                    WinForms.MessageBoxDefaultButton.Button2);
                if (result != WinForms.DialogResult.Yes) e.Cancel = true;
            };

            // Hook WinForms FormClosed → shut down Avalonia lifetime so the process exits cleanly.
            mainProgram.FormClosed += (_, _) => desktop.Shutdown();
        }
    }
}
