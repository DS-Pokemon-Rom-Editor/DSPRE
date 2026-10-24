using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using DSPRE.Avalonia.Data;

namespace DSPRE.Avalonia.Views.Controls
{
    /// <summary>
    /// A move animation's recorded run as lanes of bars, frames running left to right, with a ruler and a playhead.
    /// Clicking a bar picks its command; clicking the ruler asks to show that frame.
    /// </summary>
    public sealed class MoveTimelineView : Control
    {
        public const double Gutter = 96;
        private const double RulerHeight = 20, RowHeight = 18, LaneGap = 6;
        private static readonly string[] LaneNames = { "Script", "Particles", "Effects", "Background", "Sound" };
        private static readonly Color[] LaneColours =
        {
            Color.FromRgb(0x8A, 0x8F, 0x98), Color.FromRgb(0x4F, 0x9D, 0xD9), Color.FromRgb(0xE0, 0x8A, 0x3C),
            Color.FromRgb(0x5F, 0xB8, 0x7A), Color.FromRgb(0x9B, 0x7F, 0xD4),
        };

        private IReadOnlyList<BattleAnimTrace.Item> _bars = Array.Empty<BattleAnimTrace.Item>();
        private int[] _rows = Array.Empty<int>();
        private Rect[] _boxes = Array.Empty<Rect>();
        private int _total = 1, _playhead = -1, _selected = -1;
        private readonly int[] _laneRows = new int[LaneNames.Length];
        private double _frameWidth = 6;

        public event Action<int> CommandPicked;
        public event Action<int> SeekRequested;

        /// <summary>Hands over the bars and how many frames the move runs.</summary>
        public void SetBars(IReadOnlyList<BattleAnimTrace.Item> bars, int totalFrames)
        {
            _bars = bars ?? Array.Empty<BattleAnimTrace.Item>();
            _rows = new int[_bars.Count];
            _boxes = new Rect[_bars.Count];
            _total = Math.Max(1, totalFrames);
            Pack();
            InvalidateMeasure();
            InvalidateVisual();
        }

        /// <summary>The frame being shown, or -1 for none.</summary>
        public int Playhead { get => _playhead; set { if (_playhead != value) { _playhead = value; InvalidateVisual(); } } }

        /// <summary>The command whose bars are highlighted, or -1.</summary>
        public int SelectedCommand { get => _selected; set { if (_selected != value) { _selected = value; InvalidateVisual(); } } }

        /// <summary>Screen pixels per frame.</summary>
        public double FrameWidth
        {
            get => _frameWidth;
            set { double v = Math.Clamp(value, 1, 40); if (v != _frameWidth) { _frameWidth = v; InvalidateMeasure(); InvalidateVisual(); } }
        }

        // Bars in one lane that overlap go on separate rows.
        private void Pack()
        {
            Array.Clear(_laneRows, 0, _laneRows.Length);
            List<int>[] rowEnds = new List<int>[LaneNames.Length];
            for (int i = 0; i < rowEnds.Length; i++) rowEnds[i] = new List<int>();
            for (int i = 0; i < _bars.Count; i++)
            {
                BattleAnimTrace.Item b = _bars[i];
                // Commands follow one another, so the script is one row with instant ones as ticks.
                if (b.Lane == BattleAnimTrace.Lane.Script) { _rows[i] = 0; if (rowEnds[0].Count == 0) rowEnds[0].Add(0); continue; }
                List<int> ends = rowEnds[(int)b.Lane];
                int row = ends.FindIndex(e => e <= b.Start);
                if (row < 0) { ends.Add(0); row = ends.Count - 1; }
                ends[row] = Math.Max(b.End, b.Start + 1);
                _rows[i] = row;
            }
            for (int i = 0; i < rowEnds.Length; i++) _laneRows[i] = Math.Max(1, rowEnds[i].Count);
        }

        private double LaneTop(int lane)
        {
            double y = RulerHeight + 4;
            for (int i = 0; i < lane; i++) y += _laneRows[i] * RowHeight + LaneGap;
            return y;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            double h = LaneTop(LaneNames.Length);
            return new Size(Gutter + (_total + 2) * _frameWidth, h);
        }

        public double XOf(int frame) => Gutter + frame * _frameWidth;

        /// <summary>The height that shows every lane without scrolling.</summary>
        public double ContentHeight => LaneTop(LaneNames.Length);

        public int FrameAt(double x) => Math.Clamp((int)Math.Floor((x - Gutter) / _frameWidth), 0, _total);

        private BattleAnimTrace.Item BarAt(Point p)
        {
            for (int i = _bars.Count - 1; i >= 0; i--) if (_boxes[i].Contains(p)) return _bars[i];
            return null;
        }

        public MoveTimelineView() { Focusable = true; }

        // The lane names stay put while the bars scroll under them.
        private ScrollViewer _scroller;
        private double ScrollX => _scroller?.Offset.X ?? 0;

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            _scroller = this.FindAncestorOfType<ScrollViewer>();
            if (_scroller != null) _scroller.ScrollChanged += OnScrolled;
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            if (_scroller != null) _scroller.ScrollChanged -= OnScrolled;
            _scroller = null;
            base.OnDetachedFromVisualTree(e);
        }

        private void OnScrolled(object sender, ScrollChangedEventArgs e) => InvalidateVisual();

        /// <summary>Finds the value a bar can be dragged by: its end, or the whole bar when the flag is set.</summary>
        public Func<BattleAnimTrace.Item, bool, BattleAnimTrace.Knob> FindKnob;
        /// <summary>A drag ended on a new value.</summary>
        public event Action<BattleAnimTrace.Knob, int> KnobDropped;

        private const double EdgeGrip = 5, DragStart = 4;
        private BattleAnimTrace.Item _pressBar, _dragBar;
        private bool _pressOnEdge, _pressOnRuler, _probed;
        private double _pressX;
        private BattleAnimTrace.Knob _knob;
        private int _dragSteps;

        private bool OnEdge(BattleAnimTrace.Item b, Point p)
        {
            int i = IndexOf(b);
            return i >= 0 && p.X >= _boxes[i].Right - EdgeGrip;
        }

        private int IndexOf(BattleAnimTrace.Item b)
        {
            for (int i = 0; i < _bars.Count; i++) if (ReferenceEquals(_bars[i], b)) return i;
            return -1;
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            Focus();
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
            Point p = e.GetPosition(this);
            if (p.X - ScrollX < Gutter) return;
            if (p.Y < RulerHeight) { _pressOnRuler = true; e.Pointer.Capture(this); SeekRequested?.Invoke(FrameAt(p.X)); e.Handled = true; return; }
            BattleAnimTrace.Item b = BarAt(p);
            if (b == null || b.Command < 0) return;
            CommandPicked?.Invoke(b.Command);
            _pressBar = b; _pressOnEdge = OnEdge(b, p); _pressX = p.X; _probed = false;
            e.Pointer.Capture(this);
            e.Handled = true;
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            Point p = e.GetPosition(this);
            if (_pressOnRuler)
            {
                SeekRequested?.Invoke(FrameAt(p.X));
                return;
            }
            if (_pressBar != null)
            {
                // Look the value up only once the bar is actually dragged, since that runs the move a few times.
                if (!_probed && Math.Abs(p.X - _pressX) >= DragStart)
                {
                    _probed = true;
                    _knob = FindKnob?.Invoke(_pressBar, !_pressOnEdge);
                    if (_knob != null) { _dragBar = _pressBar; _dragSteps = 0; }
                    else ToolTip.SetTip(this, "Can't be dragged");
                }
                if (_knob != null)
                {
                    int steps = (int)Math.Round((p.X - _pressX) / _frameWidth / _knob.FramesPerStep);
                    steps = Math.Clamp(steps, _knob.Min - _knob.Value, _knob.Max - _knob.Value);
                    if (steps != _dragSteps) { _dragSteps = steps; InvalidateVisual(); }
                }
                return;
            }
            BattleAnimTrace.Item b = BarAt(p);
            ToolTip.SetTip(this, b?.Tip);
            Cursor = b != null && b.Command >= 0 && OnEdge(b, p) ? new Cursor(StandardCursorType.SizeWestEast) : Cursor.Default;
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            BattleAnimTrace.Knob knob = _knob;
            int steps = _dragSteps;
            EndPress();
            e.Pointer.Capture(null);
            if (knob != null && steps != 0) KnobDropped?.Invoke(knob, knob.Value + steps);
        }

        protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
        {
            base.OnPointerCaptureLost(e);
            EndPress();
        }

        private void EndPress()
        {
            bool dragging = _dragBar != null;
            _pressOnRuler = false; _pressBar = null; _dragBar = null; _knob = null; _dragSteps = 0;
            if (dragging) InvalidateVisual();
        }

        protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
        {
            base.OnPointerWheelChanged(e);
            if ((e.KeyModifiers & KeyModifiers.Control) == 0) return;
            FrameWidth *= e.Delta.Y > 0 ? 1.25 : 0.8;
            e.Handled = true;
        }

        public override void Render(DrawingContext ctx)
        {
            double w = Bounds.Width, h = Bounds.Height;
            if (w <= 0 || h <= 0) return;
            // Clicks only land where something was drawn, so the empty ruler needs a fill too.
            ctx.FillRectangle(Brushes.Transparent, new Rect(0, 0, w, h));
            IBrush text = this.TryFindResource("Editor.Subtle", ActualThemeVariant, out object found) && found is IBrush brush ? brush : Brushes.Gray;
            Typeface face = new Typeface("Segoe UI, Inter, sans-serif");
            Pen grid = new Pen(new SolidColorBrush(Color.FromArgb(40, 128, 128, 128)), 1);

            // Ruler: a tick every 10 frames, numbered every 20 or more so the numbers don't collide.
            int every = _frameWidth >= 4 ? 20 : _frameWidth >= 2 ? 40 : 100;
            for (int f = 0; f <= _total; f += 10)
            {
                double x = Gutter + f * _frameWidth;
                ctx.DrawLine(grid, new Point(x, f % every == 0 ? 2 : 12), new Point(x, h));
                if (f % every == 0) DrawText(ctx, f.ToString(CultureInfo.InvariantCulture), x + 2, 2, 11, text, face);
            }

            for (int i = 0; i < _bars.Count; i++)
            {
                BattleAnimTrace.Item b = _bars[i];
                // A bar being dragged shows where the new value puts it.
                int shift = ReferenceEquals(b, _dragBar) ? _dragSteps * _knob.FramesPerStep : 0;
                int start = b.Start + (_knob != null && _knob.MovesBar ? shift : 0), end = b.End + shift;
                double x = Gutter + start * _frameWidth;
                double bw = Math.Max(2, (end - start) * _frameWidth);
                double y = LaneTop((int)b.Lane) + _rows[i] * RowHeight;
                Rect box = new Rect(x, y, bw, RowHeight - 3);
                _boxes[i] = box;
                Color c = LaneColours[(int)b.Lane];
                bool picked = b.Command >= 0 && b.Command == _selected;
                ctx.FillRectangle(new SolidColorBrush(c, picked ? 0.95 : 0.6), box, 2);
                if (picked) ctx.DrawRectangle(new Pen(Brushes.White, 1), box, 2);
                if (bw > 24)
                    using (ctx.PushClip(box))
                        DrawText(ctx, b.Label, x + 3, y + 1, 11, Brushes.White, face);
                if (shift != 0 || ReferenceEquals(b, _dragBar))
                    DrawText(ctx, $"{_knob.Name} {_knob.Value + _dragSteps}", box.Right + 4, y + 1, 11, text, face);
            }

            if (_playhead >= 0)
            {
                double x = Gutter + _playhead * _frameWidth;
                ctx.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(0xE5, 0x48, 0x48)), 2), new Point(x, 0), new Point(x, h));
            }

            double sx = ScrollX;
            IBrush gutter = this.TryFindResource("Editor.PanelBg", ActualThemeVariant, out object bg) && bg is IBrush b2 ? b2 : Brushes.Transparent;
            ctx.FillRectangle(gutter, new Rect(sx, 0, Gutter - 2, h));
            for (int lane = 0; lane < LaneNames.Length; lane++)
                DrawText(ctx, LaneNames[lane], sx + 4, LaneTop(lane) + 1, 12, text, face);
        }

        private static void DrawText(DrawingContext ctx, string s, double x, double y, double size, IBrush brush, Typeface face)
        {
            if (string.IsNullOrEmpty(s)) return;
            FormattedText ft = new FormattedText(s, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, face, size, brush);
            ctx.DrawText(ft, new Point(x, y));
        }
    }
}
