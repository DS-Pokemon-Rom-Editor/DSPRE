using System;
using System.Linq;
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
            EditorWindowChrome.Attach(this, vm);
            EditorWindowChrome.AttachUndoKeys(this, vm);
        }

        private void Save_Click(object sender, RoutedEventArgs e) => ViewModel?.SaveChanges();

        private async void PaletteColour_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Control { Tag: int number } || ViewModel == null) return;
            (byte R, byte G, byte B)? now = ViewModel.PaletteColours.FirstOrDefault(s => s.Number == number)?.Fill is global::Avalonia.Media.ISolidColorBrush brush
                ? (brush.Color.R, brush.Color.G, brush.Color.B) : null;
            (byte R, byte G, byte B)? picked = await DialogHelper.PickColour(this, "Change colour " + number, now);
            if (picked == null) return;
            string err = ViewModel.SetPaletteColour(number, picked.Value.R, picked.Value.G, picked.Value.B);
            if (err != null) await DialogHelper.ShowError(err, "Change colour");
        }
        private void Discard_Click(object sender, RoutedEventArgs e) => ViewModel?.DiscardChanges();

        /// <summary>Empties the search box, which is what the button beside it is for.</summary>
        private void ClearSearch_Click(object sender, RoutedEventArgs e)
        {
            SearchBox.Text = "";
            SearchBox.Focus();
        }

        private async void SavePicture_Click(object sender, RoutedEventArgs e)
        {
            GraphicsBrowserViewModel vm = ViewModel;
            if (vm?.Selected == null) return;

            string path = await DialogHelper.SaveFile(this, "Export PNG",
                new[] { Png }, vm.SuggestedFileName(".png"));
            if (path == null) return;

            string err = vm.SavePicture(path);
            vm.Status = err ?? $"Exported to {path}.";
            if (err != null) await DialogHelper.ShowInfo(err, "Export PNG");
        }

        private async void SaveRaw_Click(object sender, RoutedEventArgs e)
        {
            GraphicsBrowserViewModel vm = ViewModel;
            if (vm?.Selected == null)
            {
                await DialogHelper.ShowInfo("Pick something on the left first.", "Export file");
                return;
            }

            string path = await DialogHelper.SaveFile(this, "Export file",
                new[] { new FilePickerFileType("The file as it is in the ROM") { Patterns = new[] { "*.*" } } },
                vm.SuggestedFileName(".bin"));
            if (path == null) return;

            string err = vm.SaveFileAsItIs(path);
            vm.Status = err ?? $"Exported to {path}.";
            if (err != null) await DialogHelper.ShowInfo(err, "Export file");
        }

        private async void Replace_Click(object sender, RoutedEventArgs e)
        {
            GraphicsBrowserViewModel vm = ViewModel;
            if (vm?.Selected == null) return;

            // The button is off when this cannot work, and its tooltip says why, but somebody may still get
            // here another way, so say it plainly rather than doing nothing.
            if (!vm.CanReplace)
            {
                await DialogHelper.ShowInfo(vm.ReplaceHelp, "Import PNG");
                return;
            }

            string path = await DialogHelper.OpenFile(this, "Import PNG", new[] { Png });
            if (path == null) return;

            string err = vm.Replace(path, out string note);
            vm.Status = err ?? "Imported. Save to keep it.";
            if (err != null) { await DialogHelper.ShowInfo(err, "Import PNG"); return; }

            // A background shares its pieces, so painting one square changes every square drawn from the
            // same one. Say so rather than leaving it to be found later.
            if (!string.IsNullOrEmpty(note))
            {
                vm.Status = note;
                await DialogHelper.ShowInfo(note, "Import PNG");
            }
        }

        /// <summary>Sends you to whatever decides this graphic's numbers, which is the other half of the
        /// hand-off those editors make coming this way.</summary>
        private void OpenOwner_Click(object sender, RoutedEventArgs e) => ViewModel?.OpenOwningEditor();

        private async void Paint_Click(object sender, RoutedEventArgs e)
        {
            GraphicsBrowserViewModel vm = ViewModel;
            if (vm?.Selected == null) return;

            if (!vm.CanReplace)
            {
                await DialogHelper.ShowInfo(vm.ReplaceHelp, "Paint");
                return;
            }

            // The painter works on the saved graphic, so pending imports are settled first.
            if (!await RecordSwitchGuard.ConfirmLeaveAsync(vm, this, "graphic", "Save them before painting?")) return;

            GraphicPainterView painter = new GraphicPainterView(
                new GraphicPainterViewModel(vm.ShowingArchive, vm.ShowingIndex));
            painter.ShowManaged();
        }
    }
}
