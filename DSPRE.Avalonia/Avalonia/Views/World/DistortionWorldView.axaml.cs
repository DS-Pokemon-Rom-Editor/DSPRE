using System;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DSPRE.Avalonia.Gl;
using DSPRE.Avalonia.ViewModels.World;
using DSPRE.Avalonia.Views.Battle;

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
            var window = new MapModelEditorView(VM.MapModel);
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
            var preview = new AnimatedPreviewWindow();
            preview.ViewModel.CameraAnglesAt = (x, z, facing) => VM.CameraAt(x, z, facing);
            preview.ViewModel.PlayerArrivedOn = (x, z, frame) => VM.SomebodyStoodOn(x, z, frame);
            VM.CurrentFrame = () => preview.ViewModel.Frame;
            VM.PutPlayerOn = (x, z, facing) => preview.ViewModel.PutPlayerOn(x, z, facing);
            preview.ViewModel.PlayerRollAt = (x, z) => VM.RollAt(x, z);
            preview.ShowPeopleThroughWalls = true;
            preview.ShowFor(this, VM.Model3D, VM.Area, VM.Events, null,
                VM.Collision, (x, z) => VM.TileFoot(x, z),
                cameraId: VM.CameraId, musicDayId: VM.MusicDayId, musicNightId: VM.MusicNightId);

            if (!VM.WholeWorld) preview.ViewModel.StartTile = VM.WhereToStand();
        }

        private void Edited(object sender, DataGridCellEditEndedEventArgs e) => VM?.MarkEdited();

        private void Save_Click(object sender, RoutedEventArgs e) => VM?.SaveChanges();
    }
}
