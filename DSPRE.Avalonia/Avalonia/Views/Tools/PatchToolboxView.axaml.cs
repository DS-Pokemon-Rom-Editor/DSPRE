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
            PatchList.SizeChanged += (_, _) => FitColumns();
            PatchList.LayoutUpdated += (_, _) => FitColumns();
        }

        // One column per 420 pixels of list, up to three, so the cards stay readable as the window widens.
        private void FitColumns()
        {
            if (PatchList.ItemsPanelRoot is not global::Avalonia.Controls.Primitives.UniformGrid grid) return;
            int columns = System.Math.Clamp((int)(PatchList.Bounds.Width / 420), 1, 3);
            if (grid.Columns != columns) grid.Columns = columns;
        }

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

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Control c && c.DataContext is PatchRowViewModel row)
                VM?.Apply(row);
        }
    }
}
