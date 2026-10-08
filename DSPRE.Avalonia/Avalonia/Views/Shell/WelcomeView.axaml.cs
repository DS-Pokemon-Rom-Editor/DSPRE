using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views.Shell
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
            WelcomeView w = new WelcomeView(main);
            if (main != null) w.ShowDialog(main);
            else w.Show();
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
        private void Back_Click(object sender, RoutedEventArgs e) => VM?.Back();
        private void Next_Click(object sender, RoutedEventArgs e) => VM?.Next();

        private async void OpenRom_Click(object sender, RoutedEventArgs e)
        {
            MainWindowView main = _main;
            Close();
            if (main != null) await main.OpenRomInteractiveAsync();
        }

        private async void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            MainWindowView main = _main;
            Close();
            if (main != null) await main.OpenFolderInteractiveAsync();
        }

        private async void OpenHgEngineFolder_Click(object sender, RoutedEventArgs e)
        {
            MainWindowView main = _main;
            Close();
            if (main != null) await main.OpenHgEngineFolderInteractiveAsync();
        }

        private async void Recent_DoubleTapped(object sender, TappedEventArgs e) => await OpenSelected(sender);

        // The list handles Enter itself, so the handler has to see handled events too.
        private void RecentList_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is not ListBox list || list.Tag is "keys") return;
            list.Tag = "keys";
            list.AddHandler(KeyDownEvent, Recent_KeyDown, RoutingStrategies.Bubble, handledEventsToo: true);
        }

        private async void Recent_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            await OpenSelected(sender);
        }

        private async System.Threading.Tasks.Task OpenSelected(object sender)
        {
            if ((sender as ListBox)?.SelectedItem is not WelcomeViewModel.RecentEntry entry) return;
            MainWindowView main = _main;
            Close();
            if (main != null) await main.OpenRecentAsync(entry.Path);
        }
    }
}
