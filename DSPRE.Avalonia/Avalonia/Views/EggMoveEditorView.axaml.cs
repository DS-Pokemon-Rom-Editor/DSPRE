using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views
{
    public partial class EggMoveEditorView : Window
    {
        private EggMoveEditorViewModel VM => (EggMoveEditorViewModel)DataContext;

        public EggMoveEditorView()
        {
            AvaloniaXamlLoader.Load(this);
            var vm = new EggMoveEditorViewModel();
            DataContext = vm;
            // VM owns the bound Title (+ "*" marker); chrome adds Ctrl+S + the close guard.
            EditorWindowChrome.Attach(this, vm, manageTitle: false, onClosed: vm.Detach);
        }

        private async void Save_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
            => await VM.SaveCommand();

        private async void Export_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
            => await VM.ExportCommand(this);

        private async void Import_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
            => await VM.ImportCommand(this);

        private void AddMon_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
            => VM.AddMonCommand();

        private void ReplaceMon_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
            => VM.ReplaceMonCommand();

        private void DeleteMon_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
            => VM.DeleteMonCommand();

        private void AddMove_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
            => VM.AddMoveCommand();

        private void ReplaceMove_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
            => VM.ReplaceMoveCommand();

        private void DeleteMove_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
            => VM.DeleteMoveCommand();

        private async void BulkReplace_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
            => await VM.BulkReplaceCommand();

        private async void BulkDelete_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
            => await VM.BulkDeleteCommand();

        private void Search_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
            => VM.SearchMonCommand();

        private void Search_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) VM.SearchMonCommand();
        }

        private void SearchResult_DoubleTapped(object sender, global::Avalonia.Input.TappedEventArgs e)
            => VM.JumpToSearchResult();
    }
}
