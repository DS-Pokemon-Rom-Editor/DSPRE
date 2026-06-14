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
            DataContext = new TMEditorViewModel();
            Closing += OnWindowClosing;
        }

        private async void OnWindowClosing(object sender, WindowClosingEventArgs e)
        {
            // Cancel immediately so we can await the async confirm dialog
            e.Cancel = true;
            bool canClose = await ViewModel.ConfirmCloseAsync();
            if (canClose)
            {
                // Detach handler to avoid re-entry, then close for real
                ViewModel.Detach();
                Closing -= OnWindowClosing;
                Close();
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
            => ViewModel.SaveCommand();

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
