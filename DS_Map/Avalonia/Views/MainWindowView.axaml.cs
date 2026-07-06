using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using DSPRE.Avalonia.ViewModels;

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
        }

        private void CommandPalette_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenCommandPalette(this);

        public MainWindowView(MainWindowViewModel vm) : this()
        {
            DataContext = vm;
        }

        // ── File ────────────────────────────────────────────────────────────
        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private async void OpenRom_Click(object sender, RoutedEventArgs e)
        {
            var files = await StorageProvider.OpenFilePickerAsync(new global::Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = "Open ROM",
                AllowMultiple = false,
                FileTypeFilter = new[] { new global::Avalonia.Platform.Storage.FilePickerFileType("NDS ROM") { Patterns = new[] { "*.nds" } } }
            });
            string path = files != null && files.Count > 0 ? files[0].TryGetLocalPath() : null;
            if (!string.IsNullOrEmpty(path))
                await LoadRom(err0 => { bool ok = AvaloniaRomLoader.LoadFromFile(path, out var er); err0(er); return ok; });
        }

        private async void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new global::Avalonia.Platform.Storage.FolderPickerOpenOptions
            {
                Title = "Open extracted ROM folder", AllowMultiple = false
            });
            string path = folders != null && folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
            if (!string.IsNullOrEmpty(path))
                await LoadRom(err0 => { bool ok = AvaloniaRomLoader.LoadFromFolder(path, out var er); err0(er); return ok; });
        }

        // Runs a ROM load off the UI thread (unpacking blocks), then refreshes the menus/title and reports errors.
        private async System.Threading.Tasks.Task LoadRom(System.Func<System.Action<string>, bool> load)
        {
            string error = null;
            bool ok = await System.Threading.Tasks.Task.Run(() => load(e => error = e));
            if (DataContext is MainWindowViewModel vm) vm.RefreshRomState();
            if (!ok) { await DialogHelper.ShowError(error ?? "Failed to load the ROM.", "Open ROM"); return; }
            // The Maps workspace skipped its setup at boot (no ROM yet) — run it now.
            await Maps.EnsureSetupAsync();
        }

        private async void SaveRom_Click(object sender, RoutedEventArgs e)
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

            string error = null;
            bool ok = await System.Threading.Tasks.Task.Run(() =>
            {
                try { return DSUtils.RepackROM(path); }        // builds the .nds from RomInfo.workDir
                catch (System.Exception ex) { error = ex.Message; return false; }
            });
            if (ok) await DialogHelper.ShowInfo("ROM built successfully:\n" + path, "Save ROM");
            else await DialogHelper.ShowError(error ?? "Building the ROM failed. See the log for details.", "Save ROM");
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

        private void MoveDataEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenMoveDataEditor();

        private void TMEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenTMEditor();

        private void EggMoveEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenEggMoveEditor();

        private void ItemEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenItemEditor();

        private void ItemTableEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenItemTableEditor();

        private void TradeEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenTradeEditor();

        private void TrainerEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenTrainerEditor();

        private void TextEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenTextEditor();

        private void ScriptEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenScriptEditor();

        private void LevelScriptEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenLevelScriptEditor();

        private void TableEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenTableEditor();

        private void HiddenItemsEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenHiddenItemsEditor();

        private void PickupTableEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenPickupTableEditor();

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

        private void ToggleTheme_Click(object sender, RoutedEventArgs e)
            => DSPRE.Avalonia.ThemeManager.Toggle();

        private void GlTest_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenGlTest();
    }
}
