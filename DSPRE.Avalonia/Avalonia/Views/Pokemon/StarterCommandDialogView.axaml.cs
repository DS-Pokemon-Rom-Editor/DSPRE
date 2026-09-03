using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using DSPRE.Avalonia.ViewModels.Pokemon;

namespace DSPRE.Avalonia.Views.Pokemon
{
    public partial class StarterCommandDialogView : Window
    {
        private StarterCommandDialogViewModel VM => DataContext as StarterCommandDialogViewModel;

        public StarterCommandDialogView() => AvaloniaXamlLoader.Load(this);

        public StarterCommandDialogView(StarterCommandDialogViewModel vm) : this() => DataContext = vm;

        private void Verify_Click(object sender, RoutedEventArgs e) => VM?.Verify();

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            VM?.Accept();
            Close(true);
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => Close(false);
    }
}
