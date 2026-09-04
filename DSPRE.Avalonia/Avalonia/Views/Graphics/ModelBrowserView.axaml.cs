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
            var vm = new ModelBrowserViewModel();
            vm.Reload();
            return vm;
        }

        public ModelBrowserView(ModelBrowserViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
            // Drag to turn it round, wheel to come closer, the same as everywhere else in DSPRE.
            _nav = new Gl3DPointerNavigation(GlHost, GlView);
            vm.ModelReady += (_, _) =>
            {
                GlView.SetModel(vm.Model3D);
                // What the chosen animations do to each material this frame. Null means nothing does.
                GlView.SetTextureMatrices(vm.TextureMatrices);
                GlView.SetTextureSwaps(vm.TextureSwaps);
                GlView.SetMaterialFades(vm.MaterialFades);
                GlView.SetHiddenMaterials(vm.HiddenMaterials);
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
            var vm = ViewModel;
            if (vm == null) return;
            vm.Playing = !vm.Playing;
        }

        /// <summary>Empties the search box, which is what the button beside it is for.</summary>
        private void ClearSearch_Click(object sender, RoutedEventArgs e)
        {
            SearchBox.Text = "";
            SearchBox.Focus();
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private async void SaveDae_Click(object sender, RoutedEventArgs e) => await Save(glb: false);
        private async void SaveGlb_Click(object sender, RoutedEventArgs e) => await Save(glb: true);

        private async System.Threading.Tasks.Task Save(bool glb)
        {
            var vm = ViewModel;
            if (vm?.Selected == null) return;
            if (!vm.CanSaveModel)
            {
                await DialogHelper.ShowInfo(vm.SaveModelHelp, "Save as a 3D file");
                return;
            }

            string ext = glb ? ".glb" : ".dae";
            var type = glb
                ? new FilePickerFileType("glTF model") { Patterns = new[] { "*.glb" } }
                : new FilePickerFileType("Collada model") { Patterns = new[] { "*.dae" } };

            string path = await DialogHelper.SaveFile(this, "Save this model",
                new[] { type }, vm.SuggestedFileName(ext));
            if (path == null) return;

            string err = vm.SaveAsThreeD(path, glb);
            vm.Status = err ?? $"Saved to {path}. The shape and its pictures are in there; the animations "
                             + "are separate entries and are not.";
            if (err != null) await DialogHelper.ShowInfo(err, "Save as a 3D file");
        }

        private async void PutIn_Click(object sender, RoutedEventArgs e)
        {
            var vm = ViewModel;
            if (vm?.Selected == null)
            {
                await DialogHelper.ShowInfo("Pick something on the left first.", "Put a file in");
                return;
            }
            if (!vm.CanPutFileIn)
            {
                await DialogHelper.ShowInfo(vm.PutFileInHelp, "Put a file in");
                return;
            }

            string path = await DialogHelper.OpenFile(this, "Choose a file to put in",
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
                vm.Status = err;
                await DialogHelper.ShowInfo(err, "Put a file in");
                return;
            }

            await System.Threading.Tasks.Task.Run(vm.Scan);
            vm.Publish();
            vm.Status = note == null
                ? "That file is in. Save the ROM to keep it."
                : note + " Save the ROM to keep it.";
            if (note != null) await DialogHelper.ShowInfo(vm.Status, "Mesh put in as a model");
        }

        private async void SaveRaw_Click(object sender, RoutedEventArgs e)
        {
            var vm = ViewModel;
            if (vm?.Selected == null)
            {
                await DialogHelper.ShowInfo("Pick something on the left first.", "Save file");
                return;
            }

            string path = await DialogHelper.SaveFile(this, "Save this file as it is",
                new[] { new FilePickerFileType("The file as it is in the ROM") { Patterns = new[] { "*.*" } } },
                vm.SuggestedFileName(".bin"));
            if (path == null) return;

            string err = vm.SaveFileAsItIs(path);
            vm.Status = err ?? $"Saved to {path}, exactly as it sits in the ROM.";
            if (err != null) await DialogHelper.ShowInfo(err, "Save file");
        }
    }
}
