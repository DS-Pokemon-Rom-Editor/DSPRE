using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views.Shell
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
            => await VM.SaveCommandAsync(this);

        private async void ChangeExportPath_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
            => await VM.ChangeExportPathCommandAsync(this);

        private async void ChangeMapImportPath_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
            => await VM.ChangeMapImportPathCommandAsync(this);

        private async void ChangeOpenDefaultRom_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
            => await VM.ChangeOpenDefaultRomCommandAsync(this);

        private void ClearExportPath_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
            => VM.ClearExportPath();

        private void ClearMapImportPath_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
            => VM.ClearMapImportPath();

        private void ClearOpenDefaultRom_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
            => VM.ClearOpenDefaultRom();

        private async void ChangeEmulatorPath_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
            => await VM.ChangeEmulatorPathCommandAsync(this);

        private void ClearEmulatorPath_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
            => VM.ClearEmulatorPath();

        private async void ChangeGitPath_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
            => await VM.ChooseToolPathAsync(this, "git", p => VM.GitPath = p);

        private void ClearGitPath_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e) => VM.GitPath = "";

        private async void ChangeGhPath_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
            => await VM.ChooseToolPathAsync(this, "gh", p => VM.GitHubCliPath = p);

        private void ClearGhPath_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e) => VM.GitHubCliPath = "";

        private async void ChooseTestSave_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
            => await VM.ChooseTestSaveCommandAsync(this);

        private void ClearTestSave_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
            => VM.TestSavePath = "";

        private void CheckForUpdates_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
            => VM.CheckForUpdates();

        private void ShowWelcome_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            MainWindowView main = (global::Avalonia.Application.Current?.ApplicationLifetime
                as global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow as MainWindowView;
            WelcomeView.ShowWelcome(main);
        }

        private void CheckDBUpdates_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
            => VM.CheckDBUpdates();
    }
}
