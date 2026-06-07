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
            DataContext = new TradeEditorViewModel();
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

        private void SaveTrade_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
            => VM.SaveTradeCommand();

        private void SaveText_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
            => VM.SaveTextCommand();

        private void SaveAll_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
            => VM.SaveAllCommand();

        private async void TradeID_Changed(object sender, NumericUpDownValueChangedEventArgs e)
        {
            if (e.NewValue.HasValue)
                await VM.ChangeTradeIDAsync((int)e.NewValue.Value);
        }
    }
}
