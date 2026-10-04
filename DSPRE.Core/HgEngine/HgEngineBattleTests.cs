using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace DSPRE.HgEngine
{
    /// <summary>
    /// hg-engine's battle test scenarios: one BEGIN_TEST { ... } END_TEST block per .c file under data/battle_tests,
    /// gathered into BattleTests[] when the checkout is built with AUTO_TEST=Y (scripts/build_tests.py).
    /// </summary>
    public static class HgEngineBattleTests
    {
        public const string RelDir = "data/battle_tests";

        private static string Root => Path.Combine(HgEngineProject.RepoRootWindows, "data", "battle_tests");

        /// <summary>Every test, as a path relative to data/battle_tests with forward slashes, sorted.</summary>
        public static List<string> List()
        {
            if (!HgEngineProject.IsActive || !Directory.Exists(Root)) return new List<string>();
            return Directory.EnumerateFiles(Root, "*.c", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(Root, f).Replace('\\', '/'))
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static string FullPath(string rel)
        {
            string full = Path.GetFullPath(Path.Combine(Root, rel.Replace('/', Path.DirectorySeparatorChar)));
            if (!full.StartsWith(Path.GetFullPath(Root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("That test is outside data/battle_tests.");
            return full;
        }

        public static string Read(string rel) => File.ReadAllText(FullPath(rel));

        /// <summary>Why this text isn't one test the build can gather, or null.</summary>
        public static string Check(string text)
        {
            if (text == null) return "There is no text.";
            string code = Regex.Replace(Regex.Replace(text, @"/\*.*?\*/", "", RegexOptions.Singleline), @"//[^\n]*", "");
            int begin = Regex.Matches(code, @"\bBEGIN_TEST\b").Count, end = Regex.Matches(code, @"\bEND_TEST\b").Count;
            if (begin != 1 || end != 1) return "A test file holds exactly one BEGIN_TEST { ... } END_TEST block.";
            if (!code.Contains("battle_tests.h")) return "A test file includes battle_tests.h, like the others do.";
            int depth = 0;
            foreach (char c in code)
            {
                if (c == '{') depth++;
                else if (c == '}' && --depth < 0) break;
            }
            return depth == 0 ? null : "The braces don't match up.";
        }

        public static string Write(string rel, string text)
        {
            string why = Check(text);
            if (why != null) return why;
            try { File.WriteAllText(FullPath(rel), text.Replace("\r\n", "\n")); return null; }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { return ex.Message; }
        }

        /// <summary>A new test at <paramref name="rel"/>, starting as a copy of <paramref name="text"/>. Null on success.</summary>
        public static string Create(string rel, string text)
        {
            if (string.IsNullOrWhiteSpace(rel) || !rel.EndsWith(".c", StringComparison.OrdinalIgnoreCase))
                return "Name the test like abilities/intimidate/doubles.c.";
            if (!Regex.IsMatch(rel, @"^[A-Za-z0-9_./-]+$") || rel.Contains("..")) return "Use letters, numbers, _ and / in the name.";
            string full;
            try { full = FullPath(rel); } catch (ArgumentException ex) { return ex.Message; }
            if (File.Exists(full)) return "There is already a test with that name.";
            string why = Check(text);
            if (why != null) return why;
            // build_tests.py includes tests by a path two folders down, as battle_tests.h is reached by "../../".
            int depth = rel.Count(c => c == '/');
            string header = string.Concat(Enumerable.Repeat("../", depth)) + "battle_tests.h";
            text = Regex.Replace(text, "#include\\s+\"[./]*battle_tests\\.h\"", "#include \"" + header + "\"");
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(full));
                File.WriteAllText(full, text.Replace("\r\n", "\n"));
                return null;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { return ex.Message; }
        }

        public static string Delete(string rel)
        {
            try
            {
                string full = FullPath(rel);
                File.Delete(full);
                // Leave no empty folders behind, short of data/battle_tests itself.
                for (string dir = Path.GetDirectoryName(full);
                     dir != null && Path.GetFullPath(dir) != Path.GetFullPath(Root) && !Directory.EnumerateFileSystemEntries(dir).Any();
                     dir = Path.GetDirectoryName(dir))
                    Directory.Delete(dir);
                return null;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException) { return ex.Message; }
        }
    }
}
