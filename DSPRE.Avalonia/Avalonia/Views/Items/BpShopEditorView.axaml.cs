using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels.Items;

namespace DSPRE.Avalonia.Views.Items
{
    public partial class BpShopEditorView : UserControl
    {
        private BpShopEditorViewModel VM => DataContext as BpShopEditorViewModel;

        public BpShopEditorView() { InitializeComponent(); }

        public BpShopEditorView(BpShopEditorViewModel vm) : this() { DataContext = vm; }

        private async void Save_Click(object sender, RoutedEventArgs e) { if (VM != null) await VM.SaveChangesAsync(); }
        private void Discard_Click(object sender, RoutedEventArgs e) => VM?.DiscardChanges();

        private void AddLeft_Click(object sender, RoutedEventArgs e) => VM?.Add(false);
        private void AddRight_Click(object sender, RoutedEventArgs e) => VM?.Add(true);
        private void RemoveLeft_Click(object sender, RoutedEventArgs e) => VM?.Remove(false);
        private void RemoveRight_Click(object sender, RoutedEventArgs e) => VM?.Remove(true);
        private void UpLeft_Click(object sender, RoutedEventArgs e) => VM?.Move(false, -1);
        private void DownLeft_Click(object sender, RoutedEventArgs e) => VM?.Move(false, 1);
        private void UpRight_Click(object sender, RoutedEventArgs e) => VM?.Move(true, -1);
        private void DownRight_Click(object sender, RoutedEventArgs e) => VM?.Move(true, 1);
        private void ToRight_Click(object sender, RoutedEventArgs e) => VM?.Transfer(false);
        private void ToLeft_Click(object sender, RoutedEventArgs e) => VM?.Transfer(true);
    }
}
