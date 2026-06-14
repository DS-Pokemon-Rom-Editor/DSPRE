using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views
{
    public partial class EventEditorView : Window
    {
        private EventEditorViewModel VM => DataContext as EventEditorViewModel;
        private bool _setupDone;
        private bool _closeConfirmed;
        private Point? _lastPointer;
        private bool _panning;
        private int _dragAxis = -1;   // gizmo axis being dragged (0=X,1=Y,2=Z), -1 = none

        public EventEditorView()
        {
            InitializeComponent();

            // Left-drag orbits, right-drag pans, wheel zooms. In 3D edit mode a left-press grabs a
            // gizmo axis (to drag the event) or picks the nearest event.
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
                    else PickEvent(pos);
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
                    VM.NudgeSelectedEventRaw(_dragAxis, normDelta / scale);
                }
                else if (_panning) GlView.PanByScreen(-(float)(p.X - last.X), -(float)(p.Y - last.Y));
                else { GlView.Yaw += (float)(p.X - last.X) * 0.5f; GlView.Pitch += (float)(p.Y - last.Y) * 0.5f; }
                _lastPointer = p;
            };
            GlHost.PointerWheelChanged += (s, e) => GlView.Distance -= (float)e.Delta.Y * 0.4f;

            KeyDown += (s, e) =>
            {
                // In edit mode with an event selected, arrow keys nudge it one tile along X/Z; else pan.
                if (VM != null && VM.EditMode3D && VM.HasSelectedEvent)
                {
                    switch (e.Key)
                    {
                        case Key.Left:  VM.NudgeSelectedEventTiles(-1, 0); e.Handled = true; return;
                        case Key.Right: VM.NudgeSelectedEventTiles(1, 0);  e.Handled = true; return;
                        case Key.Up:    VM.NudgeSelectedEventTiles(0, -1); e.Handled = true; return;
                        case Key.Down:  VM.NudgeSelectedEventTiles(0, 1);  e.Handled = true; return;
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
            };

            Loaded += OnLoadedSetup;
        }

        public EventEditorView(EventEditorViewModel vm) : this() { DataContext = vm; }

        private async void OnLoadedSetup(object sender, RoutedEventArgs e)
        {
            if (_setupDone || Design.IsDesignMode) return;
            var vm = VM;
            if (vm == null) return;
            _setupDone = true;

            DSPRE.Avalonia.EditorWindowChrome.Attach(this, vm);
            vm.MapLoaded += (_, _) => { GlView.SetModel(VM.Model3D); RefreshGizmo(); };
            vm.MarkersChanged += (_, _) => GlView.SetMarkers(VM.MarkerMesh, VM.MarkerVertexCount);
            vm.SpritesChanged += (_, _) => GlView.SetSprites(VM.Sprites);
            vm.EditModeChanged += (_, _) => RefreshGizmo();
            vm.GizmoTargetChanged += (_, _) => RefreshGizmo();
            await vm.SetupAsync(this);
        }

        /// <summary>Syncs the GL control's translate gizmo with the VM's edit mode + selected event.</summary>
        private void RefreshGizmo()
        {
            if (VM == null) return;
            GlView.EditMode = VM.EditMode3D;
            if (VM.EditMode3D && VM.TrySelectedEventAnchorNorm(out float nx, out float ny, out float nz))
                GlView.SetGizmoTarget(nx, ny, nz);
            else
                GlView.ClearGizmoTarget();
        }

        /// <summary>Selects the event whose anchor is nearest the screen point (within a pixel radius).</summary>
        private void PickEvent(Point p)
        {
            if (VM == null) return;
            int bestType = -1, bestIdx = -1; float bestD = 18f;
            foreach (var (type, index, nx, ny, nz) in VM.EventAnchorsNorm())
            {
                if (!GlView.WorldToScreen(nx, ny, nz, out float sx, out float sy)) continue;
                float d = (float)Math.Sqrt((p.X - sx) * (p.X - sx) + (p.Y - sy) * (p.Y - sy));
                if (d < bestD) { bestD = d; bestType = type; bestIdx = index; }
            }
            if (bestType >= 0) VM.SelectEvent(bestType, bestIdx);
        }

        private void Gizmos_Toggled(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb) GlView.ShowGizmos = cb.IsChecked == true;
        }

        private void CamTop_Click(object sender, RoutedEventArgs e) => GlView.SetOrientation(0f, 89f);
        private void CamIso_Click(object sender, RoutedEventArgs e) => GlView.SetOrientation(30f, 30f);
        private void CamReset_Click(object sender, RoutedEventArgs e) { GlView.SetOrientation(30f, 20f); GlView.ResetView(); }

        private void Save_Click(object sender, RoutedEventArgs e) => VM?.Save();
        private async void Import_Click(object sender, RoutedEventArgs e) => await Safe(VM?.ImportAsync());
        private async void Export_Click(object sender, RoutedEventArgs e) => await Safe(VM?.ExportAsync());

        private void AddOw_Click(object sender, RoutedEventArgs e) => VM?.AddOverworld();
        private void RemoveOw_Click(object sender, RoutedEventArgs e) => VM?.RemoveOverworld();
        private void DupOw_Click(object sender, RoutedEventArgs e) => VM?.DuplicateOverworld();
        private void SortAsc_Click(object sender, RoutedEventArgs e) => VM?.SortOverworldsAsc();
        private void SortDesc_Click(object sender, RoutedEventArgs e) => VM?.SortOverworldsDesc();
        private void AddWarp_Click(object sender, RoutedEventArgs e) => VM?.AddWarp();
        private void RemoveWarp_Click(object sender, RoutedEventArgs e) => VM?.RemoveWarp();
        private void DupWarp_Click(object sender, RoutedEventArgs e) => VM?.DuplicateWarp();
        private void TestWarp_Click(object sender, RoutedEventArgs e) => VM?.TestWarp();
        private void AddTrig_Click(object sender, RoutedEventArgs e) => VM?.AddTrigger();
        private void RemoveTrig_Click(object sender, RoutedEventArgs e) => VM?.RemoveTrigger();
        private void DupTrig_Click(object sender, RoutedEventArgs e) => VM?.DuplicateTrigger();
        private void AddSpawn_Click(object sender, RoutedEventArgs e) => VM?.AddSpawnable();
        private void RemoveSpawn_Click(object sender, RoutedEventArgs e) => VM?.RemoveSpawnable();
        private void DupSpawn_Click(object sender, RoutedEventArgs e) => VM?.DuplicateSpawnable();
        private void AddFile_Click(object sender, RoutedEventArgs e) => VM?.AddEventFile();
        private async void RemFile_Click(object sender, RoutedEventArgs e) => await Safe(VM?.RemoveLastEventFileAsync());

        protected override async void OnClosing(WindowClosingEventArgs e)
        {
            if (VM != null && VM.HasUnsavedChanges && !_closeConfirmed)
            {
                e.Cancel = true;
                bool discard = await DialogHelper.AskYesNo($"Discard unsaved changes to {VM.UnsavedChangesDescription}?", "Unsaved Changes");
                if (discard) { _closeConfirmed = true; VM.DiscardChanges(); Close(); }
                return;
            }
            base.OnClosing(e);
        }

        private static async Task Safe(Task task)
        {
            if (task == null) return;
            try { await task; } catch { }
        }
    }
}
