using System;
using System.Globalization;
using static DSPRE.RomInfo;

namespace DSPRE.Tracking
{
    /// <summary>An archive member ("TrainerParty/0012"), a ROM tree file ("arm9/arm9.bin") or a DSPRE source ("src:expanded/...").</summary>
    public readonly record struct TrackedFile(string Key) : IComparable<TrackedFile>
    {
        private const string SourcePrefix = "src:";

        public static TrackedFile Member(DirNames dir, int id) => new TrackedFile(dir + "/" + id.ToString("D4", CultureInfo.InvariantCulture));
        public static TrackedFile Tree(string relativePath) => new TrackedFile(relativePath.Replace('\\', '/'));
        public static TrackedFile Source(string relativePath) => new TrackedFile(SourcePrefix + relativePath.Replace('\\', '/'));

        private const string RowPrefix = "table:";

        /// <summary>One row of a table inside code, like a map header in arm9.</summary>
        public static TrackedFile Row(string table, int index) => new TrackedFile(RowPrefix + table + "/" + index.ToString("D4", CultureInfo.InvariantCulture));

        public bool TryRow(out string table, out int index)
        {
            table = null;
            index = -1;
            if (!Key.StartsWith(RowPrefix, StringComparison.Ordinal)) return false;
            int slash = Key.LastIndexOf('/');
            // "table:name" alone names a whole table, as a record's members do.
            if (slash <= RowPrefix.Length) return false;
            table = Key.Substring(RowPrefix.Length, slash - RowPrefix.Length);
            return int.TryParse(Key.AsSpan(slash + 1), NumberStyles.None, CultureInfo.InvariantCulture, out index);
        }

        public bool IsSource => Key.StartsWith(SourcePrefix, StringComparison.Ordinal);
        public string SourcePath => IsSource ? Key.Substring(SourcePrefix.Length) : null;

        public bool TryMember(out DirNames dir, out int id)
        {
            dir = default;
            id = -1;
            int slash = Key.IndexOf('/');
            if (slash <= 0 || IsSource || Key.StartsWith(RowPrefix, StringComparison.Ordinal)) return false;
            if (!Enum.TryParse(Key.AsSpan(0, slash), out dir) || !Enum.IsDefined(dir)) return false;
            return int.TryParse(Key.AsSpan(slash + 1), NumberStyles.None, CultureInfo.InvariantCulture, out id);
        }

        public string TreePath => IsSource || TryMember(out _, out _) || Key.StartsWith(RowPrefix, StringComparison.Ordinal) ? null : Key;

        public int CompareTo(TrackedFile other) => string.CompareOrdinal(Key, other.Key);
        public override string ToString() => Key;
    }
}
