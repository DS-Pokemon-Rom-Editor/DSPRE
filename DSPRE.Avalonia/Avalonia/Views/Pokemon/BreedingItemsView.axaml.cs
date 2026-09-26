using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels.Pokemon;

namespace DSPRE.Avalonia.Views.Pokemon
{
    public partial class BreedingItemsView : UserControl
    {
        private BreedingItemsViewModel VM => DataContext as BreedingItemsViewModel;

        public BreedingItemsView() { InitializeComponent(); }

        public BreedingItemsView(BreedingItemsViewModel vm) : this() { DataContext = vm; }

        private async void Save_Click(object sender, RoutedEventArgs e) { if (VM != null) await VM.SaveChangesAsync(); }
        private void Discard_Click(object sender, RoutedEventArgs e) => VM?.DiscardChanges();
    }
}
