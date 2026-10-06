using System;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using DSPRE.ROMFiles;
using DSPRE.Avalonia.Gl;
using DSPRE.Avalonia.ViewModels.World;
using DSPRE.Avalonia.Views.Battle;
using System.Collections;

namespace DSPRE.Avalonia.Views.World
{
    public partial class DistortionWorldView : Window
    {
        private DistortionWorldViewModel VM => DataContext as DistortionWorldViewModel;
        private Gl3DPointerNavigation _nav;

        public DistortionWorldView(DistortionWorldViewModel vm)
        {
            DataContext = vm;
            InitializeComponent();
            EditorWindowChrome.Attach(this, vm);

            _nav = new Gl3DPointerNavigation(GlHost, GlView);

            CollisionGrid.IsCollision = true;
            BehaviourGrid.IsCollision = false;
            CollisionGrid.SetData(vm.CollisionCells);
            BehaviourGrid.SetData(vm.BehaviourCells);
            CollisionGrid.PaintValue = vm.CollisionPaintValue;
            BehaviourGrid.PaintValue = vm.BehaviourPaint;
            CollisionGrid.Changed += Painted;
            BehaviourGrid.Changed += Painted;
            CollisionGrid.Hovered += Hovered;
            BehaviourGrid.Hovered += Hovered;

            vm.SceneShown += ShowScene;
            vm.PlatformShown += ShowPlatform;
            vm.PropertyChanged += VmChanged;

            GlView.SetOrientation(30f, 30f);
            ShowScene();
            ShowPlatform();
        }

        private void ShowScene()
        {
            if (VM == null) return;
            GlView.SetModel(VM.Model3D);
            GlView.SetOverlay(VM.OverlayMesh, VM.OverlayVertexCount);

            if (VM.WholeWorld && VM.SelectedFloorCentre() is (float x, float y, float z)) GlView.LookAt(x, y, z);
            else GlView.ResetView();
        }

        private void ShowPlatform()
        {
            if (VM == null) return;
            CollisionGrid.UsedRows = BehaviourGrid.UsedRows = VM.UsedRows;
            CollisionGrid.UsedColumns = BehaviourGrid.UsedColumns = VM.UsedColumns;
            CollisionGrid.InvalidateVisual();
            BehaviourGrid.InvalidateVisual();
        }

        private void VmChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DistortionWorldViewModel.CollisionPaintValue)) CollisionGrid.PaintValue = VM.CollisionPaintValue;
            else if (e.PropertyName == nameof(DistortionWorldViewModel.BehaviourPaint)) BehaviourGrid.PaintValue = VM.BehaviourPaint;
            else if (e.PropertyName == nameof(DistortionWorldViewModel.Flat2D))
            {
                GlView.Orthographic = VM.Flat2D;
                if (VM.Flat2D) GlView.SetOrientation(0f, 90f);
            }
        }

        private void Painted(object sender, EventArgs e) => VM?.ApplyPaintedGrid();

        private void Hovered(object sender, (int col, int row)? at)
        {
            VM?.HoverGrid(at?.col, at?.row);
            CollisionGrid.Marked = at;
            BehaviourGrid.Marked = at;
        }

        private async void EditModel_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null) return;
            MapModelEditorView window = new MapModelEditorView(VM.MapModel);
            await window.ShowDialog(this);
            VM.ShowFloorAgain();
        }

        private void CamTop_Click(object sender, RoutedEventArgs e) => GlView.SetOrientation(0f, 89f);
        private void CamIso_Click(object sender, RoutedEventArgs e) => GlView.SetOrientation(30f, 30f);
        private void CamFront_Click(object sender, RoutedEventArgs e) => GlView.SetOrientation(0f, 8f);
        private void CamSide_Click(object sender, RoutedEventArgs e) => GlView.SetOrientation(90f, 8f);

        private async void Animate_Click(object sender, RoutedEventArgs e)
        {
            if (VM?.Model3D == null)
            {
                await DialogHelper.ShowError("This floor has no map to play.", "Distortion World");
                return;
            }
            DistortionWorldViewModel vm = VM;
            AnimatedPreviewWindow preview = new AnimatedPreviewWindow();
            AnimatedPreviewViewModel walk = preview.ViewModel;
            vm.StartWalk();
            walk.CameraAnglesAt = (x, z, facing) => vm.CameraAt(x, z, facing);
            walk.PlayerArrivedOn = (x, z, frame) => vm.SomebodyStoodOn(x, z, frame, walk.Player?.Facing ?? MoveFacing.Down);
            vm.CurrentFrame = () => walk.Frame;
            vm.PutPlayerOn = (x, z, facing) => walk.PutPlayerOn(x, z, facing);
            walk.PlayerRollAt = (x, z) => vm.RollAt(x, z);
            walk.PlayerHeld = () => vm.PlayerHeld;
            walk.PlayerSpriteShift = vm.PlayerSpriteShift;
            walk.BuildingOpacity = vm.OpacityOf;
            walk.FrameAdvanced += (_, _) => vm.Tick(walk.Frame);

            // A ride that leaves the floor shown carries on on the next one; the reload waits for the frame to end.
            Action changed = () => Dispatcher.UIThread.Post(() =>
            {
                if (!vm.ShowRideFloor()) return;
                preview.ReplaceScene(vm.Model3D, vm.Area, vm.Events, vm.Collision);
                (int x, int z)? tile = vm.RideWalkTile();
                if (tile != null) walk.StandOn(tile.Value.x, tile.Value.z, vm.RideFacing);
                if (walk.Player != null) vm.RideContinuesFrom(walk.Player.TileX, walk.Player.TileZ, walk.Frame);
            });
            vm.RideChangedFloor += changed;
            preview.Closed += (_, _) => vm.RideChangedFloor -= changed;

            preview.ShowPeopleThroughWalls = true;
            preview.ShowFor(this, vm.Model3D, vm.Area, vm.Events, null,
                vm.Collision, (x, z) => vm.TileFoot(x, z),
                cameraId: vm.CameraId, musicDayId: vm.MusicDayId, musicNightId: vm.MusicNightId);

            if (!vm.WholeWorld) walk.StartTile = vm.WhereToStand();
        }

        private void Edited(object sender, DataGridCellEditEndedEventArgs e)
        {
            if (VM == null || VM.MarkEdited() || sender is not DataGrid grid) return;
            // The rows raise no change of their own, so the grid only shows the value put back once it rebinds.
            Dispatcher.UIThread.Post(() =>
            {
                IEnumerable rows = grid.ItemsSource;
                grid.ItemsSource = null;
                grid.ItemsSource = rows;
            });
        }

        private void Save_Click(object sender, RoutedEventArgs e) => VM?.SaveChanges();
        private void Discard_Click(object sender, RoutedEventArgs e) => VM?.DiscardChanges();
    }
}
