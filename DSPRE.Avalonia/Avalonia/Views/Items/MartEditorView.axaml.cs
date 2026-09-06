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
        }

        public MartEditorView(MartEditorViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            try { ViewModel?.SaveChanges(); }
            catch (System.Exception ex) { await DialogHelper.ShowError("The marts could not be saved:\n" + ex.Message, "Mart Editor"); }
        }
        private void AddItem_Click(object sender, RoutedEventArgs e) => ViewModel?.AddItem();
        private void RemoveItem_Click(object sender, RoutedEventArgs e) => ViewModel?.RemoveLastItem();
        private void AddShop_Click(object sender, RoutedEventArgs e) => ViewModel?.AddShop();
        private async void RemoveShop_Click(object sender, RoutedEventArgs e)
        {
            if (!await DialogHelper.AskYesNo(
                    "Remove this custom mart? DSPRE will not update scripts that call its SpMartScreen ID.",
                    "Remove custom mart")) return;
            ViewModel?.RemoveCustomShop();
        }
    }
}
