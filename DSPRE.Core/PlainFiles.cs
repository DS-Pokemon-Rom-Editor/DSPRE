using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using DSPRE.HgEngine;
using static DSPRE.RomInfo;

namespace DSPRE
{
    /// <summary>
    /// Reads and writes files that are neither archive members nor code: exports, DSPRE's own data,
    /// project sources, backups and temp files. Writing a member, the project's arm9 or an overlay
    /// is refused, since those go through <see cref="ArchiveMembers"/> and
    /// <see cref="DSUtils.WriteWholeFile"/>.
    /// </summary>
    public static class PlainFiles
    {
        public static bool Exists(string path) => File.Exists(path);

        public static bool FolderExists(string path) => Directory.Exists(path);

        public static byte[] ReadBytes(string path) => File.ReadAllBytes(path);

        public static byte[] ReadBytesOrNull(string path) => File.Exists(path) ? File.ReadAllBytes(path) : null;

        public static FileStream OpenRead(string path) => File.OpenRead(path);

        public static long Length(string path) => new FileInfo(path).Length;

        public static DateTime? WrittenAt(string path) => File.Exists(path) ? File.GetLastWriteTimeUtc(path) : null;

        public static void Touch(string path, DateTime utc) => File.SetLastWriteTimeUtc(path, utc);

        public static FileStream Create(string path)
        {
            Refuse(path);
            return File.Create(path);
        }

        public static void WriteBytes(string path, byte[] data)
        {
            Refuse(path);
            File.WriteAllBytes(path, data);
            ProjectWrites.Raise(path);
        }

        public static void WriteText(string path, string text, Encoding encoding = null)
        {
            Refuse(path);
            if (encoding == null) File.WriteAllText(path, text);
            else File.WriteAllText(path, text, encoding);
            ProjectWrites.Raise(path);
        }

        private static readonly JsonSerializerOptions Indented = new JsonSerializerOptions { WriteIndented = true };

        /// <summary>Writes <paramref name="value"/> as JSON, indented unless other options are given.</summary>
        public static void WriteJson<T>(string path, T value, JsonSerializerOptions options = null) =>
            WriteText(path, JsonSerializer.Serialize(value, options ?? Indented));

        /// <summary>Reads a JSON file into <typeparamref name="T"/>. Throws as reading and parsing do.</summary>
        public static T ReadJson<T>(string path, JsonSerializerOptions options = null) =>
            JsonSerializer.Deserialize<T>(File.ReadAllText(path), options);

        public static void WriteLines(string path, IEnumerable<string> lines)
        {
            Refuse(path);
            File.WriteAllLines(path, lines);
            ProjectWrites.Raise(path);
        }

        public static void Copy(string from, string to, bool overwrite = false)
        {
            Refuse(to);
            File.Copy(from, to, overwrite);
            ProjectWrites.Raise(to);
        }

        public static void Move(string from, string to, bool overwrite = false)
        {
            Refuse(from);
            Refuse(to);
            File.Move(from, to, overwrite);
            ProjectWrites.Raise(from);
            ProjectWrites.Raise(to);
        }

        public static bool IsTaken(string path) =>
            File.Exists(path) || (Directory.Exists(path) && Directory.EnumerateFileSystemEntries(path).Any());

        public static void Remove(string path)
        {
            Refuse(path);
            if (File.Exists(path)) File.Delete(path);
            else if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
            ProjectWrites.Raise(path);
        }

        private static void Refuse(string path)
        {
            if (ArchiveMembers.TryMemberAt(path, out DirNames dir, out int id))
                throw new IOException($"Member {id} of {dir} is written through ArchiveMembers, not as a plain file.");
            if (IsProjectCode(path))
                throw new IOException($"{Path.GetFileName(path)} is code and is written through DSUtils.WriteWholeFile.");
        }

        private static bool IsProjectCode(string path)
        {
            return ProjectPaths.IsInside(path, WorkDir) && HgEngineWriteGuard.TryIdentify(Path.GetFullPath(path), out int _);
        }
    }
}
