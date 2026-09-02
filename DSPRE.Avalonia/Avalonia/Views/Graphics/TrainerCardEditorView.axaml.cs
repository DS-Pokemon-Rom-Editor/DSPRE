using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using DSPRE.Avalonia.ViewModels;
using System.Collections.Generic;

namespace DSPRE.Avalonia.Views.Graphics
{
    public partial class TrainerCardEditorView : Window
    {
        private readonly TrainerCardEditorViewModel _vm;

        private static readonly FilePickerFileType NclrFilter =
            new("NCLR Palette") { Patterns = new[] { "*.nclr", "*.bin" } };

        public TrainerCardEditorView()
        {
            AvaloniaXamlLoader.Load(this);
            _vm = new TrainerCardEditorViewModel();
            DataContext = _vm;
        }

        private async void ImportCardFront_Click(object sender, RoutedEventArgs e) => await ImportPng(_vm.ImportCardFront);
        private async void ImportCardBack_Click(object sender, RoutedEventArgs e) => await ImportPng(_vm.ImportCardBack);
        private async void ImportTrainerMale_Click(object sender, RoutedEventArgs e) => await ImportPng(_vm.ImportTrainerMale);
        private async void ImportTrainerFemale_Click(object sender, RoutedEventArgs e) => await ImportPng(_vm.ImportTrainerFemale);

        private async void ExportCardFront_Click(object sender, RoutedEventArgs e) => await ExportPng(_vm.ExportCardFront, "card_front.png");
        private async void ExportCardBack_Click(object sender, RoutedEventArgs e) => await ExportPng(_vm.ExportCardBack, "card_back.png");
        private async void ExportTrainerMale_Click(object sender, RoutedEventArgs e) => await ExportPng(_vm.ExportTrainerMale, "trainer_male.png");
        private async void ExportTrainerFemale_Click(object sender, RoutedEventArgs e) => await ExportPng(_vm.ExportTrainerFemale, "trainer_female.png");

        private async System.Threading.Tasks.Task ImportPng(System.Func<string, string> import)
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Import Image",
                AllowMultiple = false,
                FileTypeFilter = new List<FilePickerFileType> { DialogHelper.PngFilter }
            });
            if (files.Count == 0) return;
            string path = files[0].TryGetLocalPath();
            if (path == null) return;

            string error = import(path);
            if (error != null)
                await DialogHelper.ShowError($"Import failed: {error}", "Import Error", this);
        }

        private async System.Threading.Tasks.Task ExportPng(System.Func<string, string> export, string suggestedName)
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export Image",
                DefaultExtension = "png",
                SuggestedFileName = suggestedName,
                FileTypeChoices = new List<FilePickerFileType> { DialogHelper.PngFilter }
            });
            string path = file?.TryGetLocalPath();
            if (path == null) return;

            string error = export(path);
            if (error != null)
                await DialogHelper.ShowError($"Export failed: {error}", "Export Error", this);
        }

        private async void ImportRankPalette_Click(object sender, RoutedEventArgs e)
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Import Palette",
                AllowMultiple = false,
                FileTypeFilter = new List<FilePickerFileType> { NclrFilter, DialogHelper.AllFilter }
            });
            if (files.Count == 0) return;
            string path = files[0].TryGetLocalPath();
            if (path == null) return;

            string error = _vm.ImportRankPalette(path);
            if (error != null)
                await DialogHelper.ShowError($"Import failed: {error}", "Import Error", this);
        }

        private async void ExportRankPalette_Click(object sender, RoutedEventArgs e)
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export Palette",
                DefaultExtension = "nclr",
                SuggestedFileName = "rank_palette.nclr",
                FileTypeChoices = new List<FilePickerFileType> { NclrFilter }
            });
            string path = file?.TryGetLocalPath();
            if (path == null) return;

            string error = _vm.ExportRankPalette(path);
            if (error != null)
                await DialogHelper.ShowError($"Export failed: {error}", "Export Error", this);
        }
        /// <summary>Hands the card to the Graphics window, where its drawing, its two arrangements and
        /// every rank's colours are one row.</summary>
        private void OpenInGraphics_Click(object sender, RoutedEventArgs e)
        {
            int drawing = Data.GraphicUnits.TrainerCardDrawing();
            if (drawing < 0)
            {
                _ = DialogHelper.ShowInfo("This game does not lay its trainer card out in a way DSPRE knows.",
                                          "Open in Graphics");
                return;
            }
            AvaloniaEditorLauncher.OpenGraphicAt(DSPRE.RomInfo.DirNames.trainerCardGraphics, drawing);
        }


        private async void RevertChanges_Click(object sender, RoutedEventArgs e)
        {
            bool ok = await DialogHelper.AskYesNo(
                "Revert all trainer card changes made in this session back to what they were when this editor was opened?",
                "Revert Changes", this);
            if (!ok) return;
            _vm.RevertChanges();
        }
    }
}
