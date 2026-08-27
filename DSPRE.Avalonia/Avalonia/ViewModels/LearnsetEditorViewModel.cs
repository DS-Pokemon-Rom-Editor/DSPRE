using Avalonia.Controls;
using DSPRE.Editors;
using DSPRE.ROMFiles;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace DSPRE.Avalonia.ViewModels
{
    /// <summary>One learnset entry row: level + move name (for display) + move index (for editing).</summary>
    public class LearnsetEntryRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        private int _level;
        public int Level
        {
            get => _level;
            set { if (_level != value) { _level = value; OnPropertyChanged(); OnPropertyChanged(nameof(Display)); } }
        }

        private int _moveIndex;
        public int MoveIndex
        {
            get => _moveIndex;
            set { if (_moveIndex != value) { _moveIndex = value; OnPropertyChanged(); OnPropertyChanged(nameof(Display)); } }
        }

        // Display string shown in the list (e.g. "Lv.  5: Tackle")
        public string Display { get; private set; }

        public void UpdateDisplay(string[] moveNames)
        {
            string moveName = (_moveIndex >= 0 && _moveIndex < moveNames.Length) ? moveNames[_moveIndex] : $"#{_moveIndex}";
            Display = $"Lv. {_level,3}: {moveName}";
            OnPropertyChanged(nameof(Display));
        }
    }

    public class LearnsetEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        {
            if (System.Collections.Generic.EqualityComparer<T>.Default.Equals(f, v)) return false;
            f = v; OnPropertyChanged(n); return true;
        }
        // ─── Collections ──────────────────────────────────────────────────────────
        public ObservableCollection<string>          MoveNames    { get; } = new();
        public ObservableCollection<LearnsetEntryRow> Entries     { get; } = new();

        // ─── Add-entry inputs ─────────────────────────────────────────────────────
        private int _addLevel = 1;
        public int AddLevel
        {
            get => _addLevel;
            set { Set(ref _addLevel, value); UpdateCanAdd(); }
        }

        private int _addMoveIndex;
        public int AddMoveIndex
        {
            get => _addMoveIndex;
            set { Set(ref _addMoveIndex, value); UpdateCanAdd(); }
        }

        private int _selectedEntryIndex = -1;
        public int SelectedEntryIndex
        {
            get => _selectedEntryIndex;
            set { Set(ref _selectedEntryIndex, value); OnPropertyChanged(nameof(CanEdit)); OnPropertyChanged(nameof(CanMoveUp)); OnPropertyChanged(nameof(CanMoveDown)); }
        }

        public bool CanAdd  { get; private set; }
        public bool CanEdit => _selectedEntryIndex >= 0 && _selectedEntryIndex < Entries.Count;
        public bool CanMoveUp   => CanEdit && _selectedEntryIndex > 0 && Entries[_selectedEntryIndex].Level == Entries[_selectedEntryIndex - 1].Level;
        public bool CanMoveDown => CanEdit && _selectedEntryIndex < Entries.Count - 1 && Entries[_selectedEntryIndex].Level == Entries[_selectedEntryIndex + 1].Level;

        private string _statusText = "";
        public string StatusText { get => _statusText; private set => Set(ref _statusText, value); }

        private int _entryCount;
        public int EntryCount { get => _entryCount; private set => Set(ref _entryCount, value); }

        public bool ExceedsVanillaLimit => EntryCount > LearnsetData.VanillaLimit;

        // ─── Dirty tracking ───────────────────────────────────────────────────────
        private bool _dirty;
        public bool HasUnsavedChanges => _dirty;
        public string UnsavedChangesDescription => $"Learnset (Mon {_currentId})";
        public void SaveChanges() => Save();
        public void DiscardChanges() { _dirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); }

        private int _currentId = -1;
        private LearnsetData _current;
        private string[] _moveNamesArr = System.Array.Empty<string>();

        // ─── Design-time constructor ──────────────────────────────────────────────
        public LearnsetEditorViewModel()
        {
            if (!Design.IsDesignMode) return;

            for (int i = 0; i < 20; i++) MoveNames.Add($"Move {i}");
            _moveNamesArr = System.Linq.Enumerable.Range(0, 20).Select(i => $"Move {i}").ToArray();

            Entries.Add(new LearnsetEntryRow { Level = 1, MoveIndex = 1 });
            Entries.Add(new LearnsetEntryRow { Level = 4, MoveIndex = 2 });
            Entries.Add(new LearnsetEntryRow { Level = 7, MoveIndex = 3 });
            foreach (var e in Entries) e.UpdateDisplay(_moveNamesArr);
            EntryCount = Entries.Count;
        }

        // ─── Runtime constructor ──────────────────────────────────────────────────
        public LearnsetEditorViewModel(string[] moveNames)
        {
            _moveNamesArr = moveNames;
            foreach (var n in moveNames) MoveNames.Add(n);
        }

        public int CurrentId => _currentId;

        /// <summary>Builds the current mon's learnset as CSV (level, move id, move name).</summary>
        public string BuildCsv()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Level,MoveID,MoveName");
            foreach (var e in Entries)
                sb.AppendLine($"{e.Level},{e.MoveIndex},{(e.MoveIndex >= 0 && e.MoveIndex < MoveNames.Count ? MoveNames[e.MoveIndex] : "")}");
            return sb.ToString();
        }

        // ─── Load ─────────────────────────────────────────────────────────────────
        public void LoadMon(int id)
        {
            _currentId = id;
            _current = id >= 0 ? new LearnsetData(id) : null;

            Entries.Clear();
            if (_current != null)
            {
                foreach (var (level, move) in _current.list)
                {
                    var row = new LearnsetEntryRow { Level = level, MoveIndex = move };
                    row.UpdateDisplay(_moveNamesArr);
                    Entries.Add(row);
                }
            }

            EntryCount = Entries.Count;
            OnPropertyChanged(nameof(ExceedsVanillaLimit));
            SelectedEntryIndex = -1;
            UpdateCanAdd();
            _dirty = false;
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        // ─── Add / Delete / Move ──────────────────────────────────────────────────
        public void AddEntry()
        {
            if (_current == null || _addLevel < 1 || _addLevel > 100 || _addMoveIndex <= 0) return;
            var entry = ((byte)_addLevel, (ushort)_addMoveIndex);
            if (_current.list.Contains(entry)) { StatusText = "Entry already exists!"; return; }

            int insertAt = _current.list.FindIndex(x => x.level > entry.Item1 || (x.level == entry.Item1 && x.move > entry.Item2));
            if (insertAt < 0) _current.list.Add(entry);
            else              _current.list.Insert(insertAt, entry);

            RefreshEntries();
            SetDirty();
            StatusText = "";
        }

        public void DeleteEntry()
        {
            if (_current == null || !CanEdit) return;
            _current.list.RemoveAt(_selectedEntryIndex);
            RefreshEntries();
            SetDirty();
        }

        public void MoveEntryUp()
        {
            if (!CanMoveUp) return;
            SwapEntries(_selectedEntryIndex, _selectedEntryIndex - 1);
            SelectedEntryIndex--;
            SetDirty();
        }

        public void MoveEntryDown()
        {
            if (!CanMoveDown) return;
            SwapEntries(_selectedEntryIndex, _selectedEntryIndex + 1);
            SelectedEntryIndex++;
            SetDirty();
        }

        // ─── Save ─────────────────────────────────────────────────────────────────
        public void Save()
        {
            if (_currentId < 0 || _current == null) return;
            _current.SaveToFileDefaultDir(_currentId, showSuccessMessage: false);
            _dirty = false;
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        // ─── Helpers ──────────────────────────────────────────────────────────────
        private void RefreshEntries()
        {
            Entries.Clear();
            foreach (var (level, move) in _current.list)
            {
                var row = new LearnsetEntryRow { Level = level, MoveIndex = move };
                row.UpdateDisplay(_moveNamesArr);
                Entries.Add(row);
            }
            EntryCount = Entries.Count;
            OnPropertyChanged(nameof(ExceedsVanillaLimit));
            UpdateCanAdd();
        }

        private void SwapEntries(int a, int b)
        {
            if (_current == null) return;
            var tmp = _current.list[a];
            _current.list[a] = _current.list[b];
            _current.list[b] = tmp;
            RefreshEntries();
        }

        private void UpdateCanAdd()
        {
            bool now = _addLevel >= 1 && _addLevel <= 100 && _addMoveIndex > 0;
            if (now != CanAdd) { CanAdd = now; OnPropertyChanged(nameof(CanAdd)); }
        }

        private void SetDirty()
        {
            if (!_dirty) { _dirty = true; OnPropertyChanged(nameof(HasUnsavedChanges)); }
        }
    }
}
