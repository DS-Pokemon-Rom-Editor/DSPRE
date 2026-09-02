using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views.Text
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

        private async void OnLoadedSetup(object sender, RoutedEventArgs e)
        {
            // TabControl raises Loaded again whenever this embedded tab is revisited. ROM loads call
            // EnsureSetupAsync(owner) explicitly, so automatic attachment only needs to bootstrap once.
            if (!_setupDone) await EnsureSetupAsync();
        }

        /// <summary>
        /// VM setup. No-ops until a ROM is loaded: the embedded Maps-workspace instance is created at
        /// app boot, before any ROM; <see cref="MapsWorkspaceView"/> re-invokes this after EVERY
        /// successful load (including switching ROMs mid-session), so <c>vm.SetupAsync</c> always re-runs.
        /// </summary>
        /// <param name="ownerOverride">Pass the owning Window explicitly when this control may not be
        /// attached to the visual tree yet (a non-selected TabItem's content in the Maps workspace,
        /// right after a ROM load), since <see cref="TopLevel.GetTopLevel"/> returns null in that case.</param>
        public async Task EnsureSetupAsync(Window ownerOverride = null)
        {
            if (Design.IsDesignMode) return;
            var vm = VM;
            if (vm == null || !AvaloniaEditorLauncher.IsRomLoaded) return;
            var owner = ownerOverride ?? TopLevel.GetTopLevel(this) as Window;
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
