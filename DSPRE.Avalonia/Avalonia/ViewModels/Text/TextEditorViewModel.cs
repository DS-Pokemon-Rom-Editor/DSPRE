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

    public class TextSearchResultVM
    {
        public int Archive { get; }
        public int Line { get; }
        public string Display { get; }
        public TextSearchResultVM(int archive, int line, string display)
        { Archive = archive; Line = line; Display = display; }
        public override string ToString() => Display;
    }

    /// <summary>Edits text archives: lines, archives, import and export, search and replace.</summary>
    public partial class TextEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, ISupportsUndo, IDetachable
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (EqualityComparer<T>.Default.Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        private Window _owner;
        private bool _isLoading;
        private TextArchive _current;
        // Non-null when hg-engine builds this archive from its own source, which is then what is edited.
        private HgEngineOwnedFile _managedSource;
        private HgEngineOwnedFile _generatedSource;

        // The field a generated archive is built from, and its lines as last read or saved.
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
                TextArchive.Delete(id);
            }
            foreach ((int id, List<string> lines) in _addedArchives)
            {
                List<string> now = _current != null && _current.ID == id ? _current.Messages : lines;
                new TextArchive(id, new List<string>(now)).SaveToExpandedDir(id, showSuccessMessage: false, sender: this);
                (string binPath, string jsonPath) = TextArchive.GetFilePaths(id);
                TextConverter.JSONToBin(jsonPath, binPath, CharMapManager.GetCharMapPath());
                // Dated newer so the ROM build encodes the JSON again: JSONToBin drops chatot's warnings.
                try { PlainFiles.Touch(jsonPath, DateTime.UtcNow.AddSeconds(2)); }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { AppLogger.Warn($"Text archive {id}: {ex.Message}"); }
            }
            _removedArchives.Clear();
            _addedArchives.Clear();
            _savedArchiveCount = Filesystem.GetTextArchivesCount();
        }
        public string UnsavedChangesDescription => StagedArchive.Join(_current != null ? $"Text archive {_current.ID}" : "Text archive", _dirty,
            StagedArchive.Describe("text archive", "text archives",
                _addedArchives.Keys.Select(id => (id, PendingArchiveWrites.PendingKind.Added))
                    .Concat(_removedArchives.Select(id => (id, PendingArchiveWrites.PendingKind.Removed)))));

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
                    ArchiveNames.Add("Text archive " + ArchiveNames.Count + Marker(HgEngineOwnedFiles.Get(TextArchivePath, ArchiveNames.Count)));
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

        public TextEditorViewModel()
        {
            if (!Design.IsDesignMode) return;
            ArchiveNames.Add("Text archive 0");
            Lines.Add(new TextLineVM(0, "Sample line 0") { Number = "0x0" });
            Lines.Add(new TextLineVM(1, "Sample line 1") { Number = "0x1" });
            StatusText = "Design mode";
        }

        public TextEditorViewModel(bool _)
        {
            _hexNumbering = SettingsManager.Settings.TextEditorPreferHex;
            TextArchive.Saved += OnArchiveSavedElsewhere;
        }

        /// <summary>For a standalone window closing; the Maps workspace's instance lives for the session.</summary>
        public void Detach()
        {
            TextArchive.Saved -= OnArchiveSavedElsewhere;
            _playStops?.Stop();
            _playStops = null;
        }

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
            StatusText = "";
        }

        // False when the user cancelled or the save did not go through.
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

        /// <summary>Set by a host locked to one archive: true means it showed the result itself.</summary>
        public Func<int, int, bool> OpenResultElsewhere { get; set; }

        private void RevealLine(int line)
        {
            if (line < 0 || line >= Lines.Count) return;
            SelectedLineIndex = line;
            LineRevealed?.Invoke(this, EventArgs.Empty);
        }

        public async Task SetupAsync(Window owner)
        {
            _owner = owner;
            _isLoading = true;
            StatusText = "Loading text archives…";

            try
            {
                string unpackedPath = GameDirs[DirNames.TextArchives].unpackedDir;
                string expandedPath = TextConverter.GetExpandedFolderPath();

                TextArchive.EnsureFolders();

                // JSON files are only (re)written when missing or older than the binary.
                TextConverter.FolderToJSON(unpackedPath, expandedPath, CharMapManager.GetCharMapPath());

                ArchiveNames.Clear();
                int count = Filesystem.GetTextArchivesCount();
                _savedArchiveCount = count;
                for (int i = 0; i < count; i++)
                {
                    ArchiveNames.Add("Text archive " + i + Marker(HgEngineOwnedFiles.Get(TextArchivePath, i)));
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
                StatusText = $"Couldn't load text archives: {ex.Message}";
                await DialogHelper.ShowErrorAsync($"Couldn't load text archives:\n{ex.Message}", "Text editor");
            }
            finally
            {
                _isLoading = false;
            }
        }

        private async Task OnArchiveSelectedAsync(int index)
        {
            if (_dirty && _current != null)
            {
                DialogHelper.MsgResult r = await DialogHelper.AskYesNoCancelAsync(
                    "Save the changes to this text archive?",
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
    }
}
