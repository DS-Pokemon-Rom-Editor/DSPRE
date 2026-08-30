using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using DSPRE.Avalonia.Gl;
using DSPRE.Avalonia.ViewModels;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.Views
{
    /// <summary>
    /// Authored as a <see cref="UserControl"/> so it can be embedded as the Events tab in the Maps
    /// workspace; standalone launches (<see cref="AvaloniaEditorLauncher.OpenEventEditor"/>) host it
    /// in an <see cref="EditorHostWindow"/>. Do not change the base type back to Window: Avalonia
    /// windows cannot be re-parented as a child control (embedding one throws "already has a visual
    /// parent TopLevelHost").
    /// </summary>
    public partial class EventEditorView : UserControl
    {
        private EventEditorViewModel VM => DataContext as EventEditorViewModel;
        private bool _setupDone;
        private Gl3DPointerNavigation _nav;

        public EventEditorView()
        {
            InitializeComponent();

            // Left-drag pans, right-drag orbits, wheel zooms. In 3D edit mode a left-press grabs a
            // gizmo axis (to drag the event) or picks the nearest event. See Gl3DPointerNavigation.
            _nav = new Gl3DPointerNavigation(GlHost, GlView)
            {
                IsEditModeActive = () => VM?.EditMode3D == true,
                BeginGizmoDrag = () => VM?.BeginGizmoDrag(),
                Pick = PickEvent,
                NudgeAxis = (axis, normDelta) =>
                {
                    if (VM == null) return;
                    float scale = VM.ModelScale; if (scale <= 0) scale = 1f;
                    VM.NudgeSelectedEventRaw(axis, normDelta / scale);
                },
            };

            // Arrow keys nudge the selected event / pan the camera, but only while the 3D
            // viewport itself has keyboard focus (Gl3DPointerNavigation focuses it on click);
            // otherwise they'd steal input from a focused dropdown/spinner in the side panel.
            GlHost.KeyDown += (s, e) =>
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
            if (!_setupDone) await EnsureSetupAsync();
        }

        /// <summary>
        /// VM setup. No-ops until a ROM is loaded; the embedded Maps-workspace instance is created at
        /// app boot, before any ROM. <see cref="MapsWorkspaceView"/> re-invokes this after EVERY
        /// successful load (including switching ROMs mid-session), so <c>vm.SetupAsync</c> always
        /// re-runs, only the event-subscription wiring is one-time.
        /// </summary>
        /// <param name="ownerOverride">Pass the owning Window explicitly when this control may not be
        /// attached to the visual tree yet (a non-selected TabItem's content in the Maps workspace,
        /// right after a ROM load), since <see cref="TopLevel.GetTopLevel"/> returns null in that case.</param>
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
                // No EditorWindowChrome here (it requires a Window): the toolbar's own Save button covers
                // saving, and when hosted standalone, EditorHostWindow already guards unsaved changes on
                // close via this VM's IEditorWithUnsavedChanges. When embedded in the Maps workspace tab,
                // the workspace's own Save/Reset handles it instead.
                vm.MapLoaded += (_, _) => { GlView.SetModel(VM.Model3D); RefreshGizmo(); LoadPegmanIcon(); };
                vm.WalkTileChanged += (_, _) =>
                    GlView.SetTileTint(VM.WalkTintOn, VM.WalkTintStrength, VM.WalkTintOx, VM.WalkTintOz,
                                       VM.WalkTintSx, VM.WalkTintSz, VM.WalkTintRgba);
                vm.MarkersChanged += (_, _) => GlView.SetMarkers(VM.MarkerMesh, VM.MarkerVertexCount);
                vm.SpritesChanged += (_, _) => GlView.SetSprites(VM.Sprites);
                vm.EditModeChanged += (_, _) => RefreshGizmo();
                vm.ViewModeChanged += (_, _) => ApplyViewMode();
                vm.GizmoTargetChanged += (_, _) => RefreshGizmo();
            }
            await vm.SetupAsync(owner);
            ApplyViewMode();
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

        /// <summary>2D locks the camera flat and drops perspective; 3D restores a normal orbit camera.</summary>
        private void ApplyViewMode()
        {
            if (VM == null) return;
            GlView.Orthographic = VM.Flat2D;
            if (VM.Flat2D) GlView.SetOrientation(0f, 90f);
        }

        private void CamTop_Click(object sender, RoutedEventArgs e) => GlView.SetOrientation(0f, 89f);
        private void CamIso_Click(object sender, RoutedEventArgs e) => GlView.SetOrientation(30f, 30f);
        private void CamReset_Click(object sender, RoutedEventArgs e) { GlView.SetOrientation(30f, 20f); GlView.ResetView(); }

        private async void CenterOnEvent_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null) return;
            if (!VM.TrySelectedEventAnchorNorm(out float nx, out float ny, out float nz))
            {
                await DialogHelper.ShowInfo("Select an event first.", "Centre on event");
                return;
            }
            GlView.LookAt(nx, ny, nz);
        }

        private async void AnimatedPreview_Click(object sender, RoutedEventArgs e)
        {
            if (VM?.Model3D == null)
            {
                await DialogHelper.ShowError("Load an event file with a map first, so there is something to animate.", "Animated preview");
                return;
            }
            var owner = TopLevel.GetTopLevel(this) as Window;
            var win = new AnimatedPreviewWindow();
            win.ShowFor(owner, VM.Model3D, VM.Area, VM.Events, ow => VM.EventFoot(ow), VM.Collision,
                        (x, z) => VM.TileFoot(x, z), n => VM.WalkerFor(n),
                        n => VM.WalkerStartId(n), n => VM.ScriptHome(n), VM.CameraId,
                        VM.MusicDayId, VM.MusicNightId, n => VM.ActionsFor(n), LoadLevelScripts(VM.LevelScriptId), VM.GatherStringVars());
        }

        /// <summary>
        /// The map's level script file, or null when there is none to read. A header can point at a
        /// file that turns out to be an ordinary script, which just means the map runs nothing by itself.
        /// </summary>
        private static LevelScriptFile LoadLevelScripts(int id)
        {
            if (id < 0) return null;
            try
            {
                var file = new LevelScriptFile(id);
                return file.bufferSet != null && file.bufferSet.Count > 0 ? file : null;
            }
            catch { return null; }
        }

        // ── dragging the player onto the map ─────────────────────────────────────────
        // Pick the little player up off the toolbar, drop them anywhere on the map, and the walk starts
        // there. The same idea as dropping the figure onto a street map.

        private bool _pegDragging;
        private (int x, int z)? _pegTile;
        // Collision is worked out afresh every time it is asked for, so it is taken once when the drag
        // starts rather than twice for every twitch of the pointer. Where each tile lands on screen is
        // worked out once too: a header that stitches the whole matrix has about three hundred thousand
        // tiles, and walking all of them per twitch took longer than a frame.
        private MapCollisionGrid _pegMap;
        private FieldTilePicker.Prepared _pegTiles;

        /// <summary>The player's own sprite, so the thing you drag is who you will be walking as.</summary>
        private void LoadPegmanIcon()
        {
            try
            {
                var pix = OverworldSprites.Get(AnimatedPreviewViewModel.PlayerSpriteEntry, 1);
                if (pix == null || pix.Width <= 0 || pix.Height <= 0) return;
                var bmp = ToBitmap(pix);
                PegmanIcon.Source = bmp;
                DragGhost.Source = bmp;
            }
            catch { }
        }

        private static Bitmap ToBitmap(OverworldSprites.SpritePixels pix)
        {
            var wb = new WriteableBitmap(new PixelSize(pix.Width, pix.Height), new Vector(96, 96),
                                         global::Avalonia.Platform.PixelFormat.Rgba8888,
                                         global::Avalonia.Platform.AlphaFormat.Unpremul);
            using (var fb = wb.Lock())
            {
                for (int y = 0; y < pix.Height; y++)
                    System.Runtime.InteropServices.Marshal.Copy(
                        pix.Rgba, y * pix.Width * 4,
                        fb.Address + y * fb.RowBytes, pix.Width * 4);
            }
            return wb;
        }

        private void Pegman_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (VM?.Model3D == null) return;
            _pegDragging = true;
            _pegTile = null;
            _pegMap = VM.Collision;
            _pegTiles = FieldTilePicker.Prepared.Build(_pegMap, (x, z) => VM.TileFoot(x, z),
                (x, y, z) => GlView.WorldToScreen(x, y, z, out float px, out float py) ? (px, py) : ((float, float)?)null);
            e.Pointer.Capture(Pegman);
            e.Handled = true;
        }

        private void Pegman_PointerMoved(object sender, PointerEventArgs e)
        {
            if (!_pegDragging) return;

            // Where the pointer is over the map itself, which is what the projection is measured in.
            var overMap = e.GetPosition(GlView);
            var overOverlay = e.GetPosition(DragOverlay);

            DragGhost.IsVisible = true;
            Canvas.SetLeft(DragGhost, overOverlay.X - DragGhost.Width / 2);
            Canvas.SetTop(DragGhost, overOverlay.Y - DragGhost.Height);

            bool onMap = overMap.X >= 0 && overMap.Y >= 0
                      && overMap.X <= GlView.Bounds.Width && overMap.Y <= GlView.Bounds.Height;

            if (!onMap)
            {
                _pegTile = null;
                VM.WalkTile = null;
                DropLabel.IsVisible = false;
                return;
            }

            var under = _pegTiles?.Nearest(overMap.X, overMap.Y);
            _pegTile = under == null ? null
                     : FieldTilePicker.NearestFree(_pegMap, under.Value.x, under.Value.z);

            DropLabel.IsVisible = true;
            Canvas.SetLeft(DropLabel, overOverlay.X + 16);
            Canvas.SetTop(DropLabel, overOverlay.Y + 8);

            // The tile itself is painted on the map, the same way the grid paints one.
            VM.WalkTile = _pegTile;

            DropText.Text = _pegTile != null
                ? $"Walk from tile {_pegTile.Value.x}, {_pegTile.Value.z}"
                : under == null ? "Not over the map" : "Nowhere to stand here";
        }

        private void Pegman_PointerReleased(object sender, PointerReleasedEventArgs e)
        {
            if (!_pegDragging) return;
            _pegDragging = false;
            e.Pointer.Capture(null);
            DragGhost.IsVisible = false;
            DropLabel.IsVisible = false;
            _pegMap = null;
            _pegTiles = null;
            if (VM != null) VM.WalkTile = null;

            var tile = _pegTile;
            _pegTile = null;
            if (tile == null) return;          // let go somewhere off the map, so nothing happens

            StartWalkAt(tile.Value.x, tile.Value.z);
        }

        /// <summary>Opens the preview walking from a tile.</summary>
        private void StartWalkAt(int tileX, int tileZ)
        {
            var owner = TopLevel.GetTopLevel(this) as Window;
            var win = new AnimatedPreviewWindow();
            win.ShowFor(owner, VM.Model3D, VM.Area, VM.Events, ow => VM.EventFoot(ow), VM.Collision,
                        (x, z) => VM.TileFoot(x, z), n => VM.WalkerFor(n),
                        n => VM.WalkerStartId(n), n => VM.ScriptHome(n), VM.CameraId,
                        VM.MusicDayId, VM.MusicNightId, n => VM.ActionsFor(n), LoadLevelScripts(VM.LevelScriptId), VM.GatherStringVars());
            win.StepInOn(tileX, tileZ);
        }

        /// <summary>
        /// Opens the preview already standing next to the selected event, so you can talk to that person
        /// or walk onto that trigger without hunting for them first.
        /// </summary>
        private async void StepInHere_Click(object sender, RoutedEventArgs e)
        {
            if (VM?.Model3D == null)
            {
                await DialogHelper.ShowError("Load an event file with a map first, so there is something to animate.", "Step in here");
                return;
            }
            var tile = VM.SelectedEventTile;
            if (tile == null)
            {
                await DialogHelper.ShowInfo("Select an event first, and the walk will start next to it.", "Step in here");
                return;
            }

            var owner = TopLevel.GetTopLevel(this) as Window;
            var win = new AnimatedPreviewWindow();
            win.ShowFor(owner, VM.Model3D, VM.Area, VM.Events, ow => VM.EventFoot(ow), VM.Collision,
                        (x, z) => VM.TileFoot(x, z), n => VM.WalkerFor(n),
                        n => VM.WalkerStartId(n), n => VM.ScriptHome(n), VM.CameraId,
                        VM.MusicDayId, VM.MusicNightId, n => VM.ActionsFor(n), LoadLevelScripts(VM.LevelScriptId), VM.GatherStringVars());
            win.StepInBeside(tile.Value.x, tile.Value.z);
        }

        private async void Screenshot_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string path = await DialogHelper.SaveFile(TopLevel.GetTopLevel(this) as Window,
                    "Save map view screenshot",
                    new[] { new FilePickerFileType("PNG image") { Patterns = new[] { "*.png" } } },
                    $"dspre_events_{DateTime.Now:yyyyMMdd_HHmmss}.png");
                if (path == null) return;

                GlView.CaptureFrame((rgba, w, h) =>
                {
                    try
                    {
                        if (rgba == null || w <= 0 || h <= 0) throw new Exception("the view returned no pixels");
                        SaveRgbaToPng(rgba, w, h, path);
                        if (VM != null) VM.StatusText = $"Screenshot saved to {path}";
                    }
                    catch (Exception ex) { _ = DialogHelper.ShowError($"Could not save the screenshot:\n{ex.Message}", "Screenshot"); }
                });
            }
            catch (Exception ex) { await DialogHelper.ShowError($"Could not save the screenshot:\n{ex.Message}", "Screenshot"); }
        }

        private void Save_Click(object sender, RoutedEventArgs e) => VM?.Save();
        private async void Import_Click(object sender, RoutedEventArgs e) => await Safe(VM?.ImportAsync());
        private async void Export_Click(object sender, RoutedEventArgs e) => await Safe(VM?.ExportAsync());

        private async void ManageGroundItems_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null) return;

            var dlgVm = new GroundItemScriptsViewModel();
            var dlg = new GroundItemScriptsView(dlgVm);
            var owner = TopLevel.GetTopLevel(this) as Window;
            if (owner != null) await dlg.ShowDialog(owner);
            else dlg.Show();

            if (dlgVm.Changed) VM.RefreshOwItemEntries();
        }

        // Diagnostic dump: text report (matrix layout, per-cell map placements, event world positions)
        // + a PNG of the current render, written to a user-chosen folder, for diagnosing stitching issues.
        private async void ExportDebug_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null) return;
            try
            {
                string dir = await DialogHelper.OpenFolder(TopLevel.GetTopLevel(this) as Window, "Choose a folder for the 3D debug dump");
                if (dir == null) return;

                string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string txtPath = System.IO.Path.Combine(dir, $"dspre_event_debug_{stamp}.txt");
                string pngPath = System.IO.Path.Combine(dir, $"dspre_event_render_{stamp}.png");

                System.IO.File.WriteAllText(txtPath, VM.BuildDebugReport());

                // Capture the live GL render (async, fires after the next frame).
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
            bmp.Save(path, PngBitmapEncoderOptions.Default);
        }

        private void GoToOwScript_Click(object sender, RoutedEventArgs e) => VM?.GoToOverworldScript();
        private void GoToTrScript_Click(object sender, RoutedEventArgs e) => VM?.GoToTriggerScript();
        private void GoToSpScript_Click(object sender, RoutedEventArgs e) => VM?.GoToSpawnableScript();

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
