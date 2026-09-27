using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace DSPRE.Avalonia.Controls
{
    /// <summary>Five stars showing a Pokéathlon stat: deep gold up to the minimum, gold up to the base, faint gold up
    /// to the maximum, grey beyond. Values are 0-4 for one to five stars.</summary>
    public sealed class StarRange : Control
    {
        public static readonly StyledProperty<int> MinProperty = AvaloniaProperty.Register<StarRange, int>(nameof(Min));
        public static readonly StyledProperty<int> BaseProperty = AvaloniaProperty.Register<StarRange, int>(nameof(Base));
        public static readonly StyledProperty<int> MaxProperty = AvaloniaProperty.Register<StarRange, int>(nameof(Max));

        public int Min { get => GetValue(MinProperty); set => SetValue(MinProperty, value); }
        public int Base { get => GetValue(BaseProperty); set => SetValue(BaseProperty, value); }
        public int Max { get => GetValue(MaxProperty); set => SetValue(MaxProperty, value); }

        static StarRange() { AffectsRender<StarRange>(MinProperty, BaseProperty, MaxProperty); }

        private const double Size = 20;
        public StarRange() { Width = Size * 5 + 8; Height = Size + 4; }

        private static readonly IBrush Deep = new SolidColorBrush(Color.FromRgb(0xD9, 0x8E, 0x04));
        private static readonly IBrush Gold = new SolidColorBrush(Color.FromRgb(0xF7, 0xC5, 0x3B));
        private static readonly IBrush Faint = new SolidColorBrush(Color.FromArgb(0x70, 0xF7, 0xC5, 0x3B));
        private static readonly IBrush Off = new SolidColorBrush(Color.FromArgb(0x50, 0x90, 0x90, 0x90));

        public override void Render(DrawingContext context)
        {
            for (int i = 0; i < 5; i++)
            {
                IBrush b = i <= Min ? Deep : i <= Base ? Gold : i <= Max ? Faint : Off;
                var star = new FormattedText("★", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, Size, b);
                context.DrawText(star, new Point(i * (Size + 2), (Height - star.Height) / 2));
            }
        }
    }
}
