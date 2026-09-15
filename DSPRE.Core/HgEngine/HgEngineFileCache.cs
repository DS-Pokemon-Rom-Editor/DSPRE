using System;
using System.Collections.Generic;
using System.IO;

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

        private static readonly Dictionary<string, Entry> _cache = new(StringComparer.OrdinalIgnoreCase);

        internal static void ClearCache() => _cache.Clear();

        /// <summary>The one way DSPRE writes an hg-engine source file. It writes a temporary file and moves it into
        /// place, so a failed write can't leave the source half-written. It keeps the file's CRLF line endings
        /// (<paramref name="crlf"/> forces them), and refreshes the cache so the next read sees this text even
        /// within the same timestamp tick. Inside a <see cref="HgEngineWriteSession"/> the text is held until the
        /// session commits.</summary>
        internal static void WriteText(string path, string text, System.Text.Encoding encoding = null, bool crlf = false, bool keepLostComments = true)
        {
            var session = HgEngineWriteSession.Current;
            bool exists = session?.Holds(path) == true || File.Exists(path);
            bool keepCrlf = crlf || (exists && GetText(path).Contains("\r\n"));
            string lf = text.Replace("\r\n", "\n");
            string output = keepCrlf ? lf.Replace("\n", "\r\n") : lf;

            if (session != null)
            {
                session.Record(path, session.Holds(path) || !File.Exists(path) ? null : DiskText(path), output, encoding, keepCrlf);
                return;
            }

            // A write no editor asked about keeps what it would delete rather than lose it unseen.
            if (keepLostComments && File.Exists(path))
            {
                var lost = HgEngineSourceComments.Lost(path, DiskText(path), output);
                if (lost.Count > 0)
                {
                    AppLogger.Info($"Kept {lost.Count} comment(s) a write would have deleted from {path}.");
                    lf = HgEngineSourceComments.AppendKept(output, lost).Replace("\r\n", "\n");
                    output = keepCrlf ? lf.Replace("\n", "\r\n") : lf;
                }
            }

            string temp = path + ".dspre-tmp";
            try
            {
                File.WriteAllText(temp, output, encoding ?? new System.Text.UTF8Encoding(false));
                File.Move(temp, path, overwrite: true);
            }
            catch
            {
                try { if (File.Exists(temp)) File.Delete(temp); } catch (IOException) { }
                throw;
            }
            _cache[path] = new Entry(File.GetLastWriteTimeUtc(path), output);
        }

        public static string GetText(string path)
        {
            if (HgEngineWriteSession.Current?.TryGetText(path, out string pending) == true) return pending;
            return DiskText(path);
        }

        private static string DiskText(string path)
        {
            var writeTime = File.GetLastWriteTimeUtc(path);
            if (_cache.TryGetValue(path, out var cached) && cached.WriteTimeUtc == writeTime)
                return cached.Text;

            string text = File.ReadAllText(path);
            _cache[path] = new Entry(writeTime, text);
            return text;
        }
    }
}
