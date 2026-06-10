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

        public EventEditorView()
        {
            InitializeComponent();

            // Orbit/zoom the 3D map view.
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

        public EventEditorView(EventEditorViewModel vm) : this() { DataContext = vm; }

        private async void OnLoadedSetup(object sender, RoutedEventArgs e)
        {
            if (_setupDone || Design.IsDesignMode) return;
            var vm = VM;
            if (vm == null) return;
            _setupDone = true;

            vm.MapLoaded += (_, _) => GlView.SetModel(VM.Model3D);
            vm.MarkersChanged += (_, _) => GlView.SetMarkers(VM.MarkerMesh, VM.MarkerVertexCount);
            vm.SpritesChanged += (_, _) => GlView.SetSprites(VM.Sprites);
            await vm.SetupAsync(this);
        }

        private void Save_Click(object sender, RoutedEventArgs e) => VM?.Save();
        private async void Import_Click(object sender, RoutedEventArgs e) => await Safe(VM?.ImportAsync());
        private async void Export_Click(object sender, RoutedEventArgs e) => await Safe(VM?.ExportAsync());

        private void AddOw_Click(object sender, RoutedEventArgs e) => VM?.AddOverworld();
        private void RemoveOw_Click(object sender, RoutedEventArgs e) => VM?.RemoveOverworld();
        private void AddWarp_Click(object sender, RoutedEventArgs e) => VM?.AddWarp();
        private void RemoveWarp_Click(object sender, RoutedEventArgs e) => VM?.RemoveWarp();
        private void AddTrig_Click(object sender, RoutedEventArgs e) => VM?.AddTrigger();
        private void RemoveTrig_Click(object sender, RoutedEventArgs e) => VM?.RemoveTrigger();
        private void AddSpawn_Click(object sender, RoutedEventArgs e) => VM?.AddSpawnable();
        private void RemoveSpawn_Click(object sender, RoutedEventArgs e) => VM?.RemoveSpawnable();

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
