using System;
using System.Collections.Generic;
using System.Globalization;
using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Input;
using global::Avalonia.Media;

namespace DSPRE.Avalonia.Gl
{
    /// <summary>
    /// Paintable 32×32 byte grid for map movement permissions (collision / type).
    /// Left-click or drag paints, middle-click fills, right-click picks; cells are colour-coded
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
            RenderOptions.SetTextRenderingMode(this, TextRenderingMode.Antialias);
        }

        public void SetData(byte[,] data)
        {
            _data = data;
            _text.Clear();
            InvalidateVisual();
        }

        /// <summary>Raised with the value under a right-click, so the painter can take it.</summary>
        public event EventHandler<byte> Picked;

        protected override Size MeasureOverride(Size availableSize)
        {
            double side = Math.Min(availableSize.Width, availableSize.Height);
            if (double.IsInfinity(side)) side = MinHeight;
            return new Size(Math.Max(side, MinHeight), Math.Max(side, MinHeight));
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            PointerUpdateKind kind = e.GetCurrentPoint(this).Properties.PointerUpdateKind;
            if (kind == PointerUpdateKind.RightButtonPressed)
            {
                if (_data != null && At(e.GetPosition(this)) is (int pc, int pr)) Picked?.Invoke(this, _data[pr, pc]);
                e.Handled = true;
                return;
            }
            if (kind == PointerUpdateKind.MiddleButtonPressed)
            {
                if (At(e.GetPosition(this)) is (int fc, int fr)) Fill(fc, fr);
                e.Handled = true;
                return;
            }
            _painting = true;
            e.Pointer.Capture(this);
            Paint(e.GetPosition(this));
        }

        private void Fill(int col, int row)
        {
            int cols = Math.Min(Size, UsedColumns), rows = Math.Min(Size, UsedRows);
            if (_data == null || col >= cols || row >= rows) return;
            byte from = _data[row, col];
            if (from == PaintValue) return;
            Stack<(int c, int r)> todo = new Stack<(int c, int r)>();
            todo.Push((col, row));
            while (todo.Count > 0)
            {
                (int c, int r) = todo.Pop();
                if (c < 0 || r < 0 || c >= cols || r >= rows || _data[r, c] != from) continue;
                _data[r, c] = PaintValue;
                todo.Push((c + 1, r)); todo.Push((c - 1, r)); todo.Push((c, r + 1)); todo.Push((c, r - 1));
            }
            InvalidateVisual();
            Changed?.Invoke(this, EventArgs.Empty);
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

        // Hex IDs per value, rebuilt when the cell size changes.
        private readonly Dictionary<(byte, bool), FormattedText> _text = new Dictionary<(byte, bool), FormattedText>();
        private double _textSize;

        private FormattedText IdText(byte value)
        {
            if (_text.TryGetValue((value, IsCollision), out FormattedText t)) return t;
            (float r, float g, float b) = PermissionColors.Rgb(value, IsCollision);
            IBrush ink = 0.299 * r + 0.587 * g + 0.114 * b > 0.55 ? Brushes.Black
                       : !IsCollision && value == 0 ? new SolidColorBrush(Color.FromArgb(70, 255, 255, 255)) : Brushes.White;
            t = new FormattedText(value.ToString("X2"), CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                                  Typeface.Default, Math.Min(12, _textSize * 0.6), ink);
            _text[(value, IsCollision)] = t;
            return t;
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

            Pen grid = new Pen(new SolidColorBrush(Color.FromArgb(140, 0, 0, 0)));
            bool ids = cs * (TopLevel.GetTopLevel(this)?.RenderScaling ?? 1) >= 12;
            if (ids && Math.Abs(_textSize - cs) > 0.01) { _text.Clear(); _textSize = cs; }
            for (int row = 0; row < Size; row++)
                for (int col = 0; col < Size; col++)
                {
                    byte v = _data[row, col];
                    Rect cell = new Rect(col * cs, row * cs, cs, cs);
                    ctx.FillRectangle(PermissionColors.Brush(v, IsCollision), cell);
                    if (ids)
                    {
                        FormattedText t = IdText(v);
                        ctx.DrawText(t, new Point(cell.X + (cs - t.Width) / 2, cell.Y + (cs - t.Height) / 2));
                    }
                    if (row >= UsedRows || col >= UsedColumns)
                        ctx.FillRectangle(new SolidColorBrush(Color.FromArgb(190, 0, 0, 0)), cell);
                }
            for (int i = 0; i <= Size; i++)
            {
                ctx.DrawLine(grid, new Point(i * cs, 0), new Point(i * cs, full));
                ctx.DrawLine(grid, new Point(0, i * cs), new Point(full, i * cs));
            }

            Pen every = new Pen(new SolidColorBrush(Color.FromArgb(110, 255, 255, 255)));
            for (int i = 0; i <= Size; i += 4)
            {
                ctx.DrawLine(every, new Point(i * cs, 0), new Point(i * cs, full));
                ctx.DrawLine(every, new Point(0, i * cs), new Point(full, i * cs));
            }

            if (_marked is (int mc, int mr) && mc >= 0 && mr >= 0 && mc < Size && mr < Size)
            {
                Pen ring = new Pen(new SolidColorBrush(Color.FromRgb(0xFF, 0xD8, 0x40)), 2);
                ctx.DrawRectangle(null, ring, new Rect(mc * cs, mr * cs, cs, cs));
            }
        }
    }

    /// <summary>Colours for permission values, keyed by what the value means in the loaded game.</summary>
    public static class PermissionColors
    {
        public static IBrush Brush(byte value, bool isCollision)
        {
            (float r, float g, float b) = Rgb(value, isCollision);
            return new SolidColorBrush(Color.FromRgb((byte)(r * 255), (byte)(g * 255), (byte)(b * 255)));
        }

        /// <summary>Normalized (0 to 1) RGB for a permission value, shared by the 2D grid and the 3D overlay.</summary>
        public static (float r, float g, float b) Rgb(byte value, bool isCollision)
            => DSPRE.ROMFiles.TilePermissions.Colour(value, isCollision, DSPRE.RomInfo.GameFamily);
    }
}
