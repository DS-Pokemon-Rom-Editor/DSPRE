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

        public static string GetText(string path)
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
