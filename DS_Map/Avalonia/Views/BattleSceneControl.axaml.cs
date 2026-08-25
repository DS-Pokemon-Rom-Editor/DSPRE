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

        // Static battle-scene chrome (background/platforms/shadows/health bars), loaded via code-behind
        // instead of a literal XAML "avares://" Image.Source: the declarative form throws and crashes
        // the whole editor if an asset can't be resolved, so this try/catch keeps a missing file to one
        // blank image instead.
        private static Bitmap LoadAsset(string name)
        {
            try { return new Bitmap(global::Avalonia.Platform.AssetLoader.Open(new System.Uri($"avares://DSPRE.Avalonia/Avalonia/Assets/Battle/{name}"))); }
            catch { return null; }
        }

        public static Bitmap BackgroundAsset       { get; } = LoadAsset("background.png");
        public static Bitmap PlatformOpponentAsset { get; } = LoadAsset("platform_opponent.png");
        public static Bitmap PlatformYouAsset      { get; } = LoadAsset("platform_you.png");
        public static Bitmap ShadowSmallAsset      { get; } = LoadAsset("shadow_small.png");
        public static Bitmap ShadowMediumAsset     { get; } = LoadAsset("shadow_medium.png");
        public static Bitmap ShadowLargeAsset      { get; } = LoadAsset("shadow_large.png");
        public static Bitmap HealthOpponentAsset   { get; } = LoadAsset("health_opponent.png");
        public static Bitmap HealthYouAsset        { get; } = LoadAsset("health_you.png");
        public static Bitmap TextBarAsset          { get; } = LoadAsset("text_bar.png");

        public BattleSceneControl() => InitializeComponent();
    }
}
