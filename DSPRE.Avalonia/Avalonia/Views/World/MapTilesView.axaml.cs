using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using DSPRE.Avalonia.Gl;
using DSPRE.Avalonia.ViewModels.World;
using DSPRE.Models;
using System.Collections.Generic;
using System.Linq;

namespace DSPRE.Avalonia.Views.World
{
    public partial class MapTilesView : UserControl
    {
        private MapTilesViewModel VM => DataContext as MapTilesViewModel;
        private MapTilesViewModel _hooked;
        private readonly Gl3DPointerNavigation _nav, _mapNav;

        public MapTilesView()
        {
            InitializeComponent();
            _nav = new Gl3DPointerNavigation(TileHost, TileView);
            TileView.SetOrientation(35f, 28f);
            _mapNav = new Gl3DPointerNavigation(GlHost, GlView);
            GlView.SetOrientation(30f, 30f);

            Painter.Pressed += (_, e) => VM?.Press(e.x, e.z, e.button switch
            {
                MouseButton.Right => TilePainter.Button.Right,
                MouseButton.Middle => TilePainter.Button.Middle,
                _ => TilePainter.Button.Left,
            }, (e.keys & KeyModifiers.Shift) != 0, (e.keys & KeyModifiers.Control) != 0);
            Painter.Dragged += (_, e) => VM?.Drag(e.x, e.z);
            Painter.Released += (_, e) => VM?.Release(e.x, e.z);
            Painter.Hovered += (_, at) => VM?.Over(at?.x, at?.z);
            Painter.BuildingPressed += (_, i) => VM?.PressBuilding(i);
            Painter.BuildingDragged += (_, e) => VM?.DragBuilding(e.index, e.dx, e.dz);
            Painter.BuildingReleased += (_, e) => VM?.ReleaseBuilding(e.index, e.dx, e.dz);
            Painter.BuildingHovered += (_, i) => VM?.OverBuilding(i);
            Painter.KeyDown += Painter_KeyDown;

            // The map editor owns the view model, so a closed window must unhook from it.
            DataContextChanged += (_, _) => Hook(VM);
            DetachedFromVisualTree += (_, _) => Hook(null);
            AttachedToVisualTree += (_, _) => Hook(VM);
        }

        private void Hook(MapTilesViewModel vm)
        {
            if (ReferenceEquals(vm, _hooked)) return;
            if (_hooked != null)
            {
                _hooked.GridChanged -= OnGridChanged;
                _hooked.PendingChanged -= OnPending;
                _hooked.Changed -= OnChanged;
                _hooked.TileShown -= OnTileShown;
            }
            _hooked = vm;
            if (_hooked == null) return;

            _hooked.GridChanged += OnGridChanged;
            _hooked.PendingChanged += OnPending;
            _hooked.Changed += OnChanged;
            _hooked.TileShown += OnTileShown;
            Repaint();
            GlView.SetModel(_hooked.Model3D);
            TileView.SetModel(_hooked.Tile3D);
        }

        private void OnGridChanged(object sender, System.EventArgs e) => Repaint();
        private void OnChanged(object sender, System.EventArgs e) => GlView.SetModel(VM?.Model3D);
        private void OnTileShown(object sender, System.EventArgs e) => TileView.SetModel(VM?.Tile3D);

        private void OnPending(object sender, System.EventArgs e)
        {
            if (VM == null) return;
            Painter.ShowPending(VM.Pending);
            Painter.ShowGhost(VM.Ghost);
            Painter.ShowSelection(VM.Selection);
        }

        private void Repaint()
        {
            if (VM == null) return;
            Painter.DimOthers = VM.DimOtherLayers;
            Painter.SelectedBuilding = VM.SelectedBuilding;
            Painter.ShowBuildings(VM.ShowBuildings ? VM.BuildingSquares() : null);
            Painter.Show(VM.PlacedTiles(), VM.VisibleLayers(), VM.ActiveLayer, VM.ActiveHeights(),
                         VM.ShowHeights, (tile, turn) => VM.FromAbove(tile, turn));
        }

        private void CollisionCell_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (sender is Border { Tag: int index } && VM != null) VM.ChosenCell = index;
        }

        private static readonly Dictionary<Key, TilePainter.Tool> ToolKeys = new Dictionary<Key, TilePainter.Tool>
        {
            [Key.B] = TilePainter.Tool.Paint, [Key.E] = TilePainter.Tool.Clear, [Key.S] = TilePainter.Tool.Smart,
            [Key.G] = TilePainter.Tool.Bucket, [Key.I] = TilePainter.Tool.Picker, [Key.L] = TilePainter.Tool.Line,
            [Key.U] = TilePainter.Tool.Rectangle, [Key.O] = TilePainter.Tool.Ellipse, [Key.M] = TilePainter.Tool.Select,
            [Key.Q] = TilePainter.Tool.Lasso, [Key.W] = TilePainter.Tool.Wand,
        };

        private void Painter_KeyDown(object sender, KeyEventArgs e)
        {
            if (VM == null) return;
            bool ctrl = (e.KeyModifiers & KeyModifiers.Control) != 0;
            if (ctrl && e.Key == Key.Z) { VM.UndoPaint(); e.Handled = true; }
            else if (ctrl && e.Key == Key.Y) { VM.RedoPaint(); e.Handled = true; }
            else if (e.Key == Key.R) { VM.Turn += 1; e.Handled = true; }
            else if (e.Key == Key.H) { VM.ShowHeights = !VM.ShowHeights; e.Handled = true; }
            else if (e.Key >= Key.D1 && e.Key <= Key.D9) { VM.ActiveLayer = e.Key - Key.D1; e.Handled = true; }
            else if (ctrl && e.Key == Key.C) { VM.CopySelection(); e.Handled = true; }
            else if (ctrl && e.Key == Key.X) { VM.CutSelection(); e.Handled = true; }
            else if (ctrl && e.Key == Key.V) { VM.Paste(); e.Handled = true; }
            else if (ctrl && e.Key == Key.A) { VM.SelectAll(); e.Handled = true; }
            else if (ctrl && e.Key == Key.F) { VM.FillSelection(); e.Handled = true; }
            else if (ctrl && e.Key == Key.D) { VM.SelectNone(); e.Handled = true; }
            else if (e.Key == Key.Delete) { VM.DeleteSelection(); e.Handled = true; }
            else if (e.Key == Key.Escape) { VM.SelectNone(); e.Handled = true; }
            else if (!ctrl && ToolKeys.TryGetValue(e.Key, out TilePainter.Tool tool))
            {
                if (tool == TilePainter.Tool.Smart && e.KeyModifiers.HasFlag(KeyModifiers.Shift)) tool = TilePainter.Tool.SmartInverted;
                int at = VM.Tools.ToList().FindIndex(t => t.Tool == tool);
                if (at >= 0) { VM.ToolIndex = at; e.Handled = true; }
            }
        }

        private void TakeApart_Click(object sender, RoutedEventArgs e) => VM?.TakeApart();

        private async void BringIn_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null) return;
            TopLevel top = TopLevel.GetTopLevel(this);
            if (top == null) return;

            IReadOnlyList<IStorageFile> files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Import tileset or map",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Tileset or map") { Patterns = new[] { "*.pdsts", "*.pdsmap", "*.obj" } },
                    new FilePickerFileType("Map Studio tileset") { Patterns = new[] { "*.pdsts" } },
                    new FilePickerFileType("Map Studio map") { Patterns = new[] { "*.pdsmap" } },
                    new FilePickerFileType("OBJ") { Patterns = new[] { "*.obj" } },
                },
            });
            if (files == null || files.Count == 0) return;

            VM.BringIn(files[0].Path.LocalPath);
        }

        private async void SaveSet_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null || !VM.Ready) return;
            TopLevel top = TopLevel.GetTopLevel(this);
            if (top == null) return;

            IStorageFile file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export tileset",
                SuggestedFileName = "tileset.obj",
                DefaultExtension = "obj",
                FileTypeChoices = new[] { new FilePickerFileType("Model") { Patterns = new[] { "*.obj" } } },
            });
            if (file == null) return;

            VM.SaveTo(file.Path.LocalPath);
        }

        private void SaveCollision_Click(object sender, RoutedEventArgs e) => VM?.SaveCollisionToTileset();

        private async void PutPictures_Click(object sender, RoutedEventArgs e)
        {
            MapTilesViewModel vm = VM;
            if (vm == null || !vm.HasOwnPictures) return;
            MapTilesViewModel.TexturePlan plan = vm.PlanPictures();
            if (plan == null) return;
            Window owner = TopLevel.GetTopLevel(this) as Window;
            if (owner == null) return;
            bool add = await new AddTexturesDialogView(plan).ShowDialog<bool>(owner);
            if (add && VM == vm) vm.PutPicturesIntoTheRom(plan);
        }

        private void Slot_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (VM == null || sender is not Control { Tag: int slot }) return;
            bool empty = e.GetCurrentPoint(this).Properties.IsRightButtonPressed;
            VM.SetSlot(slot, empty);
            e.Handled = true;
        }

        private async void AnimFrames_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null) return;
            TopLevel top = TopLevel.GetTopLevel(this);
            if (top == null) return;
            IReadOnlyList<IStorageFile> files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Animation frames",
                AllowMultiple = true,
                FileTypeFilter = new[] { new FilePickerFileType("PNG") { Patterns = new[] { "*.png" } } },
            });
            if (files == null || files.Count == 0) return;
            VM.AnimateChosen(System.Linq.Enumerable.ToList(System.Linq.Enumerable.OrderBy(System.Linq.Enumerable.Select(files, f => f.Path.LocalPath), p => p, System.StringComparer.OrdinalIgnoreCase)));
        }

        private void AnimStop_Click(object sender, RoutedEventArgs e) => VM?.StopAnimatingChosen();
        private void Ramp_Click(object sender, RoutedEventArgs e) { VM?.MarkRamp(); RampButton.Flyout?.Hide(); }
        private void Unramp_Click(object sender, RoutedEventArgs e) { VM?.UnmarkRamp(); RampButton.Flyout?.Hide(); }

        private async void AddTiles_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null) return;
            TopLevel top = TopLevel.GetTopLevel(this);
            if (top == null) return;
            IReadOnlyList<IStorageFile> files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Append tiles",
                AllowMultiple = false,
                FileTypeFilter = new[] { new FilePickerFileType("Tileset") { Patterns = new[] { "*.pdsts", "*.obj" } } },
            });
            if (files == null || files.Count == 0) return;
            VM.AddTilesFrom(files[0].Path.LocalPath);
        }

        private void NudgeWest_Click(object sender, RoutedEventArgs e) => VM?.NudgeTile(-1, 0, 0);
        private void NudgeEast_Click(object sender, RoutedEventArgs e) => VM?.NudgeTile(1, 0, 0);
        private void NudgeNorth_Click(object sender, RoutedEventArgs e) => VM?.NudgeTile(0, 0, -1);
        private void NudgeSouth_Click(object sender, RoutedEventArgs e) => VM?.NudgeTile(0, 0, 1);
        private void NudgeUp_Click(object sender, RoutedEventArgs e) => VM?.NudgeTile(0, 1, 0);
        private void NudgeDown_Click(object sender, RoutedEventArgs e) => VM?.NudgeTile(0, -1, 0);
        private void TurnShape_Click(object sender, RoutedEventArgs e) => VM?.TurnTileShape();
        private void Mirror_Click(object sender, RoutedEventArgs e) => VM?.MirrorTile();
        private void MoveEarlier_Click(object sender, RoutedEventArgs e) => VM?.MoveTile(-1);
        private void MoveLater_Click(object sender, RoutedEventArgs e) => VM?.MoveTile(1);
        private void Duplicate_Click(object sender, RoutedEventArgs e) => VM?.DuplicateTile();
        private void RemoveTile_Click(object sender, RoutedEventArgs e) => VM?.RemoveTile();
        private void Copy_Click(object sender, RoutedEventArgs e) => VM?.CopySelection();
        private void Cut_Click(object sender, RoutedEventArgs e) => VM?.CutSelection();
        private void Paste_Click(object sender, RoutedEventArgs e) => VM?.Paste();
        private void Delete_Click(object sender, RoutedEventArgs e) => VM?.DeleteSelection();
        private void FillSel_Click(object sender, RoutedEventArgs e) => VM?.FillSelection();
        private void Rotate_Click(object sender, RoutedEventArgs e) => VM?.RotateSelection();
        private void FlipAcross_Click(object sender, RoutedEventArgs e) => VM?.FlipSelectionAcross();
        private void FlipDown_Click(object sender, RoutedEventArgs e) => VM?.FlipSelectionDown();
        private async void Apply_Click(object sender, RoutedEventArgs e)
        {
            MapTilesViewModel vm = VM;
            if (vm == null) return;
            int replaced = vm.PlatesApplyWouldReplace();
            bool terrain = vm.AlsoTerrain;
            if (replaced > 0)
            {
                bool replace = await DSPRE.Avalonia.DialogHelper.AskYesNo(
                    $"Rebuilding the terrain replaces {replaced} of this map's plates over the squares you changed. Replace them?",
                    "Rebuild terrain");
                // Closing the window while asked cancels the Apply.
                if (VM != vm) return;
                if (!replace) vm.AlsoTerrain = false;
            }
            bool bringInBuildings = true;
            (int newBuildings, int currentBuildings)? swap = vm.BuildingsApplyWouldReplace();
            if (swap != null)
            {
                bringInBuildings = await DSPRE.Avalonia.DialogHelper.AskYesNo(
                    swap.Value.newBuildings > 0
                        ? $"The PDSMS map carries {swap.Value.newBuildings} buildings. Replace the {swap.Value.currentBuildings} buildings on this map with them?"
                        : $"The PDSMS map carries no buildings. Remove the {swap.Value.currentBuildings} buildings on this map?",
                    "Replace buildings");
                if (VM != vm) { vm.AlsoTerrain = terrain; return; }
            }
            vm.Apply(bringInBuildings);
            vm.AlsoTerrain = terrain;
        }
        private void Undo_Click(object sender, RoutedEventArgs e) => VM?.Undo();
        private void UndoPaint_Click(object sender, RoutedEventArgs e) => VM?.UndoPaint();
        private void RedoPaint_Click(object sender, RoutedEventArgs e) => VM?.RedoPaint();
        private void AddSmart_Click(object sender, RoutedEventArgs e) => VM?.AddSmartDrawing();
        private void RemoveSmart_Click(object sender, RoutedEventArgs e) => VM?.RemoveSmartDrawing();
        private void ShiftUp_Click(object sender, RoutedEventArgs e) => VM?.ShiftLayer(0, -1);
        private void ShiftDown_Click(object sender, RoutedEventArgs e) => VM?.ShiftLayer(0, 1);
        private void ShiftLeft_Click(object sender, RoutedEventArgs e) => VM?.ShiftLayer(-1, 0);
        private void ShiftRight_Click(object sender, RoutedEventArgs e) => VM?.ShiftLayer(1, 0);
        private void RaiseUp_Click(object sender, RoutedEventArgs e) => VM?.RaiseLayer(1);
        private void RaiseDown_Click(object sender, RoutedEventArgs e) => VM?.RaiseLayer(-1);
        private void CopyLayer_Click(object sender, RoutedEventArgs e) => VM?.CopyLayer();
        private void PasteLayer_Click(object sender, RoutedEventArgs e) => VM?.PasteLayer();
        private void ClearLayer_Click(object sender, RoutedEventArgs e) => VM?.ClearLayer();
    }
}
