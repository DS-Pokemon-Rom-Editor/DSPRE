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
        }

        private async void ImportIcon_Click(object sender, RoutedEventArgs e)
        {
            if (VM != null) await VM.ImportIconAsync(this);
        }

        private async void ExportIcon_Click(object sender, RoutedEventArgs e)
        {
            if (VM != null) await VM.ExportIconAsync(this);
        }

        private void SaveTitles_Click(object sender, RoutedEventArgs e) => VM?.SaveTitles();
    }
}
