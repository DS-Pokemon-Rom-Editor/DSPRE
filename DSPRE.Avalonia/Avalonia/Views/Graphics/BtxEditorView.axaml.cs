using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using DSPRE.Avalonia.ViewModels;
using DSPRE.Avalonia.ViewModels.Graphics;
using System.Collections.Generic;
using System.Linq;

namespace DSPRE.Avalonia.Views.Graphics
{
    public partial class BtxEditorView : Window
    {
        private BtxEditorViewModel VM => (BtxEditorViewModel)DataContext;

        public BtxEditorView(BtxEditorViewModel vm)
        {
            DataContext = vm;
            InitializeComponent();
            EditorWindowChrome.Attach(this, vm);
        }

        private async void Import_Click(object sender, RoutedEventArgs e)
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Import PNG",
                AllowMultiple = false,
                FileTypeFilter = new List<FilePickerFileType>
                {
                    new FilePickerFileType("PNG Image") { Patterns = new[] { "*.png" } }
                }
            });
            if (files.Count == 0) return;

            string path = files[0].TryGetLocalPath();
            if (path == null) return;

            if (VM == null) return;

            bool needsProfile = VM.TryGetCompatibleProfiles(path, out var profiles, out string analysisError);
            if (analysisError != null)
            {
                await DialogHelper.ShowError($"Import failed: {analysisError}");
                return;
            }

            string error;
            if (needsProfile)
            {
                var pickerVm = new OverworldProfilePickerViewModel(profiles);
                var picker = new OverworldProfilePickerView(pickerVm);
                bool accepted = await picker.ShowDialog<bool>(this);
                if (!accepted || pickerVm.SelectedProfile == null) return;

                string warning = VM.GetSelectedMemberUsageWarning();
                if (warning != null && !await DialogHelper.AskYesNo(warning, "Shared texture slot", this))
                    return;

                error = VM.ImportPngUsingProfile(path, pickerVm.SelectedProfile.AppearanceId);
            }
            else
            {
                string warning = VM.GetSelectedMemberUsageWarning();
                if (warning != null && !await DialogHelper.AskYesNo(warning, "Shared texture slot", this))
                    return;

                error = VM.ImportPng(path);
            }
            if (error != null)
                await DialogHelper.ShowError($"Import failed: {error}");
        }

        private async void Export_Click(object sender, RoutedEventArgs e)
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export PNG",
                DefaultExtension = "png",
                FileTypeChoices = new List<FilePickerFileType>
                {
                    new FilePickerFileType("PNG Image") { Patterns = new[] { "*.png" } }
                }
            });
            if (file == null) return;

            string path = file.TryGetLocalPath();
            if (path == null) return;

            if (VM?.ExportPng(path) == false)
                await DialogHelper.ShowError("Export failed.");
        }

        private void SaveSelected_Click(object sender, RoutedEventArgs e) => VM?.SaveSelected();
        private void SaveAll_Click(object sender, RoutedEventArgs e)      => VM?.SaveAll();

        private void ShowFile_Click(object sender, RoutedEventArgs e)
        {
            string path = VM?.GetCurrentFilePath();
            if (path != null) SystemShell.RevealInFileManager(path);
        }

        private async void AddEntry_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null) return;

            var dlgVm = new AddOverworldEntryViewModel();
            var dlg = new AddOverworldEntryView(dlgVm);
            await dlg.ShowDialog(this);
            if (!dlgVm.Confirmed) return;

            if (dlgVm.SelectedSlot == null || dlgVm.SelectedCloneSource == null)
            {
                await DialogHelper.ShowError("Choose a texture slot and a clone source.");
                return;
            }

            string error = VM.AddEntryWithImage(dlgVm.AppearanceIdText, dlgVm.SelectedSlot.Id, dlgVm.SelectedCloneSource.Id, dlgVm.PngPath, dlgVm.RawBtxPath);
            if (error != null)
                await DialogHelper.ShowError($"Could not add entry: {error}");
        }

        private async void DeleteEntry_Click(object sender, RoutedEventArgs e)
        {
            if (!await DialogHelper.AskYesNo("Delete this custom overworld entry? This cannot be undone.", "Confirm delete", this))
                return;

            string error = VM?.DeleteSelectedEntry();
            if (error != null)
                await DialogHelper.ShowError($"Could not delete entry: {error}");
        }
    }
}
