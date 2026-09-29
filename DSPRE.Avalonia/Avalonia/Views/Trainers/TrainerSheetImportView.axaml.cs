using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels.Trainers;

namespace DSPRE.Avalonia.Views.Trainers
{
    public partial class TrainerSheetImportView : Window
    {
        private TrainerSheetImportViewModel VM => (TrainerSheetImportViewModel)DataContext;

        public TrainerSheetImportView() => InitializeComponent();

        public TrainerSheetImportView(TrainerSheetImportViewModel vm) : this()
        {
            DataContext = vm;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => Close(false);

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            if (VM.CanApply) Close(true);
        }
    }
}
