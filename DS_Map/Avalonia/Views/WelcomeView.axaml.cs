using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views
{
    public partial class WelcomeView : Window
    {
        private WelcomeViewModel VM => DataContext as WelcomeViewModel;
        private MainWindowView _main;

        public WelcomeView()
        {
            InitializeComponent();
        }

        public WelcomeView(MainWindowView main) : this()
        {
            _main = main;
            DataContext = new WelcomeViewModel();
        }

        /// <summary>Opens the Welcome window (over the main shell window when available).</summary>
        public static void ShowWelcome(MainWindowView main)
        {
            var w = new WelcomeView(main);
            if (main != null) w.ShowDialog(main);
            else w.Show();
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
        private void Back_Click(object sender, RoutedEventArgs e) => VM?.Back();
        private void Next_Click(object sender, RoutedEventArgs e) => VM?.Next();

        private async void OpenRom_Click(object sender, RoutedEventArgs e)
        {
            var main = _main;
            Close();
            if (main != null) await main.OpenRomInteractiveAsync();
        }

        private async void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            var main = _main;
            Close();
            if (main != null) await main.OpenFolderInteractiveAsync();
        }

        private async void LinkHgEngine_Click(object sender, RoutedEventArgs e)
        {
            if (_main == null || !AvaloniaEditorLauncher.IsRomLoaded)
            {
                await DialogHelper.ShowInfo(
                    "Open a ROM project first (Open ROM or Open extracted folder), then link its hg-engine checkout from here or File > Link hg-engine checkout…",
                    "Open a project first");
                return;
            }
            Close();
            AvaloniaEditorLauncher.OpenHgEngineLink();
        }

        private async void Recent_DoubleTapped(object sender, TappedEventArgs e)
        {
            if (RecentList.SelectedItem is not string path) return;
            var main = _main;
            Close();
            if (main != null) await main.OpenRecentAsync(path);
        }
    }
}
