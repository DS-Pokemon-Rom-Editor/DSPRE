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

        private void Save_Click(object sender, RoutedEventArgs e) => VM?.SaveAll();

        private void Discard_Click(object sender, RoutedEventArgs e) => VM?.DiscardChanges();
    }
}
