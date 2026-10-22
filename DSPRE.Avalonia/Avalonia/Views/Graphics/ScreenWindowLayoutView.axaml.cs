using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using DSPRE.Avalonia.ViewModels.Graphics;

namespace DSPRE.Avalonia.Views.Graphics
{
    public partial class ScreenWindowLayoutView : UserControl
    {
        private ScreenWindowLayoutViewModel VM => DataContext as ScreenWindowLayoutViewModel;

        private ScreenWindowRow _dragging;
        private (int X, int Y) _grabbedAt;

        public ScreenWindowLayoutView()
        {
            InitializeComponent();
            DrawGrid(this.FindControl<Canvas>("TopGrid"));
            DrawGrid(this.FindControl<Canvas>("BottomGrid"));
            DataContextChanged += (_, _) =>
            {
                if (VM == null) return;
                VM.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(ScreenWindowLayoutViewModel.ScreensIndex)) Arrange(); };
                Arrange();
            };
        }

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

        public ScreenWindowLayoutView(ScreenWindowLayoutViewModel vm) : this() => DataContext = vm;

        // The grid the boxes snap to, like the HP bar editor's board.
        private static void DrawGrid(Canvas grid)
        {
            if (grid == null) return;
            double s = ScreenWindowLayoutViewModel.Tile;
            IBrush line = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
            for (int x = 0; x <= 32; x++)
                grid.Children.Add(new Line { StartPoint = new Point(x * s, 0), EndPoint = new Point(x * s, 24 * s), Stroke = line, StrokeThickness = 1 });
            for (int y = 0; y <= 24; y++)
                grid.Children.Add(new Line { StartPoint = new Point(0, y * s), EndPoint = new Point(32 * s, y * s), Stroke = line, StrokeThickness = 1 });
        }

        private static (int X, int Y) TileAt(Point p) =>
            ((int)(p.X / ScreenWindowLayoutViewModel.Tile), (int)(p.Y / ScreenWindowLayoutViewModel.Tile));

        private void Board_Pressed(object sender, PointerPressedEventArgs e)
        {
            if (VM == null || sender is not Control board) return;
            (int x, int y) = TileAt(e.GetPosition(board));
            // Picks the smallest window under the pointer, so windows that share tiles can both be picked.
            _dragging = (board.Name == "BottomBoard" ? VM.BottomWindows : VM.TopWindows)
                .Where(r => x >= r.Window.X && x < r.Window.X + r.Window.Width && y >= r.ShownY && y < r.ShownY + r.Window.Height)
                .OrderBy(r => r.Window.Width * r.Window.Height).FirstOrDefault();
            if (_dragging == null) return;
            _grabbedAt = (x - _dragging.Window.X, y - _dragging.ShownY);
            e.Pointer.Capture(board);
        }

        private void Board_Moved(object sender, PointerEventArgs e)
        {
            if (_dragging == null || sender is not Control board) return;
            (int x, int y) = TileAt(e.GetPosition(board));
            // A scrolled window shows higher than its row, so the scroll is added back.
            VM?.Move(_dragging, x - _grabbedAt.X, y - _grabbedAt.Y + _dragging.Scrolled);
        }

        private void Board_Released(object sender, PointerReleasedEventArgs e)
        {
            if (_dragging == null) return;
            _dragging = null;
            e.Pointer.Capture(null);
            VM?.Commit();
        }

        private void Save_Click(object sender, RoutedEventArgs e) => VM?.SaveChanges();
        private void SizePatch_Click(object sender, RoutedEventArgs e) => VM?.OpenSizePatch();
        private void RebuildCursor_Click(object sender, RoutedEventArgs e) => VM?.RebuildCursor();
        private void RebuildPanelArt_Click(object sender, RoutedEventArgs e) => VM?.RebuildPanelArt();
        private void RebuildBackground_Click(object sender, RoutedEventArgs e) => VM?.RebuildBackground();

        private async void Discard_Click(object sender, RoutedEventArgs e)
        {
            if (await DialogHelper.ConfirmDiscardAsync(this)) VM?.DiscardChanges();
        }
    }
}
