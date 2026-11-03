using System.Collections.Generic;
using System.Linq;

namespace DSPRE.HgEngine
{
    /// <summary>
    /// Each entry as an editor loaded it. A save compares the editor's values with this and with the file, so it
    /// writes only what the user changed, keeps what changed outside DSPRE, and refuses a field changed on both sides.
    /// </summary>
    internal static class HgEngineLoadedEntries
    {
        private static readonly Dictionary<(string Path, string Designator), string> Entries = new();

        internal static void Clear()
        {
            lock (Entries) Entries.Clear();
        }

        /// <summary>Inside a save session the entry changes only when the session commits.</summary>
        internal static void Remember(string path, string designator, string entry)
        {
            HgEngineWriteSession session = HgEngineWriteSession.Current;
            if (session != null) session.RememberEntry(path, designator, entry);
            else Store(path, designator, entry);
        }

        internal static void Store(string path, string designator, string entry)
        {
            lock (Entries) Entries[(path, designator)] = entry;
        }

        internal static HgEngineSourceBlock? Get(string path, string designator)
        {
            if (HgEngineWriteSession.Current?.TryGetEntry(path, designator, out string pending) == true) return new HgEngineSourceBlock(pending);
            lock (Entries) return Entries.TryGetValue((path, designator), out string entry) ? new HgEngineSourceBlock(entry) : null;
        }

        /// <summary>For a list saved whole: true when the file's copy no longer matches what the editor loaded.</summary>
        internal static bool ChangedOutside(string path, string key, string onDisk) =>
            Get(path, key) is HgEngineSourceBlock loaded && !Same(loaded.Raw, onDisk);

        /// <summary>Returns the writes that still apply to <paramref name="disk"/>. It drops fields that already hold the
        /// value and fields only the file changed. A field changed on both sides goes in <paramref name="conflicts"/>.</summary>
        internal static List<HgEngineFieldWrite> Reconcile(HgEngineSourceBlock? loaded, HgEngineSourceBlock disk, IEnumerable<HgEngineFieldWrite> writes, out List<string> conflicts)
        {
            List<HgEngineFieldWrite> kept = new List<HgEngineFieldWrite>();
            conflicts = new List<string>();
            foreach (HgEngineFieldWrite write in writes)
            {
                disk.TryGetRaw(write.Path, out string onDisk);
                if (Same(write.ValueLiteral, onDisk)) continue;
                if (loaded != null)
                {
                    loaded.Value.TryGetRaw(write.Path, out string atLoad);
                    if (!Same(onDisk, atLoad))
                    {
                        if (!Same(write.ValueLiteral, atLoad)) conflicts.Add(string.Concat(write.Path.Select(p => p.ToString())));
                        continue;
                    }
                }
                kept.Add(write);
            }
            return kept;
        }

        /// <summary>The loaded entry with this save's writes applied. Changes made outside DSPRE aren't merged in, so the
        /// next save doesn't treat the editor's older values as edits.</summary>
        internal static string Merge(HgEngineSourceBlock loaded, IEnumerable<HgEngineFieldWrite> writes, IEnumerable<HgEngineArrayCount> arrayCounts = null)
        {
            const string token = "X";
            string prefix = $"[{token}] = ";
            string text = prefix + loaded.Raw;
            foreach (HgEngineArrayCount resize in arrayCounts ?? Enumerable.Empty<HgEngineArrayCount>())
                HgEngineSourcePatcher.TrySetArrayCount(ref text, token, resize.Path, resize.Count, resize.NewElement);
            foreach (HgEngineFieldWrite write in writes)
            {
                if (write.ValueLiteral == null) HgEngineSourcePatcher.TryRemoveField(ref text, token, write.Path);
                else HgEngineSourcePatcher.TryUpsertField(ref text, token, write.Path, write.ValueLiteral);
            }
            return text.Substring(prefix.Length);
        }

        internal static string ConflictMessage(string file, IEnumerable<string> fields) =>
            $"{string.Join(", ", fields.Select(f => f.TrimStart('.')))} also changed in {file} outside DSPRE, so nothing was written. Reload this entry to see the new values.";

        private static bool Same(string a, string b) => a == null || b == null ? a == b : HgEngineSourcePatcher.SameTokens(a, b);
    }
}
