using System;
using System.IO;
using System.Runtime.InteropServices;

namespace DSPRE
{
    /// <summary>Windows paths into a WSL distro: \\wsl.localhost\..., \\wsl$\..., or a drive letter mapped to either.</summary>
    public static partial class WslPaths
    {
        /// <summary>True for a path inside WSL.</summary>
        public static bool IsWslPath(string path) => TryParse(path, out _, out _);

        /// <summary>Splits a WSL path into the distro name and its Linux path.</summary>
        public static bool TryParse(string path, out string distro, out string posixPath)
        {
            distro = null; posixPath = null;
            if (string.IsNullOrWhiteSpace(path)) return false;
            path = MappedDriveTarget(path) ?? path;
            foreach (string prefix in new[] { @"\\wsl.localhost\", @"\\wsl$\" })
            {
                if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                string rest = path.Substring(prefix.Length).TrimEnd('\\');
                int sep = rest.IndexOf('\\');
                if (sep < 0) { distro = rest; posixPath = "/"; return true; }
                distro = rest.Substring(0, sep);
                posixPath = "/" + rest.Substring(sep + 1).Replace('\\', '/');
                return true;
            }
            return false;
        }

        /// <summary>Resolves a mapped drive letter to its network path, or null when the drive isn't mapped.</summary>
        private static string MappedDriveTarget(string path)
        {
            if (!OperatingSystem.IsWindows() || path.Length < 2 || path[1] != ':' || !char.IsLetter(path[0])) return null;
            try
            {
                string drive = path.Substring(0, 2);
                if (new DriveInfo(drive).DriveType != DriveType.Network) return null;
                char[] buffer = new char[1024];
                int length = buffer.Length;
                if (WNetGetConnection(drive, buffer, ref length) != 0) return null;
                int end = Array.IndexOf(buffer, '\0');
                string remote = new string(buffer, 0, end < 0 ? buffer.Length : end).TrimEnd('\\');
                string rest = path.Substring(2).Replace('/', '\\').TrimStart('\\');
                return rest.Length == 0 ? remote : remote + "\\" + rest;
            }
            catch (Exception ex)
            {
                AppLogger.WarnOnce("WslPaths.MappedDriveTarget", ex);
                return null;
            }
        }

        [LibraryImport("mpr.dll", EntryPoint = "WNetGetConnectionW", StringMarshalling = StringMarshalling.Utf16)]
        private static partial int WNetGetConnection(string localName, [Out] char[] remoteName, ref int length);
    }
}
