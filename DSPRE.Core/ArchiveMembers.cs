using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE.HgEngine;
using static DSPRE.RomInfo;

namespace DSPRE
{
    /// <summary>
    /// The one way an archive member reaches the disk. An hg-engine checkout's copy of the member is written too,
    /// since its build would otherwise put the old one back, and <see cref="Written"/> lets the app rebuild the PNGs
    /// hg-engine makes graphics from and tell open editors.
    /// </summary>
    public static class ArchiveMembers
    {
        /// <summary>The archive, the member, the bytes it held before (null when new) and now (null when deleted).</summary>
        public static event Action<DirNames, int, byte[], byte[]> Written;

        public static string PathOf(DirNames dir, int id) => Path.Combine(gameDirs[dir].unpackedDir, id.ToString("D4"));

        public static void Put(DirNames dir, int id, byte[] data)
        {
            string path = PathOf(dir, id);
            HgEngineSourceAssets.WriteVerbatim(HgEngineOwnedFiles.ArchiveOf(dir), id, data);
            byte[] before = File.Exists(path) ? File.ReadAllBytes(path) : null;
            File.WriteAllBytes(path, data);
            Written?.Invoke(dir, id, before, data);
        }

        public static void Delete(DirNames dir, int id)
        {
            string path = PathOf(dir, id);
            if (!File.Exists(path)) return;
            byte[] before = File.ReadAllBytes(path);
            File.Delete(path);
            Written?.Invoke(dir, id, before, null);
        }

        /// <summary>
        /// Moves members <paramref name="from"/> and up one place, leaving <paramref name="from"/> free. Only for DSPRE's
        /// copy following a checkout that already inserted there, so the checkout and listeners are not told.
        /// </summary>
        public static void ShiftUpFollowingCheckout(DirNames dir, int from)
        {
            string folder = gameDirs[dir].unpackedDir;
            int last = Directory.GetFiles(folder).Select(Path.GetFileName).Select(n => int.TryParse(n, out int i) ? i : -1).Max();
            for (int i = last; i >= from; i--) File.Move(PathOf(dir, i), PathOf(dir, i + 1));
        }

        /// <summary>
        /// Replaces every member of an unpacked archive with <paramref name="members"/>, built beside it and swapped in
        /// whole so a failure leaves it as it was. For DSPRE's copy taking a checkout build or dropping stale members;
        /// the checkout and listeners are not told. Files that are not numbered members are dropped.
        /// </summary>
        public static void ReplaceAll(string unpackedDir, IReadOnlyList<byte[]> members)
        {
            string dir = unpackedDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string building = dir + ".repair", previous = dir + ".old";
            try
            {
                if (Directory.Exists(building)) Directory.Delete(building, true);
                Directory.CreateDirectory(building);
                for (int i = 0; i < members.Count; i++) File.WriteAllBytes(Path.Combine(building, i.ToString("D4")), members[i]);

                if (Directory.Exists(previous)) Directory.Delete(previous, true);
                if (Directory.Exists(dir)) Directory.Move(dir, previous);
                try { Directory.Move(building, dir); }
                catch
                {
                    if (Directory.Exists(previous)) Directory.Move(previous, dir);
                    throw;
                }
                if (Directory.Exists(previous)) Directory.Delete(previous, true);
            }
            finally
            {
                if (Directory.Exists(building)) Directory.Delete(building, true);
            }
        }

        /// <summary>The archive member a file in an unpacked archive folder is, if it is one.</summary>
        public static bool TryMemberAt(string path, out DirNames dir, out int id)
        {
            dir = default;
            id = -1;
            if (gameDirs == null || string.IsNullOrEmpty(path)) return false;
            string full = System.IO.Path.GetFullPath(path);
            if (!int.TryParse(System.IO.Path.GetFileName(full), out id)) return false;
            string folder = System.IO.Path.GetDirectoryName(full);
            foreach (KeyValuePair<DirNames, (string packedDir, string unpackedDir)> d in gameDirs)
            {
                if (string.IsNullOrEmpty(d.Value.unpackedDir)) continue;
                if (!string.Equals(System.IO.Path.GetFullPath(d.Value.unpackedDir).TrimEnd('\\', '/'), folder, StringComparison.OrdinalIgnoreCase)) continue;
                dir = d.Key;
                return true;
            }
            id = -1;
            return false;
        }

        /// <summary>Writes a file by its path; an archive member goes through <see cref="Put"/>.</summary>
        public static void Write(string path, byte[] data)
        {
            if (TryMemberAt(path, out DirNames dir, out int id)) Put(dir, id, data);
            else File.WriteAllBytes(path, data);
        }

        /// <summary>Deletes a file by its path; an archive member goes through <see cref="Delete(DirNames, int)"/>.</summary>
        public static void Delete(string path)
        {
            if (TryMemberAt(path, out DirNames dir, out int id)) Delete(dir, id);
            else if (File.Exists(path)) File.Delete(path);
        }

        /// <summary>
        /// Changes several files as one: archive members, arm9 or overlay code and other project files are written
        /// or deleted together, and if any fails every one is put back as it was.
        /// </summary>
        public sealed class Batch
        {
            private sealed class Change
            {
                public string Path;
                public byte[] Bytes;      // null deletes the file
                public DirNames? Dir;
                public int Id;
                public bool Code;
            }

            private readonly List<Change> _changes = new List<Change>();

            public Batch Put(DirNames dir, int id, byte[] data) => Add(new Change { Path = PathOf(dir, id), Bytes = data, Dir = dir, Id = id });
            public Batch Delete(DirNames dir, int id) => Add(new Change { Path = PathOf(dir, id), Dir = dir, Id = id });

            /// <summary>A whole arm9, overlay or synthetic overlay file; the hg-engine write guard is asked first.</summary>
            public Batch PutCode(string path, byte[] data) => Add(new Change { Path = path, Bytes = data ?? throw new ArgumentNullException(nameof(data)), Code = true });

            /// <summary>Any other project file changed in the same step, such as a text bank's JSON or a script's source.</summary>
            public Batch PutFile(string path, byte[] data) => Add(new Change { Path = path, Bytes = data ?? throw new ArgumentNullException(nameof(data)) });
            public Batch DeleteFile(string path) => Add(new Change { Path = path });

            private Batch Add(Change c)
            {
                _changes.Add(c);
                return this;
            }

            public bool TryCommit(out string error)
            {
                error = null;
                if (_changes.Count == 0) { error = "Nothing to write."; return false; }
                if (_changes.Select(c => System.IO.Path.GetFullPath(c.Path)).Distinct(StringComparer.OrdinalIgnoreCase).Count() != _changes.Count)
                {
                    error = "The same file is written twice in one step.";
                    return false;
                }
                foreach (Change c in _changes.Where(c => c.Code))
                {
                    byte[] now = File.Exists(c.Path) ? File.ReadAllBytes(c.Path) : Array.Empty<byte>();
                    int first = 0;
                    while (first < c.Bytes.Length && first < now.Length && c.Bytes[first] == now[first]) first++;
                    int last = c.Bytes.Length - 1;
                    if (now.Length == c.Bytes.Length) while (last > first && c.Bytes[last] == now[last]) last--;
                    if (first < c.Bytes.Length && HgEngineWriteGuard.Refuses(c.Path, first, Math.Max(1, last - first + 1)))
                    {
                        error = $"hg-engine owns part of {System.IO.Path.GetFileName(c.Path)}, so nothing was written.";
                        return false;
                    }
                }

                string token = Guid.NewGuid().ToString("N");
                List<(Change Change, string Stage, bool Existed, byte[] Original, DateTime Time)> staged = new();
                List<string> createdDirectories = new List<string>();
                try
                {
                    foreach (Change c in _changes)
                    {
                        string full = System.IO.Path.GetFullPath(c.Path);
                        string directory = System.IO.Path.GetDirectoryName(full);
                        if (!Directory.Exists(directory))
                        {
                            Directory.CreateDirectory(directory);
                            createdDirectories.Add(directory);
                        }
                        bool existed = File.Exists(full);
                        if (c.Bytes == null && !existed) throw new FileNotFoundException("A file to delete is missing.", full);
                        byte[] original = existed ? File.ReadAllBytes(full) : null;
                        DateTime time = existed ? File.GetLastWriteTimeUtc(full) : default;
                        string stage = c.Bytes == null ? null : full + ".dspre-stage-" + token;
                        staged.Add((c, stage, existed, original, time));
                        if (stage != null) File.WriteAllBytes(stage, c.Bytes);
                    }
                    foreach ((Change c, string stage, bool _, byte[] _, DateTime _) in staged)
                    {
                        string target = System.IO.Path.GetFullPath(c.Path);
                        if (c.Bytes == null) File.Delete(target);
                        else File.Move(stage, target, overwrite: true);
                    }
                }
                catch (Exception ex)
                {
                    error = $"The change failed and was rolled back: {ex.Message}";
                    foreach ((Change c, string _, bool existed, byte[] original, DateTime time) in staged)
                    {
                        string target = System.IO.Path.GetFullPath(c.Path);
                        try
                        {
                            if (existed)
                            {
                                File.WriteAllBytes(target, original);
                                File.SetLastWriteTimeUtc(target, time);
                            }
                            else if (File.Exists(target)) File.Delete(target);
                        }
                        catch (Exception rollback)
                        {
                            error += $" Putting {System.IO.Path.GetFileName(target)} back also failed: {rollback.Message}";
                        }
                    }
                    return false;
                }
                finally
                {
                    foreach ((Change _, string stage, bool _, byte[] _, DateTime _) in staged)
                        if (!string.IsNullOrEmpty(stage) && File.Exists(stage)) File.Delete(stage);
                    for (int i = createdDirectories.Count - 1; i >= 0; i--)
                        if (Directory.Exists(createdDirectories[i]) && !Directory.EnumerateFileSystemEntries(createdDirectories[i]).Any())
                            Directory.Delete(createdDirectories[i]);
                }

                foreach ((Change c, string _, bool _, byte[] original, DateTime _) in staged)
                {
                    if (c.Dir is not DirNames dir) continue;
                    if (c.Bytes != null) HgEngineSourceAssets.WriteVerbatim(HgEngineOwnedFiles.ArchiveOf(dir), c.Id, c.Bytes);
                    Written?.Invoke(dir, c.Id, original, c.Bytes);
                }
                return true;
            }
        }
    }
}
