using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels.Pokemon;

namespace DSPRE.Avalonia.Views.Pokemon
{
    public partial class EncounterSlotOddsView : UserControl
    {
        private EncounterSlotOddsViewModel VM => DataContext as EncounterSlotOddsViewModel;

        public EncounterSlotOddsView() { InitializeComponent(); }

        private void OpenHooks_Click(object sender, RoutedEventArgs e) => AvaloniaEditorLauncher.OpenHgEngineHooks(VM?.FirstMislabelledHook);

        private async void HookFix_Click(object sender, RoutedEventArgs e)
        {
            TopLevel top = TopLevel.GetTopLevel(this);
            if (top != null) await top.Launcher.LaunchUriAsync(new System.Uri(DSPRE.HgEngine.HgEngineSlotOdds.HookFixUrl));
        }

        public EncounterSlotOddsView(EncounterSlotOddsViewModel vm) : this() { DataContext = vm; }

        private async void Save_Click(object sender, RoutedEventArgs e) { if (VM != null) await VM.SaveChangesAsync(); }
        private async void Discard_Click(object sender, RoutedEventArgs e)
        {
            if (await DialogHelper.ConfirmDiscardAsync(this)) VM?.DiscardChanges();
        }
    }
}
