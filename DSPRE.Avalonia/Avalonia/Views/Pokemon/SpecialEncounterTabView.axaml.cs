using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views.Pokemon
{
    public partial class SpecialEncounterTabView : UserControl
    {
        private SpecialEncounterTabViewModel VM => DataContext as SpecialEncounterTabViewModel;

        public SpecialEncounterTabView()
        {
            InitializeComponent();
            TabSetup.OnFirstShow(this, owner => VM?.SetupAsync(owner) ?? Task.CompletedTask);
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
            => await DialogHelper.GuardAsync(() => VM?.SaveChangesAsync() ?? Task.FromResult(true), $"The {VM?.UnsavedChangesDescription} weren't saved.", ErrorTitle, TopLevel.GetTopLevel(this) as Window);
        private async void Discard_Click(object sender, RoutedEventArgs e)
        {
            if (await DialogHelper.ConfirmDiscardAsync(this)) VM?.DiscardChanges();
        }
        private async void Export_Click(object sender, RoutedEventArgs e) => await DialogHelper.GuardAsync(() => VM?.ExportAsync() ?? Task.CompletedTask, $"The {VM?.UnsavedChangesDescription} weren't exported.", ErrorTitle, TopLevel.GetTopLevel(this) as Window);
        private async void Import_Click(object sender, RoutedEventArgs e) => await DialogHelper.GuardAsync(() => VM?.ImportAsync() ?? Task.CompletedTask, $"The {VM?.UnsavedChangesDescription} weren't imported.", ErrorTitle, TopLevel.GetTopLevel(this) as Window);
        private void Locate_Click(object sender, RoutedEventArgs e) => VM?.Locate();

        private const string ErrorTitle = "Special encounters editor";
    }
}
