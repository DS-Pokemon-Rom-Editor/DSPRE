using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using DSPRE.Editors;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Trainers
{
    /// <summary>
    /// Avalonia port of the WinForms <c>VsSeekerRematchEditor</c>: a master-detail view over the
    /// 240-row Vs. Seeker rematch table (<see cref="VsSeekerRematchTable"/>), keyed by the stored
    /// encounter trainer ID rather than row index. Diamond/Pearl/Platinum (English) only.
    /// </summary>
    public class VsSeekerRematchViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, DSPRE.Avalonia.ISupportsUndo
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (EqualityComparer<T>.Default.Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        private readonly List<RematchTable.Row> _rows;
        private readonly RematchTable.Location _location;
        private readonly HashSet<int> _dirtyRows = new();
        private List<int> _filteredIndices = new();
        private bool _suppress;
        private int _currentRowIndex = -1;

        public ObservableCollection<string> RowLabels { get; } = new();
        public ObservableCollection<string> TrainerNames { get; } = new();
        public ObservableCollection<string> RematchChoices { get; } = new();

        /// <summary>"Rematch N", with where it unlocks.</summary>
        public IReadOnlyList<string> LevelLabels { get; } = VsSeekerRematchTable.LevelUnlocks
            .Select((unlock, i) => $"Rematch {i + 1}  ·  {unlock}").ToList();

        public bool IsSupported => VsSeekerRematchTable.IsSupported;

        private string _tableNote = "";
        public string TableNote { get => _tableNote; set => Set(ref _tableNote, value); }

        /// <summary>Row layouts the game mishandles, one per line.</summary>
        public string RowProblems => _currentRowIndex < 0 ? ""
            : string.Join("\n", VsSeekerRematchTable.Problems(_rows, _currentRowIndex));

        public bool HasRowProblems => RowProblems.Length > 0;

        private void RowChanged()
        {
            OnPropertyChanged(nameof(IsRowSelected));
            OnPropertyChanged(nameof(RowProblems));
            OnPropertyChanged(nameof(HasRowProblems));
        }

        private string _filterText = "";
        public string FilterText
        {
            get => _filterText;
            set { if (Set(ref _filterText, value)) RebuildRowList(); }
        }

        private int _selectedRowListIndex = -1;
        public int SelectedRowListIndex
        {
            get => _selectedRowListIndex;
            set
            {
                if (!Set(ref _selectedRowListIndex, value)) return;
                if (_suppress) return;

                if (value < 0 || value >= _filteredIndices.Count)
                {
                    _currentRowIndex = -1;
                    RowChanged();
                    return;
                }

                _currentRowIndex = _filteredIndices[value];
                LoadRowIntoDetail(_currentRowIndex);
                RowChanged();
            }
        }

        public bool IsRowSelected => _currentRowIndex >= 0;

        private int _encounterIndex = -1;
        public int EncounterIndex
        {
            get => _encounterIndex;
            set { if (Set(ref _encounterIndex, value)) FieldChanged(); }
        }

        private int _rematchA = -1, _rematchB = -1, _rematchC = -1, _rematchD = -1, _rematchE = -1;
        public int RematchA { get => _rematchA; set { if (Set(ref _rematchA, value)) FieldChanged(); } }
        public int RematchB { get => _rematchB; set { if (Set(ref _rematchB, value)) FieldChanged(); } }
        public int RematchC { get => _rematchC; set { if (Set(ref _rematchC, value)) FieldChanged(); } }
        public int RematchD { get => _rematchD; set { if (Set(ref _rematchD, value)) FieldChanged(); } }
        public int RematchE { get => _rematchE; set { if (Set(ref _rematchE, value)) FieldChanged(); } }

        private string _statusText = "Not loaded";
        public string StatusText { get => _statusText; set => Set(ref _statusText, value); }

        // ── IEditorWithUnsavedChanges ──
        public bool HasUnsavedChanges => _dirtyRows.Count > 0;
        public string UnsavedChangesDescription => "Vs. Seeker Rematch Editor";
        public void SaveChanges() => SaveAll();
        public void DiscardChanges()
        {
            if (_dirtyRows.Count > 0 && IsSupported)
            {
                var saved = VsSeekerRematchTable.ReadAll();
                foreach (int r in _dirtyRows)
                    if (r < saved.Count) _rows[r] = saved[r];

                int listPos = _selectedRowListIndex;
                _suppress = true;
                for (int i = 0; i < _filteredIndices.Count && i < RowLabels.Count; i++)
                    if (_dirtyRows.Contains(_filteredIndices[i])) RowLabels[i] = RowLabel(_filteredIndices[i]);
                _suppress = false;
                if (_selectedRowListIndex != listPos) { _selectedRowListIndex = listPos; OnPropertyChanged(nameof(SelectedRowListIndex)); }
                if (_currentRowIndex >= 0) LoadRowIntoDetail(_currentRowIndex);
                RowChanged();
            }
            _dirtyRows.Clear();
            ResetUndo();
            OnPropertyChanged(nameof(HasUnsavedChanges));
            UpdateStatus();
        }

        // ── Undo / redo: every row's trainer ids ──
        private DSPRE.Avalonia.ByteStateUndo _undo;
        private List<ushort[]> _savedIds = new();
        public bool CanUndo => _undo?.CanUndo == true;
        public bool CanRedo => _undo?.CanRedo == true;
        public void Undo() => _undo?.Undo();
        public void Redo() => _undo?.Redo();
        private void RaiseUndo() { OnPropertyChanged(nameof(CanUndo)); OnPropertyChanged(nameof(CanRedo)); }

        private void ResetUndo()
        {
            _savedIds = _rows.Select(r => (ushort[])r.Ids.Clone()).ToList();
            _undo = new DSPRE.Avalonia.ByteStateUndo(() => DSPRE.Avalonia.UndoJson.Take(_rows.Select(r => r.Ids)), ApplyState, RaiseUndo);
            RaiseUndo();
        }

        private void ApplyState(byte[] state)
        {
            var ids = DSPRE.Avalonia.UndoJson.Read<List<ushort[]>>(state);
            for (int r = 0; r < ids.Count && r < _rows.Count; r++) _rows[r].Ids = ids[r];
            RecountDirtyRows();
            RefreshRowLabels();
            if (_currentRowIndex >= 0) LoadRowIntoDetail(_currentRowIndex);
            RowChanged();
            UpdateStatus();
        }

        // A row is unsaved while it differs from what was last read or saved, so undoing back clears it.
        private void RecountDirtyRows()
        {
            _dirtyRows.Clear();
            for (int r = 0; r < _rows.Count && r < _savedIds.Count; r++)
                if (!_rows[r].Ids.SequenceEqual(_savedIds[r])) _dirtyRows.Add(r);
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        private void RefreshRowLabels()
        {
            int listPos = _selectedRowListIndex;
            _suppress = true;
            for (int i = 0; i < _filteredIndices.Count && i < RowLabels.Count; i++)
            {
                string label = RowLabel(_filteredIndices[i]);
                if (RowLabels[i] != label) RowLabels[i] = label;
            }
            _suppress = false;
            // Replacing the selected row clears the list's selection; keep the row being edited selected.
            if (listPos >= 0) { _selectedRowListIndex = listPos; OnPropertyChanged(nameof(SelectedRowListIndex)); }
        }

        public VsSeekerRematchViewModel(int initialRowIndex = -1)
        {
            if (!IsSupported)
            {
                StatusText = "The Vs. Seeker Rematch Editor only supports Diamond, Pearl and Platinum (English).";
                _rows = new List<RematchTable.Row>();
                return;
            }

            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.trainerProperties });

            foreach (var n in DSPRE.TrainerNames.GetAll()) TrainerNames.Add(n);

            RematchChoices.Add("(skip this level - 0xFFFF)");
            RematchChoices.Add("(end of chain - 0x0000)");
            foreach (var n in TrainerNames) RematchChoices.Add(n);

            _rows = VsSeekerRematchTable.ReadAll(out _location, out string loadError);
            TableNote = _location != null
                ? "Table found in " + _location.Description
                : loadError ?? "The Vs. Seeker rematch table couldn't be located in this ROM.";

            RebuildRowList();
            ResetUndo();

            int listPosition = initialRowIndex >= 0 ? _filteredIndices.IndexOf(initialRowIndex) : -1;
            if (listPosition < 0 && _filteredIndices.Count > 0) listPosition = 0;
            SelectedRowListIndex = listPosition;

            UpdateStatus();
        }

        private string RowLabel(int rowIndex)
        {
            var row = _rows[rowIndex];
            return row.IsEmpty ? $"Row {rowIndex}: (empty)" : $"Row {rowIndex}: {TrainerLabel(row.BaseTrainerId)}";
        }

        private string TrainerLabel(int trainerId) =>
            trainerId >= 0 && trainerId < TrainerNames.Count ? TrainerNames[trainerId] : $"(raw 0x{trainerId:X4})";

        private void RebuildRowList()
        {
            _suppress = true;
            RowLabels.Clear();

            string filter = FilterText?.Trim();
            bool hasFilter = !string.IsNullOrEmpty(filter);

            _filteredIndices = new List<int>();
            for (int r = 0; r < _rows.Count; r++)
            {
                if (hasFilter && !SearchMatch.Contains(RowLabel(r), filter)) continue;
                RowLabels.Add(RowLabel(r));
                _filteredIndices.Add(r);
            }

            _suppress = false;
        }

        private void LoadRowIntoDetail(int rowIndex)
        {
            _suppress = true;
            var row = _rows[rowIndex];

            EncounterIndex = row.BaseTrainerId < TrainerNames.Count ? row.BaseTrainerId : -1;

            int[] slots = { 0, 0, 0, 0, 0 };
            for (int i = 0; i < VsSeekerRematchTable.RematchLevelCount; i++)
            {
                ushort v = row.Rematch(i);
                if (v == VsSeekerRematchTable.NoRematch) slots[i] = 0;
                else if (v == VsSeekerRematchTable.ChainEnd) slots[i] = 1;
                else if (v < TrainerNames.Count) slots[i] = 2 + v;
                else slots[i] = -1;
            }
            RematchA = slots[0]; RematchB = slots[1]; RematchC = slots[2]; RematchD = slots[3]; RematchE = slots[4];

            _suppress = false;
        }

        private void FieldChanged()
        {
            if (_suppress || _currentRowIndex < 0) return;

            var row = _rows[_currentRowIndex];
            if (EncounterIndex >= 0) row.BaseTrainerId = (ushort)EncounterIndex;

            int[] slots = { RematchA, RematchB, RematchC, RematchD, RematchE };
            for (int i = 0; i < VsSeekerRematchTable.RematchLevelCount; i++)
            {
                int idx = slots[i];
                if (idx == 0) row.SetRematch(i, VsSeekerRematchTable.NoRematch);
                else if (idx == 1) row.SetRematch(i, VsSeekerRematchTable.ChainEnd);
                else if (idx >= 2) row.SetRematch(i, (ushort)(idx - 2));
            }
            _rows[_currentRowIndex] = row;

            _undo?.Record();
            RecountDirtyRows();
            RowChanged();
            UpdateStatus();
            RefreshRowLabels();
        }

        public void SaveAll()
        {
            if (_dirtyRows.Count == 0)
            {
                UpdateStatus("Nothing to save.");
                return;
            }

            foreach (int r in _dirtyRows.ToList())
            {
                if (!VsSeekerRematchTable.WriteRow(r, _rows[r], out string error))
                {
                    AppMessages.Error($"Save failed on row {r}: {error}", "Error");
                    return;
                }
            }

            int count = _dirtyRows.Count;
            _dirtyRows.Clear();
            _savedIds = _rows.Select(r => (ushort[])r.Ids.Clone()).ToList();
            _undo?.MarkSaved();
            SaveNotice.Saved(UnsavedChangesDescription);
            OnPropertyChanged(nameof(HasUnsavedChanges));
            UpdateStatus($"Saved {count} row(s).");
        }

        private void UpdateStatus(string message = null) =>
            StatusText = message ?? $"{_rows.Count} rows.{(_dirtyRows.Count > 0 ? $" {_dirtyRows.Count} unsaved row(s)." : "")}";
    }
}
