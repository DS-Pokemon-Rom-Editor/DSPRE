using System;
using System.IO;

namespace DSPRE
{
    /// <summary>Comparing and naming paths the same way everywhere: case-insensitive, with / in stored keys.</summary>
    public static class ProjectPaths
    {
        /// <summary><paramref name="path"/> relative to <paramref name="root"/>, with / on every host.</summary>
        public static string RelativeKey(string root, string path) => Path.GetRelativePath(root, path).Replace('\\', '/');

        /// <summary>Whether <paramref name="path"/> is <paramref name="folder"/> or anything under it.</summary>
        public static bool IsInside(string path, string folder)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(folder)) return false;
            return (Full(path) + Path.DirectorySeparatorChar).StartsWith(Full(folder) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Whether two paths name the same file or folder.</summary>
        public static bool Same(string a, string b) =>
            a != null && b != null && string.Equals(Full(a), Full(b), StringComparison.OrdinalIgnoreCase);

        private static string Full(string path) => Path.GetFullPath(path).TrimEnd('\\', '/');
    }
}
