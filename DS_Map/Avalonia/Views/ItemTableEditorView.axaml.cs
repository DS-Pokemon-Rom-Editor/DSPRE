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
        }

        private void Save_Click(object sender, RoutedEventArgs e) => VM?.SaveChanges();

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private void HiddenAdd_Click(object sender, RoutedEventArgs e)    => VM?.AddHiddenItem();
        private void HiddenRemove_Click(object sender, RoutedEventArgs e) => VM?.RemoveSelectedHiddenItem();

        protected override async void OnClosing(WindowClosingEventArgs e)
        {
            if (VM?.HasUnsavedChanges == true)
            {
                e.Cancel = true;
                bool discard = await DialogHelper.AskYesNo(
                    $"Discard unsaved changes to {VM.UnsavedChangesDescription}?",
                    "Unsaved Changes");
                if (discard) { VM.DiscardChanges(); Close(); }
            }
            base.OnClosing(e);
        }
    }
}
