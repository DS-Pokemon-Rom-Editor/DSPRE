using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views.Pokemon
{
    public partial class TMEditorView : Window
    {
        private TMEditorViewModel ViewModel => (TMEditorViewModel)DataContext;

        public TMEditorView()
        {
            AvaloniaXamlLoader.Load(this);
            TMEditorViewModel vm = new TMEditorViewModel();
            DataContext = vm;
            // VM owns the bound Title (+ "●" marker); chrome adds Ctrl+S + the close guard.
            EditorWindowChrome.Attach(this, vm, title: () => vm.Title);
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
            => ViewModel.SaveCommand();

        private async void Discard_Click(object sender, RoutedEventArgs e)
        {
            if (await DialogHelper.ConfirmDiscardAsync(this)) ViewModel.DiscardChanges();
        }

        private void AutoPaletteButton_Click(object sender, RoutedEventArgs e)
            => ViewModel.AutoPaletteCommand();

        private async void AutoPaletteAllButton_Click(object sender, RoutedEventArgs e)
            => await ViewModel.AutoPaletteAllCommandAsync(this);

        private async void ExportButton_Click(object sender, RoutedEventArgs e)
            => await ViewModel.ExportCommandAsync(this);

        private async void ImportButton_Click(object sender, RoutedEventArgs e)
            => await ViewModel.ImportCommandAsync(this);
    }
}
