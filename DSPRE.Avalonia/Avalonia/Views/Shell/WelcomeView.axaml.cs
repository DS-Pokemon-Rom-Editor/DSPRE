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

        private static WelcomeView _open;

        /// <summary>Opens the Welcome window over the shell without blocking it; asking again brings the open one forward.</summary>
        public static void ShowWelcome(MainWindowView main)
        {
            if (_open != null) { _open.Activate(); return; }
            WelcomeView w = new WelcomeView(main);
            _open = w;
            w.Closed += (_, _) => _open = null;
            if (main != null) w.Show(main);
            else w.Show();
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
        private void Back_Click(object sender, RoutedEventArgs e) => VM?.Back();
        private void Next_Click(object sender, RoutedEventArgs e) => VM?.Next();

        private void Start_Opening(object sender, System.EventArgs e) => Close();
    }
}
