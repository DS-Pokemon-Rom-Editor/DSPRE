using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DSPRE.Avalonia.Gl;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views.World
{
    public partial class BuildingEditorView : Window
    {
        private BuildingEditorViewModel VM => DataContext as BuildingEditorViewModel;
        private bool _setupDone;
        private Gl3DPointerNavigation _nav;

        public BuildingEditorView()
        {
            InitializeComponent();

            // Left-drag pans, right-drag orbits, wheel zooms. See Gl3DPointerNavigation.
            _nav = new Gl3DPointerNavigation(GlHost, GlView);

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
