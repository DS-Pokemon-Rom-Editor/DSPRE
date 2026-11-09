using System;
using System.Collections.Generic;

namespace DSPRE
{
    /// <summary>Where two versions of a file differ, as runs of changed bytes.</summary>
    public static class ByteRanges
    {
        /// <summary>
        /// The changed runs, joined over unchanged stretches shorter than <paramref name="joinGap"/>. A length change adds
        /// the tail as a run, joined onto the last one with <paramref name="joinTail"/> when they are that close.
        /// </summary>
        public static List<(int At, int Length)> Changed(byte[] before, byte[] after, int joinGap, bool joinTail = false)
        {
            before ??= Array.Empty<byte>();
            after ??= Array.Empty<byte>();
            List<(int At, int Length)> ranges = new List<(int At, int Length)>();
            int shared = Math.Min(before.Length, after.Length);
            int start = -1, last = -1;
            for (int i = 0; i < shared; i++)
            {
                if (before[i] == after[i]) continue;
                if (start >= 0 && i - last - 1 >= joinGap)
                {
                    ranges.Add((start, last - start + 1));
                    start = -1;
                }
                if (start < 0) start = i;
                last = i;
            }
            if (before.Length == after.Length)
            {
                if (start >= 0) ranges.Add((start, last - start + 1));
                return ranges;
            }
            int end = Math.Max(before.Length, after.Length);
            if (start >= 0 && joinTail && shared - last - 1 < joinGap)
            {
                ranges.Add((start, end - start));
                return ranges;
            }
            if (start >= 0) ranges.Add((start, last - start + 1));
            ranges.Add((shared, end - shared));
            return ranges;
        }
    }
}
