using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using DSPRE.Avalonia.ViewModels;
using System;
using System.Collections.Generic;

namespace DSPRE.Avalonia.Views.Graphics
{
    public partial class DungeonCutinEditorView : Window
    {
        private DungeonCutinEditorViewModel _vm;

        public DungeonCutinEditorView(List<string> headerNames)
        {
            AvaloniaXamlLoader.Load(this);
            _vm = new DungeonCutinEditorViewModel(headerNames);
            DataContext = _vm;
            // Members imported and then undone stay in the archive until a Discard, so a clean close drops them.
            EditorWindowChrome.Attach(this, _vm, title: () => _vm.Title, onClosed: () => _vm.DropUnsavedImports());
        }

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
        /// <summary>Hands the picked splash screen to All graphics on its noon drawing, which most locations
        /// reuse for the other times of day.</summary>
        private void OpenInGraphics_Click(object sender, RoutedEventArgs e)
        {
            DungeonCutinEditorViewModel vm = DataContext as DSPRE.Avalonia.ViewModels.Graphics.DungeonCutinEditorViewModel;
            int row = vm?.SelectedRow?.RowNumber ?? 0;
            int drawing = row > 0
                ? DSPRE.ROMFiles.DungeonCutinTable.DrawingFor(row, DSPRE.ROMFiles.DungeonCutinTable.TimeOfDay.Noon) : -1;
            if (drawing < 0)
            {
                _ = DialogHelper.ShowInfoAsync("Pick a splash screen first.", "Open in All graphics");
                return;
            }
            AvaloniaEditorLauncher.OpenGraphicAt(DSPRE.RomInfo.DirNames.DungeonCutinGraphics, drawing);
        }


        private async void Discard_Click(object sender, RoutedEventArgs e)
        {
            if (await DialogHelper.ConfirmDiscardAsync(this)) (DataContext as DungeonCutinEditorViewModel)?.DiscardChanges();
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
            => await _vm.SaveCommandAsync();

        private async void Export_Click(object sender, RoutedEventArgs e)
        {
            IStorageFile file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export dungeon cut-in table",
                DefaultExtension = "csv",
                SuggestedFileName = "dungeon_cutin.csv",
                FileTypeChoices = new List<FilePickerFileType> { DialogHelper.CsvFilter }
            });
            string path = file?.TryGetLocalPath();
            if (path == null) return;

            string error = await _vm.ExportCsvAsync(path);
            if (error != null)
                await DialogHelper.ShowErrorAsync($"Couldn't export: {error}", "Dungeon cut-in editor", this);
            else SaveNotice.Exported(path);
        }

        private async void Import_Click(object sender, RoutedEventArgs e)
        {
            IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Import dungeon cutin table",
                AllowMultiple = false,
                FileTypeFilter = new List<FilePickerFileType> { DialogHelper.CsvFilter }
            });
            if (files.Count == 0) return;
            string path = files[0].TryGetLocalPath();
            if (path == null) return;

            await _vm.ImportCsvAsync(this, path);
        }

        private async void ImportMorning_Click(object sender, RoutedEventArgs e) => await ImportTimezoneAsync(DungeonCutinTimezone.Morning);
        private async void ImportNoon_Click(object sender, RoutedEventArgs e) => await ImportTimezoneAsync(DungeonCutinTimezone.Noon);
        private async void ImportEvening_Click(object sender, RoutedEventArgs e) => await ImportTimezoneAsync(DungeonCutinTimezone.Evening);
        private async void ImportNight_Click(object sender, RoutedEventArgs e) => await ImportTimezoneAsync(DungeonCutinTimezone.Night);

        private async void ExportMorning_Click(object sender, RoutedEventArgs e) => await ExportTimezoneAsync(DungeonCutinTimezone.Morning);
        private async void ExportNoon_Click(object sender, RoutedEventArgs e) => await ExportTimezoneAsync(DungeonCutinTimezone.Noon);
        private async void ExportEvening_Click(object sender, RoutedEventArgs e) => await ExportTimezoneAsync(DungeonCutinTimezone.Evening);
        private async void ExportNight_Click(object sender, RoutedEventArgs e) => await ExportTimezoneAsync(DungeonCutinTimezone.Night);

        private async System.Threading.Tasks.Task ImportTimezoneAsync(DungeonCutinTimezone tz)
        {
            if (_vm.SelectedRow == null) { await DialogHelper.ShowErrorAsync("Select a row first.", "Dungeon cut-in editor", this); return; }

            IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Import image",
                AllowMultiple = false,
                FileTypeFilter = new List<FilePickerFileType> { DialogHelper.PngFilter }
            });
            if (files.Count == 0) return;
            string path = files[0].TryGetLocalPath();
            if (path == null) return;

            string error = _vm.ImportTimezoneImage(tz, path);
            if (error != null)
                await DialogHelper.ShowErrorAsync($"Couldn't import: {error}", "Dungeon cut-in editor", this);
            else SaveNotice.Imported($"the {tz.ToString().ToLowerInvariant()} picture", path);
        }

        private async System.Threading.Tasks.Task ExportTimezoneAsync(DungeonCutinTimezone tz)
        {
            if (_vm.SelectedRow == null) { await DialogHelper.ShowErrorAsync("Select a row first.", "Dungeon cut-in editor", this); return; }

            IStorageFile file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export image",
                DefaultExtension = "png",
                SuggestedFileName = $"{tz}.png",
                FileTypeChoices = new List<FilePickerFileType> { DialogHelper.PngFilter }
            });
            string path = file?.TryGetLocalPath();
            if (path == null) return;

            string error = _vm.ExportTimezoneImage(tz, path);
            if (error != null)
                await DialogHelper.ShowErrorAsync($"Couldn't export: {error}", "Dungeon cut-in editor", this);
            else SaveNotice.Exported(path);
        }
    }
}
