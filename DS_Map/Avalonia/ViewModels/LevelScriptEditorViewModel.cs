using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using global::Avalonia.Controls;
using global::Avalonia.Platform.Storage;
using DSPRE.Avalonia;
using DSPRE.Editors;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels
{
    /// <summary>
    /// Avalonia port of the WinForms <c>LevelScriptEditor</c>. A level script is a script file
    /// whose body is a set of triggers — either map/screen/load triggers (run a script when the
    /// map is entered / a fade happens / the game loads) or variable-value triggers (keep running
    /// a script while a variable holds a value). Lets you pick a script file, list its triggers,
    /// add/remove them, and save / import / export. Files that aren't level scripts load empty.
    /// </summary>
    public class LevelScriptEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (EqualityComparer<T>.Default.Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        private Window _owner;
        private bool _suppress;
        private LevelScriptFile _file;

        public ObservableCollection<string> ScriptNames { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> Triggers { get; } = new ObservableCollection<string>();

        // Trigger types in the same order as the radio buttons in WinForms.
        public ObservableCollection<string> TriggerTypes { get; } = new ObservableCollection<string>
        { "Variable value", "On map enter", "On screen reset", "On game load" };
        private static readonly int[] TypeConst =
        { LevelScriptTrigger.VARIABLEVALUE, LevelScriptTrigger.MAPCHANGE, LevelScriptTrigger.SCREENRESET, LevelScriptTrigger.LOADGAME };

        private int _typeIndex;
        public int TriggerTypeIndex { get => _typeIndex; set { if (Set(ref _typeIndex, value)) OnPropertyChanged(nameof(IsVariableType)); } }
        public bool IsVariableType => _typeIndex == 0;

        private decimal _newScriptId, _newVariable, _newValue;
        public decimal NewScriptId { get => _newScriptId; set => Set(ref _newScriptId, value); }
        public decimal NewVariable { get => _newVariable; set => Set(ref _newVariable, value); }
        public decimal NewValue { get => _newValue; set => Set(ref _newValue, value); }

        private bool _padding;
        public bool WordAlignmentPadding { get => _padding; set => Set(ref _padding, value); }

        private int _selScript = -1;
        public int SelectedScriptIndex { get => _selScript; set { if (Set(ref _selScript, value) && !_suppress && value >= 0) LoadFile(value); } }

        private int _selTrigger = -1;
        public int SelectedTriggerIndex { get => _selTrigger; set { if (Set(ref _selTrigger, value)) OnPropertyChanged(nameof(HasTrigger)); } }
        public bool HasTrigger => _selTrigger >= 0 && _file != null && _selTrigger < _file.bufferSet.Count;

        private string _statusText = "Not loaded";
        public string StatusText { get => _statusText; set => Set(ref _statusText, value); }

        // ── Dirty tracking ───────────────────────────────────────────────────────────
        private bool _dirty;
        public bool HasUnsavedChanges => _dirty;
        public string UnsavedChangesDescription => $"Level script {_selScript}";
        public void SaveChanges() => Save();
        public void DiscardChanges() { _dirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); if (_selScript >= 0) LoadFile(_selScript); }
        private void Dirty() { if (_dirty) return; _dirty = true; OnPropertyChanged(nameof(HasUnsavedChanges)); }
        private void SetClean() { if (!_dirty) return; _dirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); }

        public LevelScriptEditorViewModel() { if (Design.IsDesignMode) ScriptNames.Add("Script 0"); }
        public LevelScriptEditorViewModel(bool _) { }
        public int InitialIndex { get; set; }

        public async Task SetupAsync(Window owner)
        {
            _owner = owner;
            try
            {
                DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.scripts });
                int count = Filesystem.GetScriptCount();
                ScriptNames.Clear();
                for (int i = 0; i < count; i++) ScriptNames.Add("Script File " + i);
                StatusText = $"{count} script files.";
                if (count > 0) SelectedScriptIndex = Math.Min(Math.Max(0, InitialIndex), count - 1);
            }
            catch (Exception ex)
            {
                StatusText = "Error: " + ex.Message;
                await DialogHelper.ShowError($"Failed to set up Level Script Editor:\n{ex.Message}", "Level Script Editor");
            }
        }

        private void LoadFile(int index)
        {
            try
            {
                _file = new LevelScriptFile(index);
                RefreshTriggers();
                SetClean();
                StatusText = $"Loaded level script {index} ({_file.bufferSet.Count} trigger(s)).";
                OnPropertyChanged(nameof(UnsavedChangesDescription));
            }
            catch (InvalidDataException)
            {
                _file = new LevelScriptFile { ID = index };
                RefreshTriggers();
                SetClean();
                StatusText = $"Script {index} is not a level script (empty). Add a trigger to make it one.";
            }
            catch (Exception ex) { _ = DialogHelper.ShowError($"Failed to load level script {index}:\n{ex.Message}", "Level Script Editor"); }
        }

        private void RefreshTriggers()
        {
            _suppress = true;
            Triggers.Clear();
            if (_file != null)
                foreach (var t in _file.bufferSet) Triggers.Add(t.ToString());
            _suppress = false;
            SelectedTriggerIndex = Triggers.Count > 0 ? 0 : -1;
        }

        public void AddTrigger()
        {
            if (_file == null) return;
            int type = TypeConst[_typeIndex];
            LevelScriptTrigger trigger = type == LevelScriptTrigger.VARIABLEVALUE
                ? new VariableValueTrigger((int)_newScriptId, (int)_newVariable, (int)_newValue)
                : new MapScreenLoadTrigger(type, (int)_newScriptId);
            _file.bufferSet.Add(trigger);
            RefreshTriggers();
            Dirty();
            SelectedTriggerIndex = _file.bufferSet.Count - 1;
        }

        public void RemoveTrigger()
        {
            if (!HasTrigger) return;
            _file.bufferSet.RemoveAt(_selTrigger);
            RefreshTriggers();
            Dirty();
        }

        public void Save()
        {
            if (_file == null || _selScript < 0) return;
            try
            {
                _file.write_file(Filesystem.GetScriptPath(_selScript), _padding);
                SetClean();
                StatusText = $"Saved level script {_selScript}.";
            }
            catch (Exception ex) { _ = DialogHelper.ShowError($"Save failed:\n{ex.Message}", "Level Script Editor"); }
        }

        public async Task ImportAsync()
        {
            if (_file == null) return;
            var filter = new FilePickerFileType("Level script") { Patterns = new[] { "*.lscr", "*.bin", "*.*" } };
            string path = await DialogHelper.OpenFile(_owner, "Import level script", new[] { filter });
            if (path == null) return;
            try
            {
                var imported = new LevelScriptFile();
                imported.parse_file(path);
                _file.bufferSet.Clear();
                foreach (var t in imported.bufferSet) _file.bufferSet.Add(t);
                RefreshTriggers();
                Dirty();
                StatusText = "Imported (unsaved).";
            }
            catch (Exception ex) { await DialogHelper.ShowError($"Import failed:\n{ex.Message}", "Import Error"); }
        }

        public async Task ExportAsync()
        {
            if (_file == null) return;
            var filter = new FilePickerFileType("Level script") { Patterns = new[] { "*.lscr" } };
            string path = await DialogHelper.SaveFile(_owner, "Export level script", new[] { filter }, $"levelscript_{_selScript:D4}.lscr");
            if (path == null) return;
            try { _file.write_file(path, _padding); StatusText = "Exported."; }
            catch (Exception ex) { await DialogHelper.ShowError($"Export failed:\n{ex.Message}", "Export Error"); }
        }
    }
}
