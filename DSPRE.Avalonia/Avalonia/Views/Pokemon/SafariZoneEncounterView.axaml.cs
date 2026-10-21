using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views.Pokemon
{
    public partial class SafariZoneEncounterView : UserControl
    {
        private SafariZoneEncounterViewModel VM => DataContext as SafariZoneEncounterViewModel;

        public SafariZoneEncounterView()
        {
            InitializeComponent();
        }

        private async void Save_Click(object sender, RoutedEventArgs e) => await DialogHelper.GuardAsync(() => VM?.SaveAsync() ?? Task.CompletedTask, "The Safari Zone area wasn't saved.", ErrorTitle);
        private async void SaveAs_Click(object sender, RoutedEventArgs e) => await DialogHelper.GuardAsync(() => VM?.SaveAsAsync() ?? Task.CompletedTask, "The Safari Zone area wasn't saved.", ErrorTitle);
        private async void Import_Click(object sender, RoutedEventArgs e) => await DialogHelper.GuardAsync(() => VM?.ImportAsync() ?? Task.CompletedTask, "The Safari Zone area wasn't imported.", ErrorTitle);

        private const string ErrorTitle = "Special encounters editor error";

        private async void Discard_Click(object sender, RoutedEventArgs e)
        {
            if (await DialogHelper.ConfirmDiscardAsync(this)) (DataContext as DSPRE.Editors.IEditorWithUnsavedChanges)?.DiscardChanges();
        }
    }
}
