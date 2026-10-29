using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using DSPRE.Editors;
using DSPRE.Avalonia.ViewModels;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
using NarcAPI;
using System.IO;

namespace DSPRE.Avalonia.Views.Shell
{
    /// <summary>
    /// The main window. Menus open editors through <see cref="AvaloniaEditorLauncher"/>.
    /// </summary>
    public partial class MainWindowView : Window
    {
        private bool _closeConfirmed;

        public MainWindowView()
        {
            InitializeComponent();
            ScreensMenu.AddHandler(PointerReleasedEvent, ScreensMenu_Released, global::Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
#if DEBUG
            AddChangelogPreviewMenuItem();
#endif
            // Ctrl+P → quick-open command palette (jump to any editor by name). Other windows: App.InstallPaletteShortcut.
            KeyDown += (s, e) =>
            {
                // The busy card covers the menus but not their shortcuts.
                if (IsShellBusy) return;
                if (e.Key == global::Avalonia.Input.Key.P && AvaloniaEditorLauncher.IsRomLoaded &&
                    e.KeyModifiers.HasFlag(global::Avalonia.Input.KeyModifiers.Control))
                {
                    AvaloniaEditorLauncher.OpenCommandPalette(this);
                    e.Handled = true;
                }
                else if (e.Key == global::Avalonia.Input.Key.F5 && e.KeyModifiers == global::Avalonia.Input.KeyModifiers.None
                         && AvaloniaEditorLauncher.IsRomLoaded)
                {
                    _ = BuildAndRunAsync();
                    e.Handled = true;
                }
            };

            RecentMenu.SubmenuOpened += (_, _) => RebuildRecentMenu();
            NewWindowMenu.SubmenuOpened += (_, _) => RebuildNewWindowMenu();
            SetupFavourites();
            ProjectDrop.Attach(this, () => this);
            ProjectSourceWatcher.ScriptsCompiled += (result, paths) =>
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() => NoteOutsideScriptCompile(result, paths));

            AppEvents.BannerChanged += (_, _) =>
                global::Avalonia.Threading.Dispatcher.UIThread.Post(RefreshGameIcon);

            AppEvents.HgEngineLinkChanged += (_, _) =>
                global::Avalonia.Threading.Dispatcher.UIThread.Post(
                    () => (DataContext as MainWindowViewModel)?.RefreshHgEngineState());

            RestoreWindowPlacement();
        }

        protected override async void OnClosing(WindowClosingEventArgs e)
        {
            if (!_closeConfirmed)
            {
                e.Cancel = true;
                if (!await ConfirmProjectCloseAsync(openingAnother: false)) return;
                OpenEditors.CloseEditorWindows(this);
                _closeConfirmed = true;
                Close();
                return;
            }

            SaveWindowPlacement();
            base.OnClosing(e);
        }

        /// <summary>Shows the loaded game's banner icon at the right end of the menu bar,
        /// with its DS-menu title as tooltip. Hidden when no ROM (or no readable banner).</summary>
        private void RefreshGameIcon()
        {
            try
            {
                GameIconImage.Source = null;
                GameIconImage.IsVisible = false;
                if (!AvaloniaEditorLauncher.IsRomLoaded) return;
                (global::Avalonia.Media.Imaging.Bitmap icon, string title) = ViewModels.Graphics.GameBannerUi.TryLoad();
                if (icon == null) return;
                GameIconImage.Source = icon;
                GameIconImage.IsVisible = true;
                global::Avalonia.Controls.ToolTip.SetTip(GameIconImage,
                    string.IsNullOrWhiteSpace(title) ? RomInfo.ProjectName : title);
            }
            catch (System.Exception ex)
            {
                AppLogger.Warn("Game icon refresh failed: " + ex.Message);
            }
        }

        private async void GameIcon_DoubleTapped(object sender, global::Avalonia.Input.TappedEventArgs e)
            => await OpenBannerEditorAsync();

        private async void BannerEditor_Click(object sender, RoutedEventArgs e)
            => await OpenBannerEditorAsync();

        private async System.Threading.Tasks.Task OpenBannerEditorAsync()
            => await AvaloniaEditorLauncher.OpenBannerEditorAsync();

        // The window keeps its size and maximized state; the OS centres it.
        private void RestoreWindowPlacement()
        {
            DspreSettings s = SettingsManager.Settings;
            if (s == null) return;
            if (s.MainWindowWidth >= MinWidth && s.MainWindowHeight >= MinHeight)
            {
                Width = s.MainWindowWidth;
                Height = s.MainWindowHeight;
            }
            if (s.MainWindowMaximized) WindowState = WindowState.Maximized;
        }

        private void SaveWindowPlacement()
        {
            DspreSettings s = SettingsManager.Settings;
            if (s == null) return;
            s.MainWindowMaximized = WindowState == WindowState.Maximized;
            if (WindowState == WindowState.Normal)
            {
                s.MainWindowWidth = Width;
                s.MainWindowHeight = Height;
            }
            SettingsManager.Save();
        }

        // The recent projects submenu is rebuilt each time it opens.
        private void RebuildRecentMenu()
        {
            RecentMenu.Items.Clear();
            List<string> recents = SettingsManager.Settings?.RecentProjects;
            if (recents == null || recents.Count == 0)
            {
                RecentMenu.Items.Add(new MenuItem { Header = "(no recent projects)", IsEnabled = false });
                return;
            }
            foreach (string path in recents)
            {
                MenuItem item = new MenuItem { Header = CompactPath(path), Tag = path };
                global::Avalonia.Controls.ToolTip.SetTip(item, path);
                item.Click += async (_, _) => await OpenRecentAsync((string)item.Tag);
                RecentMenu.Items.Add(item);
            }
        }

        private void RebuildNewWindowMenu()
        {
            NewWindowMenu.Items.Clear();
            MenuItem rom = new MenuItem { Header = "ROM…" };
            rom.Click += async (_, _) => { if (await PickRomAsync() is string p) ProjectWindows.OpenInNewWindow(p); };
            MenuItem folder = new MenuItem { Header = "Extracted folder…" };
            folder.Click += async (_, _) => { if (await PickFolderAsync() is string p) ProjectWindows.OpenInNewWindow(p); };
            NewWindowMenu.Items.Add(rom);
            NewWindowMenu.Items.Add(folder);
            List<string> recents = SettingsManager.Settings?.RecentProjects;
            if (recents == null || recents.Count == 0) return;
            NewWindowMenu.Items.Add(new Separator());
            foreach (string path in recents)
            {
                MenuItem item = new MenuItem { Header = CompactPath(path), Tag = path };
                global::Avalonia.Controls.ToolTip.SetTip(item, path);
                item.Click += (_, _) => ProjectWindows.OpenInNewWindow((string)item.Tag);
                NewWindowMenu.Items.Add(item);
            }
        }

        /// <summary>Asks about unsaved work in every editor; true when saved, discarded or clean. Editors
        /// close only once the new project is chosen.</summary>
        /// <param name="openingAnother">False when the program is closing, not opening another project.</param>
        private async System.Threading.Tasks.Task<bool> ConfirmProjectCloseAsync(bool openingAnother = true)
        {
            IReadOnlyList<UnsavedChangesDialog.UnsavedEditorInfo> editors = OpenEditors.GetUnsavedEditors(this);
            if (editors.Count > 0)
            {
                return await UnsavedChangesDialog.ShowIfNeededAsync(this, editors);
            }

            // Ask even with nothing unsaved: otherwise the project vanishes on click and looks like a crash.
            if (!openingAnother || !AvaloniaEditorLauncher.IsRomLoaded) return true;
            if (SettingsManager.Settings?.AskBeforeSwitchingProject == false) return true;

            (bool yes, bool dontAsk) = await DialogHelper.AskWithCheckAsync(
                $"Opening another project closes the current one first.\n\n"
                + $"{RomInfo.GetGameDisplayName()} will be closed, along with any editor windows it has "
                + "open. There are no unsaved changes, so nothing is lost.\n\nContinue?",
                "Close the current project?", "Yes", "No", "Don't ask again", false, this);
            if (yes && dontAsk && SettingsManager.Settings != null)
            {
                SettingsManager.Settings.AskBeforeSwitchingProject = false;
                SettingsManager.Save();
            }
            return yes;
        }

        // Opening, saving, building and converting all rewrite the project, so only one runs at a time.
        private bool _romOperation;

        private bool IsShellBusy => _romOperation || BusyOverlay.IsWorking
                                    || (DataContext as MainWindowViewModel)?.IsBusy == true;

        private async System.Threading.Tasks.Task RunRomOperationAsync(System.Func<System.Threading.Tasks.Task> operation)
        {
            if (IsShellBusy)
            {
                if (DataContext is MainWindowViewModel vm) vm.StatusText = "Wait for the current task to finish.";
                return;
            }
            _romOperation = true;
            try { await operation(); }
            finally { _romOperation = false; }
        }

        /// <summary>Shows why a picked ROM or folder can't be opened, before anything of the open project closes.</summary>
        private async System.Threading.Tasks.Task<bool> RefuseUnsupportedAsync(string path)
        {
            if (ProjectWindows.OpenElsewhere(path))
            {
                await DialogHelper.ShowInfoAsync("This project is already open in another DSPRE window.", "Open ROM");
                return true;
            }
            string why = AvaloniaRomLoader.WhyUnsupported(path);
            if (why == null) return false;
            await DialogHelper.ShowErrorAsync(why, "Open ROM", this);
            return true;
        }

        private static string CompactPath(string path)
        {
            string name = System.IO.Path.GetFileName(path.TrimEnd('\\', '/'));
            string parent = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(path.TrimEnd('\\', '/')) ?? "");
            return string.IsNullOrEmpty(parent) ? name : parent + System.IO.Path.DirectorySeparatorChar + name;
        }

#if DEBUG
        // Debug builds get an entry under Tools for checking how a release's notes will read before tagging.
        private void AddChangelogPreviewMenuItem()
        {
            MenuItem menu = this.FindControl<MenuItem>("ToolsMenu");
            if (menu == null) return;
            MenuItem item = new MenuItem { Header = "Preview the update prompt…" };
            item.Click += async (_, _) => await new ChangelogPreviewWindow().ShowDialog(this);
            menu.Items.Add(new Separator());
            menu.Items.Add(item);
        }
#endif

        private void CommandPalette_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenCommandPalette(this);

        public MainWindowView(MainWindowViewModel vm) : this()
        {
            DataContext = vm;
            // A fresh walker each time the card comes up: the sprites may only have been unpacked since.
            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName != nameof(MainWindowViewModel.IsBusy)) return;
                WalkerHost.Children.Clear();
                if (vm.IsBusy) WalkerHost.Children.Add(new DSPRE.Avalonia.Controls.LoadingWalker());
            };
        }

        public IEnumerable<(string EditorName, IEditorWithUnsavedChanges Editor)> GetEmbeddedEditors()
            => Maps?.GetEmbeddedEditors()
                ?? System.Linq.Enumerable.Empty<(string, IEditorWithUnsavedChanges)>();

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private async void OpenRom_Click(object sender, RoutedEventArgs e) => await OpenRomInteractiveAsync();

        private async void OpenFolder_Click(object sender, RoutedEventArgs e) => await OpenFolderInteractiveAsync();

        private async void OpenHgEngineFolder_Click(object sender, RoutedEventArgs e) => await OpenHgEngineFolderInteractiveAsync();

        /// <summary>Pick and open a .nds ROM (also used by the Welcome window).</summary>
        public System.Threading.Tasks.Task OpenRomInteractiveAsync() => RunRomOperationAsync(OpenRomCoreAsync);

        // The new project is picked and checked first, so a cancelled or refused open never costs the
        // current project its unsaved edits.
        private async System.Threading.Tasks.Task<string> PickRomAsync()
        {
            IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new global::Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = "Open ROM",
                AllowMultiple = false,
                FileTypeFilter = new[] { new global::Avalonia.Platform.Storage.FilePickerFileType("NDS ROM") { Patterns = new[] { "*.nds" } } }
            });
            return files != null && files.Count > 0 ? files[0].TryGetLocalPath() : null;
        }

        private async System.Threading.Tasks.Task<string> PickFolderAsync()
        {
            IReadOnlyList<IStorageFolder> folders = await StorageProvider.OpenFolderPickerAsync(new global::Avalonia.Platform.Storage.FolderPickerOpenOptions
            {
                Title = "Open extracted ROM folder", AllowMultiple = false
            });
            return folders != null && folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
        }

        private async System.Threading.Tasks.Task OpenRomCoreAsync()
        {
            string path = await PickRomAsync();
            if (string.IsNullOrEmpty(path) || await RefuseUnsupportedAsync(path)) return;

            bool? reExtract = await CheckExtractedDataChoiceAsync(path);
            if (reExtract == null) return;   // user aborted
            if (!await ConfirmProjectCloseAsync()) return;
            OpenEditors.CloseEditorWindows(this);
            await LoadRomAsync(err0 => { bool ok = AvaloniaRomLoader.LoadFromFile(path, out string er, reExtract.Value); err0(er); return ok; }, sourcePath: path);
        }

        /// <summary>
        /// If this .nds was extracted before, asks whether to reuse that folder or extract again. Returns false
        /// to reuse, true to extract again, null to cancel.
        /// </summary>
        private async System.Threading.Tasks.Task<bool?> CheckExtractedDataChoiceAsync(string ndsPath)
        {
            int folderType = AvaloniaRomLoader.PeekFolderType(ndsPath);
            if (folderType == -1) return false;   // nothing extracted yet, nothing to ask

            string message = folderType == 0
                ? "This ROM was extracted before. Use that folder?"
                : "This ROM was extracted before, by a DSPRE older than 1.15.0. Use that folder?";
            message += "\n\nExtracting again replaces it, and any edits in it are lost.";

            DialogHelper.MsgResult choice = await DialogHelper.AskThreeWayAsync(message, "Extracted before",
                "Use it", "Extract again");
            if (choice == DialogHelper.MsgResult.Cancel) return null;
            if (choice == DialogHelper.MsgResult.Yes) return false;

            bool confirmReExtract = await DialogHelper.AskYesNoAsync(
                "Extract the ROM again? Edits in the extracted folder are lost.", "Extract again?");
            return confirmReExtract ? (bool?)true : null;
        }

        /// <summary>Pick and open an extracted project folder (also used by the Welcome window).</summary>
        public System.Threading.Tasks.Task OpenFolderInteractiveAsync() => RunRomOperationAsync(OpenFolderCoreAsync);

        private async System.Threading.Tasks.Task OpenFolderCoreAsync()
        {
            string path = await PickFolderAsync();
            if (string.IsNullOrEmpty(path)) return;

            if (CheckoutFor(path) is string checkout)
            {
                await OpenHgEngineFolderCoreAsync(checkout);
                return;
            }

            if (await RefuseUnsupportedAsync(path)) return;
            if (!await ConfirmProjectCloseAsync()) return;
            OpenEditors.CloseEditorWindows(this);
            await LoadRomAsync(err0 => { bool ok = AvaloniaRomLoader.LoadFromFolder(path, out string er); err0(er); return ok; }, sourcePath: path);
        }

        /// <summary>The checkout a folder opens as: an hg-engine checkout itself, or the one whose base/ tree it is.</summary>
        private static string CheckoutFor(string path)
        {
            if (HgEngineProject.LooksLikeCheckout(path)) return path;
            string parent = System.IO.Path.GetDirectoryName(path.TrimEnd('\\', '/'));
            return HgEngineProject.LooksLikeCheckout(parent) && DSUtils.GetFolderType(path) == 2 ? parent : null;
        }

        /// <summary>Pick and open an hg-engine checkout as the project.</summary>
        public System.Threading.Tasks.Task OpenHgEngineFolderInteractiveAsync() => RunRomOperationAsync(async () =>
        {
            string path = await PickHgEngineFolderAsync();
            if (path != null) await OpenHgEngineFolderCoreAsync(path);
        });

        /// <summary>Asks for an hg-engine checkout; null when none was chosen or the folder isn't one.</summary>
        private async System.Threading.Tasks.Task<string> PickHgEngineFolderAsync()
        {
            IReadOnlyList<IStorageFolder> folders = await StorageProvider.OpenFolderPickerAsync(new global::Avalonia.Platform.Storage.FolderPickerOpenOptions
            {
                Title = "Open hg-engine folder", AllowMultiple = false
            });
            string path = folders != null && folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
            if (string.IsNullOrEmpty(path)) return null;
            if (HgEngineProject.LooksLikeCheckout(path)) return path;
            await DialogHelper.ShowErrorAsync("That folder isn't an hg-engine checkout (no Makefile, data/ and armips/ at its root).", "Open hg-engine folder", this);
            return null;
        }

        /// <summary>
        /// Opens an hg-engine checkout as the project: checks what its make would fail on, takes a rom.nds when it
        /// has none, builds once when base/ doesn't exist yet, then opens base/ with DSPRE's folders in .dspre/.
        /// </summary>
        /// <param name="askToClose">False when the user has just chosen this folder over the project open now, so
        /// only unsaved edits are asked about.</param>
        private async System.Threading.Tasks.Task OpenHgEngineFolderCoreAsync(string checkout, bool askToClose = true)
        {
            checkout = checkout.TrimEnd('\\', '/');
            const string title = "Open hg-engine folder";
            if (!HgEngineFolder.IsDsRomCheckout(checkout))
            {
                await DialogHelper.ShowErrorAsync("This hg-engine checkout is older than its ds-rom build. Update it to the current hg-engine, then open it again.", title, this);
                return;
            }
            List<string> problems = HgEngineFolder.Problems(checkout);
            if (problems.Count > 0)
            {
                await DialogHelper.ShowErrorAsync("This hg-engine folder can't be built yet:\n\n" + string.Join("\n\n", problems), title, this);
                return;
            }

            HgEngineShell? stored = HgEngineProject.StoredShellFor(checkout);
            if (stored == null && HgEngineProject.IsWslPath(checkout) && !HgEngineProject.HostIsPosix
                && !await DialogHelper.AskYesNoAsync(WslLinuxBuildAdvice, "This checkout is inside WSL"))
                return;

            HgEngineShell? shell = stored ?? await ViewModels.Tools.HgEngineLinkViewModel.AskShellAsync(checkout);
            if (shell == null) return;
            HgEngineProject.OpenFolder(checkout, shell.Value);
            try
            {
                List<string> missing = HgEngineFolder.MissingTools(checkout);
                if (missing == null)
                {
                    await DialogHelper.ShowErrorAsync("DSPRE couldn't start the build shell for this folder.", title, this);
                    return;
                }
                if (missing.Count > 0)
                {
                    await DialogHelper.ShowErrorAsync("hg-engine's build needs these, and the build shell can't find them:\n\n"
                        + string.Join(", ", missing) + "\n\nInstall them as hg-engine's README describes, then open the folder again.", title, this);
                    return;
                }

                if (!System.IO.File.Exists(HgEngineFolder.RomPath(checkout)) && !await ProvideRomAsync(checkout)) return;
                string code = HgEngineFolder.ReadGameCode(HgEngineFolder.RomPath(checkout));
                if (code != HgEngineFolder.GameCode)
                {
                    await DialogHelper.ShowErrorAsync($"This checkout's rom.nds is {code ?? "unreadable"}, but hg-engine builds from a HeartGold (USA) ROM ({HgEngineFolder.GameCode}).", title, this);
                    return;
                }

                if (!HgEngineFolder.HasBase(checkout))
                {
                    if (!await DialogHelper.AskYesNoAsync("This hg-engine folder hasn't been built yet. Build it now? DSPRE opens what the build extracts.", title)) return;
                    if (!await new CompileRomView().BuildAsync(this)) return;
                    if (!HgEngineFolder.HasBase(checkout))
                    {
                        await DialogHelper.ShowErrorAsync("The build finished but left no base/ folder to open.", title, this);
                        return;
                    }
                }

                bool close = askToClose
                    ? await ConfirmProjectCloseAsync()
                    : await UnsavedChangesDialog.ShowIfNeededAsync(this, OpenEditors.GetUnsavedEditors(this));
                if (!close) return;
                OpenEditors.CloseEditorWindows(this);
                string baseDir = HgEngineFolder.BaseDir(checkout);
                await LoadRomAsync(err0 =>
                {
                    bool ok = AvaloniaRomLoader.LoadFromFolder(baseDir, out string er, recordRecent: false);
                    if (ok) SettingsManager.RecordRecentProject(checkout);   // the checkout, so reopening runs these checks again
                    err0(er);
                    return ok;
                }, sourcePath: checkout);
            }
            // Every way out, including a cancel, leaves the link matching the project that is open.
            finally { HgEngineProject.Refresh(); }
        }

        private const string WslLinuxBuildAdvice =
            "Every file DSPRE reads crosses from Windows into WSL, which is slow, and hg-engine already builds inside WSL. "
            + "Running DSPRE's Linux version inside WSL avoids that. In a WSL terminal:\n\n"
            + "mkdir -p ~/dspre && cd ~/dspre\n"
            + "curl -LO https://github.com/DS-Pokemon-Rom-Editor/DSPRE/releases/download/canary-avalonia/DSPRE-Avalonia-linux-x64-canary.tar.gz\n"
            + "tar -xzf DSPRE-Avalonia-linux-x64-canary.tar.gz\n"
            + "chmod +x DSPRE.Avalonia\n"
            + "./DSPRE.Avalonia\n\n"
            + "Continue here on Windows anyway?";

        /// <summary>Asks for a HeartGold (USA) ROM and copies it into the checkout as rom.nds. False when none was given.</summary>
        private async System.Threading.Tasks.Task<bool> ProvideRomAsync(string checkout)
        {
            if (!await DialogHelper.AskYesNoAsync("This hg-engine folder has no rom.nds yet. hg-engine builds from a HeartGold (USA) ROM. "
                + "Choose one now? DSPRE copies it into the folder as rom.nds and leaves your file where it is.", "Open hg-engine folder"))
                return false;
            IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new global::Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = "Choose a HeartGold (USA) ROM", AllowMultiple = false,
                FileTypeFilter = new[] { new global::Avalonia.Platform.Storage.FilePickerFileType("Nintendo DS ROM") { Patterns = new[] { "*.nds" } } },
            });
            string rom = files != null && files.Count > 0 ? files[0].TryGetLocalPath() : null;
            if (string.IsNullOrEmpty(rom)) return false;
            string error = HgEngineFolder.ProvideRom(checkout, rom);
            if (error != null)
            {
                await DialogHelper.ShowErrorAsync(error, "Open hg-engine folder", this);
                return false;
            }
            if (HgEngineFolder.Sha1Of(HgEngineFolder.RomPath(checkout)) != HgEngineFolder.CleanRetailSha1)
                await DialogHelper.ShowInfoAsync("This ROM isn't a clean HeartGold (USA) dump. hg-engine builds on top of whatever it already holds.", "Open hg-engine folder");
            return true;
        }

        /// <summary>Open a recent-projects entry: a .nds file or an extracted folder.</summary>
        public System.Threading.Tasks.Task OpenRecentAsync(string path) => RunRomOperationAsync(() => OpenRecentCoreAsync(path));

        private async System.Threading.Tasks.Task OpenRecentCoreAsync(string path)
        {
            if ((System.IO.File.Exists(path) || System.IO.Directory.Exists(path)) && await RefuseUnsupportedAsync(path)) return;

            if (System.IO.File.Exists(path))
            {
                bool? reExtract = await CheckExtractedDataChoiceAsync(path);
                if (reExtract == null) return;   // user aborted
                if (!await ConfirmProjectCloseAsync()) return;
                OpenEditors.CloseEditorWindows(this);
                await LoadRomAsync(err0 => { bool ok = AvaloniaRomLoader.LoadFromFile(path, out string er, reExtract.Value); err0(er); return ok; }, sourcePath: path);
            }
            else if (System.IO.Directory.Exists(path) && CheckoutFor(path) is string checkout)
            {
                await OpenHgEngineFolderCoreAsync(checkout);
            }
            else if (System.IO.Directory.Exists(path))
            {
                if (!await ConfirmProjectCloseAsync()) return;
                OpenEditors.CloseEditorWindows(this);
                await LoadRomAsync(err0 => { bool ok = AvaloniaRomLoader.LoadFromFolder(path, out string er); err0(er); return ok; }, sourcePath: path);
            }
            else
            {
                SettingsManager.RemoveRecentProject(path);
                if (string.Equals(SettingsManager.Settings.OpenDefaultRom, path, System.StringComparison.OrdinalIgnoreCase))
                {
                    SettingsManager.Settings.OpenDefaultRom = "";
                    SettingsManager.Save();
                }
                (DataContext as MainWindowViewModel)?.RefreshRecents();
                await DialogHelper.ShowErrorAsync("This project no longer exists and was removed from the recent list:\n" + path, "Open recent");
            }
        }

        /// <summary>True for a path served over WSL's network redirector (\\wsl.localhost\... or \\wsl$\...), where every file operation pays extra latency compared to a local drive.</summary>
        private static bool IsWslPath(string path) =>
            path != null && (path.StartsWith(@"\\wsl.localhost\", System.StringComparison.OrdinalIgnoreCase)
                           || path.StartsWith(@"\\wsl$\", System.StringComparison.OrdinalIgnoreCase));

        // An archive replaced while DSPRE was closed would lose to its older unpacked folder at the next save.
        private static async System.Threading.Tasks.Task SettleChangedArchivesAsync(MainWindowViewModel vm)
        {
            if (vm != null)
            {
                vm.BusyText = "Checking unpacked archives…";
                vm.BusyHint = "";
                vm.IsBusy = true;
            }
            try { await BusyOverlay.RunLockedAsync(() => NarcSync.Settle()); }
            catch (System.Exception ex) { AppLogger.Error("Checking unpacked archives failed: " + ex.Message); }
            finally { if (vm != null) vm.IsBusy = false; }
        }

        // Runs a ROM load off the UI thread (unpacking blocks), then refreshes the menus/title and reports errors.
        // sourcePath: the picked .nds/folder, used only to detect a WSL path for the busy hint.
        private async System.Threading.Tasks.Task LoadRomAsync(System.Func<System.Action<string>, bool> load, string sourcePath = null)
        {
            MainWindowViewModel vm = DataContext as MainWindowViewModel;
            string openBefore = RomInfo.WorkDir, claimedBefore = ProjectWindows.HeldPath;
            if (!ProjectWindows.TryClaim(sourcePath))
            {
                await DialogHelper.ShowInfoAsync("This project is already open in another DSPRE window.", "Open ROM");
                return;
            }
            if (vm != null)
            {
                vm.BusyText = "Opening ROM…";
                vm.BusyHint = IsWslPath(sourcePath)
                    ? "Projects in WSL load slower"
                    : "";
                vm.IsBusy = true;
            }
            string error = null;
            bool ok;
            try
            {
                ok = await BusyOverlay.RunLockedAsync(() => load(e => error = e));
            }
            finally
            {
                if (vm != null) vm.IsBusy = false;
            }
            vm?.RefreshRomState();
            if (!ok)
            {
                ProjectWindows.Release();
                // A load that fails early leaves the previous project open, so it keeps its claim.
                if (claimedBefore != null && openBefore != null && RomInfo.WorkDir == openBefore) ProjectWindows.TryClaim(claimedBefore);
                if (vm != null) vm.StatusText = "ROM load failed.";
                await DialogHelper.ShowErrorAsync(error ?? "Couldn't load the ROM.", "Open ROM");
                return;
            }
            if (vm != null) vm.StatusText = $"Loaded {RomInfo.ProjectName ?? "project"}";
            ProjectSourceWatcher.Start();
            await SettleChangedArchivesAsync(vm);

            // Nothing else dismisses the welcome window, and a loaded project makes it redundant.
            if (global::Avalonia.Application.Current?.ApplicationLifetime
                is global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                foreach (WelcomeView w in System.Linq.Enumerable.ToList(
                             System.Linq.Enumerable.OfType<WelcomeView>(desktop.Windows)))
                    w.Close();
            }

            RefreshGameIcon();

            // Whatever the last ROM needed switching on, this one has to earn for itself.
            BetaEditors.ForgetWhatWasNeeded();
            vm?.RefreshHgEngineState();

            string offerFolder = RomInfo.IsHGE ? await HandleHgEngineDetectedAsync() : null;
            if (offerFolder == "") offerFolder = await PickHgEngineFolderAsync();
            // Switching straight away keeps this ROM's own setup prompts from coming up for a project being left.
            if (!string.IsNullOrEmpty(offerFolder))
            {
                await OpenHgEngineFolderCoreAsync(offerFolder, askToClose: false);
                return;
            }
            // The Maps workspace skipped its setup at boot (no ROM yet); run it now.
            await Maps.EnsureSetupAsync();
            // First successful ROM load ever: ask once everything is loaded, so the tour starts straight away.
            if (SettingsManager.Settings?.GuidedTourShown == false)
            {
                SettingsManager.Settings.GuidedTourShown = true;
                SettingsManager.Save();
                if (await DialogHelper.AskTwoWayAsync(
                    "A short tour shows the main window and the editor menus. Tools > Guided tour shows it again later.",
                    "New to DSPRE?", "Show me around", "Not now"))
                    GuidedTour.Start(this);
            }
        }

        /// <summary>
        /// Leftover members in a/0/2/8 shift every hg-engine table without crashing (wrong icon colours,
        /// hidden abilities, TM learnsets), so this warns on load.
        /// </summary>
        private async System.Threading.Tasks.Task WarnIfCodeTablesShiftedAsync()
        {
            try
            {
                HgEngineCodeAddons.Layout layout = await System.Threading.Tasks.Task.Run(() => HgEngineCodeAddons.Describe());
                if (layout == null || layout.IsHealthy || layout.TableBlockStart < 0) return;

                // The fix is a beta editor, so a real fault switches it on.
                BetaEditors.AllowBecauseNeeded("HgeRomReviewView");
                (DataContext as MainWindowViewModel)?.RefreshHgEngineState();

                await DialogHelper.ShowInfoAsync(
                    layout.Summary + "\n\nFile > hg-engine > hg-engine ROM review shows which member holds what, and "
                    + "can repair the order. It has been switched on for this ROM because it is needed.",
                    "hg-engine tables are out of place");
            }
            catch (System.Exception ex) { AppLogger.Error("WarnIfCodeTablesShiftedAsync: " + ex.Message); }
        }

        /// <summary>
        /// Handles an hg-engine ROM on load. An hg-engine folder project is already set up. A ROM opened on its
        /// own keeps the editors hg-engine owns disabled and offers its hg-engine folder instead: the checkout it sits in,
        /// or an empty string when the user is to pick one, for the caller to open once this load has finished.
        /// </summary>
        private async System.Threading.Tasks.Task<string> HandleHgEngineDetectedAsync()
        {
            await WarnIfCodeTablesShiftedAsync();
            if (RomInfo.IsHgEngineBaseProject) return null;

            string beside = CheckoutBesideProject();
            if (beside != null && HgEngineFolder.IsDsRomCheckout(beside))
            {
                bool openBeside = await DialogHelper.AskYesNoAsync(
                    $"This ROM was built in the hg-engine folder {beside}. Opened as a ROM, the editors for data hg-engine "
                    + "builds from its own source stay disabled.\n\nOpen the hg-engine folder instead?",
                    "hg-engine ROM");
                return openBeside ? beside : null;
            }
            bool pick = await DialogHelper.AskYesNoAsync(
                "This is an hg-engine ROM. Opened as a ROM, the editors for data hg-engine builds from its own source stay "
                + "disabled, and edits made here aren't in the hg-engine source.\n\nOpen its hg-engine folder instead?",
                "hg-engine ROM");
            return pick ? "" : null;
        }

        private static string CheckoutBesideProject()
        {
            try
            {
                string parent = System.IO.Directory.GetParent(RomInfo.WorkDir.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar))?.FullName;
                return HgEngineProject.LooksLikeCheckout(parent) ? parent : null;
            }
            catch (System.Exception ex) { AppLogger.Error("CheckoutBesideProject: " + ex.Message); return null; }
        }

        private void NoteOutsideScriptCompile(RotomTool.Result result, IReadOnlyList<string> paths)
        {
            if (DataContext is not MainWindowViewModel vm) return;
            string names = string.Join(", ", paths.Select(System.IO.Path.GetFileName).Distinct());
            if (result.Success) { vm.StatusText = "Compiled " + names + " after an outside edit."; return; }
            List<string> failures = RotomTool.FailureLines(result);
            vm.StatusText = "Didn't compile: " + failures[0] + (failures.Count > 1 ? $" (and {failures.Count - 1} more)" : "");
        }

        private async void SaveRom_Click(object sender, RoutedEventArgs e) => await SaveRomAsync();

        /// <summary>Builds a playable .nds from the current project. Public so other embedded views
        /// (e.g. the Maps workspace's own "Save ROM" button) can trigger the exact same flow as the
        /// File menu, with the same busy overlay and result dialogs.</summary>
        public System.Threading.Tasks.Task SaveRomAsync() => RunRomOperationAsync(SaveRomCoreAsync);

        private async System.Threading.Tasks.Task SaveRomCoreAsync()
        {
            if (!AvaloniaEditorLauncher.IsRomLoaded) return;
            // Anything unsaved in an open editor would otherwise be missing from the build.
            if (!await UnsavedChangesDialog.ShowIfNeededAsync(this, OpenEditors.GetUnsavedEditors(this))) return;
            if (RomInfo.IsHgEngineBaseProject)
            {
                if (await BuildHgEngineFolderAsync(HgEngineProject.BuildRomName)) await OfferPatchCreditsAsync();
                return;
            }
            IStorageFile file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save ROM",
                DefaultExtension = "nds",
                SuggestedFileName = (RomInfo.ProjectName ?? "rom") + ".nds",
                FileTypeChoices = new[] { new FilePickerFileType("NDS ROM") { Patterns = new[] { "*.nds" } } }
            });
            string path = file?.TryGetLocalPath();
            if (string.IsNullOrEmpty(path)) return;
            if (await BuildRomAsync(path)) await OfferPatchCreditsAsync();
        }

        /// <summary>Once per project, after a save, offers credits for the patches it has applied or found.</summary>
        private async System.Threading.Tasks.Task OfferPatchCreditsAsync()
        {
            if (!PatchToolboxLogic.CreditsOfferDue()) return;
            List<string> keys;
            try { keys = await System.Threading.Tasks.Task.Run(() => PatchToolboxLogic.AppliedCreditKeys(PatchToolboxLogic.GetPatchStatuses())); }
            catch (System.Exception ex) { AppLogger.Warn("Patch credits check failed: " + ex.Message); return; }
            if (keys.Count == 0) return;

            (bool generate, bool stopAsking) = await DialogHelper.AskWithCheckAsync(
                "This ROM has patches applied but no credits generated for them yet. Do you want DSPRE to generate template credits? " +
                "You can also generate them any time from the ROM patch toolbox.",
                "Patch credits", "Generate credits", "Not now", "Don't ask again", isChecked: true, owner: this);
            if (generate || stopAsking) PatchToolboxLogic.MarkCreditsHandled();
            if (generate) await DialogHelper.ShowCopyableTextAsync(PatchToolboxLogic.CreditsText(keys), "Credits", this);
        }

        /// <summary>Repacks the project into <paramref name="path"/>, reporting failure itself. True when built.</summary>
        private async System.Threading.Tasks.Task<bool> BuildRomAsync(string path)
        {
            await OfferToClearArm9CompressionMarkAsync();
            string error = null;
            bool ok = await WriteProjectFilesAsync("Saving ROM…", "Repacking the project into a playable .nds file.",
                () => DSUtils.RepackROM(path), e => error = e);
            MainWindowViewModel vm = DataContext as MainWindowViewModel;
            if (ok)
            {
                if (vm != null) vm.StatusText = "ROM built: " + path;
                AppLogger.Info("ROM built successfully: " + path);
                return true;
            }
            if (vm != null) vm.StatusText = "ROM build failed.";
            if (error != null) await DialogHelper.ShowErrorAsync(error, "Save ROM", this);
            return false;
        }

        /// <summary>
        /// For an hg-engine folder project: writes the project's files into base/, then builds with make, so the
        /// ROM is what hg-engine's own build makes of them. True when the ROM was built.
        /// </summary>
        private async System.Threading.Tasks.Task<bool> BuildHgEngineFolderAsync(string buildRom)
        {
            string error = null;
            string checkout = HgEngineProject.RepoRootWindows;
            bool ok = await WriteProjectFilesAsync("Building with hg-engine…", "Writing the project's files into base/ for make.",
                () => { HgEngineBuildCache.Invalidate(checkout); return true; }, e => error = e);
            if (!ok)
            {
                if (error != null) await DialogHelper.ShowErrorAsync(error, "Build", this);
                return false;
            }
            Dictionary<RomInfo.DirNames, string> before = await System.Threading.Tasks.Task.Run(HgEngineFolder.SnapshotUnpackedArchives);
            bool built = await new CompileRomView().BuildAsync(this, buildRom);
            // What make rebuilt replaces DSPRE's copies, so open editors and the next save start from the build.
            int refreshed = built ? (await System.Threading.Tasks.Task.Run(() => HgEngineFolder.RefreshRebuiltArchives(before))).Count : 0;
            if (DataContext is MainWindowViewModel vm)
                vm.StatusText = built ? "Built " + (buildRom ?? "test.nds") + " with make." + (refreshed > 0 ? $" {refreshed} archives reloaded from the build." : "")
                    : "The hg-engine build failed.";
            return built;
        }

        /// <summary>
        /// Compiles changed scripts, rebuilds text and script binaries and repacks every unpacked archive into the
        /// project, then runs <paramref name="finish"/>. Reports a script compile failure itself; any other failure
        /// is handed to <paramref name="fail"/>.
        /// </summary>
        private async System.Threading.Tasks.Task<bool> WriteProjectFilesAsync(string busyText, string busyHint,
            System.Func<bool> finish, System.Action<string> fail)
        {
            MainWindowViewModel vm = DataContext as MainWindowViewModel;
            if (vm != null)
            {
                vm.BusyText = busyText;
                vm.BusyHint = busyHint;
                vm.IsBusy = true;
            }
            string error = null;
            bool ok;
            try
            {
                // Catches source edits made while DSPRE was closed.
                if (RomInfo.HasRotomProject && RotomTool.IsAvailable)
                {
                    if (vm != null) vm.BusyHint = "Compiling changed scripts.";
                    RotomTool.Result compiled = await RotomTool.CompileProjectAsync();
                    if (!compiled.Success)
                    {
                        if (vm != null) { vm.IsBusy = false; vm.StatusText = "ROM build stopped: scripts didn't compile."; }
                        await DialogHelper.ShowErrorAsync("These scripts didn't compile, so no ROM was written:\n\n"
                                                     + string.Join("\n", RotomTool.FailureLines(compiled))
                                                     + "\n\nFix them and save again.",
                                                     "Save ROM", RotomTool.FormatDetails(compiled));
                        return false;
                    }
                    if (vm != null) vm.BusyHint = busyHint;
                }

                // Every folder is packed over its archive below, so an archive changed outside DSPRE is settled first.
                NarcSync.Outcome archives = await BusyOverlay.RunLockedAsync(() => NarcSync.Settle(saving: true));
                string doing = busyText.TrimEnd('…');
                if (!archives.Settled)
                {
                    if (vm != null) { vm.IsBusy = false; vm.StatusText = doing + " cancelled. Nothing was written."; }
                    return false;
                }
                if (archives.Unpacked.Count > 0)
                {
                    if (vm != null) vm.IsBusy = false;
                    bool saveNow = await DialogHelper.AskTwoWayAsync(
                        "The newer archive was unpacked over its folder for:\n\n" + string.Join("\n", archives.Unpacked)
                        + "\n\nYou can look it over in its editors first, or carry on now.",
                        doing, "Carry on", "Review first");
                    if (!saveNow)
                    {
                        if (vm != null) vm.StatusText = doing + " paused so the unpacked archives can be reviewed. Nothing was written.";
                        return false;
                    }
                    if (vm != null) vm.IsBusy = true;
                }

                ok = await BusyOverlay.RunLockedAsync(() =>
                {
                    try
                    {
                        // Expanded text and script folders and every unpacked archive have to be written back
                        // before building, or edits made only to the unpacked side would be missing.
                        if (!TextArchive.BuildRequiredBins(out string textError)) { error = textError ?? "Rebuilding text archives failed."; return false; }
                        if (!ScriptFile.BuildRequiredBins(out string scriptError)) { error = scriptError ?? "Rebuilding script files failed."; return false; }

                        foreach (KeyValuePair<RomInfo.DirNames, (string packedDir, string unpackedDir)> kvp in RomInfo.GameDirs)
                        {
                            // hg-engine builds these from its own source, which is where DSPRE's edits to them go.
                            if (HgEngineDomains.IsOwned(kvp.Key)) continue;

                            DirectoryInfo di = new System.IO.DirectoryInfo(kvp.Value.unpackedDir);
                            if (di.Exists)
                                Narc.FromFolder(kvp.Value.unpackedDir).Save(kvp.Value.packedDir);
                        }

                        return finish();
                    }
                    catch (System.Exception ex) { error = ex.Message; return false; }
                });
            }
            finally
            {
                if (vm != null) vm.IsBusy = false;
            }
            if (!ok) fail(error ?? "Building the ROM failed. See the log for details.");
            return ok;
        }

        private async void BuildAndRun_Click(object sender, RoutedEventArgs e) => await BuildAndRunAsync();

        /// <summary>Builds the ROM, optionally through hg-engine's make on a linked checkout, and opens it in the chosen emulator.</summary>
        public System.Threading.Tasks.Task BuildAndRunAsync() => RunRomOperationAsync(BuildAndRunCoreAsync);

        private async System.Threading.Tasks.Task BuildAndRunCoreAsync()
        {
            if (!AvaloniaEditorLauncher.IsRomLoaded) return;
            // Anything unsaved in an open editor would otherwise be missing from the build.
            if (!await UnsavedChangesDialog.ShowIfNeededAsync(this, OpenEditors.GetUnsavedEditors(this))) return;

            (EmulatorKind Kind, string Path)? emulator = Emulators.Preferred() ?? await EmulatorPickerView.AskAsync(this);
            if (emulator == null) return;

            string rom;
            if (RomInfo.IsHgEngineBaseProject)
            {
                string name = HgEngineProject.BuildRomName;
                if (name == null)
                {
                    name = await DialogHelper.PromptTextAsync(
                        "File name for the ROM that Build and run makes. Emulators keep saves by ROM name, so keep it the same between runs.",
                        "Build and run", "test.nds", this);
                    if (name == null) return;
                    if (!HgEngineProject.IsSafeBuildRomName(name))
                    {
                        await DialogHelper.ShowErrorAsync("Use a plain file name ending in .nds, made of letters, numbers, spaces, dots, dashes or underscores.", "Build and run", this);
                        return;
                    }
                    HgEngineProject.SetBuildRomName(name);
                }
                if (!await BuildHgEngineFolderAsync(name)) return;
                rom = System.IO.Path.Combine(HgEngineProject.RepoRootWindows, name);
            }
            else
            {
                rom = BuildAndRunRomPath();
                if (!await BuildRomAsync(rom)) return;
            }

            if (!await OfferTestSaveAsync(emulator.Value.Kind, emulator.Value.Path, rom)) return;

            string error = Emulators.Launch(emulator.Value.Kind, emulator.Value.Path, rom);
            if (error != null) { await DialogHelper.ShowErrorAsync(error, "Build and run", this); return; }
            if (DataContext is MainWindowViewModel vm) vm.StatusText = $"Running {System.IO.Path.GetFileName(rom)} in {Emulators.DisplayName(emulator.Value.Kind)}.";
        }

        // Asks whether to start from the project's chosen save; false when the run is cancelled.
        private async System.Threading.Tasks.Task<bool> OfferTestSaveAsync(EmulatorKind kind, string emulatorPath, string rom)
        {
            string save = TestSaves.For(RomInfo.WorkDir);
            if (string.IsNullOrEmpty(save)) return true;
            if (!TestSaves.Supports(kind))
            {
                if (DataContext is MainWindowViewModel note)
                    note.StatusText = $"Save not copied: {Emulators.DisplayName(kind)} keeps its own saves.";
                return true;
            }
            DialogHelper.MsgResult answer = await DialogHelper.AskThreeWayAsync(
                $"Start from {System.IO.Path.GetFileName(save)}?\n\nCarry on keeps the save the emulator already has for this ROM.",
                "Build and run", "Start from it", "Carry on");
            if (answer == DialogHelper.MsgResult.Cancel) return false;
            if (answer != DialogHelper.MsgResult.Yes) return true;
            try
            {
                TestSaves.Place(kind, emulatorPath, rom, save);
                return true;
            }
            catch (System.Exception ex)
            {
                await DialogHelper.ShowErrorAsync("The save couldn't be put in place. " + ex.Message, "Build and run", this);
                return false;
            }
        }

        // A fixed name keeps the emulator's saves between runs.
        private static string BuildAndRunRomPath()
        {
            string folder = SettingsManager.Settings?.ExportPath;
            if (string.IsNullOrWhiteSpace(folder) || !System.IO.Directory.Exists(folder))
                folder = System.IO.Path.GetDirectoryName(RomInfo.WorkDir.TrimEnd('\\', '/'));
            return System.IO.Path.Combine(folder, (RomInfo.ProjectName ?? "rom") + " (DSPRE build).nds");
        }

        /// <summary>
        /// A legacy project's arm9.bin is decompressed in place when an editor first needs it, but keeps the mark
        /// that tells the game to decompress it again at boot, which stops the ROM working on hardware.
        /// </summary>
        private async System.Threading.Tasks.Task OfferToClearArm9CompressionMarkAsync()
        {
            try
            {
                if (RomInfo.IsDsRomProject || !ARM9.IsFlatButMarked()) return;
                if (await DialogHelper.AskYesNoAsync(
                        "This ROM's ARM9 is decompressed but still marked as compressed, so it won't run on a real DS.\n\n" +
                        "Fix the mark?", "ARM9 compression mark", this))
                    ARM9.ClearCompressionMark();
            }
            catch (System.Exception ex) when (ex is System.IO.IOException || ex is System.UnauthorizedAccessException)
            {
                AppLogger.Warn("ARM9 compression mark check failed: " + ex.Message);
            }
        }

        private async void ConvertDsRom_Click(object sender, RoutedEventArgs e) => await RunRomOperationAsync(ConvertDsRomAsync);

        private async System.Threading.Tasks.Task ConvertDsRomAsync()
        {
            if (!AvaloniaEditorLauncher.IsRomLoaded) return;
            if (RomInfo.IsDsRomProject)
            {
                await DialogHelper.ShowInfoAsync("This project is already in ds-rom format.", "Convert to ds-rom");
                return;
            }
            // Every path an open editor holds changes, so they close first and the project opens again after.
            if (!await UnsavedChangesDialog.ShowIfNeededAsync(this, OpenEditors.GetUnsavedEditors(this))) return;
            OpenEditors.CloseEditorWindows(this);
            RotomLanguageServerClient.StopAll();

            string folder = RomInfo.WorkDir.TrimEnd('\\', '/');
            MainWindowViewModel vm = DataContext as MainWindowViewModel;
            if (vm != null)
            {
                vm.BusyText = "Converting to ds-rom…";
                vm.BusyHint = "Backs up the project first";
                vm.IsBusy = true;
            }
            int result;
            try { result = await BusyOverlay.RunLockedAsync(() => DSUtils.ConvertNdstoolToDsRom(folder)); }
            finally { if (vm != null) vm.IsBusy = false; }

            if (result != 1) return;
            await LoadRomAsync(err0 => { bool ok = AvaloniaRomLoader.LoadFromFolder(folder, out string er, recordRecent: false); err0(er); return ok; },
                sourcePath: folder);
        }

        private void PatchToolbox_Click(object sender, RoutedEventArgs e)
        {
            try { AvaloniaEditorLauncher.OpenPatchToolbox(); }
            catch (System.Exception ex) { _ = DialogHelper.ShowErrorAsync("Couldn't open the ROM patch toolbox: " + ex.Message, "ROM patch toolbox"); }
        }

        private void ScriptCommandDatabase_Click(object sender, RoutedEventArgs e) => AvaloniaEditorLauncher.OpenScriptCommandDatabase();

        private void CustomCommandManager_Click(object sender, RoutedEventArgs e)
        {
            try { AvaloniaEditorLauncher.OpenCustomCommandManager(); }
            catch (System.Exception ex) { _ = DialogHelper.ShowErrorAsync("Couldn't open the Custom script command manager: " + ex.Message, "Custom script command manager"); }
        }

        private void AudioEditor_Click(object sender, RoutedEventArgs e)
            => _ = AvaloniaEditorLauncher.OpenAudioEditorAsync();

        private async void PokemonEditor_Click(object sender, RoutedEventArgs e)
            => await AvaloniaEditorLauncher.OpenPokemonEditorAsync();

        private void HgEngineFormEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenHgEngineFormEditor();

        private void AbilityFlags_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenAbilityFlagsEditor();

        private void MoveDataEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenMoveDataEditor();

        private void TMEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenTMEditor();

        private void EggMoveEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenEggMoveEditor();

        private void BattleScriptEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenBattleScriptEditor();

        private void MoveBackgrounds_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenMoveBackgroundEditor();

        private void ItemEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenItemEditor();

        private void MartEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenMartEditor();

        private void ItemTableEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenItemTableEditor();

        private void TradeEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenTradeEditor();

        private void StarterEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenStarterEditor();

        private void TrainerEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenTrainerEditor();

        private void TrainerSpriteEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenTrainerSpriteEditor();

        private void TrainerBackSpriteEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenTrainerBackSpriteEditor();

        private void TextEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenTextEditor();

        private void ScriptEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenScriptEditor();

        private void LevelScriptEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenLevelScriptEditor();

        private void TableEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenTableEditor();

        private void HeaderEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenHeaderEditor();

        private void CameraEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenCameraEditor();

        private void MapEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenMapEditor();

        private void BuildingEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenBuildingEditor();

        private void MatrixEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenMatrixEditor();

        private void EventEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenEventEditor();

        private void NsbtxEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenNsbtxEditor();


        // The Screens item opens the editor; its arrow lists the screens.
        private void Screens_Click(object sender, RoutedEventArgs e)
        {
            if (!ReferenceEquals(e.Source, sender)) return;
            AvaloniaEditorLauncher.OpenScreensEditor();
        }

        // A menu item with a submenu only opens the submenu, so clicking its text opens the editor directly.
        private void ScreensMenu_Released(object sender, global::Avalonia.Input.PointerReleasedEventArgs e)
        {
            if (sender is not MenuItem item || e.InitialPressMouseButton != global::Avalonia.Input.MouseButton.Left) return;
            if (e.GetPosition(item).X > item.Bounds.Width - 32) return;
            MainMenu.Close();
            AvaloniaEditorLauncher.OpenScreensEditor();
        }

        private void ScreensAt_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            if ((sender as MenuItem)?.Tag is string key) AvaloniaEditorLauncher.OpenScreensEditor(key);
        }

        private void GraphicsBrowser_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenGraphicsBrowser();

        private void ModelBrowser_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenModelBrowser();

        private void BattleSceneBrowser_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenBattleSceneBrowser();




        private void TilesetBuilder_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenTilesetBuilder();

        private void FontEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenFontEditor();

        private void AreaDataEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenAreaDataEditor();

        private void FlyWarpEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenFlyWarpEditor();

        private void DungeonCutinEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenDungeonCutinEditor();



        private void CellAnimations_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenCellAnimationPicker();

        private void Particles_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenParticleLibrary();

        private void BallCapsules_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenBallCapsuleEditor();


        private void NamingScreenEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenNamingScreenEditor();

        private void OverlayEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenOverlayEditor();

        private void OverworldEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenOverworldEditor();

        private void FriendshipChanges_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenFriendshipChanges();

        private void EncounterSlotOdds_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenEncounterSlotOdds();

        private void BreedingItems_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenBreedingItems();

        private void BerryData_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenBerryData();

        private void TypeChart_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenTypeChart();
        private void VsIntroEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenVsIntroEditor();
        private void WildIntroEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenWildIntroEditor();

        private void MoveTutors_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenMoveTutors();

        private void BpShop_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenBpShop();

        private void UndergroundMining_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenUndergroundMining();

        private void GrowthCurves_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenGrowthCurves();

        private void WildHeldItems_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenWildHeldItems();

        private void PokedexLists_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenPokedexLists();

        private void SpecialEncountersEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenSpecialEncountersEditor();

        private void WildEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenWildEditor();

        private void SpawnEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenSpawnEditor();

        private void HeaderSearch_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenHeaderSearch();

        private void VsSeekerRematchEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenVsSeekerRematchEditor();

        private void PokegearRematchEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenPokegearRematchEditor();

        private void FollowerTalk_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenFollowerTalk();

        private void PokegearPhoneBook_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenPokegearPhoneBook();

        private void TrainerFlagBulkEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenTrainerFlagBulkEditor();

        private void BattleTowerEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenBattleTowerEditor();

        private void Welcome_Click(object sender, RoutedEventArgs e)
            => WelcomeView.ShowWelcome(this);

        private void GuidedTour_Click(object sender, RoutedEventArgs e)
            => GuidedTour.Start(this);

        private async void ExportDocs_Click(object sender, RoutedEventArgs e)
        {
            if (AvaloniaEditorLauncher.Refused("ExportDocs")) return;
            string folder = await DialogHelper.OpenFolderAsync(this, "Choose where to export the docs");
            if (string.IsNullOrEmpty(folder)) return;
            string error = null;
            await System.Threading.Tasks.Task.Run(() =>
            {
                try { DocTool.ExportDocs(folder); }
                catch (System.Exception ex) { error = ex.Message; }
            });
            if (error == null) SaveNotice.ExportedTo("the docs", folder);
            else await DialogHelper.ShowErrorAsync("Exporting docs failed:\n" + error, "Export docs error");
        }

        private async void TrainerUsageCsv_Click(object sender, RoutedEventArgs e)
        {
            if (AvaloniaEditorLauncher.Refused("TrainerUsageReport")) return;
            string path = await DialogHelper.SaveFileAsync(this, "Save trainer usage report",
                new[] { DialogHelper.CsvFilter }, "TrainerUsage.csv");
            if (string.IsNullOrEmpty(path)) return;
            string error = null;
            await System.Threading.Tasks.Task.Run(() =>
            {
                try { TrainerUsageReport.Generate(path); }
                catch (System.Exception ex) { error = ex.Message; }
            });
            if (error == null) SaveNotice.Exported(path);
            else await DialogHelper.ShowErrorAsync("Generating the report failed:\n" + error, "Trainer usage CSV error");
        }

        // These file tools work without a ROM.
        private async void NarcUnpack_Click(object sender, RoutedEventArgs e) => await FileToolActions.UnpackNarcToFolderAsync(this);
        private async void NarcPack_Click(object sender, RoutedEventArgs e) => await FileToolActions.PackFolderToNarcAsync(this);
        private async void RenameByList_Click(object sender, RoutedEventArgs e) => await FileToolActions.RenameByListAsync(this);
        private async void RenameByContent_Click(object sender, RoutedEventArgs e) => await FileToolActions.RenameByContentAsync(this);
        private async void ListFromFolder_Click(object sender, RoutedEventArgs e) => await FileToolActions.ListFromFolderAsync(this);
        private async void ListFromEnum_Click(object sender, RoutedEventArgs e) => await FileToolActions.ListFromEnumAsync(this);
        private async void NsbmdAddTex_Click(object sender, RoutedEventArgs e) => await FileToolActions.AddTexturesToNsbmdAsync(this);
        private async void NsbmdRemoveTex_Click(object sender, RoutedEventArgs e) => await FileToolActions.RemoveTexturesFromNsbmdAsync(this);
        private async void NsbmdSaveTex_Click(object sender, RoutedEventArgs e) => await FileToolActions.SaveTexturesFromNsbmdAsync(this);

        private void AddressHelper_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenAddressHelper();

        private void ResearchHelper_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenResearchHelper();

        private void HgeRomReview_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenHgeRomReview();

        private void DistortionWorld_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenDistortionWorldEditor();

        private void CharMapManager_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenCharMapManager();

        private void LabelEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenLabelEditor();

        private void ProjectChecks_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenProjectChecks();

        private void Settings_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenSettings();

        private void About_Click(object sender, RoutedEventArgs e) => _ = DialogHelper.ShowInfoAsync(
            "DS Pokémon ROM Editor Reloaded by AdAstra, Mixone, Kuha, Yako & Kalaay\n"
            + "Version " + AppInfo.GetDSPREVersion() + "\n\n"
            + "Icons by SkidMarc25.\n\n"
            + "Built on these tools: ds-rom by AetiasHax, rotom by Kalaay, chatot by Yako, apicula by scurest "
            + "and BLZ by CUE. Parts of the image code come from Pleonex's Tinke.\n\n"
            + "Based on Nømura's DS Pokémon ROM Editor 1.0.4.\n"
            + "Largely inspired by Markitus95's \"Spiky's DS Map Editor\" (SDSME), from which certain assets were also reused.\n"
            + "Credits go to Markitus, Ark, Zark, Florian, and everyone else who deserves credit for SDSME.\n\n"
            + "Special thanks to Trifindo, Mikelan98, turtleisaac, JackHack96, Pleonex and BagBoy.\n"
            + "Their help, research and expertise in many fields of NDS ROM hacking made the development of this tool possible.",
            "About");

        private void HgEnginePatches_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenHgEnginePatches();

        private void HgEngineSettings_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenHgEngineSettings();

        private void BattleTests_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenBattleTests();

        private async void CompileRom_Click(object sender, RoutedEventArgs e) => await RunRomOperationAsync(CompileRomCoreAsync);

        private async System.Threading.Tasks.Task CompileRomCoreAsync()
        {
            if (!HgEngineProject.IsActive) return;
            // make builds from base/, so editors and DSPRE's unpacked copies are written there first, as Save ROM does.
            if (!await UnsavedChangesDialog.ShowIfNeededAsync(this, OpenEditors.GetUnsavedEditors(this))) return;
            if (RomInfo.IsHgEngineBaseProject) { await BuildHgEngineFolderAsync(null); return; }
            await new CompileRomView().ShowAndRunAsync(this);
        }

        private void ToggleTheme_Click(object sender, RoutedEventArgs e)
            => DSPRE.Avalonia.ThemeManager.Toggle();
    }
}
