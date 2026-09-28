using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels.Pokemon;

namespace DSPRE.Avalonia.Views.Pokemon
{
    public partial class WildIntroEditorView : UserControl
    {
        private WildIntroEditorViewModel VM => DataContext as WildIntroEditorViewModel;

        public WildIntroEditorView() { InitializeComponent(); }

        public WildIntroEditorView(WildIntroEditorViewModel vm) : this() { DataContext = vm; }

        private async void Save_Click(object sender, RoutedEventArgs e) { if (VM != null) await VM.SaveChangesAsync(); }
        private void Discard_Click(object sender, RoutedEventArgs e) => VM?.DiscardChanges();
        private void OpenVsIntros_Click(object sender, RoutedEventArgs e) => AvaloniaEditorLauncher.OpenVsIntroEditor();
    }
}
