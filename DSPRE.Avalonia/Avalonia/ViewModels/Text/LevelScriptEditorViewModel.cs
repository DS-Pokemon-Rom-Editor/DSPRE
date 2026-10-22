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

namespace DSPRE.Avalonia.ViewModels.Text
{
    /// <summary>
    /// Avalonia port of the WinForms <c>LevelScriptEditor</c>. A level script is a script file
    /// whose body is a set of triggers: either map/screen/load triggers (run a script when the
    /// map is entered / a fade happens / the game loads) or variable-value triggers (keep running
    /// a script while a variable holds a value). Lets you pick a script file, list its triggers,
    /// add/remove them, and save / import / export. Files that aren't level scripts load empty.
    /// </summary>
    public class LevelScriptEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, ISupportsUndo, IDetachable
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

        private decimal _newScriptId, _newVariable = VariableValueTrigger.FirstVariable, _newValue;
        public decimal NewScriptId
        {
            get => _newScriptId;
            set { if (Set(ref _newScriptId, value)) { OnPropertyChanged(nameof(NewScriptCommonInfo)); OnPropertyChanged(nameof(NewScriptHasCommonInfo)); } }
        }
        public decimal NewVariable { get => _newVariable; set { if (Set(ref _newVariable, value)) OnPropertyChanged(nameof(NewVariableHex)); } }
        public decimal FirstVariable => VariableValueTrigger.FirstVariable;
        public decimal LastVariable => VariableValueTrigger.LastVariable;
        public decimal NewValue { get => _newValue; set { if (Set(ref _newValue, value)) OnPropertyChanged(nameof(NewValueHex)); } }

        // Hex shows the trigger list and the two numbers below in hex, as variables are usually written.
        public bool ShowHex
        {
            get => LevelScriptTrigger.DisplayInHex;
            set
            {
                if (LevelScriptTrigger.DisplayInHex == value) return;
                LevelScriptTrigger.DisplayInHex = value;
                OnPropertyChanged(); OnPropertyChanged(nameof(ShowDecimal));
                int keep = SelectedTriggerIndex;
                RefreshTriggers();
                SelectedTriggerIndex = keep;
            }
        }
        public bool ShowDecimal => !ShowHex;
        public string NewVariableHex
        {
            get => "0x" + ((ushort)_newVariable).ToString("X4");
            set { if (DSPRE.Avalonia.Data.HexText.TryParseUShort(value, out ushort v) && v >= FirstVariable && v <= LastVariable) NewVariable = v; else OnPropertyChanged(nameof(NewVariableHex)); }
        }
        public string NewValueHex
        {
            get => "0x" + ((ushort)_newValue).ToString("X4");
            set { if (DSPRE.Avalonia.Data.HexText.TryParseUShort(value, out ushort v) && v <= LastVariable) NewValue = v; else OnPropertyChanged(nameof(NewValueHex)); }
        }

        /// <summary>A script number of 2000+ on Platinum/HGSS may be a "common"/global script rather than
        /// a plain local one in the current file (see <see cref="CommonScriptId"/>); surfaced here so
        /// adding a trigger with such a number isn't a silent surprise.</summary>
        public string NewScriptCommonInfo
        {
            get
            {
                CommonScriptId.Result result = CommonScriptId.Resolve(RomInfo.GameFamily, (int)_newScriptId);
                return result.Kind == CommonScriptId.Kind.Resolved ? result.Where : null;
            }
        }
        public bool NewScriptHasCommonInfo => !string.IsNullOrEmpty(NewScriptCommonInfo);

        // The game's level scripts end on a 4-byte boundary.
        private bool _padding = true;
        public bool WordAlignmentPadding { get => _padding; set => Set(ref _padding, value); }

        private int _selScript = -1;
        public int SelectedScriptIndex
        {
            get => _selScript;
            set
            {
                if (RecordSwitchGuard.IsSnappingBack) return;
                if (value == _selScript) return;
                if (_dirty && !_suppress && value >= 0 && _selScript >= 0)
                {
                    // Snap the list back to the file still loaded until the user has answered.
                    int requested = value;
                    RecordSwitchGuard.SnapBack(() => _selScript, v => _selScript = v, () => OnPropertyChanged(nameof(SelectedScriptIndex)));
                    _ = SwitchScriptAsync(requested);
                    return;
                }
                if (Set(ref _selScript, value) && !_suppress && value >= 0) LoadFile(value);
            }
        }

        private async Task SwitchScriptAsync(int requested)
        {
            if (!await RecordSwitchGuard.ConfirmLeaveAsync(this, _owner, "level script")) return;
            SetClean();
            if (Set(ref _selScript, requested, nameof(SelectedScriptIndex))) LoadFile(requested);
        }

        private int _selTrigger = -1;
        public int SelectedTriggerIndex { get => _selTrigger; set { if (Set(ref _selTrigger, value)) OnPropertyChanged(nameof(HasTrigger)); } }
        public bool HasTrigger => _selTrigger >= 0 && _file != null && _selTrigger < _file.BufferSet.Count;

        private string _statusText = "Not loaded";
        public string StatusText { get => _statusText; set => Set(ref _statusText, value); }

        // ── Dirty tracking ───────────────────────────────────────────────────────────
        private bool _dirty;
        public bool HasUnsavedChanges => _dirty;
        public string UnsavedChangesDescription => $"Level script {_selScript}";
        public void SaveChanges() => Save();
        public void DiscardChanges() { _dirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); if (_selScript >= 0) LoadFile(_selScript); }
        private void Dirty() { _undo?.Record(); if (_dirty) return; _dirty = true; OnPropertyChanged(nameof(HasUnsavedChanges)); }

        // ── Undo / redo ────────────────────────────────────────────────────────────
        private ByteStateUndo _undo;
        public bool CanUndo => _undo?.CanUndo == true;
        public bool CanRedo => _undo?.CanRedo == true;
        public void Undo() { _undo?.Undo(); SyncDirtyWithUndo(); }
        public void Redo() { _undo?.Redo(); SyncDirtyWithUndo(); }
        private void SyncDirtyWithUndo()
        {
            if (_undo == null || _undo.IsDirty == _dirty) return;
            _dirty = _undo.IsDirty; OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        private void StartUndo()
        {
            _undo = _file == null ? null : new ByteStateUndo(_file.TriggerState, state =>
            {
                _file.RestoreTriggers(state);
                RefreshTriggers();
                Dirty();
            }, () => { OnPropertyChanged(nameof(CanUndo)); OnPropertyChanged(nameof(CanRedo)); });
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
        }
        private void SetClean() { _undo?.MarkSaved(); if (!_dirty) return; _dirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); }

        public LevelScriptEditorViewModel() { if (Design.IsDesignMode) ScriptNames.Add("Script 0"); }
        public LevelScriptEditorViewModel(bool _) { AppEvents.LevelScriptSaved += OnSavedElsewhere; }
        public int InitialIndex { get; set; }

        /// <summary>For a standalone window closing; the Maps workspace's instance lives for the session.</summary>
        public void Detach() => AppEvents.LevelScriptSaved -= OnSavedElsewhere;

        private bool _askingAboutSavedElsewhere;

        // The other open copy saved this file: show it, or ask when this copy holds its own edits.
        private async void OnSavedElsewhere(object sender, int id)
        {
            if (ReferenceEquals(sender, this) || id != _selScript || _askingAboutSavedElsewhere) return;
            if (_dirty)
            {
                _askingAboutSavedElsewhere = true;
                try
                {
                    if (!await RecordSwitchGuard.TakeSavedVersionAsync(UnsavedChangesDescription)) return;
                }
                finally { _askingAboutSavedElsewhere = false; }
                if (id != _selScript) return;
            }
            LoadFile(id);
            StatusText = "";
        }

        /// <summary>Adds a level script with no triggers at the end of the script archive, built at once.</summary>
        public async Task AddFileAsync()
        {
            if (_dirty && !await RecordSwitchGuard.ConfirmLeaveAsync(this, _owner, "level script")) return;
            if (HgEngine.HgEngineProject.IsActive || !RotomTool.IsAvailable)
            {
                await DialogHelper.ShowInfoAsync("New level scripts are built with Rotom, which this project can't use.", "Level script editor");
                return;
            }
            try
            {
                (int id, string _) = NewProjectFiles.AddBlankLevelScript();
                RotomTool.Result built = await RotomTool.CompileProjectAsync();
                if (!built.Success) { await DialogHelper.ShowErrorAsync("The script build failed:\n" + RotomTool.FormatDetails(built), "Level script editor error"); return; }
                SyncScriptNames();
                SetClean();
                SelectedScriptIndex = id;
                StatusText = $"Added level script {id}.";
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                await DialogHelper.ShowErrorAsync("Couldn't add a level script:\n" + ex.Message, "Level script editor error");
            }
        }

        /// <summary>Deletes the last script-archive file, unless a header loads it as its scripts or level script.</summary>
        public async Task RemoveLastFileAsync()
        {
            if (HgEngine.HgEngineProject.IsActive) return;
            int id = NewProjectFiles.NextScriptId() - 1;
            if (id <= 0) return;
            if (NewProjectFiles.WhyKeepLastScriptFile(id) is string keep) { await DialogHelper.ShowInfoAsync(keep, "Level script editor"); return; }
            if (!await DialogHelper.AskYesNoAsync($"Delete script file {id}, its source and its compiled copy?", "Delete script file?")) return;
            try
            {
                if (_selScript == id) { SetClean(); SelectedScriptIndex = id - 1; }
                NewProjectFiles.DeleteScriptFile(id);
                SyncScriptNames();
                StatusText = $"Removed script file {id}.";
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                await DialogHelper.ShowErrorAsync("Couldn't remove the file:\n" + ex.Message, "Level script editor error");
            }
        }

        private void SyncScriptNames()
        {
            int count = Filesystem.GetScriptCount();
            _suppress = true;
            while (ScriptNames.Count > count) ScriptNames.RemoveAt(ScriptNames.Count - 1);
            while (ScriptNames.Count < count) ScriptNames.Add("Script file " + ScriptNames.Count);
            _suppress = false;
        }

        public async Task SetupAsync(Window owner)
        {
            _owner = owner;
            try
            {
                DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.Scripts });
                int count = Filesystem.GetScriptCount();
                ScriptNames.Clear();
                for (int i = 0; i < count; i++) ScriptNames.Add("Script file " + i);
                StatusText = $"{count} script files.";
                if (count > 0) SelectedScriptIndex = Math.Min(Math.Max(0, InitialIndex), count - 1);
            }
            catch (Exception ex)
            {
                StatusText = "Error: " + ex.Message;
                await DialogHelper.ShowErrorAsync($"Failed to set up Level script editor:\n{ex.Message}", "Level script editor error");
            }
        }

        private void LoadFile(int index)
        {
            try
            {
                _file = new LevelScriptFile(index);
                RefreshTriggers();
                SetClean();
                StartUndo();
                StatusText = "";
                OnPropertyChanged(nameof(UnsavedChangesDescription));
            }
            catch (InvalidDataException)
            {
                _file = new LevelScriptFile { ID = index };
                RefreshTriggers();
                SetClean();
                StartUndo();
                StatusText = $"Script {index} has no triggers.";
            }
            catch (Exception ex)
            {
                // The previous file must not stay loaded under this number, or Save would write it here.
                _file = null;
                RefreshTriggers();
                SetClean();
                StartUndo();
                StatusText = $"Level script {index} could not be read.";
                _ = DialogHelper.ShowErrorAsync($"Failed to load level script {index}:\n{ex.Message}", "Level script editor error");
            }
        }

        private void RefreshTriggers()
        {
            _suppress = true;
            Triggers.Clear();
            if (_file != null)
                foreach (LevelScriptTrigger t in _file.BufferSet) Triggers.Add(t.ToString());
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
            _file.BufferSet.Add(trigger);
            RefreshTriggers();
            Dirty();
            SelectedTriggerIndex = _file.BufferSet.Count - 1;
        }

        /// <summary>Opens the selected trigger's script, in the script file of the first header using this level script.</summary>
        public void GoToTriggerScript()
        {
            if (!HasTrigger) return;
            int levelScript = _selScript;
            StatusText = AvaloniaEditorLauncher.GoToScript(_file.BufferSet[_selTrigger].ScriptTriggered, () =>
            {
                foreach (ushort id in DSPRE.Avalonia.Data.ProjectIndex.HeadersUsing(DSPRE.Avalonia.Data.RefKind.LevelScript, levelScript))
                {
                    try { return MapHeader.GetMapHeader(id).ScriptFileID; }
                    catch (Exception ex) { AppLogger.Warn($"Level script {levelScript}: header {id} unreadable: {ex.Message}"); }
                }
                return -1;
            });
        }

        public void RemoveTrigger()
        {
            if (!HasTrigger) return;
            _file.BufferSet.RemoveAt(_selTrigger);
            RefreshTriggers();
            Dirty();
        }

        public void Save()
        {
            if (_file == null || _selScript < 0) return;
            if (_file.Problem() is string problem) { _ = DialogHelper.ShowErrorAsync(problem, "Level script editor error"); return; }
            // A file that failed to load as a level script may be an ordinary script; saving would replace it.
            ScriptFile onDisk = new ScriptFile(_selScript, readFunctions: false, readActions: false);
            if (!onDisk.IsLevelScript && !onDisk.HasNoScripts)
            {
                _ = DialogHelper.ShowErrorAsync($"File {_selScript} holds ordinary scripts, so it was not saved as a level script.", "Level script editor error");
                return;
            }
            try
            {
                _file.SaveToFileDefaultDir(_selScript, _padding);
                SetClean();
                SaveNotice.Saved(UnsavedChangesDescription);
                StatusText = $"Saved level script {_selScript}.";
                AppEvents.RaiseLevelScriptSaved(this, _selScript);
            }
            catch (Exception ex) { _ = DialogHelper.ShowErrorAsync($"Save failed:\n{ex.Message}", "Level script editor error"); }
        }

        public async Task ImportAsync()
        {
            if (_file == null) return;
            FilePickerFileType filter = new FilePickerFileType("Level script") { Patterns = new[] { "*.lscr", "*.bin", "*.*" } };
            string path = await DialogHelper.OpenFileAsync(_owner, "Import level script", new[] { filter });
            if (path == null) return;
            try
            {
                LevelScriptFile imported = new LevelScriptFile();
                imported.ParseFile(path);
                _file.BufferSet.Clear();
                foreach (LevelScriptTrigger t in imported.BufferSet) _file.BufferSet.Add(t);
                RefreshTriggers();
                Dirty();
                SaveNotice.Imported($"level script {_selScript}", path);
            }
            catch (Exception ex) { await DialogHelper.ShowErrorAsync($"Import failed:\n{ex.Message}", "Level script editor error"); }
        }

        /// <summary>The level script's file in the file manager, as last saved.</summary>
        public void ShowFile()
        {
            if (_selScript < 0) return;
            SystemShell.RevealInFileManager(Path.Combine(Filesystem.Scripts, _selScript.ToString("D4")));
        }

        public async Task ExportAsync()
        {
            if (_file == null) return;
            FilePickerFileType filter = new FilePickerFileType("Level script") { Patterns = new[] { "*.lscr" } };
            string path = await DialogHelper.SaveFileAsync(_owner, "Export level script", new[] { filter }, $"levelscript_{_selScript:D4}.lscr");
            if (path == null) return;
            try { _file.WriteFile(path, _padding); SaveNotice.Exported(path); }
            catch (Exception ex) { await DialogHelper.ShowErrorAsync($"Export failed:\n{ex.Message}", "Level script editor error"); }
        }
    }
}
