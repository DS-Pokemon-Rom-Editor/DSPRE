using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels.Pokemon;

namespace DSPRE.Avalonia.Views.Pokemon
{
    public partial class MoveTutorEditorView : UserControl
    {
        private MoveTutorEditorViewModel VM => DataContext as MoveTutorEditorViewModel;

        public MoveTutorEditorView() { InitializeComponent(); }

        public MoveTutorEditorView(MoveTutorEditorViewModel vm) : this() { DataContext = vm; }

        private async void Save_Click(object sender, RoutedEventArgs e) { if (VM != null) await VM.SaveChangesAsync(); }
        private void Discard_Click(object sender, RoutedEventArgs e) => VM?.DiscardChanges();
    }
}
