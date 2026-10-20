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
        private void Save_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e) => (DataContext as BuildingEditorViewModel)?.SaveChanges();
        private async void Discard_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (await DialogHelper.ConfirmDiscardAsync(this)) (DataContext as BuildingEditorViewModel)?.DiscardChanges();
        }

        private async void OnLoadedSetup(object sender, RoutedEventArgs e)
        {
            if (_setupDone || Design.IsDesignMode) return;
            BuildingEditorViewModel vm = VM;
            if (vm == null) return;
            _setupDone = true;
            vm.ModelLoaded += (_, _) => GlView.SetModel(VM.Model3D);
            await vm.SetupAsync(this);
        }

        private async void Import_Click(object sender, RoutedEventArgs e) => await SafeAsync(VM?.ImportAsync());
        private async void Export_Click(object sender, RoutedEventArgs e) => await SafeAsync(VM?.ExportAsync(FormatOf(sender)));
        private async void ExportAll_Click(object sender, RoutedEventArgs e) => await SafeAsync(VM?.ExportAllAsync(FormatOf(sender)));

        private static BuildingEditorViewModel.ExportFormat FormatOf(object sender)
            => Enum.Parse<BuildingEditorViewModel.ExportFormat>((string)((Control)sender).Tag);

        private static async Task SafeAsync(Task task)
        {
            if (task == null) return;
            try { await task; }
            catch (System.Exception ex)
            {
                CrashReporter.LogHandled(ex);
                await DialogHelper.ShowErrorAsync(ex.Message);
            }
        }
    }
}
