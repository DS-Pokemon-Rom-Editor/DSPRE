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
        private async void Save_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
            => await DialogHelper.GuardAsync(() => { (DataContext as BuildingEditorViewModel)?.SaveChanges(); return System.Threading.Tasks.Task.CompletedTask; }, "The building models weren't saved.", ErrorTitle, this);
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

        private const string ErrorTitle = "Building editor error";
        private async void Import_Click(object sender, RoutedEventArgs e)
            => await DialogHelper.GuardAsync(() => VM?.ImportAsync() ?? Task.CompletedTask, "The model wasn't imported.", ErrorTitle, this);
        private async void Export_Click(object sender, RoutedEventArgs e)
            => await DialogHelper.GuardAsync(() => VM?.ExportAsync(FormatOf(sender)) ?? Task.CompletedTask, "The model wasn't exported.", ErrorTitle, this);
        private async void ExportAll_Click(object sender, RoutedEventArgs e)
            => await DialogHelper.GuardAsync(() => VM?.ExportAllAsync(FormatOf(sender)) ?? Task.CompletedTask, "The building models weren't exported.", ErrorTitle, this);

        private static BuildingEditorViewModel.ExportFormat FormatOf(object sender)
            => Enum.Parse<BuildingEditorViewModel.ExportFormat>((string)((Control)sender).Tag);
    }
}
