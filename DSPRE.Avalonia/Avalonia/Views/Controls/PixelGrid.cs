using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace DSPRE.Avalonia.Views.Controls
{
    /// <summary>A line between every pixel of a blown-up drawing, drawn over the top of it.</summary>
    public sealed class PixelGrid : Control
    {
        public static readonly StyledProperty<int> CellProperty =
            AvaloniaProperty.Register<PixelGrid, int>(nameof(Cell), 1);

        /// <summary>How many screen pixels across one pixel of the drawing is.</summary>
        public int Cell
        {
            get => GetValue(CellProperty);
            set => SetValue(CellProperty, value);
        }

        static PixelGrid()
        {
            AffectsRender<PixelGrid>(CellProperty);
        }

        public override void Render(DrawingContext ctx)
        {
            int cell = Cell;
            double w = Bounds.Width, h = Bounds.Height;
            if (cell < 4 || w <= 0 || h <= 0) return;

            // One mid grey line per edge. Grey shows up on light art and dark art alike, and a single
            // line keeps the drawing readable where two would close the pixel up.
            var thin = new Pen(new SolidColorBrush(Color.FromArgb(70, 128, 128, 128)), 1);
            // Every eighth line is stronger. These drawings are stored in eight by eight blocks and the
            // shapes in them line up to that, so it is the ruler people count by.
            var block = new Pen(new SolidColorBrush(Color.FromArgb(150, 128, 128, 128)), 1);

            for (int i = 0, n = 0; i <= w + 0.5; i += cell, n++)
                ctx.DrawLine(n % 8 == 0 ? block : thin, new Point(i + 0.5, 0), new Point(i + 0.5, h));
            for (int i = 0, n = 0; i <= h + 0.5; i += cell, n++)
                ctx.DrawLine(n % 8 == 0 ? block : thin, new Point(0, i + 0.5), new Point(w, i + 0.5));
        }
    }
}
