using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views
{
    public partial class SpriteImportWizardView : Window
    {
        private SpriteImportWizardViewModel VM => (SpriteImportWizardViewModel)DataContext;

        public SpriteImportWizardView() => InitializeComponent();

        public SpriteImportWizardView(SpriteImportWizardViewModel vm) : this()
        {
            DataContext = vm;
        }

        private void ModeImage_Click(object sender, RoutedEventArgs e) => VM.Mode = "image";
        private void ModePalette_Click(object sender, RoutedEventArgs e) => VM.Mode = "palette";
        private void ModeFull_Click(object sender, RoutedEventArgs e) => VM.Mode = "full";

        private void FaceBack_Click(object sender, RoutedEventArgs e) => VM.FaceMode = "Back";
        private void FaceFront_Click(object sender, RoutedEventArgs e) => VM.FaceMode = "Front";
        private void FaceBoth_Click(object sender, RoutedEventArgs e) => VM.FaceMode = "Both";

        private void GenderFemale_Click(object sender, RoutedEventArgs e) => VM.GenderMode = "Female";
        private void GenderMale_Click(object sender, RoutedEventArgs e) => VM.GenderMode = "Male";
        private void GenderBoth_Click(object sender, RoutedEventArgs e) => VM.GenderMode = "Both";

        private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

        private async void Run_Click(object sender, RoutedEventArgs e)
        {
            IsEnabled = false;
            try { await VM.RunAsync(); }
            finally { IsEnabled = true; }
            Close();
        }
    }
}
