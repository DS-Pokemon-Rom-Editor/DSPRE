using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views.Pokemon
{
    public partial class DVCalcNatureViewerView : Window
    {
        private DVCalcNatureViewerViewModel VM => DataContext as DVCalcNatureViewerViewModel;

        public DVCalcNatureViewerView()
        {
            InitializeComponent();
        }

        public DVCalcNatureViewerView(DVCalcNatureViewerViewModel vm) : this()
        {
            DataContext = vm;
        }

        private void Select_Click(object sender, RoutedEventArgs e)
        {
            if (VM != null && VM.ConfirmSelection()) Close();
        }

        private void Row_DoubleTapped(object sender, TappedEventArgs e)
        {
            if (VM != null && VM.ConfirmSelection()) Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
    }
}
