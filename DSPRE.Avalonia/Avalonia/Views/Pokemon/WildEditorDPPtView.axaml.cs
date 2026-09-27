using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views.Pokemon
{
    /// <summary>Authored as a <see cref="UserControl"/> so it can be embedded as the Encounters tab in
    /// the Maps workspace; standalone launches host it in an <see cref="EditorHostWindow"/> (which calls
    /// <see cref="WildEditorDPPtViewModel.Detach"/> on close, see AvaloniaEditorLauncher.OpenWildEditor).</summary>
    public partial class WildEditorDPPtView : UserControl
    {
        private WildEditorDPPtViewModel ViewModel => (WildEditorDPPtViewModel)DataContext;

        public WildEditorDPPtView(WildEditorDPPtViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
            => await ViewModel.SaveCommand();

        private void Discard_Click(object sender, RoutedEventArgs e) => ViewModel.DiscardChanges();

        private void AddFile_Click(object sender, RoutedEventArgs e) => ViewModel.AddEncounterFile();
        private async void RemFile_Click(object sender, RoutedEventArgs e) => await ViewModel.RemoveLastEncounterFileAsync();
        private async void RepairAll_Click(object sender, RoutedEventArgs e) => await ViewModel.RepairAllAsync();
        private void SlotOdds_Click(object sender, RoutedEventArgs e) => AvaloniaEditorLauncher.OpenEncounterSlotOdds();

        // Coming back from the Slot Odds editor shows the new chances in the slot labels.
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

        private void Window_Activated(object sender, System.EventArgs e) => (DataContext as WildEditorDPPtViewModel)?.RefreshSlotLabels();

        private async void ImportFile_Click(object sender, RoutedEventArgs e)
        {
            var filter = new FilePickerFileType("Wild encounters") { Patterns = new[] { "*.wld", "*.bin", "*.*" } };
            string path = await DialogHelper.OpenFile(TopLevel.GetTopLevel(this) as Window, "Import encounter file", new[] { filter });
            if (path == null) return;
            try { ViewModel.ImportEncounterFile(path); } catch (Exception ex) { await DialogHelper.ShowError($"Import failed:\n{ex.Message}", "Import Error"); }
        }

        private async void ExportFile_Click(object sender, RoutedEventArgs e)
        {
            var filter = new FilePickerFileType("Wild encounters") { Patterns = new[] { "*.wld" } };
            string path = await DialogHelper.SaveFile(TopLevel.GetTopLevel(this) as Window, "Export encounter file", new[] { filter }, $"encounters_{ViewModel.SelectedEncounterIndex:D4}.wld");
            if (path == null) return;
            try { ViewModel.ExportEncounterFile(path); } catch (Exception ex) { await DialogHelper.ShowError($"Export failed:\n{ex.Message}", "Export Error"); }
        }
    }
}
