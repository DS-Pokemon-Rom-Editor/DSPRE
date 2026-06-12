using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using global::Avalonia.Controls;
using global::Avalonia.Platform.Storage;
using DSPRE.Avalonia;
using DSPRE.CharMaps;
using DSPRE.Editors;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels
{
    // ── One editable text line ─────────────────────────────────────────────────
    public class TextLineVM : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Notify([CallerMemberName] string p = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));

        public int Index { get; set; }

        private string _number;
        public string Number { get => _number; set { _number = value; Notify(); } }

        private string _text;
        public string Text { get => _text; set { if (_text != value) { _text = value; Notify(); } } }

        public TextLineVM(int index, string text) { Index = index; _text = text; }
    }

    // ── Search result row ──────────────────────────────────────────────────────
    public class TextSearchResultVM
    {
        public int Archive { get; }
        public int Line { get; }
        public string Display { get; }
        public TextSearchResultVM(int archive, int line, string display)
        { Archive = archive; Line = line; Display = display; }
        public override string ToString() => Display;
    }

    /// <summary>
    /// Avalonia port of the WinForms <c>TextEditor</c>. Edits in-game text archives
    /// (dual binary/JSON format) — archive selection, per-line editing, add/remove
    /// strings &amp; archives, reorder, import/export, and search &amp; replace.
    /// </summary>
    public class TextEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (EqualityComparer<T>.Default.Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        // ── State ──────────────────────────────────────────────────────────────
        private Window _owner;
        private bool _isLoading;
        private TextArchive _current;

        public ObservableCollection<string> ArchiveNames { get; } = new ObservableCollection<string>();
        public ObservableCollection<TextLineVM> Lines { get; } = new ObservableCollection<TextLineVM>();
        public ObservableCollection<TextSearchResultVM> SearchResults { get; } = new ObservableCollection<TextSearchResultVM>();

        private int _selectedArchiveIndex = -1;
        public int SelectedArchiveIndex
        {
            get => _selectedArchiveIndex;
            set
            {
                if (!Set(ref _selectedArchiveIndex, value)) return;
                if (_isLoading || value < 0) return;
                _ = OnArchiveSelectedAsync(value);
            }
        }

        private int _selectedLineIndex = -1;
        public int SelectedLineIndex
        {
            get => _selectedLineIndex;
            set { if (Set(ref _selectedLineIndex, value)) { OnPropertyChanged(nameof(CanMoveUp)); OnPropertyChanged(nameof(CanMoveDown)); } }
        }

        public bool CanMoveUp => _selectedLineIndex > 0;
        public bool CanMoveDown => _selectedLineIndex >= 0 && _selectedLineIndex < Lines.Count - 1;

        private bool _hexNumbering = true;
        public bool HexNumbering
        {
            get => _hexNumbering;
            set { if (Set(ref _hexNumbering, value)) { RenumberLines(); SettingsManager.Settings.textEditorPreferHex = value; } }
        }

        // ── Search / replace inputs ──────────────────────────────────────────────
        private string _searchText = "";
        public string SearchText { get => _searchText; set => Set(ref _searchText, value); }

        private string _replaceText = "";
        public string ReplaceText { get => _replaceText; set => Set(ref _replaceText, value); }

        private bool _searchAllArchives;
        public bool SearchAllArchives { get => _searchAllArchives; set => Set(ref _searchAllArchives, value); }

        private bool _caseSensitive;
        public bool CaseSensitive { get => _caseSensitive; set => Set(ref _caseSensitive, value); }

        private string _statusText = "Not loaded";
        public string StatusText { get => _statusText; set => Set(ref _statusText, value); }

        // ── Dirty tracking ───────────────────────────────────────────────────────
        private bool _dirty;
        public bool HasUnsavedChanges => _dirty;
        public string UnsavedChangesDescription => _current != null ? $"Text Archive {_current.ID}" : "Text Archive";

        public void SaveChanges() => Save();
        public void DiscardChanges() => SetClean();

        private void SetDirty()
        {
            if (_dirty) return;
            _dirty = true;
            OnPropertyChanged(nameof(HasUnsavedChanges));
            OnPropertyChanged(nameof(Title));
        }
        private void SetClean()
        {
            if (!_dirty) return;
            _dirty = false;
            OnPropertyChanged(nameof(HasUnsavedChanges));
            OnPropertyChanged(nameof(Title));
        }

        public string Title => _current != null
            ? $"Text Editor — Archive {_current.ID}{(_dirty ? " *" : "")}"
            : "Text Editor";

        // ── Design-time constructor ──────────────────────────────────────────────
        public TextEditorViewModel()
        {
            if (!Design.IsDesignMode) return;
            ArchiveNames.Add("Text Archive 0");
            Lines.Add(new TextLineVM(0, "Sample line 0") { Number = "0x0" });
            Lines.Add(new TextLineVM(1, "Sample line 1") { Number = "0x1" });
            StatusText = "Design mode";
        }

        // ── Runtime constructor ──────────────────────────────────────────────────
        public TextEditorViewModel(bool _)
        {
            _hexNumbering = SettingsManager.Settings.textEditorPreferHex;
        }
        public int InitialIndex { get; set; }

        // ── Setup ────────────────────────────────────────────────────────────────
        public async Task SetupAsync(Window owner)
        {
            _owner = owner;
            _isLoading = true;
            StatusText = "Loading text archives…";

            try
            {
                string unpackedPath = gameDirs[DirNames.textArchives].unpackedDir;
                string expandedPath = TextConverter.GetExpandedFolderPath();

                if (!Directory.Exists(expandedPath))
                {
                    Directory.CreateDirectory(expandedPath);
                    DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.textArchives });
                }
                if (!Directory.Exists(unpackedPath))
                    Directory.CreateDirectory(unpackedPath);

                // JSON files are only (re)written when missing or older than the binary.
                TextConverter.FolderToJSON(unpackedPath, expandedPath, CharMapManager.GetCharMapPath());

                ArchiveNames.Clear();
                int count = Filesystem.GetTextArchivesCount();
                for (int i = 0; i < count; i++)
                    ArchiveNames.Add("Text Archive " + i);

                _isLoading = false;
                StatusText = $"Loaded {count} text archives.";

                if (count > 0)
                    SelectedArchiveIndex = Math.Min(Math.Max(0, InitialIndex), count - 1);
            }
            catch (Exception ex)
            {
                StatusText = $"Error loading text archives: {ex.Message}";
                await DialogHelper.ShowError($"Failed to load text archives:\n{ex.Message}", "Text Editor Error");
            }
            finally
            {
                _isLoading = false;
            }
        }

        // ── Archive selection ────────────────────────────────────────────────────
        private async Task OnArchiveSelectedAsync(int index)
        {
            if (_dirty && _current != null)
            {
                var r = await DialogHelper.AskYesNoCancel(
                    "There are unsaved changes to the currently loaded Text Archive.\nDo you want to save them?",
                    "Text Editor - Unsaved changes");

                if (r == DialogHelper.MsgResult.Cancel)
                {
                    _isLoading = true;
                    SelectedArchiveIndex = _current.ID;
                    _isLoading = false;
                    return;
                }
                if (r == DialogHelper.MsgResult.Yes)
                    Save();
            }

            LoadArchive(index);
        }

        private void LoadArchive(int id)
        {
            _isLoading = true;
            try
            {
                _current = new TextArchive(id);

                foreach (var l in Lines) l.PropertyChanged -= OnLineChanged;
                Lines.Clear();
                for (int i = 0; i < _current.messages.Count; i++)
                {
                    var line = new TextLineVM(i, _current.messages[i]);
                    line.PropertyChanged += OnLineChanged;
                    Lines.Add(line);
                }
                RenumberLines();
                SetClean();
                StatusText = $"Loaded Text Archive {id} ({_current.messages.Count} lines).";
                OnPropertyChanged(nameof(Title));
            }
            finally
            {
                _isLoading = false;
            }
        }

        private void OnLineChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_isLoading || e.PropertyName != nameof(TextLineVM.Text)) return;
            if (sender is TextLineVM line && _current != null && line.Index < _current.messages.Count)
            {
                _current.messages[line.Index] = line.Text ?? "";
                SetDirty();
            }
        }

        private void RenumberLines()
        {
            for (int i = 0; i < Lines.Count; i++)
            {
                Lines[i].Index = i;
                Lines[i].Number = _hexNumbering ? "0x" + i.ToString("X") : i.ToString();
            }
        }

        // ── Add / remove strings ─────────────────────────────────────────────────
        public void AddString()
        {
            if (_current == null) return;
            _current.messages.Add("");
            var line = new TextLineVM(Lines.Count, "");
            line.PropertyChanged += OnLineChanged;
            Lines.Add(line);
            RenumberLines();
            SetDirty();
        }

        public void RemoveString()
        {
            if (_current == null || _current.messages.Count == 0) return;
            _current.messages.RemoveAt(_current.messages.Count - 1);
            var last = Lines[Lines.Count - 1];
            last.PropertyChanged -= OnLineChanged;
            Lines.RemoveAt(Lines.Count - 1);
            RenumberLines();
            SetDirty();
        }

        public void MoveSelectedUp()
        {
            int i = _selectedLineIndex;
            if (i <= 0 || i >= Lines.Count) return;
            (Lines[i].Text, Lines[i - 1].Text) = (Lines[i - 1].Text, Lines[i].Text);
            SelectedLineIndex = i - 1;
        }

        public void MoveSelectedDown()
        {
            int i = _selectedLineIndex;
            if (i < 0 || i >= Lines.Count - 1) return;
            (Lines[i].Text, Lines[i + 1].Text) = (Lines[i + 1].Text, Lines[i].Text);
            SelectedLineIndex = i + 1;
        }

        // ── Add / remove archives ────────────────────────────────────────────────
        public void AddArchive()
        {
            int newId = ArchiveNames.Count;
            var archive = new TextArchive(newId, new List<string> { "Your text here." });
            archive.SaveToExpandedDir(newId);

            (string binPath, string jsonPath) = TextArchive.GetFilePaths(newId);
            TextConverter.JSONToBin(jsonPath, binPath, CharMapManager.GetCharMapPath());

            ArchiveNames.Add("Text Archive " + newId);
            SelectedArchiveIndex = newId;
        }

        public async Task RemoveArchiveAsync()
        {
            if (ArchiveNames.Count == 0) return;
            if (!await DialogHelper.AskYesNo("Are you sure you want to delete the last Text Archive?", "Confirm deletion"))
                return;

            int lastIndex = ArchiveNames.Count - 1;
            try
            {
                File.Delete(TextArchive.GetFilePaths(lastIndex).jsonPath);
                File.Delete(TextArchive.GetFilePaths(lastIndex).binPath);
            }
            catch (Exception ex)
            {
                await DialogHelper.ShowError("Failed to delete Text Archive files: " + ex.Message, "Error");
                return;
            }

            if (SelectedArchiveIndex == lastIndex)
            {
                SetClean();
                _isLoading = true;
                SelectedArchiveIndex = lastIndex - 1;
                _isLoading = false;
                if (lastIndex - 1 >= 0) LoadArchive(lastIndex - 1);
            }
            ArchiveNames.RemoveAt(lastIndex);
        }

        // ── Save ─────────────────────────────────────────────────────────────────
        public void Save()
        {
            if (_current == null) return;
            _current.SaveToExpandedDir(_current.ID);
            SetClean();
            StatusText = $"Saved Text Archive {_current.ID}.";
            if (_current.ID == locationNamesTextNumber)
                ReloadHeaderEditorLocations(_current.messages);
        }

        // ── Import / export ──────────────────────────────────────────────────────
        public async Task ImportAsync()
        {
            if (_current == null) return;
            var filters = new[]
            {
                new FilePickerFileType("JSON Text Archive") { Patterns = new[] { "*.json" } },
                new FilePickerFileType("Binary Text Archive") { Patterns = new[] { "*.msg", "*.bin" } },
            };
            string path = await DialogHelper.OpenFile(_owner, "Import Text Archive", filters);
            if (path == null) return;

            string binPath = TextArchive.GetFilePaths(_current.ID).binPath;
            string jsonPath = TextArchive.GetFilePaths(_current.ID).jsonPath;
            string ext = Path.GetExtension(path).ToLowerInvariant();

            try
            {
                if (ext == ".msg" || ext == ".bin" || ext == "")
                {
                    File.Copy(path, binPath, true);
                    TextConverter.BinToJSON(binPath, jsonPath, CharMapManager.GetCharMapPath());
                }
                else if (ext == ".json")
                {
                    File.Copy(path, jsonPath, true);
                }
                LoadArchive(_current.ID);
                await DialogHelper.ShowInfo("Text Archive imported successfully!", "Import");
            }
            catch (Exception ex)
            {
                await DialogHelper.ShowError($"Import failed:\n{ex.Message}", "Import Error");
            }
        }

        public async Task ExportAsync()
        {
            if (_current == null) return;
            int id = _current.ID;
            var filters = new[]
            {
                new FilePickerFileType("JSON Text Archive") { Patterns = new[] { "*.json" } },
                new FilePickerFileType("Binary Text Archive") { Patterns = new[] { "*.msg" } },
            };
            string path = await DialogHelper.SaveFile(_owner, "Export Text Archive", filters, "Text Archive " + id);
            if (path == null) return;

            string ext = Path.GetExtension(path).ToLowerInvariant();
            try
            {
                if (ext == ".msg" || ext == "")
                {
                    string jsonPath = TextArchive.GetFilePaths(id).jsonPath;
                    TextConverter.JSONToBin(jsonPath, path, CharMapManager.GetCharMapPath());
                }
                else if (ext == ".json")
                {
                    File.Copy(TextArchive.GetFilePaths(id).jsonPath, path, true);
                }
                await DialogHelper.ShowInfo("Text Archive exported.", "Export");
            }
            catch (Exception ex)
            {
                await DialogHelper.ShowError($"Export failed:\n{ex.Message}", "Export Error");
            }
        }

        // ── Search / replace ─────────────────────────────────────────────────────
        public void Search()
        {
            if (string.IsNullOrEmpty(SearchText)) return;

            int first, last;
            if (SearchAllArchives) { first = 0; last = Filesystem.GetTextArchivesCount(); }
            else { first = SelectedArchiveIndex; last = first + 1; }
            last = Math.Min(last, 828);

            SearchResults.Clear();
            Func<string, bool> match = CaseSensitive
                ? (x => x.Contains(SearchText))
                : (x => x.IndexOf(SearchText, StringComparison.InvariantCultureIgnoreCase) >= 0);

            for (int i = first; i < last; i++)
            {
                var file = new TextArchive(i);
                for (int j = 0; j < file.messages.Count; j++)
                {
                    if (match(file.messages[j]))
                    {
                        string preview = file.messages[j].Substring(0, Math.Min(file.messages[j].Length, 40));
                        SearchResults.Add(new TextSearchResultVM(i, j,
                            $"({i:D3}) - #{j:D2} --- {preview}"));
                    }
                }
            }
            StatusText = $"Found {SearchResults.Count} match(es).";
        }

        public async Task ReplaceAsync()
        {
            if (string.IsNullOrEmpty(SearchText)) return;

            int first, last;
            string specify;
            if (SearchAllArchives)
            {
                first = 0; last = Filesystem.GetTextArchivesCount();
                specify = $" in every Text Bank of the game ({first} to {last})";
            }
            else
            {
                first = SelectedArchiveIndex; last = first + 1;
                specify = $" in the current text bank only ({first})";
            }

            string message = $"You are about to replace every occurrence of \"{SearchText}\" with \"{ReplaceText}\"{specify}." +
                             "\nThe operation can't be interrupted nor undone.\n\nProceed?";
            if (!await DialogHelper.AskYesNo(message, "Confirm to proceed")) return;

            last = Math.Min(last, 828);
            SearchResults.Clear();
            int edited = 0;

            for (int cur = first; cur < last; cur++)
            {
                var archive = new TextArchive(cur);
                bool found = false;
                for (int j = 0; j < archive.messages.Count; j++)
                {
                    if (CaseSensitive)
                    {
                        if (archive.messages[j].IndexOf(SearchText, StringComparison.Ordinal) >= 0)
                        {
                            archive.messages[j] = archive.messages[j].Replace(SearchText, ReplaceText);
                            found = true;
                        }
                    }
                    else
                    {
                        int pos;
                        while ((pos = archive.messages[j].IndexOf(SearchText, StringComparison.InvariantCultureIgnoreCase)) >= 0)
                        {
                            archive.messages[j] = archive.messages[j].Substring(0, pos) + ReplaceText +
                                                  archive.messages[j].Substring(pos + SearchText.Length);
                            found = true;
                        }
                    }
                }

                if (found)
                {
                    archive.SaveToExpandedDir(cur, showSuccessMessage: false);
                    SearchResults.Add(new TextSearchResultVM(cur, 0, $"Text archive ({cur}) - Successfully edited"));
                    edited++;
                }
            }

            // Reload the currently displayed archive so edits are visible.
            if (SelectedArchiveIndex >= 0) LoadArchive(SelectedArchiveIndex);
            StatusText = $"Replace complete — {edited} archive(s) edited.";
            await DialogHelper.ShowInfo("Operation completed.", "Replace All Text");
        }

        /// <summary>Navigate to a search result (used on double-click).</summary>
        public void GoToResult(TextSearchResultVM result)
        {
            if (result == null) return;
            if (SelectedArchiveIndex != result.Archive)
                SelectedArchiveIndex = result.Archive;
            if (result.Line >= 0 && result.Line < Lines.Count)
                SelectedLineIndex = result.Line;
        }

        // ── Header editor location list refresh (WinForms bridge) ─────────────────
        private static void ReloadHeaderEditorLocations(IEnumerable<string> contents)
        {
            var headerEditor = EditorPanels.headerEditor;
            if (headerEditor == null) return;
            var combo = headerEditor.locationNameComboBox;
            int selection = combo.SelectedIndex;
            combo.Items.Clear();
            combo.Items.AddRange(contents.ToArray());
            if (selection >= 0 && selection < combo.Items.Count)
                combo.SelectedIndex = selection;
        }
    }
}
