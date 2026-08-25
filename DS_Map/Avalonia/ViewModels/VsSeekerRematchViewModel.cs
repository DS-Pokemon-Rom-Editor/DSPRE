using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using DSPRE.Editors;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels
{
    /// <summary>
    /// Avalonia port of the WinForms <c>VsSeekerRematchEditor</c>: a master-detail view over the
    /// 240-row Vs. Seeker rematch table (<see cref="VsSeekerRematchTable"/>), keyed by the stored
    /// encounter trainer ID rather than row index. Diamond/Pearl/Platinum (English) only.
    /// </summary>
    public class VsSeekerRematchViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (EqualityComparer<T>.Default.Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        private readonly List<VsSeekerRematchTable.Row> _rows;
        private readonly HashSet<int> _dirtyRows = new();
        private List<int> _filteredIndices = new();
        private bool _suppress;
        private int _currentRowIndex = -1;

        public ObservableCollection<string> RowLabels { get; } = new();
        public ObservableCollection<string> TrainerNames { get; } = new();
        public ObservableCollection<string> RematchChoices { get; } = new();

        public bool IsSupported => VsSeekerRematchTable.IsSupported;

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
                    OnPropertyChanged(nameof(IsRowSelected));
                    return;
                }

                _currentRowIndex = _filteredIndices[value];
                LoadRowIntoDetail(_currentRowIndex);
                OnPropertyChanged(nameof(IsRowSelected));
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
        public void DiscardChanges() { _dirtyRows.Clear(); OnPropertyChanged(nameof(HasUnsavedChanges)); }

        public VsSeekerRematchViewModel(int initialRowIndex = -1)
        {
            if (!IsSupported)
            {
                StatusText = "The Vs. Seeker Rematch Editor only supports Diamond, Pearl and Platinum (English).";
                _rows = new List<VsSeekerRematchTable.Row>();
                return;
            }

            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.trainerProperties });

            foreach (var n in DSPRE.TrainerNames.GetAll()) TrainerNames.Add(n);

            RematchChoices.Add("(none - 0xFFFF)");
            RematchChoices.Add("(chain ends here - 0x0000)");
            foreach (var n in TrainerNames) RematchChoices.Add(n);

            _rows = VsSeekerRematchTable.ReadAll();

            RebuildRowList();

            int listPosition = initialRowIndex >= 0 ? _filteredIndices.IndexOf(initialRowIndex) : -1;
            if (listPosition < 0 && _filteredIndices.Count > 0) listPosition = 0;
            SelectedRowListIndex = listPosition;

            UpdateStatus();
        }

        private string RowLabel(int rowIndex)
        {
            var row = _rows[rowIndex];
            bool empty = row.EncounterTrainerId == 0 && row.RematchTrainerIds.All(v => v == 0);
            return empty ? $"Row {rowIndex}: (empty)" : $"Row {rowIndex}: {TrainerLabel(row.EncounterTrainerId)}";
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
                if (hasFilter && RowLabel(r).IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                RowLabels.Add(RowLabel(r));
                _filteredIndices.Add(r);
            }

            _suppress = false;
        }

        private void LoadRowIntoDetail(int rowIndex)
        {
            _suppress = true;
            var row = _rows[rowIndex];

            EncounterIndex = row.EncounterTrainerId < TrainerNames.Count ? row.EncounterTrainerId : -1;

            int[] slots = { 0, 0, 0, 0, 0 };
            for (int i = 0; i < VsSeekerRematchTable.RematchLevelCount; i++)
            {
                ushort v = row.RematchTrainerIds[i];
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
            if (EncounterIndex >= 0) row.EncounterTrainerId = (ushort)EncounterIndex;

            int[] slots = { RematchA, RematchB, RematchC, RematchD, RematchE };
            for (int i = 0; i < VsSeekerRematchTable.RematchLevelCount; i++)
            {
                int idx = slots[i];
                if (idx == 0) row.RematchTrainerIds[i] = VsSeekerRematchTable.NoRematch;
                else if (idx == 1) row.RematchTrainerIds[i] = VsSeekerRematchTable.ChainEnd;
                else if (idx >= 2) row.RematchTrainerIds[i] = (ushort)(idx - 2);
            }
            _rows[_currentRowIndex] = row;

            _dirtyRows.Add(_currentRowIndex);
            OnPropertyChanged(nameof(HasUnsavedChanges));
            UpdateStatus();

            int listPos = _selectedRowListIndex;
            if (listPos >= 0 && listPos < RowLabels.Count)
            {
                _suppress = true;
                RowLabels[listPos] = RowLabel(_currentRowIndex);
                _suppress = false;
            }
        }

        public void SaveCurrentRow()
        {
            if (_currentRowIndex < 0) return;

            if (!VsSeekerRematchTable.WriteRow(_currentRowIndex, _rows[_currentRowIndex], out string error))
            {
                AppMessages.Error("Save failed: " + error, "Error");
                return;
            }
            _dirtyRows.Remove(_currentRowIndex);
            OnPropertyChanged(nameof(HasUnsavedChanges));
            UpdateStatus($"Row {_currentRowIndex} saved.");
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
            OnPropertyChanged(nameof(HasUnsavedChanges));
            UpdateStatus($"Saved {count} row(s).");
        }

        private void UpdateStatus(string message = null) =>
            StatusText = message ?? $"{_rows.Count} rows.{(_dirtyRows.Count > 0 ? $" {_dirtyRows.Count} unsaved row(s)." : "")}";
    }
}
