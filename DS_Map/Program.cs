using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
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
            ShowNoticeIfLaunchedByExternalTool();

#if DEBUG
            // Enable Ctrl+Click screenshot capture on all forms
            ScreenshotTool.EnableGlobally();
#endif

            MainProgram mainProgram = new MainProgram();
            CrashReporter.Initialize(mainProgram);
            Application.Run(mainProgram);
        }

        static void ShowNoticeIfLaunchedByExternalTool()
        {
            string parent;
            try
            {
                parent = ParentProcessName();
            }
            catch
            {
                return;
            }

            if (parent == null)
                return;

            bool launchedByHub = parent.Equals("PokeHub", StringComparison.OrdinalIgnoreCase)
                || parent.Equals("PokeHub.App", StringComparison.OrdinalIgnoreCase)
                || parent.Equals("PokeHub.Mcp", StringComparison.OrdinalIgnoreCase);
            if (!launchedByHub)
                return;

            MessageBox.Show(
                "This version of DSPRE has been launched by an external tool."
                + Environment.NewLine + Environment.NewLine
                + "Official builds: https://github.com/DS-Pokemon-Rom-Editor/DSPRE",
                "DSPRE",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        static string ParentProcessName()
        {
            const uint snapProcess = 2;
            IntPtr snapshot = CreateToolhelp32Snapshot(snapProcess, 0);
            if (snapshot == new IntPtr(-1))
                return null;

            try
            {
                uint self = (uint)Process.GetCurrentProcess().Id;
                var entry = new PROCESSENTRY32();
                entry.dwSize = (uint)Marshal.SizeOf(typeof(PROCESSENTRY32));
                if (!Process32First(snapshot, ref entry))
                    return null;

                uint parentId = 0;
                bool foundSelf = false;
                do
                {
                    if (entry.th32ProcessID == self)
                    {
                        parentId = entry.th32ParentProcessID;
                        foundSelf = true;
                        break;
                    }
                }
                while (Process32Next(snapshot, ref entry));

                if (!foundSelf)
                    return null;

                entry = new PROCESSENTRY32();
                entry.dwSize = (uint)Marshal.SizeOf(typeof(PROCESSENTRY32));
                if (!Process32First(snapshot, ref entry))
                    return null;

                do
                {
                    if (entry.th32ProcessID == parentId)
                        return Path.GetFileNameWithoutExtension(entry.szExeFile);
                }
                while (Process32Next(snapshot, ref entry));

                return null;
            }
            finally
            {
                CloseHandle(snapshot);
            }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        struct PROCESSENTRY32
        {
            public uint dwSize;
            public uint cntUsage;
            public uint th32ProcessID;
            public IntPtr th32DefaultHeapID;
            public uint th32ModuleID;
            public uint cntThreads;
            public uint th32ParentProcessID;
            public int pcPriClassBase;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szExeFile;
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern bool Process32First(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern bool Process32Next(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CloseHandle(IntPtr hObject);

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
