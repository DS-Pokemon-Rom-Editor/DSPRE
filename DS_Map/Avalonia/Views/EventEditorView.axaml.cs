using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views
{
    public partial class EventEditorView : UserControl
    {
        private EventEditorViewModel VM => DataContext as EventEditorViewModel;
        private Window OwnerWindow => TopLevel.GetTopLevel(this) as Window;
        private bool _setupDone;
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
                else if (_panning) GlView.PanByDrag((float)(p.X - last.X), (float)(p.Y - last.Y));
                else GlView.OrbitByDrag((float)(p.X - last.X), (float)(p.Y - last.Y));
                _lastPointer = p;
            };
            GlHost.PointerWheelChanged += (s, e) => GlView.ZoomByWheel((float)e.Delta.Y);

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

            // Standalone dirty-close + Ctrl+S/Z/Y come from EditorHostWindow; embedded, the Maps workspace
            // owns the guard — so no EditorWindowChrome here (same as HeaderEditorView).
            vm.MapLoaded += (_, _) => { GlView.SetModel(VM.Model3D); RefreshGizmo(); };
            vm.MarkersChanged += (_, _) => GlView.SetMarkers(VM.MarkerMesh, VM.MarkerVertexCount);
            vm.SpritesChanged += (_, _) => GlView.SetSprites(VM.Sprites);
            vm.EditModeChanged += (_, _) => RefreshGizmo();
            vm.GizmoTargetChanged += (_, _) => RefreshGizmo();
            await vm.SetupAsync(OwnerWindow);
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

        // Diagnostic dump: text report (matrix layout, per-cell map placements, event world positions)
        // + a PNG of the current render, written to a user-chosen folder, for diagnosing stitching issues.
        private async void ExportDebug_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null) return;
            try
            {
                string dir = await DialogHelper.OpenFolder(OwnerWindow, "Choose a folder for the 3D debug dump");
                if (dir == null) return;

                string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string txtPath = System.IO.Path.Combine(dir, $"dspre_event_debug_{stamp}.txt");
                string pngPath = System.IO.Path.Combine(dir, $"dspre_event_render_{stamp}.png");

                System.IO.File.WriteAllText(txtPath, VM.BuildDebugReport());

                // Capture the live GL render (async — fires after the next frame).
                GlView.CaptureFrame((rgba, w, h) =>
                {
                    bool pngOk = false;
                    try { if (rgba != null && w > 0 && h > 0) { SaveRgbaToPng(rgba, w, h, pngPath); pngOk = true; } }
                    catch { pngOk = false; }
                    _ = DialogHelper.ShowInfo(
                        $"3D debug dump written:\n\n• {System.IO.Path.GetFileName(txtPath)}\n" +
                        (pngOk ? $"• {System.IO.Path.GetFileName(pngPath)}" : "• (render capture failed)") +
                        $"\n\nFolder:\n{dir}", "3D debug dump");
                });
            }
            catch (Exception ex) { await DialogHelper.ShowError($"Debug dump failed:\n{ex.Message}", "3D debug dump"); }
        }

        // Builds a PNG from a raw RGBA framebuffer (origin bottom-left → flipped to top-left for the image).
        private static void SaveRgbaToPng(byte[] rgba, int w, int h, string path)
        {
            var bmp = new global::Avalonia.Media.Imaging.WriteableBitmap(
                new PixelSize(w, h), new Vector(96, 96),
                global::Avalonia.Platform.PixelFormats.Rgba8888,
                global::Avalonia.Platform.AlphaFormat.Unpremul);
            using (var fb = bmp.Lock())
            {
                int rowBytes = w * 4;
                for (int y = 0; y < h; y++)
                    System.Runtime.InteropServices.Marshal.Copy(
                        rgba, (h - 1 - y) * rowBytes, fb.Address + y * fb.RowBytes, rowBytes);
            }
            bmp.Save(path);
        }

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

        private static async Task Safe(Task task)
        {
            if (task == null) return;
            try { await task; } catch { }
        }
    }
}
