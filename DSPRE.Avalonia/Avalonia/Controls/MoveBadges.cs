using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using DSPRE.Avalonia.Data;

namespace DSPRE.Avalonia.Controls
{
    /// <summary>A TM or HM item id to the move it teaches, 0 for other items.</summary>
    public sealed class TaughtMoveConverter : global::Avalonia.Data.Converters.IValueConverter
    {
        public static readonly TaughtMoveConverter Instance = new();
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            => value is int item ? MoveInfo.TaughtByItem(item) : 0;
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => throw new NotSupportedException();
    }

    /// <summary>A move's type and category icons, and with <see cref="ShowStats"/> its power, accuracy and PP.
    /// <see cref="ShowIcons"/> off leaves only the numbers, for a list that puts them after the name.</summary>
    public sealed class MoveBadges : StackPanel
    {
        public static readonly StyledProperty<int> MoveProperty = AvaloniaProperty.Register<MoveBadges, int>(nameof(Move));
        public static readonly StyledProperty<bool> ShowStatsProperty = AvaloniaProperty.Register<MoveBadges, bool>(nameof(ShowStats));
        public static readonly StyledProperty<bool> ShowIconsProperty = AvaloniaProperty.Register<MoveBadges, bool>(nameof(ShowIcons), true);

        public int Move { get => GetValue(MoveProperty); set => SetValue(MoveProperty, value); }
        public bool ShowStats { get => GetValue(ShowStatsProperty); set => SetValue(ShowStatsProperty, value); }
        public bool ShowIcons { get => GetValue(ShowIconsProperty); set => SetValue(ShowIconsProperty, value); }

        private readonly Image _type = Icon16(32);
        private readonly Image _category = Icon16(32);
        private readonly Grid _stats = new Grid { ColumnDefinitions = new ColumnDefinitions("40,44,28"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0) };
        private readonly TextBlock _power = Number(), _accuracy = Number(), _pp = Number();

        public MoveBadges()
        {
            Orientation = Orientation.Horizontal;
            Spacing = 2;
            VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(_accuracy, 1); Grid.SetColumn(_pp, 2);
            _stats.Children.Add(_power); _stats.Children.Add(_accuracy); _stats.Children.Add(_pp);
            Children.Add(_type); Children.Add(_category); Children.Add(_stats);
            _stats.IsVisible = false;
            EventHandler changed = (_, _) => Show();
            AttachedToVisualTree += (_, _) => { MoveInfo.Changed += changed; Show(); };
            DetachedFromVisualTree += (_, _) => MoveInfo.Changed -= changed;
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == MoveProperty || change.Property == ShowStatsProperty || change.Property == ShowIconsProperty) Show();
        }

        private void Show()
        {
            MoveInfo.Facts? facts = MoveInfo.Of(Move);
            _type.Source = facts == null ? null : TypeIcons.For(facts.Value.Type);
            _category.Source = facts == null ? null : TypeIcons.Category(facts.Value.Category);
            _type.IsVisible = _category.IsVisible = ShowIcons && facts != null;
            _stats.IsVisible = ShowStats;
            _stats.Margin = new Thickness(ShowIcons ? 6 : 0, 0, 0, 0);
            if (!ShowStats) return;
            // Power 0 or 1 means no fixed power and accuracy 0 never misses; the game shows a dash for both.
            _power.Text = facts == null || facts.Value.Power <= 1 ? "-" : facts.Value.Power.ToString();
            _accuracy.Text = facts == null || facts.Value.Accuracy == 0 ? "-" : facts.Value.Accuracy + "%";
            _pp.Text = facts == null ? "" : facts.Value.Pp.ToString();
        }

        private static Image Icon16(double width)
        {
            Image image = new Image { Width = width, Height = 16, Stretch = Stretch.Uniform, VerticalAlignment = VerticalAlignment.Center };
            RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.None);
            return image;
        }

        private static TextBlock Number() => new TextBlock { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, FontSize = 12 };
    }
}
