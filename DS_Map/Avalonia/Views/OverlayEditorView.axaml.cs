using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views
{
    public partial class OverlayEditorView : Window
    {
        private OverlayEditorViewModel VM => (OverlayEditorViewModel)DataContext;

        public OverlayEditorView()
        {
            AvaloniaXamlLoader.Load(this);
            var vm = new OverlayEditorViewModel();
            DataContext = vm;
            // VM owns the bound Title (+ "*" marker); chrome adds Ctrl+S + the close guard.
            EditorWindowChrome.Attach(this, vm, manageTitle: false, confirmClose: vm.ConfirmCloseAsync);
        }

        private async void Save_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
            => await VM.SaveChangesCore();

        private void DecompressAll_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
            => VM.ToggleAllCompressed();

        private void ToggleMarked_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
            => VM.ToggleAllMarked();

        private void Revert_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
            => VM.RevertChanges();
    }
}
