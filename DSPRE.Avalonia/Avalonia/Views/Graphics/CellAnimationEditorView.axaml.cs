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

        private void BackToPoketch_Click(object sender, RoutedEventArgs e) => VM?.BackToPoketch();

        private void AddSequence_Click(object sender, RoutedEventArgs e) => VM?.AddSequence();

        private void RemoveSequence_Click(object sender, RoutedEventArgs e) => VM?.RemoveLastSequence();

        private void AddFrame_Click(object sender, RoutedEventArgs e)
        {
            if (Which(sender) is int n) VM?.AddFrameAfter(n);
        }

        private void RemoveFrame_Click(object sender, RoutedEventArgs e)
        {
            if (Which(sender) is int n) VM?.RemoveFrameAt(n);
        }

        // Each frame's buttons carry that frame's number, since they are stamped out of one template.
        private static int? Which(object sender) => (sender as Control)?.Tag as int?;
    }
}
