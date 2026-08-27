using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace DSPRE
{
    public static class RotomTool
    {
        public sealed class Result
        {
            public int ExitCode { get; set; }
            public string Stdout { get; set; }
            public string Stderr { get; set; }
            public bool Success => ExitCode == 0;
        }

        public static string ProjectRoot => RomInfo.workDir?.TrimEnd('\\', '/') ?? "";
        public static string ExePath => DSUtils.ToolPath("rotom");
        public static string LspPath => DSUtils.ToolPath("rotom-lsp");
        public static bool IsAvailable => File.Exists(ExePath);
        public static bool IsLspAvailable => File.Exists(LspPath);

        public static async Task<Result> RunAsync(params string[] args)
        {
            if (!IsAvailable)
                throw new FileNotFoundException("rotom was not found in DSPRE's Tools folder.", ExePath);

            using var process = new Process
            {
                StartInfo =
                {
                    WorkingDirectory = ProjectRoot,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };

            foreach (string arg in args)
                process.StartInfo.ArgumentList.Add(arg);
            if (!DSUtils.ConfigureToolStartInfo(process.StartInfo, "rotom"))
            {
                string error = DSUtils.ToolAvailabilityError("rotom");
                AppLogger.Error(error);
                return new Result { ExitCode = -1, Stderr = error };
            }

            var stdout = new StringBuilder();
            var stderr = new StringBuilder();
            process.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };

            AppLogger.Info("Running rotom: " + process.StartInfo.FileName + " "
                + string.Join(" ", process.StartInfo.ArgumentList));
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            // ConfigureAwait(false): some callers (StarterPokemonData) run this from a synchronous UI-thread
            // call chain via .GetAwaiter().GetResult() — capturing the UI SynchronizationContext here would
            // deadlock (this await's continuation would need the UI thread, which is blocked waiting for it).
            // Existing `await RotomTool.RunAsync(...)` callers are unaffected: this only changes which thread
            // THIS method's own continuation runs on, not where the caller's own await resumes.
            await process.WaitForExitAsync().ConfigureAwait(false);

            return new Result
            {
                ExitCode = process.ExitCode,
                Stdout = stdout.ToString(),
                Stderr = stderr.ToString()
            };
        }

        public static string FormatResult(Result result)
        {
            if (result == null) return "";
            if (!string.IsNullOrWhiteSpace(result.Stdout))
            {
                try
                {
                    using var doc = JsonDocument.Parse(result.Stdout);
                    if (doc.RootElement.TryGetProperty("successes", out var successes)
                        && doc.RootElement.TryGetProperty("failures", out var failures))
                    {
                        int ok = successes.GetArrayLength();
                        int failed = failures.GetArrayLength();
                        return failed == 0 ? $"{ok} file(s) compiled." : $"{ok} compiled, {failed} failed.";
                    }
                }
                catch { }
            }

            string output = !string.IsNullOrWhiteSpace(result.Stderr) ? result.Stderr : result.Stdout;
            return string.IsNullOrWhiteSpace(output) ? $"rotom exited with code {result.ExitCode}." : output.Trim();
        }

        public static string FormatDetails(Result result)
        {
            if (result == null) return "";

            var details = new StringBuilder();
            details.AppendLine(FormatResult(result));
            if (!string.IsNullOrWhiteSpace(result.Stderr))
            {
                details.AppendLine();
                details.AppendLine("stderr:");
                details.AppendLine(result.Stderr.Trim());
            }

            bool stdoutSummarizedAsJson = false;
            if (!string.IsNullOrWhiteSpace(result.Stdout))
            {
                try
                {
                    using var doc = JsonDocument.Parse(result.Stdout);
                    stdoutSummarizedAsJson = doc.RootElement.TryGetProperty("successes", out _)
                        && doc.RootElement.TryGetProperty("failures", out _);
                }
                catch { }
            }

            if (!stdoutSummarizedAsJson && !string.IsNullOrWhiteSpace(result.Stdout))
            {
                details.AppendLine();
                details.AppendLine("stdout:");
                details.AppendLine(result.Stdout.Trim());
            }
            return details.ToString().Trim();
        }
    }
}
