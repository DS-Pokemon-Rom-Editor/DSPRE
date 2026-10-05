using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels.Pokemon;

namespace DSPRE.Avalonia.Views.Pokemon
{
    public partial class RoamersView : UserControl
    {
        private RoamersViewModel VM => DataContext as RoamersViewModel;

        public RoamersView() { InitializeComponent(); }

        private async void Save_Click(object sender, RoutedEventArgs e) { if (VM != null) await VM.SaveChangesAsync(); }
        private void Discard_Click(object sender, RoutedEventArgs e) => VM?.DiscardChanges();
    }
}
