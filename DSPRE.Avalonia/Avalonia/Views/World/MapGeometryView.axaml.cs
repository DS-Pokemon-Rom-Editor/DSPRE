using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Interactivity;
using DSPRE.Avalonia.Gl;
using DSPRE.Avalonia.ViewModels.World;
using System;
using System.Collections.Generic;

namespace DSPRE.Avalonia.Views.World
{
    public partial class MapGeometryView : UserControl
    {
        private Gl3DPointerNavigation _nav;
        private MapGeometryViewModel VM => DataContext as MapGeometryViewModel;

        public MapGeometryView()
        {
            InitializeComponent();
            _nav = new Gl3DPointerNavigation(GlHost, GlView)
            {
                IsEditModeActive = () => VM?.Ready == true,
                BeginGizmoDrag = () => VM?.BeginDrag(),
                NudgeAxis = (axis, delta) => VM?.DragAxis(axis, delta),
            };

            GlHost.AddHandler(PointerPressedEvent, Host_Pressed, RoutingStrategies.Tunnel);

            // The map editor owns the view model, so a closed window must unhook from it.
            DataContextChanged += (_, _) => Watch(VM);
            DetachedFromVisualTree += (_, _) => Watch(null);
            AttachedToVisualTree += (_, _) => Watch(VM);
        }

        private MapGeometryViewModel _watched;

        private void Watch(MapGeometryViewModel vm)
        {
            if (ReferenceEquals(vm, _watched)) return;
            if (_watched != null)
            {
                _watched.Changed -= VM_Changed;
                _watched.PlatesChanged -= VM_PlatesChanged;
            }
            _watched = vm;
            if (vm == null) return;
            vm.Changed += VM_Changed;
            vm.PlatesChanged += VM_PlatesChanged;
            Refresh();
        }

        private void VM_Changed(object sender, EventArgs e) => Refresh();

        private void VM_PlatesChanged(object sender, EventArgs e)
        {
            if (sender is MapGeometryViewModel vm) GlView.SetOverlay(vm.PlateMesh, vm.PlateVertexCount);
        }

        private void Refresh()
        {
            if (VM == null) return;
            GlView.SetModel(VM.Model3D);
            GlView.EditMode = true;

            if (VM.PickedCornerInScene(out float x, out float y, out float z))
                GlView.SetGizmoTarget(x, y, z);
            else
                GlView.ClearGizmoTarget();
        }

        private void Host_Pressed(object sender, PointerPressedEventArgs e)
        {
            if (VM == null || !VM.Ready) return;

            PointerPoint point = e.GetCurrentPoint(GlView);
            if (!point.Properties.IsLeftButtonPressed) return;

            if (GlView.HitTestGizmoAxis((float)point.Position.X, (float)point.Position.Y) >= 0) return;

            bool gather = e.KeyModifiers == KeyModifiers.Shift;
            if (e.KeyModifiers != KeyModifiers.None && !gather) return;

            if (!GlView.ScreenToRay((float)point.Position.X, (float)point.Position.Y,
                                    out float ox, out float oy, out float oz,
                                    out float dx, out float dy, out float dz)) return;

            if (VM.PickAlong(ox, oy, oz, dx, dy, dz))
            {
                if (gather) VM.GatherCorner();
                e.Handled = true;
            }
        }

        private void Step_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (VM == null) return;
            VM.Step = StepBox.SelectedIndex switch
            {
                0 => 1f,
                1 => 0.5f,
                3 => 0.125f,
                _ => 0.25f,
            };
        }

        private async void Export_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null || !VM.Ready) return;

            TopLevel top = TopLevel.GetTopLevel(this);
            if (top == null) return;

            IStorageFile file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export map model",
                SuggestedFileName = "map.obj",
                DefaultExtension = "obj",
                FileTypeChoices = new[] { new FilePickerFileType("Model") { Patterns = new[] { "*.obj" } } },
            });
            if (file == null) return;

            VM.Told(VM.ExportTo(file.Path.LocalPath));
        }

        private async void Import_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null) return;
            TopLevel top = TopLevel.GetTopLevel(this);
            if (top == null) return;
            IReadOnlyList<IStorageFile> files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Import map model",
                AllowMultiple = false,
                FileTypeFilter = new[] { new FilePickerFileType("Model") { Patterns = new[] { "*.obj" } } },
            });
            if (files == null || files.Count == 0) return;
            if (!await DSPRE.Avalonia.DialogHelper.AskYesNo(
                    "Replace the map model with this OBJ?",
                    "Import map model")) return;
            VM.Told(VM.ImportFrom(files[0].Path.LocalPath));
        }

        private void Undo_Click(object sender, RoutedEventArgs e) => VM?.Undo();
        private void DeleteFace_Click(object sender, RoutedEventArgs e) => VM?.DeleteFace();
        private void MakeFace_Click(object sender, RoutedEventArgs e) => VM?.MakeFace();
        private void ForgetCorners_Click(object sender, RoutedEventArgs e) => VM?.ForgetCorners();

        private void Terrain_Click(object sender, RoutedEventArgs e) => VM?.ProposePlates();
        private void UseProposal_Click(object sender, RoutedEventArgs e) => VM?.UseProposal();
        private void DiscardProposal_Click(object sender, RoutedEventArgs e) => VM?.DiscardProposal();
        private void AddPlate_Click(object sender, RoutedEventArgs e) => VM?.AddPlate();
        private void DeletePlate_Click(object sender, RoutedEventArgs e) => VM?.DeletePlate();

        private void Paint_Click(object sender, RoutedEventArgs e) => VM?.Paint();

        private void XLess_Click(object sender, RoutedEventArgs e) => VM?.Nudge(-Tiles(), 0f, 0f);
        private void XMore_Click(object sender, RoutedEventArgs e) => VM?.Nudge(Tiles(), 0f, 0f);
        private void YLess_Click(object sender, RoutedEventArgs e) => VM?.Nudge(0f, -Tiles(), 0f);
        private void YMore_Click(object sender, RoutedEventArgs e) => VM?.Nudge(0f, Tiles(), 0f);
        private void ZLess_Click(object sender, RoutedEventArgs e) => VM?.Nudge(0f, 0f, -Tiles());
        private void ZMore_Click(object sender, RoutedEventArgs e) => VM?.Nudge(0f, 0f, Tiles());

        private float Tiles() => (VM?.Step ?? 0.25f) * NsbmdGeometry.TileSize;

        private void CamTop_Click(object sender, RoutedEventArgs e) => GlView.SetOrientation(0f, 89f);
        private void CamIso_Click(object sender, RoutedEventArgs e) => GlView.SetOrientation(30f, 30f);
        private void CamFront_Click(object sender, RoutedEventArgs e) => GlView.SetOrientation(0f, 8f);
        private void CamSide_Click(object sender, RoutedEventArgs e) => GlView.SetOrientation(90f, 8f);
    }
}
