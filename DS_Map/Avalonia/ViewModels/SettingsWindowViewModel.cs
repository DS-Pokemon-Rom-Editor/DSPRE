using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia.Controls;

namespace DSPRE.Avalonia.ViewModels
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

        private bool _autoUpdateDBs;
        public bool AutoUpdateDBs
        {
            get => _autoUpdateDBs;
            set => Set(ref _autoUpdateDBs, value);
        }

        private string _versionLabel = string.Empty;
        public string VersionLabel
        {
            get => _versionLabel;
            private set => Set(ref _versionLabel, value);
        }

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
            VersionLabel = $"DSPRE Version {Helpers.GetDSPREVersion()}";

            ExportPath       = SettingsManager.Settings.exportPath        ?? string.Empty;
            MapImportPath    = SettingsManager.Settings.mapImportStarterPoint ?? string.Empty;
            OpenDefaultRom   = SettingsManager.Settings.openDefaultRom    ?? string.Empty;
            NeverAskForOpening = SettingsManager.Settings.neverAskForOpening;
            AutoCheckUpdates = SettingsManager.Settings.automaticallyCheckForUpdates;
            AutoUpdateDBs    = SettingsManager.Settings.automaticallyUpdateDBs;

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

        public void CheckForUpdates()    => Helpers.CheckForUpdates(false);
        public void CheckDBUpdates()     => Helpers.CheckForDatabaseUpdates(false);

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
