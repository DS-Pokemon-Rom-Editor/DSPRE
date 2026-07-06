using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views
{
    public partial class CustomScrcmdManagerView : Window
    {
        private CustomScrcmdManagerViewModel VM => DataContext as CustomScrcmdManagerViewModel;

        public CustomScrcmdManagerView()
        {
            InitializeComponent();
        }

        public CustomScrcmdManagerView(CustomScrcmdManagerViewModel vm) : this()
        {
            DataContext = vm;
        }

        private async void Import_Click(object sender, RoutedEventArgs e) { if (VM != null) await VM.Import(this); }
        private async void Export_Click(object sender, RoutedEventArgs e) { if (VM != null) await VM.Export(this); }
        private async void OpenFolder_Click(object sender, RoutedEventArgs e) { if (VM != null) await VM.OpenFolder(); }
        private void Refresh_Click(object sender, RoutedEventArgs e) => VM?.Refresh();
    }
}
