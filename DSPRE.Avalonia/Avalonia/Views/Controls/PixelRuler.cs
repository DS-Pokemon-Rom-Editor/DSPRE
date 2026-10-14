using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace DSPRE.Avalonia.Views.Controls
{
    /// <summary>Pixel numbers along one edge of a blown-up drawing, so a painter can tell which row or
    /// column is under the pointer.</summary>
    public sealed class PixelRuler : Control
    {
        public static readonly StyledProperty<int> CellProperty =
            AvaloniaProperty.Register<PixelRuler, int>(nameof(Cell), 1);
        public static readonly StyledProperty<int> CountProperty =
            AvaloniaProperty.Register<PixelRuler, int>(nameof(Count));
        public static readonly StyledProperty<double> StartProperty =
            AvaloniaProperty.Register<PixelRuler, double>(nameof(Start));
        public static readonly StyledProperty<int> HotProperty =
            AvaloniaProperty.Register<PixelRuler, int>(nameof(Hot), -1);
        public static readonly StyledProperty<Orientation> OrientationProperty =
            AvaloniaProperty.Register<PixelRuler, Orientation>(nameof(Orientation));
        public static readonly StyledProperty<IBrush> BackgroundProperty =
            AvaloniaProperty.Register<PixelRuler, IBrush>(nameof(Background));
        public static readonly StyledProperty<IBrush> ForegroundProperty =
            AvaloniaProperty.Register<PixelRuler, IBrush>(nameof(Foreground));
        public static readonly StyledProperty<IBrush> AccentProperty =
            AvaloniaProperty.Register<PixelRuler, IBrush>(nameof(Accent));

        /// <summary>How many screen pixels across one pixel of the drawing is.</summary>
        public int Cell { get => GetValue(CellProperty); set => SetValue(CellProperty, value); }
        /// <summary>How many pixels the drawing has along this edge.</summary>
        public int Count { get => GetValue(CountProperty); set => SetValue(CountProperty, value); }
        /// <summary>Where pixel 0 starts, in this ruler's own coordinates; it moves as the view scrolls.</summary>
        public double Start { get => GetValue(StartProperty); set => SetValue(StartProperty, value); }
        /// <summary>The pixel under the pointer, or -1.</summary>
        public int Hot { get => GetValue(HotProperty); set => SetValue(HotProperty, value); }
        public Orientation Orientation { get => GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }
        public IBrush Background { get => GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }
        public IBrush Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
        public IBrush Accent { get => GetValue(AccentProperty); set => SetValue(AccentProperty, value); }

        private static readonly Typeface Numbers = new Typeface(new FontFamily("Cascadia Mono, Consolas, Menlo, monospace"));
        private const double FontSize = 10;

        static PixelRuler()
        {
            AffectsRender<PixelRuler>(CellProperty, CountProperty, StartProperty, HotProperty, OrientationProperty,
                                      BackgroundProperty, ForegroundProperty, AccentProperty);
            ClipToBoundsProperty.OverrideDefaultValue<PixelRuler>(true);
        }

        public override void Render(DrawingContext ctx)
        {
            bool across = Orientation == Orientation.Horizontal;
            double length = across ? Bounds.Width : Bounds.Height;
            double depth = across ? Bounds.Height : Bounds.Width;
            int cell = Cell, count = Count;
            if (Background != null) ctx.FillRectangle(Background, new Rect(Bounds.Size));
            if (cell <= 0 || count <= 0 || length <= 0) return;

            IBrush ink = Foreground ?? Brushes.Gray;
            IBrush accent = Accent ?? Brushes.DodgerBlue;
            double start = Start;

            Pen edge = new Pen(new SolidColorBrush(Colors.Gray, 0.5), 1);
            if (across) ctx.DrawLine(edge, new Point(0, depth - 0.5), new Point(length, depth - 0.5));
            else ctx.DrawLine(edge, new Point(depth - 0.5, 0), new Point(depth - 0.5, length));

            int hot = Hot;
            if (hot >= 0 && hot < count)
            {
                Rect band = across ? new Rect(start + hot * cell, 0, cell, depth) : new Rect(0, start + hot * cell, depth, cell);
                using (ctx.PushOpacity(0.25)) ctx.FillRectangle(accent, band);
            }

            // Ticks on every pixel while they stay apart, thinning out as the drawing shrinks. Every eighth
            // is longer: the drawings are stored in eight by eight blocks and their shapes line up to them.
            int tickEvery = cell >= 6 ? 1 : cell >= 3 ? 2 : 8;
            Pen major = new Pen(ink, 1);
            Pen minor = new Pen(new SolidColorBrush(Colors.Gray, 0.6), 1);
            for (int i = 0; i <= count; i += tickEvery)
            {
                double p = System.Math.Round(start + i * cell) + 0.5;
                if (p < -1 || p > length + 1) continue;
                bool block = i % 8 == 0;
                double len = depth * (block ? 0.5 : 0.22);
                if (across) ctx.DrawLine(block ? major : minor, new Point(p, depth), new Point(p, depth - len));
                else ctx.DrawLine(block ? major : minor, new Point(depth, p), new Point(depth - len, p));
            }

            // Labels on the smallest step that keeps them readable apart.
            int step = Step(cell, across ? 26 : 18);
            for (int i = 0; i < count; i += step)
            {
                if (i == hot) continue;
                double at = start + i * cell;
                if (at + cell * step < 0 || at > length) continue;
                Label(ctx, i, at, step == 1, ink, across, depth, cell, null);
            }
            // The hovered number shows even between labels, over a patch of background so it reads.
            if (hot >= 0 && hot < count)
                Label(ctx, hot, start + hot * cell, true, accent, across, depth, cell, Background);
        }

        private static int Step(int cell, int minGap)
        {
            foreach (int s in new[] { 1, 2, 4, 8, 16, 32 })
                if (s * cell >= minGap) return s;
            return 64;
        }

        private static void Label(DrawingContext ctx, int number, double at, bool centred, IBrush brush, bool across,
                                  double depth, int cell, IBrush patch)
        {
            FormattedText text = new FormattedText(number.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture,
                                                   FlowDirection.LeftToRight, Numbers, FontSize, brush);
            double x, y;
            if (across)
            {
                x = centred ? at + (cell - text.Width) / 2 : at + 3;
                y = 2;
            }
            else
            {
                x = (depth - text.Width) / 2 - 2;
                y = centred ? at + (cell - text.Height) / 2 : at + 1;
            }
            if (patch != null)
                ctx.FillRectangle(patch, new Rect(x - 2, y, text.Width + 4, text.Height));
            ctx.DrawText(text, new Point(x, y));
        }
    }
}
