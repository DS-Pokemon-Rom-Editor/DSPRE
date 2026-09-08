using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels.Trainers;

namespace DSPRE.Avalonia.Views.Trainers
{
    public partial class PokegearRematchView : UserControl
    {
        private PokegearRematchViewModel VM => DataContext as PokegearRematchViewModel;

        public PokegearRematchView()
        {
            InitializeComponent();
        }

        public PokegearRematchView(PokegearRematchViewModel vm) : this()
        {
            DataContext = vm;
        }

        private void SaveRow_Click(object sender, RoutedEventArgs e) => VM?.SaveCurrentRow();

        private void SaveAll_Click(object sender, RoutedEventArgs e) => VM?.SaveAll();
    }
}
