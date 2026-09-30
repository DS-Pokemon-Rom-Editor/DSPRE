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
            /// <summary>Scripts a whole-project rebuild changed although nobody edited them; their previous binaries were kept.</summary>
            public System.Collections.Generic.List<int> KeptBinaries { get; set; } = new System.Collections.Generic.List<int>();
        }

        public static string ProjectRoot => RomInfo.workDir?.TrimEnd('\\', '/') ?? "";
        public static string ExePath => DSUtils.ToolPath("rotom");
        public static string LspPath => DSUtils.ToolPath("rotom-lsp");
        public static bool IsAvailable => File.Exists(ExePath);
        public static bool IsLspAvailable => File.Exists(LspPath);

        // One rotom at a time: two project compiles both rewrite compile state and text archives.
        private static readonly System.Threading.SemaphoreSlim OneAtATime = new System.Threading.SemaphoreSlim(1, 1);

        public static Task<Result> RunAsync(params string[] args) => RunInAsync(ProjectRoot, args);

        /// <summary>
        /// Compiles the project. Waits for pending source regeneration first, or a just-written binary
        /// would be rebuilt from its old source. rotom prints nothing with --json when the project itself
        /// fails to load, so that case is run again without it to get the message.
        /// </summary>
        public static async Task<Result> CompileProjectAsync()
        {
            await DSPRE.ROMFiles.ScriptSourceSync.WhenIdleAsync().ConfigureAwait(false);
            var archivesBefore = TextArchiveTimes();
            var guard = DSPRE.ROMFiles.RotomRebuildGuard.Before(ProjectRoot);
            var result = await RunAsync("compile", "--json").ConfigureAwait(false);
            if (guard != null) result.KeptBinaries = guard.After();
            // A compile appends inline messages to the text archives, which open text editors must reload.
            foreach (var (id, time) in TextArchiveTimes())
                if (!archivesBefore.TryGetValue(id, out var before) || before != time)
                    DSPRE.ROMFiles.TextArchive.RaiseSaved(Compiler, id);
            if (!result.Success && string.IsNullOrWhiteSpace(result.Stdout))
            {
                var plain = await RunAsync("compile").ConfigureAwait(false);
                string message = !string.IsNullOrWhiteSpace(plain.Stderr) ? plain.Stderr : plain.Stdout;
                if (!string.IsNullOrWhiteSpace(message)) result.Stderr = message;
            }
            return result;
        }

        /// <summary>The sender of text archive saves a rotom compile made.</summary>
        public static readonly object Compiler = new object();

        private static System.Collections.Generic.Dictionary<int, DateTime> TextArchiveTimes()
        {
            var times = new System.Collections.Generic.Dictionary<int, DateTime>();
            string dir = Path.Combine(ProjectRoot, "expanded", "textArchives");
            if (!Directory.Exists(dir)) return times;
            foreach (string file in Directory.GetFiles(dir, "*.json"))
                if (int.TryParse(Path.GetFileNameWithoutExtension(file), out int id))
                    times[id] = File.GetLastWriteTimeUtc(file);
            return times;
        }

        /// <summary>The compile error rotom reported for this source file, or null when it compiled.</summary>
        public static string FailureFor(Result result, string sourcePath)
        {
            if (result == null || string.IsNullOrWhiteSpace(result.Stdout)) return result?.Success == false ? FormatResult(result) : null;
            try
            {
                using var doc = JsonDocument.Parse(result.Stdout);
                if (!doc.RootElement.TryGetProperty("failures", out var failures)) return null;
                string wanted = Path.GetFullPath(sourcePath);
                foreach (var failure in failures.EnumerateArray())
                {
                    if (!failure.TryGetProperty("path", out var path)) continue;
                    string full = Path.GetFullPath(Path.Combine(ProjectRoot, path.GetString() ?? ""));
                    if (!string.Equals(full, wanted, StringComparison.OrdinalIgnoreCase)) continue;
                    return failure.TryGetProperty("error", out var error) && error.TryGetProperty("details", out var details)
                        && details.TryGetProperty("message", out var message) ? message.GetString() : error.ToString();
                }
                return null;
            }
            catch (JsonException) { return result.Success ? null : FormatResult(result); }
        }

        /// <summary>Runs rotom with <paramref name="workingDirectory"/> as its project root.</summary>
        public static async Task<Result> RunInAsync(string workingDirectory, params string[] args)
        {
            if (!IsAvailable)
                throw new FileNotFoundException("rotom was not found in DSPRE's Tools folder.", ExePath);

            await OneAtATime.WaitAsync().ConfigureAwait(false);
            try { return await RunLockedAsync(workingDirectory, args).ConfigureAwait(false); }
            finally { OneAtATime.Release(); }
        }

        private static async Task<Result> RunLockedAsync(string workingDirectory, string[] args)
        {

            using var process = new Process
            {
                StartInfo =
                {
                    WorkingDirectory = workingDirectory,
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
            // call chain via .GetAwaiter().GetResult(), capturing the UI SynchronizationContext here would
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
