using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using DSPRE.Editors;
using DSPRE.Avalonia.ViewModels;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
using NarcAPI;

namespace DSPRE.Avalonia.Views
{
    /// <summary>
    /// Avalonia MainWindow shell (preview). Hosts migrated editors as tabs and
    /// launches the remaining standalone Avalonia editor windows via the menu.
    /// All launch logic is delegated to <see cref="AvaloniaEditorLauncher"/> so the
    /// behaviour stays identical to the WinForms main window.
    /// </summary>
    public partial class MainWindowView : Window
    {
        private bool _closeConfirmed;

        public MainWindowView()
        {
            InitializeComponent();
            // Ctrl+P → quick-open command palette (jump to any editor by name).
            KeyDown += (s, e) =>
            {
                if (e.Key == global::Avalonia.Input.Key.P &&
                    e.KeyModifiers.HasFlag(global::Avalonia.Input.KeyModifiers.Control))
                {
                    AvaloniaEditorLauncher.OpenCommandPalette(this);
                    e.Handled = true;
                }
            };

            RecentMenu.SubmenuOpened += (_, _) => RebuildRecentMenu();

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
                if (!await ConfirmProjectCloseAsync()) return;
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
                var (icon, title) = ViewModels.GameBannerUi.TryLoad();
                if (icon == null) return;
                GameIconImage.Source = icon;
                GameIconImage.IsVisible = true;
                global::Avalonia.Controls.ToolTip.SetTip(GameIconImage,
                    string.IsNullOrWhiteSpace(title) ? RomInfo.projectName : title);
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
        {
            if (!AvaloniaEditorLauncher.IsRomLoaded) return;
            if (!RomInfo.IsDsRomProject)
            {
                await DialogHelper.ShowInfo(
                    "Editing the game icon and banner titles requires a ds-rom-format project.\n" +
                    "Use File → Convert to ds-rom format, then reopen this editor.",
                    "ds-rom project required");
                return;
            }
            new BannerEditorView(new ViewModels.BannerEditorViewModel()).Show();
        }

        // ── Window placement persistence (size + maximized; centered by the OS otherwise) ──
        private void RestoreWindowPlacement()
        {
            var s = SettingsManager.Settings;
            if (s == null) return;
            if (s.mainWindowWidth >= MinWidth && s.mainWindowHeight >= MinHeight)
            {
                Width = s.mainWindowWidth;
                Height = s.mainWindowHeight;
            }
            if (s.mainWindowMaximized) WindowState = WindowState.Maximized;
        }

        private void SaveWindowPlacement()
        {
            var s = SettingsManager.Settings;
            if (s == null) return;
            s.mainWindowMaximized = WindowState == WindowState.Maximized;
            if (WindowState == WindowState.Normal)
            {
                s.mainWindowWidth = Width;
                s.mainWindowHeight = Height;
            }
            SettingsManager.Save();
        }

        // ── Recent projects submenu (rebuilt each time it opens) ─────────────
        private void RebuildRecentMenu()
        {
            RecentMenu.Items.Clear();
            var recents = SettingsManager.Settings?.recentProjects;
            if (recents == null || recents.Count == 0)
            {
                RecentMenu.Items.Add(new MenuItem { Header = "(no recent projects)", IsEnabled = false });
                return;
            }
            foreach (var path in recents)
            {
                var item = new MenuItem { Header = CompactPath(path), Tag = path };
                global::Avalonia.Controls.ToolTip.SetTip(item, path);
                item.Click += async (_, _) => await OpenRecentAsync((string)item.Tag);
                RecentMenu.Items.Add(item);
            }
        }

        /// <summary>Prompts the user about unsaved work across every open editor. Returns true if they
        /// saved/discarded (or there was nothing to lose). Does NOT close any editor windows — callers
        /// close them only once the new project is actually chosen, so cancelling the file picker or a
        /// preflight prompt doesn't leave the current project editor-less.</summary>
        private async System.Threading.Tasks.Task<bool> ConfirmProjectCloseAsync()
        {
            var editors = OpenEditors.GetUnsavedEditors(this);
            return await UnsavedChangesDialog.ShowIfNeededAsync(this, editors);
        }

        private static string CompactPath(string path)
        {
            string name = System.IO.Path.GetFileName(path.TrimEnd('\\', '/'));
            string parent = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(path.TrimEnd('\\', '/')) ?? "");
            return string.IsNullOrEmpty(parent) ? name : parent + System.IO.Path.DirectorySeparatorChar + name;
        }

        private void CommandPalette_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenCommandPalette(this);

        public MainWindowView(MainWindowViewModel vm) : this()
        {
            DataContext = vm;
        }

        public IEnumerable<(string EditorName, IEditorWithUnsavedChanges Editor)> GetEmbeddedEditors()
            => Maps?.GetEmbeddedEditors()
                ?? System.Linq.Enumerable.Empty<(string, IEditorWithUnsavedChanges)>();

        // ── File ────────────────────────────────────────────────────────────
        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private async void OpenRom_Click(object sender, RoutedEventArgs e) => await OpenRomInteractiveAsync();

        private async void OpenFolder_Click(object sender, RoutedEventArgs e) => await OpenFolderInteractiveAsync();

        /// <summary>Pick and open a .nds ROM (also used by the Welcome window).</summary>
        public async System.Threading.Tasks.Task OpenRomInteractiveAsync()
        {
            if (!await ConfirmProjectCloseAsync()) return;

            var files = await StorageProvider.OpenFilePickerAsync(new global::Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = "Open ROM",
                AllowMultiple = false,
                FileTypeFilter = new[] { new global::Avalonia.Platform.Storage.FilePickerFileType("NDS ROM") { Patterns = new[] { "*.nds" } } }
            });
            string path = files != null && files.Count > 0 ? files[0].TryGetLocalPath() : null;
            if (string.IsNullOrEmpty(path)) return;

            bool? reExtract = await CheckExtractedDataChoiceAsync(path);
            if (reExtract == null) return;   // user aborted
            OpenEditors.CloseEditorWindows(this);
            await LoadRom(err0 => { bool ok = AvaloniaRomLoader.LoadFromFile(path, out var er, reExtract.Value); err0(er); return ok; });
        }

        /// <summary>
        /// If existing extracted data is found for this .nds, asks whether to reuse it or re-extract (matching
        /// the WinForms "Extracted data detected" flow). Returns false = reuse, true = re-extract, null = abort.
        /// </summary>
        private async System.Threading.Tasks.Task<bool?> CheckExtractedDataChoiceAsync(string ndsPath)
        {
            int folderType = AvaloniaRomLoader.PeekFolderType(ndsPath);
            if (folderType == -1) return false;   // nothing extracted yet, nothing to ask

            string message = folderType == 0
                ? "Extracted data of this ROM has been found.\nDo you want to load it?"
                : "Extracted data of this ROM has been found, but it is of legacy type (extracted with a version of DSPRE prior to 1.15.0).\nDo you want to load it?";

            var choice = await DialogHelper.AskYesNoCancel(message, "Extracted Data Detected");
            if (choice == DialogHelper.MsgResult.Cancel) return null;
            if (choice == DialogHelper.MsgResult.Yes) return false;

            bool confirmReExtract = await DialogHelper.AskYesNo(
                "All data of this ROM will be re-extracted. Proceed?", "Existing Data Will Be Deleted");
            return confirmReExtract ? (bool?)true : null;
        }

        /// <summary>Pick and open an extracted project folder (also used by the Welcome window).</summary>
        public async System.Threading.Tasks.Task OpenFolderInteractiveAsync()
        {
            if (!await ConfirmProjectCloseAsync()) return;

            var folders = await StorageProvider.OpenFolderPickerAsync(new global::Avalonia.Platform.Storage.FolderPickerOpenOptions
            {
                Title = "Open extracted ROM folder", AllowMultiple = false
            });
            string path = folders != null && folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
            if (string.IsNullOrEmpty(path)) return;

            // Not a DSPRE project folder, but looks like an hg-engine checkout: open ITS rom.nds instead
            // and auto-link this checkout, rather than failing with "not a valid extracted ROM folder".
            if (DSUtils.GetFolderType(path) == -1 && HgEngineProject.LooksLikeCheckout(path))
            {
                string romPath = System.IO.Path.Combine(path, "rom.nds");
                if (!System.IO.File.Exists(romPath))
                {
                    await DialogHelper.ShowError(
                        "This looks like an hg-engine checkout, but it has no rom.nds at its root " +
                        "(hg-engine's own build needs one there before DSPRE can open it this way).",
                        "No rom.nds found", this);
                    return;
                }
                bool? reExtractHge = await CheckExtractedDataChoiceAsync(romPath);
                if (reExtractHge == null) return;
                OpenEditors.CloseEditorWindows(this);
                await LoadRom(err0 => { bool ok = AvaloniaRomLoader.LoadFromFile(romPath, out var er, reExtractHge.Value); err0(er); return ok; }, autoLinkHgEnginePath: path);
                return;
            }

            OpenEditors.CloseEditorWindows(this);
            await LoadRom(err0 => { bool ok = AvaloniaRomLoader.LoadFromFolder(path, out var er); err0(er); return ok; });
        }

        /// <summary>Open a recent-projects entry: a .nds file or an extracted folder.</summary>
        public async System.Threading.Tasks.Task OpenRecentAsync(string path)
        {
            if (!await ConfirmProjectCloseAsync()) return;

            if (System.IO.File.Exists(path))
            {
                bool? reExtract = await CheckExtractedDataChoiceAsync(path);
                if (reExtract == null) return;   // user aborted
                OpenEditors.CloseEditorWindows(this);
                await LoadRom(err0 => { bool ok = AvaloniaRomLoader.LoadFromFile(path, out var er, reExtract.Value); err0(er); return ok; });
            }
            else if (System.IO.Directory.Exists(path))
            {
                OpenEditors.CloseEditorWindows(this);
                await LoadRom(err0 => { bool ok = AvaloniaRomLoader.LoadFromFolder(path, out var er); err0(er); return ok; });
            }
            else
            {
                SettingsManager.RemoveRecentProject(path);
                (DataContext as MainWindowViewModel)?.RefreshRecents();
                await DialogHelper.ShowError("This project no longer exists and was removed from the recent list:\n" + path, "Open Recent");
            }
        }

        // Runs a ROM load off the UI thread (unpacking blocks), then refreshes the menus/title and reports errors.
        // autoLinkHgEnginePath: set when the user explicitly opened an hg-engine checkout folder (its rom.nds
        // was opened on their behalf) — link it immediately instead of asking, since they already chose it.
        private async System.Threading.Tasks.Task LoadRom(System.Func<System.Action<string>, bool> load, string autoLinkHgEnginePath = null)
        {
            var vm = DataContext as MainWindowViewModel;
            if (vm != null)
            {
                vm.BusyText = "Opening ROM…";
                vm.BusyHint = "First-time opens unpack the ROM and can take a little while.";
                vm.IsLoadingRom = true;
            }
            string error = null;
            bool ok;
            try
            {
                ok = await System.Threading.Tasks.Task.Run(() => load(e => error = e));
            }
            finally
            {
                if (vm != null) vm.IsLoadingRom = false;
            }
            vm?.RefreshRomState();
            if (!ok)
            {
                if (vm != null) vm.StatusText = "ROM load failed.";
                await DialogHelper.ShowError(error ?? "Failed to load the ROM.", "Open ROM");
                return;
            }
            if (vm != null) vm.StatusText = $"Loaded {RomInfo.projectName ?? "project"} from {RomInfo.workDir}";
            RefreshGameIcon();
            if (RomInfo.isHGE)
                await HandleHgEngineDetectedAsync(vm, autoLinkHgEnginePath);
            // The Maps workspace skipped its setup at boot (no ROM yet) — run it now.
            await Maps.EnsureSetupAsync();
            // First successful ROM load ever: walk the user through the UI once.
            if (SettingsManager.Settings?.guidedTourShown == false)
                GuidedTour.Start(this);
        }

        /// <summary>Handles an hg-engine ROM on load: auto-links if the caller already picked a checkout
        /// (opened it directly), reminds silently if this project was already linked in an earlier
        /// session, otherwise offers to link one now — same "no source folder" behavior as before this
        /// feature existed if the user declines.</summary>
        private async System.Threading.Tasks.Task HandleHgEngineDetectedAsync(MainWindowViewModel vm, string autoLinkHgEnginePath)
        {
            if (autoLinkHgEnginePath != null)
            {
                if (HgEngineProject.TryLink(autoLinkHgEnginePath, out string linkError))
                {
                    vm?.RefreshHgEngineState();
                    await DialogHelper.ShowInfo(
                        $"hg-engine ROM detected and linked to its checkout:\n{autoLinkHgEnginePath}\n\n" +
                        "The Pokémon, Move Data, Item, Trainer and Wild Pokémon editors now read and write " +
                        "that checkout's source directly.",
                        "hg-engine checkout linked");
                }
                else
                {
                    await DialogHelper.ShowError(linkError, "Couldn't link hg-engine checkout", this);
                }
                return;
            }

            if (HgEngineProject.IsLinked) return;   // already configured in an earlier session — banner says it all

            bool link = await DialogHelper.AskYesNo(
                "This is an hg-engine ROM. hg-engine manages the Pokémon, Move Data, Item, Trainer and " +
                "wild-encounter data itself, so those editors are disabled by default (editing the ROM " +
                "copy here would just get overwritten on hg-engine's next build).\n\n" +
                "Link this ROM's hg-engine source checkout now so those 5 editors read and write its " +
                "data/*.c directly instead?",
                "hg-engine ROM detected");

            if (!link)
            {
                await DialogHelper.ShowInfo(
                    "Continuing without a linked checkout: the Pokémon, Move Data, Item, Trainer and " +
                    "wild-encounter editors stay disabled. Link one later from File > Link hg-engine " +
                    "checkout…\n\nAlso note: text or script files that hg-engine edits will be " +
                    "overwritten if you save the ROM; manage those through hg-engine.",
                    "hg-engine detected");
                return;
            }

            string path = await DialogHelper.OpenFolder(this,
                "Select this ROM's hg-engine checkout (a WSL folder, e.g. \\\\wsl.localhost\\Ubuntu\\home\\you\\hg-engine)");
            if (string.IsNullOrEmpty(path)) return;

            if (HgEngineProject.TryLink(path, out string error))
            {
                vm?.RefreshHgEngineState();
                await DialogHelper.ShowInfo("Linked. The 5 editors above now read and write this checkout's source.", "hg-engine checkout linked");
            }
            else
            {
                await DialogHelper.ShowError(error, "Couldn't link hg-engine checkout", this);
            }
        }

        private async void SaveRom_Click(object sender, RoutedEventArgs e) => await SaveRomAsync();

        /// <summary>Builds a playable .nds from the current project. Public so other embedded views
        /// (e.g. the Maps workspace's own "Save ROM" button) can trigger the exact same flow as the
        /// File menu, with the same busy overlay and result dialogs.</summary>
        public async System.Threading.Tasks.Task SaveRomAsync()
        {
            if (!AvaloniaEditorLauncher.IsRomLoaded) return;
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save ROM",
                DefaultExtension = "nds",
                SuggestedFileName = (RomInfo.projectName ?? "rom") + ".nds",
                FileTypeChoices = new[] { new FilePickerFileType("NDS ROM") { Patterns = new[] { "*.nds" } } }
            });
            string path = file?.TryGetLocalPath();
            if (string.IsNullOrEmpty(path)) return;

            var vm = DataContext as MainWindowViewModel;
            if (vm != null)
            {
                vm.BusyText = "Saving ROM…";
                vm.BusyHint = "Repacking the project into a playable .nds file.";
                vm.IsLoadingRom = true;
            }
            string error = null;
            bool ok;
            try
            {
                ok = await System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        // Mirrors Main_Window.cs's saveRom_Click: expanded text/script folders and every
                        // touched unpacked/<dir> NARC folder have to be repacked back into their binary/
                        // packed form BEFORE building, or the build just packs whatever was already on
                        // disk (e.g. patches that only ever touch the unpacked side, like the synthetic
                        // overlay used by ARM9 Expansion/Building Rotation, would silently vanish).
                        if (!TextArchive.BuildRequiredBins()) { error = "Rebuilding text archives failed."; return false; }
                        if (!ScriptFile.BuildRequiredBins()) { error = "Rebuilding script files failed."; return false; }

                        foreach (var kvp in RomInfo.gameDirs)
                        {
                            // hg-engine-owned domains are never repacked from the unpacked-dir snapshot here:
                            // HgEngineSync already copies the real, freshly-built narc straight into packedDir
                            // on every sync, and Compile ROM's own `make` will regenerate them from source
                            // again regardless. Repacking from the unpacked dir would risk baking in a stale
                            // or DSPRE-only edit that never reached data/*.c, silently disagreeing with what
                            // Compile ROM actually produces.
                            if (HgEngineDomains.IsOwned(kvp.Key)) continue;

                            var di = new System.IO.DirectoryInfo(kvp.Value.unpackedDir);
                            if (di.Exists)
                                Narc.FromFolder(kvp.Value.unpackedDir).Save(kvp.Value.packedDir);
                        }

                        return DSUtils.RepackROM(path);        // builds the .nds from RomInfo.workDir
                    }
                    catch (System.Exception ex) { error = ex.Message; return false; }
                });
            }
            finally
            {
                if (vm != null) vm.IsLoadingRom = false;
            }
            if (vm != null) vm.StatusText = ok ? "ROM built: " + path : "ROM build failed.";
            if (ok)
            {
                AppLogger.Info("ROM built successfully: " + path);
                return;
            }

            await DialogHelper.ShowError(error ?? "Building the ROM failed. See the log for details.", "Save ROM", this);
        }

        private async void ConvertDsRom_Click(object sender, RoutedEventArgs e)
        {
            if (!AvaloniaEditorLauncher.IsRomLoaded) return;
            string folder = RomInfo.workDir;
            int result = await System.Threading.Tasks.Task.Run(() => DSUtils.ConvertNdstoolToDsRom(folder));
            if (result == 1) await DialogHelper.ShowInfo("Converted the project to ds-rom format (a backup was made).", "Convert to ds-rom");
            else await DialogHelper.ShowError("Conversion to ds-rom format failed or wasn't needed. See the log.", "Convert to ds-rom");
        }

        // ── Tools ───────────────────────────────────────────────────────────
        private void PatchToolbox_Click(object sender, RoutedEventArgs e)
        {
            // Native Avalonia toolbox over the shared PatchToolboxDialog apply-logic (identical ROM writes).
            try { AvaloniaEditorLauncher.OpenPatchToolbox(); }
            catch (System.Exception ex) { _ = DialogHelper.ShowError("Couldn't open the Patch Toolbox: " + ex.Message, "ROM Patch Toolbox"); }
        }

        private void CustomCommandManager_Click(object sender, RoutedEventArgs e)
        {
            try { AvaloniaEditorLauncher.OpenCustomCommandManager(); }
            catch (System.Exception ex) { _ = DialogHelper.ShowError("Couldn't open the Custom Command Manager: " + ex.Message, "Custom Script Command Manager"); }
        }

        // ── Pokémon ─────────────────────────────────────────────────────────
        private void PokemonEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenPokemonEditor();

        private void HgEngineFormEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenHgEngineFormEditor();

        private void MoveDataEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenMoveDataEditor();

        private void TMEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenTMEditor();

        private void EggMoveEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenEggMoveEditor();

        private void BattleScriptEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenBattleScriptEditor();

        private void ItemEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenItemEditor();

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

        private void TextEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenTextEditor();

        private void ScriptEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenScriptEditor();

        private void LevelScriptEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenLevelScriptEditor();

        private void TableEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenTableEditor();

        // ── World ───────────────────────────────────────────────────────────
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

        private void AreaDataEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenAreaDataEditor();

        private void FlyWarpEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenFlyWarpEditor();

        private void DungeonCutinEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenDungeonCutinEditor();

        private void TitleScreenEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenTitleScreenEditor();

        private void TrainerCardEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenTrainerCardEditor();

        private void OverlayEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenOverlayEditor();

        private void OverworldEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenOverworldEditor();

        private void EncountersEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenEncountersEditor();

        private void HeadbuttEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenHeadbuttEncounterEditor();

        private void WildEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenWildEditor();

        private void SpawnEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenSpawnEditor();

        private void HeaderSearch_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenHeaderSearch();

        private void Welcome_Click(object sender, RoutedEventArgs e)
            => WelcomeView.ShowWelcome(this);

        private void GuidedTour_Click(object sender, RoutedEventArgs e)
            => GuidedTour.Start(this);

        // Quick-open buttons in the pre-ROM empty state (item DataContext = the full path).
        private async void RecentQuick_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is string path)
                await OpenRecentAsync(path);
        }

        private async void ExportDocs_Click(object sender, RoutedEventArgs e)
        {
            if (!AvaloniaEditorLauncher.IsRomLoaded) return;
            string folder = await DialogHelper.OpenFolder(this, "Choose where to export the docs");
            if (string.IsNullOrEmpty(folder)) return;
            string error = null;
            await System.Threading.Tasks.Task.Run(() =>
            {
                try { DocTool.ExportDocs(folder); }
                catch (System.Exception ex) { error = ex.Message; }
            });
            if (error == null) await DialogHelper.ShowInfo("Docs exported to:\n" + folder, "Export Docs");
            else await DialogHelper.ShowError("Exporting docs failed:\n" + error, "Export Docs");
        }

        private async void TrainerUsageCsv_Click(object sender, RoutedEventArgs e)
        {
            if (!AvaloniaEditorLauncher.IsRomLoaded) return;
            string path = await DialogHelper.SaveFile(this, "Save trainer usage report",
                new[] { DialogHelper.CsvFilter }, "TrainerUsage.csv");
            if (string.IsNullOrEmpty(path)) return;
            string error = null;
            await System.Threading.Tasks.Task.Run(() =>
            {
                try { TrainerUsageReport.Generate(path); }
                catch (System.Exception ex) { error = ex.Message; }
            });
            if (error == null) await DialogHelper.ShowInfo("Report saved to:\n" + path, "Trainer Usage CSV");
            else await DialogHelper.ShowError("Generating the report failed:\n" + error, "Trainer Usage CSV");
        }

        // ── Standalone file tools (no ROM required) ─────────────────────────
        private async void NarcUnpack_Click(object sender, RoutedEventArgs e) => await FileToolActions.UnpackNarcToFolder(this);
        private async void NarcPack_Click(object sender, RoutedEventArgs e) => await FileToolActions.PackFolderToNarc(this);
        private async void NsbmdAddTex_Click(object sender, RoutedEventArgs e) => await FileToolActions.AddTexturesToNsbmd(this);
        private async void NsbmdRemoveTex_Click(object sender, RoutedEventArgs e) => await FileToolActions.RemoveTexturesFromNsbmd(this);
        private async void NsbmdSaveTex_Click(object sender, RoutedEventArgs e) => await FileToolActions.SaveTexturesFromNsbmd(this);

        // ── Tools ───────────────────────────────────────────────────────────
        private void AddressHelper_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenAddressHelper();

        private void ResearchHelper_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenResearchHelper();

        private void CharMapManager_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenCharMapManager();

        private void LabelEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenLabelEditor();

        private void ProjectChecks_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenProjectChecks();

        private void Settings_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenSettings();

        private void LinkHgEngine_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenHgEngineLink();

        private async void CompileRom_Click(object sender, RoutedEventArgs e)
        {
            if (!HgEngineProject.IsActive) return;
            await new CompileRomView().ShowAndRunAsync(this);
        }

        private void ToggleTheme_Click(object sender, RoutedEventArgs e)
            => DSPRE.Avalonia.ThemeManager.Toggle();

        private void GlTest_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenGlTest();
    }
}
