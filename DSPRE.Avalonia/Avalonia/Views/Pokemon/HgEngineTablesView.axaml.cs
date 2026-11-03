using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels.Pokemon;

namespace DSPRE.Avalonia.Views.Pokemon
{
    public partial class HgEngineTablesView : UserControl
    {
        private HgEngineTablesViewModel VM => DataContext as HgEngineTablesViewModel;

        public HgEngineTablesView() { InitializeComponent(); }

        public HgEngineTablesView(HgEngineTablesViewModel vm) : this() { DataContext = vm; }

        private void Save_Click(object sender, RoutedEventArgs e) => VM?.SaveChanges();
        private void Add_Click(object sender, RoutedEventArgs e) => VM?.AddRow();
        private void Remove_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Control)?.DataContext is HgEngineTablesViewModel.TableRow row) VM?.RemoveRow(row);
        }
        private async void Discard_Click(object sender, RoutedEventArgs e)
        {
            if (await DialogHelper.ConfirmDiscardAsync(this)) VM?.DiscardChanges();
        }
    }
}
