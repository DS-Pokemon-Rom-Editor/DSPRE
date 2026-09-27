using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views.Pokemon
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
            EditorWindowChrome.Attach(this, vm, manageTitle: false, onClosed: vm.Detach);
        }

        private void Save_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
            => VM.SaveChanges();

        private void Discard_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
            => VM.DiscardChanges();

        private async void TradeID_Changed(object sender, NumericUpDownValueChangedEventArgs e)
        {
            if (e.NewValue.HasValue)
                await VM.ChangeTradeIDAsync((int)e.NewValue.Value);
        }
    }
}
