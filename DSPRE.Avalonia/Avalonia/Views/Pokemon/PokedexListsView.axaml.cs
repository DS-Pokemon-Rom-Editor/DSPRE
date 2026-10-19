using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels.Pokemon;

namespace DSPRE.Avalonia.Views.Pokemon
{
    public partial class PokedexListsView : UserControl
    {
        private PokedexListsViewModel VM => DataContext as PokedexListsViewModel;

        public PokedexListsView() { InitializeComponent(); NameRefresh.Bind(this, () => VM?.RefreshNames()); }

        public PokedexListsView(PokedexListsViewModel vm) : this()
        {
            DataContext = vm;
        }

        private void CountPatch_Click(object sender, RoutedEventArgs e) => AvaloniaEditorLauncher.OpenPatchToolboxAt("regionalDexCount");

        private void Say(string message) => Status.Text = message ?? "";

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null || !await VM.SaveChangesAsync()) return;
            Say("Saved. Save ROM puts the lists in the game.");
            // The game's code holds its own count of the regional dex, so a new size is offered as the patch.
            (DSPRE.ROMFiles.RegionalDexCount.Counts Now, DSPRE.ROMFiles.RegionalDexCount.Counts Next, string Problem)? offer = VM.RegionalCountOffer();
            if (offer == null) return;
            (DSPRE.ROMFiles.RegionalDexCount.Counts now, DSPRE.ROMFiles.RegionalDexCount.Counts next, string problem) = offer.Value;
            if (problem != null) { await DialogHelper.ShowErrorAsync(problem, "Regional Pokédex size"); return; }
            await PatchHandover.OfferAsync("regionalDexCount",
                $"The game counts the regional Pokédex complete at {now.Completion} species and the saved order now gives {next.Completion}. Matching them",
                "Regional Pokédex size");
        }

        private async void Discard_Click(object sender, RoutedEventArgs e)
        {
            if (!await DialogHelper.ConfirmDiscardAsync(this)) return;
            VM?.DiscardChanges();
            Say(null);
        }

        private async void Rebuild_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null) return;
            if (!await DialogHelper.AskTwoWayAsync("Build every sort list again from the species' names, types, heights, weights and body shapes? "
                                              + "Changes made to them by hand are replaced. The regional order is kept.",
                                              "Rebuild sort lists", "Rebuild", "Cancel")) return;
            string err = VM.RebuildLists(out bool changed);
            if (err != null) await DialogHelper.ShowErrorAsync(err, "Rebuild sort lists");
            else Say(changed ? "Rebuilt. Save to keep them." : "The sort lists already match the species' data.");
        }

        private void AddRegional_Click(object sender, RoutedEventArgs e) => Say(VM?.AddRegional());
        private void RemoveRegional_Click(object sender, RoutedEventArgs e) { VM?.RemoveRegional(); Say(null); }
        private void RaiseRegional_Click(object sender, RoutedEventArgs e) => VM?.RaiseRegionalEntry();
        private void LowerRegional_Click(object sender, RoutedEventArgs e) => VM?.LowerRegionalEntry();

        private void AddEntry_Click(object sender, RoutedEventArgs e) => Say(VM?.AddEntry());
        private void RemoveEntry_Click(object sender, RoutedEventArgs e) { VM?.RemoveEntry(); Say(null); }
        private void RaiseEntry_Click(object sender, RoutedEventArgs e) => VM?.RaiseEntry();
        private void LowerEntry_Click(object sender, RoutedEventArgs e) => VM?.LowerEntry();
    }
}
