using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views.World
{
    public partial class CameraEditorView : UserControl
    {
        private CameraEditorViewModel VM => DataContext as CameraEditorViewModel;
        private bool _setupDone;

        public CameraEditorView()
        {
            InitializeComponent();
            // Run setup once attached to the visual tree, regardless of whether the
            // VM arrives via the (vm) constructor or via DataContext binding when the
            // control is embedded as a tab in the Avalonia MainWindow.
            Loaded += OnLoadedSetup;
            DetachedFromVisualTree += (_, _) => VM?.Detach();
            AttachedToVisualTree += (_, _) => { if (_setupDone) VM?.Reattach(); };
        }

        public CameraEditorView(CameraEditorViewModel vm) : this()
        {
            DataContext = vm;
        }

        private async void OnLoadedSetup(object sender, RoutedEventArgs e)
        {
            if (_setupDone) return;
            if (Design.IsDesignMode) return;

            CameraEditorViewModel vm = VM;
            if (vm == null) return;
            Window owner = TopLevel.GetTopLevel(this) as Window;
            if (owner == null) return;

            _setupDone = true;
            vm.PropertyChanged += OnVmChanged;
            await vm.SetupAsync(owner);
            WatchRow(vm.SelectedCamera);
            ShowPreview();
        }

        private CameraRowVM _watchedRow;

        private void OnVmChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CameraEditorViewModel.SelectedCamera)) WatchRow(VM?.SelectedCamera);
            if (e.PropertyName is nameof(CameraEditorViewModel.SelectedCamera) or nameof(CameraEditorViewModel.PreviewSpot))
                ShowPreview();
            if (e.PropertyName == nameof(CameraEditorViewModel.PreviewWeatherValue))
                CameraPreviewBox.ShowWeather(VM.PreviewWeatherValue);
        }

        // Edits show as they are typed, before anything is saved.
        private void WatchRow(CameraRowVM row)
        {
            if (_watchedRow != null) _watchedRow.PropertyChanged -= OnRowChanged;
            _watchedRow = row;
            if (_watchedRow != null) _watchedRow.PropertyChanged += OnRowChanged;
        }

        private void OnRowChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) => ShowPreview();

        private void ShowPreview()
        {
            CameraEditorViewModel vm = VM;
            if (vm?.SelectedEntry == null || vm.PreviewSpot is not DSPRE.FlyTable.Spot spot) return;
            CameraPreviewBox.Show(spot.HeaderId, vm.SelectedEntry, (spot.X, spot.Z));
        }

        // ── Toolbar handlers ─────────────────────────────────────────────────
        private async void SaveTable_Click(object sender, RoutedEventArgs e)
            => await RunSafe(() => VM?.SaveAsync());

        private void Discard_Click(object sender, RoutedEventArgs e) => VM?.DiscardChanges();

        private async void ExportTable_Click(object sender, RoutedEventArgs e)
            => await RunSafe(() => VM?.ExportTableAsync());

        private async void ImportTable_Click(object sender, RoutedEventArgs e)
            => await RunSafe(() => VM?.ImportTableAsync());

        private async void Rename_Click(object sender, RoutedEventArgs e)
            => await RunSafe(() => VM?.RenameSelectedAsync());

        private async void ExportCamera_Click(object sender, RoutedEventArgs e)
            => await RunSafe(() => VM?.ExportCameraAsync());

        private async void ImportCamera_Click(object sender, RoutedEventArgs e)
            => await RunSafe(() => VM?.ImportCameraAsync());

        private static async Task RunSafe(System.Func<Task> action)
        {
            if (action == null) return;
            try { await action(); } catch { /* errors handled inside the VM */ }
        }
    }
}
