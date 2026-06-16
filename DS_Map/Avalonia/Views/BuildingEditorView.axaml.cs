using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views
{
    public partial class BuildingEditorView : Window
    {
        private BuildingEditorViewModel VM => DataContext as BuildingEditorViewModel;
        private bool _setupDone;
        private Point? _lastPointer;

        public BuildingEditorView()
        {
            InitializeComponent();

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

        public BuildingEditorView(BuildingEditorViewModel vm) : this() { DataContext = vm; EditorWindowChrome.Attach(this, vm); }

        private async void OnLoadedSetup(object sender, RoutedEventArgs e)
        {
            if (_setupDone || Design.IsDesignMode) return;
            var vm = VM;
            if (vm == null) return;
            _setupDone = true;
            vm.ModelLoaded += (_, _) => GlView.SetModel(VM.Model3D);
            await vm.SetupAsync(this);
        }

        private async void Import_Click(object sender, RoutedEventArgs e) => await Safe(VM?.ImportAsync());
        private async void Export_Click(object sender, RoutedEventArgs e) => await Safe(VM?.ExportAsync());

        private static async Task Safe(Task task)
        {
            if (task == null) return;
            try { await task; } catch { }
        }
    }
}
