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
    /// Master-detail view over a <see cref="RematchTable"/>: a base trainer and five rematch levels per row.
    /// The Vs. Seeker and Pokégear editors differ only in where their table lives and what they show beside it.
    /// </summary>
    public abstract class RematchEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, DSPRE.Avalonia.ISupportsUndo
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        protected bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (EqualityComparer<T>.Default.Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        private List<RematchTable.Row> _rows = new();
        private int _currentRowIndex = -1;
        protected IReadOnlyList<RematchTable.Row> Rows => _rows;
        protected RematchTable.Location Location { get; private set; }
        /// <summary>The row shown in the detail panel, or -1.</summary>
        protected int CurrentRowIndex => _currentRowIndex;
        private readonly HashSet<int> _dirtyRows = new();
        private List<int> _filteredIndices = new();
        private bool _suppress;

        public ObservableCollection<string> RowLabels { get; } = new();
        public ObservableCollection<string> TrainerNames { get; } = new();
        public ObservableCollection<string> RematchChoices { get; } = new();

        // Choice list positions in front of the trainer names.
        private const int ChoiceChainEnd = 0;
        private const int ChoiceSkip = 1;
        private const int ChoiceFirstTrainer = 2;

        /// <summary>"Rematch N", with what unlocks it.</summary>
        public IReadOnlyList<string> LevelLabels { get; }

        public abstract bool IsSupported { get; }
        public abstract string UnsavedChangesDescription { get; }
        protected abstract string EditorTitle { get; }
        protected abstract string TableName { get; }
        protected abstract List<RematchTable.Row> ReadRows(out RematchTable.Location location, out string error);
        protected abstract bool WriteRow(int rowIndex, RematchTable.Row row, out string error);
        protected abstract List<string> Problems(int rowIndex);

        /// <summary>Runs after the rows are read and before the list is built.</summary>
        protected virtual void RowsLoaded() { }

        /// <summary>Runs whenever row contents change: an edit, undo, redo or discard.</summary>
        protected virtual void RowsEdited() { }

        protected RematchEditorViewModel(IReadOnlyList<string> levelUnlocks)
        {
            LevelLabels = levelUnlocks.Select((unlock, i) => $"Rematch {i + 1}  ·  {unlock}").ToList();
        }

        /// <summary>Reads the table and selects the starting row. Called by each editor's <c>Open</c>.</summary>
        protected void Load(int initialRowIndex)
        {
            if (!IsSupported)
            {
                StatusText = "Not in this game";
                return;
            }

            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.TrainerProperties });

            foreach (string n in DSPRE.TrainerNames.GetAll()) TrainerNames.Add(n);

            RematchChoices.Add("Ends here");
            RematchChoices.Add("Skip this level");
            foreach (string n in TrainerNames) RematchChoices.Add(n);

            _rows = ReadRows(out RematchTable.Location location, out string loadError);
            Location = location;
            TableNote = location != null
                ? ""
                : loadError ?? $"The {TableName} couldn't be located in this ROM.";

            RowsLoaded();
            RebuildRowList();
            ResetUndo();

            int listPosition = initialRowIndex >= 0 ? _filteredIndices.IndexOf(initialRowIndex) : -1;
            if (listPosition < 0 && _filteredIndices.Count > 0) listPosition = 0;
            SelectedRowListIndex = listPosition;

            UpdateStatus();
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

        private int _baseTrainerIndex = -1;
        public int BaseTrainerIndex
        {
            get => _baseTrainerIndex;
            set { if (Set(ref _baseTrainerIndex, value)) FieldChanged(); }
        }

        private int _rematch1 = -1, _rematch2 = -1, _rematch3 = -1, _rematch4 = -1, _rematch5 = -1;
        public int Rematch1 { get => _rematch1; set { if (Set(ref _rematch1, value)) FieldChanged(); } }
        public int Rematch2 { get => _rematch2; set { if (Set(ref _rematch2, value)) FieldChanged(); } }
        public int Rematch3 { get => _rematch3; set { if (Set(ref _rematch3, value)) FieldChanged(); } }
        public int Rematch4 { get => _rematch4; set { if (Set(ref _rematch4, value)) FieldChanged(); } }
        public int Rematch5 { get => _rematch5; set { if (Set(ref _rematch5, value)) FieldChanged(); } }

        private string _statusText = "Not loaded";
        public string StatusText { get => _statusText; set => Set(ref _statusText, value); }

        private string _tableNote = "";
        public string TableNote { get => _tableNote; set { if (Set(ref _tableNote, value)) OnPropertyChanged(nameof(HasNotes)); } }

        public virtual bool HasNotes => !string.IsNullOrEmpty(_tableNote);

        /// <summary>Row layouts the game mishandles, one per line.</summary>
        public string RowProblems => _currentRowIndex < 0 ? "" : string.Join("\n", Problems(_currentRowIndex));

        public bool HasRowProblems => RowProblems.Length > 0;

        protected virtual void RowChanged()
        {
            OnPropertyChanged(nameof(IsRowSelected));
            OnPropertyChanged(nameof(RowProblems));
            OnPropertyChanged(nameof(HasRowProblems));
        }

        // ── IEditorWithUnsavedChanges ──
        public bool HasUnsavedChanges => _dirtyRows.Count > 0;
        public void SaveChanges() => SaveAll();
        public void DiscardChanges()
        {
            if (_dirtyRows.Count > 0 && IsSupported)
            {
                List<RematchTable.Row> saved = ReadRows(out _, out _);
                foreach (int r in _dirtyRows)
                    if (r < saved.Count && r < _rows.Count) _rows[r] = saved[r];

                int listPos = _selectedRowListIndex;
                _suppress = true;
                for (int i = 0; i < _filteredIndices.Count && i < RowLabels.Count; i++)
                    if (_dirtyRows.Contains(_filteredIndices[i])) RowLabels[i] = RowLabel(_filteredIndices[i]);
                _suppress = false;
                if (_selectedRowListIndex != listPos) { _selectedRowListIndex = listPos; OnPropertyChanged(nameof(SelectedRowListIndex)); }
                if (_currentRowIndex >= 0) LoadRowIntoDetail(_currentRowIndex);
                RowsEdited();
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
            List<ushort[]> ids = DSPRE.Avalonia.UndoJson.Read<List<ushort[]>>(state);
            for (int r = 0; r < ids.Count && r < _rows.Count; r++) _rows[r].Ids = ids[r];
            RecountDirtyRows();
            RefreshRowLabels();
            if (_currentRowIndex >= 0) LoadRowIntoDetail(_currentRowIndex);
            RowChanged();
            RowsEdited();
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

        protected virtual string RowLabel(int rowIndex)
        {
            RematchTable.Row row = _rows[rowIndex];
            return row.IsEmpty ? $"{rowIndex}: (empty)" : $"{rowIndex}: {TrainerLabel(row.BaseTrainerId)}";
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
            RematchTable.Row row = _rows[rowIndex];

            BaseTrainerIndex = row.BaseTrainerId < TrainerNames.Count ? row.BaseTrainerId : -1;

            int[] slots = new int[RematchTable.RematchLevelCount];
            for (int i = 0; i < slots.Length; i++)
            {
                ushort v = row.Rematch(i);
                if (v == RematchTable.ChainEnd) slots[i] = ChoiceChainEnd;
                else if (v == RematchTable.NoRematch) slots[i] = ChoiceSkip;
                else if (v < TrainerNames.Count) slots[i] = ChoiceFirstTrainer + v;
                else slots[i] = -1;
            }
            Rematch1 = slots[0]; Rematch2 = slots[1]; Rematch3 = slots[2]; Rematch4 = slots[3]; Rematch5 = slots[4];

            _suppress = false;
        }

        private void FieldChanged()
        {
            if (_suppress || _currentRowIndex < 0) return;

            RematchTable.Row row = _rows[_currentRowIndex];
            if (BaseTrainerIndex >= 0) row.BaseTrainerId = (ushort)BaseTrainerIndex;

            int[] slots = { Rematch1, Rematch2, Rematch3, Rematch4, Rematch5 };
            for (int i = 0; i < RematchTable.RematchLevelCount; i++)
            {
                int idx = slots[i];
                if (idx == ChoiceChainEnd) row.SetRematch(i, RematchTable.ChainEnd);
                else if (idx == ChoiceSkip) row.SetRematch(i, RematchTable.NoRematch);
                else if (idx >= ChoiceFirstTrainer) row.SetRematch(i, (ushort)(idx - ChoiceFirstTrainer));
            }

            _undo?.Record();
            RecountDirtyRows();
            RowChanged();
            RowsEdited();
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
                if (!WriteRow(r, _rows[r], out string error))
                {
                    if (error != null) AppMessages.Error($"Row {r} wasn't saved:\n{error}", EditorTitle);
                    return;
                }
            }

            _dirtyRows.Clear();
            _savedIds = _rows.Select(r => (ushort[])r.Ids.Clone()).ToList();
            _undo?.MarkSaved();
            SaveNotice.Saved(UnsavedChangesDescription);
            OnPropertyChanged(nameof(HasUnsavedChanges));
            UpdateStatus();
        }

        private void UpdateStatus(string message = null) =>
            StatusText = message ?? "";
    }
}
