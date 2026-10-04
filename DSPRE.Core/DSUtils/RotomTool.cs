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

        public static string ProjectRoot => RomInfo.dspreDir?.TrimEnd('\\', '/') ?? "";
        public static string ExePath => DSUtils.ToolPath("rotom");
        public static string LspPath => DSUtils.ToolPath("rotom-lsp");
        public static bool IsAvailable => File.Exists(ExePath);
        public static bool IsLspAvailable => File.Exists(LspPath);

        // One rotom at a time: two project compiles both rewrite compile state and text archives.
        private static readonly System.Threading.SemaphoreSlim OneAtATime = new System.Threading.SemaphoreSlim(1, 1);

        public static Task<Result> RunAsync(params string[] args) => RunInAsync(ProjectRoot, args);

        /// <summary>
        /// Runs `rotom init`, which reads the game family from a ROM tree in the project root. An hg-engine folder
        /// project keeps its Rotom project in .dspre/, apart from base/, so the files rotom looks for are copied in
        /// for the run and removed afterwards; later commands only need the rotom.toml it writes.
        /// </summary>
        public static async Task<Result> InitProjectAsync()
        {
            var staged = new System.Collections.Generic.List<string>();
            try
            {
                if (RomInfo.HgEngineDsRomMetaDir != null)
                {
                    Stage(RomInfo.arm9Path, Path.Combine(ProjectRoot, "arm9.bin"), staged);
                    Stage(Path.Combine(RomInfo.HgEngineDsRomMetaDir, "config.yaml"), Path.Combine(ProjectRoot, "config.yaml"), staged);
                }
                var result = await RunAsync("init", "--non-interactive").ConfigureAwait(false);
                if (result.Success) DSPRE.ROMFiles.RotomDatabaseUpdate.OverlayHgEngineFlags();
                return result;
            }
            finally
            {
                foreach (string file in staged)
                {
                    try { File.Delete(file); }
                    catch (IOException ex) { AppLogger.Error("RotomTool.InitProjectAsync: " + ex.Message); }
                }
            }
        }

        /// <summary>
        /// hg-engine builds some field scripts from its own source and replaces their members every build. Rotom's
        /// decompiled copies of those are moved out of its source root, so a project compile never rebuilds them
        /// and their binaries stay as hg-engine built them.
        /// </summary>
        public static void SetAsideHgEngineOwnedSources()
        {
            if (!DSPRE.HgEngine.HgEngineProject.IsActive || string.IsNullOrEmpty(ProjectRoot)) return;
            string sources = Path.Combine(ProjectRoot, "expanded", "scripts");
            string aside = Path.Combine(ProjectRoot, ".rotom", "backups", "hg-engine-owned");
            foreach (int id in DSPRE.HgEngine.HgEngineOwnedFiles.OwnedScriptIds())
            {
                foreach (string ext in new[] { ".rotom", ".json" })
                {
                    string source = Path.Combine(sources, id.ToString("D4") + ext);
                    if (!File.Exists(source)) continue;
                    try
                    {
                        Directory.CreateDirectory(aside);
                        File.Move(source, Path.Combine(aside, Path.GetFileName(source)), overwrite: true);
                    }
                    catch (IOException ex) { AppLogger.Error("RotomTool.SetAsideHgEngineOwnedSources: " + ex.Message); }
                }
            }
        }

        private static void Stage(string from, string to, System.Collections.Generic.List<string> staged)
        {
            if (File.Exists(to) || !File.Exists(from)) return;
            File.Copy(from, to);
            staged.Add(to);
        }

        /// <summary>
        /// Compiles the project. Waits for pending source regeneration first, or a just-written binary
        /// would be rebuilt from its old source. rotom prints nothing with --json when the project itself
        /// fails to load, so that case is run again without it to get the message.
        /// </summary>
        public static async Task<Result> CompileProjectAsync()
        {
            await DSPRE.ROMFiles.ScriptSourceSync.WhenIdleAsync().ConfigureAwait(false);
            SetAsideHgEngineOwnedSources();
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
            using var quiet = DSPRE.ROMFiles.ProjectSourceWatcher.Hold();
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
                    RedirectStandardError = true,
                    // rotom writes UTF-8; the default console code page garbles its marks and accented names.
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
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

        /// <summary>"file, line N: message" per failing source, or rotom's own error text when it names none.</summary>
        public static System.Collections.Generic.List<string> FailureLines(Result result)
        {
            var lines = new System.Collections.Generic.List<string>();
            if (result == null || result.Success) return lines;
            try
            {
                using var doc = JsonDocument.Parse(result.Stdout ?? "");
                if (doc.RootElement.TryGetProperty("failures", out var failures) && failures.ValueKind == JsonValueKind.Array)
                {
                    foreach (var failure in failures.EnumerateArray())
                    {
                        string rel = failure.TryGetProperty("path", out var p) ? p.GetString() ?? "" : "";
                        string message = null;
                        int start = -1;
                        if (failure.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object)
                        {
                            if (error.TryGetProperty("details", out var details) && details.ValueKind == JsonValueKind.Object)
                            {
                                if (details.TryGetProperty("message", out var m)) message = m.GetString();
                                if (details.TryGetProperty("span", out var span) && span.ValueKind == JsonValueKind.Object
                                    && span.TryGetProperty("start", out var s) && s.TryGetInt32(out int at)) start = at;
                            }
                            if (message == null && error.TryGetProperty("type", out var type)) message = type.GetString();
                        }
                        string full = Path.GetFullPath(Path.Combine(ProjectRoot, rel));
                        int line = start < 0 ? 0 : LineOfByte(full, start);
                        string shown = Path.GetRelativePath(ProjectRoot, full).Replace('\\', '/');
                        lines.Add(shown + (line > 0 ? ", line " + line : "") + ": " + (message ?? "did not compile"));
                    }
                }
            }
            catch (JsonException) { }

            if (lines.Count == 0)
            {
                string output = !string.IsNullOrWhiteSpace(result.Stderr) ? result.Stderr : result.Stdout;
                lines.Add(string.IsNullOrWhiteSpace(output) ? $"rotom exited with code {result.ExitCode}." : output.Trim());
            }
            return lines;
        }

        // rotom reports UTF-8 byte offsets into the source file.
        private static int LineOfByte(string path, int byteOffset)
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                int line = 1;
                for (int i = 0; i < Math.Min(byteOffset, bytes.Length); i++)
                    if (bytes[i] == (byte)'\n') line++;
                return line;
            }
            catch (IOException) { return 0; }
            catch (UnauthorizedAccessException) { return 0; }
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
