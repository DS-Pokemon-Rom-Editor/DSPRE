using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia.Controls;

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

            ExportPath       = SettingsManager.Settings.exportPath        ?? string.Empty;
            MapImportPath    = SettingsManager.Settings.mapImportStarterPoint ?? string.Empty;
            OpenDefaultRom   = SettingsManager.Settings.openDefaultRom    ?? string.Empty;
            NeverAskForOpening = SettingsManager.Settings.neverAskForOpening;
            AutoCheckUpdates = SettingsManager.Settings.automaticallyCheckForUpdates;
            AutoUpdateDBs    = SettingsManager.Settings.automaticallyUpdateDBs;
            ShowWelcomeOnStartup = SettingsManager.Settings.showWelcomeOnStartup;
            ShowGuidedTourNextLoad = !SettingsManager.Settings.guidedTourShown;
            UiScale           = (decimal)SettingsManager.Settings.uiScale;
            _themeIndex       = ThemeManager.IsDark ? 0 : 1;

            CamPanSpeed      = (decimal)SettingsManager.Settings.camPanSpeed;
            CamOrbitSpeed    = (decimal)SettingsManager.Settings.camOrbitSpeed;
            CamZoomSpeed     = (decimal)SettingsManager.Settings.camZoomSpeed;
            CamInvertPanX    = SettingsManager.Settings.camInvertPanX;
            CamInvertPanY    = SettingsManager.Settings.camInvertPanY;
            CamInvertOrbitX  = SettingsManager.Settings.camInvertOrbitX;
            CamInvertOrbitY  = SettingsManager.Settings.camInvertOrbitY;
            CamInvertZoom    = SettingsManager.Settings.camInvertZoom;

            // snapshot for unsaved-changes detection
            _oldExportPath      = ExportPath;
            _oldMapImportPath   = MapImportPath;
            _oldOpenDefaultPath = OpenDefaultRom;
        }

        // ----------------------------------------------------------------
        // Commands (called from code-behind)
        // ----------------------------------------------------------------

        public async Task SaveCommand(Window owner)
        {
            SettingsManager.Settings.exportPath              = ExportPath;
            SettingsManager.Settings.mapImportStarterPoint  = MapImportPath;
            SettingsManager.Settings.openDefaultRom         = OpenDefaultRom;
            SettingsManager.Settings.neverAskForOpening     = NeverAskForOpening;
            SettingsManager.Settings.automaticallyCheckForUpdates = AutoCheckUpdates;
            SettingsManager.Settings.automaticallyUpdateDBs = AutoUpdateDBs;
            SettingsManager.Settings.showWelcomeOnStartup   = ShowWelcomeOnStartup;
            SettingsManager.Settings.guidedTourShown        = !ShowGuidedTourNextLoad;
            SettingsManager.Settings.uiScale                = (double)UiScale;

            SettingsManager.Settings.camPanSpeed     = (float)CamPanSpeed;
            SettingsManager.Settings.camOrbitSpeed   = (float)CamOrbitSpeed;
            SettingsManager.Settings.camZoomSpeed    = (float)CamZoomSpeed;
            SettingsManager.Settings.camInvertPanX   = CamInvertPanX;
            SettingsManager.Settings.camInvertPanY   = CamInvertPanY;
            SettingsManager.Settings.camInvertOrbitX = CamInvertOrbitX;
            SettingsManager.Settings.camInvertOrbitY = CamInvertOrbitY;
            SettingsManager.Settings.camInvertZoom   = CamInvertZoom;

            _oldExportPath      = ExportPath;
            _oldMapImportPath   = MapImportPath;
            _oldOpenDefaultPath = OpenDefaultRom;

            SettingsManager.Save();
            await DialogHelper.ShowInfo("Settings saved successfully!", string.Empty);
        }

        public async Task ChangeExportPathCommand(Window owner)
        {
            string path = await DialogHelper.OpenFolder(owner, "Select ROM Export Path");
            if (path != null)
                ExportPath = path;
        }

        public async Task ChangeMapImportPathCommand(Window owner)
        {
            string path = await DialogHelper.OpenFolder(owner, "Select Initial Map Import Path");
            if (path != null)
                MapImportPath = path;
        }

        public async Task ChangeOpenDefaultRomCommand(Window owner)
        {
            string path = await DialogHelper.OpenFolder(owner, "Select Default ROM Folder");
            if (path == null) return;

            if (!path.EndsWith("DSPRE_contents"))
            {
                bool proceed = await DialogHelper.AskYesNo(
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

            return await DialogHelper.AskYesNo(
                "You still have unsaved modifications, are you sure you want to quit?",
                "Exit");
        }
    }
}
