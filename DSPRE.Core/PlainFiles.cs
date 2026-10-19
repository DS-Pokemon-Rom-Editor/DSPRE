using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DSPRE.HgEngine;
using static DSPRE.RomInfo;

namespace DSPRE
{
    /// <summary>
    /// Writes files that are neither archive members nor code: exports, DSPRE's own data, project sources, backups and
    /// temp files. A member or the project's arm9 or an overlay is refused, since those go through
    /// <see cref="ArchiveMembers"/> and <see cref="DSUtils.WriteWholeFile"/>.
    /// </summary>
    public static class PlainFiles
    {
        public static void WriteBytes(string path, byte[] data)
        {
            Refuse(path);
            File.WriteAllBytes(path, data);
        }

        public static void WriteText(string path, string text, Encoding encoding = null)
        {
            Refuse(path);
            if (encoding == null) File.WriteAllText(path, text);
            else File.WriteAllText(path, text, encoding);
        }

        public static void WriteLines(string path, IEnumerable<string> lines)
        {
            Refuse(path);
            File.WriteAllLines(path, lines);
        }

        public static void Copy(string from, string to, bool overwrite = false)
        {
            Refuse(to);
            File.Copy(from, to, overwrite);
        }

        public static void Move(string from, string to, bool overwrite = false)
        {
            Refuse(from);
            Refuse(to);
            File.Move(from, to, overwrite);
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
            if (string.IsNullOrEmpty(workDir) || string.IsNullOrEmpty(path)) return false;
            string full = Path.GetFullPath(path);
            string root = Path.GetFullPath(workDir).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
            return full.StartsWith(root, StringComparison.OrdinalIgnoreCase) && HgEngineWriteGuard.TryIdentify(full, out int _);
        }
    }
}
