using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels.Pokemon;

namespace DSPRE.Avalonia.Views.Pokemon
{
    public partial class BugContestPrizesView : UserControl
    {
        private BugContestPrizesViewModel VM => DataContext as BugContestPrizesViewModel;

        public BugContestPrizesView()
        {
            InitializeComponent();
            TabSetup.OnFirstShow(this, () => VM?.Setup());
        }

        private async void Save_Click(object sender, RoutedEventArgs e) { if (VM != null) await VM.SaveChangesAsync(); }

        private async void Discard_Click(object sender, RoutedEventArgs e)
        {
            if (await DialogHelper.ConfirmDiscardAsync(this)) VM?.DiscardChanges();
        }

        private void AddSlot_Click(object sender, RoutedEventArgs e) => VM?.AddSlot();

        private void RemoveSlot_Click(object sender, RoutedEventArgs e) =>
            VM?.RemoveSlot((sender as Control)?.DataContext as BugContestPrizesViewModel.SlotRow);
    }
}
