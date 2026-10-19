using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views.Items
{
    public partial class ItemTableEditorView : Window
    {
        private ItemTableEditorViewModel VM => (ItemTableEditorViewModel)DataContext;

        public ItemTableEditorView(ItemTableEditorViewModel vm)
        {
            DataContext = vm;
            InitializeComponent();
            EditorWindowChrome.Attach(this, vm);
            TabDefault.SelectFirstVisible(Tabs);
        }

        // Pickup columns fold to their icons; a header click unfolds that one and folds the rest.
        private void PickupColumn_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null || sender is not Button { Tag: string tag } || !int.TryParse(tag, out int open)) return;
            VM.ExpandedPickupColumn = open;
            for (int c = 0; c <= 10; c++)
            {
                DataGridColumn column = PickupGrid.Columns[c + 1];
                bool unfolded = c == open;
                column.MinWidth = unfolded ? 230 : 56;
                column.Width = unfolded ? new DataGridLength(1, DataGridLengthUnitType.Star) : new DataGridLength(56);
                if (column.Header is Button header)
                {
                    string name = c < 9 ? $"Slot {c + 1}" : $"Rare {c - 8}";
                    header.Content = unfolded ? name : (c < 9 ? $"{c + 1}" : $"R{c - 8}");
                }
            }
        }

        public void ShowHiddenItem(int script)
        {
            if (VM == null || !VM.ShowHiddenItemsTab) return;
            Tabs.SelectedItem = HiddenTab;
            VM.SelectHiddenScript(script);
        }

        private void Save_Click(object sender, RoutedEventArgs e) => VM?.SaveChanges();

        private async void Discard_Click(object sender, RoutedEventArgs e)
        {
            if (await DialogHelper.ConfirmDiscardAsync(this)) VM?.DiscardChanges();
        }

        private void HiddenAdd_Click(object sender, RoutedEventArgs e)    => VM?.AddHiddenItem();
        private void GoToHiddenEvent_Click(object sender, RoutedEventArgs e) => VM?.GoToHiddenEvent();
        private void GoToHiddenScript_Click(object sender, RoutedEventArgs e) => VM?.GoToHiddenItemScript();
        private async void HiddenRemove_Click(object sender, RoutedEventArgs e) { if (VM != null) await VM.RemoveSelectedHiddenItemAsync(); }
    }
}
