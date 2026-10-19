using System;
using System.IO;

namespace DSPRE
{
    /// <summary>Where DSPRE keeps its own data, outside any project.</summary>
    public static class AppPaths
    {
        public static string DspreDataPath { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DSPRE");

        public static string DatabasePath { get; } = Path.Combine(DspreDataPath, "databases");
    }
}
