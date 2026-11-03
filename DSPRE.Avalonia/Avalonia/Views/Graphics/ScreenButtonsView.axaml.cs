using System;
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
    public partial class ScreenButtonsView : UserControl
    {
        private ScreenButtonsViewModel VM => DataContext as ScreenButtonsViewModel;

        // Drags snap to whole tiles because the game's buttons sit on tiles.
        private const int Snap = 8;

        // How close to a part's spot a press has to land, in screen pixels.
        private const int PartReach = 5;

        private TouchAreaRow _dragging;
        private ButtonPartRow _draggingPart;
        private (int X, int Y) _grabbedAt;

        public ScreenButtonsView()
        {
            InitializeComponent();
            DrawGrid(this.FindControl<Canvas>("Grid"));
        }

        public ScreenButtonsView(ScreenButtonsViewModel vm) : this() => DataContext = vm;

        // The picture is drawn at twice the screen's size.
        private void Preview_Pressed(object sender, PointerPressedEventArgs e)
        {
            if (VM == null || sender is not Control image) return;
            Point p = e.GetPosition(image);
            VM.SquareClick(p.X / 2, p.Y / 2, e.GetCurrentPoint(image).Properties.IsRightButtonPressed);
        }

        private static void DrawGrid(Canvas grid)
        {
            if (grid == null) return;
            double s = Snap * ScreenButtonsViewModel.Scale;
            IBrush line = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
            for (int x = 0; x <= 32; x++)
                grid.Children.Add(new Line { StartPoint = new Point(x * s, 0), EndPoint = new Point(x * s, 24 * s), Stroke = line, StrokeThickness = 1 });
            for (int y = 0; y <= 24; y++)
                grid.Children.Add(new Line { StartPoint = new Point(0, y * s), EndPoint = new Point(32 * s, y * s), Stroke = line, StrokeThickness = 1 });
        }

        private static (int X, int Y) PixelAt(Point p) =>
            ((int)(p.X / ScreenButtonsViewModel.Scale), (int)(p.Y / ScreenButtonsViewModel.Scale));

        private void Board_Pressed(object sender, PointerPressedEventArgs e)
        {
            if (VM == null || sender is not Control board) return;
            (int x, int y) = PixelAt(e.GetPosition(board));
            _draggingPart = VM.Parts.Where(r => Math.Abs(r.Across - x) <= PartReach && Math.Abs(r.Down - y) <= PartReach)
                .OrderBy(r => Math.Abs(r.Across - x) + Math.Abs(r.Down - y)).FirstOrDefault();
            if (_draggingPart != null)
            {
                e.Pointer.Capture(board);
                return;
            }
            // Pick the smallest area under the pointer so overlapping areas can both be grabbed.
            _dragging = VM.Areas.Where(r => r.Open && x >= r.Across && x < r.Across + r.Wide && y >= r.Down && y < r.Down + r.Tall)
                .OrderBy(r => r.Wide * r.Tall).FirstOrDefault();
            if (_dragging == null) return;
            _grabbedAt = (x - _dragging.Across, y - _dragging.Down);
            e.Pointer.Capture(board);
        }

        private void Board_Moved(object sender, PointerEventArgs e)
        {
            if (sender is not Control board) return;
            (int x, int y) = PixelAt(e.GetPosition(board));
            if (_draggingPart != null)
            {
                VM?.MovePartTo(_draggingPart, Math.Clamp(x, 0, 255), Math.Clamp(y, 0, 191));
                return;
            }
            if (_dragging == null) return;
            // Snap the distance moved so an area off the tile grid keeps its offset.
            int left = _dragging.Across + (int)Math.Round((x - _grabbedAt.X - _dragging.Across) / (double)Snap) * Snap;
            int top = _dragging.Down + (int)Math.Round((y - _grabbedAt.Y - _dragging.Down) / (double)Snap) * Snap;
            VM?.MoveTo(_dragging, left, top);
        }

        private void Board_Released(object sender, PointerReleasedEventArgs e)
        {
            if (_dragging == null && _draggingPart == null) return;
            _dragging = null;
            _draggingPart = null;
            e.Pointer.Capture(null);
            VM?.Commit();
        }


        private void Save_Click(object sender, RoutedEventArgs e) => VM?.SaveChanges();

        private async void Discard_Click(object sender, RoutedEventArgs e)
        {
            if (await DialogHelper.ConfirmDiscardAsync(this)) VM?.DiscardChanges();
        }
    }
}
