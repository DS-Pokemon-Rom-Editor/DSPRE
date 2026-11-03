using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using DSPRE.Avalonia.Data;
using DSPRE.Avalonia.ViewModels.Graphics;

namespace DSPRE.Avalonia.Views.Graphics
{
    /// <summary>The pages of a book (the Pokédex, the summary screen) with their layers and files, editable in place.</summary>
    public partial class ScreenPagesView : UserControl
    {
        private ScreenPagesEditorViewModel VM => DataContext as ScreenPagesEditorViewModel;
        private Window Owner => TopLevel.GetTopLevel(this) as Window;

        private static readonly FilePickerFileType Png = new("PNG image") { Patterns = new[] { "*.png" } };

        public ScreenPagesView()
        {
            AvaloniaXamlLoader.Load(this);
            Loaded += (_, _) => ApplyShows();
        }

        private string _shows = "All";
        /// <summary>Which part of the book this view shows: "Pieces" (layers and files), "Layout" (text boxes and bars) or "All".</summary>
        public string Shows { get => _shows; set { _shows = value ?? "All"; ApplyShows(); } }

        private void ApplyShows()
        {
            Control layers = this.FindControl<Control>("LayerList"), parts = this.FindControl<Control>("PartPanel"),
                    boxes = this.FindControl<Control>("BoxPanel"), heading = this.FindControl<Control>("LayerHeading"),
                    left = this.FindControl<Control>("LeftColumn");
            if (layers == null || parts == null || boxes == null || heading == null || left == null) return;
            bool layout = _shows == "Layout";
            layers.IsVisible = !layout;
            heading.IsVisible = !layout;
            parts.IsVisible = !layout;
            // On its own, the box list sits at the top, as wide as the HP bar editor's part list.
            DockPanel.SetDock(boxes, layout ? Dock.Top : Dock.Bottom);
            if (layout) left.Width = 300;
            if (_shows == "Pieces") boxes.IsVisible = false;
            foreach (string name in new[] { "TopBoxes", "BottomBoxes" })
                if (this.FindControl<Control>(name) is Control overlay) overlay.IsVisible = layout;
        }

        private string ErrorTitle => (VM?.Title ?? "Screen") + " error";

        private ScreenBoxRow _dragging;
        private (int X, int Y) _grabbedAt;

        private static (int X, int Y) TileAt(Point p) => ((int)(p.X / ScreenBoxRow.Tile), (int)(p.Y / ScreenBoxRow.Tile));

        private void Screen_Pressed(object sender, PointerPressedEventArgs e)
        {
            if (VM == null || sender is not Image image || image.Bounds.Width <= 0) return;
            if (_shows == "Layout")
            {
                bool top = image.Name == "TopScreen";
                (int tx, int ty) = TileAt(e.GetPosition(image));
                // Picks the smallest box under the pointer, so boxes that share tiles can both be picked.
                _dragging = (top ? VM.TopBoxes : VM.BottomBoxes)
                    .Where(r => tx >= r.Across && tx < r.Across + r.Item.Width && ty >= r.Down && ty < r.Down + r.Item.Height)
                    .OrderBy(r => r.Item.Width * r.Item.Height).FirstOrDefault();
                if (_dragging == null) return;
                _grabbedAt = (tx - _dragging.Across, ty - _dragging.Down);
                e.Pointer.Capture(image);
                return;
            }
            Point p = e.GetPosition(image);
            int x = (int)(p.X * ScreenPageComposer.Width / image.Bounds.Width);
            int y = (int)(p.Y * ScreenPageComposer.Height / image.Bounds.Height);
            VM.PickAt(image.Name == "TopScreen", x, y);
        }

        private void Screen_Moved(object sender, PointerEventArgs e)
        {
            if (_dragging == null || sender is not Image image) return;
            (int x, int y) = TileAt(e.GetPosition(image));
            _dragging.Drag(x - _grabbedAt.X, y - _grabbedAt.Y);
        }

        // The page is redrawn once, when the box is dropped.
        private void Screen_Released(object sender, PointerReleasedEventArgs e)
        {
            if (_dragging == null) return;
            ScreenBoxRow dropped = _dragging;
            _dragging = null;
            e.Pointer.Capture(null);
            dropped.Drop();
        }

        private async void Paint_Click(object sender, RoutedEventArgs e) => await DialogHelper.GuardAsync(PaintAsync, "The painter didn't open.", ErrorTitle, Owner);
        private async void Export_Click(object sender, RoutedEventArgs e) => await DialogHelper.GuardAsync(ExportAsync, "The PNG wasn't exported.", ErrorTitle, Owner);
        private async void Import_Click(object sender, RoutedEventArgs e) => await DialogHelper.GuardAsync(ImportAsync, "The PNG wasn't imported.", ErrorTitle, Owner);
        private async void Colour_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Control { Tag: int number }) await DialogHelper.GuardAsync(() => ChangeColourAsync(number), "The colour wasn't changed.", ErrorTitle, Owner);
        }

        private async Task PaintAsync()
        {
            ScreenPartRow part = VM?.SelectedPart;
            GraphicAssets.Archive archive = ScreenPagesEditorViewModel.ArchiveOf(part);
            if (archive == null || !await WarnIfSharedAsync(part) || await RowAsync(part) is not int row) return;
            // The painter works on the saved file, so pending imports are settled first.
            if (!await RecordSwitchGuard.ConfirmLeaveAsync(VM, Owner, "graphic", "Save them before painting?")) return;
            // The painter saves on its own, so the page is put together again once it closes.
            AvaloniaEditorLauncher.OpenPainter(archive, part.Member, () => VM?.Reload(), part.ColoursFile, row);
        }

        private async Task ExportAsync()
        {
            ScreenPartRow part = VM?.SelectedPart;
            GraphicAssets.Archive archive = ScreenPagesEditorViewModel.ArchiveOf(part);
            if (archive == null || await RowAsync(part) is not int row) return;
            string name = VM.Title.ToLowerInvariant().Replace(' ', '_').Replace("é", "e");
            string path = await DialogHelper.SaveFileAsync(Owner, "Save as a PNG", new[] { Png }, $"{name}_{part.Member}.png");
            if (path == null) return;
            string trouble;
            using (VM.Reading()) trouble = GraphicAssets.ExportPng(archive, part.Member, path, part.ColoursFile, row);
            if (trouble != null) await DialogHelper.ShowErrorAsync(trouble, ErrorTitle);
            else SaveNotice.Exported(path);
        }

        private async Task ImportAsync()
        {
            ScreenPartRow part = VM?.SelectedPart;
            GraphicAssets.Archive archive = ScreenPagesEditorViewModel.ArchiveOf(part);
            if (archive == null || !await WarnIfSharedAsync(part) || await RowAsync(part) is not int row) return;
            string path = await DialogHelper.OpenFileAsync(Owner, "Import PNG", new[] { Png });
            if (path == null) return;
            (PngColourPrompt.Choice choice, GraphicAssets.PngColourCheck check) = await PngColourPrompt.AskAsync(Owner, archive, part.Member, path, part.ColoursFile, row);
            if (choice == PngColourPrompt.Choice.Cancel) return;
            string note = null;
            string trouble = VM.Import(() => PngColourPrompt.Import(archive, part.Member, path, choice, check, out note, part.ColoursFile, row));
            if (trouble != null) { await DialogHelper.ShowErrorAsync(trouble, ErrorTitle); return; }
            if (!string.IsNullOrEmpty(note)) await DialogHelper.ShowInfoAsync(note, VM.Title);
            SaveNotice.Imported(part.Name.ToLowerInvariant() + " " + part.Member, path);
        }

        private static async Task<bool> WarnIfSharedAsync(ScreenPartRow part)
        {
            if (string.IsNullOrEmpty(part.SharedNote)) return true;
            return await DialogHelper.AskThreeWayAsync(
                part.SharedNote + "\n\nChanging it changes all of them.",
                "This piece is shared", "Change it", "Leave it alone", "Cancel")
                == DialogHelper.MsgResult.Yes;
        }

        // Asks only when the pieces use more than one row; null to stop.
        private static async Task<int?> RowAsync(ScreenPartRow part)
        {
            if (part.Rows.Count < 2) return part.ColoursRow;
            DialogHelper.MsgResult pick = await DialogHelper.AskThreeWayAsync(
                $"This drawing is shown in colour rows {string.Join(", ", part.Rows)}. Which row should it use here?",
                "Colour row", "Row " + part.Rows[0], "Row " + part.Rows[1], "Cancel");
            return pick switch
            {
                DialogHelper.MsgResult.Yes => part.Rows[0],
                DialogHelper.MsgResult.No => part.Rows[1],
                _ => null,
            };
        }

        private async Task ChangeColourAsync(int number)
        {
            if (VM == null) return;
            (byte R, byte G, byte B)? now = VM.Swatches.FirstOrDefault(s => s.Number == number)?.Fill is global::Avalonia.Media.ISolidColorBrush brush
                ? (brush.Color.R, brush.Color.G, brush.Color.B) : null;
            (byte R, byte G, byte B)? picked = await DialogHelper.PickColourAsync(Owner, "Change colour " + number % 16, now);
            if (picked == null) return;
            string trouble = VM.SetColour(number, picked.Value.R, picked.Value.G, picked.Value.B);
            if (trouble != null) await DialogHelper.ShowErrorAsync(trouble, ErrorTitle);
        }

        private void Save_Click(object sender, RoutedEventArgs e) => VM?.SaveChanges();
        private async void Discard_Click(object sender, RoutedEventArgs e)
        {
            if (await DialogHelper.ConfirmDiscardAsync(this)) VM?.DiscardChanges();
        }
        private void Undo_Click(object sender, RoutedEventArgs e) => VM?.Undo();
        private void Redo_Click(object sender, RoutedEventArgs e) => VM?.Redo();
    }
}
