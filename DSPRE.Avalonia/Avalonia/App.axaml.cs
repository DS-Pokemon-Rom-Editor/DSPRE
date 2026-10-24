using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Rendering.Composition;

namespace DSPRE
{
    /// <summary>Avalonia Application entry point.</summary>
    public class AvaloniaApp : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);

            // A cleared number box would sit blank over an unchanged value, so it gets its last number back on losing focus.
            ConditionalWeakTable<NumericUpDown, object> lastNumber = new System.Runtime.CompilerServices.ConditionalWeakTable<NumericUpDown, object>();
            void RestoreIfEmpty(NumericUpDown box) => global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (box.Value == null && !box.IsKeyboardFocusWithin && lastNumber.TryGetValue(box, out object last))
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
            // Nearly every picture is the game's pixel art, which smoothing blurs. A style can't set this, so it is set
            // here; a picture that wants smoothing sets it on itself, as Icon does.
            global::Avalonia.Controls.Image.SourceProperty.Changed.AddClassHandler<global::Avalonia.Controls.Image>((image, _) =>
            {
                if (global::Avalonia.Media.RenderOptions.GetBitmapInterpolationMode(image) == global::Avalonia.Media.Imaging.BitmapInterpolationMode.Unspecified)
                    global::Avalonia.Media.RenderOptions.SetBitmapInterpolationMode(image, global::Avalonia.Media.Imaging.BitmapInterpolationMode.None);
            });
        }

        // Without GPU interop every 3D view stays blank, so say which one this machine got.
        private static async void LogRenderingMode(Window window)
        {
            try
            {
                Compositor compositor = global::Avalonia.Rendering.Composition.ElementComposition.GetElementVisual(window)?.Compositor;
                ICompositionGpuInterop interop = compositor == null ? null : await compositor.TryGetCompositionGpuInterop();
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
                // Prevent Avalonia from shutting down when an editor window closes; the main window controls the process lifetime.
                desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

                // Catch exceptions from async-void UI handlers (Save/Import/close, …) so one editor
                // throwing doesn't kill the process and every other editor's unsaved work with it.
                DSPRE.Avalonia.AvaloniaErrorHandler.Install();
                DSPRE.Avalonia.OwnerWindow.Install();
                DSPRE.Avalonia.EditorTours.Install();
                InstallPaletteShortcut();

                // Grey secondary text in list rows is unreadable on the blue selection, in both themes.
                SolidColorBrush subtleOnSelection = new global::Avalonia.Media.SolidColorBrush(global::Avalonia.Media.Color.FromRgb(0xD8, 0xE8, 0xF4));
                void Selected(global::Avalonia.Controls.Control row, bool on)
                {
                    if (on) row.Resources["Editor.Subtle"] = subtleOnSelection;
                    else row.Resources.Remove("Editor.Subtle");
                }
                global::Avalonia.Controls.ListBoxItem.IsSelectedProperty.Changed.AddClassHandler<global::Avalonia.Controls.ListBoxItem>((row, e) => Selected(row, row.IsSelected));
                global::Avalonia.Controls.TreeViewItem.IsSelectedProperty.Changed.AddClassHandler<global::Avalonia.Controls.TreeViewItem>((row, e) => Selected(row, row.IsSelected));

                // ndstool/blz/apicula have no native Linux build yet, so without
                // Wine (or WSL's own interop) nothing DSPRE does can actually touch a ROM.
                if (DSUtils.RequiresWineButUnavailable())
                {
                    global::Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
                    {
                        await DSPRE.Avalonia.DialogHelper.ShowErrorAsync(
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

                // ROM loads read Settings, and the logger needs its file path.
                SettingsManager.Load();
                DSPRE.Avalonia.ThemeManager.ApplySaved();
                AppLogger.Initialize(DSPRE.Avalonia.ProjectWindows.LogFileName());
                DatabaseSetup.CopyBundledDatabases();

                // RomInfo warnings → an Avalonia dialog (marshalled to the UI thread; loads run off-thread).
                DSPRE.RomInfo.ShowWarning = (msg, title) =>
                    global::Avalonia.Threading.Dispatcher.UIThread.Post(() => _ = DSPRE.Avalonia.DialogHelper.ShowErrorAsync(msg, title));

                // Refuse a write that hg-engine's build would undo, so it isn't lost silently.
                DSPRE.HgEngine.HgEngineWriteGuard.OnRefused = msg =>
                    global::Avalonia.Threading.Dispatcher.UIThread.Post(
                        () => _ = DSPRE.Avalonia.DialogHelper.ShowErrorAsync(msg, "Built by hg-engine"));

                // Archive members saved from Core keep hg-engine's PNGs in step and reach open editors.
                DSPRE.ArchiveMembers.Written += DSPRE.Avalonia.Data.BuiltPngSources.OnMemberWritten;
                // Previews cache fonts and number glyphs, so a font save clears them before editors redraw.
                DSPRE.Avalonia.AppEvents.ArchiveMemberSaved += (_, dir) =>
                {
                    if (dir != DSPRE.RomInfo.DirNames.Fonts) return;
                    DSPRE.Avalonia.Data.GameTextScreens.ForgetFonts();
                    DSPRE.Avalonia.Data.TextScreenPreviews.ForgetFonts();
                    DSPRE.Avalonia.Data.BattleGaugeTextRenderer.Reset();
                };

                DSPRE.Avalonia.CoreDialogs.Install();

                // Velopack update check (cross-platform), unless the host already provided one.
                if (DSPRE.Avalonia.ShellIntegration.CheckForUpdatesHook == null)
                    DSPRE.Avalonia.ShellIntegration.CheckForUpdatesHook = DSPRE.Avalonia.AppUpdater.CheckForUpdates;
                if (SettingsManager.Settings?.AutomaticallyCheckForUpdates == true)
                    DSPRE.Avalonia.ShellIntegration.CheckForUpdates(silent: true);

                MainWindowView main = new DSPRE.Avalonia.Views.Shell.MainWindowView(new DSPRE.Avalonia.ViewModels.Shell.MainWindowViewModel(true));
                desktop.MainWindow = main;
                desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;   // closing the shell exits the app
                main.Show();
                LogRenderingMode(main);

                // The default ROM opens at boot, asking first unless "Open the default ROM without asking" is set, and the welcome window doesn't open.
                string defaultRom = SettingsManager.Settings?.OpenDefaultRom;
                bool haveDefaultRom = !string.IsNullOrWhiteSpace(defaultRom) &&
                    (System.IO.File.Exists(defaultRom) || System.IO.Directory.Exists(defaultRom));

                // Reopened from Tools > Welcome & tutorial or Settings.
                void ShowWelcomeIfEnabled()
                {
                    if (SettingsManager.Settings?.ShowWelcomeOnStartup != false)
                        DSPRE.Avalonia.Views.Shell.WelcomeView.ShowWelcome(main);
                }

                desktop.Exit += (_, _) => DSPRE.Avalonia.ProjectWindows.Release();

                // Started from "Open in new window", or with a ROM or folder on the command line.
                if (DSPRE.Avalonia.ProjectWindows.StartupPath is string startup)
                {
                    global::Avalonia.Threading.Dispatcher.UIThread.Post(async () => await main.OpenRecentAsync(startup));
                }
                else if (haveDefaultRom)
                {
                    global::Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
                    {
                        bool open = SettingsManager.Settings.NeverAskForOpening ||
                            await DSPRE.Avalonia.DialogHelper.AskYesNoAsync(
                                $"Open the default ROM?\n\n{defaultRom}", "DSPRE");
                        if (open) await main.OpenRecentAsync(defaultRom);
                        else ShowWelcomeIfEnabled();
                    });
                }
                else if (!string.IsNullOrWhiteSpace(defaultRom))
                {
                    // Moved or deleted since it was chosen: say so once and stop trying it on every launch.
                    SettingsManager.Settings.OpenDefaultRom = "";
                    SettingsManager.Save();
                    global::Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
                    {
                        await DSPRE.Avalonia.DialogHelper.ShowInfoAsync(
                            $"The default ROM is no longer there, so it was removed from Settings:\n\n{defaultRom}", "DSPRE");
                        ShowWelcomeIfEnabled();
                    });
                }
                else
                {
                    global::Avalonia.Threading.Dispatcher.UIThread.Post(ShowWelcomeIfEnabled);
                }
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
