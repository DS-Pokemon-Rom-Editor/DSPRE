using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
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
        /// Punto di ingresso principale dell'applicazione.
        /// </summary>
        [STAThread]
        static void Main()
        {
            if (!Directory.Exists(DspreDataPath))
                Directory.CreateDirectory(DspreDataPath);

            VelopackApp.Build().Run();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            ShowDonationNoticeIfPokeHubIsRunning();

#if DEBUG
            // Enable Ctrl+Click screenshot capture on all forms
            ScreenshotTool.EnableGlobally();
#endif

            MainProgram mainProgram = new MainProgram();
            CrashReporter.Initialize(mainProgram);
            Application.Run(mainProgram);
        }

        static void ShowDonationNoticeIfPokeHubIsRunning()
        {
            // PokHub stays open when it starts DSPRE, so a process check covers both cases.
            string[] names = { "PokeHub", "PokeHub.App", "PokeHub.Mcp" };
            bool running = false;
            try
            {
                foreach (string name in names)
                {
                    Process[] found = Process.GetProcessesByName(name);
                    try
                    {
                        if (found.Length > 0)
                        {
                            running = true;
                            break;
                        }
                    }
                    finally
                    {
                        foreach (Process process in found)
                            process.Dispose();
                    }
                }
            }
            catch
            {
                return;
            }

            if (!running)
                return;

            MessageBox.Show(
                "We do not ask for donations, and we do not support them."
                + Environment.NewLine + Environment.NewLine
                + "Do not donate to any program that launches DSPRE or that takes code from DSPRE. "
                + "Any such program that asks for donations does so against our expressed wishes.",
                "DSPRE",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        public static void SetupDatabase()
        {   
            // needs to be this verbose (copy instead of move) so this works accross drives
            try
            { 
                string sourceDbPath = Path.Combine(Application.StartupPath, "databases");
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
