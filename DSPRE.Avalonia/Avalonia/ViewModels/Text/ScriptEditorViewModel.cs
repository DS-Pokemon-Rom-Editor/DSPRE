using DSPRE.Avalonia;
using DSPRE.Avalonia.Gl;
using DSPRE.Editors;
using DSPRE.HgEngine;
using AvaloniaEdit.Document;
using global::Avalonia.Controls;
using global::Avalonia.Platform.Storage;
using global::Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using static DSPRE.RomInfo;
using static MKDS_Course_Editor.NSBTP.NSBTP.NSBTP_File;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.ViewModels.Text
{
    public partial class ScriptEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, IDetachable
    {
        private const int SearchResultLimit = 1000;

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private bool Set<T>(ref T field, T value, [CallerMemberName] string name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(name);
            return true;
        }

        private readonly List<string> _sourceFiles = new List<string>();
        private Window _owner;
        private bool _dirty;
        private bool _saving;
        private bool _isBusy = true;
        private bool _isReadOnly = true;
        private bool _suppress;
        private string _currentPath;
        private readonly Dictionary<string, HgEngineOwnedFile> _managedByPath =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> _scriptIdByPath = new(StringComparer.OrdinalIgnoreCase);
        private int _rotomFileCount;
        private string _scriptText = "";
        private TextDocument _editorDocument;
        private bool _publishingScriptText;
        private int _selectedIndex = -1;
        private int _selectedEditorThemeIndex;
        private string _searchText = "";
        private bool _searchFuzzy;
        private string _searchStatusText = "";
        private ScriptSearchResult _selectedSearchResult;
        private ScriptDiagnostic _selectedDiagnostic;
        private string _diagnosticsStatusText = "No compile diagnostics yet.";
        private int _selectedSidebarTabIndex;
        private string _statusText = "Not loaded";
        private RotomLanguageServerClient _lsp;
        private string _lspOpenPath;
        private int _documentVersion;
        private CancellationTokenSource _lspChangeDelay;

        private const int LanguageServerChangeDelayMs = 300;

        public ObservableCollection<string> ScriptNames { get; } = new ObservableCollection<string>();
        public string[] EditorThemeNames { get; } = new[]
        {
            "OneDark",
            "VisualStudioDark",
            "DarkPlus",
            "Dracula",
            "Monokai",
            "DimmedMonokai",
            "KimbieDark",
            "SolarizedDark",
            "TomorrowNightBlue",
            "LightPlus",
            "VisualStudioLight",
            "Light",
            "QuietLight",
            "SolarizedLight",
            "AtomOneLight",
            "HighContrastLight",
            "Dark",
            "Red",
            "Abyss"
        };
        public ObservableCollection<ScriptSearchResult> SearchResults { get; } = new ObservableCollection<ScriptSearchResult>();
        public ObservableCollection<ScriptDiagnostic> Diagnostics { get; } = new ObservableCollection<ScriptDiagnostic>();
        public int InitialIndex { get; set; }
        /// <summary>Jump-table slot to scroll to after the first file loads, or 0 for none.</summary>
        public int InitialScriptNumber { get; set; }

        public int SelectedEditorThemeIndex
        {
            get => _selectedEditorThemeIndex;
            set
            {
                if (value < 0 || value >= EditorThemeNames.Length) value = 0;
                if (!Set(ref _selectedEditorThemeIndex, value)) return;
                OnPropertyChanged(nameof(SelectedEditorThemeName));
                if (SettingsManager.Settings != null)
                {
                    SettingsManager.Settings.RotomEditorTheme = SelectedEditorThemeName;
                    SettingsManager.Save();
                }
            }
        }

        public string SelectedEditorThemeName =>
            _selectedEditorThemeIndex >= 0 && _selectedEditorThemeIndex < EditorThemeNames.Length
                ? EditorThemeNames[_selectedEditorThemeIndex]
                : "OneDark";

        public int SelectedScriptIndex
        {
            get => _selectedIndex;
            set
            {
                if (RecordSwitchGuard.IsSnappingBack) return;
                if (value == _selectedIndex) return;
                if (!_suppress && _dirty && value >= 0 && _selectedIndex >= 0)
                {
                    // Snap the list back to the file still loaded until the user has answered.
                    RecordSwitchGuard.SnapBack(() => _selectedIndex, v => _selectedIndex = v, () => OnPropertyChanged(nameof(SelectedScriptIndex)));
                    _ = SelectScriptAsync(value);
                    return;
                }
                if (!Set(ref _selectedIndex, value)) return;
                OnEditorStateChanged();
                if (!_suppress) LoadSelectedFile();
            }
        }

        // Loads the file at index once edits are saved or discarded. False if the user stayed.
        private async Task<bool> SelectScriptAsync(int index)
        {
            if (index < 0 || index >= _sourceFiles.Count) return false;
            if (index == _selectedIndex) return true;
            string path = _sourceFiles[index];

            if (!await RecordSwitchGuard.ConfirmLeaveAsync(this, _owner, "script")) return false;

            int at = _sourceFiles.FindIndex(p => ProjectPaths.Same(p, path));
            if (at < 0) return false;
            SetClean();
            if (at != _selectedIndex)
            {
                _selectedIndex = at;
                OnPropertyChanged(nameof(SelectedScriptIndex));
                OnEditorStateChanged();
            }
            LoadSelectedFile();
            return true;
        }

        public string ScriptText
        {
            get => !_publishingScriptText && _editorDocument != null ? _editorDocument.Text : _scriptText;
            set
            {
                value ??= "";
                if (string.Equals(ScriptText, value, StringComparison.Ordinal)) return;

                _scriptText = value;
                _publishingScriptText = true;
                try { OnPropertyChanged(); }
                finally { _publishingScriptText = false; }

                if (_suppress) return;
                Dirty();
                ScheduleCurrentDocumentChangedToLsp();
            }
        }

        public void AttachEditorDocument(TextDocument document)
        {
            _editorDocument = document;
        }

        public void DetachEditorDocument(TextDocument document)
        {
            if (!ReferenceEquals(_editorDocument, document)) return;
            _scriptText = document?.Text ?? _scriptText;
            _editorDocument = null;
        }

        public void NotifyEditorDocumentChanged()
        {
            if (_suppress) return;
            Dirty();
            ScheduleCurrentDocumentChangedToLsp();
        }

        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (!Set(ref _isBusy, value)) return;
                OnEditorStateChanged();
            }
        }

        public bool IsReadOnly
        {
            get => _isReadOnly;
            set
            {
                if (!Set(ref _isReadOnly, value)) return;
                OnEditorStateChanged();
            }
        }

        public bool IsEditable => !IsBusy && !IsReadOnly && SelectedScriptIndex >= 0;
        public bool CanSave => IsEditable && _dirty;
        public bool CanAdd => !IsBusy && RotomTool.IsAvailable;

        /// <summary>True while at least one numbered script source exists.</summary>
        public bool CanRemoveLast => !IsBusy && RotomTool.IsAvailable && NextScriptId() > 0;

        public ScriptEditorViewModel() : this(false)
        {
            if (!Design.IsDesignMode) return;
            ScriptNames.Add("0000.rotom");
            _selectedIndex = 0;
            _scriptText = "script Main #1:\n\tEnd\n";
            _statusText = "Design preview";
            _isBusy = false;
            _isReadOnly = false;
        }

        public ScriptEditorViewModel(bool _)
        {
            string savedTheme = SettingsManager.Settings?.RotomEditorTheme ?? "OneDark";
            int savedThemeIndex = Array.FindIndex(EditorThemeNames,
                theme => string.Equals(theme, savedTheme, StringComparison.OrdinalIgnoreCase));
            _selectedEditorThemeIndex = savedThemeIndex >= 0 ? savedThemeIndex : 0;
            AppEvents.ScriptSourceSaved += OnSourceSavedElsewhere;
            DSPRE.ROMFiles.ScriptSourceSync.SourcesRefreshed += OnSourcesRefreshed;
            DSPRE.ROMFiles.ProjectSourceWatcher.ScriptsCompiled += OnCompiledAfterOutsideEdit;
        }

        private void Dirty()
        {
            if (_dirty) return;
            _dirty = true;
            OnPropertyChanged(nameof(HasUnsavedChanges));
            OnPropertyChanged(nameof(CanSave));
        }

        private void SetClean()
        {
            if (!_dirty) return;
            _dirty = false;
            OnPropertyChanged(nameof(HasUnsavedChanges));
            OnPropertyChanged(nameof(CanSave));
        }

        private void OnEditorStateChanged()
        {
            OnPropertyChanged(nameof(IsEditable));
            OnPropertyChanged(nameof(CanSave));
            OnPropertyChanged(nameof(CanAdd));
            OnPropertyChanged(nameof(CanRemoveLast));
            OnPropertyChanged(nameof(CanSearchProject));
        }
    }

    public class ScriptSearchResult
    {
        public ScriptSearchResult(string path, int line, int column, int selectionLength, string display, string preview)
        {
            Path = path;
            Line = line;
            Column = column;
            SelectionLength = selectionLength;
            Display = display;
            Preview = preview;
        }

        public string Path { get; }
        public int Line { get; }
        public int Column { get; }
        public int SelectionLength { get; }
        public string Display { get; }
        public string Preview { get; }

        public override string ToString() => Display + " " + Preview;
    }

    public class ScriptDiagnostic
    {
        /// <summary>A diagnostic row. isLive marks language server results.</summary>
        public ScriptDiagnostic(string severity, string kind, string path, int line, int column, int selectionLength, string message, string display, string preview, bool isLive = false)
        {
            Severity = severity;
            Kind = kind;
            Path = path;
            Line = line;
            Column = column;
            SelectionLength = selectionLength;
            Message = message;
            Display = display;
            Preview = preview;
            IsLive = isLive;
        }

        public string Severity { get; }
        public string Kind { get; }
        public string Path { get; }
        public int Line { get; }
        public int Column { get; }
        public int SelectionLength { get; }
        public string Message { get; }
        public string Display { get; }
        public string Preview { get; }
        public bool IsLive { get; }
        public string Source => IsLive ? "Live" : "Compile";

        public override string ToString() => Severity + " " + Display + " " + Message;
    }

    public class ScriptNavigationTarget
    {
        public ScriptNavigationTarget(int line, int column, int selectionLength)
        {
            Line = line;
            Column = column;
            SelectionLength = selectionLength;
        }

        public int Line { get; }
        public int Column { get; }
        public int SelectionLength { get; }
    }

    /// <summary>One outline row: numbered scripts show "#N", others are indented by depth.</summary>
    public sealed record OutlineEntry(string Name, int Line, int Depth, int? Number = null)
    {
        public string Display => Number is int n ? $"#{n}  {Name}" : new string(' ', Depth * 2) + Name;
    }
}
