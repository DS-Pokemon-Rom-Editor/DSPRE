using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views.Tools
{
    public partial class ProjectChecksView : Window
    {
        private ProjectChecksViewModel VM => DataContext as ProjectChecksViewModel;

        public ProjectChecksView()
        {
            InitializeComponent();
            if (!Design.IsDesignMode) DataContext = new ProjectChecksViewModel();
        }

        private void Run_Click(object sender, RoutedEventArgs e) => VM?.RunValidation();
        private void Find_Click(object sender, RoutedEventArgs e) => VM?.Find();
    }
}
