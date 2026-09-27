using System;
using System.Globalization;
using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Input;
using global::Avalonia.Media;

namespace DSPRE.Avalonia.Gl
{
    /// <summary>
    /// W×H integer grid for the Matrix editor (maps / headers / altitudes); EMPTY (0xFFFF) cells are shown blank.
    /// </summary>
    public class MatrixGridControl : Control
    {
        private const double CW = 38, CH = 26;
        private const int EMPTY = 65535;

        private int _w, _h;
        private Func<int, int, int> _get;
        private Action<int, int, int> _set;
        private bool _painting;
        private int _selCol = -1, _selRow = -1;

        public int PaintValue { get; set; }
        public bool PaintMode { get; set; }
        /// <summary>A cell's fill by kind of place; null draws a neutral fill.</summary>
        public Func<int, int, Color?> CellColour { get; set; }
        /// <summary>For heights: fill by value on a light-to-deep ramp instead of by place.</summary>
        public bool RampByValue { get; set; }

        /// <summary>Each cell's header, used to outline <see cref="FocusHeader"/>'s cells.</summary>
        public Func<int, int, int> HeaderAt { get; set; }
        private int _focusHeader = -1;
        public int FocusHeader { get => _focusHeader; set { _focusHeader = value; InvalidateVisual(); } }

        public event EventHandler Changed;
        public event EventHandler<(int col, int row, int value)> CellSelected;
        public event EventHandler<(int col, int row, int value)> CellActivated;

        public MatrixGridControl()
        {
            ClipToBounds = true;
            ActualThemeVariantChanged += (_, _) => InvalidateVisual();
        }

        public void SetSource(int width, int height, Func<int, int, int> getter, Action<int, int, int> setter)
        {
            _w = width; _h = height; _get = getter; _set = setter;
            _selCol = _selRow = -1;
            InvalidateMeasure();
            InvalidateVisual();
        }

        public Rect? FocusBounds()
        {
            if (HeaderAt == null || _focusHeader < 0 || _get == null) return null;
            int minC = int.MaxValue, minR = int.MaxValue, maxC = -1, maxR = -1;
            for (int r = 0; r < _h; r++)
                for (int c = 0; c < _w; c++)
                    if (InFocus(c, r)) { minC = Math.Min(minC, c); minR = Math.Min(minR, r); maxC = Math.Max(maxC, c); maxR = Math.Max(maxR, r); }
            return maxC < 0 ? null : new Rect(minC * CW, minR * CH, (maxC - minC + 1) * CW, (maxR - minR + 1) * CH);
        }

        private bool InFocus(int c, int r) =>
            c >= 0 && r >= 0 && c < _w && r < _h && HeaderAt != null && _focusHeader >= 0 && HeaderAt(c, r) == _focusHeader;

        protected override Size MeasureOverride(Size availableSize)
            => new Size(Math.Max(1, _w) * CW, Math.Max(1, _h) * CH);

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
            Select(e.GetPosition(this));
            if (PaintMode)
            {
                _painting = true;
                e.Pointer.Capture(this);
                Paint(e.GetPosition(this));
            }
            else if (e.ClickCount >= 2 && _selCol >= 0)
                CellActivated?.Invoke(this, (_selCol, _selRow, _get(_selCol, _selRow)));
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            if (_painting) Paint(e.GetPosition(this));
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            _painting = false;
            e.Pointer.Capture(null);
        }

        private (int col, int row) CellAt(Point p) => ((int)(p.X / CW), (int)(p.Y / CH));

        private void Select(Point p)
        {
            var (c, r) = CellAt(p);
            if (_get == null || c < 0 || c >= _w || r < 0 || r >= _h) return;
            _selCol = c; _selRow = r;
            CellSelected?.Invoke(this, (c, r, _get(c, r)));
            InvalidateVisual();
        }

        private void Paint(Point p)
        {
            if (_get == null) return;
            var (c, r) = CellAt(p);
            if (c < 0 || c >= _w || r < 0 || r >= _h) return;
            if (_get(c, r) == PaintValue) return;
            _set(c, r, PaintValue);
            Changed?.Invoke(this, EventArgs.Empty);
            InvalidateVisual();
        }

        private IBrush Res(string key, IBrush fallback) =>
            this.TryFindResource(key, ActualThemeVariant, out var v) && v is IBrush b ? b : fallback;

        public override void Render(DrawingContext ctx)
        {
            IBrush back = Res("Editor.PanelBg", Brushes.Transparent);
            IBrush text = Res("Editor.Text", Brushes.Gainsboro);
            bool dark = ActualThemeVariant == global::Avalonia.Styling.ThemeVariant.Dark;

            ctx.FillRectangle(back, new Rect(0, 0, _w * CW, _h * CH));
            if (_get == null) return;

            var grid = new Pen(new SolidColorBrush(dark ? Color.FromArgb(40, 255, 255, 255) : Color.FromArgb(40, 0, 0, 0)));
            var typeface = new Typeface(FontFamily.Default);
            bool anyFocus = HeaderAt != null && _focusHeader >= 0;

            for (int row = 0; row < _h; row++)
                for (int col = 0; col < _w; col++)
                {
                    int val = _get(col, row);
                    var rect = new Rect(col * CW, row * CH, CW, CH);
                    if (val != EMPTY)
                    {
                        Color fill = RampByValue ? Ramp(val) : CellColour?.Invoke(col, row) ?? Color.FromRgb(0x4A, 0x50, 0x58);
                        ctx.FillRectangle(new SolidColorBrush(fill), rect);
                    }
                    ctx.DrawRectangle(grid, rect);
                    if (val != EMPTY)
                    {
                        var t = new FormattedText(val.ToString(), CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                            typeface, 11, Brushes.White);
                        ctx.DrawText(t, new Point(rect.X + (CW - t.Width) / 2, rect.Y + (CH - t.Height) / 2));
                    }
                }

            // Outline the current header's cells along the edges they don't share with each other.
            if (anyFocus)
            {
                var edge = new Pen(new SolidColorBrush(Color.FromRgb(0xFF, 0xD5, 0x3D)), 3);
                for (int r = 0; r < _h; r++)
                    for (int c = 0; c < _w; c++)
                    {
                        if (!InFocus(c, r)) continue;
                        double x = c * CW, y = r * CH;
                        if (!InFocus(c, r - 1)) ctx.DrawLine(edge, new Point(x, y), new Point(x + CW, y));
                        if (!InFocus(c, r + 1)) ctx.DrawLine(edge, new Point(x, y + CH), new Point(x + CW, y + CH));
                        if (!InFocus(c - 1, r)) ctx.DrawLine(edge, new Point(x, y), new Point(x, y + CH));
                        if (!InFocus(c + 1, r)) ctx.DrawLine(edge, new Point(x + CW, y), new Point(x + CW, y + CH));
                    }
            }

            if (_selCol >= 0 && _selRow >= 0)
                ctx.DrawRectangle(new Pen(text, 2), new Rect(_selCol * CW + 1, _selRow * CH + 1, CW - 2, CH - 2));
        }

        // Heights: deep blue low to warm high, dark enough for white numbers throughout.
        private static Color Ramp(int v)
        {
            double t = Math.Clamp(v / 32.0, 0, 1);
            return Color.FromRgb((byte)(0x2A + t * 0x90), (byte)(0x4A + t * 0x20), (byte)(0x8C - t * 0x60));
        }
    }
}
