using System;
using System.Collections.Generic;
using System.IO;
using NarcAPI;
using static DSPRE.RomInfo;

namespace DSPRE
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

        /// <summary>NARC files under the data folder that no archive maps, in path order.</summary>
        public static List<ArchiveFiles> Unmapped(string dataPath)
        {
            List<ArchiveFiles> found = new List<ArchiveFiles>();
            if (string.IsNullOrEmpty(dataPath) || !Directory.Exists(dataPath) || GameDirs == null) return found;
            HashSet<string> mapped = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach ((string packedDir, string unpackedDir) dir in GameDirs.Values)
            {
                try { mapped.Add(Path.GetFullPath(Path.Combine(WorkDir ?? "", dir.packedDir))); }
                // An archive path that can't be resolved maps nothing.
                catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException) { }
            }
            List<string> paths = new List<string>(Directory.EnumerateFiles(dataPath, "*", SearchOption.AllDirectories));
            paths.Sort(StringComparer.Ordinal);
            foreach (string path in paths)
            {
                string full = Path.GetFullPath(path);
                if (mapped.Contains(full) || !StartsLikeANarc(full)) continue;
                found.Add(Loose(full, ProjectPaths.RelativeKey(dataPath, full)));
            }
            return found;
        }

        private static bool StartsLikeANarc(string path)
        {
            try
            {
                using FileStream s = File.OpenRead(path);
                byte[] head = new byte[4];
                return s.Read(head, 0, 4) == 4 && head[0] == 'N' && head[1] == 'A' && head[2] == 'R' && head[3] == 'C';
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                AppLogger.WarnOnce("ArchiveFiles.StartsLikeANarc", ex);
                return false;
            }
        }

        private byte[][] LooseFiles()
        {
            if (_loose != null) return _loose;
            _loose = Array.Empty<byte[]>();
            if (!File.Exists(LoosePath)) return _loose;
            Narc narc = Narc.Open(LoosePath);
            if (narc == null) return _loose;
            try
            {
                byte[][] files = new byte[narc.ElementCount][];
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
            // As in ScriptNarc: a member hg-engine copies in from a separate file is read from that file.
            if (HgEngine.HgEngineSourceAssets.ReadVerbatim(HgEngine.HgEngineOwnedFiles.ArchiveOfPath(LoosePath), index) is byte[] source)
                return source;
            byte[][] files = LooseFiles();
            return index >= 0 && index < files.Length ? files[index] : null;
        }

        /// <summary>Writes files back. A loose archive is rewritten once, with every other file kept as it was.</summary>
        public void Put(IReadOnlyDictionary<int, byte[]> files)
        {
            if (_mapped != null)
            {
                foreach (KeyValuePair<int, byte[]> file in files) _mapped.Put(file.Key, file.Value);
                return;
            }

            foreach (KeyValuePair<int, byte[]> file in files)
                HgEngine.HgEngineSourceAssets.WriteVerbatim(HgEngine.HgEngineOwnedFiles.ArchiveOfPath(LoosePath), file.Key, file.Value);

            Narc narc = Narc.Open(LoosePath) ?? throw new IOException("That archive couldn't be read.");
            try
            {
                foreach (KeyValuePair<int, byte[]> file in files)
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
