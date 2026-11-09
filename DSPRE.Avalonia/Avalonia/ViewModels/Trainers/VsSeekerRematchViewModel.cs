using System.Collections.Generic;

namespace DSPRE.Avalonia.ViewModels.Trainers
{
    /// <summary>
    /// The 240-row Vs. Seeker rematch table (<see cref="VsSeekerRematchTable"/>), keyed by encounter trainer
    /// ID, not row. Diamond, Pearl and Platinum (English) only.
    /// </summary>
    public class VsSeekerRematchViewModel : RematchEditorViewModel
    {
        public override bool IsSupported => VsSeekerRematchTable.IsSupported;
        public override string UnsavedChangesDescription => "Vs. Seeker rematches";
        protected override string EditorTitle => "Vs. Seeker rematch editor";
        protected override string TableName => "Vs. Seeker rematch table";

        private VsSeekerRematchViewModel(bool _) : base(VsSeekerRematchTable.LevelUnlocks) { }

        /// <summary>Design preview only.</summary>
        public VsSeekerRematchViewModel() : base(VsSeekerRematchTable.LevelUnlocks) { }

        public static VsSeekerRematchViewModel Open(int initialRowIndex = -1)
        {
            VsSeekerRematchViewModel vm = new VsSeekerRematchViewModel(true);
            vm.Load(initialRowIndex);
            return vm;
        }

        protected override List<RematchTable.Row> ReadRows(out RematchTable.Location location, out string error) =>
            VsSeekerRematchTable.ReadAll(out location, out error);

        protected override bool WriteRow(int rowIndex, RematchTable.Row row, out string error) =>
            VsSeekerRematchTable.WriteRow(rowIndex, row, out error);

        protected override List<string> Problems(int rowIndex) => VsSeekerRematchTable.Problems(Rows, rowIndex);
    }
}
