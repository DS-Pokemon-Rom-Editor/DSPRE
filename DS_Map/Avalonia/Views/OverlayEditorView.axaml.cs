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
            DataContext = new OverlayEditorViewModel();
            Closing += OnWindowClosing;
        }

        private async void OnWindowClosing(object sender, WindowClosingEventArgs e)
        {
            e.Cancel = true;
            if (await VM.ConfirmCloseAsync())
            {
                Closing -= OnWindowClosing;
                Close();
            }
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
