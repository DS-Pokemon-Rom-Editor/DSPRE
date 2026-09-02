using Avalonia;
using System;
using System.IO;
using System.Windows.Forms;
using WinFormsApp = System.Windows.Forms.Application;
using Velopack;
using DSPRE.Avalonia.Data;

namespace DSPRE
{
    static class Program
    {
        // Canonical definitions live in the core AppPaths class; these forward for legacy call sites.
        public static string DspreDataPath => AppPaths.DspreDataPath;
        public static string DatabasePath => AppPaths.DatabasePath;

        /// <summary>
        /// Application entry point.
        /// Velopack update check runs first (before any UI), then Avalonia takes over
        /// the Win32 message loop. The existing WinForms MainProgram is shown from
        /// AvaloniaApp.OnFrameworkInitializationCompleted, both share the same STA thread.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            // Read before any window can be opened, since the gate is asked as each one is shown.
            BetaEditors.ReadFrom(args);

            if (!Directory.Exists(DspreDataPath))
                Directory.CreateDirectory(DspreDataPath);

            // Velopack must run before any UI is created.
            VelopackApp.Build().Run();

            // WinForms visual styles must be enabled before any WinForms control is created.
            WinFormsApp.EnableVisualStyles();
            WinFormsApp.SetCompatibleTextRenderingDefault(false);

            // This exe hosts the legacy WinForms shell (unless DSPRE_AVALONIA_SHELL=1 forces the
            // pure-Avalonia one); the cross-platform DSPRE.Avalonia exe never installs these hooks.
            WinFormsShellHost.InstallHooks();

            // Real sound-effect playback. NAudioOutput itself no-ops on non-Windows, so this is safe to
            // wire unconditionally (both shells do the same).
            AudioOutput.Current = new NAudioOutput();

#if DEBUG
            ScreenshotTool.EnableGlobally();
#endif

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        /// <summary>
        /// Avalonia app builder, also used by the AXAML previewer.
        /// </summary>
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<AvaloniaApp>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();

        public static void SetupDatabase() => DatabaseSetup.CopyBundledDatabases();
    }
}
