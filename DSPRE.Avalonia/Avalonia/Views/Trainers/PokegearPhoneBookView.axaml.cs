using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DSPRE.Avalonia.ViewModels.Trainers;

namespace DSPRE.Avalonia.Views.Trainers
{
    public partial class PokegearPhoneBookView : UserControl
    {
        private PokegearPhoneBookViewModel VM => DataContext as PokegearPhoneBookViewModel;

        private const double DragThreshold = 4;
        private const double ScrollEdge = 28;

        private int _dragFrom = -1;
        private int _dropAt = -1;
        private bool _dragging;
        private Point _pressAt;
        private Point _lastPointer;
        private readonly DispatcherTimer _autoScroll;

        public PokegearPhoneBookView()
        {
            InitializeComponent();

            SortList.AddHandler(PointerPressedEvent, SortList_PointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
            SortList.AddHandler(PointerMovedEvent, SortList_PointerMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
            SortList.AddHandler(PointerReleasedEvent, SortList_PointerReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
            SortList.PointerCaptureLost += (_, _) => EndDrag(apply: false);
            SortList.AddHandler(KeyDownEvent, SortList_KeyDown, RoutingStrategies.Tunnel);

            _autoScroll = new DispatcherTimer { Interval = System.TimeSpan.FromMilliseconds(60) };
            _autoScroll.Tick += (_, _) => AutoScroll();
        }

        public PokegearPhoneBookView(PokegearPhoneBookViewModel vm) : this()
        {
            DataContext = vm;
        }

        private void Save_Click(object sender, RoutedEventArgs e) => VM?.Save();

        private void AutoSort_Click(object sender, RoutedEventArgs e) => VM?.AutoSort();

        private void OpenRematch_Click(object sender, RoutedEventArgs e)
        {
            if (VM?.RematchRow >= 0) AvaloniaEditorLauncher.OpenPokegearRematchEditor(VM.RematchRow);
        }

        // ── Dragging a sort row ──

        private void SortList_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (!e.GetCurrentPoint(SortList).Properties.IsLeftButtonPressed) return;
            _dragFrom = (e.Source as Visual)?.FindAncestorOfType<ListBoxItem>(includeSelf: true) is ListBoxItem item
                ? SortList.IndexFromContainer(item) : -1;
            _pressAt = e.GetPosition(this);
        }

        private void SortList_PointerMoved(object sender, PointerEventArgs e)
        {
            if (_dragFrom < 0 || VM == null) return;
            _lastPointer = e.GetPosition(this);

            if (!_dragging)
            {
                if (!e.GetCurrentPoint(SortList).Properties.IsLeftButtonPressed) { _dragFrom = -1; return; }
                Vector moved = _lastPointer - _pressAt;
                if (System.Math.Abs(moved.X) < DragThreshold && System.Math.Abs(moved.Y) < DragThreshold) return;

                _dragging = true;
                e.Pointer.Capture(SortList);
                DragGhostText.Text = WithoutPosition(VM.SortRows[_dragFrom]);
                DragGhost.IsVisible = true;
                _autoScroll.Start();
            }

            MoveGhost();
            ShowDropLine();
            e.Handled = true;
        }

        private void SortList_PointerReleased(object sender, PointerReleasedEventArgs e)
        {
            if (!_dragging) { _dragFrom = -1; return; }
            e.Handled = true;
            // Ends the drag before releasing capture, whose lost-capture event would otherwise cancel it.
            EndDrag(apply: true);
            e.Pointer.Capture(null);
        }

        private void EndDrag(bool apply)
        {
            if (!_dragging) return;
            _dragging = false;
            _autoScroll.Stop();
            DragGhost.IsVisible = false;
            DropLine.IsVisible = false;

            int from = _dragFrom, insertAt = _dropAt;
            _dragFrom = _dropAt = -1;
            if (!apply || insertAt < 0 || VM == null) return;

            // Dropping below the row being moved lands one higher once it has left its old place.
            int to = insertAt > from ? insertAt - 1 : insertAt;
            if (to == from) return;
            VM.MoveSortRow(from, to);
            SortList.SelectedIndex = to;
        }

        private void MoveGhost()
        {
            Canvas.SetLeft(DragGhost, _lastPointer.X + 14);
            Canvas.SetTop(DragGhost, _lastPointer.Y + 6);
        }

        /// <summary>Finds the gap the pointer is nearest and draws the line there.</summary>
        private void ShowDropLine()
        {
            Point inList = this.TranslatePoint(_lastPointer, SortList) ?? default;
            var rows = SortList.GetRealizedContainers()
                .Select(c => (Index: SortList.IndexFromContainer(c), Top: c.TranslatePoint(default, SortList)?.Y ?? 0, c.Bounds.Height))
                .Where(r => r.Index >= 0)
                .OrderBy(r => r.Index)
                .ToList();
            if (rows.Count == 0) { DropLine.IsVisible = false; _dropAt = -1; return; }

            var target = rows.FirstOrDefault(r => inList.Y < r.Top + r.Height);
            bool pastLast = target.Height == 0;
            if (pastLast) target = rows[^1];

            bool before = !pastLast && inList.Y < target.Top + target.Height / 2;
            _dropAt = before ? target.Index : target.Index + 1;

            // Rows kept ready just outside the view would put the line over the tabs or the status bar.
            double lineY = System.Math.Clamp(before ? target.Top : target.Top + target.Height, 1, SortList.Bounds.Height - 1);
            Point at = SortList.TranslatePoint(new Point(4, lineY), this) ?? default;
            DropLine.Width = System.Math.Max(0, SortList.Bounds.Width - 12);
            Canvas.SetLeft(DropLine, at.X);
            Canvas.SetTop(DropLine, at.Y - DropLine.Height / 2);
            DropLine.IsVisible = true;
        }

        private void AutoScroll()
        {
            if (!_dragging) return;
            var scroller = SortList.FindDescendantOfType<ScrollViewer>();
            if (scroller == null) return;

            double y = (this.TranslatePoint(_lastPointer, SortList) ?? default).Y;
            double step = 0;
            if (y < ScrollEdge) step = -20;
            else if (y > SortList.Bounds.Height - ScrollEdge) step = 20;
            if (step == 0) return;

            scroller.Offset = new Vector(scroller.Offset.X, System.Math.Max(0, scroller.Offset.Y + step));
            ShowDropLine();
        }

        private static string WithoutPosition(string row)
        {
            int dot = row.IndexOf(".  ");
            return dot >= 0 ? row.Substring(dot + 3) : row;
        }

        private void SortList_KeyDown(object sender, KeyEventArgs e)
        {
            if (_dragging && e.Key == Key.Escape)
            {
                EndDrag(apply: false);
                e.Handled = true;
                return;
            }
            if (VM == null || e.KeyModifiers != KeyModifiers.Alt || (e.Key != Key.Up && e.Key != Key.Down)) return;
            int from = SortList.SelectedIndex;
            int to = from + (e.Key == Key.Up ? -1 : 1);
            if (from < 0 || to < 0 || to >= SortList.ItemCount) return;
            VM.MoveSortRow(from, to);
            SortList.SelectedIndex = to;
            e.Handled = true;
        }
    }
}
