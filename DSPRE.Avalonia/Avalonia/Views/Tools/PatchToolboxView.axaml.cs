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
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Control c && c.DataContext is PatchRowViewModel row)
                VM?.Apply(row);
        }
    }
}
