using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views.Tools
{
    public partial class OverlayEditorView : Window
    {
        private OverlayEditorViewModel VM => (OverlayEditorViewModel)DataContext;

        public OverlayEditorView()
        {
            AvaloniaXamlLoader.Load(this);
            OverlayEditorViewModel vm = new OverlayEditorViewModel();
            DataContext = vm;
            // The VM owns the bound Title; the chrome adds Ctrl+S and the close guard.
            EditorWindowChrome.Attach(this, vm, manageTitle: false);
        }

        private async void Save_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
            => await VM.SaveChangesCore();

        private void DecompressAll_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
            => VM.ToggleAllCompressed();

        private void ToggleMarked_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
            => VM.ToggleAllMarked();

        private void Discard_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
            => VM.DiscardChanges();
    }
}
