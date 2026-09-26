using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels.Items;

namespace DSPRE.Avalonia.Views.Items
{
    public partial class BerryDataEditorView : UserControl
    {
        private BerryDataEditorViewModel VM => DataContext as BerryDataEditorViewModel;

        public BerryDataEditorView() { InitializeComponent(); }

        public BerryDataEditorView(BerryDataEditorViewModel vm) : this() { DataContext = vm; }

        private async void Save_Click(object sender, RoutedEventArgs e) { if (VM != null) await VM.SaveChangesAsync(); }
        private void Discard_Click(object sender, RoutedEventArgs e) => VM?.DiscardChanges();
    }
}
