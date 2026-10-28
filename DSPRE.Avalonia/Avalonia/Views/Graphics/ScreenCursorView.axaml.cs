using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels.Graphics;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.Views.Graphics
{
    public partial class ScreenCursorView : UserControl
    {
        private ICursorEditor VM => DataContext as ICursorEditor;

        // Holding an arrow key while clicking a stop points the selected stop's arrow there.
        private readonly HashSet<Key> _held = new HashSet<Key>();
        private ICursorEditor _shown;

        public ScreenCursorView()
        {
            InitializeComponent();
            KeyDown += (_, e) => _held.Add(e.Key);
            KeyUp += (_, e) => _held.Remove(e.Key);
            LostFocus += (_, _) => _held.Clear();
            DataContextChanged += (_, _) => Show(VM);
            AttachedToVisualTree += (_, _) => Show(VM);
            DetachedFromVisualTree += (_, _) => Show(null);
        }

        public ScreenCursorView(ICursorEditor vm) : this() => DataContext = vm;

        private void Show(ICursorEditor vm)
        {
            if (ReferenceEquals(vm, _shown)) return;
            if (_shown != null) _shown.CursorChanged -= Redraw;
            _shown = vm;
            if (vm == null) return;
            vm.CursorChanged += Redraw;
            Redraw();
        }

        private void Redraw() => this.FindControl<CursorArrows>("Arrows")?.Show(VM?.CursorStops.ToList(), VM?.SelectedStop);

        private CursorDirection? Held() =>
            _held.Contains(Key.Up) ? CursorDirection.Up : _held.Contains(Key.Down) ? CursorDirection.Down
            : _held.Contains(Key.Left) ? CursorDirection.Left : _held.Contains(Key.Right) ? CursorDirection.Right : null;

        private void Board_Pressed(object sender, PointerPressedEventArgs e)
        {
            if (VM == null || sender is not Control board) return;
            Focus();
            global::Avalonia.Point p = e.GetPosition(board);
            CursorStopRow hit = VM.CursorStops.OrderBy(r => (r.At.X - p.X) * (r.At.X - p.X) + (r.At.Y - p.Y) * (r.At.Y - p.Y))
                .FirstOrDefault(r => (r.At.X - p.X) * (r.At.X - p.X) + (r.At.Y - p.Y) * (r.At.Y - p.Y) <= CursorArrows.HitRadius * CursorArrows.HitRadius);
            if (hit == null) return;
            if (Held() is CursorDirection d && VM.SelectedStop != null) VM.SetDirection(VM.SelectedStop.Stop, d, hit.Stop);
            else VM.SelectedStop = hit;
            e.Handled = true;
        }

        private void Save_Click(object sender, RoutedEventArgs e) => VM?.SaveChanges();

        private async void Discard_Click(object sender, RoutedEventArgs e)
        {
            if (await DialogHelper.ConfirmDiscardAsync(this)) VM?.DiscardChanges();
        }
    }
}
