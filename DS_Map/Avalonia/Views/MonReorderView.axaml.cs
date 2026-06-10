using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views
{
    public partial class MonReorderView : Window
    {
        private MonReorderViewModel VM => DataContext as MonReorderViewModel;

        public MonReorderView()
        {
            InitializeComponent();
        }

        public MonReorderView(MonReorderViewModel vm) : this()
        {
            DataContext = vm;
        }

        private void Up_Click(object sender, RoutedEventArgs e) => VM?.MoveUp();
        private void Down_Click(object sender, RoutedEventArgs e) => VM?.MoveDown();

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            VM?.Confirm();
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
    }
}
