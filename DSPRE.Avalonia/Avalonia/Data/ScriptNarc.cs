using System.Collections.Generic;
using System.IO;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// Thin accessor for a one-file-per-entry NARC (the battle move-sequence and move-effect archives unpack to
    /// files "0000", "0001", …). Lazily unpacks on first use, then reads/writes individual entry files; the ROM
    /// save repacks them. Guards a missing mapping (e.g. an archive not wired for a version) via <see cref="Available"/>.
    /// The second constructor reads the packed archive instead, read-only, for a caller that needs the
    /// ROM's own bytes.
    /// </summary>
    public sealed class ScriptNarc
    {
        private readonly DirNames _dir;
        private readonly bool _fromPacked;
        private readonly Dictionary<int, byte[]> _packedCache = new Dictionary<int, byte[]>();
        private bool _ready;
        private string _path;
        private int _count;

        public ScriptNarc(DirNames dir) { _dir = dir; }

        /// <summary>
        /// Reads the packed archive rather than its unpacked copy. Only that way are the bytes the ROM's
        /// own: while an hg-engine checkout is linked the unpacked copy of an owned archive holds that
        /// checkout's build instead, and asking for it rebuilds from source. Read-only.
        /// </summary>
        public ScriptNarc(DirNames dir, bool fromPacked) { _dir = dir; _fromPacked = fromPacked; }

        public void Invalidate() { _ready = false; _packedCache.Clear(); }

        private void Ensure()
        {
            if (_ready) return;
            _ready = true;
            if (!gameDirs.ContainsKey(_dir)) { _path = null; _count = 0; return; }

            if (_fromPacked)
            {
                _path = gameDirs[_dir].packedDir;
                _count = 0;
                if (_path == null || !File.Exists(_path)) { _path = null; return; }
                var narc = NarcAPI.Narc.Open(_path);
                if (narc == null) { _path = null; return; }
                try { _count = narc.ElementCount; } finally { narc.Free(); }
                return;
            }

            DSPRE.DSUtils.TryUnpackNarcs(new List<DirNames> { _dir });
            _path = gameDirs[_dir].unpackedDir;
            _count = (_path != null && Directory.Exists(_path)) ? Directory.GetFiles(_path).Length : 0;
        }

        /// <summary>True when the archive is mapped for the current game and there to read.</summary>
        public bool Available
        {
            get { Ensure(); return _path != null && (_fromPacked ? File.Exists(_path) : Directory.Exists(_path)); }
        }

        /// <summary>Number of entry files (≈ number of moves / effects / subroutines).</summary>
        public int Count { get { Ensure(); return _count; } }

        private string FilePath(int id) => Path.Combine(_path, id.ToString("D4"));

        public byte[] Get(int id)
        {
            Ensure();
            if (_path == null) return null;
            if (_fromPacked) return FromPacked(id);
            string f = FilePath(id);
            return File.Exists(f) ? File.ReadAllBytes(f) : null;
        }

        /// <summary>
        /// The archive is opened and closed around each read so nothing holds the file open against a
        /// ROM save, and what has been read is kept. Every caller gets its own copy: readers of
        /// scrambled archives unscramble in place, and handing the same array out twice would leave the
        /// second caller unscrambling what was already plain.
        /// </summary>
        private byte[] FromPacked(int id)
        {
            if (id < 0 || id >= _count) return null;
            if (_packedCache.TryGetValue(id, out byte[] cached)) return (byte[])cached?.Clone();

            var narc = NarcAPI.Narc.Open(_path);
            if (narc == null) return null;
            byte[] bytes;
            try { bytes = narc.GetElementBytes(id); } finally { narc.Free(); }
            _packedCache[id] = bytes;
            return (byte[])bytes?.Clone();
        }

        public void Put(int id, byte[] data)
        {
            Ensure();
            if (_fromPacked) return;   // read-only view of the ROM's own bytes
            if (_path != null) File.WriteAllBytes(FilePath(id), data);
        }
    }
}
