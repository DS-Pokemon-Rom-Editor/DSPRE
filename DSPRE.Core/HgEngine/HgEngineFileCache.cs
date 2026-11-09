using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DSPRE.HgEngine
{
    /// <summary>Caches hg-engine source-file text by path, validated against the file's own last-write
    /// time, so a cache hit costs one metadata stat instead of a full file re-read over the WSL UNC
    /// path.</summary>
    internal static class HgEngineFileCache
    {
        private readonly struct Entry
        {
            public readonly DateTime WriteTimeUtc;
            public readonly string Text;
            public Entry(DateTime writeTimeUtc, string text) { WriteTimeUtc = writeTimeUtc; Text = text; }
        }

        private static readonly Dictionary<string, Entry> Cache = new(StringComparer.OrdinalIgnoreCase);

        internal static void ClearCache() => Cache.Clear();

        /// <summary>The one way DSPRE writes an hg-engine source file. It writes a temporary file and moves it into
        /// place, so a failed write can't leave the source half-written. It keeps the file's CRLF line endings
        /// (<paramref name="crlf"/> forces them), and refreshes the cache so the next read sees this text even
        /// within the same timestamp tick. Inside a <see cref="HgEngineWriteSession"/> the text is held until the
        /// session commits.</summary>
        internal static void WriteText(string path, string text, System.Text.Encoding encoding = null, bool crlf = false, bool keepLostComments = true)
        {
            HgEngineWriteSession session = HgEngineWriteSession.Current;
            bool exists = session?.Holds(path) == true || File.Exists(path);
            bool keepCrlf = crlf || (exists && GetText(path).Contains("\r\n"));
            string lf = text.Replace("\r\n", "\n");
            string output = keepCrlf ? lf.Replace("\n", "\r\n") : lf;

            if (session != null)
            {
                session.Record(path, session.Holds(path) || !File.Exists(path) ? null : DiskText(path), output, encoding, keepCrlf);
                return;
            }

            // A write no editor asked about keeps the comments it would delete, so nothing is lost unseen.
            if (keepLostComments && File.Exists(path))
            {
                List<HgEngineLostComment> lost = HgEngineSourceComments.Lost(path, DiskText(path), output);
                if (lost.Count > 0)
                {
                    AppLogger.Info($"Kept {Plural.Of(lost.Count, "comment")} a write would have deleted from {path}.");
                    lf = HgEngineSourceComments.AppendKept(output, lost).Replace("\r\n", "\n");
                    output = keepCrlf ? lf.Replace("\n", "\r\n") : lf;
                }
            }

            string current = null;
            Store(new[] { (path, output, encoding) }, ref current);
        }

        /// <summary>Writes all the files or none. Each goes to a temporary file first, and if moving one into place fails,
        /// the files already moved are restored. Throws an <see cref="IOException"/> naming the file.</summary>
        internal static void WriteAll(IReadOnlyList<(string Path, string Text, System.Text.Encoding Encoding, bool Crlf)> files)
        {
            string current = null;
            try
            {
                Store(files.Select(f =>
                {
                    string lf = f.Text.Replace("\r\n", "\n");
                    return (f.Path, f.Crlf ? lf.Replace("\n", "\r\n") : lf, f.Encoding);
                }).ToList(), ref current);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                throw new IOException($"Couldn't write {Path.GetFileName(current)}: {ex.Message}", ex);
            }
        }

        private const int BackupsPerFile = 10;
        private static readonly TimeSpan BackupGap = TimeSpan.FromMinutes(1);

        /// <summary>Backs up, moves each file into place and reads it back. Any failure puts every file back.
        /// <paramref name="current"/> names the file being handled.</summary>
        private static void Store(IReadOnlyList<(string Path, string Output, System.Text.Encoding Encoding)> files, ref string current)
        {
            List<(string Path, string Temp, string Output, byte[] Before, System.Text.Encoding Encoding)> staged = new();
            int moved = 0;
            try
            {
                foreach ((string path, string output, System.Text.Encoding encoding) in files)
                {
                    current = path;
                    byte[] before = File.Exists(path) ? File.ReadAllBytes(path) : null;
                    staged.Add((path, WriteTemp(path, output, encoding), output, before, encoding));
                }
                foreach ((string path, string _, string _, byte[] before, System.Text.Encoding _) in staged) BackUp(path, before);
                foreach ((string path, string temp, string output, byte[] _, System.Text.Encoding encoding) in staged)
                {
                    current = path;
                    Install(path, temp, output);
                    moved++;
                    if (File.ReadAllText(path, encoding ?? System.Text.Encoding.UTF8) != output) throw new IOException("It read back differently.");
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                foreach ((string path, string _, string _, byte[] before, System.Text.Encoding _) in staged.Take(moved)) Restore(path, before);
                foreach ((string _, string temp, string _, byte[] _, System.Text.Encoding _) in staged.Skip(moved)) DeleteTemp(temp);
                throw;
            }
        }

        /// <summary>Keeps a copy in the checkout's ignored .dspre/backups. A burst of writes keeps the copy from
        /// before the burst. A backup that fails never stops a save.</summary>
        private static void BackUp(string path, byte[] before)
        {
            string root = HgEngineProject.RepoRootWindows;
            if (before == null || string.IsNullOrEmpty(root)) return;
            string relative = Path.GetRelativePath(root, path);
            if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative)) return;
            try
            {
                string dspre = HgEngineProject.DspreFolder(root);
                if (dspre == null) return;
                string folder = ProjectBackups.Folder(dspre);
                string name = ProjectBackups.FlatName(relative);
                List<string> copies = Directory.GetFiles(folder, name + ".*").OrderByDescending(File.GetLastWriteTimeUtc).ToList();
                if (copies.Count > 0 && DateTime.UtcNow - File.GetLastWriteTimeUtc(copies[0]) < BackupGap) return;
                PlainFiles.WriteBytes(Path.Combine(folder, name + "." + DateTime.Now.ToString("yyyyMMdd-HHmmss")), before);
                foreach (string old in copies.Skip(BackupsPerFile - 1)) File.Delete(old);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                AppLogger.Warn($"hg-engine save: no backup of {relative}: {ex.Message}");
            }
        }

        private static string WriteTemp(string path, string output, System.Text.Encoding encoding)
        {
            string temp = path + DSUtils.TempSuffix;
            try { PlainFiles.WriteText(temp, output, encoding ?? new System.Text.UTF8Encoding(false)); }
            catch
            {
                DeleteTemp(temp);
                throw;
            }
            return temp;
        }

        private static void Install(string path, string temp, string output)
        {
            PlainFiles.Move(temp, path, overwrite: true);
            Cache[path] = new Entry(File.GetLastWriteTimeUtc(path), output);
        }

        private static void Restore(string path, byte[] before)
        {
            Cache.Remove(path);
            try
            {
                if (before != null) PlainFiles.WriteBytes(path, before);
                else File.Delete(path);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                AppLogger.Error($"hg-engine save: couldn't put {path} back: {ex.Message}");
            }
        }

        private static void DeleteTemp(string temp)
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch (IOException) { }   // Cleanup only. The original failure is still reported.
        }

        public static string GetText(string path)
        {
            if (HgEngineWriteSession.Current?.TryGetText(path, out string pending) == true) return pending;
            return DiskText(path);
        }

        private static string DiskText(string path)
        {
            DateTime writeTime = File.GetLastWriteTimeUtc(path);
            if (Cache.TryGetValue(path, out Entry cached) && cached.WriteTimeUtc == writeTime)
                return cached.Text;

            string text = File.ReadAllText(path);
            Cache[path] = new Entry(writeTime, text);
            return text;
        }
    }
}
