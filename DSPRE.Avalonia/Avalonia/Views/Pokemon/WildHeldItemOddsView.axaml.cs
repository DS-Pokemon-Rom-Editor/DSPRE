using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels.Pokemon;

namespace DSPRE.Avalonia.Views.Pokemon
{
    public partial class WildHeldItemOddsView : UserControl
    {
        private WildHeldItemOddsViewModel VM => DataContext as WildHeldItemOddsViewModel;

        public WildHeldItemOddsView() { InitializeComponent(); }

        public WildHeldItemOddsView(WildHeldItemOddsViewModel vm) : this() { DataContext = vm; }

        private async void Save_Click(object sender, RoutedEventArgs e) { if (VM != null) await VM.SaveChangesAsync(); }
        private void Discard_Click(object sender, RoutedEventArgs e) => VM?.DiscardChanges();
    }
}
