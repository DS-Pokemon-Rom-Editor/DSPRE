using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace DSPRE.Avalonia.Controls
{
    /// <summary>One coloured piece of a <see cref="ProportionBar"/>.</summary>
    public sealed class BarPart
    {
        public double Value { get; init; }
        public string Label { get; init; }
        public Color Colour { get; init; }
    }

    /// <summary>A bar split into pieces sized by their share of the total, labelled where a label fits.</summary>
    public sealed class ProportionBar : Control
    {
        public static readonly StyledProperty<IReadOnlyList<BarPart>> PartsProperty =
            AvaloniaProperty.Register<ProportionBar, IReadOnlyList<BarPart>>(nameof(Parts));

        public IReadOnlyList<BarPart> Parts { get => GetValue(PartsProperty); set => SetValue(PartsProperty, value); }

        static ProportionBar() { AffectsRender<ProportionBar>(PartsProperty); }

        public ProportionBar() { Height = 26; MinWidth = 60; }

        public override void Render(DrawingContext context)
        {
            var parts = Parts;
            var r = new Rect(Bounds.Size);
            context.DrawRectangle(new SolidColorBrush(Color.FromArgb(40, 128, 128, 128)), null, r, 4, 4);
            if (parts == null || parts.Count == 0) return;
            double total = 0;
            foreach (var p in parts) total += System.Math.Max(0, p.Value);
            if (total <= 0) return;

            using (context.PushClip(new RoundedRect(r, 4)))
            {
                double x = 0;
                foreach (var p in parts)
                {
                    double w = r.Width * System.Math.Max(0, p.Value) / total;
                    if (w <= 0) continue;
                    var piece = new Rect(x, 0, w, r.Height);
                    context.FillRectangle(new SolidColorBrush(p.Colour), piece);
                    if (!string.IsNullOrEmpty(p.Label))
                    {
                        var text = new FormattedText(p.Label, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                            Typeface.Default, 11, Brushes.White);
                        if (text.Width + 6 <= w)
                            context.DrawText(text, new Point(x + (w - text.Width) / 2, (r.Height - text.Height) / 2));
                    }
                    x += w;
                }
            }
        }
    }
}
