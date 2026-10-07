using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views.Tools
{
    /// <summary>
    /// Native Avalonia ROM Patch Toolbox. Lists every patch with its applied/supported state and an
    /// Apply button; the actual ROM-writing logic lives in the shared <c>PatchToolboxDialog</c>
    /// static methods so this and the WinForms dialog stay byte-identical.
    /// </summary>
    public partial class PatchToolboxView : Window
    {
        private PatchToolboxViewModel VM => DataContext as PatchToolboxViewModel;

        public PatchToolboxView()
        {
            InitializeComponent();
            if (!Design.IsDesignMode)
            {
                // Route the shared apply-logic's prompts through native Avalonia dialogs (no WinForms UI).
                PatchDialogs.Install();
                DataContext = new PatchToolboxViewModel();
            }
            SizeChanged += (_, _) => FitColumns();
            Opened += (_, _) => FitColumns();
        }

        // One column per 420 pixels of window, up to three, so the cards stay readable as the window widens.
        private void FitColumns() => VM?.SetColumns(System.Math.Clamp((int)(Bounds.Width / 420), 1, 3));

        private async void GenerateCredits_Click(object sender, RoutedEventArgs e)
        {
            if (VM != null)
                await DialogHelper.ShowCopyableText(VM.CreditsText(), "Credits", this);
        }

        private async void OpenLink_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Control c && c.DataContext is PatchRowViewModel { Link: { } link })
                await Launcher.LaunchUriAsync(new System.Uri(link));
        }

        private async void Notes_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Control c && c.DataContext is PatchRowViewModel row)
                await DialogHelper.ShowInfo(row.Notes, row.Title);
        }

        private void Requires_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Control c && c.DataContext is PatchRowViewModel { RequiresTab: { } tab }) VM?.GoToTab(tab);
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Control c && c.DataContext is PatchRowViewModel row)
                VM?.Apply(row);
        }
    }
}
