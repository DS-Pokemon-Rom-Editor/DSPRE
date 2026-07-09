using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views
{
    /// <summary>Authored as a <see cref="UserControl"/> so it can be embedded as the Level Scripts tab in
    /// the Maps workspace; standalone launches host it in an <see cref="EditorHostWindow"/>.</summary>
    public partial class LevelScriptEditorView : UserControl
    {
        private LevelScriptEditorViewModel VM => DataContext as LevelScriptEditorViewModel;
        private bool _setupDone;

        public LevelScriptEditorView()
        {
            InitializeComponent();
            Loaded += OnLoadedSetup;
        }

        public LevelScriptEditorView(LevelScriptEditorViewModel vm) : this() { DataContext = vm; }

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
        private async void Import_Click(object sender, RoutedEventArgs e) => await Safe(VM?.ImportAsync());
        private async void Export_Click(object sender, RoutedEventArgs e) => await Safe(VM?.ExportAsync());
        private void Add_Click(object sender, RoutedEventArgs e) => VM?.AddTrigger();
        private void Remove_Click(object sender, RoutedEventArgs e) => VM?.RemoveTrigger();

        private static async Task Safe(Task task)
        {
            if (task == null) return;
            try { await task; } catch { }
        }
    }
}
