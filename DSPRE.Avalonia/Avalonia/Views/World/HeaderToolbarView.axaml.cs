using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views.World
{
    /// <summary>The header's toolbar, shared by the Header editor window and the main view's Header tab.</summary>
    public partial class HeaderToolbarView : UserControl
    {
        private HeaderEditorViewModel VM => DataContext as HeaderEditorViewModel;

        public HeaderToolbarView()
        {
            InitializeComponent();
        }

        private void Save_Click(object sender, RoutedEventArgs e) => VM?.Save();
        private void Undo_Click(object sender, RoutedEventArgs e) => VM?.Undo();
        private void Redo_Click(object sender, RoutedEventArgs e) => VM?.Redo();
        private async void Reset_Click(object sender, RoutedEventArgs e)
        {
            if (await DialogHelper.ConfirmDiscardAsync(this)) VM?.Reset();
        }
        private void Copy_Click(object sender, RoutedEventArgs e) => VM?.Copy();
        private void Paste_Click(object sender, RoutedEventArgs e) => VM?.Paste();
        private void Search_Click(object sender, RoutedEventArgs e) => AvaloniaEditorLauncher.OpenHeaderSearch();
        private async void Import_Click(object sender, RoutedEventArgs e) => await SafeAsync(VM?.ImportAsync());
        private async void Export_Click(object sender, RoutedEventArgs e) => await SafeAsync(VM?.ExportAsync());
        private async void AddHeader_Click(object sender, RoutedEventArgs e) => await SafeAsync(VM?.AddHeaderAsync());
        private async void RemoveHeader_Click(object sender, RoutedEventArgs e) => await SafeAsync(VM?.RemoveHeaderAsync());

        private static async Task SafeAsync(Task task)
        {
            if (task == null) return;
            try { await task; } catch { /* handled in VM */ }
        }
    }
}
