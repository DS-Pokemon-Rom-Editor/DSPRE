using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views
{
    /// <summary>Authored as a <see cref="UserControl"/> so it can be embedded as the Area Data tab in the
    /// Maps workspace; standalone launches host it in an <see cref="EditorHostWindow"/>.</summary>
    public partial class AreaDataEditorView : UserControl
    {
        private AreaDataEditorViewModel VM => DataContext as AreaDataEditorViewModel;
        private bool _setupDone;

        public AreaDataEditorView()
        {
            InitializeComponent();
            Loaded += OnLoadedSetup;
        }

        public AreaDataEditorView(AreaDataEditorViewModel vm) : this() { DataContext = vm; }

        private async void OnLoadedSetup(object sender, RoutedEventArgs e) => await EnsureSetupAsync();

        /// <summary>
        /// One-time VM setup. No-ops until a ROM is loaded — the embedded Maps-workspace instance is
        /// created at app boot, before any ROM; <see cref="MapsWorkspaceView"/> re-invokes this after a load.
        /// </summary>
        public async Task EnsureSetupAsync()
        {
            if (_setupDone || Design.IsDesignMode) return;
            var vm = VM;
            if (vm == null || !AvaloniaEditorLauncher.IsRomLoaded) return;
            var owner = TopLevel.GetTopLevel(this) as Window;
            if (owner == null) return;
            _setupDone = true;
            await vm.SetupAsync(owner);
        }

        private void Save_Click(object sender, RoutedEventArgs e) => VM?.Save();

        private void Undo_Click(object sender, RoutedEventArgs e) => VM?.Undo();
        private void Redo_Click(object sender, RoutedEventArgs e) => VM?.Redo();
    }
}
