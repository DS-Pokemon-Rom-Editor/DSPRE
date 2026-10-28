using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using DSPRE.Avalonia.ViewModels.Graphics;

namespace DSPRE.Avalonia.Views.Graphics
{
    /// <summary>Moves the summary screen's text boxes and bars.</summary>
    public partial class ScreenPagesLayoutView : UserControl
    {
        private ScreenPagesEditorViewModel VM => DataContext as ScreenPagesEditorViewModel;

        private ScreenBoxRow _dragging;
        private (int X, int Y) _grabbedAt;

        public ScreenPagesLayoutView()
        {
            AvaloniaXamlLoader.Load(this);
            DrawGrid(this.FindControl<Canvas>("TopGrid"));
            DrawGrid(this.FindControl<Canvas>("BottomGrid"));
            DataContextChanged += (_, _) =>
            {
                if (VM == null) return;
                VM.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(ScreenPagesEditorViewModel.ScreensIndex)) Arrange(); };
                Arrange();
            };
        }

        public ScreenPagesLayoutView(ScreenPagesEditorViewModel vm) : this() => DataContext = vm;

        // Both screens sit side by side; a single screen stacks the preview under the boxes.
        private void Arrange()
        {
            Grid panes = this.FindControl<Grid>("Panes");
            Control boards = this.FindControl<Control>("Boards"), previews = this.FindControl<Control>("Previews");
            if (panes == null || boards == null || previews == null || VM == null) return;
            bool single = VM.ScreensIndex != 0;
            panes.ColumnDefinitions = new ColumnDefinitions(single ? "*" : "*,*");
            panes.RowDefinitions = new RowDefinitions(single ? "Auto,Auto" : "Auto");
            Grid.SetColumn(previews, single ? 0 : 1);
            Grid.SetRow(previews, single ? 1 : 0);
        }

        private static void DrawGrid(Canvas grid)
        {
            if (grid == null) return;
            double s = ScreenBoxRow.Tile;
            IBrush line = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
            for (int x = 0; x <= 32; x++)
                grid.Children.Add(new Line { StartPoint = new Point(x * s, 0), EndPoint = new Point(x * s, 24 * s), Stroke = line, StrokeThickness = 1 });
            for (int y = 0; y <= 24; y++)
                grid.Children.Add(new Line { StartPoint = new Point(0, y * s), EndPoint = new Point(32 * s, y * s), Stroke = line, StrokeThickness = 1 });
        }

        private static (int X, int Y) TileAt(Point p) => ((int)(p.X / ScreenBoxRow.Tile), (int)(p.Y / ScreenBoxRow.Tile));

        private void Board_Pressed(object sender, PointerPressedEventArgs e)
        {
            if (VM == null || sender is not Control board) return;
            (int x, int y) = TileAt(e.GetPosition(board));
            // Picks the smallest box under the pointer, so boxes that share tiles can both be picked.
            _dragging = (board.Name == "BottomBoard" ? VM.BottomBoxes : VM.TopBoxes)
                .Where(r => x >= r.Across && x < r.Across + r.Item.Width && y >= r.Down && y < r.Down + r.Item.Height)
                .OrderBy(r => r.Item.Width * r.Item.Height).FirstOrDefault();
            if (_dragging == null) return;
            _grabbedAt = (x - _dragging.Across, y - _dragging.Down);
            e.Pointer.Capture(board);
        }

        private void Board_Moved(object sender, PointerEventArgs e)
        {
            if (_dragging == null || sender is not Control board) return;
            (int x, int y) = TileAt(e.GetPosition(board));
            _dragging.Drag(x - _grabbedAt.X, y - _grabbedAt.Y);
        }

        // The page is drawn again once, when the box is dropped.
        private void Board_Released(object sender, PointerReleasedEventArgs e)
        {
            if (_dragging == null) return;
            ScreenBoxRow dropped = _dragging;
            _dragging = null;
            e.Pointer.Capture(null);
            dropped.Drop();
        }

        private void Save_Click(object sender, RoutedEventArgs e) => VM?.SaveChanges();
        private void FontPatch_Click(object sender, RoutedEventArgs e) => VM?.OpenFontPatch();

        private async void Discard_Click(object sender, RoutedEventArgs e)
        {
            if (await DialogHelper.ConfirmDiscardAsync(this)) VM?.DiscardChanges();
        }
    }
}
