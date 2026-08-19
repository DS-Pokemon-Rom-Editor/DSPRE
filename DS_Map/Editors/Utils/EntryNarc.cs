using System.Collections.Generic;
using System.IO;
using static DSPRE.RomInfo;

namespace DSPRE.Editors.Utils
{
    /// <summary>Reads a one-file-per-entry NARC (unpacks to files "0000", "0001", …); Available is false
    /// if the archive isn't mapped for the current game.</summary>
    public sealed class EntryNarc
    {
        private readonly DirNames _dir;
        private bool _ready;
        private string _path;

        public EntryNarc(DirNames dir) { _dir = dir; }

        private void Ensure()
        {
            if (_ready) return;
            _ready = true;
            if (!gameDirs.ContainsKey(_dir)) { _path = null; return; }
            DSUtils.TryUnpackNarcs(new List<DirNames> { _dir });
            _path = gameDirs[_dir].unpackedDir;
        }

        public bool Available { get { Ensure(); return _path != null && Directory.Exists(_path); } }

        private string FilePath(int id) => Path.Combine(_path, id.ToString("D4"));

        public byte[] Get(int id)
        {
            Ensure();
            if (_path == null) return null;
            string f = FilePath(id);
            return File.Exists(f) ? File.ReadAllBytes(f) : null;
        }
    }
}
