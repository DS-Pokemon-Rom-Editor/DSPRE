using System.IO;

namespace DSPRE
{
    /// <summary>The backups folder inside a project's DSPRE folder, shared by every writer that keeps copies.</summary>
    public static class ProjectBackups
    {
        /// <summary>The backups folder, or one kind's folder inside it, created when missing.</summary>
        public static string Folder(string dspreDir, string kind = null)
        {
            string folder = kind == null ? Path.Combine(dspreDir, "backups") : Path.Combine(dspreDir, "backups", kind);
            Directory.CreateDirectory(folder);
            return folder;
        }

        /// <summary>A project path made into one file name.</summary>
        public static string FlatName(string relative) => relative.Replace('\\', '_').Replace('/', '_');
    }
}
