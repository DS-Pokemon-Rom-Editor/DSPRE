using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using DSPRE.Avalonia.Gl;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views.Graphics
{
    public partial class ModelBrowserView : Window
    {
        private ModelBrowserViewModel ViewModel => (ModelBrowserViewModel)DataContext;
        private Gl3DPointerNavigation _nav;

        // The designer's path, and anything that has not been given a view model. It still reads on
        // the spot; only the launcher's path does the reading away from the UI thread.
        public ModelBrowserView() : this(Loaded()) { }

        private static ModelBrowserViewModel Loaded()
        {
            ModelBrowserViewModel vm = new ModelBrowserViewModel();
            vm.Reload();
            return vm;
        }

        public ModelBrowserView(ModelBrowserViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
            EditorWindowChrome.Attach(this, vm, onClosed: vm.Dispose);
            EditorWindowChrome.AttachUndoKeys(this, vm);
            // An import, undo, redo or discard can change what kind of thing an entry is, so the list is read again.
            vm.PendingChanged += async () =>
            {
                await System.Threading.Tasks.Task.Run(vm.Scan);
                vm.Publish();
            };
            // Drag to turn it round, wheel to come closer, the same as everywhere else in DSPRE.
            _nav = new Gl3DPointerNavigation(GlHost, GlView);
            vm.ModelReady += (_, _) =>
            {
                GlView.SetModel(vm.Model3D);
                // What the chosen animations do to each material this frame. Null means nothing does.
                GlView.SetTextureMatrices(vm.TextureMatrices);
                GlView.SetTextureSwaps(vm.TextureSwaps);
                GlView.SetMaterialFades(vm.MaterialFades);
                GlView.SetHiddenNodes(vm.HiddenNodes);
                GlView.SetMaterialColours(vm.MaterialColours);
            };

            // The DS runs these at 30 frames a second, so that is the speed they are played back at.
            _clock = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
            _clock.Tick += (_, _) => ViewModel?.Step();
            vm.PlayingChanged += (_, _) =>
            {
                if (ViewModel?.Playing == true) _clock.Start(); else _clock.Stop();
            };
            Closed += (_, _) => _clock.Stop();
        }

        private readonly DispatcherTimer _clock;

        private void Play_Click(object sender, RoutedEventArgs e)
        {
            ModelBrowserViewModel vm = ViewModel;
            if (vm == null) return;
            vm.Playing = !vm.Playing;
        }

        private void ClearSearch_Click(object sender, RoutedEventArgs e)
        {
            SearchBox.Text = "";
            SearchBox.Focus();
        }

        private void Save_Click(object sender, RoutedEventArgs e) => ViewModel?.SaveChanges();
        private void OpenDeepEditor_Click(object sender, RoutedEventArgs e) => ViewModel?.OpenDeepEditor();
        private async void Discard_Click(object sender, RoutedEventArgs e)
        {
            if (await DialogHelper.ConfirmDiscardAsync(this)) ViewModel?.DiscardChanges();
        }

        private async void SaveDae_Click(object sender, RoutedEventArgs e) => await SaveAsync(glb: false);
        private async void SaveGlb_Click(object sender, RoutedEventArgs e) => await SaveAsync(glb: true);

        private async System.Threading.Tasks.Task SaveAsync(bool glb)
        {
            ModelBrowserViewModel vm = ViewModel;
            if (vm?.Selected == null) return;
            if (!vm.CanSaveModel)
            {
                await DialogHelper.ShowInfoAsync(vm.SaveModelHelp, "Export model");
                return;
            }

            string ext = glb ? ".glb" : ".dae";
            FilePickerFileType type = glb
                ? new FilePickerFileType("glTF model") { Patterns = new[] { "*.glb" } }
                : new FilePickerFileType("Collada model") { Patterns = new[] { "*.dae" } };

            string path = await DialogHelper.SaveFileAsync(this, "Export model",
                new[] { type }, vm.SuggestedFileName(ext));
            if (path == null) return;

            string err = vm.SaveAsThreeD(path, glb, out string written);
            if (err != null) { await DialogHelper.ShowErrorAsync(err, "All models and textures"); return; }
            if (written == null) return;
            SaveNotice.Exported(written);
        }

        private async void PutIn_Click(object sender, RoutedEventArgs e)
        {
            ModelBrowserViewModel vm = ViewModel;
            if (vm?.Selected == null)
            {
                await DialogHelper.ShowInfoAsync("Pick something on the left first.", "Import");
                return;
            }
            if (!vm.CanPutFileIn)
            {
                await DialogHelper.ShowInfoAsync(vm.PutFileInHelp, "Import");
                return;
            }

            string path = await DialogHelper.OpenFileAsync(this, "Import",
                new[]
                {
                    new FilePickerFileType("3D files")
                    {
                        Patterns = new[] { "*.nsbmd", "*.nsbtx", "*.nsbca", "*.nsbta", "*.nsbtp",
                                           "*.nsbva", "*.nsbma", "*.bin", "*.obj" },
                    },
                    new FilePickerFileType("OBJ meshes") { Patterns = new[] { "*.obj" } },
                });
            if (path == null) return;

            string err = vm.PutFileIn(path, out string note);
            if (err != null)
            {
                vm.StatusText = err;
                await DialogHelper.ShowErrorAsync(err, "All models and textures");
                return;
            }

            SaveNotice.Imported(vm.Selected.Name ?? $"{vm.Selected.Archive.Title} {vm.Selected.Index}", path);
            vm.StatusText = note ?? "";
            if (note != null) await DialogHelper.ShowInfoAsync(note, "Import");
        }

        private async void SaveRaw_Click(object sender, RoutedEventArgs e)
        {
            ModelBrowserViewModel vm = ViewModel;
            if (vm?.Selected == null)
            {
                await DialogHelper.ShowInfoAsync("Pick something on the left first.", "Export file");
                return;
            }

            string path = await DialogHelper.SaveFileAsync(this, "Export file",
                new[] { new FilePickerFileType("The file as it is in the ROM") { Patterns = new[] { "*.*" } } },
                vm.SuggestedFileName(".bin"));
            if (path == null) return;

            string err = vm.SaveFileAsItIs(path);
            if (err != null) { await DialogHelper.ShowErrorAsync(err, "All models and textures"); return; }
            SaveNotice.Exported(path);
        }
    }
}
