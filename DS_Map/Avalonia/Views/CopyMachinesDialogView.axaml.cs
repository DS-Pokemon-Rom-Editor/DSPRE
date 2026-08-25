using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views
{
    public partial class CopyMachinesDialogView : Window
    {
        private CopyMachinesDialogViewModel VM => DataContext as CopyMachinesDialogViewModel;

        public CopyMachinesDialogView()
        {
            InitializeComponent();
        }

        public CopyMachinesDialogView(CopyMachinesDialogViewModel vm) : this()
        {
            DataContext = vm;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            VM?.Accept();
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
    }
}
