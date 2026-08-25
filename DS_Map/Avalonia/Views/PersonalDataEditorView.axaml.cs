using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using DSPRE.Avalonia.ViewModels;
using System.Collections.Generic;

namespace DSPRE.Avalonia.Views
{
    public partial class PersonalDataEditorView : UserControl
    {
        private PersonalDataEditorViewModel ViewModel => (PersonalDataEditorViewModel)DataContext;

        public PersonalDataEditorView()
        {
            InitializeComponent();
        }

        public PersonalDataEditorView(PersonalDataEditorViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
            => await ViewModel.SaveCommand();

        private async void Export_Click(object sender, RoutedEventArgs e)
        {
            var window = TopLevel.GetTopLevel(this) as Window;
            await ViewModel.ExportCommand(window);
        }

        private async void Import_Click(object sender, RoutedEventArgs e)
        {
            var window = TopLevel.GetTopLevel(this) as Window;
            await ViewModel.ImportCommand(window);
        }

        private void AddMachine_Click(object sender, RoutedEventArgs e)
            => ViewModel.AddMachineCommand();

        private void RemoveMachine_Click(object sender, RoutedEventArgs e)
            => ViewModel.RemoveMachineCommand();

        private void AddAll_Click(object sender, RoutedEventArgs e)
            => ViewModel.AddAllMachinesCommand();

        private void RemoveAll_Click(object sender, RoutedEventArgs e)
            => ViewModel.RemoveAllMachinesCommand();

        private void TmHmBulkEditor_Click(object sender, RoutedEventArgs e)
            => AvaloniaEditorLauncher.OpenTmHmBulkEditor();

        private void CreateOwEntry_Click(object sender, RoutedEventArgs e)
            => ViewModel.CreateOwFollowerEntry();

        private async void ImportOwSprite_Click(object sender, RoutedEventArgs e)
        {
            var top = TopLevel.GetTopLevel(this);
            if (top == null || ViewModel == null) return;

            var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Import Overworld Follower Sprite Sheet PNG",
                AllowMultiple = false,
                FileTypeFilter = new List<FilePickerFileType> { DialogHelper.PngFilter }
            });
            if (files.Count == 0) return;
            string path = files[0].TryGetLocalPath();
            if (path == null) return;

            ViewModel.ImportOwFollowerSprite(path);
        }
    }
}
