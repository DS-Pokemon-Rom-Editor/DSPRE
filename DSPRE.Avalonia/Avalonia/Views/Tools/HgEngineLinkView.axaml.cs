using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views.Tools
{
    public partial class HgEngineLinkView : Window
    {
        private HgEngineLinkViewModel VM => (HgEngineLinkViewModel)DataContext;

        public HgEngineLinkView()
        {
            AvaloniaXamlLoader.Load(this);
            DataContext = new HgEngineLinkViewModel();
        }

        private async void Browse_Click(object sender, RoutedEventArgs e) => await VM.BrowseAsync(this);
        private void Unlink_Click(object sender, RoutedEventArgs e) => VM.Unlink();
        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
