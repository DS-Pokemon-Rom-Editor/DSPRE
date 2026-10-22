using System;
using System.Linq;
using System.Threading.Tasks;
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

        private async void Save_Click(object sender, RoutedEventArgs e)
            => await DialogHelper.GuardAsync(() => { ViewModel?.SaveChanges(); return System.Threading.Tasks.Task.CompletedTask; }, "The graphics weren't saved.", ErrorTitle, this);

        private const string ErrorTitle = "Graphics error";
        private async void PaletteColour_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Control { Tag: int number }) await DialogHelper.GuardAsync(() => ChangeColourAsync(number), "The colour wasn't changed.", ErrorTitle, this);
        }
        private async void SavePicture_Click(object sender, RoutedEventArgs e) => await DialogHelper.GuardAsync(SavePictureAsync, "The PNG wasn't exported.", ErrorTitle, this);
        private async void SaveRaw_Click(object sender, RoutedEventArgs e) => await DialogHelper.GuardAsync(SaveRawAsync, "The file wasn't exported.", ErrorTitle, this);
        private async void Replace_Click(object sender, RoutedEventArgs e) => await DialogHelper.GuardAsync(ReplaceAsync, "The PNG wasn't imported.", ErrorTitle, this);
        private async void Paint_Click(object sender, RoutedEventArgs e) => await DialogHelper.GuardAsync(PaintAsync, "The painter didn't open.", ErrorTitle, this);

        private async Task ChangeColourAsync(int number)
        {
            if (ViewModel == null) return;
            (byte R, byte G, byte B)? now = ViewModel.PaletteColours.FirstOrDefault(s => s.Number == number)?.Fill is global::Avalonia.Media.ISolidColorBrush brush
                ? (brush.Color.R, brush.Color.G, brush.Color.B) : null;
            (byte R, byte G, byte B)? picked = await DialogHelper.PickColourAsync(this, "Change colour " + number, now);
            if (picked == null) return;
            string err = ViewModel.SetPaletteColour(number, picked.Value.R, picked.Value.G, picked.Value.B);
            if (err != null) await DialogHelper.ShowErrorAsync(err, "Change colour");
        }
        private async void Discard_Click(object sender, RoutedEventArgs e)
        {
            if (await DialogHelper.ConfirmDiscardAsync(this)) ViewModel?.DiscardChanges();
        }

        /// <summary>Empties the search box, which is what the button beside it is for.</summary>
        private void ClearSearch_Click(object sender, RoutedEventArgs e)
        {
            SearchBox.Text = "";
            SearchBox.Focus();
        }

        private async Task SavePictureAsync()
        {
            GraphicsBrowserViewModel vm = ViewModel;
            if (vm?.Selected == null) return;

            string path = await DialogHelper.SaveFileAsync(this, "Export PNG",
                new[] { Png }, vm.SuggestedFileName(".png"));
            if (path == null) return;

            string err = vm.SavePicture(path);
            if (err != null) { await DialogHelper.ShowErrorAsync(err, ErrorTitle); return; }
            SaveNotice.Exported(path);
        }

        private async Task SaveRawAsync()
        {
            GraphicsBrowserViewModel vm = ViewModel;
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
            if (err != null) { await DialogHelper.ShowErrorAsync(err, ErrorTitle); return; }
            SaveNotice.Exported(path);
        }

        private async Task ReplaceAsync()
        {
            GraphicsBrowserViewModel vm = ViewModel;
            if (vm?.Selected == null) return;

            // The button is off when this cannot work, and its tooltip says why, but somebody may still get
            // here another way, so say it plainly rather than doing nothing.
            if (!vm.CanReplace)
            {
                await DialogHelper.ShowInfoAsync(vm.ReplaceHelp, "Import PNG");
                return;
            }

            string path = await DialogHelper.OpenFileAsync(this, "Import PNG", new[] { Png });
            if (path == null) return;

            (Data.GraphicAssets.Archive target, int at) = vm.ImportTarget;
            string err, note;
            Data.BattleGaugeComposer.Kind? bar = target?.Dir == RomInfo.DirNames.BattleObj ? Data.HpBarFit.KindOf(at) : null;
            HpBarImportPrompt.Choice barChoice = HpBarImportPrompt.Choice.PictureOnly;
            Data.HpBarFit.Plan plan = null;
            if (bar != null)
            {
                (barChoice, plan) = await HpBarImportPrompt.AskAsync(this, target, at, bar.Value, path);
                if (barChoice == HpBarImportPrompt.Choice.Cancel) return;
            }
            if (barChoice == HpBarImportPrompt.Choice.Fit)
            {
                err = vm.ReplaceHpBar(path, plan, out note);
            }
            else
            {
                (PngColourPrompt.Choice choice, Data.GraphicAssets.PngColourCheck check) = await PngColourPrompt.AskAsync(this, target, at, path, vm.ShownColours);
                if (choice == PngColourPrompt.Choice.Cancel) return;
                err = vm.Replace(path, choice, check, out note);
            }
            if (err != null) { vm.StatusText = err; await DialogHelper.ShowErrorAsync(err, ErrorTitle); return; }
            SaveNotice.Imported(vm.Selected.Name ?? $"{vm.Selected.Archive.Title} {vm.Selected.Index}", path);

            // A background shares its pieces, so painting one square changes every square drawn from the
            // same one. Say so rather than leaving it to be found later.
            if (!string.IsNullOrEmpty(note))
            {
                vm.StatusText = note;
                await DialogHelper.ShowInfoAsync(note, "Import PNG");
            }
        }

        /// <summary>Sends you to whatever decides this graphic's numbers, which is the other half of the
        /// hand-off those editors make coming this way.</summary>
        private void OpenOwner_Click(object sender, RoutedEventArgs e) => ViewModel?.OpenOwningEditor();

        private async Task PaintAsync()
        {
            GraphicsBrowserViewModel vm = ViewModel;
            if (vm?.Selected == null) return;

            if (!vm.CanPaint)
            {
                await DialogHelper.ShowInfoAsync(vm.PaintHelp, "Paint");
                return;
            }
            if (vm.ScreenEditor is (_, System.Action open))
            {
                open();
                return;
            }

            // The painter works on the saved graphic, so pending imports are settled first.
            if (!await RecordSwitchGuard.ConfirmLeaveAsync(vm, this, "graphic", "Save them before painting?")) return;

            AvaloniaEditorLauncher.OpenPainter(vm.ShowingArchive, vm.ShowingIndex, palette: vm.ShownColours);
        }
    }
}
