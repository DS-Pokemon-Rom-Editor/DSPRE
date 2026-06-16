using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views
{
    public partial class TMEditorView : Window
    {
        private TMEditorViewModel ViewModel => (TMEditorViewModel)DataContext;

        public TMEditorView()
        {
            AvaloniaXamlLoader.Load(this);
            var vm = new TMEditorViewModel();
            DataContext = vm;
            // VM owns the bound Title (+ "*" marker); chrome adds Ctrl+S + the close guard.
            EditorWindowChrome.Attach(this, vm, manageTitle: false,
                confirmClose: vm.ConfirmCloseAsync, onClosed: vm.Detach);
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
            => ViewModel.SaveCommand();

        private void Undo_Click(object sender, RoutedEventArgs e) => ViewModel.Undo();
        private void Redo_Click(object sender, RoutedEventArgs e) => ViewModel.Redo();

        private void AutoPaletteButton_Click(object sender, RoutedEventArgs e)
            => ViewModel.AutoPaletteCommand();

        private async void AutoPaletteAllButton_Click(object sender, RoutedEventArgs e)
            => await ViewModel.AutoPaletteAllCommand(this);

        private async void ExportButton_Click(object sender, RoutedEventArgs e)
            => await ViewModel.ExportCommand(this);

        private async void ImportButton_Click(object sender, RoutedEventArgs e)
            => await ViewModel.ImportCommand(this);
    }
}
