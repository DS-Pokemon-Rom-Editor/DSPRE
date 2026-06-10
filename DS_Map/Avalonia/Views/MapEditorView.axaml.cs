using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views
{
    public partial class MapEditorView : Window
    {
        private MapEditorViewModel VM => DataContext as MapEditorViewModel;
        private bool _setupDone;
        private bool _closeConfirmed;
        private Point? _lastPointer;

        public MapEditorView()
        {
            InitializeComponent();
            CollisionGrid.IsCollision = true;
            TypeGrid.IsCollision = false;
            CollisionGrid.Changed += (_, _) => { VM?.MarkDirty(); VM?.RebuildOverlay(); };
            TypeGrid.Changed += (_, _) => { VM?.MarkDirty(); VM?.RebuildOverlay(); };

            // Orbit/zoom the 3D preview (handled on the transparent host Border).
            GlHost.PointerPressed += (s, e) => { _lastPointer = e.GetPosition(GlHost); e.Pointer.Capture(GlHost); };
            GlHost.PointerReleased += (s, e) => { _lastPointer = null; e.Pointer.Capture(null); };
            GlHost.PointerMoved += (s, e) =>
            {
                if (_lastPointer is not Point last) return;
                var p = e.GetPosition(GlHost);
                GlView.Yaw += (float)(p.X - last.X) * 0.5f;
                GlView.Pitch += (float)(p.Y - last.Y) * 0.5f;
                _lastPointer = p;
            };
            GlHost.PointerWheelChanged += (s, e) => GlView.Distance -= (float)e.Delta.Y * 0.4f;

            Loaded += OnLoadedSetup;
        }

        public MapEditorView(MapEditorViewModel vm) : this()
        {
            DataContext = vm;
        }

        private async void OnLoadedSetup(object sender, RoutedEventArgs e)
        {
            if (_setupDone || Design.IsDesignMode) return;
            var vm = VM;
            if (vm == null) return;
            _setupDone = true;

            vm.MapLoaded += OnMapLoaded;
            vm.OverlayChanged += (_, _) => GlView.SetOverlay(VM.OverlayMesh, VM.OverlayVertexCount);
            vm.PropertyChanged += OnVmPropertyChanged;
            await vm.SetupAsync(this);
        }

        private void OnMapLoaded(object sender, EventArgs e)
        {
            CollisionGrid.SetData(VM.Collisions);
            TypeGrid.SetData(VM.Types);
            CollisionGrid.PaintValue = VM.CollisionPaintValue;
            TypeGrid.PaintValue = VM.TypePaintValue;
            GlView.SetModel(VM.Model3D);
        }

        private void OnVmPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MapEditorViewModel.CollisionPaintValue)) CollisionGrid.PaintValue = VM.CollisionPaintValue;
            else if (e.PropertyName == nameof(MapEditorViewModel.TypePaintValue)) TypeGrid.PaintValue = VM.TypePaintValue;
        }

        private void Save_Click(object sender, RoutedEventArgs e) => VM?.Save();
        private async void Import_Click(object sender, RoutedEventArgs e) => await Safe(VM?.ImportAsync());
        private async void Export_Click(object sender, RoutedEventArgs e) => await Safe(VM?.ExportAsync());
        private void AddBuilding_Click(object sender, RoutedEventArgs e) => VM?.AddBuilding();
        private void RemoveBuilding_Click(object sender, RoutedEventArgs e) => VM?.RemoveBuilding();

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
            try { await task; } catch { /* handled in VM */ }
        }
    }
}
