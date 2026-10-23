using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views.Tools
{
    /// <summary>
    /// ROM patch toolbox: every patch with its state; the ROM writes live in <c>PatchToolboxDialog</c>.
    /// </summary>
    public partial class PatchToolboxView : Window
    {
        private PatchToolboxViewModel VM => DataContext as PatchToolboxViewModel;

        public PatchToolboxView()
        {
            InitializeComponent();
            if (!Design.IsDesignMode)
            {
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
                await DialogHelper.ShowCopyableTextAsync(VM.CreditsText(), "Credits", this);
        }

        private async void OpenLink_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Control c && c.DataContext is PatchRowViewModel { Link: { } link })
                await Launcher.LaunchUriAsync(new System.Uri(link));
        }

        private async void Guide_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Control c && c.DataContext is PatchRowViewModel { Guide: { } guide })
                await Launcher.LaunchUriAsync(new System.Uri(guide));
        }

        private async void Notes_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Control c && c.DataContext is PatchRowViewModel row)
                await DialogHelper.ShowInfoAsync(row.Notes, row.Title);
        }

        private void Requires_Click(object sender, RoutedEventArgs e)
        {
            // The search may be narrowed to this patch, which would hide the one it needs.
            if (sender is Control c && c.DataContext is PatchRowViewModel { RequiresKey: { } key }) VM?.ShowOnly(key);
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Control c && c.DataContext is PatchRowViewModel row)
                VM?.Apply(row);
        }
    }
}
