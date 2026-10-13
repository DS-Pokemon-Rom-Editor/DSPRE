using Avalonia.Controls;
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
            ProjectDrop.Attach(this, () => _main, Close);
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

        private void Start_Opening(object sender, System.EventArgs e) => Close();
    }
}
