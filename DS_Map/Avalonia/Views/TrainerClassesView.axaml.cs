using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views
{
    public partial class TrainerClassesView : Window
    {
        private TrainerClassesViewModel VM => DataContext as TrainerClassesViewModel;

        public TrainerClassesView()
        {
            InitializeComponent();
        }

        public TrainerClassesView(TrainerClassesViewModel vm) : this()
        {
            DataContext = vm;
        }

        private void Save_Click(object sender, RoutedEventArgs e) => VM?.Save();
    }
}
