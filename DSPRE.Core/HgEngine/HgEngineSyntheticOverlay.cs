using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace DSPRE.HgEngine
{
    /// <summary>
    /// The synthetic overlay archive both tools keep tables in. hg-engine extracts it once into
    /// build/a028, generates its own members there from data/*.c, and repacks the whole archive from
    /// that directory on every build.
    ///
    /// So the archive in the ROM tree is not where a lasting edit goes: anything DSPRE writes there is
    /// replaced by the next repack. The member files in build/a028 are, and the two tools only collide
    /// if they want the same member.
    /// </summary>
    public static class HgEngineSyntheticOverlay
    {
        public const string BuildDirRelPath = "build/a028";
        public const string CodeTablesRelPath = "data/codetables.mk";

        /// <summary>Where the checkout keeps the members it repacks the archive from.</summary>
        public static string BuildDir =>
            HgEngineProject.IsActive && HgEngineProject.RepoRootWindows != null
                ? Path.Combine(HgEngineProject.RepoRootWindows,
                    BuildDirRelPath.Replace('/', Path.DirectorySeparatorChar))
                : null;

        /// <summary>Every member file present, by its name on disk.</summary>
        public static IReadOnlyList<string> Members() => MembersIn(BuildDir);

        internal static IReadOnlyList<string> MembersIn(string buildDir)
        {
            if (buildDir == null || !Directory.Exists(buildDir)) return Array.Empty<string>();
            try
            {
                return Directory.EnumerateFiles(buildDir)
                    .Select(Path.GetFileName)
                    .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch (Exception ex)
            {
                AppLogger.Error("HgEngineSyntheticOverlay.MembersIn: " + ex.Message);
                return Array.Empty<string>();
            }
        }

        /// <summary>
        /// The members the checkout generates itself, read from its own code-table rules rather than
        /// from what happens to be on disk, so an unbuilt checkout still answers.
        /// </summary>
        public static IReadOnlyList<string> GeneratedMembers() =>
            GeneratedMembersAt(HgEngineProject.IsActive ? HgEngineProject.RepoRootWindows : null);

        internal static IReadOnlyList<string> GeneratedMembersAt(string root)
        {
            if (root == null) return Array.Empty<string>();

            string path = Path.Combine(root, CodeTablesRelPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path)) return Array.Empty<string>();

            try
            {
                var found = new List<string>();
                foreach (Match m in Regex.Matches(File.ReadAllText(path), @"a028/(\S+)"))
                {
                    string name = m.Groups[1].Value.Trim();
                    if (name.Length > 0 && !found.Contains(name)) found.Add(name);
                }
                found.Sort(StringComparer.OrdinalIgnoreCase);
                return found;
            }
            catch (Exception ex)
            {
                AppLogger.Error("HgEngineSyntheticOverlay.GeneratedMembersAt: " + ex.Message);
                return Array.Empty<string>();
            }
        }

        /// <summary>
        /// Whether the checkout generates the member DSPRE expands into. False means the two only
        /// disagree about where the edit lives, not about which member it is.
        /// </summary>
        public static bool GeneratesMemberFor(uint dspreMemberId) =>
            GeneratedMembers().Any(name => MemberIndexOf(name) == dspreMemberId);

        /// <summary>
        /// The archive index a member file name stands for. narchive writes them as group_index, so the
        /// trailing number is the index and the prefix only says which group it came out of.
        /// </summary>
        internal static int MemberIndexOf(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return -1;
            Match m = Regex.Match(fileName, @"_(\d+)$");
            return m.Success && int.TryParse(m.Groups[1].Value, out int index) ? index : -1;
        }

        /// <summary>
        /// Why an expansion into the synthetic overlay will not survive, or null when it will. The
        /// archive in the ROM tree is repacked from build/a028 on every build, so the edit has to be
        /// made there to last.
        /// </summary>
        public static string RefusalFor(uint dspreMemberId)
        {
            if (!HgEngineProject.IsActive) return null;

            string member = Members().FirstOrDefault(n => MemberIndexOf(n) == dspreMemberId);
            string named = member ?? $"member {dspreMemberId}";

            if (GeneratesMemberFor(dspreMemberId))
            {
                return $"hg-engine generates {named} of the synthetic overlay from its own data, so an "
                     + "expansion here is replaced on the next compile.";
            }

            return "hg-engine repacks the synthetic overlay from its own build directory on every "
                 + $"compile, so an expansion written into the ROM's copy is lost. It keeps {named} in "
                 + BuildDirRelPath + ".";
        }
    }
}
