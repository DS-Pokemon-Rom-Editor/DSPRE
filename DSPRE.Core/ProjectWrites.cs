using System;
using System.Collections.Generic;
using System.Threading;

namespace DSPRE
{
    /// <summary>
    /// Raised after DSPRE writes, moves or deletes a project file, with the file's path.
    /// </summary>
    public static class ProjectWrites
    {
        public static event Action<string> Written;

        /// <summary>Raised once per code write, before <see cref="Written"/>, with every changed range and the <see cref="By"/> name.</summary>
        public static event Action<string, IReadOnlyList<(long Offset, int Length)>, string> CodeWritten;

        private static readonly AsyncLocal<string> _by = new AsyncLocal<string>();

        /// <summary>Names what is writing until the returned scope is disposed, like "Patch toolbox: Expand trainer classes".</summary>
        public static IDisposable By(string what)
        {
            string outer = _by.Value;
            _by.Value = what;
            return new OnDispose(() => _by.Value = outer);
        }

        public static void Raise(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            try { Written?.Invoke(path); }
            catch (Exception ex) { AppLogger.WarnOnce("ProjectWrites.Raise", ex); }
        }

        public static void RaiseCode(string path, long offset, int length)
        {
            if (string.IsNullOrEmpty(path)) return;
            CodeEvent(path, new[] { (offset, length) });
            Raise(path);
        }

        /// <summary>Shortest unchanged stretch that splits a code write into two ranges.</summary>
        public const int CodeJoinGap = 8;

        /// <summary>Reports a replaced code file: one code write with each run of changed bytes, then one plain write.</summary>
        public static void RaiseCodeChanges(string path, byte[] before, byte[] after)
        {
            if (string.IsNullOrEmpty(path) || after == null) return;
            RaiseCodeChanges(path, ByteRanges.Changed(before, after, CodeJoinGap, joinTail: true));
        }

        /// <summary>Like <see cref="RaiseCodeChanges(string, byte[], byte[])"/>, with the runs already worked out.</summary>
        public static void RaiseCodeChanges(string path, List<(int At, int Length)> changed)
        {
            if (string.IsNullOrEmpty(path)) return;
            if (changed.Count > 0) CodeEvent(path, changed.ConvertAll(r => ((long)r.At, r.Length)));
            Raise(path);
        }

        private static void CodeEvent(string path, IReadOnlyList<(long Offset, int Length)> ranges)
        {
            try { CodeWritten?.Invoke(path, ranges, _by.Value); }
            catch (Exception ex) { AppLogger.WarnOnce("ProjectWrites.RaiseCode", ex); }
        }
    }
}
