using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DSPRE.Avalonia.Gl;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views
{
    public partial class HeadbuttEncounterView : Window
    {
        private HeadbuttEncounterViewModel VM => DataContext as HeadbuttEncounterViewModel;
        private bool _setupDone;
        private Gl3DPointerNavigation _nav;

        public HeadbuttEncounterView()
        {
            InitializeComponent();

            // Left-drag pans, right-drag orbits, wheel zooms. In edit mode a left-press grabs a gizmo
            // axis (to drag the tree) or picks the nearest tree. See Gl3DPointerNavigation.
            _nav = new Gl3DPointerNavigation(GlHost, GlView)
            {
                IsEditModeActive = () => VM?.EditMode3D == true,
                BeginGizmoDrag = () => VM?.BeginGizmoDrag(),
                Pick = PickTree,
                NudgeAxis = (axis, normDelta) =>
                {
                    if (VM == null) return;
                    float scale = VM.ModelScale; if (scale <= 0) scale = 1f;
                    VM.NudgeSelectedTreeRaw(axis, normDelta / scale);
                },
            };

            // Arrow keys nudge the selected tree, but only while the 3D viewport itself has
            // keyboard focus (Gl3DPointerNavigation focuses it on click) — otherwise they'd steal
            // input from a focused dropdown/spinner in the side panel.
            GlHost.KeyDown += (s, e) =>
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
