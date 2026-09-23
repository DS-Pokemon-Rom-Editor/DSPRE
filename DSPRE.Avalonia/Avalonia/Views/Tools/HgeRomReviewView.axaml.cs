using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels.Tools;

namespace DSPRE.Avalonia.Views.Tools
{
    public partial class HgeRomReviewView : Window
    {
        private HgeRomReviewViewModel VM => DataContext as HgeRomReviewViewModel;

        public HgeRomReviewView(HgeRomReviewViewModel vm)
        {
            DataContext = vm;
            InitializeComponent();
            EditorWindowChrome.Attach(this, vm);
        }

        private void Repair_Click(object sender, RoutedEventArgs e) => VM?.StageRepair();

        private void Save_Click(object sender, RoutedEventArgs e) => VM?.SaveChanges();
    }
}
