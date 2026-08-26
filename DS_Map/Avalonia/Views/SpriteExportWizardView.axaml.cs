using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views
{
    public partial class SpriteExportWizardView : Window
    {
        private SpriteExportWizardViewModel VM => (SpriteExportWizardViewModel)DataContext;

        public SpriteExportWizardView() => InitializeComponent();

        public SpriteExportWizardView(SpriteExportWizardViewModel vm) : this()
        {
            DataContext = vm;
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e) => VM.SelectAll(true);
        private void SelectNone_Click(object sender, RoutedEventArgs e) => VM.SelectAll(false);
        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private async void Run_Click(object sender, RoutedEventArgs e)
        {
            IsEnabled = false;
            try { await VM.RunAsync(); }
            finally { IsEnabled = true; }
        }
    }
}
