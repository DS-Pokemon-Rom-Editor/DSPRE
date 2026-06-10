using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using DSPRE.Avalonia.ViewModels;
using System.Collections.Generic;
using System.Linq;

namespace DSPRE.Avalonia.Views
{
    public partial class BtxEditorView : Window
    {
        private BtxEditorViewModel VM => (BtxEditorViewModel)DataContext;

        public BtxEditorView(BtxEditorViewModel vm)
        {
            DataContext = vm;
            InitializeComponent();
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

            string error = VM?.ImportPng(path);
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
            if (path != null) Helpers.ExplorerSelect(path);
        }

        protected override async void OnClosing(WindowClosingEventArgs e)
        {
            if (VM?.HasUnsavedChanges == true)
            {
                e.Cancel = true;
                bool discard = await DialogHelper.AskYesNo(
                    $"Discard unsaved changes?\n{VM.UnsavedChangesDescription}",
                    "Unsaved Changes");
                if (discard) { VM.DiscardChanges(); Close(); }
            }
            base.OnClosing(e);
        }
    }
}
