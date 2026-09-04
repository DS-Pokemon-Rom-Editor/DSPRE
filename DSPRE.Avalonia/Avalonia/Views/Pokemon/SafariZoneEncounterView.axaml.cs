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

        private async void Save_Click(object sender, RoutedEventArgs e) => await Safe(VM?.SaveAsync());
        private async void SaveAs_Click(object sender, RoutedEventArgs e) => await Safe(VM?.SaveAsAsync());
        private async void Import_Click(object sender, RoutedEventArgs e) => await Safe(VM?.ImportAsync());

        private static async Task Safe(Task task)
        {
            if (task == null) return;
            // The view models report their own failures; log anything that still gets this far so a
            // handler that stops reporting leaves a trace instead of going quiet.
            try { await task; }
            catch (System.Exception ex) { AppLogger.Warn("Safari Zone editor handler failed: " + ex.Message); }
        }
    }
}
