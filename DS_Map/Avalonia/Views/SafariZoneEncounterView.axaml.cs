using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views
{
    public partial class SafariZoneEncounterView : UserControl
    {
        private SafariZoneEncounterViewModel VM => DataContext as SafariZoneEncounterViewModel;

        public SafariZoneEncounterView()
        {
            InitializeComponent();
        }

        private void Save_Click(object sender, RoutedEventArgs e) => VM?.Save();
        private async void SaveAs_Click(object sender, RoutedEventArgs e) => await Safe(VM?.SaveAsAsync());
        private async void Import_Click(object sender, RoutedEventArgs e) => await Safe(VM?.ImportAsync());

        private static async Task Safe(Task task)
        {
            if (task == null) return;
            try { await task; } catch { /* handled in VM */ }
        }
    }
}
