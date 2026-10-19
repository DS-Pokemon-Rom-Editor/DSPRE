using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using DSPRE.Avalonia;
namespace DSPRE.Avalonia.ViewModels.Shell
{
    public class SettingsWindowViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private bool Set<T>(ref T field, T value, [CallerMemberName] string name = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(name);
            return true;
        }

        // ----------------------------------------------------------------
        // Snapshot of paths at load time (for dirty/close detection)
        // ----------------------------------------------------------------

        private string _oldExportPath;
        private string _oldMapImportPath;
        private string _oldOpenDefaultPath;

        // ----------------------------------------------------------------
        // Bound properties
        // ----------------------------------------------------------------

        private string _exportPath = string.Empty;
        public string ExportPath
        {
            get => _exportPath;
            set => Set(ref _exportPath, value);
        }

        private string _mapImportPath = string.Empty;
        public string MapImportPath
        {
            get => _mapImportPath;
            set => Set(ref _mapImportPath, value);
        }

        private string _openDefaultRom = string.Empty;
        public string OpenDefaultRom
        {
            get => _openDefaultRom;
            set => Set(ref _openDefaultRom, value);
        }

        private bool _neverAskForOpening;
        public bool NeverAskForOpening
        {
            get => _neverAskForOpening;
            set => Set(ref _neverAskForOpening, value);
        }

        private bool _autoCheckUpdates;
        public bool AutoCheckUpdates
        {
            get => _autoCheckUpdates;
            set => Set(ref _autoCheckUpdates, value);
        }

        private bool _showWelcomeOnStartup = true;
        public bool ShowWelcomeOnStartup
        {
            get => _showWelcomeOnStartup;
            set => Set(ref _showWelcomeOnStartup, value);
        }

        // Inverse of DspreSettings.guidedTourShown: checked = tour runs after the next ROM load.
        private bool _showGuidedTourNextLoad;
        public bool ShowGuidedTourNextLoad
        {
            get => _showGuidedTourNextLoad;
            set => Set(ref _showGuidedTourNextLoad, value);
        }

        private bool _autoUpdateDBs;
        public bool AutoUpdateDBs
        {
            get => _autoUpdateDBs;
            set => Set(ref _autoUpdateDBs, value);
        }

        private decimal _uiScale;
        public decimal UiScale
        {
            get => _uiScale;
            set => Set(ref _uiScale, value);
        }

        public string[] ThemeNames { get; } = { "Dark", "Light" };

        // ── Emulator for Build and Run: index 0 asks each time, the rest follow Emulators.All ──
        public string[] EmulatorChoices { get; } = new[] { "Ask each time" }.Concat(Emulators.All.Select(Emulators.DisplayName)).ToArray();

        private readonly System.Collections.Generic.Dictionary<string, string> _emulatorPaths = new();

        private int _emulatorIndex;
        public int EmulatorIndex
        {
            get => _emulatorIndex;
            set { if (value >= 0 && Set(ref _emulatorIndex, value)) RaiseEmulator(); }
        }

        private EmulatorKind? SelectedEmulator => _emulatorIndex > 0 ? Emulators.All[_emulatorIndex - 1] : null;
        public bool HasSelectedEmulator => SelectedEmulator != null;
        public string EmulatorPath => SelectedEmulator is EmulatorKind k && _emulatorPaths.TryGetValue(k.ToString(), out string p) ? p : "";

        private void RaiseEmulator()
        {
            OnPropertyChanged(nameof(HasSelectedEmulator));
            OnPropertyChanged(nameof(EmulatorPath));
        }

        public async Task ChangeEmulatorPathCommandAsync(Window owner)
        {
            List<FilePickerFileType> filters = OperatingSystem.IsWindows()
                ? new System.Collections.Generic.List<global::Avalonia.Platform.Storage.FilePickerFileType> { new("Programs") { Patterns = new[] { "*.exe" } } }
                : null;
            string path = await DialogHelper.OpenFileAsync(owner, "Choose the emulator", filters);
            if (path == null) return;
            EmulatorKind kind = SelectedEmulator ?? Emulators.Guess(path) ?? EmulatorKind.BizHawk;
            _emulatorPaths[kind.ToString()] = path;
            _emulatorIndex = Array.IndexOf(Emulators.All, kind) + 1;
            OnPropertyChanged(nameof(EmulatorIndex));
            RaiseEmulator();
        }

        public void ClearEmulatorPath()
        {
            if (SelectedEmulator is EmulatorKind k) _emulatorPaths.Remove(k.ToString());
            RaiseEmulator();
        }

        // ── The save Build and Run offers to start the open project from ──
        public bool HasProject => AvaloniaEditorLauncher.IsRomLoaded && !string.IsNullOrEmpty(RomInfo.WorkDir);

        private string _testSavePath = "";
        public string TestSavePath { get => _testSavePath; set => Set(ref _testSavePath, value ?? ""); }

        public async Task ChooseTestSaveCommandAsync(Window owner)
        {
            List<FilePickerFileType> filters = new()
            {
                new("Save files") { Patterns = new[] { "*.sav", "*.SaveRAM", "*.dsv", "*.bin" } },
                new("All files") { Patterns = new[] { "*" } },
            };
            string path = await DialogHelper.OpenFileAsync(owner, "Choose a save", filters);
            if (path != null) TestSavePath = path;
        }

        private int _themeIndex;
        public int ThemeIndex
        {
            get => _themeIndex;
            // Applied as it is picked rather than on Save, so the window shows what was chosen.
            set { if (Set(ref _themeIndex, value) && value >= 0) ThemeManager.SetDark(value == 0); }
        }

        private string _versionLabel = string.Empty;
        public string VersionLabel
        {
            get => _versionLabel;
            private set => Set(ref _versionLabel, value);
        }

        // ── 3D-view camera behaviour (mouse) ──────────────────────────────────────────
        private decimal _camPanSpeed = 1m;
        public decimal CamPanSpeed { get => _camPanSpeed; set => Set(ref _camPanSpeed, value); }

        private decimal _camOrbitSpeed = 1m;
        public decimal CamOrbitSpeed { get => _camOrbitSpeed; set => Set(ref _camOrbitSpeed, value); }

        private decimal _camZoomSpeed = 1m;
        public decimal CamZoomSpeed { get => _camZoomSpeed; set => Set(ref _camZoomSpeed, value); }

        private bool _camInvertPanX;   public bool CamInvertPanX   { get => _camInvertPanX;   set => Set(ref _camInvertPanX, value); }
        private bool _camInvertPanY;   public bool CamInvertPanY   { get => _camInvertPanY;   set => Set(ref _camInvertPanY, value); }
        private bool _camInvertOrbitX; public bool CamInvertOrbitX { get => _camInvertOrbitX; set => Set(ref _camInvertOrbitX, value); }
        private bool _camInvertOrbitY; public bool CamInvertOrbitY { get => _camInvertOrbitY; set => Set(ref _camInvertOrbitY, value); }
        private bool _camInvertZoom;   public bool CamInvertZoom   { get => _camInvertZoom;   set => Set(ref _camInvertZoom, value); }

        // ----------------------------------------------------------------
        // Constructor
        // ----------------------------------------------------------------

        public SettingsWindowViewModel()
        {
            if (Design.IsDesignMode)
            {
                VersionLabel = "DSPRE Version 1.0.0 (Preview)";
                ExportPath = @"C:\Export";
                MapImportPath = @"C:\Maps";
                OpenDefaultRom = @"C:\game.nds";
                AutoCheckUpdates = true;
                AutoUpdateDBs = false;
                NeverAskForOpening = false;
                return;
            }
            VersionLabel = $"DSPRE Version {AppInfo.GetDSPREVersion()}";

            ExportPath       = SettingsManager.Settings.ExportPath        ?? string.Empty;
            MapImportPath    = SettingsManager.Settings.MapImportStarterPoint ?? string.Empty;
            OpenDefaultRom   = SettingsManager.Settings.OpenDefaultRom    ?? string.Empty;
            NeverAskForOpening = SettingsManager.Settings.NeverAskForOpening;
            AutoCheckUpdates = SettingsManager.Settings.AutomaticallyCheckForUpdates;
            AutoUpdateDBs    = SettingsManager.Settings.AutomaticallyUpdateDBs;
            ShowWelcomeOnStartup = SettingsManager.Settings.ShowWelcomeOnStartup;
            ShowGuidedTourNextLoad = !SettingsManager.Settings.GuidedTourShown;
            UiScale           = (decimal)SettingsManager.Settings.UiScale;
            _themeIndex       = ThemeManager.IsDark ? 0 : 1;

            CamPanSpeed      = (decimal)SettingsManager.Settings.CamPanSpeed;
            CamOrbitSpeed    = (decimal)SettingsManager.Settings.CamOrbitSpeed;
            CamZoomSpeed     = (decimal)SettingsManager.Settings.CamZoomSpeed;
            CamInvertPanX    = SettingsManager.Settings.CamInvertPanX;
            CamInvertPanY    = SettingsManager.Settings.CamInvertPanY;
            CamInvertOrbitX  = SettingsManager.Settings.CamInvertOrbitX;
            CamInvertOrbitY  = SettingsManager.Settings.CamInvertOrbitY;
            CamInvertZoom    = SettingsManager.Settings.CamInvertZoom;

            foreach (KeyValuePair<string, string> kv in SettingsManager.Settings.EmulatorPaths ?? new System.Collections.Generic.Dictionary<string, string>())
                _emulatorPaths[kv.Key] = kv.Value;
            _emulatorIndex = Enum.TryParse(SettingsManager.Settings.PreferredEmulator, out EmulatorKind preferred)
                ? Array.IndexOf(Emulators.All, preferred) + 1 : 0;
            _testSavePath = HasProject ? TestSaves.For(RomInfo.WorkDir) ?? "" : "";

            // snapshot for unsaved-changes detection
            _oldExportPath      = ExportPath;
            _oldMapImportPath   = MapImportPath;
            _oldOpenDefaultPath = OpenDefaultRom;
        }

        // ----------------------------------------------------------------
        // Commands (called from code-behind)
        // ----------------------------------------------------------------

        public async Task SaveCommandAsync(Window owner)
        {
            SettingsManager.Settings.ExportPath              = ExportPath;
            SettingsManager.Settings.MapImportStarterPoint  = MapImportPath;
            SettingsManager.Settings.OpenDefaultRom         = OpenDefaultRom;
            SettingsManager.Settings.NeverAskForOpening     = NeverAskForOpening;
            SettingsManager.Settings.AutomaticallyCheckForUpdates = AutoCheckUpdates;
            SettingsManager.Settings.AutomaticallyUpdateDBs = AutoUpdateDBs;
            SettingsManager.Settings.ShowWelcomeOnStartup   = ShowWelcomeOnStartup;
            SettingsManager.Settings.GuidedTourShown        = !ShowGuidedTourNextLoad;
            if (ShowGuidedTourNextLoad) SettingsManager.Settings.EditorToursShown?.Clear();
            SettingsManager.Settings.UiScale                = (double)UiScale;

            SettingsManager.Settings.CamPanSpeed     = (float)CamPanSpeed;
            SettingsManager.Settings.CamOrbitSpeed   = (float)CamOrbitSpeed;
            SettingsManager.Settings.CamZoomSpeed    = (float)CamZoomSpeed;
            SettingsManager.Settings.CamInvertPanX   = CamInvertPanX;
            SettingsManager.Settings.CamInvertPanY   = CamInvertPanY;
            SettingsManager.Settings.CamInvertOrbitX = CamInvertOrbitX;
            SettingsManager.Settings.CamInvertOrbitY = CamInvertOrbitY;
            SettingsManager.Settings.CamInvertZoom   = CamInvertZoom;

            SettingsManager.Settings.EmulatorPaths = new System.Collections.Generic.Dictionary<string, string>(_emulatorPaths);
            SettingsManager.Settings.PreferredEmulator = SelectedEmulator?.ToString() ?? "";
            if (HasProject) TestSaves.Remember(RomInfo.WorkDir, TestSavePath);

            _oldExportPath      = ExportPath;
            _oldMapImportPath   = MapImportPath;
            _oldOpenDefaultPath = OpenDefaultRom;

            SettingsManager.Save();
            SaveNotice.Saved("settings");
        }

        public async Task ChangeExportPathCommandAsync(Window owner)
        {
            string path = await DialogHelper.OpenFolderAsync(owner, "Select ROM export path");
            if (path != null)
                ExportPath = path;
        }

        public async Task ChangeMapImportPathCommandAsync(Window owner)
        {
            string path = await DialogHelper.OpenFolderAsync(owner, "Select initial map import path");
            if (path != null)
                MapImportPath = path;
        }

        public async Task ChangeOpenDefaultRomCommandAsync(Window owner)
        {
            string path = await DialogHelper.OpenFolderAsync(owner, "Select default ROM folder");
            if (path == null) return;

            if (!path.EndsWith("DSPRE_contents"))
            {
                bool proceed = await DialogHelper.AskYesNoAsync(
                    "The folder you selected does not appear to be a DSPRE folder (DSPRE_contents), are you sure you want to proceed?",
                    "Warning");
                if (!proceed) return;
            }

            OpenDefaultRom = path;
        }

        public void ClearExportPath()      => ExportPath     = string.Empty;
        public void ClearMapImportPath()   => MapImportPath  = string.Empty;
        public void ClearOpenDefaultRom()  => OpenDefaultRom = string.Empty;

        public void CheckForUpdates()    => ShellIntegration.CheckForUpdates(false);
        public void CheckDBUpdates()     => ScriptDatabaseSetup.CheckForDatabaseUpdates(false);

        /// <summary>Returns false if the window close should be cancelled.</summary>
        public async Task<bool> ConfirmCloseAsync()
        {
            bool dirty = ExportPath    != _oldExportPath
                      || MapImportPath != _oldMapImportPath
                      || OpenDefaultRom != _oldOpenDefaultPath;

            if (!dirty) return true;

            return await DialogHelper.AskYesNoAsync(
                "You still have unsaved modifications, are you sure you want to quit?",
                "Exit");
        }
    }
}
