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
            TradeEditorViewModel vm = new TradeEditorViewModel();
            DataContext = vm;
            // VM owns the bound Title (+ "●" marker); chrome adds Ctrl+S + the close guard.
            EditorWindowChrome.Attach(this, vm, title: () => vm.Title);
        }

        private async void Save_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
            => await DialogHelper.GuardAsync(() => { VM.SaveChanges(); return System.Threading.Tasks.Task.CompletedTask; }, "The trade wasn't saved.", "Trade editor", this);

        private async void Discard_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (await DialogHelper.ConfirmDiscardAsync(this)) VM.DiscardChanges();
        }

        private async void TradeID_Changed(object sender, NumericUpDownValueChangedEventArgs e)
        {
            if (e.NewValue.HasValue)
                await VM.ChangeTradeIDAsync((int)e.NewValue.Value);
        }
    }
}
