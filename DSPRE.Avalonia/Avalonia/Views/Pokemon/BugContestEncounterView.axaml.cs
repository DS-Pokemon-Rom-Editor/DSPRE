using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views.Pokemon
{
    public partial class BugContestEncounterView : UserControl
    {
        private BugContestEncounterViewModel VM => DataContext as BugContestEncounterViewModel;

        public BugContestEncounterView()
        {
            InitializeComponent();
        }

        private void Save_Click(object sender, RoutedEventArgs e) => VM?.Save();
        private async void Export_Click(object sender, RoutedEventArgs e) => await Safe(VM?.ExportAsync());
        private async void Import_Click(object sender, RoutedEventArgs e) => await Safe(VM?.ImportAsync());
        private void Locate_Click(object sender, RoutedEventArgs e) => VM?.Locate();

        private static async Task Safe(Task task)
        {
            if (task == null) return;
            try { await task; } catch { /* handled in VM */ }
        }

        private async void Discard_Click(object sender, RoutedEventArgs e)
        {
            if (await DialogHelper.ConfirmDiscard(this)) (DataContext as DSPRE.Editors.IEditorWithUnsavedChanges)?.DiscardChanges();
        }
    }
}
