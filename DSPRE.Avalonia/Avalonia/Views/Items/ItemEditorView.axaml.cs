using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views.Items
{
    public partial class ItemEditorView : Window
    {
        private ItemEditorViewModel VM => (ItemEditorViewModel)DataContext;

        public ItemEditorView(ItemEditorViewModel vm)
        {
            DataContext = vm;
            InitializeComponent();
            this.FindControl<Button>("SaveAllButton").Click += (_, _) => VM?.SaveChanges();
            this.FindControl<Button>("AddItemButton").Click += async (_, _) => { if (VM != null) await VM.AddNewItemAsync(this); };
            EditorWindowChrome.Attach(this, vm, onClosed: vm.Detach);
        }

        private void Export_Click(object sender, RoutedEventArgs e) => VM?.ExportToFile();

        /// <summary>Hands this item's icon to the Graphics window. Items do not sit in the icon archive in
        /// item order; the game's own table says which drawing each one uses.</summary>
        private void OpenInGraphics_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null) return;
            int drawing = Data.GraphicAssets.DrawingForItem(VM.SelectedItemIndex);
            if (drawing < 0)
            {
                _ = DialogHelper.ShowInfo("This item does not name a drawing, so there is nothing to open.",
                                          "Open icon in Graphics");
                return;
            }
            AvaloniaEditorLauncher.OpenGraphicAt(DSPRE.RomInfo.DirNames.itemIcons, drawing);
        }

        private void Undo_Click(object sender, RoutedEventArgs e) => VM?.Undo();
        private void Redo_Click(object sender, RoutedEventArgs e) => VM?.Redo();
    }
}
