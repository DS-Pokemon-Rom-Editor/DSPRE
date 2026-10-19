using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views.Items
{
    public partial class GroundItemScriptsView : Window
    {
        private GroundItemScriptsViewModel VM => DataContext as GroundItemScriptsViewModel;

        public GroundItemScriptsView()
        {
            InitializeComponent();
        }

        public GroundItemScriptsView(GroundItemScriptsViewModel vm) : this()
        {
            DataContext = vm;
            EditorWindowChrome.Attach(this, vm);
        }

        private void Add_Click(object sender, RoutedEventArgs e) => VM?.AddEntry();

        private void Change_Click(object sender, RoutedEventArgs e) => VM?.ChangeSelectedEntry();

        private void Remove_Click(object sender, RoutedEventArgs e) => VM?.RemoveSelectedEntry();

        private void Save_Click(object sender, RoutedEventArgs e) => VM?.Save();
        private async void Discard_Click(object sender, RoutedEventArgs e)
        {
            if (await DialogHelper.ConfirmDiscardAsync(this)) VM?.Discard();
        }
        private void Close_Click(object sender, RoutedEventArgs e) => Close();
        private void Undo_Click(object sender, RoutedEventArgs e) => VM?.Undo();
        private void Redo_Click(object sender, RoutedEventArgs e) => VM?.Redo();
    }
}
