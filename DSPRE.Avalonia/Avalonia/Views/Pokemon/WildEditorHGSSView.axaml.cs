using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views.Pokemon
{
    public partial class WildEditorHGSSView : UserControl
    {
        private WildEditorHGSSViewModel ViewModel => (WildEditorHGSSViewModel)DataContext;

        public WildEditorHGSSView(WildEditorHGSSViewModel vm)
        {
            InitializeComponent();
            SlotOddsButton.IsVisible = BetaEditors.Allows("EncounterSlotOddsView");
            DataContext = vm;
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
            => await ViewModel.SaveCommandAsync();

        private async void Discard_Click(object sender, RoutedEventArgs e)
        {
            if (await DialogHelper.ConfirmDiscardAsync(this)) ViewModel.DiscardChanges();
        }

        private void AddFile_Click(object sender, RoutedEventArgs e) => ViewModel.AddEncounterFile();
        private async void RemFile_Click(object sender, RoutedEventArgs e) => await ViewModel.RemoveLastEncounterFileAsync();
        private async void RepairAll_Click(object sender, RoutedEventArgs e) => await ViewModel.RepairAllAsync();
        private void SlotOdds_Click(object sender, RoutedEventArgs e) => AvaloniaEditorLauncher.OpenEncounterSlotOdds();

        // Coming back from Encounter slot odds shows the new chances in the slot labels.
        private Window _window;
        protected override void OnAttachedToVisualTree(global::Avalonia.VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            _window = TopLevel.GetTopLevel(this) as Window;
            if (_window != null) _window.Activated += Window_Activated;
        }

        protected override void OnDetachedFromVisualTree(global::Avalonia.VisualTreeAttachmentEventArgs e)
        {
            if (_window != null) _window.Activated -= Window_Activated;
            _window = null;
            base.OnDetachedFromVisualTree(e);
        }

        private void Window_Activated(object sender, System.EventArgs e) => (DataContext as WildEditorHGSSViewModel)?.RefreshSlotLabels();

        private async void ImportFile_Click(object sender, RoutedEventArgs e)
        {
            FilePickerFileType filter = new FilePickerFileType("Wild encounters") { Patterns = new[] { "*.wld", "*.bin", "*.*" } };
            string path = await DialogHelper.OpenFileAsync(TopLevel.GetTopLevel(this) as Window, "Import encounter file", new[] { filter });
            if (path == null) return;
            try { ViewModel.ImportEncounterFile(path); SaveNotice.Imported($"encounter file {ViewModel.SelectedEncounterIndex}", path); }
            catch (Exception ex) { await DialogHelper.ShowErrorAsync($"Couldn't import:\n{ex.Message}", "Wild Pokémon editor"); }
        }

        private async void ExportFile_Click(object sender, RoutedEventArgs e)
        {
            FilePickerFileType filter = new FilePickerFileType("Wild encounters") { Patterns = new[] { "*.wld" } };
            string path = await DialogHelper.SaveFileAsync(TopLevel.GetTopLevel(this) as Window, "Export encounter file", new[] { filter }, $"encounters_{ViewModel.SelectedEncounterIndex:D4}.wld");
            if (path == null) return;
            try { ViewModel.ExportEncounterFile(path); SaveNotice.Exported(path); }
            catch (Exception ex) { await DialogHelper.ShowErrorAsync($"Couldn't export:\n{ex.Message}", "Wild Pokémon editor"); }
        }
    }
}
