using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;

namespace DSPRE.Avalonia.Views
{
    /// <summary>
    /// One 256×192 battle scene (background, platforms, shadows, the enemy/front + player/back sprites and the
    /// UI chrome). The two sprites are passed in via <see cref="Enemy"/> / <see cref="Player"/>; everything
    /// else (positions, shadows) binds to the inherited <c>BattleDisplayEditorViewModel</c> DataContext, so the
    /// scene can be instantiated once (separate display) or twice (unified Male + Female display).
    /// </summary>
    public partial class BattleSceneControl : UserControl
    {
        public static readonly StyledProperty<Bitmap> EnemyProperty =
            AvaloniaProperty.Register<BattleSceneControl, Bitmap>(nameof(Enemy));
        public static readonly StyledProperty<Bitmap> PlayerProperty =
            AvaloniaProperty.Register<BattleSceneControl, Bitmap>(nameof(Player));
        public static readonly StyledProperty<double> EnemyYProperty =
            AvaloniaProperty.Register<BattleSceneControl, double>(nameof(EnemyY));
        public static readonly StyledProperty<double> PlayerYProperty =
            AvaloniaProperty.Register<BattleSceneControl, double>(nameof(PlayerY));

        public Bitmap Enemy  { get => GetValue(EnemyProperty);  set => SetValue(EnemyProperty, value); }
        public Bitmap Player { get => GetValue(PlayerProperty); set => SetValue(PlayerProperty, value); }
        public double EnemyY  { get => GetValue(EnemyYProperty);  set => SetValue(EnemyYProperty, value); }
        public double PlayerY { get => GetValue(PlayerYProperty); set => SetValue(PlayerYProperty, value); }

        public BattleSceneControl() => InitializeComponent();
    }
}
