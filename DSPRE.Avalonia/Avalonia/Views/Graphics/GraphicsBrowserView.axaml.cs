using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views.Graphics
{
    public partial class GraphicsBrowserView : Window
    {
        private GraphicsBrowserViewModel ViewModel => (GraphicsBrowserViewModel)DataContext;

        private static readonly FilePickerFileType Png =
            new FilePickerFileType("PNG picture") { Patterns = new[] { "*.png" } };

        public GraphicsBrowserView() : this(new GraphicsBrowserViewModel()) { }

        public GraphicsBrowserView(GraphicsBrowserViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
        }

        /// <summary>Empties the search box, which is what the button beside it is for.</summary>
        private void ClearSearch_Click(object sender, RoutedEventArgs e)
        {
            SearchBox.Text = "";
            SearchBox.Focus();
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private async void SavePicture_Click(object sender, RoutedEventArgs e)
        {
            var vm = ViewModel;
            if (vm?.Selected == null) return;

            string path = await DialogHelper.SaveFile(this, "Save this picture",
                new[] { Png }, vm.SuggestedFileName(".png"));
            if (path == null) return;

            string err = vm.SavePicture(path);
            vm.Status = err ?? $"Saved to {path}. It keeps its numbered colours, so it can go back in.";
            if (err != null) await DialogHelper.ShowInfo(err, "Save picture");
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

        private async void Replace_Click(object sender, RoutedEventArgs e)
        {
            var vm = ViewModel;
            if (vm?.Selected == null) return;

            // The button is off when this cannot work, and its tooltip says why, but somebody may still get
            // here another way, so say it plainly rather than doing nothing.
            if (!vm.CanReplace)
            {
                await DialogHelper.ShowInfo(vm.ReplaceHelp, "Put a picture in");
                return;
            }

            string path = await DialogHelper.OpenFile(this, "Choose a PNG to put in", new[] { Png });
            if (path == null) return;

            string err = vm.Replace(path, out string note);
            vm.Status = err ?? "That picture is in. Save the ROM to keep it.";
            if (err != null) { await DialogHelper.ShowInfo(err, "Put a picture in"); return; }

            // A background shares its pieces, so painting one square changes every square drawn from the
            // same one. Say so rather than leaving it to be found later.
            if (!string.IsNullOrEmpty(note))
            {
                vm.Status = note;
                await DialogHelper.ShowInfo(note, "Put a picture in");
            }
        }

        /// <summary>Sends you to whatever decides this graphic's numbers, which is the other half of the
        /// hand-off those editors make coming this way.</summary>
        private void OpenOwner_Click(object sender, RoutedEventArgs e) => ViewModel?.OpenOwningEditor();

        private async void Paint_Click(object sender, RoutedEventArgs e)
        {
            var vm = ViewModel;
            if (vm?.Selected == null) return;

            if (!vm.CanReplace)
            {
                await DialogHelper.ShowInfo(vm.ReplaceHelp, "Paint this");
                return;
            }

            var painter = new GraphicPainterView(
                new GraphicPainterViewModel(vm.ShowingArchive, vm.ShowingIndex));
            painter.ShowManaged();
        }
    }
}
