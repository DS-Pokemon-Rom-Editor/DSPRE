using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels.Shell;

namespace DSPRE.Avalonia.Views.Shell
{
    public partial class TrackingBar : UserControl
    {
        public TrackingBar()
        {
            InitializeComponent();
        }

        private ChangesViewModel Vm => DataContext as ChangesViewModel;
        private Window Owner => TopLevel.GetTopLevel(this) as Window;

        private async void Start_Click(object sender, RoutedEventArgs e) => await TrackingActions.StartAsync(Owner, Vm);
        private async void Open_Click(object sender, RoutedEventArgs e) => await TrackingActions.OpenFolderAsync(Owner, Vm);
        private async void Clone_Click(object sender, RoutedEventArgs e) => await TrackingActions.CloneAsync(Owner, Vm);
        private void Setup_Click(object sender, RoutedEventArgs e) => TrackingActions.ShowSetup(Owner, Vm);
        private void Decline_Click(object sender, RoutedEventArgs e) => Vm?.Decline();
        private void About_Click(object sender, RoutedEventArgs e) =>
            WelcomeView.ShowWelcome(Owner as MainWindowView, WelcomeViewModel.TrackChangesPage);
        private async void Apply_Click(object sender, RoutedEventArgs e) => await TrackingActions.ApplyIncomingAsync(Owner, Vm);
        private void Dismiss_Click(object sender, RoutedEventArgs e) { if (Vm != null) Vm.Message = null; }

        private void Summary_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (Vm == null || e.Source is Button) return;
            Vm.PanelOpen = !Vm.PanelOpen;
        }
    }
}
