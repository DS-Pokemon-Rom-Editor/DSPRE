using System;
using System.Collections.Generic;
using System.IO;
using NarcAPI;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.Data
{
    /// <summary>One archive's files: a mapped archive goes through its unpacked copy, any other NARC is rewritten in place.</summary>
    public sealed class ArchiveFiles
    {
        public DirNames? Dir { get; }
        public string LoosePath { get; }
        public string Name { get; }

        private readonly ScriptNarc _mapped;
        private byte[][] _loose;

        private ArchiveFiles(DirNames? dir, string loosePath, string name, ScriptNarc mapped)
        {
            Dir = dir; LoosePath = loosePath; Name = name; _mapped = mapped;
        }

        public static ArchiveFiles Mapped(DirNames dir) => new ArchiveFiles(dir, null, dir.ToString(), new ScriptNarc(dir));

        public static ArchiveFiles Loose(string path, string name) => new ArchiveFiles(null, path, name, null);

        private byte[][] LooseFiles()
        {
            if (_loose != null) return _loose;
            _loose = Array.Empty<byte[]>();
            if (!File.Exists(LoosePath)) return _loose;
            var narc = Narc.Open(LoosePath);
            if (narc == null) return _loose;
            try
            {
                var files = new byte[narc.ElementCount][];
                for (int i = 0; i < files.Length; i++) files[i] = narc.GetElementBytes(i);
                _loose = files;
            }
            finally { narc.Free(); }
            return _loose;
        }

        public bool Available => _mapped != null ? _mapped.Available : LooseFiles().Length > 0;

        public int Count => _mapped != null ? _mapped.Count : LooseFiles().Length;

        public byte[] Get(int index)
        {
            if (_mapped != null) return _mapped.Get(index);
            var files = LooseFiles();
            return index >= 0 && index < files.Length ? files[index] : null;
        }

        /// <summary>Writes files back. A loose archive is rewritten once, with every other file kept as it was.</summary>
        public void Put(IReadOnlyDictionary<int, byte[]> files)
        {
            if (_mapped != null)
            {
                foreach (var file in files) _mapped.Put(file.Key, file.Value);
                return;
            }

            var narc = Narc.Open(LoosePath) ?? throw new IOException("That archive could not be read.");
            try
            {
                foreach (var file in files)
                {
                    if (file.Key < 0 || file.Key >= narc.ElementCount)
                        throw new IOException($"That archive has no file {file.Key}.");
                    narc[file.Key] = new MemoryStream(file.Value);
                }
                narc.Save(LoosePath);
            }
            finally { narc.Free(); }
            _loose = null;
        }
    }
}
