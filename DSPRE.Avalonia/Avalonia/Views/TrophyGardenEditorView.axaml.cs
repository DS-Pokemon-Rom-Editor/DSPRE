using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views
{
    public partial class TrophyGardenEditorView : UserControl
    {
        private TrophyGardenEditorViewModel VM => DataContext as TrophyGardenEditorViewModel;

        public TrophyGardenEditorView()
        {
            InitializeComponent();
        }

        public TrophyGardenEditorView(TrophyGardenEditorViewModel vm) : this()
        {
            DataContext = vm;
        }

        private void Save_Click(object sender, RoutedEventArgs e) => VM?.Save();

        private async void Export_Click(object sender, RoutedEventArgs e)
        {
            var top = TopLevel.GetTopLevel(this);
            if (top == null || VM == null) return;

            var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export Trophy Garden Encounters",
                DefaultExtension = "bin",
                SuggestedFileName = "trophy_garden_encounters.bin",
                FileTypeChoices = new List<FilePickerFileType> { new FilePickerFileType("Binary file") { Patterns = new[] { "*.bin" } } }
            });
            if (file == null) return;
            string path = file.TryGetLocalPath();
            if (path == null) return;
            VM.Export(path);
        }

        private async void Import_Click(object sender, RoutedEventArgs e)
        {
            var top = TopLevel.GetTopLevel(this);
            if (top == null || VM == null) return;

            var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Import Trophy Garden Encounters",
                AllowMultiple = false,
                FileTypeFilter = new List<FilePickerFileType> { new FilePickerFileType("Binary file") { Patterns = new[] { "*.bin" } } }
            });
            if (files.Count == 0) return;
            string path = files[0].TryGetLocalPath();
            if (path == null) return;
            VM.Import(path);
        }

        private void Locate_Click(object sender, RoutedEventArgs e) => VM?.Locate();

        private void Help_Click(object sender, RoutedEventArgs e) => VM?.ShowHelp();
    }
}
