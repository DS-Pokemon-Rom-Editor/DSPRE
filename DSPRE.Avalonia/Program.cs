using System;
using System.Globalization;
using Avalonia;

namespace DSPRE.AvaloniaShell
{
    /// <summary>
    /// Entry point of the cross-platform, pure-Avalonia DSPRE. No WinForms host hook is installed,
    /// so <see cref="DSPRE.AvaloniaApp"/> always boots the Avalonia main window.
    /// </summary>
    internal static class Program
    {
        [STAThread]   // required on Windows; harmless elsewhere
        public static void Main(string[] args)
        {
            // Velopack hooks (install/update/uninstall) must run before any UI is created.
            // Cross-platform: Windows installer packages and Linux AppImages alike.
            Velopack.VelopackApp.Build().Run();

            DSPRE.SettingsManager.Load();
            ApplyUiScaleOverride();
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        private static void ApplyUiScaleOverride()
        {
            double scale = DSPRE.SettingsManager.Settings?.uiScale ?? 0;
            if (scale >= 0.5 && scale <= 8 &&
                string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AVALONIA_GLOBAL_SCALE_FACTOR")))
            {
                Environment.SetEnvironmentVariable("AVALONIA_GLOBAL_SCALE_FACTOR",
                    scale.ToString(CultureInfo.InvariantCulture));
            }
        }

        /// <summary>Avalonia app builder — also used by the AXAML previewer.</summary>
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<DSPRE.AvaloniaApp>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
    }
}
