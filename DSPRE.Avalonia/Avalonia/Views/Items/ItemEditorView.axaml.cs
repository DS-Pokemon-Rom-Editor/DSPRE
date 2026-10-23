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
            this.FindControl<Button>("SaveAllButton").Click += async (_, _) => await DialogHelper.GuardAsync(
                () => VM?.SaveAsync() ?? System.Threading.Tasks.Task.CompletedTask, "The item wasn't saved.", "Item editor", this);
            this.FindControl<Button>("AddItemButton").Click += async (_, _) => { if (VM != null) await VM.AddNewItemAsync(this); };
            EditorWindowChrome.Attach(this, vm);
        }

        private async void Export_Click(object sender, RoutedEventArgs e) { if (VM != null) await VM.ExportToFileAsync(TopLevel.GetTopLevel(this) as Window); }
        private async void Import_Click(object sender, RoutedEventArgs e) { if (VM != null) await VM.ImportFromFileAsync(this); }

        /// <summary>Hands this item's icon to All graphics. Items do not sit in the icon archive in
        /// item order; the game's own table says which drawing each one uses.</summary>
        private void OpenInGraphics_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null) return;
            int drawing = Data.GraphicAssets.DrawingForItem(VM.SelectedItemIndex);
            if (drawing < 0)
            {
                _ = DialogHelper.ShowInfoAsync("This item doesn't name a drawing, so there is nothing to open.",
                                          "Open in All graphics");
                return;
            }
            AvaloniaEditorLauncher.OpenGraphicAt(DSPRE.RomInfo.DirNames.ItemIcons, drawing);
        }

        private async void Discard_Click(object sender, RoutedEventArgs e)
        {
            if (await DialogHelper.ConfirmDiscardAsync(this)) VM?.DiscardChanges();
        }
        private void Undo_Click(object sender, RoutedEventArgs e) => VM?.Undo();
        private void Redo_Click(object sender, RoutedEventArgs e) => VM?.Redo();
    }
}
