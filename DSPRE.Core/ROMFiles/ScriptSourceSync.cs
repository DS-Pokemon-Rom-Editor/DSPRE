using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// Keeps a Rotom project's script sources in step with binaries DSPRE writes directly.
    /// The Script Editor compiles the whole project from <c>expanded/scripts</c>, so a source left
    /// describing the old binary would undo the write on the next compile. Every writer of a script
    /// or level-script binary reports it here, and only those files' sources are regenerated.
    /// </summary>
    public static class ScriptSourceSync
    {
        private static readonly object Gate = new object();
        private static readonly HashSet<int> Pending = new HashSet<int>();
        private static readonly SemaphoreSlim OneAtATime = new SemaphoreSlim(1, 1);
        private static Task _queued = Task.CompletedTask;
        private static bool _scheduled;

        /// <summary>Raised after sources were regenerated, with the files whose sources changed.</summary>
        public static event Action<IReadOnlyCollection<int>> SourcesRefreshed;

        /// <summary>
        /// Records that script file <paramref name="fileId"/>'s binary was written outside the Script
        /// Editor. Its source is regenerated shortly after, in the background, so a save that writes
        /// several files costs one pass. The returned task completes when that pass has.
        /// </summary>
        public static Task BinaryWritten(int fileId)
        {
            if (!RomInfo.hasRotomProject || !RotomTool.IsAvailable || fileId < 0) return Task.CompletedTask;

            lock (Gate)
            {
                Pending.Add(fileId);
                if (_scheduled) return _queued;
                _scheduled = true;
                _queued = Task.Run(DrainAsync);
                return _queued;
            }
        }

        /// <summary>
        /// Completes once every reported write has its source regenerated. A project compile waits for
        /// this first, or it would rebuild a just-written binary from its old source.
        /// </summary>
        public static Task WhenIdleAsync()
        {
            lock (Gate) return _queued;
        }

        private static async Task DrainAsync()
        {
            await Task.Delay(250).ConfigureAwait(false);
            int[] ids;
            lock (Gate)
            {
                ids = Pending.ToArray();
                Pending.Clear();
                _scheduled = false;
            }
            await RefreshAsync(ids).ConfigureAwait(false);
        }

        /// <summary>
        /// Regenerates the sources of these files, and only these. Best-effort: a failure only affects
        /// the sources, the binaries are already written.
        /// </summary>
        public static async Task RefreshAsync(IEnumerable<int> fileIds)
        {
            if (!RomInfo.hasRotomProject || !RotomTool.IsAvailable || fileIds == null) return;
            List<int> ids = fileIds.Distinct().ToList();
            if (ids.Count == 0) return;

            IReadOnlyList<int> refreshed = Array.Empty<int>();
            await OneAtATime.WaitAsync().ConfigureAwait(false);
            try
            {
                string root = RotomTool.ProjectRoot;
                refreshed = await RegenerateAsync(ids, root, Path.Combine(root, "expanded", "scripts")).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is InvalidOperationException)
            {
                AppLogger.Warn("Could not refresh the Rotom script sources of " + string.Join(", ", ids.Select(i => i.ToString("D4"))) + ": " + ex.Message);
            }
            finally
            {
                OneAtATime.Release();
            }

            if (refreshed.Count == 0) return;
            AppLogger.Info("Rotom script sources refreshed for " + string.Join(", ", refreshed.Select(i => i.ToString("D4"))) + ".");
            try { SourcesRefreshed?.Invoke(refreshed); }
            catch (Exception ex) { AppLogger.Warn("A script source listener failed: " + ex.Message); }
        }

        /// <summary>
        /// Rebuilds these files' sources with <c>rotom decompile --file</c> in the project at
        /// <paramref name="root"/>, which uses the whole project for names but writes only these sources
        /// and their compile-state entries. Each replaced source is backed up under
        /// <c>.rotom/backups</c> first, since hand-written names and comments do not survive a decompile.
        /// Files with no source are skipped: the project compile only builds existing sources.
        /// </summary>
        internal static async Task<IReadOnlyList<int>> RegenerateAsync(IReadOnlyCollection<int> ids, string root, string sourceDir,
            Func<int, string> binaryOf = null)
        {
            List<(int id, string source)> targets = new List<(int id, string source)>();
            foreach (int id in ids)
            {
                string name = id.ToString("D4");
                string rotom = Path.Combine(sourceDir, name + ".rotom"), json = Path.Combine(sourceDir, name + ".json");
                if (File.Exists(rotom)) targets.Add((id, rotom));
                else if (File.Exists(json)) targets.Add((id, json));
            }
            if (targets.Count == 0) return Array.Empty<int>();

            string backup = Path.Combine(root, ".rotom", "backups", "dspre-" + DateTime.Now.ToString("yyyyMMddHHmmssfff"));
            Directory.CreateDirectory(backup);
            foreach ((int _, string source) in targets)
                File.Copy(source, Path.Combine(backup, Path.GetFileName(source)), overwrite: true);

            binaryOf ??= Filesystem.GetScriptPath;
            // Windows refuses a command line past 32767 characters, which a whole project's paths exceed.
            const int MaxArgumentLength = 24000;
            // "--file", the separating spaces and a pair of quotes around the path.
            const int PerFileOverhead = 12;
            List<string> args = new List<string> { "decompile" };
            int length = 0;
            foreach ((int id, string _) in targets)
            {
                string binary = binaryOf(id);
                if (args.Count > 1 && length + binary.Length + PerFileOverhead > MaxArgumentLength)
                {
                    await DecompileAsync(root, args).ConfigureAwait(false);
                    args = new List<string> { "decompile" };
                    length = 0;
                }
                args.Add("--file"); args.Add(binary);
                length += binary.Length + PerFileOverhead;
            }
            await DecompileAsync(root, args).ConfigureAwait(false);
            return targets.Select(t => t.id).ToList();
        }

        private static async Task DecompileAsync(string root, List<string> args)
        {
            RotomTool.Result result = await RotomTool.RunInAsync(root, args.ToArray()).ConfigureAwait(false);
            if (!result.Success) throw new InvalidOperationException("rotom decompile failed: " + RotomTool.FormatResult(result));
        }
    }
}
