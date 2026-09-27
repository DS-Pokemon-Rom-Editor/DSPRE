using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views.Graphics
{
    public partial class BannerEditorView : Window
    {
        private BannerEditorViewModel VM => DataContext as BannerEditorViewModel;

        public BannerEditorView()
        {
            InitializeComponent();
        }

        public BannerEditorView(BannerEditorViewModel vm) : this()
        {
            DataContext = vm;
            EditorWindowChrome.Attach(this, vm);
        }

        private async void ImportIcon_Click(object sender, RoutedEventArgs e)
        {
            if (VM != null) await VM.ImportIconAsync(this);
        }

        private async void ExportIcon_Click(object sender, RoutedEventArgs e)
        {
            if (VM != null) await VM.ExportIconAsync(this);
        }

        private void Save_Click(object sender, RoutedEventArgs e) => VM?.SaveChanges();
        private void Discard_Click(object sender, RoutedEventArgs e) => VM?.DiscardChanges();
    }
}
