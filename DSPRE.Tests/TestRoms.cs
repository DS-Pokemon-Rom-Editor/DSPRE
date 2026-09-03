using System;
using System.IO;
using System.Text.Json;

namespace DSPRE.Tests
{
    /// <summary>
    /// Where the extracted game projects the tests read live. Everybody keeps them somewhere different,
    /// so this is set per machine rather than written into each test.
    ///
    /// Set them either way round:
    ///   testroms.json, beside DS_Map.sln (git ignores it):
    ///       { "heartGold": "D:\\roms\\HeartGold (USA)_DSPRE_contents", "platinum": "...", "diamond": "..." }
    ///   or environment variables, which win over the file:
    ///       DSPRE_TEST_HEARTGOLD, DSPRE_TEST_PLATINUM, DSPRE_TEST_DIAMOND
    ///
    /// A path that is not set falls back to the layout under DSPRE_TEST_ROMS (default C:\Romhacking\ROMs\NDS),
    /// which is where they sat when these tests were written. Tests that need a project it cannot find skip
    /// themselves and say so, so a machine with only one game still gets a useful run.
    /// </summary>
    internal static class TestRoms
    {
        public static string HeartGold { get; } = Resolve(
            "DSPRE_TEST_HEARTGOLD", "heartGold",
            @"HGSS\HeartGold (USA)_DSPRE_contents");

        public static string Platinum { get; } = Resolve(
            "DSPRE_TEST_PLATINUM", "platinum",
            @"Plat\Pokemon - Platinum Version (USA) (Rev 1)\Pokemon - Platinum Version (USA) (Rev 1)_DSPRE_contents");

        public static string Diamond { get; } = Resolve(
            "DSPRE_TEST_DIAMOND", "diamond",
            @"DP\Pokemon Diamond (v05) (U)(Legacy)\1015 - Pokemon Diamond (v05) (U)(Legacy)_DSPRE_contents");

        /// <summary>The folder the default layout hangs off, for anyone who keeps all three together.</summary>
        private static string Root =>
            Environment.GetEnvironmentVariable("DSPRE_TEST_ROMS") ?? @"C:\Romhacking\ROMs\NDS";

        internal static string Resolve(string variable, string key, string belowRoot)
        {
            string set = Environment.GetEnvironmentVariable(variable);
            if (!string.IsNullOrWhiteSpace(set)) return set.TrimEnd('\\', '/');

            string fromFile = FromConfig(key);
            if (!string.IsNullOrWhiteSpace(fromFile)) return fromFile.TrimEnd('\\', '/');

            return Path.Combine(Root, belowRoot);
        }

        private static JsonElement? _config;

        private static string FromConfig(string key)
        {
            if (_config == null)
            {
                _config = JsonDocument.Parse("{}").RootElement;
                string path = ConfigPath();
                if (path != null)
                {
                    try { _config = JsonDocument.Parse(File.ReadAllText(path)).RootElement.Clone(); }
                    catch (Exception ex) { Console.WriteLine($"testroms.json could not be read: {ex.Message}"); }
                }
            }
            return _config.Value.ValueKind == JsonValueKind.Object
                && _config.Value.TryGetProperty(key, out var value)
                && value.ValueKind == JsonValueKind.String
                    ? value.GetString() : null;
        }

        /// <summary>testroms.json where it was pointed at, or beside the solution.</summary>
        private static string ConfigPath()
        {
            string named = Environment.GetEnvironmentVariable("DSPRE_TEST_ROMS_CONFIG");
            if (!string.IsNullOrWhiteSpace(named)) return File.Exists(named) ? named : null;

            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                string here = Path.Combine(dir.FullName, "testroms.json");
                if (File.Exists(here)) return here;
                if (File.Exists(Path.Combine(dir.FullName, "DS_Map.sln"))) return null;
                dir = dir.Parent;
            }
            return null;
        }
    }
}
