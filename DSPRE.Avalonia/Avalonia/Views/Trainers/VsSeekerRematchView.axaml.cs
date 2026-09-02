using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views.Trainers
{
    public partial class VsSeekerRematchView : UserControl
    {
        private VsSeekerRematchViewModel VM => DataContext as VsSeekerRematchViewModel;

        public VsSeekerRematchView()
        {
            InitializeComponent();
        }

        public VsSeekerRematchView(VsSeekerRematchViewModel vm) : this()
        {
            DataContext = vm;
        }

        private void SaveRow_Click(object sender, RoutedEventArgs e) => VM?.SaveCurrentRow();

        private void SaveAll_Click(object sender, RoutedEventArgs e) => VM?.SaveAll();
    }
}
