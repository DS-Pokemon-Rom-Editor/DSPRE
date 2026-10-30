using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels.Shell;

namespace DSPRE.Avalonia.Views.Shell
{
    public partial class ChangesPanel : UserControl
    {
        public ChangesPanel()
        {
            InitializeComponent();
            ApplyMode();
        }

        private bool _open;

        /// <summary>False: the thin strip beside the editors. True: the panel itself, in its own window beside the main one.</summary>
        public bool Open
        {
            get => _open;
            set { _open = value; ApplyMode(); }
        }

        private void ApplyMode()
        {
            StripPart.IsVisible = !_open;
            OpenPart.IsVisible = _open;
        }

        private ChangesViewModel Vm => DataContext as ChangesViewModel;
        private Window Owner => TopLevel.GetTopLevel(this) as Window;

        private void Fold_Click(object sender, RoutedEventArgs e) { if (Vm != null) Vm.PanelOpen = !Vm.PanelOpen; }
        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            if (Vm == null) return;
            await Vm.RefreshAsync();
            await Vm.FetchQuietlyAsync(force: true);
        }
        private async void Commit_Click(object sender, RoutedEventArgs e) => await TrackingActions.CommitAsync(Owner, Vm);
        private async void Pull_Click(object sender, RoutedEventArgs e) => await TrackingActions.PullAsync(Owner, Vm);
        private async void Push_Click(object sender, RoutedEventArgs e) => await TrackingActions.PushAsync(Owner, Vm);
        private void OpenFolder_Click(object sender, RoutedEventArgs e) { if (Vm?.FolderPath != null) SystemShell.OpenWithDefaultApp(Vm.FolderPath); }
        private void Setup_Click(object sender, RoutedEventArgs e) => TrackingActions.ShowSetup(Owner, Vm);

        private void Tour_Click(object sender, RoutedEventArgs e) => EditorTours.Start(this, nameof(ChangesPanel));
        private async void Changelog_Click(object sender, RoutedEventArgs e) { if (DSPRE.Tracking.ChangeTracker.Current is DSPRE.Tracking.ChangeTracker t) await ChangelogWindow.ShowAsync(Owner, t); }
        private async void Apply_Click(object sender, RoutedEventArgs e) => await TrackingActions.ApplyIncomingAsync(Owner, Vm);
        private async void ChangeFolder_Click(object sender, RoutedEventArgs e)
        {
            if (Vm == null || !await DialogHelper.AskTwoWayAsync("Stop tracking into " + Vm.FolderName + " and open another changes folder?", "Change folder", "Choose…", "Cancel", Owner)) return;
            Vm.StopTracking();
            await TrackingActions.OpenFolderAsync(Owner, Vm);
        }

        private async void StopTracking_Click(object sender, RoutedEventArgs e)
        {
            if (Vm == null || !await DialogHelper.AskTwoWayAsync("Stop tracking this project? " + Vm.FolderName + " and its history stay as they are.", "Stop tracking", "Stop tracking", "Cancel", Owner)) return;
            Vm.StopTracking();
        }

        private async void ResolveMerge_Click(object sender, RoutedEventArgs e) => await TrackingActions.ResumeMergeAsync(Owner, Vm);

        private async void AbortMerge_Click(object sender, RoutedEventArgs e)
        {
            if (Vm == null) return;
            await TrackingActions.ReportAsync(Owner, await Vm.AbortMergeAsync(), "Abort merge");
        }

        private async void TakeRepo_Click(object sender, RoutedEventArgs e)
        {
            if (Vm == null) return;
            await TrackingActions.ReportAsync(Owner, await Vm.ApplyIncomingAsync(Vm.Conflicts.Where(c => c.Incoming != null).Select(c => c.Incoming)), "Changes not applied");
        }

        private async void KeepProject_Click(object sender, RoutedEventArgs e)
        {
            if (Vm == null) return;
            await Vm.KeepProjectAsync(Vm.Conflicts.Where(c => c.Incoming != null).Select(c => c.Incoming));
        }

        private void Row_DoubleTapped(object sender, TappedEventArgs e)
        {
            if ((sender as Control)?.DataContext is ChangesViewModel.ChangeRow row) TrackingActions.OpenRecord(Owner, Vm, row);
        }
    }
}
