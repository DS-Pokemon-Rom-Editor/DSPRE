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
            HeightGrid.RampByValue = true;

            MapGrid.Changed += (_, _) => VM?.MarkDirty();
            HeaderGrid.Changed += (_, _) => VM?.MarkDirty();
            HeightGrid.Changed += (_, _) => VM?.MarkDirty();
            MapGrid.CellSelected += (_, e) => SetCellInfo("Map", e);
            HeaderGrid.CellSelected += (_, e) =>
            {
                SetCellInfo("Header", e);
                // Out of paint mode a header cell is a way to that header.
                if (VM != null && !VM.PaintMode && e.value != 65535) VM.OpenHeader(e.value);
            };
            MapGrid.CellActivated += (_, e) => { if (e.value != 65535) AvaloniaEditorLauncher.OpenMapEditor(e.value); };
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

        private async void SetSpawn_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null || !VM.InBounds || AvaloniaEditorLauncher.Refused("SpawnEditorView")) return;
            if (VM.SpawnHeaderNumber is not ushort header)
            {
                await DialogHelper.ShowError(
                    "This matrix has no header section, and no single header uses it.\n\nOpen the matrix from the header the spawn belongs to.",
                    "No header for this cell");
                return;
            }
            var names = HeaderLists.GetHeaderListBoxNames();
            new SpawnEditorView(null, names, header, VM.SelCol, VM.SelRow).ShowManaged();
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
            // A matrix without a section clears its grid, which would otherwise keep the previous matrix's size.
            if (VM.HasHeaders) HeaderGrid.SetSource(VM.Width, VM.Height, VM.GetHeader, VM.SetHeader);
            else HeaderGrid.SetSource(0, 0, null, null);
            if (VM.HasHeights) HeightGrid.SetSource(VM.Width, VM.Height, VM.GetHeight, VM.SetHeight);
            else HeightGrid.SetSource(0, 0, null, null);
            ApplyFocus();
        }

        // Outlines the current header's cells in every grid and scrolls the visible one to them.
        private void ApplyFocus()
        {
            foreach (var g in new[] { MapGrid, HeaderGrid, HeightGrid })
            {
                g.HeaderAt = VM.HeaderOfCell;
                g.CellColour = VM.CellColour;
                g.FocusHeader = VM.FocusHeader;
                g.PaintMode = VM.PaintMode;
            }
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                foreach (var g in new[] { MapGrid, HeaderGrid, HeightGrid })
                    if (g.Parent is ScrollViewer sv && g.FocusBounds() is global::Avalonia.Rect b)
                        sv.Offset = new global::Avalonia.Vector(
                            System.Math.Max(0, b.Center.X - sv.Viewport.Width / 2),
                            System.Math.Max(0, b.Center.Y - sv.Viewport.Height / 2));
            }, global::Avalonia.Threading.DispatcherPriority.Background);
        }

        private void OnVmChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(MatrixEditorViewModel.MapPaint): MapGrid.PaintValue = (int)VM.MapPaint; break;
                case nameof(MatrixEditorViewModel.HeaderPaint): HeaderGrid.PaintValue = (int)VM.HeaderPaint; break;
                case nameof(MatrixEditorViewModel.HeightPaint): HeightGrid.PaintValue = (int)VM.HeightPaint; break;
                case nameof(MatrixEditorViewModel.PaintMode):
                case nameof(MatrixEditorViewModel.FocusHeader): ApplyFocus(); break;
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
