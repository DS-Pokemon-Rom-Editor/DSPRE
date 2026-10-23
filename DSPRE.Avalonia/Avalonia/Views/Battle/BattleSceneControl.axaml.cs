using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;

namespace DSPRE.Avalonia.Views.Battle
{
    /// <summary>
    /// One battle scene. The two sprites are passed in; the rest binds to the BattleDisplayEditorViewModel DataContext,
    /// so one or two scenes can share it.
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

        // Loaded in code: a XAML image source that can't resolve crashes the editor, so a missing file becomes a blank
        // image.
        private static Bitmap LoadAsset(string name)
        {
            try { return new Bitmap(global::Avalonia.Platform.AssetLoader.Open(new System.Uri($"avares://DSPRE.Avalonia/Avalonia/Assets/Battle/{name}"))); }
            catch (System.Exception ex) { AppLogger.WarnOnce("BattleSceneControl.LoadAsset", ex); return null; }
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
