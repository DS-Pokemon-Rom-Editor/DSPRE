using Avalonia;
using Avalonia.Controls;

namespace DSPRE.Avalonia.Controls
{
    /// <summary>Lets a form fill the width it's given up to <see cref="Cap"/>, kept to the left, so fields
    /// stretch in a narrow window without spreading across a wide one.</summary>
    public sealed class CappedWidth : Decorator
    {
        public static readonly StyledProperty<double> CapProperty = AvaloniaProperty.Register<CappedWidth, double>(nameof(Cap), 800);
        public double Cap { get => GetValue(CapProperty); set => SetValue(CapProperty, value); }

        static CappedWidth() { AffectsMeasure<CappedWidth>(CapProperty); }

        protected override Size MeasureOverride(Size availableSize)
        {
            if (Child == null) return default;
            Child.Measure(availableSize.WithWidth(System.Math.Min(availableSize.Width, Cap)));
            return Child.DesiredSize;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            Child?.Arrange(new Rect(0, 0, System.Math.Min(finalSize.Width, Cap), finalSize.Height));
            return finalSize;
        }
    }
}
