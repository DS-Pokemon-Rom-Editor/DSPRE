using System;
using System.IO;

namespace DSPRE
{
    /// <summary>Where DSPRE keeps its own data, outside any project.</summary>
    public static class AppPaths
    {
        // DSPRE_DATA_DIR lets a host (an automation server, a test run) keep its settings and logs apart from the user's.
        public static string DspreDataPath { get; } = string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DSPRE_DATA_DIR"))
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DSPRE")
            : Path.GetFullPath(Environment.GetEnvironmentVariable("DSPRE_DATA_DIR"));

        public static string DatabasePath { get; } = Path.Combine(DspreDataPath, "databases");
    }
}
