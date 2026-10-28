using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// The byte runs a screen's button reader edits, gathered from overlays and arm9 into one buffer. Each file is written back
    /// whole, so a refused or failed write leaves every file as it was.
    /// </summary>
    public sealed class ScreenTables
    {
        public const int InArm9 = -1;
        private const int Loose = -2;

        // Bytes from a file, or with Loose set, bytes the owner reads and writes itself.
        private sealed record Segment(int File, int At, int Length, int Start, Func<byte[]> Reread = null);

        private readonly List<Segment> _segments = new List<Segment>();
        private readonly Dictionary<int, byte[]> _files = new Dictionary<int, byte[]>();
        private readonly List<byte> _building = new List<byte>();
        private byte[] _data, _saved;
        private HashSet<string> _known;

        /// <summary>True once a run fell outside its file, or the file couldn't be read.</summary>
        public bool Missing { get; private set; }

        /// <summary>Where the next run starts in the buffer.</summary>
        public int Count => _data?.Length ?? _building.Count;

        /// <summary>The live buffer, after <see cref="Finish"/>.</summary>
        public byte[] Data => _data;

        public static string PathOf(int file) => file == InArm9 ? RomInfo.Arm9Path : OverlayUtils.DecompressedPath(file);

        /// <summary>The file as read when the tables were gathered, or null when it is missing or arm9 is still compressed.</summary>
        public byte[] FileOf(int file)
        {
            if (_files.TryGetValue(file, out byte[] bytes)) return bytes;
            bool compressed = file == InArm9 && !RomInfo.IsDsRomProject && ARM9.CheckCompressionMark();
            string path = compressed ? null : PathOf(file);
            return _files[file] = path != null && File.Exists(path) ? File.ReadAllBytes(path) : null;
        }

        /// <summary>A file read outside the tables, or null.</summary>
        public static byte[] ReadFile(int file) => new ScreenTables().FileOf(file);

        /// <summary>Adds <paramref name="length"/> bytes of the file from <paramref name="at"/>, and returns where they start.</summary>
        public int Take(int file, int at, int length)
        {
            int start = _building.Count;
            byte[] bytes = FileOf(file);
            if (bytes == null || at < 0 || length < 0 || at + length > bytes.Length)
            {
                Missing = true;
                _building.AddRange(new byte[Math.Max(0, length)]);
            }
            else _building.AddRange(bytes.Skip(at).Take(length));
            _segments.Add(new Segment(file, at, length, start));
            return start;
        }

        /// <summary>Adds bytes kept somewhere else, which the owner writes; <paramref name="reread"/> reads them again.</summary>
        public int Add(byte[] bytes, Func<byte[]> reread)
        {
            int start = _building.Count;
            _building.AddRange(bytes);
            _segments.Add(new Segment(Loose, 0, bytes.Length, start, reread));
            return start;
        }

        public byte[] Finish() => _data = _building.ToArray();

        public byte[] Copy() => (byte[])_data.Clone();

        public void Load(byte[] bytes)
        {
            if (bytes != null && bytes.Length == _data.Length) Array.Copy(bytes, _data, _data.Length);
        }

        /// <summary>The bytes the files hold now, in buffer order.</summary>
        public byte[] FileBytes()
        {
            byte[] bytes = new byte[_data.Length];
            Dictionary<int, byte[]> files = new Dictionary<int, byte[]>();
            foreach (Segment s in _segments)
            {
                byte[] from = s.File == Loose ? s.Reread?.Invoke() : files.TryGetValue(s.File, out byte[] f) ? f : files[s.File] = File.ReadAllBytes(PathOf(s.File));
                if (from == null) continue;
                int at = s.File == Loose ? 0 : s.At;
                Array.Copy(from, at, bytes, s.Start, Math.Min(s.Length, from.Length - at));
            }
            return bytes;
        }

        /// <summary>Writes every run; the loose bytes are the owner's to write.</summary>
        public void Write(byte[] bytes)
        {
            if (bytes == null || bytes.Length != _data.Length) throw new InvalidOperationException("The tables don't match this ROM.");
            List<(string Path, byte[] Before)> written = new List<(string, byte[])>();
            try
            {
                foreach (IGrouping<int, Segment> file in _segments.Where(s => s.File != Loose).GroupBy(s => s.File))
                {
                    string path = PathOf(file.Key);
                    byte[] before = File.ReadAllBytes(path), after = (byte[])before.Clone();
                    foreach (Segment s in file) Array.Copy(bytes, s.Start, after, s.At, s.Length);
                    DSUtils.WriteWholeFile(path, after);
                    written.Add((path, before));
                }
            }
            catch
            {
                foreach ((string path, byte[] before) in written) DSUtils.WriteWholeFile(path, before);
                throw;
            }
            _saved = (byte[])bytes.Clone();
            _known = null;
        }

        /// <summary>The first problem in <paramref name="data"/> that the files didn't already have, or null.</summary>
        public string NewProblem(Func<byte[], IEnumerable<string>> problems, byte[] data)
        {
            _known ??= problems(_saved ?? FileBytes()).ToHashSet();
            return problems(data).FirstOrDefault(p => !_known.Contains(p));
        }
    }
}
