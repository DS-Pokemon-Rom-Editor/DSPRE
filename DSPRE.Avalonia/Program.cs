using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia;
using DSPRE;
using DSPRE.Avalonia.Data;

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
            PreferWslGpu();
            BetaEditors.ReadFrom(args);
            DSPRE.HgEngine.HgEngineDev.ReadFrom(args);

            // Velopack hooks (install/update/uninstall) must run before any UI is created.
            // Cross-platform: Windows installer packages and Linux AppImages alike.
            Velopack.VelopackApp.Build().Run();

            DSPRE.SettingsManager.Load();
            ApplyUiScaleOverride();

            // Real sound-effect playback. NAudioOutput itself no-ops on non-Windows, so this is safe to
            // wire unconditionally (matches the WinForms-hybrid shell's Program.cs).
            AudioOutput.Current = new NAudioOutput();

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        // WSL's Mesa defaults to llvmpipe, which Avalonia refuses, leaving the 3D views without OpenGL; d3d12 reaches
        // the GPU. Mesa reads the native environment, which Environment.SetEnvironmentVariable leaves alone on Linux.
        private static void PreferWslGpu()
        {
            if (!OperatingSystem.IsLinux() || !DSUtils.IsWsl() || !File.Exists("/dev/dxg")) return;
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GALLIUM_DRIVER"))) return;
            setenv("GALLIUM_DRIVER", "d3d12", 0);
        }

        [DllImport("libc")]
        private static extern int setenv(string name, string value, int overwrite);

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

        /// <summary>Avalonia app builder, also used by the AXAML previewer.</summary>
        public static AppBuilder BuildAvaloniaApp()
        {
            AppBuilder builder = AppBuilder.Configure<DSPRE.AvaloniaApp>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
            global::Avalonia.Logging.Logger.Sink = new DSPRE.Avalonia.AvaloniaLogSink(global::Avalonia.Logging.Logger.Sink);
            return builder;
        }
    }
}
