using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views
{
    public partial class TrainerSearchView : Window
    {
        private TrainerSearchViewModel VM => DataContext as TrainerSearchViewModel;

        public TrainerSearchView()
        {
            InitializeComponent();
        }

        public TrainerSearchView(TrainerSearchViewModel vm) : this()
        {
            DataContext = vm;
        }

        private void Search_Click(object sender, RoutedEventArgs e) => VM?.Search();
        private void Reset_Click(object sender, RoutedEventArgs e) => VM?.Reset();

        private void Search_KeyUp(object sender, KeyEventArgs e)
        {
            if (VM == null) return;
            if (VM.AutoSearch) VM.Search();
            else if (e.Key == Key.Enter) VM.Search();
        }

        private void GoTo_Click(object sender, RoutedEventArgs e)
        {
            if (VM != null && VM.GoTo()) Close();
        }

        private void Result_DoubleTapped(object sender, TappedEventArgs e)
        {
            if (VM != null && VM.GoTo()) Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
    }
}
