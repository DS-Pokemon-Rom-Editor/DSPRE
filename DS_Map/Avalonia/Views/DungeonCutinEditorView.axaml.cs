using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using DSPRE.Avalonia.ViewModels;
using System;
using System.Collections.Generic;

namespace DSPRE.Avalonia.Views
{
    public partial class DungeonCutinEditorView : Window
    {
        private DungeonCutinEditorViewModel _vm;

        public DungeonCutinEditorView(List<string> headerNames)
        {
            AvaloniaXamlLoader.Load(this);
            _vm = new DungeonCutinEditorViewModel(headerNames);
            DataContext = _vm;
            EditorWindowChrome.Attach(this, _vm, manageTitle: false);
        }

        // Parameterless constructor for previewer only
        public DungeonCutinEditorView()
        {
            AvaloniaXamlLoader.Load(this);
            if (Design.IsDesignMode)
            {
                _vm = new DungeonCutinEditorViewModel();
                DataContext = _vm;
                return;
            }
            throw new InvalidOperationException("Parameterless constructor only for design time.");
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
            => await _vm.SaveCommand();

        private async void Export_Click(object sender, RoutedEventArgs e)
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export Dungeon Cutin Table",
                DefaultExtension = "csv",
                SuggestedFileName = "dungeon_cutin.csv",
                FileTypeChoices = new List<FilePickerFileType> { DialogHelper.CsvFilter }
            });
            string path = file?.TryGetLocalPath();
            if (path == null) return;

            string error = await _vm.ExportCsvAsync(path);
            if (error != null)
                await DialogHelper.ShowError($"Export failed: {error}", "Export Error", this);
        }

        private async void Import_Click(object sender, RoutedEventArgs e)
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Import Dungeon Cutin Table",
                AllowMultiple = false,
                FileTypeFilter = new List<FilePickerFileType> { DialogHelper.CsvFilter }
            });
            if (files.Count == 0) return;
            string path = files[0].TryGetLocalPath();
            if (path == null) return;

            string error = await _vm.ImportCsvAsync(path);
            if (error != null)
                await DialogHelper.ShowError($"Import failed: {error}", "Import Error", this);
        }

        private async void ImportMorning_Click(object sender, RoutedEventArgs e) => await ImportTimezone(DungeonCutinTimezone.Morning);
        private async void ImportNoon_Click(object sender, RoutedEventArgs e) => await ImportTimezone(DungeonCutinTimezone.Noon);
        private async void ImportEvening_Click(object sender, RoutedEventArgs e) => await ImportTimezone(DungeonCutinTimezone.Evening);
        private async void ImportNight_Click(object sender, RoutedEventArgs e) => await ImportTimezone(DungeonCutinTimezone.Night);

        private async void ExportMorning_Click(object sender, RoutedEventArgs e) => await ExportTimezone(DungeonCutinTimezone.Morning);
        private async void ExportNoon_Click(object sender, RoutedEventArgs e) => await ExportTimezone(DungeonCutinTimezone.Noon);
        private async void ExportEvening_Click(object sender, RoutedEventArgs e) => await ExportTimezone(DungeonCutinTimezone.Evening);
        private async void ExportNight_Click(object sender, RoutedEventArgs e) => await ExportTimezone(DungeonCutinTimezone.Night);

        private async System.Threading.Tasks.Task ImportTimezone(DungeonCutinTimezone tz)
        {
            if (_vm.SelectedRow == null) { await DialogHelper.ShowError("Select a row first.", "Import Error", this); return; }

            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Import Image",
                AllowMultiple = false,
                FileTypeFilter = new List<FilePickerFileType> { DialogHelper.PngFilter }
            });
            if (files.Count == 0) return;
            string path = files[0].TryGetLocalPath();
            if (path == null) return;

            string error = _vm.ImportTimezoneImage(tz, path);
            if (error != null)
                await DialogHelper.ShowError($"Import failed: {error}", "Import Error", this);
        }

        private async System.Threading.Tasks.Task ExportTimezone(DungeonCutinTimezone tz)
        {
            if (_vm.SelectedRow == null) { await DialogHelper.ShowError("Select a row first.", "Export Error", this); return; }

            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export Image",
                DefaultExtension = "png",
                SuggestedFileName = $"{tz}.png",
                FileTypeChoices = new List<FilePickerFileType> { DialogHelper.PngFilter }
            });
            string path = file?.TryGetLocalPath();
            if (path == null) return;

            string error = _vm.ExportTimezoneImage(tz, path);
            if (error != null)
                await DialogHelper.ShowError($"Export failed: {error}", "Export Error", this);
        }
    }
}
