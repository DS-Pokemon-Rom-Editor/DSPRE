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

        private const double Size = 16;
        public StarRange() { Width = Size * 5 + 8; Height = Size + 4; }

        public override void Render(DrawingContext context)
        {
            for (int i = 0; i < 5; i++)
            {
                var bmp = Icon.Get(i <= Min ? "star_deep" : i <= Base || i <= Max ? "star" : "star_off");
                if (bmp == null) continue;
                using (context.PushOpacity(i > Base && i <= Max ? 0.45 : 1))
                using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = global::Avalonia.Media.Imaging.BitmapInterpolationMode.None }))
                    context.DrawImage(bmp, new Rect(i * (Size + 2), (Height - Size) / 2, Size, Size));
            }
        }
    }
}
