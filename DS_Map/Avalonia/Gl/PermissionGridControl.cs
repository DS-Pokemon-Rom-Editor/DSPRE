using System;
using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Input;
using global::Avalonia.Media;

namespace DSPRE.Avalonia.Gl
{
    /// <summary>
    /// Paintable 32×32 byte grid for map movement permissions (collision / type).
    /// Click or drag to fill cells with the current paint value; cells are colour-coded
    /// by value. Raises <see cref="Changed"/> on edits so the editor can mark itself dirty.
    /// </summary>
    public class PermissionGridControl : Control
    {
        public const int Size = 32;

        private byte[,] _data;
        private bool _painting;
        public byte PaintValue { get; set; }
        public bool IsCollision { get; set; } = true;
        public event EventHandler Changed;

        public PermissionGridControl()
        {
            ClipToBounds = true;
            Focusable = false;
        }

        public void SetData(byte[,] data)
        {
            _data = data;
            InvalidateVisual();
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            _painting = true;
            e.Pointer.Capture(this);
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

        private double CellSize => Math.Max(1, Math.Min(Bounds.Width, Bounds.Height) / Size);

        private void Paint(Point p)
        {
            if (_data == null) return;
            double cs = CellSize;
            int col = (int)(p.X / cs);
            int row = (int)(p.Y / cs);
            if (col < 0 || col >= Size || row < 0 || row >= Size) return;
            if (_data[row, col] == PaintValue) return;
            _data[row, col] = PaintValue;
            InvalidateVisual();
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public override void Render(DrawingContext ctx)
        {
            double cs = CellSize;
            double full = cs * Size;
            ctx.FillRectangle(Brushes.Black, new Rect(0, 0, full, full));
            if (_data == null) return;

            var grid = new Pen(new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)));
            for (int row = 0; row < Size; row++)
                for (int col = 0; col < Size; col++)
                {
                    var brush = PermissionColors.Brush(_data[row, col], IsCollision);
                    ctx.FillRectangle(brush, new Rect(col * cs, row * cs, cs, cs));
                }
            // light grid lines
            for (int i = 0; i <= Size; i++)
            {
                ctx.DrawLine(grid, new Point(i * cs, 0), new Point(i * cs, full));
                ctx.DrawLine(grid, new Point(0, i * cs), new Point(full, i * cs));
            }
        }
    }

    /// <summary>Deterministic colour mapping for permission values.</summary>
    public static class PermissionColors
    {
        public static IBrush Brush(byte value, bool isCollision)
        {
            var (r, g, b) = Rgb(value, isCollision);
            return new SolidColorBrush(Color.FromRgb((byte)(r * 255), (byte)(g * 255), (byte)(b * 255)));
        }

        /// <summary>Normalized (0–1) RGB for a permission value, shared by the 2D grid and the 3D overlay.</summary>
        public static (float r, float g, float b) Rgb(byte value, bool isCollision)
        {
            if (isCollision)
            {
                if (value == 0x00) return (60 / 255f, 160 / 255f, 70 / 255f);   // walkable
                if (value == 0x80) return (180 / 255f, 60 / 255f, 60 / 255f);   // blocked
            }
            var c = FromHsv((value * 47) % 360, 0.55, 0.85);
            return (c.R / 255f, c.G / 255f, c.B / 255f);
        }

        private static Color FromHsv(double h, double s, double v)
        {
            double c = v * s;
            double x = c * (1 - Math.Abs((h / 60.0) % 2 - 1));
            double m = v - c;
            double r = 0, g = 0, b = 0;
            if (h < 60) { r = c; g = x; }
            else if (h < 120) { r = x; g = c; }
            else if (h < 180) { g = c; b = x; }
            else if (h < 240) { g = x; b = c; }
            else if (h < 300) { r = x; b = c; }
            else { r = c; b = x; }
            return Color.FromRgb((byte)((r + m) * 255), (byte)((g + m) * 255), (byte)((b + m) * 255));
        }
    }
}
