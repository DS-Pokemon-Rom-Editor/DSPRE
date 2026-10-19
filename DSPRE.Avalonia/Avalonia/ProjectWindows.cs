using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace DSPRE.Avalonia
{
    /// <summary>
    /// Each window is its own DSPRE process with its own project. This starts one, reads the project it was started
    /// with, and keeps two windows from opening the same project.
    /// </summary>
    public static class ProjectWindows
    {
        /// <summary>The ROM or folder named on the command line, opened once the main window is up.</summary>
        public static string StartupPath { get; private set; }

        public static void ReadFrom(IEnumerable<string> args)
        {
            StartupPath = args?.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal)
                                                    && (File.Exists(a) || Directory.Exists(a)));
        }

        /// <summary>Starts another DSPRE on <paramref name="path"/>, with the beta editors when this one has them.</summary>
        public static void OpenInNewWindow(string path)
        {
            ProcessStartInfo start = new ProcessStartInfo(Environment.ProcessPath) { UseShellExecute = false };
            start.ArgumentList.Add(path);
            if (BetaEditors.Enabled) start.ArgumentList.Add("--beta");
            Process.Start(start);
        }

        /// <summary>The first window logs to application.log, a second one to application-2.log and so on, so two
        /// windows never append to the same file. A slot whose window has exited is free again.</summary>
        public static string LogFileName()
        {
            Directory.CreateDirectory(LockDir);
            for (int slot = 1; slot < 10; slot++)
            {
                string file = Path.Combine(LockDir, $"log-{slot}.lock");
                if (File.Exists(file) && int.TryParse(File.ReadAllText(file).Trim(), out int owner)
                    && owner != Environment.ProcessId && IsDspre(owner))
                    continue;
                PlainFiles.WriteText(file, Environment.ProcessId.ToString());
                return slot == 1 ? "application.log" : $"application-{slot}.log";
            }
            return $"application-{Environment.ProcessId}.log";
        }

        private static string _held;

        private static string LockDir => Path.Combine(AppPaths.DspreDataPath, "locks");

        // A ROM and the folder it extracts to are the same project.
        private static string ProjectKey(string path)
        {
            string full = Path.GetFullPath(path).TrimEnd('\\', '/');
            if (File.Exists(full))
                full = Path.Combine(Path.GetDirectoryName(full), Path.GetFileNameWithoutExtension(full) + RomInfo.FolderSuffix);
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(full.ToLowerInvariant()));
            return Convert.ToHexString(hash, 0, 12);
        }

        /// <summary>Whether another running DSPRE has <paramref name="path"/> open.</summary>
        public static bool OpenElsewhere(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            string file = Path.Combine(LockDir, ProjectKey(path) + ".lock");
            return File.Exists(file) && int.TryParse(File.ReadAllText(file).Trim(), out int owner)
                   && owner != Environment.ProcessId && IsDspre(owner);
        }

        /// <summary>Claims <paramref name="path"/> for this window, giving up the project it held before. Returns false
        /// when another running DSPRE already has it open.</summary>
        public static bool TryClaim(string path)
        {
            if (string.IsNullOrEmpty(path)) return true;
            if (OpenElsewhere(path)) return false;
            string file = Path.Combine(LockDir, ProjectKey(path) + ".lock");
            Release();
            Directory.CreateDirectory(LockDir);
            PlainFiles.WriteText(file, Environment.ProcessId.ToString());
            _held = file;
            return true;
        }

        public static void Release()
        {
            if (_held == null) return;
            try { if (File.Exists(_held)) File.Delete(_held); }
            catch (IOException) { }   // the next DSPRE sees a dead owner and takes it anyway
            _held = null;
        }

        private static bool IsDspre(int pid)
        {
            try
            {
                using Process p = Process.GetProcessById(pid);
                return !p.HasExited && p.ProcessName.StartsWith("DSPRE", StringComparison.OrdinalIgnoreCase);
            }
            catch (ArgumentException) { return false; }   // no such process
        }
    }
}
