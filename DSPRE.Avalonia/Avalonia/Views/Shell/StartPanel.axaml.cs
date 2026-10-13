using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels.Shell;

namespace DSPRE.Avalonia.Views.Shell
{
    /// <summary>
    /// The open buttons and recent projects, shared by the Welcome window and the main window's empty state so the
    /// two offer the same ways in.
    /// </summary>
    public partial class StartPanel : UserControl
    {
        /// <summary>Raised just before something is opened, so a window hosting the panel can close first.</summary>
        public event EventHandler Opening;

        public StartPanel()
        {
            InitializeComponent();
        }

        // The main window itself, or the one that owns the Welcome window.
        private MainWindowView Main => TopLevel.GetTopLevel(this) switch
        {
            MainWindowView main => main,
            Window window => window.Owner as MainWindowView,
            _ => null,
        };

        private async Task Open(Func<MainWindowView, Task> open)
        {
            MainWindowView main = Main;
            Opening?.Invoke(this, EventArgs.Empty);
            if (main != null) await open(main);
        }

        private async void OpenRom_Click(object sender, RoutedEventArgs e) => await Open(m => m.OpenRomInteractiveAsync());
        private async void OpenFolder_Click(object sender, RoutedEventArgs e) => await Open(m => m.OpenFolderInteractiveAsync());
        private async void OpenHgEngineFolder_Click(object sender, RoutedEventArgs e) => await Open(m => m.OpenHgEngineFolderInteractiveAsync());

        private async void Recent_DoubleTapped(object sender, TappedEventArgs e) => await OpenSelected(sender);

        // The list handles Enter itself, so the handler has to see handled events too.
        private void RecentList_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is not ListBox list || list.Tag is "keys") return;
            list.Tag = "keys";
            list.AddHandler(KeyDownEvent, Recent_KeyDown, RoutingStrategies.Bubble, handledEventsToo: true);
        }

        private async void Recent_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            await OpenSelected(sender);
        }

        private async Task OpenSelected(object sender)
        {
            if ((sender as ListBox)?.SelectedItem is not RecentProjectsViewModel.RecentEntry entry) return;
            await Open(m => m.OpenRecentAsync(entry.Path));
        }
    }
}
