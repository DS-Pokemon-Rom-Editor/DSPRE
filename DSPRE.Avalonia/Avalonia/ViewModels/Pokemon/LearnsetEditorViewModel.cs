using Avalonia.Controls;
using DSPRE.Editors;
using DSPRE.ROMFiles;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace DSPRE.Avalonia.ViewModels.Pokemon
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

    public class LearnsetEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, ISupportsUndo
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
            set
            {
                if (!Set(ref _selectedEntryIndex, value)) return;
                OnPropertyChanged(nameof(CanEdit)); OnPropertyChanged(nameof(CanMoveUp)); OnPropertyChanged(nameof(CanMoveDown));
                // The picked row fills the boxes below, so Replace changes it in place.
                if (CanEdit) { AddLevel = Entries[value].Level; AddMoveIndex = Entries[value].MoveIndex; }
            }
        }

        public bool CanAdd  { get; private set; }
        public bool CanEdit => _selectedEntryIndex >= 0 && _selectedEntryIndex < Entries.Count;
        public bool CanMoveUp   => CanEdit && _selectedEntryIndex > 0 && Entries[_selectedEntryIndex].Level == Entries[_selectedEntryIndex - 1].Level;
        public bool CanMoveDown => CanEdit && _selectedEntryIndex < Entries.Count - 1 && Entries[_selectedEntryIndex].Level == Entries[_selectedEntryIndex + 1].Level;

        private string _statusText = "";
        public string StatusText { get => _statusText; private set => Set(ref _statusText, value); }

        private int _entryCount;
        public int EntryCount { get => _entryCount; private set => Set(ref _entryCount, value); }

        public bool ExceedsVanillaLimit => !DSPRE.HgEngine.HgEngineProject.IsActive && EntryCount > LearnsetData.VanillaLimit;

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
            foreach (LearnsetEntryRow e in Entries) e.UpdateDisplay(_moveNamesArr);
            EntryCount = Entries.Count;
        }

        // ─── Runtime constructor ──────────────────────────────────────────────────
        public LearnsetEditorViewModel(string[] moveNames)
        {
            _moveNamesArr = moveNames;
            foreach (string n in moveNames) MoveNames.Add(n);
        }

        public int CurrentId => _currentId;

        /// <summary>Builds the current mon's learnset as CSV (level, move id, move name).</summary>
        public string BuildCsv()
        {
            StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("Level,MoveID,MoveName");
            foreach (LearnsetEntryRow e in Entries)
                sb.AppendLine($"{e.Level},{e.MoveIndex},{(e.MoveIndex >= 0 && e.MoveIndex < MoveNames.Count ? MoveNames[e.MoveIndex] : "")}");
            return sb.ToString();
        }

        // ─── Load ─────────────────────────────────────────────────────────────────
        // Set when learnsets.json couldn't be read for this species; saving would write the shown list over it.
        private string _loadError;

        public void LoadMon(int id)
        {
            _currentId = id;
            _loadError = null;
            _current = id >= 0 ? LoadLearnset(id, out _loadError) : null;
            StatusText = _loadError ?? "";

            Entries.Clear();
            if (_current != null)
            {
                foreach ((byte level, ushort move) in _current.list)
                {
                    LearnsetEntryRow row = new LearnsetEntryRow { Level = level, MoveIndex = move };
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
            _undo = _current == null ? null : new ByteStateUndo(LearnsetState, ApplyLearnsetState, RaiseUndoState);
            RaiseUndoState();
        }

        // ─── Undo / redo ──────────────────────────────────────────────────────────
        private ByteStateUndo _undo;
        public bool CanUndo => _undo?.CanUndo == true;
        public bool CanRedo => _undo?.CanRedo == true;
        public void Undo() { _undo?.Undo(); SyncDirtyWithUndo(); }
        public void Redo() { _undo?.Redo(); SyncDirtyWithUndo(); }
        private void RaiseUndoState() { OnPropertyChanged(nameof(CanUndo)); OnPropertyChanged(nameof(CanRedo)); }
        private void SyncDirtyWithUndo()
        {
            if (_undo == null || _undo.IsDirty == _dirty) return;
            _dirty = _undo.IsDirty; OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        private byte[] LearnsetState()
        {
            byte[] bytes = new byte[_current.list.Count * 3];
            int i = 0;
            foreach ((byte level, ushort move) in _current.list) { bytes[i++] = level; bytes[i++] = (byte)move; bytes[i++] = (byte)(move >> 8); }
            return bytes;
        }

        private void ApplyLearnsetState(byte[] state)
        {
            _current.list.Clear();
            for (int i = 0; i + 2 < state.Length; i += 3) _current.list.Add((state[i], (ushort)(state[i + 1] | state[i + 2] << 8)));
            RefreshEntries();
            SelectedEntryIndex = -1;
        }

        /// <summary>On hg-engine the list comes from learnsets.json, not the last built copy.</summary>
        private static LearnsetData LoadLearnset(int id, out string error)
        {
            error = null;
            if (!DSPRE.HgEngine.HgEngineProject.IsActive) return new LearnsetData(id);

            List<byte> bytes = new System.Collections.Generic.List<byte>();
            bool read = DSPRE.HgEngine.HgEngineLearnsets.TryGetLevelMoves(id, out List<(int level, int move)> moves, out error);
            if (read)
            {
                foreach ((int level, int move) in moves)
                {
                    if (level < 0 || level > byte.MaxValue || move < 0 || move >= 0xFFFF)
                    {
                        error = $"Level {level} move {move} is out of range.";
                        break;
                    }
                    bytes.AddRange(System.BitConverter.GetBytes(((uint)level << 16) | (uint)move));
                }
            }
            if (error != null) bytes.Clear();
            bytes.AddRange(System.BitConverter.GetBytes(DSPRE.HgEngine.HgEngineLearnsets.Terminator));

            LearnsetData data = new LearnsetData(new System.IO.MemoryStream(bytes.ToArray()), wide: true);
            // The list drops repeated rows, so a save would silently remove them.
            if (error == null && data.list.Count != moves.Count)
                error = "learnsets.json repeats a level and move.";
            return data;
        }

        // ─── Add / Delete / Move ──────────────────────────────────────────────────
        public void AddEntry()
        {
            if (_current == null || _addLevel < 1 || _addLevel > 100 || _addMoveIndex <= 0) return;
            (byte, ushort) entry = ((byte)_addLevel, (ushort)_addMoveIndex);
            if (_current.list.Contains(entry)) { StatusText = "Entry already exists!"; return; }

            int insertAt = _current.list.FindIndex(x => x.level > entry.Item1 || (x.level == entry.Item1 && x.move > entry.Item2));
            if (insertAt < 0) _current.list.Add(entry);
            else              _current.list.Insert(insertAt, entry);

            RefreshEntries();
            SetDirty();
            StatusText = _loadError ?? "";
        }

        /// <summary>Puts the boxes' level and move in place of the selected row.</summary>
        public void ReplaceEntry()
        {
            if (_current == null || !CanEdit || _addLevel < 1 || _addLevel > 100 || _addMoveIndex <= 0) return;
            (byte, ushort) entry = ((byte)_addLevel, (ushort)_addMoveIndex);
            if (_current.list[_selectedEntryIndex] == entry) return;
            if (_current.list.Contains(entry)) { StatusText = "Entry already exists!"; return; }

            _current.list.RemoveAt(_selectedEntryIndex);
            int insertAt = _current.list.FindIndex(x => x.level > entry.Item1 || (x.level == entry.Item1 && x.move > entry.Item2));
            if (insertAt < 0) { _current.list.Add(entry); insertAt = _current.list.Count - 1; }
            else              _current.list.Insert(insertAt, entry);

            RefreshEntries();
            Reselect(insertAt);
            SetDirty();
            StatusText = _loadError ?? "";
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
            int from = _selectedEntryIndex;
            SwapEntries(from, from - 1);
            Reselect(from - 1);
            SetDirty();
        }

        public void MoveEntryDown()
        {
            if (!CanMoveDown) return;
            int from = _selectedEntryIndex;
            SwapEntries(from, from + 1);
            Reselect(from + 1);
            SetDirty();
        }

        // ─── Save ─────────────────────────────────────────────────────────────────
        public void Save()
        {
            if (_currentId < 0 || _current == null) return;

            if (DSPRE.HgEngine.HgEngineProject.IsActive)
            {
                if (_loadError != null)
                {
                    StatusText = "Not saved: " + _loadError;
                    _ = DialogHelper.ShowError("The learnset could not be saved.\n" + _loadError, "Save Error");
                    return;
                }
                List<(int level, int move)> entries = new System.Collections.Generic.List<(int level, int move)>(_current.list.Count);
                foreach ((byte level, ushort move) in _current.list) entries.Add((level, move));
                string error;
                bool ok;
                try { ok = DSPRE.HgEngine.HgEngineLearnsets.TrySaveLevelMoves(_currentId, entries, out error); }
                catch (System.Exception ex) { ok = false; error = ex.Message; }

                if (!ok)
                {
                    StatusText = "Not saved: " + error;
                    _ = DialogHelper.ShowError("The learnset could not be saved.\n" + error, "Save Error");
                    return;
                }
            }

            // Readers of the unpacked copy see the edit before the next sync replaces it.
            _current.SaveToFileDefaultDir(_currentId, showSuccessMessage: false);
            StatusText = "";
            _dirty = false;
            _undo?.MarkSaved();
            SaveNotice.Saved(UnsavedChangesDescription);
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        // ─── Helpers ──────────────────────────────────────────────────────────────
        private void RefreshEntries()
        {
            Entries.Clear();
            foreach ((byte level, ushort move) in _current.list)
            {
                LearnsetEntryRow row = new LearnsetEntryRow { Level = level, MoveIndex = move };
                row.UpdateDisplay(_moveNamesArr);
                Entries.Add(row);
            }
            EntryCount = Entries.Count;
            OnPropertyChanged(nameof(ExceedsVanillaLimit));
            UpdateCanAdd();
        }

        // Refilling Entries pushes -1 back through the list's binding, so the row is selected again afterwards.
        private void Reselect(int index)
        {
            SelectedEntryIndex = index;
            OnPropertyChanged(nameof(SelectedEntryIndex));
        }

        private void SwapEntries(int a, int b)
        {
            if (_current == null) return;
            (byte level, ushort move) tmp = _current.list[a];
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
            _undo?.Record();
            if (!_dirty) { _dirty = true; OnPropertyChanged(nameof(HasUnsavedChanges)); }
        }
    }
}
