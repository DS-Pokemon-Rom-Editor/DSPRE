using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.Models;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views
{
    public partial class TmHmBulkEditorView : UserControl
    {
        private TmHmBulkEditorViewModel VM => DataContext as TmHmBulkEditorViewModel;

        public TmHmBulkEditorView()
        {
            InitializeComponent();
        }

        public TmHmBulkEditorView(TmHmBulkEditorViewModel vm) : this()
        {
            DataContext = vm;
        }

        private void ByPokemonMode_Click(object sender, RoutedEventArgs e) { if (VM != null) VM.IsByMachineMode = false; }
        private void ByMachineMode_Click(object sender, RoutedEventArgs e) { if (VM != null) VM.IsByMachineMode = true; }

        private void SaveAll_Click(object sender, RoutedEventArgs e) => VM?.SaveAllChanges();

        private void SelectAll_Click(object sender, RoutedEventArgs e) => VM?.SetAllVisibleLeavesChecked(true);
        private void SelectNone_Click(object sender, RoutedEventArgs e) => VM?.SetAllVisibleLeavesChecked(false);

        private void SyncUnion_Click(object sender, RoutedEventArgs e) => VM?.SyncFamilies(true);
        private void SyncIntersect_Click(object sender, RoutedEventArgs e) => VM?.SyncFamilies(false);

        private void MachineCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null) return;
            if ((sender as CheckBox)?.DataContext is FlagChecklistItem item)
                VM.ToggleMachineForSelection(item.Index);
        }

        private async void CopyTo_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null) return;

            var dlgVm = new CopyMachinesDialogViewModel(
                PokemonNamesFromTree(),
                VM.FamilyGroups,
                VM.SingleSelectedSpeciesId,
                VM.GetSpeciesLabel);

            var dlg = new CopyMachinesDialogView(dlgVm);
            var owner = TopLevel.GetTopLevel(this) as Window;
            if (owner != null) await dlg.ShowDialog(owner); else dlg.Show();

            if (!dlgVm.Confirmed || dlgVm.SelectedTargetIds.Count == 0) return;

            VM.CopyMachinesTo(dlgVm.SourceIndex, dlgVm.SelectedTargetIds);
        }

        private string[] PokemonNamesFromTree()
        {
            // The dialog needs the full species-name list (for its own source dropdown), which the
            // main VM doesn't expose directly — rebuilt here from the same source it was constructed
            // with via RomInfo, matching AvaloniaEditorLauncher's own lookup.
            return RomInfo.GetPokemonNames();
        }
    }
}
