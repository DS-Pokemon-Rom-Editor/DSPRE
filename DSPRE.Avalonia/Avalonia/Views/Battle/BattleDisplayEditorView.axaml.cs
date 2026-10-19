using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using DSPRE.Avalonia.ViewModels;
using System.Collections.Generic;
using System.IO;

namespace DSPRE.Avalonia.Views.Battle
{
    public partial class BattleDisplayEditorView : UserControl
    {
        public BattleDisplayEditorView()
        {
            InitializeComponent();
        }

        private async void ImportIcon_Click(object sender, RoutedEventArgs e)
        {
            TopLevel top = TopLevel.GetTopLevel(this);
            if (top == null || VM == null) return;

            IReadOnlyList<IStorageFile> files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Import icon PNG",
                AllowMultiple = false,
                FileTypeFilter = new List<FilePickerFileType> { new FilePickerFileType("PNG Image") { Patterns = new[] { "*.png" } } }
            });
            if (files.Count == 0) return;
            string path = files[0].TryGetLocalPath();
            if (path == null) return;

            DSPRE.RawImage imported;
            try
            {
                using FileStream fs = System.IO.File.OpenRead(path);
                imported = DSPRE.Avalonia.ImageConverter.DecodeRawImage(fs);
            }
            catch (System.Exception ex) { await DialogHelper.ShowErrorAsync($"Could not read the image: {ex.Message}", "Pokémon editor error"); return; }
            if (imported == null) { await DialogHelper.ShowErrorAsync("Image could not be decoded.", "Pokémon editor error"); return; }

            string error = VM.ImportIconGraphic(imported);
            if (error != null) await DialogHelper.ShowErrorAsync($"Import failed: {error}", "Pokémon editor error");
            else SaveNotice.Imported("the icon", path);
        }

        private async void ExportIcon_Click(object sender, RoutedEventArgs e)
        {
            TopLevel top = TopLevel.GetTopLevel(this);
            if (top == null || VM == null) return;

            byte[] indexedPng = VM.FullPaletteExport ? VM.ExportIconGraphicIndexedPng() : null;
            RawImage raw = VM.FullPaletteExport ? null : VM.ExportIconGraphic();
            if (indexedPng == null && raw == null)
            {
                string reason = VM.FullPaletteExport ? "save the icon graphic first (full-palette export needs it on disk)" : "nothing to export";
                await DialogHelper.ShowErrorAsync($"Export failed: {reason}.", "Pokémon editor error");
                return;
            }

            IStorageFile file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export icon PNG",
                DefaultExtension = "png",
                FileTypeChoices = new List<FilePickerFileType> { new FilePickerFileType("PNG Image") { Patterns = new[] { "*.png" } } }
            });
            if (file == null) return;
            string path = file.TryGetLocalPath();
            if (path == null) return;

            try
            {
                if (indexedPng != null) PlainFiles.WriteBytes(path, indexedPng);
                else DSPRE.Avalonia.ImageConverter.ToAvaloniaBitmap(raw).Save(path, PngBitmapEncoderOptions.Default);
                SaveNotice.Exported(path);
            }
            catch (System.Exception ex) { await DialogHelper.ShowErrorAsync($"Export failed: {ex.Message}", "Pokémon editor error"); }
        }

        private void Play_Click(object sender, RoutedEventArgs e) => VM?.ToggleAnimationPlayback();
        private void PlayFrames_Click(object sender, RoutedEventArgs e) => VM?.ToggleFramePlayback();
        private void PlaySendOut_Click(object sender, RoutedEventArgs e) => VM?.ToggleSendOutPlayback();

        private BattleDisplayEditorViewModel VM => DataContext as BattleDisplayEditorViewModel;
        private static ProgramCmdRow Row(object sender) => (sender as Control)?.DataContext as ProgramCmdRow;

        private void AddProgramCmd_Click(object sender, RoutedEventArgs e) => VM?.AddProgramCmd();
        private void SaveProgramScript_Click(object sender, RoutedEventArgs e) => VM?.SaveProgramScript();
        private void ProgramCmdUp_Click(object sender, RoutedEventArgs e) { ProgramCmdRow r = Row(sender); if (r != null) VM?.MoveProgramCmd(r, -1); }
        private void ProgramCmdDown_Click(object sender, RoutedEventArgs e) { ProgramCmdRow r = Row(sender); if (r != null) VM?.MoveProgramCmd(r, 1); }
        private void ProgramCmdRemove_Click(object sender, RoutedEventArgs e) { ProgramCmdRow r = Row(sender); if (r != null) VM?.RemoveProgramCmd(r); }

        private void AddAnimStep_Click(object sender, RoutedEventArgs e)
            => (DataContext as BattleDisplayEditorViewModel)?.AddAnimStep();

        private void RemoveAnimStep_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Control c && c.DataContext is AnimPatternStep step)
                (DataContext as BattleDisplayEditorViewModel)?.RemoveAnimStep(step);
        }
    }
}
