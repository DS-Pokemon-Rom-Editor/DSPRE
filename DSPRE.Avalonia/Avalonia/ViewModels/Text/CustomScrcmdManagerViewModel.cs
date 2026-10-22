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

namespace DSPRE.Avalonia.ViewModels.Text
{
    /// <summary>
    /// The open project's script command database: a Rotom project's own (the file its rotom.toml names), or, for a
    /// project whose scripts haven't been converted to Rotom yet, its legacy copy under
    /// <c>AppPaths.DatabasePath\edited_databases</c>, which rotom carries over when it converts them. Import replaces
    /// it (a Rotom project's is backed up under .rotom/backups first) and can reload the app's command list; Export
    /// copies it out; Open folder shows where it lives.
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

        private static readonly FilePickerFileType JsonFilter =
            new FilePickerFileType("JSON files") { Patterns = new[] { "*.json" } };

        public ObservableCollection<string> Folders { get; } = new();

        // The database file each row stands for, and whether it is a Rotom project's own.
        private readonly List<(string Path, bool Rotom)> _files = new();

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
            _files.Clear();
            if (!AvaloniaEditorLauncher.IsRomLoaded)
            {
                StatusText = "Open a project to manage its script commands.";
                return;
            }
            string rotom = RomInfo.HasRotomProject ? DSPRE.RotomTool.ProjectDatabaseFile() : null;
            if (rotom != null && File.Exists(rotom))
            {
                Folders.Add($"{RomInfo.ProjectName} (Rotom project)");
                _files.Add((rotom, true));
                FolderHelp = "The project's scripts are read and written with this database";
                return;
            }
            Folders.Add($"{RomInfo.ProjectName} (scripts not converted to Rotom yet)");
            _files.Add((Path.Combine(DSPRE.ROMFiles.ScriptFile.CurrentRomDatabaseFolder(), "scrcmd_database.json"), false));
            FolderHelp = "Rotom carries these commands over when the scripts are first opened";
        }

        private string _folderHelp;
        public string FolderHelp { get => _folderHelp; private set => Set(ref _folderHelp, value); }

        private (string Path, bool Rotom) Selected => HasSelection && SelectedIndex < _files.Count ? _files[SelectedIndex] : (null, false);

        public async Task ImportAsync(Window owner)
        {
            if (!HasSelection || IsBusy) return;
            (string targetPath, bool rotom) = Selected;

            string picked = await DialogHelper.OpenFileAsync(owner, "Select a script command database file",
                new[] { JsonFilter, DialogHelper.AllFilter });
            if (string.IsNullOrEmpty(picked)) return;

            if (rotom && WhyNotRotomDatabase(picked) is string why)
            {
                await DialogHelper.ShowErrorAsync(why, "Custom script command manager error");
                return;
            }
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
                if (rotom && File.Exists(targetPath))
                {
                    string backup = Path.Combine(DSPRE.RotomTool.ProjectRoot, ".rotom", "backups", "custom-database-" + DateTime.Now.ToString("yyyyMMddHHmmss"));
                    Directory.CreateDirectory(backup);
                    PlainFiles.Copy(targetPath, Path.Combine(backup, Path.GetFileName(targetPath)), overwrite: true);
                }
                PlainFiles.Copy(picked, targetPath, overwrite: true);
            }
            catch (Exception ex)
            {
                await DialogHelper.ShowErrorAsync($"Could not replace the database:\n{ex.Message}", "Custom script command manager error");
                return;
            }

            Refresh();

            bool reload = await DialogHelper.AskTwoWayAsync(
                "Reparse every script with the new database now, or wait for the next ROM load?",
                "Reload database?", "Reparse now", "Next load");
            if (reload)
            {
                // The app's command list is built from the legacy copy with the Rotom project's names laid over it.
                await ReloadAndReparseScriptsAsync(rotom ? Path.Combine(DSPRE.ROMFiles.ScriptFile.CurrentRomDatabaseFolder(), "scrcmd_database.json") : targetPath);
            }
        }

        // A Rotom project needs a v2 database: an object with a "commands" object in it.
        private static string WhyNotRotomDatabase(string path)
        {
            try
            {
                using System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
                return doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object
                       && doc.RootElement.TryGetProperty("commands", out System.Text.Json.JsonElement commands)
                       && commands.ValueKind == System.Text.Json.JsonValueKind.Object
                    ? null
                    : "That isn't a Rotom command database (it has no \"commands\" list), so the project's database was left as it is.";
            }
            catch (Exception ex)
            {
                return "That file couldn't be read as JSON, so the project's database was left as it is.\n" + ex.Message;
            }
        }

        private async Task ReloadAndReparseScriptsAsync(string databasePath)
        {
            if (!AvaloniaEditorLauncher.IsRomLoaded)
            {
                await DialogHelper.ShowInfoAsync("No ROM is loaded. The new database will be used on the next ROM load.", "Reload skipped");
                return;
            }

            // Reloading would apply another ROM's database to this one and rewrite that ROM's name files.
            if (!DSPRE.ROMFiles.ScriptFile.IsCurrentRomDatabaseFolder(Path.GetDirectoryName(databasePath)))
            {
                await DialogHelper.ShowInfoAsync("That database belongs to another ROM. It will be used when that ROM is loaded.", "Reload skipped");
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
                    List<int> affectedFiles = invalidCommands.Select(c => c.fileID).Distinct().OrderBy(x => x).ToList();
                    string fileList = string.Join(", ", affectedFiles.Select(f => f.ToString("D4")));
                    List<ushort> uniqueCommands = invalidCommands.Select(c => c.commandID).Distinct().OrderBy(x => x).ToList();
                    string commandList = string.Join(", ", uniqueCommands.Select(c => $"0x{c:X4}"));

                    await DialogHelper.ShowErrorAsync(
                        $"Database reloaded, but {invalidCommands.Count} script command(s) across {affectedFiles.Count} file(s) still could not be parsed.\n\n" +
                        $"Affected files: {fileList}\n" +
                        $"Unrecognized commands: {commandList}\n\n" +
                        $"Affected script files will be incomplete and read-only.",
                        "Unrecognised commands");
                }
                else
                {
                    SaveNotice.Show("Reparsed every script with this database.");
                }
                StatusText = "";
            }
            catch (Exception ex)
            {
                await DialogHelper.ShowErrorAsync($"Error reloading database: {ex.Message}", "Custom script command manager error");
                StatusText = "";
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task ExportAsync(Window owner)
        {
            if (!HasSelection || IsBusy) return;
            (string sourcePath, bool rotom) = Selected;

            if (!File.Exists(sourcePath))
            {
                await DialogHelper.ShowErrorAsync($"Script command database not found:\n{sourcePath}", "Custom script command manager error");
                return;
            }

            string dest = await DialogHelper.SaveFileAsync(owner, "Export script command database",
                new[] { JsonFilter, DialogHelper.AllFilter },
                rotom ? Path.GetFileName(sourcePath) : $"{RomInfo.ProjectName}_scrcmd_database.json");
            if (string.IsNullOrEmpty(dest)) return;

            try
            {
                PlainFiles.Copy(sourcePath, dest, overwrite: true);
                SaveNotice.Exported(dest);
            }
            catch (DirectoryNotFoundException)
            {
                await DialogHelper.ShowErrorAsync($"That folder doesn't exist:\n{Path.GetDirectoryName(dest)}", "Custom script command manager error");
            }
            catch (UnauthorizedAccessException)
            {
                await DialogHelper.ShowErrorAsync($"That folder can't be written to:\n{Path.GetDirectoryName(dest)}", "Custom script command manager error");
            }
            catch (Exception ex)
            {
                await DialogHelper.ShowErrorAsync($"Export failed:\n{ex.Message}", "Custom script command manager error");
            }
        }

        public async Task OpenFolderAsync()
        {
            string folder = Path.GetDirectoryName(Selected.Path ?? "");
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                await DialogHelper.ShowErrorAsync($"Folder not found:\n{folder}", "Custom script command manager error");
                return;
            }
            SystemShell.OpenWithDefaultApp(folder);
        }
    }
}
