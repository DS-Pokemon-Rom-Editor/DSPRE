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
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Text
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
    /// (dual binary/JSON format): archive selection, per-line editing, add/remove
    /// strings &amp; archives, reorder, import/export, and search &amp; replace.
    /// </summary>
    public class TextEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, ISupportsUndo, IDetachable
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
        // Non-null when hg-engine builds this archive from its own source, which is then what is edited.
        private HgEngineOwnedFile _managedSource;
        private HgEngineOwnedFile _generatedSource;

        // A generated archive whose lines come from one Species.c or Moves.c field, and the lines as last read
        // or saved, so a save writes only the entries that changed.
        private HgEngineGeneratedText.Source _fieldSource;
        private List<string> _fieldBefore;

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
                _archiveLoad = OnArchiveSelectedAsync(value);
            }
        }

        private int _selectedLineIndex = -1;
        public int SelectedLineIndex
        {
            get => _selectedLineIndex;
            set
            {
                if (!Set(ref _selectedLineIndex, value)) return;
                OnPropertyChanged(nameof(CanMoveUp));
                OnPropertyChanged(nameof(CanMoveDown));
                RefreshPreview();
            }
        }

        // ── seeing the line the way the game shows it ────────────────────────
        private readonly List<FieldMessageFrame> _previewFrames = new List<FieldMessageFrame>();
        private int _previewFrame;

        /// <summary>Measures a run of letters, pointed at the ROM's own font by the view.</summary>
        public Func<string, int> MeasureText { get; set; } = t => (t ?? "").Length * 6;

        private bool _showPreview = true;

        /// <summary>Whether the message box preview is on show under the list.</summary>
        public bool ShowPreview
        {
            get => _showPreview;
            set { if (Set(ref _showPreview, value)) RefreshPreview(); }
        }

        private FieldMessageFrame CurrentPreview =>
            _previewFrame >= 0 && _previewFrame < _previewFrames.Count ? _previewFrames[_previewFrame] : null;

        /// <summary>What the box would be showing at this point in the message.</summary>
        public string PreviewText => CurrentPreview?.Text ?? "";

        public bool HasPreview => CurrentPreview != null;

        /// <summary>Whether the box is waiting on the player here rather than finishing.</summary>
        public bool PreviewHasMore => CurrentPreview != null && CurrentPreview.Wait != MessageWait.None;

        /// <summary>Which of the message's stops this is.</summary>
        public string PreviewStepText => _previewFrames.Count > 1
            ? $"Stop {_previewFrame + 1} of {_previewFrames.Count}" : "";

        /// <summary>What the player pressing A or B would do next.</summary>
        public string PreviewWaitText
        {
            get
            {
                FieldMessageFrame f = CurrentPreview;
                if (f == null) return "";
                switch (f.Wait)
                {
                    case MessageWait.Clear: return "Waits, then clears the box and starts again";
                    case MessageWait.Scroll: return "Waits, then scrolls up one line and carries on";
                    case MessageWait.Simple: return "Waits, then carries on where it left off";
                    default: return "The end of the message";
                }
            }
        }

        /// <summary>Says so when the line will not fit the box the way it is written.</summary>
        public string PreviewWarning
        {
            get
            {
                FieldMessageFrame f = CurrentPreview;
                if (f == null) return null;
                if (f.TooWide && f.TooManyLines) return "This runs past the right edge and past the bottom of the box.";
                if (f.TooWide) return "A line here runs past the right edge of the box.";
                if (f.TooManyLines) return "There are more lines here than the box can show.";
                return null;
            }
        }

        public bool HasPreviewWarning => PreviewWarning != null;

        /// <summary>The twenty borders the player can pick between in the games' own Options.</summary>
        public ObservableCollection<string> BorderNames { get; } = new ObservableCollection<string>();

        private int _borderIndex;

        /// <summary>Which border to draw the box with.</summary>
        public int BorderIndex
        {
            get => _borderIndex;
            set { if (Set(ref _borderIndex, value)) BorderChanged?.Invoke(this, EventArgs.Empty); }
        }

        /// <summary>Raised when a different border is picked, so the view can read it out of the ROM.</summary>
        public event EventHandler BorderChanged;

        /// <summary>Says when the letters are a stand-in rather than the game's own.</summary>
        public string PreviewFontNote { get; set; }
        public bool HasPreviewFontNote => !string.IsNullOrEmpty(PreviewFontNote);

        /// <summary>Steps on to the message's next stop, wrapping round at the end.</summary>
        public void NextPreviewStep()
        {
            if (_previewFrames.Count == 0) return;
            _previewFrame = (_previewFrame + 1) % _previewFrames.Count;
            RaisePreviewChanged();
        }

        /// <summary>Works the selected line out again, after picking a different one or editing it.</summary>
        public void RefreshPreview()
        {
            _previewFrames.Clear();
            _previewFrame = 0;

            if (_showPreview && _selectedLineIndex >= 0 && _selectedLineIndex < Lines.Count)
            {
                string text = Lines[_selectedLineIndex]?.Text;
                if (!string.IsNullOrEmpty(text))
                    _previewFrames.AddRange(FieldMessageScript.Frames(text, MeasureText));
            }
            RaisePreviewChanged();
        }

        private void RaisePreviewChanged()
        {
            OnPropertyChanged(nameof(PreviewText));
            OnPropertyChanged(nameof(HasPreview));
            OnPropertyChanged(nameof(PreviewHasMore));
            OnPropertyChanged(nameof(PreviewStepText));
            OnPropertyChanged(nameof(PreviewWaitText));
            OnPropertyChanged(nameof(PreviewWarning));
            OnPropertyChanged(nameof(HasPreviewWarning));
            OnPropertyChanged(nameof(PreviewFontNote));
            OnPropertyChanged(nameof(HasPreviewFontNote));
        }

        public bool CanMoveUp => _selectedLineIndex > 0;
        public bool CanMoveDown => _selectedLineIndex >= 0 && _selectedLineIndex < Lines.Count - 1;

        private bool _hexNumbering = true;
        public bool HexNumbering
        {
            get => _hexNumbering;
            set { if (Set(ref _hexNumbering, value)) { RenumberLines(); SettingsManager.Settings.TextEditorPreferHex = value; } }
        }

        // ── Search / replace inputs ──────────────────────────────────────────────
        private string _searchText = "";
        public string SearchText { get => _searchText; set => Set(ref _searchText, value); }

        private string _replaceText = "";
        public string ReplaceText { get => _replaceText; set => Set(ref _replaceText, value); }

        private bool _searchAllArchives = true;
        public bool SearchAllArchives { get => _searchAllArchives; set => Set(ref _searchAllArchives, value); }

        private bool _caseSensitive;
        public bool CaseSensitive { get => _caseSensitive; set => Set(ref _caseSensitive, value); }

        private string _statusText = "Not loaded";
        public string StatusText { get => _statusText; set => Set(ref _statusText, value); }

        // ── Dirty tracking ───────────────────────────────────────────────────────
        private bool _dirty;
        public bool HasUnsavedChanges => _dirty || _addedArchives.Count > 0 || _removedArchives.Count > 0;

        // Archives added (their lines) and removed since the last save; Save writes them, Discard drops them.
        private readonly SortedDictionary<int, List<string>> _addedArchives = new();
        private readonly SortedSet<int> _removedArchives = new();
        private int _savedArchiveCount;

        private int ArchiveCount => _savedArchiveCount - _removedArchives.Count + _addedArchives.Count;

        private void WriteStagedArchives()
        {
            foreach (int id in _removedArchives.Reverse())
            {
                (string binPath, string jsonPath) = TextArchive.GetFilePaths(id);
                if (File.Exists(jsonPath)) File.Delete(jsonPath);
                ArchiveMembers.Delete(binPath);
            }
            foreach ((int id, List<string> lines) in _addedArchives)
            {
                List<string> now = _current != null && _current.ID == id ? _current.Messages : lines;
                new TextArchive(id, new List<string>(now)).SaveToExpandedDir(id, showSuccessMessage: false, sender: this);
                (string binPath, string jsonPath) = TextArchive.GetFilePaths(id);
                TextConverter.JSONToBin(jsonPath, binPath, CharMapManager.GetCharMapPath());
                // JSONToBin drops chatot's warnings; a JSON newer than its .bin is encoded again, with them, by the ROM build.
                try { File.SetLastWriteTimeUtc(jsonPath, DateTime.UtcNow.AddSeconds(2)); }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { AppLogger.Warn($"Text archive {id}: {ex.Message}"); }
            }
            _removedArchives.Clear();
            _addedArchives.Clear();
            _savedArchiveCount = Filesystem.GetTextArchivesCount();
        }
        public string UnsavedChangesDescription => DSPRE.Avalonia.Data.StagedArchive.Join(_current != null ? $"Text Archive {_current.ID}" : "Text Archive", _dirty,
            DSPRE.Avalonia.Data.StagedArchive.Describe("text archive", "text archives",
                _addedArchives.Keys.Select(id => (id, DSPRE.Avalonia.Data.PendingArchiveWrites.PendingKind.Added))
                    .Concat(_removedArchives.Select(id => (id, DSPRE.Avalonia.Data.PendingArchiveWrites.PendingKind.Removed)))));

        public void SaveChanges() => Save();
        // The edited lines live in the loaded archive, so dropping them means reading it again.
        public void DiscardChanges()
        {
            bool reload = (_dirty || _addedArchives.Count > 0 || _removedArchives.Count > 0) && _current != null;
            bool structural = _addedArchives.Count > 0 || _removedArchives.Count > 0;
            _addedArchives.Clear();
            _removedArchives.Clear();
            SetClean();
            if (structural)
            {
                _isLoading = true;
                while (ArchiveNames.Count > _savedArchiveCount) ArchiveNames.RemoveAt(ArchiveNames.Count - 1);
                while (ArchiveNames.Count < _savedArchiveCount)
                    ArchiveNames.Add("Text Archive " + ArchiveNames.Count + Marker(HgEngineOwnedFiles.Get(TextArchivePath, ArchiveNames.Count)));
                if (_selectedArchiveIndex >= ArchiveNames.Count) SelectedArchiveIndex = ArchiveNames.Count - 1;
                _isLoading = false;
                OnPropertyChanged(nameof(HasUnsavedChanges));
            }
            if (reload && _selectedArchiveIndex >= 0) LoadArchive(_selectedArchiveIndex);
        }

        private void SetDirty()
        {
            _undo?.Record();
            if (_dirty) return;
            _dirty = true;
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }
        private void SetClean()
        {
            _undo?.MarkSaved();
            if (!_dirty) return;
            _dirty = false;
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

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
            _hexNumbering = SettingsManager.Settings.TextEditorPreferHex;
            TextArchive.Saved += OnArchiveSavedElsewhere;
        }

        /// <summary>For a standalone window closing; the Maps workspace's instance lives for the session.</summary>
        public void Detach() => TextArchive.Saved -= OnArchiveSavedElsewhere;

        // Saving the old list over another editor's write would undo it, so the open archive follows it.
        private void OnArchiveSavedElsewhere(object sender, int id)
        {
            if (ReferenceEquals(sender, this)) return;
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() => _ = ReloadSavedElsewhereAsync(id));
        }

        private bool _askingAboutSavedElsewhere;

        private async Task ReloadSavedElsewhereAsync(int id)
        {
            // An hg-engine archive is edited from its own source, not the JSON that was written.
            if (_current == null || _current.ID != id || _managedSource != null || _askingAboutSavedElsewhere) return;
            if (_dirty)
            {
                _askingAboutSavedElsewhere = true;
                try
                {
                    if (!await RecordSwitchGuard.TakeSavedVersionAsync(UnsavedChangesDescription)) return;
                }
                finally { _askingAboutSavedElsewhere = false; }
                if (_current == null || _current.ID != id) return;
            }
            LoadArchive(id);
            StatusText = $"Text Archive {id} was saved in another editor and reloaded.";
        }

        /// <summary>Saves or discards unsaved edits before an action that reloads the archive.
        /// False when the user cancelled or the save did not go through.</summary>
        private Task<bool> SaveOrDiscardBeforeReloadAsync(string action)
            => RecordSwitchGuard.ConfirmLeaveAsync(this, _owner, "text archive",
                $"{action} reloads it. Save them first?");

        public int InitialIndex { get; set; }

        /// <summary>A line of <see cref="InitialIndex"/> to show once it has loaded, or -1.</summary>
        public int InitialLine { get; set; } = -1;

        // The archive load the last selection started; a go-to waits for it before picking a line.
        private Task _archiveLoad = Task.CompletedTask;

        /// <summary>Raised when a line is picked for the user, so the view scrolls it into sight.</summary>
        public event EventHandler LineRevealed;

        /// <summary>
        /// Set by a host that keeps this editor on one archive (the grouped view's Text tab): given a search result's
        /// archive and line, it shows them somewhere else and returns true, or returns false to show them here.
        /// </summary>
        public Func<int, int, bool> OpenResultElsewhere { get; set; }

        private void RevealLine(int line)
        {
            if (line < 0 || line >= Lines.Count) return;
            SelectedLineIndex = line;
            LineRevealed?.Invoke(this, EventArgs.Empty);
        }

        // ── Setup ────────────────────────────────────────────────────────────────
        public async Task SetupAsync(Window owner)
        {
            _owner = owner;
            _isLoading = true;
            StatusText = "Loading text archives…";

            try
            {
                string unpackedPath = GameDirs[DirNames.TextArchives].unpackedDir;
                string expandedPath = TextConverter.GetExpandedFolderPath();

                if (!Directory.Exists(expandedPath))
                {
                    Directory.CreateDirectory(expandedPath);
                    DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.TextArchives });
                }
                if (!Directory.Exists(unpackedPath))
                    Directory.CreateDirectory(unpackedPath);

                // JSON files are only (re)written when missing or older than the binary.
                TextConverter.FolderToJSON(unpackedPath, expandedPath, CharMapManager.GetCharMapPath());

                ArchiveNames.Clear();
                int count = Filesystem.GetTextArchivesCount();
                _savedArchiveCount = count;
                for (int i = 0; i < count; i++)
                {
                    ArchiveNames.Add("Text Archive " + i + Marker(HgEngineOwnedFiles.Get(TextArchivePath, i)));
                }

                _isLoading = false;
                StatusText = $"Loaded {count} text archives.";

                if (count > 0)
                {
                    SelectedArchiveIndex = Math.Min(Math.Max(0, InitialIndex), count - 1);
                    await _archiveLoad;
                    if (InitialLine >= 0 && SelectedArchiveIndex == InitialIndex) RevealLine(InitialLine);
                }
            }
            catch (Exception ex)
            {
                StatusText = $"Error loading text archives: {ex.Message}";
                await DialogHelper.ShowErrorAsync($"Failed to load text archives:\n{ex.Message}", "Text editor error");
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
                DialogHelper.MsgResult r = await DialogHelper.AskYesNoCancelAsync(
                    "There are unsaved changes to the currently loaded Text Archive.\nDo you want to save them?",
                    "Unsaved changes");

                if (r == DialogHelper.MsgResult.Yes) await SaveAsync();
                // A cancel, or a save that didn't go through, stays on the archive that holds the edits.
                if (r == DialogHelper.MsgResult.Cancel || (r == DialogHelper.MsgResult.Yes && _dirty))
                {
                    _isLoading = true;
                    SelectedArchiveIndex = _current.ID;
                    _isLoading = false;
                    return;
                }
            }

            LoadArchive(index);
        }

        // ── Undo / redo ──────────────────────────────────────────────────────────
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

        private byte[] MessagesState()
        {
            using MemoryStream ms = new MemoryStream();
            using (BinaryWriter w = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
                foreach (string m in _current.Messages) w.Write(m ?? "");
            return ms.ToArray();
        }

        private void RestoreMessages(byte[] state)
        {
            List<string> messages = new List<string>();
            using (BinaryReader r = new BinaryReader(new MemoryStream(state), System.Text.Encoding.UTF8))
                while (r.BaseStream.Position < r.BaseStream.Length) messages.Add(r.ReadString());
            _current.Messages.Clear();
            _current.Messages.AddRange(messages);
            ShowLines();
            SetDirty();
            RefreshPreview();
        }

        private void ShowLines()
        {
            bool wasLoading = _isLoading;
            _isLoading = true;
            try
            {
                foreach (TextLineVM l in Lines) l.PropertyChanged -= OnLineChanged;
                Lines.Clear();
                for (int i = 0; i < _current.Messages.Count; i++)
                {
                    TextLineVM line = new TextLineVM(i, _current.Messages[i]);
                    line.PropertyChanged += OnLineChanged;
                    Lines.Add(line);
                }
                RenumberLines();
            }
            finally { _isLoading = wasLoading; }
        }

        private void LoadArchive(int id)
        {
            _isLoading = true;
            try
            {
                _current = _addedArchives.TryGetValue(id, out List<string> added)
                    ? new TextArchive(id, new List<string>(added))
                    : new TextArchive(id);
                HgEngineOwnedFile owned = HgEngineOwnedFiles.Get(TextArchivePath, id);
                _generatedSource = owned?.Ownership == HgEngineOwnership.Generated ? owned : null;
                _managedSource = owned?.Ownership == HgEngineOwnership.EditableSource ? owned : null;
                if (_managedSource != null)
                {
                    if (HgEngineOwnedFiles.TryReadLines(_managedSource, out List<string> sourceLines, out string readError))
                    {
                        _current.Messages.Clear();
                        _current.Messages.AddRange(sourceLines);
                    }
                    else
                    {
                        _managedSource = null;
                        StatusText = readError;
                    }
                }
                _fieldSource = _generatedSource != null ? HgEngineGeneratedText.For(id) : null;
                _fieldBefore = null;
                if (_fieldSource?.IsMain == true)
                {
                    if (HgEngineGeneratedText.TryReadLines(_fieldSource, _current.Messages.Count, out string[] fieldLines, out string fieldError))
                    {
                        for (int i = 0; i < fieldLines.Length; i++)
                            if (fieldLines[i] != null) _current.Messages[i] = fieldLines[i];
                        _fieldBefore = new List<string>(_current.Messages);
                    }
                    else
                    {
                        _fieldSource = null;
                        StatusText = fieldError;
                    }
                }
                OnPropertyChanged(nameof(IsManagedByHgEngine));
                OnPropertyChanged(nameof(IsGeneratedByHgEngine));
                OnPropertyChanged(nameof(HasHgEngineNote));
                OnPropertyChanged(nameof(ManagedNote));

                ShowLines();
                SetClean();
                _undo = new ByteStateUndo(MessagesState, RestoreMessages,
                    () => { OnPropertyChanged(nameof(CanUndo)); OnPropertyChanged(nameof(CanRedo)); });
                OnPropertyChanged(nameof(CanUndo));
                OnPropertyChanged(nameof(CanRedo));
                RefreshPreview();
                StatusText = _managedSource != null
                    ? $"Loaded {_managedSource.RelPath} ({_current.Messages.Count} lines) from the hg-engine checkout."
                    : $"Loaded Text Archive {id} ({_current.Messages.Count} lines).";
            }
            finally
            {
                _isLoading = false;
            }
        }

        private void OnLineChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_isLoading || e.PropertyName != nameof(TextLineVM.Text)) return;
            if (sender is TextLineVM line && _current != null && line.Index < _current.Messages.Count)
            {
                _current.Messages[line.Index] = line.Text ?? "";
                SetDirty();
                if (line.Index == _selectedLineIndex) RefreshPreview();
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
            // A field-backed archive has exactly one line per entry.
            if (_current == null || _fieldSource != null) return;
            _current.Messages.Add("");
            TextLineVM line = new TextLineVM(Lines.Count, "");
            line.PropertyChanged += OnLineChanged;
            Lines.Add(line);
            RenumberLines();
            SetDirty();
        }

        public void RemoveString()
        {
            if (_current == null || _current.Messages.Count == 0 || _fieldSource != null) return;
            _current.Messages.RemoveAt(_current.Messages.Count - 1);
            TextLineVM last = Lines[Lines.Count - 1];
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
            int newId = ArchiveCount;
            _removedArchives.Remove(newId);
            _addedArchives[newId] = new List<string> { "Your text here." };
            ArchiveNames.Add("Text Archive " + newId);
            SelectedArchiveIndex = newId;
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        public async Task RemoveArchiveAsync()
        {
            if (ArchiveNames.Count == 0) return;
            if (!await DialogHelper.AskYesNoAsync("Delete the last text archive? It goes when you save.", "Delete text archive?"))
                return;

            int lastIndex = ArchiveNames.Count - 1;
            if (!_addedArchives.Remove(lastIndex)) _removedArchives.Add(lastIndex);

            if (SelectedArchiveIndex == lastIndex)
            {
                SetClean();
                _isLoading = true;
                SelectedArchiveIndex = lastIndex - 1;
                _isLoading = false;
                if (lastIndex - 1 >= 0) LoadArchive(lastIndex - 1);
            }
            ArchiveNames.RemoveAt(lastIndex);
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        // ── Save ─────────────────────────────────────────────────────────────────
        /// <summary>
        /// The file the shown archive is edited from: hg-engine's own source, the project's text file, or the packed
        /// archive when neither exists. Null when no archive is shown.
        /// </summary>
        private string ArchiveSourceFile()
        {
            if (_selectedArchiveIndex < 0) return null;
            string owned = (_managedSource ?? _generatedSource)?.FullPath;
            if (!string.IsNullOrEmpty(owned) && File.Exists(owned)) return owned;
            (string binPath, string jsonPath) = TextArchive.GetFilePaths(_selectedArchiveIndex);
            return File.Exists(jsonPath) ? jsonPath : binPath;
        }

        public void ShowArchiveFile()
        {
            if (ArchiveSourceFile() is not string path) return;
            SystemShell.RevealInFileManager(path);
            if (HasUnsavedChanges) StatusText = "The file holds the archive as last saved.";
        }

        public void OpenArchiveFile()
        {
            if (ArchiveSourceFile() is not string path) return;
            SystemShell.OpenWithDefaultApp(path);
            StatusText = HasUnsavedChanges ? "Opened the archive as last saved." : "Opened the archive.";
        }

        public void Save() => _ = SaveAsync();

        async Task<bool> IEditorWithUnsavedChanges.SaveChangesAsync()
        {
            await SaveAsync();
            return !HasUnsavedChanges;
        }

        /// <summary>Completes once the archive is written, hg-engine source writes included.</summary>
        public async Task SaveAsync()
        {
            if (_current == null) return;
            if (_managedSource != null) { await SaveToHgEngineSourceAsync(); return; }
            if (_fieldSource?.IsMain == true) { await SaveToGeneratedFieldAsync(); return; }
            if (_generatedSource != null) { await RefusedForManagedArchiveAsync("Saving"); return; }

            if (!_addedArchives.ContainsKey(_current.ID))
                _current.SaveToExpandedDir(_current.ID, showSuccessMessage: false, sender: this);
            WriteStagedArchives();
            SetClean();
            OnPropertyChanged(nameof(HasUnsavedChanges));
            SaveNotice.Saved(UnsavedChangesDescription);
            StatusText = $"Saved Text Archive {_current.ID}.";
            // Names (Pokémon/item/move/…) come from text archives; tell open editors to refresh their combos.
            AppEvents.RaiseNamesChanged();
        }

        // Import and export both go through the ROM's copy, which is not what a managed archive shows.
        private async Task<bool> RefusedForManagedArchiveAsync(string what)
        {
            if (_generatedSource != null)
            {
                await DialogHelper.ShowInfoAsync(
                    $"{what} works on the ROM's copy, and hg-engine regenerates this archive from its own " +
                    "data every build. Edit the data it comes from instead.",
                    "Generated by hg-engine");
                return true;
            }
            if (_managedSource == null) return false;
            await DialogHelper.ShowInfoAsync(
                $"{what} works on the ROM's copy, and hg-engine builds this archive from " +
                $"{_managedSource.RelPath}. Edit that file instead.",
                "Managed by hg-engine");
            return true;
        }

        public bool IsManagedByHgEngine => _managedSource != null;

        public bool HasHgEngineNote => _managedSource != null || _generatedSource != null;

        /// <summary>Generated at build time from another domain, so nothing here reaches the game.</summary>
        public bool IsGeneratedByHgEngine => _generatedSource != null;

        public string ManagedNote =>
            _managedSource != null
                ? $"hg-engine builds this archive from {_managedSource.RelPath}. Editing it here edits that file."
            : _fieldSource?.IsMain == true
                ? $"hg-engine builds this archive from {_fieldSource.FieldLabel} in {_fieldSource.RelPath}, one line per entry. Editing a line here edits that field."
            : _fieldSource != null
                ? $"hg-engine copies this archive from archive {_fieldSource.MainArchive} at build time. Edit that archive instead."
            : _generatedSource != null
                ? "hg-engine generates this archive from its own data at build time, so changes made here " +
                  "are overwritten. Edit the data it comes from instead."
                : "";

        private static string TextArchivePath => HgEngineOwnedFiles.ArchiveOf(DirNames.TextArchives);

        private static string Marker(HgEngineOwnedFile owned) => owned?.Ownership switch
        {
            HgEngineOwnership.EditableSource => "   [hg-engine]",
            HgEngineOwnership.Generated => "   [hg-engine, generated]",
            _ => "",
        };

        /// <summary>
        /// Writes the archive back to the checkout instead of the ROM. hg-engine's own validator runs
        /// afterwards because a bad tag fails its build, and the reminder that a compile is needed can be
        /// turned off per project.
        /// </summary>
        private async Task SaveToHgEngineSourceAsync()
        {
            HgEngineOwnedFile source = _managedSource;
            if (!HgEngineOwnedFiles.TryWriteLines(source, _current.Messages, out string error))
            {
                StatusText = error;
                await DialogHelper.ShowErrorAsync(error, "Text editor error");
                return;
            }

            SetClean();
            SaveNotice.Saved(UnsavedChangesDescription);
            StatusText = $"Saved {source.RelPath}.";
            AppEvents.RaiseNamesChanged();

            if (HgEngineBuild.TryValidateTextArchive(source.RelPath, out string problems) && problems != null)
            {
                StatusText = $"Saved {source.RelPath}, with problems its build will reject.";
                await DialogHelper.ShowErrorAsync(problems, "hg-engine text validator");
                return;
            }

            if (HgEngineProject.SuppressManagedFileSaveNotice) return;

            bool never = await DialogHelper.ShowNoticeWithOptOutAsync(
                $"This file is managed by hg-engine. {source.RelPath} has been saved, but you will need to " +
                "compile, not just save the ROM, to see the change in game.",
                "Managed by hg-engine");
            if (never) HgEngineProject.SuppressManagedFileSaveNoticeForProject();
        }

        private async Task SaveToGeneratedFieldAsync()
        {
            HgEngineGeneratedText.Source source = _fieldSource;
            List<string> lines = new List<string>(_current.Messages);
            List<string> before = _fieldBefore ?? lines;
            (bool saved, string error) = await HgEngineSave.RunAsync(() =>
                HgEngineGeneratedText.TryWriteLines(source, lines, before, out string writeError) ? null : writeError);
            if (!saved)
            {
                if (error != null)
                {
                    StatusText = error;
                    await DialogHelper.ShowErrorAsync(error, "Text editor error");
                }
                return;
            }

            _fieldBefore = lines;
            SetClean();
            SaveNotice.Saved(UnsavedChangesDescription);
            StatusText = $"Saved {source.FieldLabel} in {source.RelPath}.";
            AppEvents.RaiseNamesChanged();
        }

        // ── Import / export ──────────────────────────────────────────────────────
        public async Task ImportAsync()
        {
            if (_current == null) return;
            if (await RefusedForManagedArchiveAsync("Importing")) return;
            FilePickerFileType[] filters = new[]
            {
                new FilePickerFileType("JSON Text Archive") { Patterns = new[] { "*.json" } },
                new FilePickerFileType("Binary Text Archive") { Patterns = new[] { "*.msg", "*.bin" } },
            };
            string path = await DialogHelper.OpenFileAsync(_owner, "Import text archive", filters);
            if (path == null) return;
            if (_current == null) return;

            try
            {
                // The lines replace the open archive's, as an edit Save keeps and Undo takes back.
                List<string> lines = TextArchive.ReadMessagesFrom(path);
                if (lines.SequenceEqual(_current.Messages))
                {
                    StatusText = $"That file matches Text Archive {_current.ID} already.";
                    return;
                }
                _current.Messages.Clear();
                _current.Messages.AddRange(lines);
                ShowLines();
                RefreshPreview();
                SetDirty();
                SaveNotice.Imported($"{lines.Count} lines into Text Archive {_current.ID}", path);
            }
            catch (Exception ex)
            {
                await DialogHelper.ShowErrorAsync($"Import failed:\n{ex.Message}", "Text editor error");
            }
        }

        public async Task ExportAsync()
        {
            if (_current == null) return;
            if (await RefusedForManagedArchiveAsync("Exporting")) return;
            int id = _current.ID;
            FilePickerFileType[] filters = new[]
            {
                new FilePickerFileType("JSON Text Archive") { Patterns = new[] { "*.json" } },
                new FilePickerFileType("Binary Text Archive") { Patterns = new[] { "*.msg" } },
            };
            string path = await DialogHelper.SaveFileAsync(_owner, "Export text archive", filters, "Text Archive " + id);
            if (path == null) return;

            string ext = Path.GetExtension(path).ToLowerInvariant();
            try
            {
                // The archive as the editor shows it, unsaved edits and added archives included.
                byte[] json = _current.ToExpandedJsonBytes(id);
                if (ext == ".json")
                {
                    PlainFiles.WriteBytes(path, json);
                }
                else
                {
                    string temp = Path.Combine(Path.GetTempPath(), "dspre_text_" + Guid.NewGuid().ToString("N") + ".json");
                    try
                    {
                        PlainFiles.WriteBytes(temp, json);
                        TextConverter.JSONToBin(temp, path, CharMapManager.GetCharMapPath());
                    }
                    finally { if (File.Exists(temp)) File.Delete(temp); }
                }
                SaveNotice.Exported(path);
            }
            catch (Exception ex)
            {
                await DialogHelper.ShowErrorAsync($"Export failed:\n{ex.Message}", "Text editor error");
            }
        }

        // ── Search / replace ─────────────────────────────────────────────────────
        public void Search()
        {
            if (string.IsNullOrEmpty(SearchText)) return;

            int first, last;
            if (SearchAllArchives) { first = 0; last = Filesystem.GetTextArchivesCount(); }
            else { first = SelectedArchiveIndex; last = first + 1; }
            if (first < 0) return;

            SearchResults.Clear();
            Func<string, bool> match = CaseSensitive
                ? (x => x.Contains(SearchText))
                : (x => x.IndexOf(SearchText, StringComparison.InvariantCultureIgnoreCase) >= 0);

            for (int i = first; i < last; i++)
            {
                TextArchive file = new TextArchive(i);
                for (int j = 0; j < file.Messages.Count; j++)
                {
                    if (match(file.Messages[j]))
                    {
                        string preview = file.Messages[j].Substring(0, Math.Min(file.Messages[j].Length, 40));
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
                specify = $" in every Text Bank of the game ({first} to {last - 1})";
            }
            else
            {
                if (_current == null) return;
                if (await RefusedForManagedArchiveAsync("Replacing")) return;
                first = SelectedArchiveIndex; last = first + 1;
                specify = $" in the current text bank only ({first})";
            }
            if (first < 0) return;

            string message = $"You are about to replace every occurrence of \"{SearchText}\" with \"{ReplaceText}\"{specify}." +
                             "\nThe operation can't be interrupted nor undone.\n\nProceed?";
            if (!await DialogHelper.AskYesNoAsync(message, "Replace text?")) return;
            if (!await SaveOrDiscardBeforeReloadAsync("Replacing")) return;

            SearchResults.Clear();

            // hg-engine builds these from its own files, which a replace here would not reach.
            int skipped = 0;
            bool OwnedByHgEngine(int id)
            {
                if (HgEngineOwnedFiles.Get(TextArchivePath, id) == null) return false;
                skipped++;
                return true;
            }

            List<int> edited;
            try
            {
                edited = DSUtils.ReplaceTextInArchives(
                    new[] { (SearchText, ReplaceText ?? "", CaseSensitive) }, first, last, OwnedByHgEngine, this);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                // Archives before the failure are already rewritten, so the shown one is read again either way.
                if (SelectedArchiveIndex >= 0) LoadArchive(SelectedArchiveIndex);
                AppEvents.RaiseNamesChanged();
                bool one = last - first == 1;
                StatusText = one ? "Text bank not changed." : "Replace stopped partway.";
                await DialogHelper.ShowErrorAsync(one
                    ? $"Text bank {first} wasn't changed:\n{ex.Message}"
                    : $"Replace stopped partway; text banks before the failure were already changed:\n{ex.Message}", "Text editor error");
                return;
            }
            foreach (int cur in edited)
                SearchResults.Add(new TextSearchResultVM(cur, 0, $"Text archive ({cur}) - Successfully edited"));

            // Reload the currently displayed archive so edits are visible.
            if (SelectedArchiveIndex >= 0) LoadArchive(SelectedArchiveIndex);
            if (edited.Count > 0) AppEvents.RaiseNamesChanged();
            StatusText = $"Replace complete: {edited.Count} archive(s) edited"
                         + (skipped > 0 ? $", {skipped} hg-engine archive(s) skipped." : ".");
        }

        /// <summary>Shows a search result's line, after its archive has loaded when it is another one.</summary>
        public async Task GoToResultAsync(TextSearchResultVM result)
        {
            if (result == null) return;
            if (SelectedArchiveIndex != result.Archive)
            {
                if (OpenResultElsewhere?.Invoke(result.Archive, result.Line) == true) return;
                SelectedArchiveIndex = result.Archive;
                await _archiveLoad;
                // Cancelled at the unsaved changes prompt.
                if (SelectedArchiveIndex != result.Archive) return;
            }
            RevealLine(result.Line);
        }
    }
}
