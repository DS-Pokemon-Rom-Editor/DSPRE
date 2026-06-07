using Avalonia;
using System;
using System.IO;
using System.Windows.Forms;
using WinFormsApp = System.Windows.Forms.Application;
using Velopack;

namespace DSPRE
{
    static class Program
    {
        public static string DspreDataPath { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DSPRE");
        public static string DatabasePath = Path.Combine(Program.DspreDataPath, "databases");

        /// <summary>
        /// Application entry point.
        /// Velopack update check runs first (before any UI), then Avalonia takes over
        /// the Win32 message loop. The existing WinForms MainProgram is shown from
        /// AvaloniaApp.OnFrameworkInitializationCompleted — both share the same STA thread.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            if (!Directory.Exists(DspreDataPath))
                Directory.CreateDirectory(DspreDataPath);

            // Velopack must run before any UI is created.
            VelopackApp.Build().Run();

            // WinForms visual styles must be enabled before any WinForms control is created.
            WinFormsApp.EnableVisualStyles();
            WinFormsApp.SetCompatibleTextRenderingDefault(false);

#if DEBUG
            ScreenshotTool.EnableGlobally();
#endif

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        /// <summary>
        /// Avalonia app builder — also used by the AXAML previewer.
        /// </summary>
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<AvaloniaApp>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();

        public static void SetupDatabase()
        {
            // needs to be this verbose (copy instead of move) so this works across drives
            try
            {
                string sourceDbPath = Path.Combine(WinFormsApp.StartupPath, "databases");
                if (Directory.Exists(sourceDbPath) && !SettingsManager.Settings.databasesPulled) {
                    if (!Directory.Exists(DatabasePath)) {
                        Directory.CreateDirectory(DatabasePath);
                    }
                    foreach (string dirPath in Directory.GetDirectories(sourceDbPath, "*", SearchOption.AllDirectories)) {
                        Directory.CreateDirectory(dirPath.Replace(sourceDbPath, DatabasePath));
                    }
                    foreach (string filePath in Directory.GetFiles(sourceDbPath, "*.*", SearchOption.AllDirectories)) {
                        File.Copy(filePath, filePath.Replace(sourceDbPath, DatabasePath), true);
                    }
                    // After successful copy, delete source and update settings
                    Directory.Delete(sourceDbPath, true);
                    SettingsManager.Settings.databasesPulled = true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to copy databases: {ex.Message}",
                              "Database Setup Error",
                              MessageBoxButtons.OK,
                              MessageBoxIcon.Error);
            }
        }
    }
}
