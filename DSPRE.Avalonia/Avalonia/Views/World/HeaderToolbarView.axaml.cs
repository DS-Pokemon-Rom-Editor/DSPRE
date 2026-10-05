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
        private void Reset_Click(object sender, RoutedEventArgs e) => VM?.Reset();
        private void Copy_Click(object sender, RoutedEventArgs e) => VM?.Copy();
        private void Paste_Click(object sender, RoutedEventArgs e) => VM?.Paste();
        private async void Import_Click(object sender, RoutedEventArgs e) => await Safe(VM?.ImportAsync());
        private async void Export_Click(object sender, RoutedEventArgs e) => await Safe(VM?.ExportAsync());
        private async void AddHeader_Click(object sender, RoutedEventArgs e) => await Safe(VM?.AddHeaderAsync());
        private async void RemoveHeader_Click(object sender, RoutedEventArgs e) => await Safe(VM?.RemoveHeaderAsync());

        private static async Task Safe(Task task)
        {
            if (task == null) return;
            try { await task; } catch { /* handled in VM */ }
        }
    }
}
