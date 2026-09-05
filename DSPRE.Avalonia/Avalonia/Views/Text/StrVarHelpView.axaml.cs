using Avalonia.Controls;
using Avalonia.Interactivity;

namespace DSPRE.Avalonia.Views.Text
{
    public partial class StrVarHelpView : Window
    {
        public StrVarHelpView(StrVarHelpViewModel viewModel)
        {
            DataContext = viewModel;
            InitializeComponent();
        }

        public StrVarHelpView() : this(new StrVarHelpViewModel()) { }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
