using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views
{
    /// <summary>Authored as a <see cref="UserControl"/> so it can be embedded as the Map tab in the Maps
    /// workspace; standalone launches host it in an <see cref="EditorHostWindow"/>.</summary>
    public partial class MapEditorView : UserControl
    {
        private MapEditorViewModel VM => DataContext as MapEditorViewModel;
        private bool _setupDone;
        private Point? _lastPointer;
        private bool _panning;
        private bool _painting;       // left-drag painting collision/type onto tiles
        private int _dragAxis = -1;   // gizmo axis being dragged (0=X,1=Y,2=Z), -1 = none

        public MapEditorView()
        {
            InitializeComponent();
            CollisionGrid.IsCollision = true;
            TypeGrid.IsCollision = false;
            CollisionGrid.Changed += (_, _) => { VM?.MarkDirty(); VM?.RebuildOverlay(); };
            TypeGrid.Changed += (_, _) => { VM?.MarkDirty(); VM?.RebuildOverlay(); };

            // Left-drag orbits, right-drag pans, wheel zooms (handled on the host Border).
            // In 3D edit mode a left-press grabs a gizmo axis (to drag the building) or picks a building.
            GlHost.PointerPressed += (s, e) =>
            {
                var pt = e.GetCurrentPoint(GlHost);
                _panning = pt.Properties.IsRightButtonPressed || pt.Properties.IsMiddleButtonPressed;
                _dragAxis = -1; _painting = false;
                if (VM != null && VM.PaintMode)
                {
                    _panning = false;                                // camera is locked to Top while painting
                    if (pt.Properties.IsLeftButtonPressed) { _painting = true; PaintAt(pt.Position); }
                }
                else if (!_panning && VM != null && VM.EditMode3D)
                {
                    var pos = pt.Position;
                    int axis = GlView.HitTestGizmoAxis((float)pos.X, (float)pos.Y);
                    if (axis >= 0) _dragAxis = axis;                 // grab the handle drag moves the building
                    else { int b = PickBuilding(pos); if (b >= 0) VM.SelectedBuildingIndex = b; }
                }
                _lastPointer = pt.Position;
                e.Pointer.Capture(GlHost);
            };
            GlHost.PointerReleased += (s, e) => { _lastPointer = null; _panning = false; _painting = false; _dragAxis = -1; e.Pointer.Capture(null); };
            GlHost.PointerMoved += (s, e) =>
            {
                if (_lastPointer is not Point last) return;
                var p = e.GetPosition(GlHost);
                if (_painting) { PaintAt(p); _lastPointer = p; return; }
                if (VM != null && VM.PaintMode) { _lastPointer = p; return; }   // camera locked while painting
                if (_dragAxis >= 0 && VM != null)
                {
                    float normDelta = GlView.ScreenDragToAxis(_dragAxis, (float)(p.X - last.X), (float)(p.Y - last.Y));
                    float scale = VM.ModelScale; if (scale <= 0) scale = 1f;
                    VM.NudgeSelectedBuildingRaw(_dragAxis, normDelta / scale);
                }
                else if (_panning) GlView.PanByDrag((float)(p.X - last.X), (float)(p.Y - last.Y));
                else GlView.OrbitByDrag((float)(p.X - last.X), (float)(p.Y - last.Y));
                _lastPointer = p;
            };
            GlHost.PointerWheelChanged += (s, e) => GlView.ZoomByWheel((float)e.Delta.Y);

            // Arrow keys pan the matrix view.
            KeyDown += OnKeyDown;

            Loaded += OnLoadedSetup;
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            // In edit mode with a building selected, arrow keys nudge the building one tile along world
            // X/Z (intuitive in the top-down view). Otherwise they pan the camera.
            if (VM != null && VM.EditMode3D && VM.HasBuildingSelected)
            {
                switch (e.Key)
                {
                    case Key.Left:  VM.NudgeSelectedBuildingTiles(-1, 0); e.Handled = true; return;
                    case Key.Right: VM.NudgeSelectedBuildingTiles(1, 0);  e.Handled = true; return;
                    case Key.Up:    VM.NudgeSelectedBuildingTiles(0, -1); e.Handled = true; return;
                    case Key.Down:  VM.NudgeSelectedBuildingTiles(0, 1);  e.Handled = true; return;
                }
            }
            const float step = 24f;
            switch (e.Key)
            {
                case Key.Left:  GlView.PanByScreen(step, 0); e.Handled = true; break;
                case Key.Right: GlView.PanByScreen(-step, 0); e.Handled = true; break;
                case Key.Up:    GlView.PanByScreen(0, step); e.Handled = true; break;
                case Key.Down:  GlView.PanByScreen(0, -step); e.Handled = true; break;
            }
        }

        // ── Camera presets ───────────────────────────────────────────────────────────────
        private void CamTop_Click(object sender, RoutedEventArgs e) => GlView.SetOrientation(0f, 89f);
        private void CamIso_Click(object sender, RoutedEventArgs e) => GlView.SetOrientation(30f, 30f);
        private void CamFront_Click(object sender, RoutedEventArgs e) => GlView.SetOrientation(0f, 8f);
        private void CamSide_Click(object sender, RoutedEventArgs e) => GlView.SetOrientation(90f, 8f);
        private void CamReset_Click(object sender, RoutedEventArgs e) { GlView.SetOrientation(30f, 20f); GlView.ResetView(); }

        public MapEditorView(MapEditorViewModel vm) : this()
        {
            DataContext = vm;
        }

        private async void OnLoadedSetup(object sender, RoutedEventArgs e) => await EnsureSetupAsync();

        /// <summary>
        /// VM setup. No-ops until a ROM is loaded — the embedded Maps-workspace instance is created at
        /// app boot, before any ROM; <see cref="MapsWorkspaceView"/> re-invokes this after EVERY
        /// successful load (including switching to a different ROM mid-session), so
        /// <c>vm.SetupAsync</c> always re-runs — only the event-subscription wiring is one-time.
        /// </summary>
        /// <param name="ownerOverride">Pass the owning Window explicitly when this control may not be
        /// attached to the visual tree yet (e.g. a non-selected TabItem's content in the Maps workspace,
        /// right after a ROM load) — <see cref="TopLevel.GetTopLevel"/> returns null in that case, which
        /// used to make this whole setup silently no-op until the tab was manually visited once.</param>
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
                vm.MapLoaded += OnMapLoaded;
                vm.OverlayChanged += (_, _) =>
                {
                    GlView.SetOverlay(VM.OverlayMesh, VM.OverlayVertexCount);
                    GlView.SetTileTint(VM.TintOn, VM.TintStrength, VM.TintOx, VM.TintOz, VM.TintSx, VM.TintSz, VM.TintRgba);
                };
                vm.PaintModeChanged += (_, _) => { if (VM.PaintMode) GlView.SetOrientation(0f, 89f); };   // lock to Top
                vm.PaintedTile += (_, _) => { CollisionGrid.SetData(VM.Collisions); TypeGrid.SetData(VM.Types); };
                vm.PropertyChanged += OnVmPropertyChanged;
                vm.EditModeChanged += (_, _) => RefreshGizmo();
                vm.GizmoTargetChanged += (_, _) => RefreshGizmo();
            }
            await vm.SetupAsync(owner);
        }

        private void OnMapLoaded(object sender, EventArgs e)
        {
            CollisionGrid.SetData(VM.Collisions);
            TypeGrid.SetData(VM.Types);
            CollisionGrid.PaintValue = VM.CollisionPaintValue;
            TypeGrid.PaintValue = VM.TypePaintValue;
            GlView.SetModel(VM.Model3D);
            RefreshGizmo();
        }

        /// <summary>Syncs the GL control's translate gizmo with the VM's edit mode + selected building.</summary>
        private void RefreshGizmo()
        {
            if (VM == null) return;
            GlView.EditMode = VM.EditMode3D;
            if (VM.EditMode3D && VM.TrySelectedBuildingAnchorNorm(out float nx, out float ny, out float nz))
                GlView.SetGizmoTarget(nx, ny, nz);
            else
                GlView.ClearGizmoTarget();
        }

        /// <summary>Finds the building whose anchor is nearest the screen point (within a pixel radius), or -1.</summary>
        private int PickBuilding(Point p)
        {
            if (VM == null) return -1;
            int best = -1; float bestD = 16f;
            for (int i = 0; i < VM.BuildingCount; i++)
            {
                if (!VM.TryBuildingAnchorNorm(i, out float nx, out float ny, out float nz)) continue;
                if (!GlView.WorldToScreen(nx, ny, nz, out float sx, out float sy)) continue;
                float d = (float)Math.Sqrt((p.X - sx) * (p.X - sx) + (p.Y - sy) * (p.Y - sy));
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        // Paints the tile under the cursor with the current collision/type value (3D paint mode).
        private void PaintAt(Point pos)
        {
            if (VM == null) return;
            if (VM.TryTileAtScreen((float)pos.X, (float)pos.Y,
                    (x, y, z) => { bool k = GlView.WorldToScreen(x, y, z, out float sx, out float sy); return (k, sx, sy); },
                    out int cellIndex, out int col, out int row))
                VM.PaintTile(cellIndex, col, row);
        }

        private void OnVmPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MapEditorViewModel.CollisionPaintValue)) CollisionGrid.PaintValue = VM.CollisionPaintValue;
            else if (e.PropertyName == nameof(MapEditorViewModel.TypePaintValue)) TypeGrid.PaintValue = VM.TypePaintValue;
        }

        private void Gizmos_Toggled(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb) GlView.ShowGizmos = cb.IsChecked == true;
        }

        private void Save_Click(object sender, RoutedEventArgs e) => VM?.Save();
        private async void Import_Click(object sender, RoutedEventArgs e) => await Safe(VM?.ImportAsync());
        private async void Export_Click(object sender, RoutedEventArgs e) => await Safe(VM?.ExportAsync());
        private void AddMap_Click(object sender, RoutedEventArgs e) => VM?.AddMapFile();
        private async void RemMap_Click(object sender, RoutedEventArgs e) => await Safe(VM?.RemoveLastMapFileAsync());
        private void AddBuilding_Click(object sender, RoutedEventArgs e) => VM?.AddBuilding();
        private void RemoveBuilding_Click(object sender, RoutedEventArgs e) => VM?.RemoveBuilding();
        private void DupBuilding_Click(object sender, RoutedEventArgs e) => VM?.DuplicateBuilding();
        private async void ImportBuildings_Click(object sender, RoutedEventArgs e) => await Safe(VM?.ImportBuildingsAsync());
        private async void ExportBuildings_Click(object sender, RoutedEventArgs e) => await Safe(VM?.ExportBuildingsAsync());

        private async void ExportNsbmd_Click(object sender, RoutedEventArgs e) => await Safe(VM?.ExportNsbmdAsync());
        private void ExportDae_Click(object sender, RoutedEventArgs e) => VM?.ExportDae();
        private void ExportGlb_Click(object sender, RoutedEventArgs e) => VM?.ExportGlb();
        private async void ImportTerrain_Click(object sender, RoutedEventArgs e) => await Safe(VM?.ImportTerrainAsync());
        private async void ExportTerrain_Click(object sender, RoutedEventArgs e) => await Safe(VM?.ExportTerrainAsync());
        private async void ImportSound_Click(object sender, RoutedEventArgs e) => await Safe(VM?.ImportSoundAsync());
        private async void ExportSound_Click(object sender, RoutedEventArgs e) => await Safe(VM?.ExportSoundAsync());
        private void BlankSound_Click(object sender, RoutedEventArgs e) => VM?.BlankSound();
        private async void ImportPerms_Click(object sender, RoutedEventArgs e) => await Safe(VM?.ImportPermissionsAsync());
        private async void ExportPerms_Click(object sender, RoutedEventArgs e) => await Safe(VM?.ExportPermissionsAsync());

        private async void ScanTypes_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null) return;
            string report = VM.ScanUsedTypes();
            var clip = TopLevel.GetTopLevel(this)?.Clipboard;
            clip?.SetTextAsync(report);
            await DialogHelper.ShowInfo($"Used types across all maps (copied to clipboard):\n\n{report}", "Used collision types");
        }

        private static async Task Safe(Task task)
        {
            if (task == null) return;
            try { await task; } catch { /* handled in VM */ }
        }
    }
}
