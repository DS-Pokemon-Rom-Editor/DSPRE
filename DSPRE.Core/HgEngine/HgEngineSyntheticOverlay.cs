using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace DSPRE.HgEngine
{
    /// <summary>
    /// The synthetic overlay archive DSPRE expands ARM9 into. hg-engine extracts it once into build/a028,
    /// generates its own members there from data/*.c, and repacks the whole archive from that directory on
    /// every build. Its ARM9 hook loads overlay 129 instead of DSPRE's member, and its overlay 131 sits at
    /// the address DSPRE's expansion is linked for.
    /// </summary>
    public static class HgEngineSyntheticOverlay
    {
        public const string BuildDirRelPath = "build/a028";
        public const string CodeTablesRelPath = "data/codetables.mk";

        /// <summary>Why the Patch Toolbox lists an expansion patch as unavailable.</summary>
        public const string ToolboxReason = "hg-engine uses this space for its own code";

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
        /// Why DSPRE must not expand into the synthetic overlay, or null when it may. On any hg-engine ROM,
        /// linked or not, the game never loads the expansion, and code that branches to its address runs
        /// hg-engine's overlay 131 instead.
        /// </summary>
        public static string ExpansionRefusal() => RomInfo.isHGE
            ? "hg-engine loads its own code where DSPRE's ARM9 expansion would go, so the game never reads the "
              + "expansion, and anything pointing into it would run hg-engine's code instead."
            : null;
    }
}
