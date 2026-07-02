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
using System.Threading.Tasks;
using global::Avalonia.Controls;
using global::Avalonia.Platform.Storage;
using global::Avalonia.Threading;
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
        private string _scriptText = "";
        private int _selectedIndex = -1;
        private int _selectedEditorThemeIndex;
        private string _searchText = "";
        private bool _searchFuzzy;
        private string _searchStatusText = "No search yet.";
        private ScriptSearchResult _selectedSearchResult;
        private ScriptDiagnostic _selectedDiagnostic;
        private string _diagnosticsStatusText = "No compile diagnostics yet.";
        private int _selectedSidebarTabIndex;
        private string _statusText = "Not loaded";
        private RotomLanguageServerClient _lsp;
        private string _lspOpenPath;
        private int _documentVersion;
        private bool _diagnosticsAreLive;

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
                    SettingsManager.Settings.rotomEditorTheme = SelectedEditorThemeName;
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
                if (value == _selectedIndex) return;
                if (!_suppress && _dirty) SaveSourceOnly(false);
                if (!Set(ref _selectedIndex, value)) return;
                OnEditorStateChanged();
                if (!_suppress) LoadSelectedFile();
            }
        }

        public string ScriptText
        {
            get => _scriptText;
            set
            {
                if (!Set(ref _scriptText, value) || _suppress) return;
                Dirty();
                _ = SendCurrentDocumentChangedToLsp();
            }
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
        public bool CanAdd => !IsBusy && RotomTool.IsAvailable;
        public bool CanSearchProject => !IsBusy && _sourceFiles.Count > 0 && !string.IsNullOrWhiteSpace(SearchText);
        public bool HasDiagnostics => Diagnostics.Count > 0;
        public bool HasUnsavedChanges => _dirty;
        public string SelectedScriptPath => _currentPath;
        public string UnsavedChangesDescription => _currentPath == null
            ? "Rotom script"
            : "Rotom script " + DisplayPath(_currentPath);

        public string StatusText
        {
            get => _statusText;
            set => Set(ref _statusText, value);
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (!Set(ref _searchText, value)) return;
                OnPropertyChanged(nameof(CanSearchProject));
            }
        }

        public bool SearchFuzzy
        {
            get => _searchFuzzy;
            set => Set(ref _searchFuzzy, value);
        }

        public string SearchStatusText
        {
            get => _searchStatusText;
            set => Set(ref _searchStatusText, value);
        }

        public ScriptSearchResult SelectedSearchResult
        {
            get => _selectedSearchResult;
            set => Set(ref _selectedSearchResult, value);
        }

        public string DiagnosticsStatusText
        {
            get => _diagnosticsStatusText;
            set => Set(ref _diagnosticsStatusText, value);
        }

        public ScriptDiagnostic SelectedDiagnostic
        {
            get => _selectedDiagnostic;
            set => Set(ref _selectedDiagnostic, value);
        }

        public int SelectedSidebarTabIndex
        {
            get => _selectedSidebarTabIndex;
            set => Set(ref _selectedSidebarTabIndex, value);
        }

        public ScriptEditorViewModel() : this(false)
        {
            if (!Design.IsDesignMode) return;
            ScriptNames.Add("0000.rotom");
            _selectedIndex = 0;
            _scriptText = "script Main #0:\n\tEnd\n";
            _statusText = "Design preview";
            _isBusy = false;
            _isReadOnly = false;
        }

        public ScriptEditorViewModel(bool _)
        {
            string savedTheme = SettingsManager.Settings?.rotomEditorTheme ?? "OneDark";
            int savedThemeIndex = Array.FindIndex(EditorThemeNames,
                theme => string.Equals(theme, savedTheme, StringComparison.OrdinalIgnoreCase));
            _selectedEditorThemeIndex = savedThemeIndex >= 0 ? savedThemeIndex : 0;
        }

        public async Task SetupAsync(Window owner)
        {
            _owner = owner;
            IsBusy = true;
            IsReadOnly = true;
            StatusText = "Preparing Rotom project...";

            try
            {
                if (!RotomTool.IsAvailable)
                    throw new FileNotFoundException("rotom.exe was not found in DSPRE's Tools folder.", RotomTool.ExePath);

                DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.scripts });

                if (!hasRotomProject)
                {
                    EnsureDspreSourceRoot();

                    StatusText = "Initializing Rotom project...";
                    await RunRequiredRotomCommand("init", "--non-interactive");
                    RefreshRotomProjectState();
                }

                if (LegacyScriptsExist())
                {
                    StatusText = "Converting legacy scripts to Rotom...";
                    await RunRequiredRotomCommand("convert", "--non-interactive");
                    RefreshRotomProjectState();
                }

                RefreshScriptList();
                if (ScriptNames.Count == 0)
                {
                    StatusText = "Decompiling binary scripts to Rotom...";
                    await RunRequiredRotomCommand("decompile");
                    RefreshScriptList();
                }

                IsReadOnly = ScriptNames.Count == 0;

                if (ScriptNames.Count == 0)
                {
                    StatusText = "No Rotom source files found.";
                    return;
                }

                _suppress = true;
                _selectedIndex = InitialSelection();
                OnPropertyChanged(nameof(SelectedScriptIndex));
                _suppress = false;
                LoadSelectedFile();
                await StartLanguageServerAsync();
            }
            catch (Exception ex)
            {
                IsReadOnly = true;
                StatusText = "Error: " + ex.Message;
                await DialogHelper.ShowError("Failed to prepare Rotom scripts:\n" + ex.Message, "Script Editor");
            }
            finally
            {
                IsBusy = false;
            }
        }

        public void SearchProject()
        {
            SearchResults.Clear();
            SelectedSearchResult = null;

            string needle = (SearchText ?? "").Trim();
            if (needle.Length == 0)
            {
                SearchStatusText = "Enter search text.";
                OnPropertyChanged(nameof(CanSearchProject));
                return;
            }

            int readErrors = 0;
            bool limited = false;

            foreach (string path in _sourceFiles)
            {
                string text;
                try
                {
                    text = _currentPath != null && SamePath(path, _currentPath)
                        ? ScriptText ?? ""
                        : File.ReadAllText(path);
                }
                catch
                {
                    readErrors++;
                    continue;
                }

                string[] lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].TrimEnd('\r');
                    int column;
                    if (SearchFuzzy)
                    {
                        if (!TryFuzzyMatch(line, needle, out column)) continue;
                    }
                    else
                    {
                        int index = line.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
                        if (index < 0) continue;
                        column = index + 1;
                    }

                    int lineNumber = i + 1;
                    string preview = line.Trim();
                    if (preview.Length == 0) preview = "(blank line)";
                    int remainingLineLength = Math.Max(1, line.Length - column + 1);
                    int selectionLength = Math.Min(needle.Length, remainingLineLength);
                    SearchResults.Add(new ScriptSearchResult(
                        path,
                        lineNumber,
                        column,
                        selectionLength,
                        DisplayPath(path) + ":" + lineNumber + ":" + column,
                        preview));

                    if (SearchResults.Count >= SearchResultLimit)
                    {
                        limited = true;
                        break;
                    }
                }

                if (limited) break;
            }

            string suffix = readErrors == 0 ? "" : " (" + readErrors + " file(s) could not be read)";
            SearchStatusText = SearchResults.Count == 1
                ? "1 result" + suffix + "."
                : SearchResults.Count + " results" + suffix + (limited ? " (limited)." : ".");
            SelectedSidebarTabIndex = 0;
            OnPropertyChanged(nameof(CanSearchProject));
        }

        public bool OpenSearchResult(ScriptSearchResult result)
        {
            if (result == null) return false;

            int index = _sourceFiles.FindIndex(path => SamePath(path, result.Path));
            if (index < 0)
            {
                SearchStatusText = "Result source no longer exists.";
                return false;
            }

            SelectedScriptIndex = index;
            StatusText = "Opened " + result.Display + ".";
            return true;
        }

        public bool OpenDiagnostic(ScriptDiagnostic diagnostic)
        {
            if (diagnostic == null) return false;

            int index = _sourceFiles.FindIndex(path => SamePath(path, diagnostic.Path));
            if (index < 0)
            {
                DiagnosticsStatusText = "Diagnostic source no longer exists.";
                return false;
            }

            SelectedScriptIndex = index;
            StatusText = "Opened " + diagnostic.Display + ".";
            return true;
        }

        public void ClearDiagnostics()
        {
            Diagnostics.Clear();
            SelectedDiagnostic = null;
            _diagnosticsAreLive = false;
            DiagnosticsStatusText = "No diagnostics.";
            OnPropertyChanged(nameof(HasDiagnostics));
        }

        public void ShutdownLsp()
        {
            if (_lsp == null) return;
            _lsp.DiagnosticsPublished -= OnLspDiagnosticsPublished;
            _lsp.Dispose();
            _lsp = null;
            _lspOpenPath = null;
        }

        public async Task<ScriptNavigationTarget> GoToDefinitionAsync(int line, int column)
        {
            if (_lsp == null || !_lsp.IsRunning || string.IsNullOrWhiteSpace(_currentPath))
            {
                StatusText = "Goto definition unavailable: live language service is not running.";
                return null;
            }

            try
            {
                if (Path.GetExtension(_currentPath).Equals(".rotom", StringComparison.OrdinalIgnoreCase))
                    await SendCurrentDocumentChangedToLsp();

                RotomLspLocation target = await _lsp.DefinitionAsync(_currentPath, line, column);
                if (target == null || string.IsNullOrWhiteSpace(target.Path))
                {
                    StatusText = "No definition found.";
                    return null;
                }

                int index = _sourceFiles.FindIndex(path => SamePath(path, target.Path));
                if (index < 0)
                {
                    RefreshScriptList();
                    index = _sourceFiles.FindIndex(path => SamePath(path, target.Path));
                }

                if (index < 0)
                {
                    StatusText = "Definition is outside the Rotom source list: " + DisplayPath(target.Path) + ".";
                    return null;
                }

                SelectedScriptIndex = index;
                StatusText = "Opened definition at " + DisplayPath(target.Path) + ":" + target.Line + ":" + target.Column + ".";
                return new ScriptNavigationTarget(target.Line, target.Column, target.SelectionLength);
            }
            catch (Exception ex)
            {
                AppLogger.Warn("rotom-lsp definition failed: " + ex.Message);
                StatusText = "Goto definition failed: " + ex.Message;
                return null;
            }
        }

        public async Task<string> HoverAsync(int line, int column)
        {
            if (_lsp == null || !_lsp.IsRunning || string.IsNullOrWhiteSpace(_currentPath))
                return null;

            try
            {
                return await _lsp.HoverAsync(_currentPath, line, column);
            }
            catch (Exception ex)
            {
                AppLogger.Warn("rotom-lsp hover failed: " + ex.Message);
                return null;
            }
        }

        public async Task SaveAsync()
        {
            if (_saving || !IsEditable) return;
            _saving = true;
            try
            {
                SaveSourceOnly(true);
                await CompileAsync(false);
            }
            catch (Exception ex)
            {
                await DialogHelper.ShowError("Save failed:\n" + ex.Message, "Script Editor");
            }
            finally
            {
                _saving = false;
            }
        }

        public async Task CompileAsync(bool saveCurrentFile = true)
        {
            if (IsBusy || IsReadOnly) return;

            try
            {
                if (saveCurrentFile && _dirty) SaveSourceOnly(false);
                IsBusy = true;
                StatusText = "Compiling Rotom project...";

                var result = await RotomTool.RunAsync("compile", "--json");
                string summary = RotomTool.FormatResult(result);
                int diagnostics = UpdateCompileDiagnostics(result);

                if (result.Success)
                {
                    StatusText = diagnostics == 0
                        ? "Compile successful: " + summary
                        : "Compile successful with " + diagnostics + " warning(s): " + summary;
                }
                else
                {
                    StatusText = "Compile failed: " + summary;
                }

                if (!result.Success && diagnostics == 0)
                    await DialogHelper.ShowError("rotom compile failed:\n" + RotomTool.FormatDetails(result), "Script Editor");
            }
            catch (Exception ex)
            {
                StatusText = "Compile failed: " + ex.Message;
                DiagnosticsStatusText = "Compile failed before diagnostics were available.";
                await DialogHelper.ShowError("Compile failed:\n" + ex.Message, "Script Editor");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private int UpdateCompileDiagnostics(RotomTool.Result result)
        {
            Diagnostics.Clear();
            SelectedDiagnostic = null;
            _diagnosticsAreLive = false;

            if (result == null || string.IsNullOrWhiteSpace(result.Stdout))
            {
                DiagnosticsStatusText = "No compile diagnostics.";
                OnPropertyChanged(nameof(HasDiagnostics));
                return 0;
            }

            try
            {
                using var doc = JsonDocument.Parse(result.Stdout);
                JsonElement root = doc.RootElement;

                if (root.TryGetProperty("failures", out var failures) && failures.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement failure in failures.EnumerateArray())
                    {
                        string path = ResolveProjectPath(failure.ReadString("path"));
                        JsonElement error = failure.TryGetProperty("error", out var err) ? err : default;
                        string kind = error.ReadString("type") ?? "Error";
                        JsonElement details = error.ValueKind == JsonValueKind.Object && error.TryGetProperty("details", out var d) ? d : default;
                        string message = details.ReadString("message") ?? kind;
                        TryReadSpan(details, out int start, out int end);
                        AddDiagnostic("Error", kind, path, start, end, message);
                    }
                }

                if (root.TryGetProperty("successes", out var successes) && successes.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement success in successes.EnumerateArray())
                    {
                        string path = ResolveProjectPath(success.ReadString("input"));
                        if (!success.TryGetProperty("warnings", out var warnings) || warnings.ValueKind != JsonValueKind.Array)
                            continue;

                        foreach (JsonElement warning in warnings.EnumerateArray())
                        {
                            string kind = warning.ReadString("type") ?? "Warning";
                            JsonElement details = warning.ValueKind == JsonValueKind.Object && warning.TryGetProperty("details", out var d) ? d : default;
                            TryReadSpan(details, out int start, out int end);
                            AddDiagnostic("Warning", kind, path, start, end, WarningMessage(kind, details));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error("Failed to parse rotom compile diagnostics: " + ex.Message);
                DiagnosticsStatusText = "Could not parse compile diagnostics.";
                OnPropertyChanged(nameof(HasDiagnostics));
                return 0;
            }

            int errors = Diagnostics.Count(d => d.Severity == "Error");
            int warningsCount = Diagnostics.Count(d => d.Severity == "Warning");
            DiagnosticsStatusText = errors == 0 && warningsCount == 0
                ? "No compile diagnostics."
                : errors + " error(s), " + warningsCount + " warning(s).";
            if (Diagnostics.Count > 0) SelectedSidebarTabIndex = 1;
            OnPropertyChanged(nameof(HasDiagnostics));
            return Diagnostics.Count;
        }

        private void AddDiagnostic(string severity, string kind, string path, int byteStart, int byteEnd, string message)
        {
            string source = ReadSourceForPath(path);
            LineColumnForByteOffset(source ?? "", byteStart, out int line, out int column);
            string preview = LineText(source, line);
            if (string.IsNullOrWhiteSpace(preview)) preview = message;

            int selectionLength = 1;
            if (!string.IsNullOrEmpty(source) && byteEnd > byteStart)
            {
                int startIndex = CharIndexFromUtf8Offset(source, byteStart);
                int endIndex = CharIndexFromUtf8Offset(source, byteEnd);
                selectionLength = Math.Max(1, endIndex - startIndex);
            }

            Diagnostics.Add(new ScriptDiagnostic(
                severity,
                kind,
                path,
                line,
                column,
                selectionLength,
                message,
                DisplayPath(path) + ":" + line + ":" + column,
                preview.Trim()));
        }

        private async Task StartLanguageServerAsync()
        {
            if (!RotomTool.IsLspAvailable)
            {
                DiagnosticsStatusText = "Live diagnostics unavailable: rotom-lsp.exe was not found.";
                return;
            }

            try
            {
                ShutdownLsp();
                _lsp = new RotomLanguageServerClient();
                _lsp.DiagnosticsPublished += OnLspDiagnosticsPublished;
                await _lsp.StartAsync();
                await OpenCurrentDocumentInLsp();
            }
            catch (Exception ex)
            {
                ShutdownLsp();
                AppLogger.Warn("Failed to start rotom-lsp: " + ex.Message);
                DiagnosticsStatusText = "Live diagnostics unavailable: " + ex.Message;
            }
        }

        private void OnLspDiagnosticsPublished(object sender, RotomLspDiagnosticsEventArgs e)
            => Dispatcher.UIThread.Post(() => ApplyLspDiagnostics(e));

        private void ApplyLspDiagnostics(RotomLspDiagnosticsEventArgs e)
        {
            if (e == null || string.IsNullOrWhiteSpace(e.Path) || string.IsNullOrWhiteSpace(_currentPath))
                return;
            if (!SamePath(e.Path, _currentPath)) return;

            Diagnostics.Clear();
            SelectedDiagnostic = null;
            _diagnosticsAreLive = true;

            string source = ReadSourceForPath(e.Path);
            foreach (RotomLspDiagnostic diagnostic in e.Diagnostics)
            {
                string preview = LineText(source, diagnostic.Line);
                if (string.IsNullOrWhiteSpace(preview)) preview = diagnostic.Message;

                Diagnostics.Add(new ScriptDiagnostic(
                    diagnostic.Severity,
                    diagnostic.Kind,
                    e.Path,
                    diagnostic.Line,
                    diagnostic.Column,
                    diagnostic.SelectionLength,
                    diagnostic.Message,
                    DisplayPath(e.Path) + ":" + diagnostic.Line + ":" + diagnostic.Column,
                    preview.Trim()));
            }

            int errors = Diagnostics.Count(d => d.Severity == "Error");
            int warnings = Diagnostics.Count(d => d.Severity == "Warning");
            DiagnosticsStatusText = Diagnostics.Count == 0
                ? "No live diagnostics."
                : "Live diagnostics: " + errors + " error(s), " + warnings + " warning(s).";
            if (Diagnostics.Count > 0) SelectedSidebarTabIndex = 1;
            OnPropertyChanged(nameof(HasDiagnostics));
        }

        public void AddScriptFile()
        {
            try
            {
                var roots = SourceRoots().ToList();
                string root = roots.FirstOrDefault() ?? Path.Combine(RotomTool.ProjectRoot, "expanded", "scripts");
                Directory.CreateDirectory(root);

                int id = NextScriptId();
                string path;
                do
                {
                    path = Path.Combine(root, id.ToString("D4") + ".rotom");
                    id++;
                } while (File.Exists(path));

                File.WriteAllText(path, "script Main #0:\n\tEnd\n");
                RefreshScriptList();
                SelectedScriptIndex = _sourceFiles.FindIndex(p => SamePath(p, path));
                StatusText = "Added " + DisplayPath(path) + ".";
            }
            catch (Exception ex)
            {
                _ = DialogHelper.ShowError("Couldn't add Rotom script:\n" + ex.Message, "Script Editor");
            }
        }

        public async Task ImportAsync()
        {
            if (!IsEditable) return;

            var filter = new FilePickerFileType("Rotom source") { Patterns = new[] { "*.rotom", "*.json", "*.*" } };
            string path = await DialogHelper.OpenFile(_owner, "Import Rotom source", new[] { filter });
            if (path == null) return;

            try
            {
                ScriptText = File.ReadAllText(path);
                SaveSourceOnly(true);
                StatusText = "Imported into " + DisplayPath(_currentPath) + ".";
            }
            catch (Exception ex)
            {
                await DialogHelper.ShowError("Import failed:\n" + ex.Message, "Import Error");
            }
        }

        public async Task ExportAsync()
        {
            if (_currentPath == null) return;

            string extension = Path.GetExtension(_currentPath);
            var filter = new FilePickerFileType("Rotom source") { Patterns = new[] { "*.rotom", "*.json", "*.*" } };
            string path = await DialogHelper.SaveFile(_owner, "Export Rotom source", new[] { filter },
                Path.GetFileNameWithoutExtension(_currentPath) + extension);
            if (path == null) return;

            try
            {
                File.WriteAllText(path, ScriptText ?? "");
                StatusText = "Exported " + DisplayPath(_currentPath) + ".";
            }
            catch (Exception ex)
            {
                await DialogHelper.ShowError("Export failed:\n" + ex.Message, "Export Error");
            }
        }

        public void SaveChanges() => _ = SaveAsync();

        public void DiscardChanges()
        {
            SetClean();
            LoadSelectedFile();
        }

        private async Task RunRequiredRotomCommand(params string[] args)
        {
            var result = await RotomTool.RunAsync(args);
            if (!result.Success)
                throw new InvalidOperationException("rotom " + string.Join(" ", args) + " failed:\n" + RotomTool.FormatDetails(result));
        }

        private void EnsureDspreSourceRoot()
        {
            Directory.CreateDirectory(Path.Combine(RotomTool.ProjectRoot, "expanded", "scripts"));
        }

        private bool LegacyScriptsExist()
            => SourceRoots().Any(root =>
                Directory.Exists(root)
                && Directory.EnumerateFiles(root, "*.script", SearchOption.AllDirectories).Any());

        // Minimal rotom.toml parsing — avoids a TOML dependency but only handles a flat source_roots = ["..."] array. Replace with Tomlyn if the config schema grows.
        private IEnumerable<string> SourceRoots()
        {
            string configPath = Path.Combine(RotomTool.ProjectRoot, "rotom.toml");
            if (File.Exists(configPath))
            {
                string config = File.ReadAllText(configPath);
                var match = Regex.Match(config, @"source_roots\s*=\s*\[(.*?)\]", RegexOptions.Singleline);
                if (match.Success)
                {
                    bool found = false;
                    foreach (Match quoted in Regex.Matches(match.Groups[1].Value, @"""([^""]+)""|'([^']+)'"))
                    {
                        found = true;
                        string path = quoted.Groups[1].Success ? quoted.Groups[1].Value : quoted.Groups[2].Value;
                        yield return Path.IsPathRooted(path) ? path : Path.Combine(RotomTool.ProjectRoot, path);
                    }
                    if (found) yield break;
                }
            }

            foreach (string path in new[]
            {
                Path.Combine(RotomTool.ProjectRoot, "expanded", "scripts"),
                Path.Combine(RotomTool.ProjectRoot, "scripts"),
                Path.Combine(RotomTool.ProjectRoot, ".rotom", "scripts")
            })
            {
                if (Directory.Exists(path)) yield return path;
            }
        }

        private void RefreshScriptList()
        {
            _sourceFiles.Clear();
            ScriptNames.Clear();

            foreach (string root in SourceRoots().Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!Directory.Exists(root)) continue;
                var files = Directory
                    .EnumerateFiles(root, "*", SearchOption.AllDirectories)
                    .Where(path =>
                    {
                        string ext = Path.GetExtension(path);
                        return ext.Equals(".rotom", StringComparison.OrdinalIgnoreCase)
                            || ext.Equals(".json", StringComparison.OrdinalIgnoreCase);
                    });
                _sourceFiles.AddRange(files);
            }

            _sourceFiles.Sort(StringComparer.OrdinalIgnoreCase);
            foreach (string file in _sourceFiles)
                ScriptNames.Add(DisplayPath(file));

            SearchResults.Clear();
            SelectedSearchResult = null;
            OnPropertyChanged(nameof(CanSearchProject));
        }

        private int InitialSelection()
        {
            int exact = _sourceFiles.FindIndex(path =>
                int.TryParse(Path.GetFileNameWithoutExtension(path), out int id) && id == InitialIndex);
            if (exact >= 0) return exact;
            return Math.Min(Math.Max(0, InitialIndex), _sourceFiles.Count - 1);
        }

        private int NextScriptId()
        {
            int max = -1;
            foreach (string file in _sourceFiles)
            {
                if (int.TryParse(Path.GetFileNameWithoutExtension(file), out int id))
                    max = Math.Max(max, id);
            }
            return max + 1;
        }

        private void LoadSelectedFile()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _sourceFiles.Count) return;

            string oldPath = _currentPath;
            _currentPath = _sourceFiles[_selectedIndex];
            _suppress = true;
            ScriptText = File.ReadAllText(_currentPath);
            _suppress = false;
            SetClean();
            if (_diagnosticsAreLive && (string.IsNullOrWhiteSpace(oldPath) || !SamePath(oldPath, _currentPath)))
            {
                Diagnostics.Clear();
                SelectedDiagnostic = null;
                DiagnosticsStatusText = "Waiting for live diagnostics.";
                OnPropertyChanged(nameof(HasDiagnostics));
            }
            IsReadOnly = false;
            StatusText = "Loaded " + DisplayPath(_currentPath) + ".";
            OnPropertyChanged(nameof(SelectedScriptPath));
            OnPropertyChanged(nameof(UnsavedChangesDescription));
            _ = OpenCurrentDocumentInLsp();
        }

        private void SaveSourceOnly(bool showStatus)
        {
            if (_currentPath == null) return;
            File.WriteAllText(_currentPath, ScriptText ?? "");
            SetClean();
            if (showStatus) StatusText = "Saved " + DisplayPath(_currentPath) + ".";
            _ = SendCurrentDocumentSavedToLsp();
        }

        private async Task OpenCurrentDocumentInLsp()
        {
            if (_lsp == null || !_lsp.IsRunning || string.IsNullOrWhiteSpace(_currentPath)) return;

            try
            {
                if (!string.IsNullOrWhiteSpace(_lspOpenPath))
                {
                    if (SamePath(_lspOpenPath, _currentPath))
                    {
                        _documentVersion++;
                        await _lsp.DidChangeAsync(_currentPath, _documentVersion, ScriptText ?? "");
                        return;
                    }

                    await _lsp.DidCloseAsync(_lspOpenPath);
                }

                _documentVersion = 1;
                _lspOpenPath = _currentPath;
                await _lsp.DidOpenAsync(_currentPath, LanguageIdForPath(_currentPath), _documentVersion, ScriptText ?? "");
            }
            catch (Exception ex)
            {
                AppLogger.Warn("rotom-lsp open failed: " + ex.Message);
            }
        }

        private async Task SendCurrentDocumentChangedToLsp()
        {
            if (_lsp == null || !_lsp.IsRunning || string.IsNullOrWhiteSpace(_currentPath)) return;

            try
            {
                if (string.IsNullOrWhiteSpace(_lspOpenPath) || !SamePath(_lspOpenPath, _currentPath))
                {
                    await OpenCurrentDocumentInLsp();
                    return;
                }

                _documentVersion++;
                await _lsp.DidChangeAsync(_currentPath, _documentVersion, ScriptText ?? "");
            }
            catch (Exception ex)
            {
                AppLogger.Warn("rotom-lsp change failed: " + ex.Message);
            }
        }

        private async Task SendCurrentDocumentSavedToLsp()
        {
            if (_lsp == null || !_lsp.IsRunning || string.IsNullOrWhiteSpace(_currentPath)) return;

            try
            {
                if (string.IsNullOrWhiteSpace(_lspOpenPath) || !SamePath(_lspOpenPath, _currentPath))
                    await OpenCurrentDocumentInLsp();
                await _lsp.DidSaveAsync(_currentPath);
            }
            catch (Exception ex)
            {
                AppLogger.Warn("rotom-lsp save failed: " + ex.Message);
            }
        }

        private void Dirty()
        {
            if (_dirty) return;
            _dirty = true;
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        private void SetClean()
        {
            if (!_dirty) return;
            _dirty = false;
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        private void OnEditorStateChanged()
        {
            OnPropertyChanged(nameof(IsEditable));
            OnPropertyChanged(nameof(CanAdd));
            OnPropertyChanged(nameof(CanSearchProject));
        }

        private string ReadSourceForPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return "";
            if (_currentPath != null && SamePath(path, _currentPath)) return ScriptText ?? "";
            try { return File.Exists(path) ? File.ReadAllText(path) : ""; }
            catch { return ""; }
        }

        private static string ResolveProjectPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return "";
            return Path.IsPathRooted(path) ? path : Path.Combine(RotomTool.ProjectRoot, path);
        }

        private static void TryReadSpan(JsonElement details, out int start, out int end)
        {
            start = 0;
            end = 0;

            if (details.ValueKind == JsonValueKind.Object
                && details.TryGetProperty("span", out var span)
                && span.ValueKind == JsonValueKind.Object
                && span.TryGetProperty("start", out var startElement)
                && startElement.TryGetInt32(out int startValue))
            {
                start = startValue;
                if (span.TryGetProperty("end", out var endElement) && endElement.TryGetInt32(out int endValue))
                    end = endValue;
            }
        }

        private static string WarningMessage(string kind, JsonElement details)
        {
            string name = details.ReadString("name");
            string command = details.ReadString("command");
            string condition = details.ReadString("condition");

            return kind switch
            {
                "UnusedAlias" => "Alias '" + name + "' is never used",
                "ShadowedAlias" => "Alias '" + name + "' shadows a previous alias definition",
                "MissingSlot" => "Script slot #" + details.ReadString("slot") + " is empty; the next available script pointer will be reused",
                "MessageLineTooLong" => "Message line " + details.ReadString("line_index") + " exceeds the maximum dialog width",
                "VariantConditionUnresolvable" => "Could not evaluate variant condition '" + condition + "' for command '" + command + "'",
                _ => kind
            };
        }

        private static void LineColumnForByteOffset(string text, int byteOffset, out int line, out int column)
        {
            line = 1;
            column = 1;
            int charIndex = CharIndexFromUtf8Offset(text, Math.Max(0, byteOffset));

            for (int i = 0; i < charIndex && i < text.Length; i++)
            {
                if (text[i] == '\r')
                {
                    line++;
                    column = 1;
                    if (i + 1 < charIndex && i + 1 < text.Length && text[i + 1] == '\n') i++;
                }
                else if (text[i] == '\n')
                {
                    line++;
                    column = 1;
                }
                else
                {
                    column++;
                }
            }
        }

        private static int CharIndexFromUtf8Offset(string text, int byteOffset)
        {
            int bytes = 0;
            for (int i = 0; i < text.Length;)
            {
                int charCount = char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]) ? 2 : 1;
                int charBytes = Encoding.UTF8.GetByteCount(text.AsSpan(i, charCount));
                if (bytes + charBytes > byteOffset) return i;
                bytes += charBytes;
                i += charCount;
            }
            return text.Length;
        }

        private static string LineText(string text, int line)
        {
            if (string.IsNullOrEmpty(text)) return "";

            string[] lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            if (line < 1 || line > lines.Length) return "";
            return lines[line - 1].TrimEnd('\r');
        }

        private static bool TryFuzzyMatch(string text, string query, out int column)
        {
            column = 1;
            int queryIndex = 0;
            int firstMatch = -1;

            for (int i = 0; i < text.Length && queryIndex < query.Length; i++)
            {
                if (char.ToUpperInvariant(text[i]) != char.ToUpperInvariant(query[queryIndex])) continue;
                if (firstMatch < 0) firstMatch = i;
                queryIndex++;
            }

            if (queryIndex != query.Length) return false;
            column = firstMatch + 1;
            return true;
        }

        private static bool SamePath(string left, string right)
            => string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);

        private static string LanguageIdForPath(string path)
            => Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase) ? "json" : "rotom";

        private static string DisplayPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return "(project)";

            string root = RotomTool.ProjectRoot;
            if (!string.IsNullOrWhiteSpace(root))
            {
                try { path = Path.GetRelativePath(root, path); }
                catch { }
            }
            return path.Replace('\\', '/');
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
        public ScriptDiagnostic(string severity, string kind, string path, int line, int column, int selectionLength, string message, string display, string preview)
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
}
