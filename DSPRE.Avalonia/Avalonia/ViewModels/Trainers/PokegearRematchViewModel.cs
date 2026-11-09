using System.Collections.Generic;
using System.Linq;

namespace DSPRE.Avalonia.ViewModels.Trainers
{
    /// <summary>
    /// Master-detail view over <see cref="PokegearRematchTable"/>. The phone book is shown alongside
    /// because a row whose base trainer nobody can phone is never reached.
    /// </summary>
    public class PokegearRematchViewModel : RematchEditorViewModel
    {
        private readonly Dictionary<ushort, int> _phoneEntryByTrainer = new();
        private string[] _contactNames = System.Array.Empty<string>();

        public override bool IsSupported => PokegearRematchTable.IsSupported;
        public override string UnsavedChangesDescription => "Pokégear rematches";
        protected override string EditorTitle => "Pokégear rematch editor";
        protected override string TableName => "Pokégear rematch table";

        private PokegearRematchViewModel(bool _) : base(PokegearRematchTable.LevelUnlocks) { }

        /// <summary>Design preview only.</summary>
        public PokegearRematchViewModel() : base(PokegearRematchTable.LevelUnlocks) { }

        public static PokegearRematchViewModel Open(int initialRowIndex = -1)
        {
            PokegearRematchViewModel vm = new PokegearRematchViewModel(true);
            vm.Load(initialRowIndex);
            return vm;
        }

        protected override List<RematchTable.Row> ReadRows(out RematchTable.Location location, out string error) =>
            PokegearRematchTable.ReadAll(out location, out error);

        protected override bool WriteRow(int rowIndex, RematchTable.Row row, out string error) =>
            PokegearRematchTable.WriteRow(Location, rowIndex, row, out error);

        protected override List<string> Problems(int rowIndex) => PokegearRematchTable.Problems(Rows, rowIndex);

        private string _reachabilityNote = "";
        public string ReachabilityNote { get => _reachabilityNote; set { if (Set(ref _reachabilityNote, value)) OnPropertyChanged(nameof(HasNotes)); } }
        public override bool HasNotes => base.HasNotes || !string.IsNullOrEmpty(_reachabilityNote);

        /// <summary>Which phone book entry reaches this row, if any.</summary>
        public string RowNote
        {
            get
            {
                if (CurrentRowIndex < 0 || _phoneEntryByTrainer.Count == 0) return "";
                return PhoneEntry >= 0
                    ? $"Called by {ContactName(PhoneEntry)} in the phone book."
                    : "No phone book entry calls this trainer, so this row is never used.";
            }
        }

        public int PhoneEntry => CurrentRowIndex >= 0 &&
            _phoneEntryByTrainer.TryGetValue(Rows[CurrentRowIndex].BaseTrainerId, out int entry) ? entry : -1;

        public bool HasPhoneEntry => PhoneEntry >= 0 && BetaEditors.Allows("PokegearPhoneBookView");

        protected override void RowChanged()
        {
            base.RowChanged();
            OnPropertyChanged(nameof(RowNote));
            OnPropertyChanged(nameof(PhoneEntry));
            OnPropertyChanged(nameof(HasPhoneEntry));
        }

        private string ContactName(int entry) =>
            entry >= 0 && entry < _contactNames.Length ? _contactNames[entry] : $"entry {entry}";

        protected override void RowsLoaded()
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

            RowsEdited();
        }

        protected override void RowsEdited()
        {
            if (_phoneEntryByTrainer.Count == 0) return;

            HashSet<ushort> rowTrainers = new HashSet<ushort>(Rows.Where(r => !r.IsEmpty).Select(r => r.BaseTrainerId));
            int unreachableRows = Rows.Count(r => !r.IsEmpty && !_phoneEntryByTrainer.ContainsKey(r.BaseTrainerId));
            List<ushort> callersWithoutRow = _phoneEntryByTrainer.Keys.Where(id => !rowTrainers.Contains(id)).ToList();

            if (unreachableRows == 0 && callersWithoutRow.Count == 0)
            {
                ReachabilityNote = "";
                return;
            }

            List<string> parts = new List<string>();
            if (unreachableRows > 0)
            {
                parts.Add($"{unreachableRows} {(unreachableRows == 1 ? "row has a base trainer" : "rows have base trainers")} no Pokégear entry calls, " +
                    $"so {(unreachableRows == 1 ? "it is" : "they are")} never reached");
            }
            if (callersWithoutRow.Count > 0)
            {
                parts.Add($"{callersWithoutRow.Count} Pokégear {(callersWithoutRow.Count == 1 ? "caller has" : "callers have")} no row, " +
                    "so their rematch battles never start");
            }
            ReachabilityNote = string.Join("; ", parts) + ".";
        }

        protected override string RowLabel(int rowIndex)
        {
            string label = base.RowLabel(rowIndex);
            if (Rows[rowIndex].IsEmpty || _phoneEntryByTrainer.Count == 0) return label;

            return _phoneEntryByTrainer.TryGetValue(Rows[rowIndex].BaseTrainerId, out int entry)
                ? $"{label}  ·  {ContactName(entry)}"
                : $"{label}  ·  not in phone book";
        }
    }
}
