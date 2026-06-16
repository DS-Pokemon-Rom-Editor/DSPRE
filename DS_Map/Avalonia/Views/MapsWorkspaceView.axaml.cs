using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views
{
    /// <summary>
    /// Maps workspace: the shared header sidebar + a context strip + map-bound tabs, all over one
    /// <see cref="HeaderEditorViewModel"/>. The Header tab embeds the real fields; the other tabs open
    /// the standalone editor at the selected header's linked file id (phase 1).
    /// </summary>
    public partial class MapsWorkspaceView : UserControl
    {
        private HeaderEditorViewModel VM => DataContext as HeaderEditorViewModel;
        private bool _setupDone;

        public MapsWorkspaceView()
        {
            InitializeComponent();
            Loaded += OnLoadedSetup;
        }

        private async void OnLoadedSetup(object sender, RoutedEventArgs e)
        {
            if (_setupDone || Design.IsDesignMode) return;
            var vm = VM;
            if (vm == null) return;
            var owner = TopLevel.GetTopLevel(this) as Window;
            if (owner == null) return;
            _setupDone = true;
            await vm.SetupAsync(owner);
            owner.Activated += (_, _) => vm.ReloadLocationNames();
        }

        private void Save_Click(object sender, RoutedEventArgs e) => VM?.Save();
        private void Reset_Click(object sender, RoutedEventArgs e) => VM?.Reset();

        // Placeholder tabs → open the standalone editor at the selected header's linked file.
        private void OpenMap_Click(object sender, RoutedEventArgs e) => AvaloniaEditorLauncher.OpenMapEditor();
        private void OpenMatrix_Click(object sender, RoutedEventArgs e) => VM?.OpenMatrix();
        private void OpenAreaData_Click(object sender, RoutedEventArgs e) => VM?.OpenAreaData();
        private void OpenEvents_Click(object sender, RoutedEventArgs e) => VM?.OpenEvents();
        private void OpenScripts_Click(object sender, RoutedEventArgs e) => VM?.OpenScripts();
        private void OpenLevelScripts_Click(object sender, RoutedEventArgs e) => VM?.OpenLevelScripts();
        private void OpenTexts_Click(object sender, RoutedEventArgs e) => VM?.OpenTexts();
        private void OpenEncounters_Click(object sender, RoutedEventArgs e) => VM?.OpenEncounters();
    }
}
