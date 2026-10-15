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

        public void ShowHiddenItem(int script)
        {
            if (VM == null || !VM.ShowHiddenItemsTab) return;
            Tabs.SelectedItem = HiddenTab;
            VM.SelectHiddenScript(script);
        }

        private void Save_Click(object sender, RoutedEventArgs e) => VM?.SaveChanges();

        private void Discard_Click(object sender, RoutedEventArgs e) => VM?.DiscardChanges();

        private void HiddenAdd_Click(object sender, RoutedEventArgs e)    => VM?.AddHiddenItem();
        private void GoToHiddenEvent_Click(object sender, RoutedEventArgs e) => VM?.GoToHiddenEvent();
        private void GoToHiddenScript_Click(object sender, RoutedEventArgs e) => VM?.GoToHiddenItemScript();
        private async void HiddenRemove_Click(object sender, RoutedEventArgs e) { if (VM != null) await VM.RemoveSelectedHiddenItemAsync(); }
    }
}
