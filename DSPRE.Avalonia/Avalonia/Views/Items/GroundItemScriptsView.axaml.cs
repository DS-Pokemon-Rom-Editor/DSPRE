using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views.Items
{
    public partial class GroundItemScriptsView : Window
    {
        private GroundItemScriptsViewModel VM => DataContext as GroundItemScriptsViewModel;

        public GroundItemScriptsView()
        {
            InitializeComponent();
        }

        public GroundItemScriptsView(GroundItemScriptsViewModel vm) : this()
        {
            DataContext = vm;
        }

        private void Add_Click(object sender, RoutedEventArgs e) => VM?.AddEntry();

        private void Remove_Click(object sender, RoutedEventArgs e) => VM?.RemoveSelectedEntry();

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
