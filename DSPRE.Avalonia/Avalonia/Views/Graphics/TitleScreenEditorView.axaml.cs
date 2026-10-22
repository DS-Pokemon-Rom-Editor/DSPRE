using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using DSPRE.Avalonia.ViewModels;
using System.Collections.Generic;

namespace DSPRE.Avalonia.Views.Graphics
{
    public partial class TitleScreenEditorView : UserControl
    {
        private Window Owner => TopLevel.GetTopLevel(this) as Window;

        private readonly TitleScreenEditorViewModel _vm;
        private readonly Image _logoOverlay;

        private static readonly FilePickerFileType NclrFilter =
            new("Colours (.nclr)") { Patterns = new[] { "*.nclr", "*.bin" } };

        public TitleScreenEditorView()
        {
            AvaloniaXamlLoader.Load(this);
            _logoOverlay = this.FindControl<Image>("LogoOverlay");
            _vm = new TitleScreenEditorViewModel();
            DataContext = _vm;
        }

        private async void ImportPiece_Click(object sender, RoutedEventArgs e) =>
            await ImportPngAsync(_vm.PieceIndex switch { 0 => _vm.ImportBackground, 2 => _vm.ImportCopyright, _ => _vm.ImportLogo });

        private async void ExportPiece_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.PieceIndex == 0) await ExportPngAsync(_vm.ExportBackground, "background.png");
            else if (_vm.PieceIndex == 2) await ExportPngAsync(_vm.ExportCopyright, "copyright.png");
            else await ExportPngAsync(_vm.ExportLogo, "logo.png");
        }

        private async System.Threading.Tasks.Task ImportPngAsync(System.Func<string, string> import)
        {
            IReadOnlyList<IStorageFile> files = await TopLevel.GetTopLevel(this).StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
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
                await DialogHelper.ShowErrorAsync($"Import failed: {error}", "Title screen editor error", Owner);
            else SaveNotice.Imported(null, path);
        }

        private async System.Threading.Tasks.Task ExportPngAsync(System.Func<string, string> export, string suggestedName)
        {
            IStorageFile file = await TopLevel.GetTopLevel(this).StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
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
                await DialogHelper.ShowErrorAsync($"Export failed: {error}", "Title screen editor error", Owner);
            else SaveNotice.Exported(path);
        }

        private async void ImportPalette_Click(object sender, RoutedEventArgs e)
        {
            IReadOnlyList<IStorageFile> files = await TopLevel.GetTopLevel(this).StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Import colours",
                AllowMultiple = false,
                FileTypeFilter = new List<FilePickerFileType> { NclrFilter, DialogHelper.AllFilter }
            });
            if (files.Count == 0) return;
            string path = files[0].TryGetLocalPath();
            if (path == null) return;

            string error = _vm.ImportPalette(path);
            if (error != null)
                await DialogHelper.ShowErrorAsync($"Import failed: {error}", "Title screen editor error", Owner);
            else SaveNotice.Imported(null, path);
        }

        private void Save_Click(object sender, RoutedEventArgs e) => _vm.SaveChanges();

        private async void RevertChanges_Click(object sender, RoutedEventArgs e)
        {
            if (!await DialogHelper.ConfirmDiscardAsync(this)) return;
            _vm.RevertChanges();
        }

        private async void ExportPalette_Click(object sender, RoutedEventArgs e)
        {
            IStorageFile file = await TopLevel.GetTopLevel(this).StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export colours",
                DefaultExtension = "nclr",
                SuggestedFileName = "title_palette.nclr",
                FileTypeChoices = new List<FilePickerFileType> { NclrFilter }
            });
            string path = file?.TryGetLocalPath();
            if (path == null) return;

            string error = _vm.ExportPalette(path);
            if (error != null)
                await DialogHelper.ShowErrorAsync($"Export failed: {error}", "Title screen editor error", Owner);
            else SaveNotice.Exported(path);
        }

        // The screen is drawn at twice size, so a DS pixel offset is doubled here.
        private const double PreviewScale = 2.0;

        /// <summary>
        /// Steps through the same integer frame counter as the game's logo entrance: a 3-frame delay, then
        /// 31 frames where the logo's alpha climbs by 1/31 per frame while its vertical offset shrinks
        /// from 15px to 0, i.e. it starts invisible, slightly below its resting spot, and fades/rises up
        /// into place, rather than a smooth tween.
        /// </summary>
        private async void PlayIntro_Click(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;
            button.IsEnabled = false;
            try
            {
                TranslateTransform transform = (global::Avalonia.Media.TranslateTransform)_logoOverlay.RenderTransform;
                _logoOverlay.Opacity = 0;
                transform.Y = 15 * PreviewScale;

                int blendWait = 0, blend = 0;
                while (blend < 31)
                {
                    blendWait++;
                    if (blendWait > 3)
                    {
                        blend = System.Math.Min(31, blend + 1);
                        _logoOverlay.Opacity = blend / 31.0;
                        transform.Y = ((31 - blend) / 2) * PreviewScale;
                    }
                    await System.Threading.Tasks.Task.Delay(16); // ~60fps, matching the NDS's own frame rate
                }
                _logoOverlay.Opacity = 1;
                transform.Y = 0;
            }
            finally { button.IsEnabled = true; }
        }
    }
}
