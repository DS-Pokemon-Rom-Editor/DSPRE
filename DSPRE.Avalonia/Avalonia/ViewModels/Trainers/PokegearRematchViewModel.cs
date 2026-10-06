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
    /// Master-detail view over <see cref="PokegearRematchTable"/>. The phone book is shown alongside
    /// because a row whose base trainer nobody can phone is never reached.
    /// </summary>
    public class PokegearRematchViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, DSPRE.Avalonia.ISupportsUndo
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (EqualityComparer<T>.Default.Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        private readonly List<RematchTable.Row> _rows;
        private readonly RematchTable.Location _location;
        private readonly Dictionary<ushort, int> _phoneEntryByTrainer = new();
        private string[] _contactNames = System.Array.Empty<string>();
        private readonly HashSet<int> _dirtyRows = new();
        private List<int> _filteredIndices = new();
        private bool _suppress;
        private int _currentRowIndex = -1;
        private string _loadError;

        public ObservableCollection<string> RowLabels { get; } = new();
        public ObservableCollection<string> TrainerNames { get; } = new();
        public ObservableCollection<string> RematchChoices { get; } = new();

        // Choice list positions in front of the trainer names.
        private const int ChoiceChainEnd = 0;
        private const int ChoiceSkip = 1;
        private const int ChoiceFirstTrainer = 2;

        /// <summary>"Rematch N", with what unlocks it.</summary>
        public IReadOnlyList<string> LevelLabels { get; } = PokegearRematchTable.LevelUnlocks
            .Select((unlock, i) => $"Rematch {i + 1}  ·  {unlock}").ToList();

        public bool IsSupported => PokegearRematchTable.IsSupported;

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
        public string TableNote { get => _tableNote; set => Set(ref _tableNote, value); }

        private string _reachabilityNote = "";
        public string ReachabilityNote { get => _reachabilityNote; set => Set(ref _reachabilityNote, value); }

        /// <summary>Which phone book entry reaches this row, if any.</summary>
        public string RowNote
        {
            get
            {
                if (_currentRowIndex < 0 || _phoneEntryByTrainer.Count == 0) return "";
                return PhoneEntry >= 0
                    ? $"Called by {ContactName(PhoneEntry)} in the phone book."
                    : "No phone book entry calls this trainer, so this row is never used.";
            }
        }

        public int PhoneEntry => _currentRowIndex >= 0 &&
            _phoneEntryByTrainer.TryGetValue(_rows[_currentRowIndex].BaseTrainerId, out int entry) ? entry : -1;

        public bool HasPhoneEntry => PhoneEntry >= 0 && BetaEditors.Allows("PokegearPhoneBookView");

        /// <summary>Row layouts the game mishandles, one per line.</summary>
        public string RowProblems => _currentRowIndex < 0 ? ""
            : string.Join("\n", PokegearRematchTable.Problems(_rows, _currentRowIndex));

        public bool HasRowProblems => RowProblems.Length > 0;

        private void RowChanged()
        {
            OnPropertyChanged(nameof(IsRowSelected));
            OnPropertyChanged(nameof(RowNote));
            OnPropertyChanged(nameof(PhoneEntry));
            OnPropertyChanged(nameof(HasPhoneEntry));
            OnPropertyChanged(nameof(RowProblems));
            OnPropertyChanged(nameof(HasRowProblems));
        }

        private string ContactName(int entry) =>
            entry >= 0 && entry < _contactNames.Length ? _contactNames[entry] : $"entry {entry}";

        // ── IEditorWithUnsavedChanges ──
        public bool HasUnsavedChanges => _dirtyRows.Count > 0;
        public string UnsavedChangesDescription => "Pokégear Rematch Editor";
        public void SaveChanges() => SaveAll();
        public void DiscardChanges()
        {
            if (_dirtyRows.Count > 0 && IsSupported)
            {
                List<RematchTable.Row> saved = PokegearRematchTable.ReadAll(out _, out _);
                foreach (int r in _dirtyRows)
                    if (r < saved.Count && r < _rows.Count) _rows[r] = saved[r];

                int listPos = _selectedRowListIndex;
                _suppress = true;
                for (int i = 0; i < _filteredIndices.Count && i < RowLabels.Count; i++)
                    if (_dirtyRows.Contains(_filteredIndices[i])) RowLabels[i] = RowLabel(_filteredIndices[i]);
                _suppress = false;
                if (_selectedRowListIndex != listPos) { _selectedRowListIndex = listPos; OnPropertyChanged(nameof(SelectedRowListIndex)); }
                if (_currentRowIndex >= 0) LoadRowIntoDetail(_currentRowIndex);
                UpdateReachability();
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
            UpdateReachability();
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

        public PokegearRematchViewModel(int initialRowIndex = -1)
        {
            if (!IsSupported)
            {
                StatusText = "The Pokégear Rematch Editor only supports HeartGold and SoulSilver.";
                _rows = new List<RematchTable.Row>();
                return;
            }

            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.trainerProperties });

            foreach (string n in DSPRE.TrainerNames.GetAll()) TrainerNames.Add(n);

            RematchChoices.Add("(end of chain - 0x0000)");
            RematchChoices.Add("(skip this level - 0xFFFF)");
            foreach (string n in TrainerNames) RematchChoices.Add(n);

            _rows = PokegearRematchTable.ReadAll(out _location, out _loadError);

            LoadPhoneBook();

            RebuildRowList();
            ResetUndo();

            int listPosition = initialRowIndex >= 0 ? _filteredIndices.IndexOf(initialRowIndex) : -1;
            if (listPosition < 0 && _filteredIndices.Count > 0) listPosition = 0;
            SelectedRowListIndex = listPosition;

            TableNote = _location != null
                ? "Table found in " + _location.Description
                : _loadError ?? "The Pokégear rematch table couldn't be located in this ROM.";

            UpdateStatus();
        }

        private void LoadPhoneBook()
        {
            if (!PokegearPhoneBook.TryReadTrainerIds(out ushort[] phoneTrainers, out string phoneError))
            {
                ReachabilityNote = phoneError;
                return;
            }

            for (int entry = 0; entry < phoneTrainers.Length; entry++)
            {
                ushort trainerId = phoneTrainers[entry];
                if (trainerId == 0) continue;
                _phoneEntryByTrainer.TryAdd(trainerId, entry);
            }
            _contactNames = PokegearContactArchives.Names(phoneTrainers.Length);

            UpdateReachability();
        }

        private void UpdateReachability()
        {
            if (_phoneEntryByTrainer.Count == 0) return;

            HashSet<ushort> rowTrainers = new HashSet<ushort>(_rows.Where(r => !r.IsEmpty).Select(r => r.BaseTrainerId));
            int unreachableRows = _rows.Count(r => !r.IsEmpty && !_phoneEntryByTrainer.ContainsKey(r.BaseTrainerId));
            List<ushort> callersWithoutRow = _phoneEntryByTrainer.Keys.Where(id => !rowTrainers.Contains(id)).ToList();

            if (unreachableRows == 0 && callersWithoutRow.Count == 0)
            {
                ReachabilityNote = $"Every row is called by one of the {_phoneEntryByTrainer.Count} " +
                    "Pokégear callers, and every caller has a row.";
                return;
            }

            List<string> parts = new List<string>();
            if (unreachableRows > 0)
            {
                parts.Add($"{unreachableRows} row(s) have a base trainer no Pokégear entry calls, " +
                    "so they are never reached");
            }
            if (callersWithoutRow.Count > 0)
            {
                parts.Add($"{callersWithoutRow.Count} Pokégear caller(s) have no row, " +
                    "so their rematch battles never start");
            }
            ReachabilityNote = string.Join("; ", parts) + ".";
        }

        private string RowLabel(int rowIndex)
        {
            RematchTable.Row row = _rows[rowIndex];
            if (row.IsEmpty) return $"Row {rowIndex}: (empty)";

            string label = $"Row {rowIndex}: {TrainerLabel(row.BaseTrainerId)}";
            if (_phoneEntryByTrainer.Count == 0) return label;

            return _phoneEntryByTrainer.TryGetValue(row.BaseTrainerId, out int entry)
                ? $"{label}  ·  {ContactName(entry)}"
                : $"{label}  ·  not in phone book";
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
                if (v == PokegearRematchTable.ChainEnd) slots[i] = ChoiceChainEnd;
                else if (v == PokegearRematchTable.NoRematch) slots[i] = ChoiceSkip;
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
                if (idx == ChoiceChainEnd) row.SetRematch(i, PokegearRematchTable.ChainEnd);
                else if (idx == ChoiceSkip) row.SetRematch(i, PokegearRematchTable.NoRematch);
                else if (idx >= ChoiceFirstTrainer) row.SetRematch(i, (ushort)(idx - ChoiceFirstTrainer));
            }

            _undo?.Record();
            RecountDirtyRows();
            RowChanged();
            UpdateReachability();
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
                if (!PokegearRematchTable.WriteRow(_location, r, _rows[r], out string error))
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
