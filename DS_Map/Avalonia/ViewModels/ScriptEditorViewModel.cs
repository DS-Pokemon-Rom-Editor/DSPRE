using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using global::Avalonia.Controls;
using global::Avalonia.Platform.Storage;
using DSPRE.Avalonia;
using DSPRE.Editors;
using DSPRE.Resources;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels
{
    /// <summary>
    /// Avalonia port of the WinForms <c>ScriptEditor</c> — core scope. Edits a script
    /// file as three plain-text sections (Scripts / Functions / Actions), mirroring the
    /// WinForms layout but replacing the three Scintilla controls with plain monospace
    /// text editors (no syntax highlighting/autocomplete — AvaloniaEdit could add that
    /// later). Loads via the existing parser and saves by re-compiling the three texts
    /// through <c>new ScriptFile(scriptLines, functionLines, actionLines, fileID)</c>.
    /// Read-only when the file failed to parse (unrecognized commands).
    /// </summary>
    public class ScriptEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (EqualityComparer<T>.Default.Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        private Window _owner;
        private bool _suppress;
        private ScriptFile _file;

        public ObservableCollection<string> ScriptNames { get; } = new ObservableCollection<string>();

        private int _selectedIndex = -1;
        public int SelectedScriptIndex
        {
            get => _selectedIndex;
            set { if (Set(ref _selectedIndex, value) && !_suppress && value >= 0) LoadScript(value); }
        }

        private string _scriptsText = "", _functionsText = "", _actionsText = "";
        public string ScriptsText { get => _scriptsText; set { if (Set(ref _scriptsText, value) && !_suppress) Dirty(); } }
        public string FunctionsText { get => _functionsText; set { if (Set(ref _functionsText, value) && !_suppress) Dirty(); } }
        public string ActionsText { get => _actionsText; set { if (Set(ref _actionsText, value) && !_suppress) Dirty(); } }

        private bool _isReadOnly;
        public bool IsReadOnly { get => _isReadOnly; set => Set(ref _isReadOnly, value); }
        public bool IsEditable => !_isReadOnly;

        private string _statusText = "Not loaded";
        public string StatusText { get => _statusText; set => Set(ref _statusText, value); }

        // ── Dirty tracking ───────────────────────────────────────────────────────────
        private bool _dirty;
        public bool HasUnsavedChanges => _dirty;
        public string UnsavedChangesDescription => $"Script {_selectedIndex}";
        public void SaveChanges() => Save();
        public void DiscardChanges() { _dirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); if (_selectedIndex >= 0) LoadScript(_selectedIndex); }
        private void Dirty() { if (_dirty) return; _dirty = true; OnPropertyChanged(nameof(HasUnsavedChanges)); }
        private void SetClean() { if (!_dirty) return; _dirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); }

        public ScriptEditorViewModel() { if (Design.IsDesignMode) ScriptNames.Add("Script 0"); }
        public ScriptEditorViewModel(bool _) { }

        public async Task SetupAsync(Window owner)
        {
            _owner = owner;
            try
            {
                DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.scripts });
                int count = Filesystem.GetScriptCount();
                for (int i = 0; i < count; i++) ScriptNames.Add("Script File " + i);
                StatusText = $"{count} script files.";
                if (count > 0) SelectedScriptIndex = 0;
            }
            catch (Exception ex)
            {
                StatusText = "Error: " + ex.Message;
                await DialogHelper.ShowError($"Failed to set up Script Editor:\n{ex.Message}", "Script Editor");
            }
        }

        private void LoadScript(int index)
        {
            try
            {
                _file = new ScriptFile(index);
                _suppress = true;
                ScriptsText = BuildCommands(_file.allScripts, "Script");
                FunctionsText = BuildCommands(_file.allFunctions, "Function");
                ActionsText = BuildActions(_file.allActions, "Action");
                IsReadOnly = _file.parseFailedDueToInvalidCommand;
                OnPropertyChanged(nameof(IsEditable));
                _suppress = false;
                SetClean();
                StatusText = IsReadOnly
                    ? $"Script {index} is READ-ONLY (unrecognized commands — load the matching command database)."
                    : $"Loaded script {index}.";
                OnPropertyChanged(nameof(UnsavedChangesDescription));
            }
            catch (Exception ex)
            {
                _ = DialogHelper.ShowError($"Failed to load script {index}:\n{ex.Message}", "Script Editor");
            }
        }

        private static string BuildCommands(List<ScriptCommandContainer> list, string typeName)
        {
            var sb = new StringBuilder();
            if (list != null)
                foreach (var c in list)
                {
                    sb.Append(typeName).Append(' ').Append(c.manualUserID).Append(":\n");
                    if (c.usedScriptID < 0)
                    {
                        if (c.commands != null)
                            foreach (var cmd in c.commands)
                            {
                                if (cmd.id != null && !ScriptDatabase.endCodes.Contains((ushort)cmd.id)) sb.Append('\t');
                                sb.Append(cmd.name).Append('\n');
                            }
                    }
                    else sb.Append('\t').Append("UseScript_#").Append(c.usedScriptID).Append('\n');
                    sb.Append('\n');
                }
            return sb.ToString();
        }

        private static string BuildActions(List<ScriptActionContainer> list, string typeName)
        {
            var sb = new StringBuilder();
            if (list != null)
                foreach (var c in list)
                {
                    sb.Append(typeName).Append(' ').Append(c.manualUserID).Append(":\n");
                    if (c.commands != null)
                        foreach (var a in c.commands)
                        {
                            if (!ScriptDatabase.movementEndCodes.Contains(a.id)) sb.Append('\t');
                            sb.Append(a.name).Append('\n');
                        }
                    sb.Append('\n');
                }
            return sb.ToString();
        }

        private static List<string> Lines(string text)
            => text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').Select(l => l.Trim()).ToList();

        // ── Save / import / export ─────────────────────────────────────────────────────
        public void Save()
        {
            if (_file == null || _selectedIndex < 0 || IsReadOnly) return;
            try
            {
                var edited = new ScriptFile(Lines(_scriptsText), Lines(_functionsText), Lines(_actionsText), _selectedIndex);
                if (edited.hasNoScripts)
                {
                    _ = DialogHelper.ShowError("Couldn't save — a script file needs at least one script.", "Can't save");
                    return;
                }
                if (edited.SaveToFileDefaultDir(_selectedIndex, showSuccessMessage: false))
                {
                    _file = edited;
                    SetClean();
                    StatusText = $"Saved script {_selectedIndex}.";
                }
                else StatusText = "Save failed (see log).";
            }
            catch (Exception ex)
            {
                _ = DialogHelper.ShowError($"Save failed:\n{ex.Message}\n\nCheck the script syntax.", "Script Editor");
            }
        }

        public async Task ExportAsync()
        {
            if (_selectedIndex < 0) return;
            var filter = new FilePickerFileType("Script file") { Patterns = new[] { "*.scr", "*.bin" } };
            string path = await DialogHelper.SaveFile(_owner, "Export script (binary)", new[] { filter }, $"script_{_selectedIndex:D4}.scr");
            if (path == null) return;
            try { File.Copy(gameDirs[DirNames.scripts].unpackedDir + "\\" + _selectedIndex.ToString("D4"), path, true); StatusText = "Exported."; }
            catch (Exception ex) { await DialogHelper.ShowError($"Export failed:\n{ex.Message}", "Export Error"); }
        }

        public async Task ImportAsync()
        {
            if (_selectedIndex < 0) return;
            var filter = new FilePickerFileType("Script file") { Patterns = new[] { "*.scr", "*.bin", "*.*" } };
            string path = await DialogHelper.OpenFile(_owner, "Import script (binary)", new[] { filter });
            if (path == null) return;
            if (!await DialogHelper.AskYesNo($"Replace script file {_selectedIndex} with this file?", "Import")) return;
            try
            {
                File.Copy(path, gameDirs[DirNames.scripts].unpackedDir + "\\" + _selectedIndex.ToString("D4"), true);
                LoadScript(_selectedIndex);
                StatusText = "Imported.";
            }
            catch (Exception ex) { await DialogHelper.ShowError($"Import failed:\n{ex.Message}", "Import Error"); }
        }
    }
}
