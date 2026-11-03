using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels.Pokemon;

namespace DSPRE.Avalonia.Views.Pokemon
{
    public partial class RoamersView : UserControl
    {
        private RoamersViewModel VM => DataContext as RoamersViewModel;

        public RoamersView()
        {
            InitializeComponent();
            TabSetup.OnFirstShow(this, () => VM?.Setup());
        }

        private async void Save_Click(object sender, RoutedEventArgs e) { if (VM != null) await VM.SaveChangesAsync(); }
        private async void Discard_Click(object sender, RoutedEventArgs e)
        {
            if (await DialogHelper.ConfirmDiscardAsync(this)) VM?.DiscardChanges();
        }
    }
}
