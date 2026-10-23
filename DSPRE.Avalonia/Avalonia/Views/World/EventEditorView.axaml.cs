using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using DSPRE.Avalonia.Gl;
using DSPRE.Avalonia.ViewModels;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.Views.World
{
    public partial class EventEditorView : UserControl
    {
        private EventEditorViewModel VM => DataContext as EventEditorViewModel;
        private bool _setupDone;
        private Gl3DPointerNavigation _nav;

        public EventEditorView()
        {
            InitializeComponent();
            DSPRE.Avalonia.Controls.SidePanelWidth.Remember(SplitGrid, () => SettingsManager.Settings.EventEditorSidePanelWidth,
                w => SettingsManager.Settings.EventEditorSidePanelWidth = w);

            // Left-drag pans, right-drag orbits, wheel zooms, a click picks an event. In 3D edit mode a left-press grabs a
            // gizmo axis (to drag the event) or picks the nearest event. See Gl3DPointerNavigation.
            _nav = new Gl3DPointerNavigation(GlHost, GlView)
            {
                IsEditModeActive = () => VM?.EditMode3D == true,
                BeginGizmoDrag = () => VM?.BeginGizmoDrag(),
                Pick = PickEvent,
                // A click that does not drag selects an event outside Move too.
                Click = PickEvent,
                NudgeAxis = (axis, normDelta) =>
                {
                    if (VM == null) return;
                    float scale = VM.ModelScale; if (scale <= 0) scale = 1f;
                    VM.NudgeSelectedEventRaw(axis, normDelta / scale);
                },
            };

            // Pointer input arrives on the host border, not the GL control, and the projection is
            // measured in that same space (Gl3DPointerNavigation picks events the same way).
            GlHost.PointerMoved += (s, e) =>
            {
                Point pos = e.GetPosition(GlHost);
                VM?.UpdateHoverCoords(pos.X, pos.Y,
                    (x, y, z) => { bool k = GlView.WorldToScreen(x, y, z, out float sx, out float sy); return (k, sx, sy); });
            };
            GlHost.PointerExited += (s, e) => VM?.ClearHoverCoords();

            // Arrow keys act only while the 3D view has focus, so side-panel inputs keep theirs.
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
            Loaded += (_, _) => VM?.Attach();
            Unloaded += (_, _) => VM?.Detach();
        }

        public EventEditorView(EventEditorViewModel vm) : this() { DataContext = vm; }

        private async void OnLoadedSetup(object sender, RoutedEventArgs e)
        {
            if (!_setupDone) await EnsureSetupAsync();
        }

        /// <summary>Sets up the view model; see <see cref="MapsWorkspaceView"/>.</summary>
        /// <param name="ownerOverride">The owning window, for a tab not yet in the visual tree.</param>
        public async Task EnsureSetupAsync(Window ownerOverride = null)
        {
            if (Design.IsDesignMode) return;
            EventEditorViewModel vm = VM;
            if (vm == null || !AvaloniaEditorLauncher.IsRomLoaded) return;
            Window owner = ownerOverride ?? TopLevel.GetTopLevel(this) as Window;
            if (owner == null) return;

            if (!_setupDone)
            {
                _setupDone = true;
                // No EditorWindowChrome (it needs a Window); EditorHostWindow or the Maps workspace guards unsaved
                // changes.
                vm.MapLoaded += (_, _) => { GlView.SetModel(VM.Model3D, keepView: VM.KeepViewOnLoad); RefreshGizmo(); LoadPegmanIcon(); };
                vm.OwModelPreviewChanged += (_, _) => OwModelView.SetModel(VM.OwModelPreview);
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
            foreach ((int type, int index, float nx, float ny, float nz) in VM.EventAnchorsNorm())
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
                await DialogHelper.ShowInfoAsync("Select an event first.", "Centre on event");
                return;
            }
            GlView.LookAt(nx, ny, nz);
        }

        private async void AnimatedPreview_Click(object sender, RoutedEventArgs e)
        {
            if (VM?.Model3D == null)
            {
                await DialogHelper.ShowErrorAsync("Load an event file with a map first, so there is something to animate.", "Animated preview");
                return;
            }
            OpenPreview();
        }

        /// <summary>Opens the animated preview on what the editor is showing, with the ROM's script lookups.</summary>
        private AnimatedPreviewWindow OpenPreview()
        {
            Window owner = TopLevel.GetTopLevel(this) as Window;
            AnimatedPreviewWindow win = new AnimatedPreviewWindow();
            VM.ConfigureScriptPreview(win.ViewModel);
            win.ShowFor(owner, VM.Model3D, VM.Area, VM.Events, ow => VM.EventFoot(ow), VM.Collision,
                        (x, z) => VM.TileFoot(x, z), n => VM.WalkerFor(n),
                        n => VM.WalkerStartId(n), n => VM.ScriptHome(n), VM.CameraId,
                        VM.MusicDayId, VM.MusicNightId, n => VM.ActionsFor(n), LoadLevelScripts(VM.LevelScriptId), VM.GatherStringVars());
            return win;
        }

        /// <summary>The map's level script file, or null when there is none to read. </summary>
        private static LevelScriptFile LoadLevelScripts(int id)
        {
            if (id < 0) return null;
            try
            {
                LevelScriptFile file = new LevelScriptFile(id);
                return file.BufferSet != null && file.BufferSet.Count > 0 ? file : null;
            }
            catch (Exception ex) { AppLogger.WarnOnce("EventEditorView.LoadLevelScripts", ex); return null; }
        }


        private bool _pegDragging;
        private (int x, int z)? _pegTile;
        // Collision is rebuilt on every read, so it is read once when the drag starts.
        private MapCollisionGrid _pegMap;
        private FieldTilePicker.Prepared _pegTiles;

        /// <summary>The player's own sprite, so the thing you drag is who you will be walking as.</summary>
        private void LoadPegmanIcon()
        {
            try
            {
                OverworldSprites.SpritePixels pix = OverworldSprites.Get(AnimatedPreviewViewModel.PlayerSpriteEntry, 1);
                if (pix == null || pix.Width <= 0 || pix.Height <= 0) return;
                Bitmap bmp = ImageConverter.FromRgba(pix.Rgba, pix.Width, pix.Height);
                PegmanIcon.Source = bmp;
                DragGhost.Source = bmp;
            }
            catch (Exception ex) { AppLogger.Warn("Event editor: pegman icon: " + ex.Message); }
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

            // The pointer's position over the map, the space the projection uses.
            Point overMap = e.GetPosition(GlView);
            Point overOverlay = e.GetPosition(DragOverlay);

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

            (int x, int z)? under = _pegTiles?.Nearest(overMap.X, overMap.Y);
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

            (int x, int z)? tile = _pegTile;
            _pegTile = null;
            if (tile == null) return;          // let go somewhere off the map, so nothing happens

            StartWalkAt(tile.Value.x, tile.Value.z);
        }

        /// <summary>Opens the preview walking from a tile.</summary>
        private void StartWalkAt(int tileX, int tileZ) => OpenPreview().StepInOn(tileX, tileZ);

        /// <summary>
        /// Opens the preview already standing next to the selected event, so you can talk to that person or
        /// walk onto that trigger without hunting for them first.
        /// </summary>
        private async void StepInHere_Click(object sender, RoutedEventArgs e)
        {
            if (VM?.Model3D == null)
            {
                await DialogHelper.ShowErrorAsync("Load an event file with a map first, so there is something to animate.", "Step in here");
                return;
            }
            (int x, int z)? tile = VM.SelectedEventTile;
            if (tile == null)
            {
                await DialogHelper.ShowInfoAsync("Select an event first, and the walk will start next to it.", "Step in here");
                return;
            }

            OpenPreview().StepInBeside(tile.Value.x, tile.Value.z);
        }

        private async void Screenshot_Click(object sender, RoutedEventArgs e) =>
            await GlScreenshot.SaveAsync(GlView, TopLevel.GetTopLevel(this) as Window, "dspre_events",
                SaveNotice.Exported);

        private async void Discard_Click(object sender, RoutedEventArgs e)
        {
            if (await DialogHelper.ConfirmDiscardAsync(this)) (DataContext as DSPRE.Editors.IEditorWithUnsavedChanges)?.DiscardChanges();
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
            => await DialogHelper.GuardAsync(() => { VM?.Save(); return System.Threading.Tasks.Task.CompletedTask; }, "The event file wasn't saved.", ErrorTitle);
        private async void Import_Click(object sender, RoutedEventArgs e) =>
            await DialogHelper.GuardAsync(() => VM?.ImportAsync(source => EventImportDialog.ShowAsync(source, TopLevel.GetTopLevel(this) as Window)) ?? Task.CompletedTask, "The events weren't imported.", ErrorTitle);
        private async void Export_Click(object sender, RoutedEventArgs e) => await DialogHelper.GuardAsync(() => VM?.ExportAsync() ?? Task.CompletedTask, "The event file wasn't exported.", ErrorTitle);

        private async void ManageGroundItems_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null) return;
            // Saving the list renumbers item events on disk; an unsaved copy here would put the old numbers back.
            if (VM.HasUnsavedChanges)
            {
                if (!await DialogHelper.AskYesNoAsync("Save this event file first? The ground item list can renumber item events.", "Event editor")) return;
                VM.SaveChanges();
                if (VM.HasUnsavedChanges) return;
            }

            GroundItemScriptsViewModel dlgVm = new GroundItemScriptsViewModel();
            GroundItemScriptsView dlg = new GroundItemScriptsView(dlgVm);
            Window owner = TopLevel.GetTopLevel(this) as Window;
            if (owner != null) await dlg.ShowDialog(owner);
            else dlg.Show();

            if (dlgVm.Changed) { VM.DiscardChanges(); VM.RefreshOwItemEntries(); }
        }

        // Diagnostic dump: text report (matrix layout, per-cell map placements, event world positions)
        // + a PNG of the current render, written to a user-chosen folder, for diagnosing stitching issues.
        private async void ExportDebug_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null) return;
            try
            {
                string dir = await DialogHelper.OpenFolderAsync(TopLevel.GetTopLevel(this) as Window, "Choose a folder for the report");
                if (dir == null) return;

                string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string txtPath = System.IO.Path.Combine(dir, $"dspre_event_debug_{stamp}.txt");
                string pngPath = System.IO.Path.Combine(dir, $"dspre_event_render_{stamp}.png");

                PlainFiles.WriteText(txtPath, VM.BuildDebugReport());

                // Capture the live GL render (async, fires after the next frame).
                GlView.CaptureFrame((rgba, w, h) =>
                {
                    bool pngOk = false;
                    try { if (rgba != null && w > 0 && h > 0) { SaveRgbaToPng(rgba, w, h, pngPath); pngOk = true; } }
                    catch (Exception ex) { AppLogger.WarnOnce("EventEditorView.ExportDebug_Click", ex); pngOk = false; }
                    SaveNotice.Exported(txtPath);
                    if (!pngOk) _ = DialogHelper.ShowErrorAsync("The report was written, but the render couldn't be captured.", "Event editor");
                });
            }
            catch (Exception ex) { await DialogHelper.ShowErrorAsync($"Couldn't write the report:\n{ex.Message}", "Event editor"); }
        }

        // Builds a PNG from a raw RGBA framebuffer (origin bottom-left → flipped to top-left for the image).
        private static void SaveRgbaToPng(byte[] rgba, int w, int h, string path) => GlScreenshot.SaveRgbaToPng(rgba, w, h, path);

        private void GoToOwScript_Click(object sender, RoutedEventArgs e) => VM?.GoToOverworldScript();
        private void GoToTrScript_Click(object sender, RoutedEventArgs e) => VM?.GoToTriggerScript();
        private void GoToSpScript_Click(object sender, RoutedEventArgs e) => VM?.GoToSpawnableScript();

        private async void RenameCommonScript_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null || sender is not Button { Tag: string tag } || !int.TryParse(tag, out int kind)) return;
            int number = VM.ShownScript(kind);
            string name = await DialogHelper.PromptTextAsync($"Name for script {number}:", "Rename shared script",
                DSPRE.Avalonia.Data.CommonScriptLabels.Of(number) ?? "", TopLevel.GetTopLevel(this) as Window);
            if (name != null) VM.RenameCommonScript(kind, name);
        }
        private void OpenHiddenItems_Click(object sender, RoutedEventArgs e) => VM?.OpenHiddenItemTable();

        private void AddOw_Click(object sender, RoutedEventArgs e) => VM?.AddOverworld();
        private void RemoveOw_Click(object sender, RoutedEventArgs e) => VM?.RemoveOverworld();
        private void DupOw_Click(object sender, RoutedEventArgs e) => VM?.DuplicateOverworld();
        // Delete removes and Ctrl+D duplicates the picked entry in whichever list has focus.
        private void EventList_KeyDown(object sender, KeyEventArgs e)
        {
            if (VM == null || sender is not Control list) return;
            bool delete = e.Key == Key.Delete;
            bool duplicate = e.Key == Key.D && e.KeyModifiers.HasFlag(KeyModifiers.Control);
            if (!delete && !duplicate) return;
            switch (list.Tag as string)
            {
                case "ow": if (delete) VM.RemoveOverworld(); else VM.DuplicateOverworld(); break;
                case "warp": if (delete) VM.RemoveWarp(); else VM.DuplicateWarp(); break;
                case "trig": if (delete) VM.RemoveTrigger(); else VM.DuplicateTrigger(); break;
                case "spawn": if (delete) VM.RemoveSpawnable(); else VM.DuplicateSpawnable(); break;
                default: return;
            }
            e.Handled = true;
            // Refilling the list drops keyboard focus with the old row, so the next shortcut would go nowhere. The
            // rows are rebuilt after this handler, so focus goes to the picked row once they have settled.
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                Control row = list is ListBox box && box.SelectedIndex >= 0 ? box.ContainerFromIndex(box.SelectedIndex) : null;
                if (row == null || !row.Focus()) list.Focus();
            }, global::Avalonia.Threading.DispatcherPriority.ContextIdle);
        }
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
        private async void RemFile_Click(object sender, RoutedEventArgs e) => await DialogHelper.GuardAsync(() => VM?.RemoveLastEventFileAsync() ?? Task.CompletedTask, "The event file wasn't removed.", ErrorTitle);

        private const string ErrorTitle = "Event editor";
    }
}
