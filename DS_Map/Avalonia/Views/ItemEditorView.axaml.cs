using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views
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

        private void Undo_Click(object sender, RoutedEventArgs e) => VM?.Undo();
        private void Redo_Click(object sender, RoutedEventArgs e) => VM?.Redo();
    }
}
