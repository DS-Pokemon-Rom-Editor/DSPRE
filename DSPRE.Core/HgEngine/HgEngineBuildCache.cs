using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace DSPRE.HgEngine
{
    /// <summary>
    /// The archives hg-engine builds by extracting base/root, replacing some members and packing it again. make
    /// reuses the cached result in build/narc until that rule's own sources change, and copies it over base/root
    /// every build, so an edit DSPRE writes into base/root is lost unless the cached copy is removed first.
    /// </summary>
    public static class HgEngineBuildCache
    {
        private static readonly Regex Target = new(@"^\s*([A-Z0-9_]+)_TARGET\s*:?=\s*\$\(FILESYS\)/(\S+)", RegexOptions.Multiline);
        private static readonly Regex Narc = new(@"^\s*([A-Z0-9_]+)_NARC\s*:?=\s*\$\(BUILD_NARC\)/(\S+)", RegexOptions.Multiline);
        private static readonly Regex Dir = new(@"^\s*([A-Z0-9_]+)_DIR\s*:?=\s*\$\(BUILD\)/([A-Za-z0-9_./-]+)\s*$", RegexOptions.Multiline);

        /// <summary>A partly built archive (as a/0/2/7), the cached file make reuses for it, and the folder its rule extracts into.</summary>
        public record PartialArchive(string Archive, string Cache, string ExtractDir);

        public static List<PartialArchive> PartialArchives(string checkout)
        {
            var result = new List<PartialArchive>();
            string narcsMk = Path.Combine(checkout, HgEngineOwnedFiles.MakeFragmentRelPath);
            if (!File.Exists(narcsMk)) return result;
            string text = File.ReadAllText(narcsMk);
            string buildDir = Path.GetFullPath(Path.Combine(checkout, "build"));

            var caches = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (Match m in Narc.Matches(text)) caches[m.Groups[1].Value] = m.Groups[2].Value;
            var dirs = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (Match m in Dir.Matches(text)) dirs[m.Groups[1].Value] = m.Groups[2].Value;

            foreach (Match m in Target.Matches(text))
            {
                string variable = m.Groups[1].Value;
                if (!caches.TryGetValue(variable, out string cache)) continue;
                var rule = Regex.Match(text, @"extract\s+\$\(" + Regex.Escape(variable) + @"_TARGET\)\s+-o\s+\$\(([A-Z0-9_]+)_DIR\)");
                if (!rule.Success) continue;

                string extractDir = null;
                if (dirs.TryGetValue(rule.Groups[1].Value, out string dir))
                {
                    string full = Path.GetFullPath(Path.Combine(buildDir, dir.Replace('/', Path.DirectorySeparatorChar)));
                    if (full.StartsWith(buildDir + Path.DirectorySeparatorChar, StringComparison.Ordinal)) extractDir = full;
                }
                result.Add(new PartialArchive(m.Groups[2].Value,
                    Path.Combine(buildDir, "narc", cache.Replace('/', Path.DirectorySeparatorChar)), extractDir));
            }
            return result;
        }

        /// <summary>
        /// Removes every partly built archive's cached copy and the members left in its extract folder, so the next
        /// make rebuilds them from base/root alone. The rules extract over whatever an earlier build left there, so a
        /// member that base/root no longer has would otherwise be packed back in.
        /// </summary>
        public static void Invalidate(string checkout)
        {
            foreach (var part in PartialArchives(checkout))
            {
                try
                {
                    if (File.Exists(part.Cache)) File.Delete(part.Cache);
                    if (part.ExtractDir != null && Directory.Exists(part.ExtractDir))
                        foreach (string file in Directory.EnumerateFiles(part.ExtractDir)) File.Delete(file);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    AppLogger.Error($"HgEngineBuildCache: could not clear the cached {part.Archive}: {ex.Message}");
                }
            }
        }
    }
}
