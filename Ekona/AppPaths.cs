using System;
using System.IO;

namespace DSPRE
{
    /// <summary>
    /// Core application data locations — no UI dependencies, safe for the cross-platform core.
    /// (Previously lived on the WinForms <c>Program</c> class, which keeps forwarding properties
    /// so existing call sites compile until they migrate.)
    /// </summary>
    public static class AppPaths
    {
        public static string DspreDataPath { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DSPRE");

        public static string DatabasePath { get; } = Path.Combine(DspreDataPath, "databases");
    }
}
