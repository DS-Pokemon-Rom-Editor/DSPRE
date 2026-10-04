using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels.Tools;

namespace DSPRE.Avalonia.Views.Tools
{
    public partial class HgEngineSettingsView : Window
    {
        private HgEngineSettingsViewModel VM => DataContext as HgEngineSettingsViewModel;

        public HgEngineSettingsView() : this(new HgEngineSettingsViewModel()) { }

        public HgEngineSettingsView(HgEngineSettingsViewModel vm)
        {
            DataContext = vm;
            InitializeComponent();
            DSPRE.Avalonia.EditorWindowChrome.Attach(this, vm);
        }

        private void Save_Click(object sender, RoutedEventArgs e) => VM?.SaveChanges();
        private void Discard_Click(object sender, RoutedEventArgs e) => VM?.DiscardChanges();
    }
}
