using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using DSPRE.Avalonia.Gl;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views
{
    /// <summary>Authored as a <see cref="UserControl"/> so it can be embedded as the Map tab in the Maps
    /// workspace; standalone launches host it in an <see cref="EditorHostWindow"/>.</summary>
    public partial class MapEditorView : UserControl
    {
        private MapEditorViewModel VM => DataContext as MapEditorViewModel;
        private bool _setupDone;
        private Gl3DPointerNavigation _nav;

        public MapEditorView()
        {
            InitializeComponent();
            CollisionGrid.IsCollision = true;
            TypeGrid.IsCollision = false;
            CollisionGrid.Changed += (_, _) => { VM?.MarkDirty(); VM?.RebuildOverlay(); };
            TypeGrid.Changed += (_, _) => { VM?.MarkDirty(); VM?.RebuildOverlay(); };

            // Left-drag pans, right-drag orbits, wheel zooms. In 3D edit mode a left-press grabs a
            // gizmo axis (to drag the building) or picks a building. See Gl3DPointerNavigation.
            _nav = new Gl3DPointerNavigation(GlHost, GlView)
            {
                IsPaintModeActive = () => VM?.PaintMode == true,
                PaintAt = PaintAt,
                IsEditModeActive = () => VM?.EditMode3D == true,
                Pick = pos => { int b = PickBuilding(pos); if (b >= 0 && VM != null) VM.SelectedBuildingIndex = b; },
                NudgeAxis = (axis, normDelta) =>
                {
                    if (VM == null) return;
                    float scale = VM.ModelScale; if (scale <= 0) scale = 1f;
                    VM.NudgeSelectedBuildingRaw(axis, normDelta / scale);
                },
            };

            // Arrow keys pan the camera / nudge a selected building, but only while the 3D
            // viewport itself has keyboard focus (Gl3DPointerNavigation focuses it on click);
            // otherwise they'd steal input from a focused dropdown/spinner in the side panel.
            GlHost.KeyDown += OnKeyDown;

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

        private async void OnLoadedSetup(object sender, RoutedEventArgs e)
        {
            if (!_setupDone) await EnsureSetupAsync();
        }

        /// <summary>
        /// VM setup. No-ops until a ROM is loaded; the embedded Maps-workspace instance is created at
        /// app boot, before any ROM; <see cref="MapsWorkspaceView"/> re-invokes this after EVERY
        /// successful load (including switching to a different ROM mid-session), so
        /// <c>vm.SetupAsync</c> always re-runs; only the event-subscription wiring is one-time.
        /// </summary>
        /// <param name="ownerOverride">Pass the owning Window explicitly when this control may not be
        /// attached to the visual tree yet (e.g. a non-selected TabItem's content in the Maps workspace,
        /// right after a ROM load); <see cref="TopLevel.GetTopLevel"/> returns null in that case, which
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
                GlView.ShowTextures = vm.ShowTextures;
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
            else if (e.PropertyName == nameof(MapEditorViewModel.ShowTextures)) GlView.ShowTextures = VM.ShowTextures;
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
