using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels.Graphics;

namespace DSPRE.Avalonia.Views.Graphics
{
    public partial class OptionsExtraView : UserControl
    {
        private OptionsExtraViewModel VM => DataContext as OptionsExtraViewModel;

        public OptionsExtraView() => InitializeComponent();

        public OptionsExtraView(OptionsExtraViewModel vm) : this() => DataContext = vm;

        private void Save_Click(object sender, RoutedEventArgs e) => VM?.SaveChanges();
        private void Add_Click(object sender, RoutedEventArgs e) => VM?.Add();
        private void OpenText_Click(object sender, RoutedEventArgs e) => VM?.OpenText();

        private void Remove_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Control)?.DataContext is OptionsExtraViewModel.SpeedRow row) row.Remove();
        }

        private async void Discard_Click(object sender, RoutedEventArgs e)
        {
            if (await DialogHelper.ConfirmDiscardAsync(this)) VM?.DiscardChanges();
        }
    }
}
