using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace DSPRE
{
    /// <summary>
    /// Avalonia Application entry point (UI-toolkit layer, no WinForms dependency).
    ///
    /// The Windows DSPRE exe installs <see cref="WinFormsHostHook"/> so that, by default, startup
    /// shows the legacy WinForms MainProgram (both toolkits share the Win32 message pump). Without
    /// the hook, or with DSPRE_AVALONIA_SHELL=1, the pure-Avalonia shell runs instead.
    /// </summary>
    public class AvaloniaApp : Application
    {
        /// <summary>
        /// Installed by the Windows host exe: shows the WinForms main form and wires its lifetime to
        /// the Avalonia application lifetime. Null (e.g. in the cross-platform exe) = pure shell.
        /// </summary>
        public static System.Action<IClassicDesktopStyleApplicationLifetime> WinFormsHostHook;

        // Force the pure-Avalonia shell even when a WinForms host is available.
        private static bool ForceAvaloniaShell =>
            string.Equals(System.Environment.GetEnvironmentVariable("DSPRE_AVALONIA_SHELL"), "1", System.StringComparison.Ordinal);

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // Prevent Avalonia from shutting down when an editor window closes; the shell
                // (Avalonia main window or hosted WinForms form) controls the process lifetime.
                desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

                // Catch exceptions from async-void UI handlers (Save/Import/close, …) so one editor
                // throwing doesn't kill the process and every other editor's unsaved work with it.
                DSPRE.Avalonia.AvaloniaErrorHandler.Install();

                if (WinFormsHostHook == null || ForceAvaloniaShell)
                {
                    // The pure-Avalonia shell is the only one that ever runs on Linux (no WinForms
                    // host exe there). ndstool/blz/apicula have no native Linux build yet, so without
                    // Wine (or WSL's own interop) nothing DSPRE does can actually touch a ROM.
                    if (DSUtils.RequiresWineButUnavailable())
                    {
                        global::Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
                        {
                            await DSPRE.Avalonia.DialogHelper.ShowError(
                                "DSPRE needs Wine to run its bundled tools (ndstool, blz, apicula) on Linux, "
                                + "but Wine wasn't found on PATH.\n\n"
                                + "Install it (e.g. \"sudo apt install wine\") and start DSPRE again.",
                                "Wine required");
                            desktop.Shutdown();
                        });
                        base.OnFrameworkInitializationCompleted();
                        return;
                    }

                    CrashReporter.Initialize();   // global crash handlers + report file

                    // The WinForms shell does these in the MainProgram ctor; the pure-Avalonia shell must
                    // do them itself (ROM loads read Settings, and the logger needs its file path).
                    SettingsManager.Load();
                    AppLogger.Initialize();
                    DatabaseSetup.CopyBundledDatabases();

                    // RomInfo warnings → an Avalonia dialog (marshalled to the UI thread; loads run off-thread).
                    DSPRE.RomInfo.ShowWarning = (msg, title) =>
                        global::Avalonia.Threading.Dispatcher.UIThread.Post(() => _ = DSPRE.Avalonia.DialogHelper.ShowError(msg, title));

                    // Core (WinForms-free ROMFiles/DSUtils) user messages + save picker → native Avalonia dialogs.
                    DSPRE.Avalonia.CoreDialogs.Install();

                    // Velopack update check (cross-platform), unless the host already provided one.
                    if (DSPRE.Avalonia.ShellIntegration.CheckForUpdatesHook == null)
                        DSPRE.Avalonia.ShellIntegration.CheckForUpdatesHook = DSPRE.Avalonia.AppUpdater.CheckForUpdates;
                    if (SettingsManager.Settings?.automaticallyCheckForUpdates == true)
                        DSPRE.Avalonia.ShellIntegration.CheckForUpdates(silent: true);

                    var main = new DSPRE.Avalonia.Views.MainWindowView(new DSPRE.Avalonia.ViewModels.MainWindowViewModel(true));
                    desktop.MainWindow = main;
                    desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;   // closing the shell exits the app
                    main.Show();

                    // "Open Default ROM" setting: auto-open it at boot (asking first unless
                    // "Open without asking" is also set). The welcome window is skipped when the
                    // default ROM opens, and shown as usual when it doesn't.
                    string defaultRom = SettingsManager.Settings?.openDefaultRom;
                    bool haveDefaultRom = !string.IsNullOrWhiteSpace(defaultRom) &&
                        (System.IO.File.Exists(defaultRom) || System.IO.Directory.Exists(defaultRom));

                    // First-run / returning-user onboarding (recent projects + tutorial); user-toggleable,
                    // relaunchable from Tools → Welcome & Tutorial and from Settings.
                    void ShowWelcomeIfEnabled()
                    {
                        if (SettingsManager.Settings?.showWelcomeOnStartup != false)
                            DSPRE.Avalonia.Views.WelcomeView.ShowWelcome(main);
                    }

                    if (haveDefaultRom)
                    {
                        global::Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
                        {
                            bool open = SettingsManager.Settings.neverAskForOpening ||
                                await DSPRE.Avalonia.DialogHelper.AskYesNo(
                                    $"Open the default ROM?\n\n{defaultRom}", "DSPRE");
                            if (open) await main.OpenRecentAsync(defaultRom);
                            else ShowWelcomeIfEnabled();
                        });
                    }
                    else
                    {
                        global::Avalonia.Threading.Dispatcher.UIThread.Post(ShowWelcomeIfEnabled);
                    }

                    base.OnFrameworkInitializationCompleted();
                    return;
                }

                // Legacy Windows shell: the host exe shows the WinForms main form and ties its
                // FormClosed to desktop.Shutdown().
                WinFormsHostHook(desktop);
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
