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
            new("Colours (.nclr)") { Patterns = new[] { "*.nclr", "*.bin" } };

        public TrainerCardEditorView()
        {
            AvaloniaXamlLoader.Load(this);
            _vm = new TrainerCardEditorViewModel();
            DataContext = _vm;
            EditorWindowChrome.Attach(this, _vm);
        }

        private async void ImportCardFront_Click(object sender, RoutedEventArgs e) => await ImportPngAsync(_vm.ImportCardFront);
        private async void ImportCardBack_Click(object sender, RoutedEventArgs e) => await ImportPngAsync(_vm.ImportCardBack);
        private async void ImportTrainerMale_Click(object sender, RoutedEventArgs e) => await ImportPngAsync(_vm.ImportTrainerMale);
        private async void ImportTrainerFemale_Click(object sender, RoutedEventArgs e) => await ImportPngAsync(_vm.ImportTrainerFemale);

        private async void ExportCardFront_Click(object sender, RoutedEventArgs e) => await ExportPngAsync(_vm.ExportCardFront, "card_front.png");
        private async void ExportCardBack_Click(object sender, RoutedEventArgs e) => await ExportPngAsync(_vm.ExportCardBack, "card_back.png");
        private async void ExportTrainerMale_Click(object sender, RoutedEventArgs e) => await ExportPngAsync(_vm.ExportTrainerMale, "trainer_male.png");
        private async void ExportTrainerFemale_Click(object sender, RoutedEventArgs e) => await ExportPngAsync(_vm.ExportTrainerFemale, "trainer_female.png");

        private async System.Threading.Tasks.Task ImportPngAsync(System.Func<string, string> import)
        {
            IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Import image",
                AllowMultiple = false,
                FileTypeFilter = new List<FilePickerFileType> { DialogHelper.PngFilter }
            });
            if (files.Count == 0) return;
            string path = files[0].TryGetLocalPath();
            if (path == null) return;

            string error = import(path);
            if (error != null)
                await DialogHelper.ShowErrorAsync($"Import failed: {error}", "Trainer card editor error", this);
            else SaveNotice.Imported(null, path);
        }

        private async System.Threading.Tasks.Task ExportPngAsync(System.Func<string, string> export, string suggestedName)
        {
            IStorageFile file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export image",
                DefaultExtension = "png",
                SuggestedFileName = suggestedName,
                FileTypeChoices = new List<FilePickerFileType> { DialogHelper.PngFilter }
            });
            string path = file?.TryGetLocalPath();
            if (path == null) return;

            string error = export(path);
            if (error != null)
                await DialogHelper.ShowErrorAsync($"Export failed: {error}", "Trainer card editor error", this);
            else SaveNotice.Exported(path);
        }

        private async void ImportRankPalette_Click(object sender, RoutedEventArgs e)
        {
            IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Import colours",
                AllowMultiple = false,
                FileTypeFilter = new List<FilePickerFileType> { NclrFilter, DialogHelper.AllFilter }
            });
            if (files.Count == 0) return;
            string path = files[0].TryGetLocalPath();
            if (path == null) return;

            string error = _vm.ImportRankPalette(path);
            if (error != null)
                await DialogHelper.ShowErrorAsync($"Import failed: {error}", "Trainer card editor error", this);
            else SaveNotice.Imported(null, path);
        }

        private async void ExportRankPalette_Click(object sender, RoutedEventArgs e)
        {
            IStorageFile file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export colours",
                DefaultExtension = "nclr",
                SuggestedFileName = "rank_palette.nclr",
                FileTypeChoices = new List<FilePickerFileType> { NclrFilter }
            });
            string path = file?.TryGetLocalPath();
            if (path == null) return;

            string error = _vm.ExportRankPalette(path);
            if (error != null)
                await DialogHelper.ShowErrorAsync($"Export failed: {error}", "Trainer card editor error", this);
            else SaveNotice.Exported(path);
        }
        /// <summary>Hands the card to the Graphics window, where its drawing, its two arrangements and
        /// every rank's colours are one row.</summary>
        private void OpenInGraphics_Click(object sender, RoutedEventArgs e)
        {
            int drawing = Data.GraphicUnits.TrainerCardDrawing();
            if (drawing < 0)
            {
                _ = DialogHelper.ShowInfoAsync("This game does not lay its trainer card out in a way DSPRE knows.",
                                          "Open in Graphics");
                return;
            }
            AvaloniaEditorLauncher.OpenGraphicAt(DSPRE.RomInfo.DirNames.TrainerCardGraphics, drawing);
        }


        private void Save_Click(object sender, RoutedEventArgs e) => _vm.SaveChanges();

        private async void RevertChanges_Click(object sender, RoutedEventArgs e)
        {
            if (!await DialogHelper.ConfirmDiscardAsync(this)) return;
            _vm.RevertChanges();
        }
    }
}
