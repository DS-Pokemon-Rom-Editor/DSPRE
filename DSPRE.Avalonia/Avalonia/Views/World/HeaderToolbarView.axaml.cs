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

        private async void Save_Click(object sender, RoutedEventArgs e)
            => await DialogHelper.GuardAsync(() => { VM?.Save(); return System.Threading.Tasks.Task.CompletedTask; }, "The header wasn't saved.", "Header editor");
        private void Undo_Click(object sender, RoutedEventArgs e) => VM?.Undo();
        private void Redo_Click(object sender, RoutedEventArgs e) => VM?.Redo();
        private async void Reset_Click(object sender, RoutedEventArgs e)
        {
            if (await DialogHelper.ConfirmDiscardAsync(this)) VM?.Reset();
        }
        private void Copy_Click(object sender, RoutedEventArgs e) => VM?.Copy();
        private void Paste_Click(object sender, RoutedEventArgs e) => VM?.Paste();
        private void Search_Click(object sender, RoutedEventArgs e) => AvaloniaEditorLauncher.OpenHeaderSearch();
        private async void Import_Click(object sender, RoutedEventArgs e) => await DialogHelper.GuardAsync(() => VM?.ImportAsync() ?? Task.CompletedTask, "The header wasn't imported.", ErrorTitle);
        private async void Export_Click(object sender, RoutedEventArgs e) => await DialogHelper.GuardAsync(() => VM?.ExportAsync() ?? Task.CompletedTask, "The header wasn't exported.", ErrorTitle);
        private async void AddHeader_Click(object sender, RoutedEventArgs e) => await DialogHelper.GuardAsync(() => VM?.AddHeaderAsync() ?? Task.CompletedTask, "The header wasn't added.", ErrorTitle);
        private async void RemoveHeader_Click(object sender, RoutedEventArgs e) => await DialogHelper.GuardAsync(() => VM?.RemoveHeaderAsync() ?? Task.CompletedTask, "The header wasn't removed.", ErrorTitle);

        private const string ErrorTitle = "Header editor";
    }
}
