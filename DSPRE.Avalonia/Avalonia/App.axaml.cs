using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace DSPRE
{
    /// <summary>
    /// Avalonia Application entry point (UI-toolkit layer, no WinForms dependency).
    ///
    /// The Windows DSPRE exe installs <see cref="WinFormsHostHook"/> so the legacy shell remains
    /// available when explicitly selected. The Avalonia shell is the default, and the cross-platform
    /// executable never installs the hook.
    /// </summary>
    public class AvaloniaApp : Application
    {
        /// <summary>
        /// Installed by the Windows host exe: shows the WinForms main form and wires its lifetime to
        /// the Avalonia application lifetime. Null (e.g. in the cross-platform exe) = pure shell.
        /// </summary>
        public static System.Action<IClassicDesktopStyleApplicationLifetime> WinFormsHostHook;

        // The Avalonia shell is what runs. The WinForms shell is still reachable for the few editors
        // that were never ported, with DSPRE_WINFORMS_SHELL=1 or --winforms on the command line.
        private static bool UseWinFormsShell =>
            string.Equals(System.Environment.GetEnvironmentVariable("DSPRE_WINFORMS_SHELL"), "1",
                          System.StringComparison.Ordinal)
            || System.Linq.Enumerable.Any(System.Environment.GetCommandLineArgs(),
                   a => string.Equals(a, "--winforms", System.StringComparison.OrdinalIgnoreCase));

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);

            // A cleared number box would sit blank over an unchanged value, so it gets its last number back on losing focus.
            var lastNumber = new System.Runtime.CompilerServices.ConditionalWeakTable<NumericUpDown, object>();
            void RestoreIfEmpty(NumericUpDown box) => global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (box.Value == null && !box.IsKeyboardFocusWithin && lastNumber.TryGetValue(box, out var last))
                    box.SetCurrentValue(NumericUpDown.ValueProperty, (decimal)last);
            });
            NumericUpDown.ValueChangedEvent.AddClassHandler<NumericUpDown>((box, e) =>
            {
                if (e.NewValue is decimal v) lastNumber.AddOrUpdate(box, v);
                else RestoreIfEmpty(box);
            }, global::Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
            global::Avalonia.Input.InputElement.LostFocusEvent.AddClassHandler<NumericUpDown>((box, _) => RestoreIfEmpty(box),
                global::Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
            DSPRE.Avalonia.Controls.DropdownTextSearch.Install();
        }

        // Without GPU interop every 3D view stays blank, so say which one this machine got.
        private static async void LogRenderingMode(Window window)
        {
            try
            {
                var compositor = global::Avalonia.Rendering.Composition.ElementComposition.GetElementVisual(window)?.Compositor;
                var interop = compositor == null ? null : await compositor.TryGetCompositionGpuInterop();
                AppLogger.Info(interop != null ? "Rendering: GPU" : "Rendering: software, 3D views unavailable");
            }
            catch (System.Exception ex) { AppLogger.Warn("Rendering mode unknown: " + ex.Message); }
        }

        // Ctrl+P opens the command palette from editor windows too; the main window handles its own, with the busy check.
        private static void InstallPaletteShortcut() =>
            global::Avalonia.Input.InputElement.KeyDownEvent.AddClassHandler<Window>((window, e) =>
            {
                if (e.Handled || e.Key != global::Avalonia.Input.Key.P || e.KeyModifiers != global::Avalonia.Input.KeyModifiers.Control) return;
                if (window is DSPRE.Avalonia.Views.Shell.MainWindowView or DSPRE.Avalonia.Views.Shell.CommandPaletteView) return;
                if (!DSPRE.Avalonia.AvaloniaEditorLauncher.IsRomLoaded) return;
                DSPRE.Avalonia.AvaloniaEditorLauncher.OpenCommandPalette(window);
                e.Handled = true;
            });

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
                DSPRE.Avalonia.OwnerWindow.Install();
                DSPRE.Avalonia.EditorTours.Install();
                InstallPaletteShortcut();

                // Grey secondary text in list rows is unreadable on the blue selection, in both themes.
                var subtleOnSelection = new global::Avalonia.Media.SolidColorBrush(global::Avalonia.Media.Color.FromRgb(0xD8, 0xE8, 0xF4));
                void Selected(global::Avalonia.Controls.Control row, bool on)
                {
                    if (on) row.Resources["Editor.Subtle"] = subtleOnSelection;
                    else row.Resources.Remove("Editor.Subtle");
                }
                global::Avalonia.Controls.ListBoxItem.IsSelectedProperty.Changed.AddClassHandler<global::Avalonia.Controls.ListBoxItem>((row, e) => Selected(row, row.IsSelected));
                global::Avalonia.Controls.TreeViewItem.IsSelectedProperty.Changed.AddClassHandler<global::Avalonia.Controls.TreeViewItem>((row, e) => Selected(row, row.IsSelected));

                if (WinFormsHostHook == null || !UseWinFormsShell)
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
                    DSPRE.Avalonia.ThemeManager.ApplySaved();
                    AppLogger.Initialize();
                    DatabaseSetup.CopyBundledDatabases();

                    // RomInfo warnings → an Avalonia dialog (marshalled to the UI thread; loads run off-thread).
                    DSPRE.RomInfo.ShowWarning = (msg, title) =>
                        global::Avalonia.Threading.Dispatcher.UIThread.Post(() => _ = DSPRE.Avalonia.DialogHelper.ShowError(msg, title));

                    // A write hg-engine's build would undo is refused rather than silently dropped.
                    DSPRE.HgEngine.HgEngineWriteGuard.OnRefused = msg =>
                        global::Avalonia.Threading.Dispatcher.UIThread.Post(
                            () => _ = DSPRE.Avalonia.DialogHelper.ShowError(msg, "Built by hg-engine"));

                    // Core (WinForms-free ROMFiles/DSUtils) user messages + save picker → native Avalonia dialogs.
                    DSPRE.Avalonia.CoreDialogs.Install();

                    // Velopack update check (cross-platform), unless the host already provided one.
                    if (DSPRE.Avalonia.ShellIntegration.CheckForUpdatesHook == null)
                        DSPRE.Avalonia.ShellIntegration.CheckForUpdatesHook = DSPRE.Avalonia.AppUpdater.CheckForUpdates;
                    if (SettingsManager.Settings?.automaticallyCheckForUpdates == true)
                        DSPRE.Avalonia.ShellIntegration.CheckForUpdates(silent: true);

                    var main = new DSPRE.Avalonia.Views.Shell.MainWindowView(new DSPRE.Avalonia.ViewModels.Shell.MainWindowViewModel(true));
                    desktop.MainWindow = main;
                    desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;   // closing the shell exits the app
                    main.Show();
                    LogRenderingMode(main);

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
                            DSPRE.Avalonia.Views.Shell.WelcomeView.ShowWelcome(main);
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
