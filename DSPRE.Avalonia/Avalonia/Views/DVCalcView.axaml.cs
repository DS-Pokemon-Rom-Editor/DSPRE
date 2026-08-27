using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views
{
    public partial class DVCalcView : Window
    {
        private DVCalcViewModel VM => DataContext as DVCalcViewModel;

        public DVCalcView()
        {
            InitializeComponent();
        }

        public DVCalcView(DVCalcViewModel vm) : this()
        {
            DataContext = vm;
        }

        private async void Change_Click(object sender, RoutedEventArgs e) => await OpenViewer(sender, true);
        private async void ShowAll_Click(object sender, RoutedEventArgs e) => await OpenViewer(sender, false);

        private async System.Threading.Tasks.Task OpenViewer(object sender, bool highestOnly)
        {
            if (VM == null || (sender as Control)?.DataContext is not DVCalcSlotViewModel slot) return;
            var triplets = VM.GenerateTriplets(slot.Index, highestOnly);
            var dlgVm = new DVCalcNatureViewerViewModel(triplets);
            var dlg = new DVCalcNatureViewerView(dlgVm);
            await dlg.ShowDialog(this);
            if (dlgVm.SelectedDV >= 0) slot.DV = dlgVm.SelectedDV;
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            VM?.Confirm();
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
    }
}
