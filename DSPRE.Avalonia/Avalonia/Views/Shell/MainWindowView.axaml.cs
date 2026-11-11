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
using DSPRE.Avalonia.Data;

namespace DSPRE.Avalonia.Views.Shell
{
    /// <summary>
    /// The main window. Menus open editors through <see cref="AvaloniaEditorLauncher"/>.
    /// </summary>
    public partial class MainWindowView : Window
    {
        private bool _closeConfirmed;

        internal ProjectActions Project { get; }

        public MainWindowView()
        {
            InitializeComponent();
            Project = new ProjectActions(this);
            // Coming back to DSPRE is when a teammate's push is most likely waiting.
            Activated += async (_, _) => { if (DataContext is MainWindowViewModel m) await m.Changes.FetchQuietlyAsync(); };
            Opened += (_, _) => { if (DataContext is MainWindowViewModel m) Shell.ChangesPanelWindow.Attach(this, m.Changes, ChangesOverlay); };
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
        internal void RefreshGameIcon()
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
            rom.Click += async (_, _) => { if (await Project.PickRomAsync() is string p) ProjectWindows.OpenInNewWindow(p); };
            MenuItem folder = new MenuItem { Header = "Extracted folder…" };
            folder.Click += async (_, _) => { if (await Project.PickFolderAsync() is string p) ProjectWindows.OpenInNewWindow(p); };
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
        internal async System.Threading.Tasks.Task<bool> ConfirmProjectCloseAsync(bool openingAnother = true)
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

        private void TrackChanges_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainWindowViewModel vm) TrackingActions.ShowSetup(this, vm.Changes);
        }

        private async void ApplyPatch_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainWindowViewModel vm) await TrackingActions.ApplyPatchAsync(this, vm.Changes);
        }

        private async void UndoApply_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainWindowViewModel vm) await TrackingActions.UndoApplyAsync(this, vm.Changes);
        }

        private async void ExportPatch_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainWindowViewModel vm) await TrackingActions.ExportPatchAsync(this, vm.Changes);
        }

        private async void OpenRom_Click(object sender, RoutedEventArgs e) => await OpenRomInteractiveAsync();

        private async void OpenFolder_Click(object sender, RoutedEventArgs e) => await OpenFolderInteractiveAsync();

        private async void OpenHgEngineFolder_Click(object sender, RoutedEventArgs e) => await OpenHgEngineFolderInteractiveAsync();

        /// <summary>Pick and open a .nds ROM (also used by the Welcome window).</summary>
        public System.Threading.Tasks.Task OpenRomInteractiveAsync() => RunRomOperationAsync(Project.OpenRomCoreAsync);

        /// <summary>Pick and open an extracted project folder (also used by the Welcome window).</summary>
        public System.Threading.Tasks.Task OpenFolderInteractiveAsync() => RunRomOperationAsync(Project.OpenFolderCoreAsync);

        /// <summary>Pick and open an hg-engine checkout as the project.</summary>
        public System.Threading.Tasks.Task OpenHgEngineFolderInteractiveAsync() => RunRomOperationAsync(async () =>
        {
            string path = await Project.PickHgEngineFolderAsync();
            if (path != null) await Project.OpenHgEngineFolderCoreAsync(path);
        });

        /// <summary>Open a recent-projects entry: a .nds file or an extracted folder.</summary>
        public System.Threading.Tasks.Task OpenRecentAsync(string path) => RunRomOperationAsync(() => Project.OpenRecentCoreAsync(path));

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
        public System.Threading.Tasks.Task SaveRomAsync() => RunRomOperationAsync(Project.SaveRomCoreAsync);

        public System.Threading.Tasks.Task<bool> BuildRomAsync(string path) => Project.BuildRomAsync(path);

        private async void BuildAndRun_Click(object sender, RoutedEventArgs e) => await BuildAndRunAsync();

        /// <summary>Builds the ROM, optionally through hg-engine's make on a linked checkout, and opens it in the chosen emulator.</summary>
        public System.Threading.Tasks.Task BuildAndRunAsync() => RunRomOperationAsync(Project.BuildAndRunCoreAsync);

        private async void ConvertDsRom_Click(object sender, RoutedEventArgs e) => await RunRomOperationAsync(Project.ConvertDsRomAsync);

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

        private void HgEngineTables_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenHgEngineTables();

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

        private void ShinyOdds_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenShinyOdds();

        private void PokedexLists_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenPokedexLists();

        private void SpecialEncountersEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenSpecialEncountersEditor();

        private void BugContestEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenBugContestEditor();

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

        private void HgEngineHooks_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenHgEngineHooks();

        private void HgEngineSettings_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenHgEngineSettings();

        private void BattleTests_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenBattleTests();

        private async void CompileRom_Click(object sender, RoutedEventArgs e) => await RunRomOperationAsync(Project.CompileRomCoreAsync);

        private void ToggleTheme_Click(object sender, RoutedEventArgs e)
            => DSPRE.Avalonia.ThemeManager.Toggle();
    }
}
