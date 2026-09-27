using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace DSPRE.Avalonia.Controls
{
    /// <summary>A small ⓘ that shows its text as a tooltip.</summary>
    public sealed class HelpTip : Border
    {
        public static readonly StyledProperty<string> TextProperty = AvaloniaProperty.Register<HelpTip, string>(nameof(Text));
        public string Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

        public HelpTip()
        {
            Width = 16; Height = 16;
            CornerRadius = new CornerRadius(8);
            BorderThickness = new Thickness(1);
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x90, 0x90, 0x90, 0x90));
            Background = Brushes.Transparent;
            VerticalAlignment = VerticalAlignment.Center;
            Margin = new Thickness(6, 0, 0, 0);
            Cursor = new global::Avalonia.Input.Cursor(global::Avalonia.Input.StandardCursorType.Help);
            Child = new TextBlock
            {
                Text = "i", FontSize = 11, FontWeight = FontWeight.Bold, FontStyle = FontStyle.Italic,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromArgb(0xC0, 0xA0, 0xA0, 0xA0)),
            };
            ToolTip.SetShowDelay(this, 150);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == TextProperty) ToolTip.SetTip(this, new TextBlock { Text = Text, TextWrapping = TextWrapping.Wrap, MaxWidth = 380 });
        }
    }
}
