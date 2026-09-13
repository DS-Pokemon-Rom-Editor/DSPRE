using System;
using Avalonia;
using Avalonia.Controls;

namespace DSPRE.Avalonia.Views.Controls
{
    /// <summary>
    /// Lays its first child out as the DS top screen, kept to the screen's 4:3 shape, and its second as the
    /// bottom screen under it. With <see cref="Letterbox"/> off the first child simply fills the panel.
    /// </summary>
    public sealed class DsScreenPanel : Panel
    {
        public static readonly StyledProperty<bool> LetterboxProperty =
            AvaloniaProperty.Register<DsScreenPanel, bool>(nameof(Letterbox));

        public static readonly StyledProperty<bool> ShowBottomProperty =
            AvaloniaProperty.Register<DsScreenPanel, bool>(nameof(ShowBottom));

        /// <summary>The gap between the two screens, in DS pixels, the way the hinge sits between them.</summary>
        public const double HingePixels = 8;

        public bool Letterbox { get => GetValue(LetterboxProperty); set => SetValue(LetterboxProperty, value); }
        public bool ShowBottom { get => GetValue(ShowBottomProperty); set => SetValue(ShowBottomProperty, value); }

        static DsScreenPanel()
        {
            AffectsMeasure<DsScreenPanel>(LetterboxProperty, ShowBottomProperty);
            AffectsArrange<DsScreenPanel>(LetterboxProperty, ShowBottomProperty);
        }

        private (Rect top, Rect bottom) Layout(Size size)
        {
            if (!Letterbox) return (new Rect(size), default);

            bool bottom = ShowBottom && Children.Count > 1;
            double unitsHigh = bottom ? 192 * 2 + HingePixels : 192;
            double scale = Math.Max(0, Math.Min(size.Width / 256, size.Height / unitsHigh));
            // Whole-number scales keep the pixel art sharp once there is room for them.
            if (scale >= 1) scale = Math.Floor(scale * 4) / 4;

            double w = 256 * scale, h = 192 * scale;
            double left = (size.Width - w) / 2;
            double top = (size.Height - unitsHigh * scale) / 2;
            var topRect = new Rect(left, top, w, h);
            var bottomRect = bottom ? new Rect(left, top + h + HingePixels * scale, w, h) : default;
            return (topRect, bottomRect);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var (top, bottom) = Layout(availableSize);
            for (int i = 0; i < Children.Count; i++)
                Children[i].Measure(i == 0 ? top.Size : i == 1 ? bottom.Size : default);
            return availableSize;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var (top, bottom) = Layout(finalSize);
            for (int i = 0; i < Children.Count; i++)
                Children[i].Arrange(i == 0 ? top : i == 1 ? bottom : default);
            return finalSize;
        }
    }
}
