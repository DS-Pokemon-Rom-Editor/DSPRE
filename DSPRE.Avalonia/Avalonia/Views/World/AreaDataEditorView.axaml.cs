using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views.World
{
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

        private async void OnLoadedSetup(object sender, RoutedEventArgs e)
        {
            if (!_setupDone) await EnsureSetupAsync();
        }

        /// <summary>Sets up the view model; see <see cref="MapsWorkspaceView"/>.</summary>
        /// <param name="ownerOverride">The owning window, for a tab not yet in the visual tree.</param>
        public async Task EnsureSetupAsync(Window ownerOverride = null)
        {
            if (Design.IsDesignMode) return;
            AreaDataEditorViewModel vm = VM;
            if (vm == null || !AvaloniaEditorLauncher.IsRomLoaded) return;
            Window owner = ownerOverride ?? TopLevel.GetTopLevel(this) as Window;
            if (owner == null) return;
            _setupDone = true;
            await vm.SetupAsync(owner);
        }

        private void Save_Click(object sender, RoutedEventArgs e) => VM?.Save();
        private async void Add_Click(object sender, RoutedEventArgs e) { if (VM != null) await VM.AddAsync(); }
        private async void Import_Click(object sender, RoutedEventArgs e) { if (VM != null) await VM.ImportAsync(TopLevel.GetTopLevel(this) as Window); }
        private async void Export_Click(object sender, RoutedEventArgs e) { if (VM != null) await VM.ExportAsync(TopLevel.GetTopLevel(this) as Window); }
        private async void RemoveLast_Click(object sender, RoutedEventArgs e) { if (VM != null) await VM.RemoveLastAsync(); }
        private void OpenMapTextures_Click(object sender, RoutedEventArgs e) => VM?.OpenTextures(false);
        private void OpenBuildingTextures_Click(object sender, RoutedEventArgs e) => VM?.OpenTextures(true);

        private void Undo_Click(object sender, RoutedEventArgs e) => VM?.Undo();
        private void Redo_Click(object sender, RoutedEventArgs e) => VM?.Redo();

        private async void Discard_Click(object sender, RoutedEventArgs e)
        {
            if (await DialogHelper.ConfirmDiscardAsync(this)) (DataContext as DSPRE.Editors.IEditorWithUnsavedChanges)?.DiscardChanges();
        }
    }
}
