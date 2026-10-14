using System;
using System.Collections.Generic;
using System.Globalization;
using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Input;
using global::Avalonia.Media;

namespace DSPRE.Avalonia.Gl
{
    /// <summary>Top-down view of a map's 32x32 tile grid, drawn with each tile's textures.</summary>
    public class TileGridControl : Control
    {
        public const int Size = 32;

        public readonly record struct Placed(int X, int Z, int Tile, int Turn, int Across, int Down, int Layer);

        public readonly record struct Look(IImage Image, int FromX, int FromZ, int Across, int Down);

        private IReadOnlyList<Placed> _placed = Array.Empty<Placed>();
        private bool[] _visible = { true, true, true, true, true, true, true, true, true };
        private int _active;
        private float[,] _heights;
        private bool _showHeights;
        private IReadOnlyList<(int x, int z)> _pending = Array.Empty<(int, int)>();
        private (int x, int z, int across, int down)? _ghost;
        private Func<int, int, Look?> _picture;

        private bool _pressed;
        private (int x, int z)? _last;

        public event EventHandler<(int x, int z, MouseButton button, KeyModifiers keys)> Pressed;
        public event EventHandler<(int x, int z)> Dragged;
        public event EventHandler<(int x, int z)> Released;

        public event EventHandler<(int x, int z)?> Hovered;

        public (int x, int z)? Marked
        {
            get => _marked;
            set { _marked = value; InvalidateVisual(); }
        }
        private (int x, int z)? _marked;

        public bool DimOthers { get; set; }

        private IReadOnlyList<(int index, double x0, double z0, double x1, double z1)> _buildings;

        /// <summary>The building drawn as chosen, by its index in the map.</summary>
        public int SelectedBuilding { get; set; } = -1;

        // A building is taken by its outline, so the squares under it can still be painted.
        public event EventHandler<int> BuildingPressed;
        public event EventHandler<(int index, int dx, int dz)> BuildingDragged;
        public event EventHandler<(int index, int dx, int dz)> BuildingReleased;
        public event EventHandler<int?> BuildingHovered;

        private (int index, Point from, int dx, int dz)? _dragging;

        private int? BuildingEdgeAt(Point p)
        {
            if (_buildings == null) return null;
            const double Band = 5;
            double cs = CellSize;
            int? best = null;
            double smallest = double.MaxValue;
            foreach ((int index, double x0, double z0, double x1, double z1) in _buildings)
            {
                Rect r = new Rect(x0 * cs, z0 * cs, Math.Max(2, (x1 - x0) * cs), Math.Max(2, (z1 - z0) * cs));
                bool near = r.Inflate(Band).Contains(p) && !r.Deflate(Band).Contains(p);
                if (near && r.Width * r.Height < smallest) { smallest = r.Width * r.Height; best = index; }
            }
            return best;
        }

        /// <summary>Building footprints in squares, outlined over the tiles.</summary>
        public void ShowBuildings(IReadOnlyList<(int index, double x0, double z0, double x1, double z1)> buildings)
        {
            _buildings = buildings;
            InvalidateVisual();
        }

        public TileGridControl()
        {
            ClipToBounds = true;
            Focusable = true;
        }

        protected override global::Avalonia.Automation.Peers.AutomationPeer OnCreateAutomationPeer()
            => new global::Avalonia.Automation.Peers.ControlAutomationPeer(this);

        public void Show(IReadOnlyList<Placed> placed, bool[] visible, int active, float[,] heights,
                         bool showHeights, Func<int, int, Look?> picture)
        {
            _placed = placed ?? Array.Empty<Placed>();
            if (visible != null) _visible = visible;
            _active = active;
            _heights = heights;
            _showHeights = showHeights;
            _picture = picture;
            InvalidateVisual();
        }

        public void ShowPending(IReadOnlyList<(int x, int z)> cells)
        {
            _pending = cells ?? Array.Empty<(int, int)>();
            InvalidateVisual();
        }

        private bool[,] _selection;

        public void ShowSelection(bool[,] selection)
        {
            _selection = selection;
            InvalidateVisual();
        }

        public void ShowGhost((int x, int z, int across, int down)? ghost)
        {
            if (_ghost == ghost) return;
            _ghost = ghost;
            InvalidateVisual();
        }

        private double CellSize => Math.Max(1, Math.Min(Bounds.Width, Bounds.Height) / Size) * _zoom;

        // The wheel scrolls (Shift sideways) and Ctrl+wheel zooms about the pointer. A middle or Space drag moves the
        // view; a middle click that does not move still fills, as in PDSMS. Home fits the map again.
        private double _zoom = 1;
        private Vector _pan;
        private bool _spaceHeld;
        private Point? _panFrom;
        public const double MostZoom = 8, LeastZoom = 0.25;
        private const double ScrollStep = 48, ClickSlop = 4, Bar = 8;

        // A middle press is a click until the pointer moves past ClickSlop, then it pans.
        private Point? _middleFrom;
        private bool _middleMoved;
        private KeyModifiers _middleKeys;

        private Point Local(Point p) => p - _pan;

        // The map's edge may come this far into the view, and no further, as in an image editor.
        private const double Gap = 32;

        // The pan limits along one axis, keeping both map edges within Gap of the view.
        private (double low, double high) PanRange(double room)
        {
            double full = CellSize * Size;
            return (Math.Min(room - full, 0) - Gap, Math.Max(room - full, 0) + Gap);
        }

        private void KeepInView()
        {
            double Clamp(double v, double room) { (double low, double high) = PanRange(room); return Math.Clamp(v, low, high); }
            _pan = new Vector(Clamp(_pan.X, Bounds.Width), Clamp(_pan.Y, Bounds.Height));
        }

        // ── Scroll bars, drawn over the grid ────────────────────────────────────────────────
        private (double start, double length) Thumb(double pan, double room)
        {
            (double low, double high) = PanRange(room);
            double length = Math.Max(24, room * Math.Min(1, room / (CellSize * Size + 2 * Gap)));
            double at = Math.Clamp((high - pan) / (high - low), 0, 1) * (room - length);
            return (at, length);
        }

        private Rect AcrossBar => new Rect(0, Bounds.Height - Bar, Bounds.Width - Bar, Bar);
        private Rect DownBar => new Rect(Bounds.Width - Bar, 0, Bar, Bounds.Height - Bar);

        // Which bar is being dragged and where on its thumb it was taken.
        private (bool across, double grip)? _barDrag;

        private bool PressBar(Point p)
        {
            foreach (bool across in new[] { true, false })
            {
                Rect bar = across ? AcrossBar : DownBar;
                if (!bar.Contains(p)) continue;
                double room = across ? bar.Width : bar.Height;
                (double start, double length) = Thumb(across ? _pan.X : _pan.Y, room);
                double at = across ? p.X : p.Y;
                // A press off the thumb jumps it there first.
                double grip = at >= start && at <= start + length ? at - start : length / 2;
                _barDrag = (across, grip);
                DragBar(p);
                return true;
            }
            return false;
        }

        private void DragBar(Point p)
        {
            if (_barDrag is not (bool across, double grip)) return;
            double room = across ? AcrossBar.Width : DownBar.Height;
            (double low, double high) = PanRange(room);
            (double _, double length) = Thumb(0, room);
            double fraction = room - length <= 0 ? 0 : Math.Clamp(((across ? p.X : p.Y) - grip) / (room - length), 0, 1);
            double pan = high - fraction * (high - low);
            _pan = across ? new Vector(pan, _pan.Y) : new Vector(_pan.X, pan);
            KeepInView();
            InvalidateVisual();
        }

        private void DrawBars(DrawingContext ctx)
        {
            SolidColorBrush track = new SolidColorBrush(Color.FromArgb(50, 0, 0, 0));
            SolidColorBrush thumb = new SolidColorBrush(Color.FromArgb(150, 220, 220, 220));
            Rect across = AcrossBar, down = DownBar;
            (double hs, double hl) = Thumb(_pan.X, across.Width);
            (double vs, double vl) = Thumb(_pan.Y, down.Height);
            ctx.FillRectangle(track, across);
            ctx.FillRectangle(track, down);
            ctx.FillRectangle(thumb, new Rect(across.X + hs, across.Y + 1, hl, Bar - 2), 3);
            ctx.FillRectangle(thumb, new Rect(down.X + 1, down.Y + vs, Bar - 2, vl), 3);
        }

        public void ResetView()
        {
            _zoom = 1;
            _pan = default;
            InvalidateVisual();
        }

        protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
        {
            base.OnPointerWheelChanged(e);
            Point at = e.GetPosition(this);
            if ((e.KeyModifiers & KeyModifiers.Control) != 0)
            {
                double was = CellSize;
                _zoom = Math.Clamp(_zoom * Math.Pow(1.25, e.Delta.Y), LeastZoom, MostZoom);
                _pan = (Vector)at - ((Vector)at - _pan) * (CellSize / was);
            }
            else
            {
                // Shift turns an up-down wheel sideways; a trackpad already reports both directions.
                Vector delta = (e.KeyModifiers & KeyModifiers.Shift) != 0 && e.Delta.X == 0 ? new Vector(e.Delta.Y, 0) : e.Delta;
                _pan += delta * ScrollStep;
            }
            KeepInView();
            InvalidateVisual();
            Hovered?.Invoke(this, At(Local(at)));
            e.Handled = true;
        }

        // Space only reaches the grid when it has the keyboard; a typed search keeps it.
        protected override void OnPointerEntered(PointerEventArgs e)
        {
            base.OnPointerEntered(e);
            if (TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is not TextBox) Focus();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                _spaceHeld = true;
                Cursor = new Cursor(StandardCursorType.Hand);
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Home) { ResetView(); e.Handled = true; return; }
            base.OnKeyDown(e);
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                _spaceHeld = false;
                if (_panFrom == null) Cursor = Cursor.Default;
                e.Handled = true;
                return;
            }
            base.OnKeyUp(e);
        }

        protected override void OnSizeChanged(SizeChangedEventArgs e)
        {
            base.OnSizeChanged(e);
            KeepInView();
        }

        private (int x, int z)? At(Point p)
        {
            double cs = CellSize;
            int x = (int)Math.Floor(p.X / cs), z = (int)Math.Floor(p.Y / cs);
            if (x < 0 || z < 0 || x >= Size || z >= Size) return null;
            return (x, z);
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            PointerPoint point = e.GetCurrentPoint(this);
            if (point.Properties.IsLeftButtonPressed && PressBar(point.Position))
            {
                e.Pointer.Capture(this);
                e.Handled = true;
                return;
            }
            if (_spaceHeld && point.Properties.IsLeftButtonPressed)
            {
                _panFrom = point.Position;
                e.Pointer.Capture(this);
                e.Handled = true;
                return;
            }
            if (point.Properties.IsMiddleButtonPressed)
            {
                _middleFrom = point.Position;
                _middleMoved = false;
                _middleKeys = e.KeyModifiers;
                e.Pointer.Capture(this);
                Focus();
                e.Handled = true;
                return;
            }
            Point local = Local(point.Position);
            if (point.Properties.IsLeftButtonPressed && BuildingEdgeAt(local) is int building)
            {
                _dragging = (building, local, 0, 0);
                e.Pointer.Capture(this);
                Focus();
                BuildingPressed?.Invoke(this, building);
                e.Handled = true;
                return;
            }
            if (At(local) is not (int x, int z)) return;

            MouseButton button = point.Properties.IsRightButtonPressed ? MouseButton.Right
                       : point.Properties.IsMiddleButtonPressed ? MouseButton.Middle
                       : MouseButton.Left;
            _pressed = true;
            _last = (x, z);
            e.Pointer.Capture(this);
            Focus();
            Pressed?.Invoke(this, (x, z, button, e.KeyModifiers));
            e.Handled = true;
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            Point screen = e.GetPosition(this);
            if (_barDrag != null) { DragBar(screen); return; }
            if (_middleFrom is Point middle && !_middleMoved
                && Math.Abs(screen.X - middle.X) + Math.Abs(screen.Y - middle.Y) > ClickSlop)
            {
                _middleMoved = true;
                _panFrom = middle;
                Cursor = new Cursor(StandardCursorType.Hand);
            }
            if (_panFrom is Point from0)
            {
                _pan += screen - from0;
                _panFrom = screen;
                KeepInView();
                InvalidateVisual();
                return;
            }
            Point position = Local(screen);
            if (_dragging is (int index, Point from, int wasX, int wasZ))
            {
                int dx = (int)Math.Round((position.X - from.X) / CellSize), dz = (int)Math.Round((position.Y - from.Y) / CellSize);
                if (dx != wasX || dz != wasZ)
                {
                    _dragging = (index, from, dx, dz);
                    BuildingDragged?.Invoke(this, (index, dx, dz));
                }
                return;
            }
            int? edge = _pressed ? null : BuildingEdgeAt(position);
            if (!_pressed) Cursor = _spaceHeld ? new Cursor(StandardCursorType.Hand) : edge != null ? new Cursor(StandardCursorType.SizeAll) : Cursor.Default;
            (int x, int z)? at = At(position);
            if (_pressed && at is (int x, int z) && at != _last)
            {
                _last = at;
                Dragged?.Invoke(this, (x, z));
            }
            Hovered?.Invoke(this, at);
            if (edge != null) BuildingHovered?.Invoke(this, edge);
        }

        protected override void OnPointerExited(PointerEventArgs e)
        {
            base.OnPointerExited(e);
            Hovered?.Invoke(this, null);
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            if (_barDrag != null) { _barDrag = null; e.Pointer.Capture(null); return; }
            if (_middleFrom is Point middle && !_middleMoved)
            {
                // A middle click that never moved is a fill on its square.
                _middleFrom = null;
                e.Pointer.Capture(null);
                if (At(Local(middle)) is (int mx, int mz))
                {
                    Pressed?.Invoke(this, (mx, mz, MouseButton.Middle, _middleKeys));
                    Released?.Invoke(this, (mx, mz));
                }
                return;
            }
            _middleFrom = null;
            if (_panFrom != null)
            {
                _panFrom = null;
                e.Pointer.Capture(null);
                if (!_spaceHeld) Cursor = Cursor.Default;
                return;
            }
            if (_dragging is (int index, _, int dx, int dz))
            {
                _dragging = null;
                e.Pointer.Capture(null);
                BuildingReleased?.Invoke(this, (index, dx, dz));
                return;
            }
            if (!_pressed) return;
            _pressed = false;
            e.Pointer.Capture(null);
            (int x, int z)? at = At(Local(e.GetPosition(this))) ?? _last;
            if (at is (int x, int z)) Released?.Invoke(this, (x, z));
        }

        protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
        {
            base.OnPointerCaptureLost(e);
            _panFrom = null;
            _middleFrom = null;
            _barDrag = null;
            if (_dragging is (int index, _, int dx, int dz))
            {
                _dragging = null;
                BuildingReleased?.Invoke(this, (index, dx, dz));
                return;
            }
            if (!_pressed) return;
            _pressed = false;
            if (_last is (int x, int z)) Released?.Invoke(this, (x, z));
        }

        public override void Render(DrawingContext ctx)
        {
            using (ctx.PushTransform(Matrix.CreateTranslation(_pan.X, _pan.Y)))
                RenderGrid(ctx);
            DrawBars(ctx);
        }

        private void RenderGrid(DrawingContext ctx)
        {
            double cs = CellSize;
            double full = cs * Size;
            ctx.FillRectangle(new SolidColorBrush(Color.FromRgb(20, 20, 24)), new Rect(0, 0, full, full));

            SolidColorBrush dim = new SolidColorBrush(Color.FromArgb(110, 12, 12, 16));
            {
                foreach (Placed p in _placed)
                {
                    if (p.Layer < 0 || p.Layer >= _visible.Length || !_visible[p.Layer]) continue;
                    bool other = p.Layer != _active;
                    Rect rect = new Rect(p.X * cs, p.Z * cs, p.Across * cs, p.Down * cs);
                    if (_picture?.Invoke(p.Tile, p.Turn) is Look look && look.Image != null)
                    {
                        Rect whole = new Rect((p.X + look.FromX) * cs, (p.Z + look.FromZ) * cs, look.Across * cs, look.Down * cs);
                        using (ctx.PushClip(new Rect(0, 0, cs * Size, cs * Size)))
                        using (ctx.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = global::Avalonia.Media.Imaging.BitmapInterpolationMode.None }))
                            ctx.DrawImage(look.Image, whole);
                        if (other && DimOthers) ctx.FillRectangle(dim, rect);
                    }
                    else
                    {
                        ctx.FillRectangle(Colour(p.Tile), rect);
                        if (other && DimOthers) ctx.FillRectangle(dim, rect);
                    }
                }
            }

            Pen faint = new Pen(new SolidColorBrush(Color.FromArgb(34, 255, 255, 255)));
            for (int i = 0; i <= Size; i++)
            {
                ctx.DrawLine(faint, new Point(i * cs, 0), new Point(i * cs, full));
                ctx.DrawLine(faint, new Point(0, i * cs), new Point(full, i * cs));
            }

            Pen edge = new Pen(new SolidColorBrush(Color.FromArgb(170, 255, 255, 255)), 1);
            foreach (Placed p in _placed)
                if (p.Layer == _active && (p.Across > 1 || p.Down > 1))
                    ctx.DrawRectangle(null, edge, new Rect(p.X * cs + 0.5, p.Z * cs + 0.5, p.Across * cs - 1, p.Down * cs - 1));

            if (_buildings != null)
            {
                Pen line = new Pen(new SolidColorBrush(Color.FromArgb(230, 255, 214, 90)), 2) { DashStyle = new DashStyle(new double[] { 3, 2 }, 0) };
                SolidColorBrush wash = new SolidColorBrush(Color.FromArgb(40, 255, 214, 90));
                Pen chosen = new Pen(Brushes.White, 2.5);
                foreach ((int index, double x0, double z0, double x1, double z1) in _buildings)
                {
                    Rect r = new Rect(x0 * cs, z0 * cs, Math.Max(2, (x1 - x0) * cs), Math.Max(2, (z1 - z0) * cs));
                    ctx.FillRectangle(wash, r);
                    ctx.DrawRectangle(null, index == SelectedBuilding ? chosen : line, r);
                }
            }

            if (_showHeights && _heights != null) DrawHeights(ctx, cs);

            Pen every = new Pen(new SolidColorBrush(Color.FromArgb(90, 255, 255, 255)));
            for (int i = 0; i <= Size; i += 4)
            {
                ctx.DrawLine(every, new Point(i * cs, 0), new Point(i * cs, full));
                ctx.DrawLine(every, new Point(0, i * cs), new Point(full, i * cs));
            }

            if (_selection != null)
            {
                SolidColorBrush tint = new SolidColorBrush(Color.FromArgb(60, 0x40, 0xA0, 0xFF));
                Pen line = new Pen(new SolidColorBrush(Color.FromArgb(230, 0x60, 0xB8, 0xFF)), 1.5);
                for (int x = 0; x < Size; x++)
                    for (int z = 0; z < Size; z++)
                    {
                        if (!_selection[x, z]) continue;
                        ctx.FillRectangle(tint, new Rect(x * cs, z * cs, cs, cs));
                        bool Out(int ox, int oz) => ox < 0 || oz < 0 || ox >= Size || oz >= Size || !_selection[ox, oz];
                        if (Out(x - 1, z)) ctx.DrawLine(line, new Point(x * cs, z * cs), new Point(x * cs, (z + 1) * cs));
                        if (Out(x + 1, z)) ctx.DrawLine(line, new Point((x + 1) * cs, z * cs), new Point((x + 1) * cs, (z + 1) * cs));
                        if (Out(x, z - 1)) ctx.DrawLine(line, new Point(x * cs, z * cs), new Point((x + 1) * cs, z * cs));
                        if (Out(x, z + 1)) ctx.DrawLine(line, new Point(x * cs, (z + 1) * cs), new Point((x + 1) * cs, (z + 1) * cs));
                    }
            }

            if (_pending.Count > 0)
            {
                SolidColorBrush fill = new SolidColorBrush(Color.FromArgb(90, 0xFF, 0xD8, 0x40));
                foreach ((int x, int z) in _pending) ctx.FillRectangle(fill, new Rect(x * cs, z * cs, cs, cs));
            }

            if (_ghost is (int gx, int gz, int ga, int gd))
            {
                Pen ring = new Pen(new SolidColorBrush(Color.FromArgb(230, 255, 255, 255)), 1.5,
                                   new DashStyle(new double[] { 3, 2 }, 0));
                ctx.DrawRectangle(null, ring, new Rect(gx * cs + 1, gz * cs + 1, ga * cs - 2, gd * cs - 2));
            }

            if (_marked is (int mx, int mz) && mx >= 0 && mz >= 0 && mx < Size && mz < Size)
            {
                Pen ring = new Pen(new SolidColorBrush(Color.FromRgb(0xFF, 0xD8, 0x40)), 2);
                ctx.DrawRectangle(null, ring, new Rect(mx * cs, mz * cs, cs, cs));
            }
        }

        private void DrawHeights(DrawingContext ctx, double cs)
        {
            Typeface typeface = new Typeface("Consolas");
            for (int z = 0; z < Size; z++)
                for (int x = 0; x < Size; x++)
                {
                    float steps = _heights[z, x];
                    if (float.IsNaN(steps)) continue;
                    int whole = (int)Math.Round(steps);
                    ctx.FillRectangle(new SolidColorBrush(HeightColour(whole, 120)), new Rect(x * cs, z * cs, cs, cs));
                    if (cs < 11) continue;

                    // Painting only sets whole steps, so a fractional height shows rounded.
                    FormattedText text = new FormattedText(whole.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture,
                                                 FlowDirection.LeftToRight, typeface, Math.Max(7, cs * 0.42),
                                                 new SolidColorBrush(Color.FromRgb(26, 26, 30)));
                    ctx.DrawText(text, new Point(x * cs + (cs - text.Width) / 2, z * cs + (cs - text.Height) / 2));
                }
        }

        private static readonly double[] UpHues = { 30, 0, 310, 270, 50, 340 };
        private static readonly double[] DownHues = { 215, 185, 245, 290, 165, 230 };

        /// <summary>Height 0 is pale; each step gets its own hue, none green so it stays visible on grass.</summary>
        public static Color HeightColour(int steps, byte alpha = 255)
        {
            if (steps == 0) return FromHsv(210, 0.12, 0.92, alpha);
            double[] hues = steps > 0 ? UpHues : DownHues;
            int k = Math.Abs(steps) - 1;
            double v = k < hues.Length ? 0.95 : 0.7;
            return FromHsv(hues[k % hues.Length], 0.75, v, alpha);
        }

        public static IBrush Colour(int tile)
        {
            if (tile < 0) return new SolidColorBrush(Color.FromRgb(30, 30, 34));
            double h = (tile * 137.508) % 360.0;
            return new SolidColorBrush(FromHsv(h, 0.45 + (tile % 3) * 0.12, 0.60 + (tile % 5) * 0.07, 255));
        }

        private static Color FromHsv(double h, double s, double v, byte alpha)
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
            return Color.FromArgb(alpha, (byte)((r + m) * 255), (byte)((g + m) * 255), (byte)((b + m) * 255));
        }
    }
}
