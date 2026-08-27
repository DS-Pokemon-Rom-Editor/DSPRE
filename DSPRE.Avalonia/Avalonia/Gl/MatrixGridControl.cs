using System;
using System.Globalization;
using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Input;
using global::Avalonia.Media;

namespace DSPRE.Avalonia.Gl
{
    /// <summary>
    /// Editable W×H integer grid for the Matrix editor (maps / headers / altitudes).
    /// Shows each cell's value; click or drag fills cells with the current
    /// <see cref="PaintValue"/>. Works on any 2-D source via get/set delegates, so it
    /// serves ushort and byte matrices alike. EMPTY (0xFFFF) cells are shown blank.
    /// </summary>
    public class MatrixGridControl : Control
    {
        private const double CW = 34, CH = 24;
        private const int EMPTY = 65535;

        private int _w, _h;
        private Func<int, int, int> _get;
        private Action<int, int, int> _set;
        private bool _painting;
        private int _selCol = -1, _selRow = -1;

        public int PaintValue { get; set; }
        /// <summary>When true the cell colour is a hue from its value (good for map IDs); else neutral.</summary>
        public bool ColorByValue { get; set; } = true;
        public event EventHandler Changed;
        public event EventHandler<(int col, int row, int value)> CellSelected;

        public MatrixGridControl() { ClipToBounds = true; }

        public void SetSource(int width, int height, Func<int, int, int> getter, Action<int, int, int> setter)
        {
            _w = width; _h = height; _get = getter; _set = setter;
            _selCol = _selRow = -1;
            InvalidateMeasure();
            InvalidateVisual();
        }

        protected override Size MeasureOverride(Size availableSize)
            => new Size(Math.Max(1, _w) * CW, Math.Max(1, _h) * CH);

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            _painting = true;
            e.Pointer.Capture(this);
            Select(e.GetPosition(this));
            Paint(e.GetPosition(this));
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
            if (c < 0 || c >= _w || r < 0 || r >= _h) return;
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

        public override void Render(DrawingContext ctx)
        {
            ctx.FillRectangle(Brushes.White, new Rect(0, 0, _w * CW, _h * CH));
            if (_get == null) return;

            var grid = new Pen(new SolidColorBrush(Color.FromArgb(60, 0, 0, 0)));
            var sel = new Pen(Brushes.OrangeRed, 2);
            var typeface = new Typeface(FontFamily.Default);

            for (int row = 0; row < _h; row++)
                for (int col = 0; col < _w; col++)
                {
                    int val = _get(col, row);
                    var rect = new Rect(col * CW, row * CH, CW, CH);

                    IBrush bg = Brushes.White;
                    if (val == EMPTY) bg = new SolidColorBrush(Color.FromRgb(235, 235, 235));
                    else if (ColorByValue) bg = new SolidColorBrush(HueColor(val));
                    ctx.FillRectangle(bg, rect);
                    ctx.DrawRectangle(grid, rect);

                    if (val != EMPTY)
                    {
                        var text = new FormattedText(val.ToString(), CultureInfo.InvariantCulture,
                            FlowDirection.LeftToRight, typeface, 11, Brushes.Black);
                        ctx.DrawText(text, new Point(rect.X + 3, rect.Y + (CH - text.Height) / 2));
                    }
                }

            if (_selCol >= 0 && _selRow >= 0)
                ctx.DrawRectangle(sel, new Rect(_selCol * CW, _selRow * CH, CW, CH));
        }

        private static Color HueColor(int v)
        {
            double h = (v * 53) % 360;
            // pastel
            double c = 0.35, x = c * (1 - Math.Abs((h / 60.0) % 2 - 1)), m = 0.75;
            double r = 0, g = 0, b = 0;
            if (h < 60) { r = c; g = x; } else if (h < 120) { r = x; g = c; }
            else if (h < 180) { g = c; b = x; } else if (h < 240) { g = x; b = c; }
            else if (h < 300) { r = x; b = c; } else { r = c; b = x; }
            return Color.FromRgb((byte)((r + m) * 255), (byte)((g + m) * 255), (byte)((b + m) * 255));
        }
    }
}
