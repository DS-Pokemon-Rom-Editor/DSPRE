using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using DSPRE.Avalonia.ViewModels;
using System.Collections.Generic;

namespace DSPRE.Avalonia.Views.World
{
    public partial class AddOverworldEntryView : Window
    {
        private AddOverworldEntryViewModel VM => DataContext as AddOverworldEntryViewModel;

        public AddOverworldEntryView()
        {
            InitializeComponent();
        }

        public AddOverworldEntryView(AddOverworldEntryViewModel vm) : this()
        {
            DataContext = vm;
        }

        private async void ChoosePng_Click(object sender, RoutedEventArgs e)
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Choose overworld image (PNG)",
                AllowMultiple = false,
                FileTypeFilter = new List<FilePickerFileType>
                {
                    new FilePickerFileType("PNG Image") { Patterns = new[] { "*.png" } }
                }
            });
            if (files.Count == 0) return;

            string path = files[0].TryGetLocalPath();
            if (path != null) VM?.SetPng(path);
        }

        private async void ChooseRawBtx_Click(object sender, RoutedEventArgs e)
        {
            // Raw NSBTX/BTX0 dumps (e.g. extracted from another ROM) commonly have no file
            // extension at all, so no FileTypeFilter here: any file can be picked and it's
            // validated as a real BTX0 texture right after selection.
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Choose a raw texture file (BTX0)",
                AllowMultiple = false,
            });
            if (files.Count == 0) return;

            string path = files[0].TryGetLocalPath();
            if (path != null) VM?.SetRawBtx(path);
        }

        private void ClearImage_Click(object sender, RoutedEventArgs e) => VM?.ClearImage();

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            VM?.Confirm();
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
    }
}
