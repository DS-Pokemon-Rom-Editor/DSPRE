using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using DSPRE.Avalonia.ViewModels.World;

namespace DSPRE.Avalonia.Views.World
{
    public partial class AddTexturesDialogView : Window
    {
        public AddTexturesDialogView() => AvaloniaXamlLoader.Load(this);

        public AddTexturesDialogView(MapTilesViewModel.TexturePlan plan) : this() => DataContext = plan;

        private void Add_Click(object sender, RoutedEventArgs e) => Close(true);

        private void Cancel_Click(object sender, RoutedEventArgs e) => Close(false);
    }
}
