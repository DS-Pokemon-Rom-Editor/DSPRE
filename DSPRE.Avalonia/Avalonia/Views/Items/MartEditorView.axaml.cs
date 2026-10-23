using Avalonia.Controls;
using Avalonia.Interactivity;

namespace DSPRE.Avalonia.Views.Items
{
    public partial class MartEditorView : UserControl
    {
        private MartEditorViewModel ViewModel => DataContext as MartEditorViewModel;

        public MartEditorView()
        {
            InitializeComponent();
            NameRefresh.Bind(this, () => ViewModel?.RefreshNames());
        }

        public MartEditorView(MartEditorViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            try { ViewModel?.SaveChanges(); }
            catch (System.Exception ex) { await DialogHelper.ShowErrorAsync("The marts couldn't be saved:\n" + ex.Message, "Mart editor"); }
        }
        private async void Discard_Click(object sender, RoutedEventArgs e)
        {
            if (!await DialogHelper.ConfirmDiscardAsync(this)) return;
            try { ViewModel?.DiscardChanges(); }
            catch (System.Exception ex) { await DialogHelper.ShowErrorAsync("The marts couldn't be reloaded:\n" + ex.Message, "Mart editor"); }
        }
        private void AddItem_Click(object sender, RoutedEventArgs e) => ViewModel?.AddItem();
        private void RemoveItem_Click(object sender, RoutedEventArgs e) => ViewModel?.RemoveLastItem();
        private void AddShop_Click(object sender, RoutedEventArgs e) => ViewModel?.AddShop();
        private void Expand_Click(object sender, RoutedEventArgs e) => ViewModel?.OfferExpansion();
        private async void RemoveShop_Click(object sender, RoutedEventArgs e)
        {
            if (!await DialogHelper.AskYesNoAsync(
                    "Remove this custom mart? Scripts that open it aren't changed.",
                    "Remove custom mart?")) return;
            ViewModel?.RemoveCustomShop();
        }
    }
}
