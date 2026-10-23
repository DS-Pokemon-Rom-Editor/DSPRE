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
            Closed += (_, _) => vm.StopPreview();
        }

        private async void Import_Click(object sender, RoutedEventArgs e)
        {
            IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Import PNG",
                AllowMultiple = false,
                FileTypeFilter = new List<FilePickerFileType>
                {
                    new FilePickerFileType("PNG image") { Patterns = new[] { "*.png" } }
                }
            });
            if (files.Count == 0) return;

            string path = files[0].TryGetLocalPath();
            if (path == null) return;

            if (VM == null) return;

            bool needsProfile = VM.TryGetCompatibleProfiles(path, out List<OverworldGraphicsProfileOption> profiles, out string analysisError);
            if (analysisError != null)
            {
                await DialogHelper.ShowErrorAsync($"Couldn't import: {analysisError}", "Overworld editor");
                return;
            }

            string error;
            if (needsProfile)
            {
                OverworldProfilePickerViewModel pickerVm = new OverworldProfilePickerViewModel(profiles);
                OverworldProfilePickerView picker = new OverworldProfilePickerView(pickerVm);
                bool accepted = await picker.ShowDialog<bool>(this);
                if (!accepted || pickerVm.SelectedProfile == null) return;

                string warning = VM.GetSelectedMemberUsageWarning();
                if (warning != null && !await DialogHelper.AskYesNoAsync(warning, "Shared texture slot", this))
                    return;

                error = VM.ImportPngUsingProfile(path, pickerVm.SelectedProfile.AppearanceId);
            }
            else
            {
                // Refuse an unusable picture before asking about the shared slot.
                error = VM.ImportPng(path, apply: false);
                if (error == null)
                {
                    string warning = VM.GetSelectedMemberUsageWarning();
                    if (warning != null && !await DialogHelper.AskYesNoAsync(warning, "Shared texture slot", this))
                        return;

                    error = VM.ImportPng(path);
                }
            }
            if (error != null)
                await DialogHelper.ShowErrorAsync($"Couldn't import: {error}", "Overworld editor");
            else SaveNotice.Imported(null, path);
        }

        private async void Export_Click(object sender, RoutedEventArgs e)
        {
            IStorageFile file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export PNG",
                DefaultExtension = "png",
                FileTypeChoices = new List<FilePickerFileType>
                {
                    new FilePickerFileType("PNG image") { Patterns = new[] { "*.png" } }
                }
            });
            if (file == null) return;

            string path = file.TryGetLocalPath();
            if (path == null) return;

            if (VM?.ExportPng(path) == false)
                await DialogHelper.ShowErrorAsync("Couldn't export.", "Overworld editor");
            else SaveNotice.Exported(path);
        }

        private void SaveSelected_Click(object sender, RoutedEventArgs e) => VM?.SaveSelected();
        private void SaveAll_Click(object sender, RoutedEventArgs e)      => VM?.SaveAll();
        private async void Discard_Click(object sender, RoutedEventArgs e)
        {
            if (await DialogHelper.ConfirmDiscardAsync(this)) VM?.DiscardChanges();
        }

        private void ShowFile_Click(object sender, RoutedEventArgs e)
        {
            string path = VM?.GetCurrentFilePath();
            if (path != null) SystemShell.RevealInFileManager(path);
        }

        private async void AddEntry_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null) return;

            AddOverworldEntryViewModel dlgVm = new AddOverworldEntryViewModel();
            AddOverworldEntryView dlg = new AddOverworldEntryView(dlgVm);
            await dlg.ShowDialog(this);
            if (!dlgVm.Confirmed) return;

            if (dlgVm.SelectedSlot == null || dlgVm.SelectedCloneSource == null)
            {
                await DialogHelper.ShowErrorAsync("Choose a texture slot and an overworld to copy.");
                return;
            }

            string error = VM.AddEntryWithImage(dlgVm.AppearanceIdText, dlgVm.SelectedSlot.Id, dlgVm.SelectedCloneSource.Id, dlgVm.PngPath, dlgVm.RawBtxPath);
            if (error != null)
                await DialogHelper.ShowErrorAsync($"Couldn't add entry: {error}");
        }

        private async void DeleteEntry_Click(object sender, RoutedEventArgs e)
        {
            if (!await DialogHelper.AskYesNoAsync("Delete this custom overworld entry? This can't be undone.", "Delete overworld entry?", this))
                return;

            string error = VM?.DeleteSelectedEntry();
            if (error != null)
                await DialogHelper.ShowErrorAsync($"Couldn't delete entry: {error}");
        }
    }
}
