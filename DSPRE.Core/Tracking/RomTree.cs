using NarcAPI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static DSPRE.RomInfo;

namespace DSPRE.Tracking
{
    /// <summary>Reads the files of a ROM project, or of the base ROM's extracted copy laid out the same way.</summary>
    public sealed class RomTree
    {
        private static readonly string[] DsRomRoots = { "arm9", "arm7", "arm9_overlays", "arm7_overlays", "banner", "files", "header.yaml", "header_logo.png" };
        private static readonly string[] LegacyRoots = { "arm9.bin", "arm7.bin", "y9.bin", "y7.bin", "banner.bin", "header.bin", "data", "overlay" };

        public string Root { get; }
        public bool IsDsRom { get; }

        /// <summary>Each archive's packed file, relative to the root.</summary>
        public IReadOnlyDictionary<DirNames, string> Archives { get; }

        /// <summary>Tables inside code files whose rows are tracked one by one.</summary>
        public IReadOnlyList<CodeTable> Tables { get; }

        private readonly string _projectDir;
        private readonly Dictionary<DirNames, Narc> _narcs = new Dictionary<DirNames, Narc>();
        private readonly Dictionary<string, byte[]> _code = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        private readonly HashSet<string> _archivePaths;
        private IReadOnlyDictionary<TrackedFile, byte[]> _overlay;

        private RomTree(string root, bool isDsRom, IReadOnlyDictionary<DirNames, string> archives, string projectDir, IReadOnlyList<CodeTable> tables)
        {
            Root = root;
            IsDsRom = isDsRom;
            Archives = archives;
            Tables = tables;
            _projectDir = projectDir;
            _archivePaths = new HashSet<string>(archives.Values, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>The open project, reading archives from their unpacked folders where they exist.</summary>
        public static RomTree Project()
        {
            string root = WorkDir.TrimEnd('\\', '/');
            return new RomTree(root, IsDsRomProject, ArchivePaths(root), DspreDir.TrimEnd('\\', '/'), CodeTable.ForOpenGame(root));
        }

        /// <summary>An extracted copy of the base ROM, with archives and tables at the same places as in the project.</summary>
        public static RomTree Base(string root) =>
            new RomTree(root, File.Exists(Path.Combine(root, "config.yaml")), ArchivePaths(WorkDir.TrimEnd('\\', '/')), null,
                CodeTable.ForOpenGame(WorkDir.TrimEnd('\\', '/')));

        /// <summary>This tree with some files read from <paramref name="files"/> instead, such as a patch laid over the base ROM.</summary>
        public RomTree WithFiles(IReadOnlyDictionary<TrackedFile, byte[]> files) =>
            new RomTree(Root, IsDsRom, Archives, _projectDir, Tables) { _overlay = files };

        public CodeTable Table(string name) => Tables.FirstOrDefault(t => t.Name == name);

        public IEnumerable<TrackedFile> Rows() => Tables.SelectMany(t => Enumerable.Range(0, t.Count).Select(i => TrackedFile.Row(t.Name, i)));

        private byte[] CodeBytes(string treePath)
        {
            if (_code.TryGetValue(treePath, out byte[] bytes)) return bytes;
            if (_overlay != null && _overlay.TryGetValue(TrackedFile.Tree(treePath), out bytes)) return _code[treePath] = bytes;
            string path = Path.Combine(Root, treePath);
            bytes = File.Exists(path) ? File.ReadAllBytes(path) : null;
            _code[treePath] = bytes;
            return bytes;
        }

        private static Dictionary<DirNames, string> ArchivePaths(string projectRoot)
        {
            Dictionary<DirNames, string> paths = new Dictionary<DirNames, string>();
            foreach (KeyValuePair<DirNames, (string packedDir, string unpackedDir)> d in GameDirs)
            {
                if (string.IsNullOrEmpty(d.Value.packedDir)) continue;
                paths[d.Key] = Path.GetRelativePath(projectRoot, d.Value.packedDir).Replace('\\', '/');
            }
            return paths;
        }

        public bool IsProject => _projectDir != null;

        public string FullPath(TrackedFile file)
        {
            if (file.TryRow(out string table, out int _)) return Table(table) is CodeTable t ? Path.Combine(Root, t.TreePath) : null;
            if (file.IsSource) return _projectDir == null ? null : Path.Combine(_projectDir, file.SourcePath);
            if (file.TryMember(out DirNames dir, out int id))
            {
                string unpacked = UnpackedDir(dir);
                return unpacked == null ? null : Path.Combine(unpacked, id.ToString("D4"));
            }
            return Path.Combine(Root, file.Key);
        }

        private string UnpackedDir(DirNames dir) =>
            _projectDir != null && GameDirs.TryGetValue(dir, out (string packedDir, string unpackedDir) d) && Directory.Exists(d.unpackedDir) ? d.unpackedDir : null;

        /// <summary>The file's bytes, or null when this tree doesn't have it.</summary>
        public byte[] Read(TrackedFile file)
        {
            if (_overlay != null && !file.TryRow(out _, out _) && _overlay.TryGetValue(file, out byte[] laid)) return laid;
            if (file.TryRow(out string tableName, out int row))
            {
                if (Table(tableName) is not CodeTable table || row < 0 || row >= table.Count) return null;
                byte[] code = CodeBytes(table.TreePath);
                long at = table.RowOffset(row);
                return code == null || at + table.RowSize > code.Length ? null : code.AsSpan((int)at, table.RowSize).ToArray();
            }
            if (!file.TryMember(out DirNames dir, out int id))
            {
                string path = FullPath(file);
                return path != null && File.Exists(path) ? File.ReadAllBytes(path) : null;
            }
            string unpacked = UnpackedDir(dir);
            if (unpacked != null)
            {
                string member = Path.Combine(unpacked, id.ToString("D4"));
                return File.Exists(member) ? File.ReadAllBytes(member) : null;
            }
            Narc narc = OpenNarc(dir);
            return narc != null && id >= 0 && id < narc.ElementCount ? narc.GetElementBytes(id) : null;
        }

        public int MemberCount(DirNames dir)
        {
            string unpacked = UnpackedDir(dir);
            if (unpacked != null) return Members(dir).Select(m => { m.TryMember(out _, out int id); return id + 1; }).DefaultIfEmpty(0).Max();
            return OpenNarc(dir)?.ElementCount ?? 0;
        }

        public IEnumerable<TrackedFile> Members(DirNames dir)
        {
            string unpacked = UnpackedDir(dir);
            if (unpacked != null)
            {
                foreach (string file in Directory.EnumerateFiles(unpacked))
                    if (int.TryParse(Path.GetFileName(file), out int id) && id >= 0) yield return TrackedFile.Member(dir, id);
                yield break;
            }
            int count = OpenNarc(dir)?.ElementCount ?? 0;
            for (int i = 0; i < count; i++) yield return TrackedFile.Member(dir, i);
        }

        /// <summary>Size and write time, to tell when a file changed without reading it.</summary>
        public (long Size, long Ticks) Stamp(TrackedFile file)
        {
            string path = FullPath(file);
            if (path == null && file.TryMember(out DirNames dir, out _) && Archives.TryGetValue(dir, out string narc)) path = Path.Combine(Root, narc);
            if (path == null) return (-1, 0);
            FileInfo info = new FileInfo(path);
            return info.Exists ? (info.Length, info.LastWriteTimeUtc.Ticks) : (-1, 0);
        }

        /// <summary>ROM files that aren't archive members, relative to the root.</summary>
        public IEnumerable<TrackedFile> TreeFiles()
        {
            foreach (string name in IsDsRom ? DsRomRoots : LegacyRoots)
            {
                string path = Path.Combine(Root, name);
                if (File.Exists(path)) yield return TrackedFile.Tree(name);
                else if (Directory.Exists(path))
                {
                    foreach (string file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                    {
                        string relative = Path.GetRelativePath(Root, file).Replace('\\', '/');
                        // Backups DSPRE leaves beside a file aren't part of the ROM, as archive packing also skips them.
                        if (_archivePaths.Contains(relative) || relative.EndsWith(".dspre.tmp", StringComparison.Ordinal)
                            || relative.EndsWith(DSUtils.BackupSuffix, StringComparison.Ordinal) || relative.EndsWith(".bak", StringComparison.Ordinal)) continue;
                        yield return TrackedFile.Tree(relative);
                    }
                }
            }
        }

        private Narc OpenNarc(DirNames dir)
        {
            if (_narcs.TryGetValue(dir, out Narc open)) return open;
            Narc narc = null;
            if (Archives.TryGetValue(dir, out string relative))
            {
                string path = Path.Combine(Root, relative);
                if (File.Exists(path)) narc = Narc.Open(path);
            }
            _narcs[dir] = narc;
            return narc;
        }

        /// <summary>Drops archives read so far, so the next read gets the files as they are now.</summary>
        public void Forget(DirNames? dir = null)
        {
            _code.Clear();
            foreach (DirNames d in dir is DirNames one ? new[] { one } : _narcs.Keys.ToArray())
            {
                if (_narcs.TryGetValue(d, out Narc narc)) narc?.Free();
                _narcs.Remove(d);
            }
        }
    }
}
