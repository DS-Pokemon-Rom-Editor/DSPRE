using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views
{
    public partial class TradeEditorView : Window
    {
        private TradeEditorViewModel VM => (TradeEditorViewModel)DataContext;

        public TradeEditorView()
        {
            AvaloniaXamlLoader.Load(this);
            var vm = new TradeEditorViewModel();
            DataContext = vm;
            // VM owns the bound Title (+ "*" marker); chrome adds Ctrl+S + the close guard.
            EditorWindowChrome.Attach(this, vm, manageTitle: false,
                confirmClose: vm.ConfirmCloseAsync, onClosed: vm.Detach);
        }

        private void SaveTrade_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
            => VM.SaveTradeCommand();

        private void SaveText_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
            => VM.SaveTextCommand();

        private void SaveAll_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
            => VM.SaveAllCommand();

        private void Undo_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e) => VM.Undo();
        private void Redo_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e) => VM.Redo();

        private async void TradeID_Changed(object sender, NumericUpDownValueChangedEventArgs e)
        {
            if (e.NewValue.HasValue)
                await VM.ChangeTradeIDAsync((int)e.NewValue.Value);
        }
    }
}
