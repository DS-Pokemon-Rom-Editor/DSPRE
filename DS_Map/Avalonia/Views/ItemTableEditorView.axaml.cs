using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views
{
    public partial class ItemTableEditorView : Window
    {
        private ItemTableEditorViewModel VM => (ItemTableEditorViewModel)DataContext;

        public ItemTableEditorView(ItemTableEditorViewModel vm)
        {
            DataContext = vm;
            InitializeComponent();
            EditorWindowChrome.Attach(this, vm);
        }

        private void Save_Click(object sender, RoutedEventArgs e) => VM?.SaveChanges();

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private void HiddenAdd_Click(object sender, RoutedEventArgs e)    => VM?.AddHiddenItem();
        private void HiddenRemove_Click(object sender, RoutedEventArgs e) => VM?.RemoveSelectedHiddenItem();
    }
}
