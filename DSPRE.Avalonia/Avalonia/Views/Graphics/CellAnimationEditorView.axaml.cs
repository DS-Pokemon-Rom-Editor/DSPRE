using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using DSPRE.Avalonia.ViewModels.Graphics;

namespace DSPRE.Avalonia.Views.Graphics
{
    public partial class CellAnimationEditorView : Window
    {
        private CellAnimationEditorViewModel VM => DataContext as CellAnimationEditorViewModel;

        public CellAnimationEditorView()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public CellAnimationEditorView(CellAnimationEditorViewModel vm) : this()
        {
            DataContext = vm;
            Title = vm?.Subject ?? Title;
            EditorWindowChrome.Attach(this, vm);
            // The playback clock runs on a timer, so it has to be let go of when the window closes.
            Closed += (_, __) => vm?.Stop();
        }

        private void Play_Click(object sender, RoutedEventArgs e) => VM?.TogglePlay();

        private void Undo_Click(object sender, RoutedEventArgs e) => VM?.Undo();

        private void Redo_Click(object sender, RoutedEventArgs e) => VM?.Redo();

        private void Save_Click(object sender, RoutedEventArgs e) => VM?.SaveChanges();
    }
}
