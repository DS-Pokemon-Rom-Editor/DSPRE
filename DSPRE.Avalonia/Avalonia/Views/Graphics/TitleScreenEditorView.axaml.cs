using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using DSPRE.Avalonia.ViewModels;
using System.Collections.Generic;

namespace DSPRE.Avalonia.Views.Graphics
{
    public partial class TitleScreenEditorView : Window
    {
        private readonly TitleScreenEditorViewModel _vm;
        private readonly Image _logoOverlay;

        private static readonly FilePickerFileType NclrFilter =
            new("NCLR Palette") { Patterns = new[] { "*.nclr", "*.bin" } };

        public TitleScreenEditorView()
        {
            AvaloniaXamlLoader.Load(this);
            _logoOverlay = this.FindControl<Image>("LogoOverlay");
            _vm = new TitleScreenEditorViewModel();
            DataContext = _vm;
        }

        private async void ImportLogo_Click(object sender, RoutedEventArgs e) => await ImportPng(_vm.ImportLogo);
        private async void ImportBackground_Click(object sender, RoutedEventArgs e) => await ImportPng(_vm.ImportBackground);
        private async void ImportCopyright_Click(object sender, RoutedEventArgs e) => await ImportPng(_vm.ImportCopyright);

        private async void ExportLogo_Click(object sender, RoutedEventArgs e) => await ExportPng(_vm.ExportLogo, "logo.png");
        private async void ExportBackground_Click(object sender, RoutedEventArgs e) => await ExportPng(_vm.ExportBackground, "background.png");
        private async void ExportCopyright_Click(object sender, RoutedEventArgs e) => await ExportPng(_vm.ExportCopyright, "copyright.png");

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

        private async void ImportPalette_Click(object sender, RoutedEventArgs e)
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

            string error = _vm.ImportPalette(path);
            if (error != null)
                await DialogHelper.ShowError($"Import failed: {error}", "Import Error", this);
        }

        private async void RevertChanges_Click(object sender, RoutedEventArgs e)
        {
            bool ok = await DialogHelper.AskYesNo(
                "Revert all title screen changes made in this session (either version's logo/background/palette, and the copyright text) back to what they were when this editor was opened?",
                "Revert Changes", this);
            if (!ok) return;
            _vm.RevertChanges();
        }

        private async void ExportPalette_Click(object sender, RoutedEventArgs e)
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export Palette",
                DefaultExtension = "nclr",
                SuggestedFileName = "title_palette.nclr",
                FileTypeChoices = new List<FilePickerFileType> { NclrFilter }
            });
            string path = file?.TryGetLocalPath();
            if (path == null) return;

            string error = _vm.ExportPalette(path);
            if (error != null)
                await DialogHelper.ShowError($"Export failed: {error}", "Export Error", this);
        }

        // Preview is drawn at 1.5x (384px wide standing in for the real 256px), so a real DS pixel offset
        // has to be scaled by the same factor to look correct here.
        private const double PreviewScale = 384.0 / 256.0;

        /// <summary>
        /// Steps through the same integer frame counter as title.c's TitleLogoMove: a 3-frame delay, then
        /// 31 frames where the logo's alpha climbs by 1/31 per frame while its vertical offset shrinks
        /// from 15px to 0, i.e. it starts invisible, slightly below its resting spot, and fades/rises up
        /// into place, rather than a smooth tween.
        /// </summary>
        private async void PlayIntro_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            button.IsEnabled = false;
            try
            {
                var transform = (global::Avalonia.Media.TranslateTransform)_logoOverlay.RenderTransform;
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
