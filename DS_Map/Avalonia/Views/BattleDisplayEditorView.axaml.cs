using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using DSPRE.Avalonia.ViewModels;
using System.Collections.Generic;

namespace DSPRE.Avalonia.Views
{
    public partial class BattleDisplayEditorView : UserControl
    {
        public BattleDisplayEditorView()
        {
            InitializeComponent();
        }

        private async void ImportIcon_Click(object sender, RoutedEventArgs e)
        {
            var top = TopLevel.GetTopLevel(this);
            if (top == null || VM == null) return;

            var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Import Icon PNG",
                AllowMultiple = false,
                FileTypeFilter = new List<FilePickerFileType> { new FilePickerFileType("PNG Image") { Patterns = new[] { "*.png" } } }
            });
            if (files.Count == 0) return;
            string path = files[0].TryGetLocalPath();
            if (path == null) return;

            DSPRE.RawImage imported;
            try
            {
                using var fs = System.IO.File.OpenRead(path);
                imported = DSPRE.Avalonia.ImageConverter.DecodeRawImage(fs);
            }
            catch (System.Exception ex) { await DialogHelper.ShowError($"Could not read the image: {ex.Message}"); return; }
            if (imported == null) { await DialogHelper.ShowError("Image could not be decoded."); return; }

            string error = VM.ImportIconGraphic(imported);
            if (error != null) await DialogHelper.ShowError($"Import failed: {error}");
        }

        private async void ExportIcon_Click(object sender, RoutedEventArgs e)
        {
            var top = TopLevel.GetTopLevel(this);
            if (top == null || VM == null) return;

            var raw = VM.ExportIconGraphic();
            if (raw == null) { await DialogHelper.ShowError("Export failed: nothing to export."); return; }

            var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export Icon PNG",
                DefaultExtension = "png",
                FileTypeChoices = new List<FilePickerFileType> { new FilePickerFileType("PNG Image") { Patterns = new[] { "*.png" } } }
            });
            if (file == null) return;
            string path = file.TryGetLocalPath();
            if (path == null) return;

            try { DSPRE.Avalonia.ImageConverter.ToAvaloniaBitmap(raw).Save(path); }
            catch (System.Exception ex) { await DialogHelper.ShowError($"Export failed: {ex.Message}"); }
        }

        private void PlayProgramAnim_Click(object sender, RoutedEventArgs e)
            => (DataContext as BattleDisplayEditorViewModel)?.ToggleProgramAnim();

        private BattleDisplayEditorViewModel VM => DataContext as BattleDisplayEditorViewModel;
        private static ProgramCmdRow Row(object sender) => (sender as Control)?.DataContext as ProgramCmdRow;

        private void AddProgramCmd_Click(object sender, RoutedEventArgs e) => VM?.AddProgramCmd();
        private void SaveProgramScript_Click(object sender, RoutedEventArgs e) => VM?.SaveProgramScript();
        private void ProgramCmdUp_Click(object sender, RoutedEventArgs e) { var r = Row(sender); if (r != null) VM?.MoveProgramCmd(r, -1); }
        private void ProgramCmdDown_Click(object sender, RoutedEventArgs e) { var r = Row(sender); if (r != null) VM?.MoveProgramCmd(r, 1); }
        private void ProgramCmdRemove_Click(object sender, RoutedEventArgs e) { var r = Row(sender); if (r != null) VM?.RemoveProgramCmd(r); }

        private void AddAnimStep_Click(object sender, RoutedEventArgs e)
            => (DataContext as BattleDisplayEditorViewModel)?.AddAnimStep();

        private void RemoveAnimStep_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Control c && c.DataContext is AnimPatternStep step)
                (DataContext as BattleDisplayEditorViewModel)?.RemoveAnimStep(step);
        }
    }
}
