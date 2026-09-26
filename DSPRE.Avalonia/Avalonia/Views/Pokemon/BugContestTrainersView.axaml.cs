using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels.Pokemon;

namespace DSPRE.Avalonia.Views.Pokemon
{
    public partial class BugContestTrainersView : UserControl
    {
        private BugContestTrainersViewModel VM => DataContext as BugContestTrainersViewModel;

        public BugContestTrainersView()
        {
            InitializeComponent();
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            if (VM != null) await VM.SaveAsync();
        }

        private void Locate_Click(object sender, RoutedEventArgs e) => VM?.Locate();
    }
}
