using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using DSPRE.Avalonia.ViewModels.World;

namespace DSPRE.Avalonia.Views.World
{
    public partial class MapImportReviewView : Window
    {
        public MapImportReviewView() => AvaloniaXamlLoader.Load(this);

        public MapImportReviewView(MapImportReviewViewModel vm) : this() => DataContext = vm;

        private MapImportReviewViewModel VM => DataContext as MapImportReviewViewModel;

        private async void FindPacks_Click(object sender, RoutedEventArgs e)
        {
            if (VM != null) await VM.FindPacksAsync();
        }

        private void UsePack_Click(object sender, RoutedEventArgs e) =>
            VM?.UsePack((sender as Control)?.Tag as MapImportReviewViewModel.PackRow);

        private void FixSelected_Click(object sender, RoutedEventArgs e) => VM?.FixSelectedEvents();

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
