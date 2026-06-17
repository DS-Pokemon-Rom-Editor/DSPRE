using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views
{
    public partial class HeadbuttEncounterView : Window
    {
        private HeadbuttEncounterViewModel VM => DataContext as HeadbuttEncounterViewModel;
        private bool _setupDone;
        private Point? _lastPointer;
        private bool _panning;
        private int _dragAxis = -1;   // gizmo axis being dragged (0=X,1=Y,2=Z), -1 = none

        public HeadbuttEncounterView()
        {
            InitializeComponent();

            // Left-drag orbits, right-drag pans, wheel zooms. In edit mode a left-press grabs a gizmo
            // axis (to drag the tree) or picks the nearest tree.
            GlHost.PointerPressed += (s, e) =>
            {
                var pt = e.GetCurrentPoint(GlHost);
                _panning = pt.Properties.IsRightButtonPressed || pt.Properties.IsMiddleButtonPressed;
                _dragAxis = -1;
                if (!_panning && VM != null && VM.EditMode3D)
                {
                    var pos = pt.Position;
                    int axis = GlView.HitTestGizmoAxis((float)pos.X, (float)pos.Y);
                    if (axis >= 0) { _dragAxis = axis; VM.BeginGizmoDrag(); }
                    else PickTree(pos);
                }
                _lastPointer = pt.Position;
                e.Pointer.Capture(GlHost);
            };
            GlHost.PointerReleased += (s, e) => { _lastPointer = null; _panning = false; _dragAxis = -1; e.Pointer.Capture(null); };
            GlHost.PointerMoved += (s, e) =>
            {
                if (_lastPointer is not Point last) return;
                var p = e.GetPosition(GlHost);
                if (_dragAxis >= 0 && VM != null)
                {
                    float normDelta = GlView.ScreenDragToAxis(_dragAxis, (float)(p.X - last.X), (float)(p.Y - last.Y));
                    float scale = VM.ModelScale; if (scale <= 0) scale = 1f;
                    VM.NudgeSelectedTreeRaw(_dragAxis, normDelta / scale);
                }
                else if (_panning) GlView.PanByDrag((float)(p.X - last.X), (float)(p.Y - last.Y));
                else GlView.OrbitByDrag((float)(p.X - last.X), (float)(p.Y - last.Y));
                _lastPointer = p;
            };
            GlHost.PointerWheelChanged += (s, e) => GlView.ZoomByWheel((float)e.Delta.Y);

            KeyDown += (s, e) =>
            {
                if (VM != null && VM.EditMode3D && VM.HasSelectedTree)
                {
                    switch (e.Key)
                    {
                        case Key.Left:  VM.NudgeSelectedTreeTiles(-1, 0); e.Handled = true; break;
                        case Key.Right: VM.NudgeSelectedTreeTiles(1, 0);  e.Handled = true; break;
                        case Key.Up:    VM.NudgeSelectedTreeTiles(0, -1); e.Handled = true; break;
                        case Key.Down:  VM.NudgeSelectedTreeTiles(0, 1);  e.Handled = true; break;
                    }
                }
            };

            Loaded += OnLoadedSetup;
        }

        public HeadbuttEncounterView(HeadbuttEncounterViewModel vm) : this() { DataContext = vm; }

        private async void OnLoadedSetup(object sender, RoutedEventArgs e)
        {
            if (_setupDone || Design.IsDesignMode) return;
            var vm = VM;
            if (vm == null) return;
            _setupDone = true;
            DSPRE.Avalonia.EditorWindowChrome.Attach(this, vm);
            vm.MapLoaded += (_, _) => { GlView.SetModel(VM.Model3D); RefreshGizmo(); };
            vm.MarkersChanged += (_, _) => GlView.SetMarkers(VM.MarkerMesh, VM.MarkerVertexCount);
            vm.EditModeChanged += (_, _) => RefreshGizmo();
            vm.GizmoTargetChanged += (_, _) => RefreshGizmo();
            await vm.SetupAsync(this);
        }

        private void RefreshGizmo()
        {
            if (VM == null) return;
            GlView.EditMode = VM.EditMode3D;
            if (VM.EditMode3D && VM.TrySelectedTreeAnchorNorm(out float nx, out float ny, out float nz))
                GlView.SetGizmoTarget(nx, ny, nz);
            else
                GlView.ClearGizmoTarget();
        }

        private void PickTree(Point p)
        {
            if (VM == null) return;
            int best = -1; float bestD = 18f;
            foreach (var (index, nx, ny, nz) in VM.TreeAnchorsNorm())
            {
                if (!GlView.WorldToScreen(nx, ny, nz, out float sx, out float sy)) continue;
                float d = (float)Math.Sqrt((p.X - sx) * (p.X - sx) + (p.Y - sy) * (p.Y - sy));
                if (d < bestD) { bestD = d; best = index; }
            }
            if (best >= 0) VM.SelectedTreeIndex = best;
        }

        private void Save_Click(object sender, RoutedEventArgs e) => VM?.Save();
        private void AddTree_Click(object sender, RoutedEventArgs e) => VM?.AddTree();
        private void RemoveTree_Click(object sender, RoutedEventArgs e) => VM?.RemoveSelectedTree();
        private void CamTop_Click(object sender, RoutedEventArgs e) => GlView.SetOrientation(0f, 89f);
        private void CamIso_Click(object sender, RoutedEventArgs e) => GlView.SetOrientation(30f, 30f);
        private void CamReset_Click(object sender, RoutedEventArgs e) { GlView.SetOrientation(30f, 20f); GlView.ResetView(); }
    }
}
