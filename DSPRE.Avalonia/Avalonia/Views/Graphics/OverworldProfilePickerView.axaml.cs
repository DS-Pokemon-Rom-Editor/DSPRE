using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels.Graphics;

namespace DSPRE.Avalonia.Views.Graphics
{
    public partial class OverworldProfilePickerView : Window
    {
        private OverworldProfilePickerViewModel ViewModel => DataContext as OverworldProfilePickerViewModel;

        public OverworldProfilePickerView(OverworldProfilePickerViewModel viewModel)
        {
            DataContext = viewModel;
            InitializeComponent();
        }

        private void UseProfile_Click(object sender, RoutedEventArgs e) =>
            Close(ViewModel?.SelectedProfile != null);

        private void Cancel_Click(object sender, RoutedEventArgs e) => Close(false);
    }
}
