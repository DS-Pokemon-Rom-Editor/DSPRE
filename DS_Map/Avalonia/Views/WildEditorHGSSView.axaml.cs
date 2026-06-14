using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views
{
    public partial class WildEditorHGSSView : Window
    {
        private WildEditorHGSSViewModel ViewModel => (WildEditorHGSSViewModel)DataContext;

        public WildEditorHGSSView(WildEditorHGSSViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
            Closing += OnWindowClosing;
        }

        private async void OnWindowClosing(object sender, WindowClosingEventArgs e)
        {
            if (!ViewModel.HasUnsavedChanges) { ViewModel.Detach(); return; }
            e.Cancel = true;
            if (await ViewModel.ConfirmCloseAsync())
            {
                ViewModel.Detach();
                Closing -= OnWindowClosing;
                Close();
            }
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
            => await ViewModel.SaveCommand();

        private void AddFile_Click(object sender, RoutedEventArgs e) => ViewModel.AddEncounterFile();
        private async void RemFile_Click(object sender, RoutedEventArgs e) => await ViewModel.RemoveLastEncounterFileAsync();
        private async void RepairAll_Click(object sender, RoutedEventArgs e) => await ViewModel.RepairAllAsync();

        private async void ImportFile_Click(object sender, RoutedEventArgs e)
        {
            var filter = new FilePickerFileType("Wild encounters") { Patterns = new[] { "*.wld", "*.bin", "*.*" } };
            string path = await DialogHelper.OpenFile(this, "Import encounter file", new[] { filter });
            if (path == null) return;
            try { ViewModel.ImportEncounterFile(path); } catch (Exception ex) { await DialogHelper.ShowError($"Import failed:\n{ex.Message}", "Import Error"); }
        }

        private async void ExportFile_Click(object sender, RoutedEventArgs e)
        {
            var filter = new FilePickerFileType("Wild encounters") { Patterns = new[] { "*.wld" } };
            string path = await DialogHelper.SaveFile(this, "Export encounter file", new[] { filter }, $"encounters_{ViewModel.SelectedEncounterIndex:D4}.wld");
            if (path == null) return;
            try { ViewModel.ExportEncounterFile(path); } catch (Exception ex) { await DialogHelper.ShowError($"Export failed:\n{ex.Message}", "Export Error"); }
        }
    }
}
