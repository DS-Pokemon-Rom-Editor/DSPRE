using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels.Pokemon;

namespace DSPRE.Avalonia.Views.Pokemon
{
    public partial class EncounterSlotOddsView : UserControl
    {
        private EncounterSlotOddsViewModel VM => DataContext as EncounterSlotOddsViewModel;

        public EncounterSlotOddsView() { InitializeComponent(); }

        public EncounterSlotOddsView(EncounterSlotOddsViewModel vm) : this() { DataContext = vm; }

        private async void Save_Click(object sender, RoutedEventArgs e) { if (VM != null) await VM.SaveChangesAsync(); }
        private void Discard_Click(object sender, RoutedEventArgs e) => VM?.DiscardChanges();
    }
}
