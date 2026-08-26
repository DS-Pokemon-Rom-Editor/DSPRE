using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views
{
    public partial class PaletteColorEditorView : Window
    {
        private PaletteColorEditorViewModel VM => (PaletteColorEditorViewModel)DataContext;

        public PaletteColorEditorView() => InitializeComponent();

        public PaletteColorEditorView(PaletteColorEditorViewModel vm) : this()
        {
            DataContext = vm;
            Closing += (_, __) => vm.CommitOnClose();
        }

        private void FavoriteSlot_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is not FavoriteSlotVM slot) return;
            if (slot.IsEmpty) VM.SaveCurrentToFavorite(slot.Slot);
            else VM.ApplyColor(slot.Argb.Value);
        }

        private void FavoriteClear_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is not FavoriteSlotVM slot) return;
            VM.ClearFavorite(slot.Slot);
        }

        private void RecentColor_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is not RecentColorVM entry) return;
            VM.ApplyColor(entry.Argb);
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
