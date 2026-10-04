using System;
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
        private string _archive;

        public ScriptNarc(DirNames dir) { _dir = dir; }

        /// <summary>
        /// An editor's writes that wait for its Save. While one is in use on this thread every unpacked read asks
        /// it first and every write goes to it instead of the disk.
        /// </summary>
        public sealed class Staging
        {
            /// <summary>The staged bytes for an entry, or null when it has none.</summary>
            public Func<DirNames, int, byte[]> Read;
            public Action<DirNames, int, byte[]> Write;
        }

        [ThreadStatic] private static Staging _staging;

        /// <summary>Routes this thread's reads and writes through <paramref name="staging"/> until disposed.</summary>
        public static IDisposable Use(Staging staging)
        {
            var before = _staging;
            _staging = staging;
            return new Restore(before);
        }

        private sealed class Restore : IDisposable
        {
            private readonly Staging _before;
            public Restore(Staging before) => _before = before;
            public void Dispose() => _staging = _before;
        }

        // Pending entries every thread reads, for an editor whose reads are spread over background work.
        private static Func<DirNames, int, byte[]>[] _overlays = Array.Empty<Func<DirNames, int, byte[]>>();

        /// <summary>Makes every read, on any thread, ask <paramref name="read"/> first until disposed.</summary>
        public static IDisposable Overlay(Func<DirNames, int, byte[]> read)
        {
            lock (typeof(ScriptNarc))
            {
                var next = new List<Func<DirNames, int, byte[]>>(_overlays) { read };
                _overlays = next.ToArray();
            }
            return new RemoveOverlay(read);
        }

        private sealed class RemoveOverlay : IDisposable
        {
            private readonly Func<DirNames, int, byte[]> _read;
            public RemoveOverlay(Func<DirNames, int, byte[]> read) => _read = read;
            public void Dispose()
            {
                lock (typeof(ScriptNarc))
                {
                    var next = new List<Func<DirNames, int, byte[]>>(_overlays);
                    next.Remove(_read);
                    _overlays = next.ToArray();
                }
            }
        }

        /// <summary>The entry as the project holds it, whatever any editor has pending.</summary>
        public byte[] GetFromDisk(int id)
        {
            Ensure();
            if (_path == null) return null;
            if (_fromPacked) return FromPacked(id);
            return FromUnpacked(id);
        }

        // A member hg-engine copies in from its own file on every build is whatever that file holds now.
        private byte[] FromUnpacked(int id)
        {
            if (HgEngine.HgEngineSourceAssets.ReadVerbatim(_archive, id) is byte[] source) return source;
            string f = FilePath(id);
            return File.Exists(f) ? File.ReadAllBytes(f) : null;
        }

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

            _archive = HgEngine.HgEngineOwnedFiles.ArchiveOf(_dir);
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
            if (_staging?.Read(_dir, id) is byte[] staged) return (byte[])staged.Clone();
            foreach (var overlay in _overlays)
                if (overlay(_dir, id) is byte[] pending) return (byte[])pending.Clone();
            return FromUnpacked(id);
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
            if (_path == null) return;
            if (_staging != null) { _staging.Write(_dir, id, (byte[])data.Clone()); return; }
            // The build copies that file over the member, so a member saved only here would be lost.
            HgEngine.HgEngineSourceAssets.WriteVerbatim(_archive, id, data);
            byte[] before = File.Exists(FilePath(id)) ? File.ReadAllBytes(FilePath(id)) : null;
            File.WriteAllBytes(FilePath(id), data);
            BuiltPngSources.Write(_dir, _archive, id, _count, before, data);
            AppEvents.RaiseArchiveMemberSaved(_dir);
        }
    }
}
