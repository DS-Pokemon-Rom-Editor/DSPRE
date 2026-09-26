using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels.Items;

namespace DSPRE.Avalonia.Views.Items
{
    public partial class UndergroundMiningView : UserControl
    {
        private UndergroundMiningViewModel VM => DataContext as UndergroundMiningViewModel;

        public UndergroundMiningView() { InitializeComponent(); }

        public UndergroundMiningView(UndergroundMiningViewModel vm) : this() { DataContext = vm; }

        private async void Save_Click(object sender, RoutedEventArgs e) { if (VM != null) await VM.SaveChangesAsync(); }
        private void Discard_Click(object sender, RoutedEventArgs e) => VM?.DiscardChanges();
    }
}
