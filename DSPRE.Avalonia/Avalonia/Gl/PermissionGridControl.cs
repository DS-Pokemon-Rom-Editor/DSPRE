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

        public int UsedRows { get; set; } = Size;
        public int UsedColumns { get; set; } = Size;

        public event EventHandler<(int col, int row)?> Hovered;

        public (int col, int row)? Marked
        {
            get => _marked;
            set { _marked = value; InvalidateVisual(); }
        }
        private (int col, int row)? _marked;

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
            Hovered?.Invoke(this, At(e.GetPosition(this)));
        }

        protected override void OnPointerExited(PointerEventArgs e)
        {
            base.OnPointerExited(e);
            Hovered?.Invoke(this, null);
        }

        private (int col, int row)? At(Point p)
        {
            double cs = CellSize;
            int col = (int)(p.X / cs), row = (int)(p.Y / cs);
            if (col < 0 || row < 0 || col >= Size || row >= Size) return null;
            return (col, row);
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
            if (col < 0 || col >= Math.Min(Size, UsedColumns) || row < 0 || row >= Math.Min(Size, UsedRows)) return;
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
                    if (row >= UsedRows || col >= UsedColumns)
                        ctx.FillRectangle(new SolidColorBrush(Color.FromArgb(190, 0, 0, 0)),
                            new Rect(col * cs, row * cs, cs, cs));
                }
            // light grid lines
            for (int i = 0; i <= Size; i++)
            {
                ctx.DrawLine(grid, new Point(i * cs, 0), new Point(i * cs, full));
                ctx.DrawLine(grid, new Point(0, i * cs), new Point(full, i * cs));
            }

            var every = new Pen(new SolidColorBrush(Color.FromArgb(110, 255, 255, 255)));
            for (int i = 0; i <= Size; i += 4)
            {
                ctx.DrawLine(every, new Point(i * cs, 0), new Point(i * cs, full));
                ctx.DrawLine(every, new Point(0, i * cs), new Point(full, i * cs));
            }

            if (_marked is (int mc, int mr) && mc >= 0 && mr >= 0 && mc < Size && mr < Size)
            {
                var ring = new Pen(new SolidColorBrush(Color.FromRgb(0xFF, 0xD8, 0x40)), 2);
                ctx.DrawRectangle(null, ring, new Rect(mc * cs, mr * cs, cs, cs));
            }
        }
    }

    /// <summary>Colours for permission values, keyed by what the value means in the loaded game.</summary>
    public static class PermissionColors
    {
        public static IBrush Brush(byte value, bool isCollision)
        {
            var (r, g, b) = Rgb(value, isCollision);
            return new SolidColorBrush(Color.FromRgb((byte)(r * 255), (byte)(g * 255), (byte)(b * 255)));
        }

        /// <summary>Normalized (0 to 1) RGB for a permission value, shared by the 2D grid and the 3D overlay.</summary>
        public static (float r, float g, float b) Rgb(byte value, bool isCollision)
            => DSPRE.ROMFiles.TilePermissions.Colour(value, isCollision, DSPRE.RomInfo.gameFamily);
    }
}
