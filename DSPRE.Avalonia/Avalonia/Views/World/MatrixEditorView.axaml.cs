using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views.World
{
    /// <summary>Authored as a <see cref="UserControl"/> so it can be embedded as the Matrix tab in the
    /// Maps workspace; standalone launches host it in an <see cref="EditorHostWindow"/>.</summary>
    public partial class MatrixEditorView : UserControl
    {
        private MatrixEditorViewModel VM => DataContext as MatrixEditorViewModel;
        private bool _setupDone;

        public MatrixEditorView()
        {
            InitializeComponent();
            MapGrid.ColorByValue = true;
            HeaderGrid.ColorByValue = true;
            HeightGrid.ColorByValue = false;

            MapGrid.Changed += (_, _) => VM?.MarkDirty();
            HeaderGrid.Changed += (_, _) => VM?.MarkDirty();
            HeightGrid.Changed += (_, _) => VM?.MarkDirty();
            MapGrid.CellSelected += (_, e) => SetCellInfo("Map", e);
            HeaderGrid.CellSelected += (_, e) => SetCellInfo("Header", e);
            HeightGrid.CellSelected += (_, e) => SetCellInfo("Height", e);

            Loaded += OnLoadedSetup;
        }

        public MatrixEditorView(MatrixEditorViewModel vm) : this() { DataContext = vm; }

        private void SetCellInfo(string which, (int col, int row, int value) e)
        {
            if (VM == null) return;
            VM.CellInfo = $"{which} [{e.col}, {e.row}] = {e.value}";
            VM.SetSelectedCell(e.col, e.row);
        }

        private void SetSpawn_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null || !VM.InBounds) return;
            var names = HeaderLists.GetHeaderListBoxNames();
            new SpawnEditorView(null, names, VM.SpawnHeaderNumber, VM.SelCol, VM.SelRow).ShowManaged();
        }

        private async void OnLoadedSetup(object sender, RoutedEventArgs e)
        {
            if (!_setupDone) await EnsureSetupAsync();
        }

        /// <summary>
        /// VM setup. No-ops until a ROM is loaded; the embedded Maps-workspace instance is created at
        /// app boot, before any ROM; <see cref="MapsWorkspaceView"/> re-invokes this after EVERY
        /// successful load (including switching ROMs mid-session), so <c>vm.SetupAsync</c> always
        /// re-runs; only the event-subscription wiring is one-time.
        /// </summary>
        /// <param name="ownerOverride">Pass the owning Window explicitly when this control may not be
        /// attached to the visual tree yet (a non-selected TabItem's content in the Maps workspace,
        /// right after a ROM load); <see cref="TopLevel.GetTopLevel"/> returns null in that case.</param>
        public async Task EnsureSetupAsync(Window ownerOverride = null)
        {
            if (Design.IsDesignMode) return;
            var vm = VM;
            if (vm == null || !AvaloniaEditorLauncher.IsRomLoaded) return;
            var owner = ownerOverride ?? TopLevel.GetTopLevel(this) as Window;
            if (owner == null) return;
            if (!_setupDone)
            {
                _setupDone = true;
                vm.MatrixLoaded += OnMatrixLoaded;
                vm.PropertyChanged += OnVmChanged;
            }
            await vm.SetupAsync(owner);
        }

        private void OnMatrixLoaded(object sender, EventArgs e)
        {
            MapGrid.PaintValue = (int)VM.MapPaint;
            HeaderGrid.PaintValue = (int)VM.HeaderPaint;
            HeightGrid.PaintValue = (int)VM.HeightPaint;
            MapGrid.SetSource(VM.Width, VM.Height, VM.GetMap, VM.SetMap);
            if (VM.HasHeaders) HeaderGrid.SetSource(VM.Width, VM.Height, VM.GetHeader, VM.SetHeader);
            if (VM.HasHeights) HeightGrid.SetSource(VM.Width, VM.Height, VM.GetHeight, VM.SetHeight);
        }

        private void OnVmChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(MatrixEditorViewModel.MapPaint): MapGrid.PaintValue = (int)VM.MapPaint; break;
                case nameof(MatrixEditorViewModel.HeaderPaint): HeaderGrid.PaintValue = (int)VM.HeaderPaint; break;
                case nameof(MatrixEditorViewModel.HeightPaint): HeightGrid.PaintValue = (int)VM.HeightPaint; break;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e) => VM?.Save();
        private void AddHeaders_Click(object sender, RoutedEventArgs e) => VM?.AddHeaderSection();
        private void AddHeights_Click(object sender, RoutedEventArgs e) => VM?.AddHeightsSection();
        private async void Import_Click(object sender, RoutedEventArgs e) => await Safe(VM?.ImportAsync());
        private async void Export_Click(object sender, RoutedEventArgs e) => await Safe(VM?.ExportAsync());

        private static async Task Safe(Task task)
        {
            if (task == null) return;
            try { await task; } catch { }
        }
    }
}
