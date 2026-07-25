using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views
{
    public partial class AddTrainerClassView : Window
    {
        private AddTrainerClassViewModel VM => DataContext as AddTrainerClassViewModel;

        public AddTrainerClassView()
        {
            InitializeComponent();
        }

        public AddTrainerClassView(AddTrainerClassViewModel vm) : this()
        {
            DataContext = vm;
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null) return;
            if (string.IsNullOrWhiteSpace(VM.ClassName))
            {
                VM.StatusText = "Enter a class name.";
                return;
            }

            VM.Confirm();
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
    }
}
