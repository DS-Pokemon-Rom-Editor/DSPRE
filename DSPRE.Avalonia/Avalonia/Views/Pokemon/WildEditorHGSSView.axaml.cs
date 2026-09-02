using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views.Pokemon
{
    /// <summary>Authored as a <see cref="UserControl"/> so it can be embedded as the Encounters tab in
    /// the Maps workspace; standalone launches host it in an <see cref="EditorHostWindow"/> (which calls
    /// <see cref="WildEditorHGSSViewModel.Detach"/> on close, see AvaloniaEditorLauncher.OpenWildEditor).</summary>
    public partial class WildEditorHGSSView : UserControl
    {
        private WildEditorHGSSViewModel ViewModel => (WildEditorHGSSViewModel)DataContext;

        public WildEditorHGSSView(WildEditorHGSSViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
            => await ViewModel.SaveCommand();

        private void Undo_Click(object sender, RoutedEventArgs e) => ViewModel.Undo();
        private void Redo_Click(object sender, RoutedEventArgs e) => ViewModel.Redo();

        private void AddFile_Click(object sender, RoutedEventArgs e) => ViewModel.AddEncounterFile();
        private async void RemFile_Click(object sender, RoutedEventArgs e) => await ViewModel.RemoveLastEncounterFileAsync();
        private async void RepairAll_Click(object sender, RoutedEventArgs e) => await ViewModel.RepairAllAsync();

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
