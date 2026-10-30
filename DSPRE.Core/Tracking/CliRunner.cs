using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace DSPRE.Tracking
{
    /// <summary>Runs git or gh and collects what they print.</summary>
    public static class CliRunner
    {
        public sealed class Result
        {
            public int ExitCode { get; init; }
            public string Stdout { get; init; } = "";
            public string Stderr { get; init; } = "";
            public bool Success => ExitCode == 0;
            public string Message => (string.IsNullOrWhiteSpace(Stderr) ? Stdout : Stderr).Trim();
        }

        /// <summary>The command's full path: the one set in settings, else the first on PATH; null when there is none.</summary>
        public static string Find(string configured, string name)
        {
            if (!string.IsNullOrWhiteSpace(configured)) return File.Exists(configured) ? configured : null;
            string[] names = OperatingSystem.IsWindows() ? new[] { name + ".exe", name + ".cmd" } : new[] { name };
            foreach (string dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(dir)) continue;
                foreach (string n in names)
                {
                    string path = Path.Combine(dir.Trim('"'), n);
                    if (File.Exists(path)) return path;
                }
            }
            return null;
        }

        public static async Task<Result> RunAsync(string exe, string workingDirectory, IEnumerable<string> args,
            Action<string> onLine = null, Func<StreamWriter, string, Task> onOutput = null)
        {
            using Process process = new Process
            {
                StartInfo =
                {
                    FileName = exe,
                    WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                }
            };
            foreach (string arg in args) process.StartInfo.ArgumentList.Add(arg);
            // Never wait on a terminal prompt nobody can see.
            process.StartInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
            process.StartInfo.Environment["GH_PROMPT_DISABLED"] = "1";
            process.StartInfo.Environment["NO_COLOR"] = "1";

            StringBuilder stdout = new StringBuilder(), stderr = new StringBuilder();
            void Seen(StringBuilder into, string line)
            {
                if (line == null) return;
                lock (into) into.AppendLine(line);
                onLine?.Invoke(line);
                if (onOutput != null) _ = onOutput(process.StandardInput, line);
            }
            process.OutputDataReceived += (_, e) => Seen(stdout, e.Data);
            process.ErrorDataReceived += (_, e) => Seen(stderr, e.Data);

            AppLogger.Info("Running " + Path.GetFileName(exe) + " " + string.Join(" ", process.StartInfo.ArgumentList));
            try { process.Start(); }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception || ex is InvalidOperationException)
            {
                return new Result { ExitCode = -1, Stderr = $"{Path.GetFileName(exe)} couldn't be started: {ex.Message}" };
            }
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync().ConfigureAwait(false);
            return new Result { ExitCode = process.ExitCode, Stdout = stdout.ToString(), Stderr = stderr.ToString() };
        }
    }
}
