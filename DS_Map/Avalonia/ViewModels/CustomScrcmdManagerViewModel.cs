using Avalonia.Controls;
using Avalonia.Platform.Storage;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace DSPRE.Avalonia.ViewModels
{
    /// <summary>
    /// Avalonia port of the WinForms <c>CustomScrcmdManager</c>: manages the per-ROM script-command
    /// databases under <c>AppPaths.DatabasePath\edited_databases</c> — import (replace) a project's
    /// scrcmd_database.json (with optional live reload + full script reparse), export it, and open
    /// the folder in the system file manager.
    /// </summary>
    public class CustomScrcmdManagerViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        {
            if (EqualityComparer<T>.Default.Equals(f, v)) return false;
            f = v; OnPropertyChanged(n); return true;
        }

        private static string CustomDBsPath => Path.Combine(AppPaths.DatabasePath, "edited_databases");

        private static readonly FilePickerFileType JsonFilter =
            new FilePickerFileType("JSON files") { Patterns = new[] { "*.json" } };

        public ObservableCollection<string> Folders { get; } = new();

        private int _selectedIndex = -1;
        public int SelectedIndex
        {
            get => _selectedIndex;
            set { if (Set(ref _selectedIndex, value)) OnPropertyChanged(nameof(HasSelection)); }
        }

        public bool HasSelection => _selectedIndex >= 0 && _selectedIndex < Folders.Count;

        private bool _isBusy;
        public bool IsBusy { get => _isBusy; private set { if (Set(ref _isBusy, value)) OnPropertyChanged(nameof(IsIdle)); } }
        public bool IsIdle => !_isBusy;

        private string _statusText = "";
        public string StatusText { get => _statusText; private set => Set(ref _statusText, value); }

        // Design-time
        public CustomScrcmdManagerViewModel()
        {
            if (!Design.IsDesignMode) return;
            Folders.Add("HeartGold (USA)");
            Folders.Add("my hack");
        }

        public CustomScrcmdManagerViewModel(bool _) => Refresh();

        public void Refresh()
        {
            Folders.Clear();
            if (Directory.Exists(CustomDBsPath))
            {
                foreach (var dir in Directory.GetDirectories(CustomDBsPath))
                    Folders.Add(Path.GetFileName(dir));
            }
            StatusText = Folders.Count == 0
                ? "No per-ROM databases yet — they are created when a ROM is opened."
                : $"{Folders.Count} per-ROM database folder(s).";
        }

        public async Task Import(Window owner)
        {
            if (!HasSelection || IsBusy) return;
            string folderName = Folders[SelectedIndex];

            string picked = await DialogHelper.OpenFile(owner, "Select a script command database file",
                new[] { JsonFilter, DialogHelper.AllFilter });
            if (string.IsNullOrEmpty(picked)) return;

            string targetPath = Path.Combine(CustomDBsPath, folderName, "scrcmd_database.json");
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
                File.Copy(picked, targetPath, overwrite: true);
            }
            catch (Exception ex)
            {
                await DialogHelper.ShowError($"Could not replace the database:\n{ex.Message}", "Import failed");
                return;
            }

            Refresh();

            var reload = await DialogHelper.AskYesNo(
                "Database replaced successfully.\n\n" +
                "Do you want to reload and reparse all scripts now?\n\n" +
                "Yes: Reload database and reparse all scripts immediately\n" +
                "No: Changes will take effect on next ROM load",
                "Reload Database?");
            if (reload)
            {
                await ReloadAndReparseScripts(targetPath);
            }
        }

        private async Task ReloadAndReparseScripts(string databasePath)
        {
            if (!AvaloniaEditorLauncher.IsRomLoaded)
            {
                await DialogHelper.ShowInfo("No ROM is loaded — the new database will be used on the next ROM load.", "Reload skipped");
                return;
            }

            IsBusy = true;
            try
            {
                List<(int fileID, ushort commandID, long offset)> invalidCommands = null;
                await Task.Run(() =>
                {
                    invalidCommands = DSPRE.ROMFiles.ScriptFile.ReloadDatabaseAndReparseAll(
                        databasePath,
                        progressCallback: (current, total) =>
                            global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                                StatusText = $"Reparsing scripts with new database… {current}/{total}"));
                });

                if (invalidCommands != null && invalidCommands.Count > 0)
                {
                    var affectedFiles = invalidCommands.Select(c => c.fileID).Distinct().OrderBy(x => x).ToList();
                    string fileList = string.Join(", ", affectedFiles.Select(f => f.ToString("D4")));
                    var uniqueCommands = invalidCommands.Select(c => c.commandID).Distinct().OrderBy(x => x).ToList();
                    string commandList = string.Join(", ", uniqueCommands.Select(c => $"0x{c:X4}"));

                    await DialogHelper.ShowError(
                        $"Database reloaded, but {invalidCommands.Count} script command(s) across {affectedFiles.Count} file(s) still could not be parsed.\n\n" +
                        $"Affected files: {fileList}\n" +
                        $"Unrecognized commands: {commandList}\n\n" +
                        $"Affected script files will be incomplete and read-only.",
                        "Partial Success");
                }
                else
                {
                    await DialogHelper.ShowInfo("Database reloaded and all scripts reparsed successfully.", "Success");
                }
                StatusText = "Reparse finished.";
            }
            catch (Exception ex)
            {
                await DialogHelper.ShowError($"Error reloading database: {ex.Message}", "Error");
                StatusText = "Reparse failed.";
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task Export(Window owner)
        {
            if (!HasSelection || IsBusy) return;
            string folderName = Folders[SelectedIndex];
            string sourcePath = Path.Combine(CustomDBsPath, folderName, "scrcmd_database.json");

            if (!File.Exists(sourcePath))
            {
                await DialogHelper.ShowError($"Script command database not found:\n{sourcePath}", "Error");
                return;
            }

            string dest = await DialogHelper.SaveFile(owner, "Export Script Command Database",
                new[] { JsonFilter, DialogHelper.AllFilter },
                $"{folderName}_scrcmd_database.json");
            if (string.IsNullOrEmpty(dest)) return;

            try
            {
                File.Copy(sourcePath, dest, overwrite: true);
                await DialogHelper.ShowInfo("Export complete.", "Success");
            }
            catch (Exception ex)
            {
                await DialogHelper.ShowError($"Export failed:\n{ex.Message}", "Export Error");
            }
        }

        public async Task OpenFolder()
        {
            if (!Directory.Exists(CustomDBsPath))
            {
                await DialogHelper.ShowError($"Folder not found:\n{CustomDBsPath}", "Error");
                return;
            }
            SystemShell.OpenWithDefaultApp(CustomDBsPath);
        }
    }
}
