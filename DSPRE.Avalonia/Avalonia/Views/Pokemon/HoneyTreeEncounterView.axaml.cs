using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views.Pokemon
{
    public partial class HoneyTreeEncounterView : UserControl
    {
        private HoneyTreeEncounterViewModel VM => DataContext as HoneyTreeEncounterViewModel;

        public HoneyTreeEncounterView()
        {
            InitializeComponent();
        }

        private void Save_Click(object sender, RoutedEventArgs e) => VM?.Save();
        private async void Discard_Click(object sender, RoutedEventArgs e)
        {
            if (await DialogHelper.ConfirmDiscardAsync(this)) VM?.DiscardChanges();
        }
        private async void Export_Click(object sender, RoutedEventArgs e) => await SafeAsync(VM?.ExportAsync());
        private async void Import_Click(object sender, RoutedEventArgs e) => await SafeAsync(VM?.ImportAsync());
        private void Locate_Click(object sender, RoutedEventArgs e) => VM?.Locate();

        private static async Task SafeAsync(Task task)
        {
            if (task == null) return;
            try { await task; } catch { /* handled in VM */ }
        }
    }
}
