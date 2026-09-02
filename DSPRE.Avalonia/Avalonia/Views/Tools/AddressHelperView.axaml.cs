using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views.Tools
{
    public partial class AddressHelperView : Window
    {
        private AddressHelperViewModel ViewModel => (AddressHelperViewModel)DataContext;

        public AddressHelperView()
        {
            AvaloniaXamlLoader.Load(this);
            DataContext = new AddressHelperViewModel();
        }

        private void SearchButton_Click(object sender, RoutedEventArgs e)
            => ViewModel.SearchCommand();
    }
}
