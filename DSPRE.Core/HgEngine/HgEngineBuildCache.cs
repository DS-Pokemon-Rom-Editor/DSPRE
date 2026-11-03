using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DSPRE.HgEngine
{
    /// <summary>
    /// The archives hg-engine builds by extracting base/root, replacing some members and packing it again. make
    /// reuses the cached result in build/narc until that rule's own sources change, and copies it over base/root
    /// every build, so an edit DSPRE writes into base/root is lost unless the cached copy is removed first.
    /// </summary>
    public static class HgEngineBuildCache
    {
        /// <summary>A partly built archive (as a/0/2/7), the cached file make reuses for it, and the folder its rule extracts into.</summary>
        public record PartialArchive(string Archive, string Cache, string ExtractDir);

        public static List<PartialArchive> PartialArchives(string checkout)
        {
            List<PartialArchive> result = new List<PartialArchive>();
            string narcsMk = Path.Combine(checkout, HgEngineOwnedFiles.MakeFragmentRelPath);
            if (!File.Exists(narcsMk)) return result;
            string text = File.ReadAllText(narcsMk);
            string buildDir = Path.GetFullPath(Path.Combine(checkout, "build"));
            HgEngineMakeFile make = HgEngineMakeFile.For(text);
            string[] words = HgEngineMakeFile.Words(text);

            // Only values under the given prefix count, as a single word where there are several assignments.
            static string Under(string value, string prefix) =>
                value.StartsWith(prefix, StringComparison.Ordinal) ? value.Substring(prefix.Length).Split((char[])null, 2)[0] : null;

            Dictionary<string, string> caches = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach ((string stem, string value) in make.Suffixed("_NARC"))
                if (Under(value, "$(BUILD_NARC)/") is string cachedAs) caches[stem] = cachedAs;
            Dictionary<string, string> dirs = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach ((string stem, string value) in make.Suffixed("_DIR"))
                if (value.StartsWith("$(BUILD)/", StringComparison.Ordinal) && Under(value, "$(BUILD)/") is string folder
                    && folder.Length == value.Length - "$(BUILD)/".Length && folder.All(c => HgEngineMakeFile.IsNameChar(c) || c is '.' or '/' or '-'))
                    dirs[stem] = folder;

            foreach ((string variable, string target) in make.Suffixed("_TARGET"))
            {
                string archive = Under(target, "$(FILESYS)/");
                if (archive == null || !caches.TryGetValue(variable, out string cache)) continue;
                int extract = HgEngineMakeFile.RunStart(words, 0, "extract", $"$({variable}_TARGET)", "-o", "$(");
                if (extract < 0) continue;
                string extractInto = HgEngineMakeFile.ReferenceName(words[extract + 3]);
                if (extractInto == null || !extractInto.EndsWith("_DIR", StringComparison.Ordinal)) continue;

                string extractDir = null;
                if (dirs.TryGetValue(extractInto.Substring(0, extractInto.Length - 4), out string dir))
                {
                    string full = Path.GetFullPath(Path.Combine(buildDir, dir.Replace('/', Path.DirectorySeparatorChar)));
                    if (full.StartsWith(buildDir + Path.DirectorySeparatorChar, StringComparison.Ordinal)) extractDir = full;
                }
                result.Add(new PartialArchive(archive,
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
            foreach (PartialArchive part in PartialArchives(checkout))
            {
                try
                {
                    if (File.Exists(part.Cache)) File.Delete(part.Cache);
                    if (part.ExtractDir != null && Directory.Exists(part.ExtractDir))
                        foreach (string file in Directory.EnumerateFiles(part.ExtractDir)) File.Delete(file);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    AppLogger.Error($"HgEngineBuildCache: couldn't clear the cached {part.Archive}: {ex.Message}");
                }
            }
        }
    }
}
