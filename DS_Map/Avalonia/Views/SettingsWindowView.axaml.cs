using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views
{
    public partial class SettingsWindowView : Window
    {
        private SettingsWindowViewModel VM => (SettingsWindowViewModel)DataContext;

        public SettingsWindowView()
        {
            AvaloniaXamlLoader.Load(this);
            DataContext = new SettingsWindowViewModel();
            Closing += OnWindowClosing;
        }

        private async void OnWindowClosing(object sender, WindowClosingEventArgs e)
        {
            e.Cancel = true;
            if (await VM.ConfirmCloseAsync())
            {
                Closing -= OnWindowClosing;
                Close();
            }
        }

        private async void Save_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
            => await VM.SaveCommand(this);

        private async void ChangeExportPath_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
            => await VM.ChangeExportPathCommand(this);

        private async void ChangeMapImportPath_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
            => await VM.ChangeMapImportPathCommand(this);

        private async void ChangeOpenDefaultRom_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
            => await VM.ChangeOpenDefaultRomCommand(this);

        private void ClearExportPath_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
            => VM.ClearExportPath();

        private void ClearMapImportPath_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
            => VM.ClearMapImportPath();

        private void ClearOpenDefaultRom_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
            => VM.ClearOpenDefaultRom();

        private void CheckForUpdates_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
            => VM.CheckForUpdates();

        private void ShowWelcome_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            var main = (global::Avalonia.Application.Current?.ApplicationLifetime
                as global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow as MainWindowView;
            WelcomeView.ShowWelcome(main);
        }

        private void CheckDBUpdates_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
            => VM.CheckDBUpdates();
    }
}
