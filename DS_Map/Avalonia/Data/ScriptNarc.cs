using System.Collections.Generic;
using System.IO;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// Thin accessor for a one-file-per-entry NARC (the battle move-sequence and move-effect archives unpack to
    /// files "0000", "0001", …). Lazily unpacks on first use, then reads/writes individual entry files; the ROM
    /// save repacks them. Guards a missing mapping (e.g. an archive not wired for a version) via <see cref="Available"/>.
    /// </summary>
    public sealed class ScriptNarc
    {
        private readonly DirNames _dir;
        private bool _ready;
        private string _path;
        private int _count;

        public ScriptNarc(DirNames dir) { _dir = dir; }

        public void Invalidate() { _ready = false; }

        private void Ensure()
        {
            if (_ready) return;
            _ready = true;
            if (!gameDirs.ContainsKey(_dir)) { _path = null; _count = 0; return; }
            DSPRE.DSUtils.TryUnpackNarcs(new List<DirNames> { _dir });
            _path = gameDirs[_dir].unpackedDir;
            _count = (_path != null && Directory.Exists(_path)) ? Directory.GetFiles(_path).Length : 0;
        }

        /// <summary>True when the archive is mapped for the current game and unpacked.</summary>
        public bool Available { get { Ensure(); return _path != null && Directory.Exists(_path); } }

        /// <summary>Number of entry files (≈ number of moves / effects / subroutines).</summary>
        public int Count { get { Ensure(); return _count; } }

        private string FilePath(int id) => Path.Combine(_path, id.ToString("D4"));

        public byte[] Get(int id)
        {
            Ensure();
            if (_path == null) return null;
            string f = FilePath(id);
            return File.Exists(f) ? File.ReadAllBytes(f) : null;
        }

        public void Put(int id, byte[] data)
        {
            Ensure();
            if (_path != null) File.WriteAllBytes(FilePath(id), data);
        }
    }
}
