using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views.Pokemon
{
    public partial class GreatMarshEncounterView : UserControl
    {
        private GreatMarshEncounterViewModel VM => DataContext as GreatMarshEncounterViewModel;

        public GreatMarshEncounterView()
        {
            InitializeComponent();
        }

        private void Save_Click(object sender, RoutedEventArgs e) => VM?.Save();
        private async void Discard_Click(object sender, RoutedEventArgs e)
        {
            if (await DialogHelper.ConfirmDiscardAsync(this)) VM?.DiscardChanges();
        }
        private async void Export_Click(object sender, RoutedEventArgs e) => await DialogHelper.GuardAsync(() => VM?.ExportAsync() ?? Task.CompletedTask, "The Great Marsh encounters weren't exported.", ErrorTitle);
        private async void Import_Click(object sender, RoutedEventArgs e) => await DialogHelper.GuardAsync(() => VM?.ImportAsync() ?? Task.CompletedTask, "The Great Marsh encounters weren't imported.", ErrorTitle);
        private void Locate_Click(object sender, RoutedEventArgs e) => VM?.Locate();

        private const string ErrorTitle = "Special encounters editor";
    }
}
