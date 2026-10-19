using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
            if (begin != end || begin > 1) return "A test file holds one BEGIN_TEST { ... } END_TEST block, or one { ... }, on its own.";
            // The build pastes each file into one array, so what it sees has to be a single { ... },.
            string built = AsBuilt(code).Trim();
            if (!built.StartsWith('{') || !built.EndsWith(',')) return "Built, the test has to read { ... }, so the next test can follow it.";
            int depth = 0;
            foreach (char c in code)
            {
                if (c == '{') depth++;
                else if (c == '}' && --depth < 0) break;
            }
            return depth == 0 ? null : "The braces don't match up.";
        }

        private static string AsBuilt(string code) => Visible(code, endTestAsComma: true);

        /// <summary>
        /// The test as BattleTests.c sees it, same length as the text so offsets still match: comments, directives and
        /// the GET_TEST_CASE_ONLY #ifndef sections (that macro is defined there) become spaces, BEGIN_TEST is empty and
        /// END_TEST, as it expands to a comma there, becomes one when asked.
        /// </summary>
        public static string Visible(string text, bool endTestAsComma = false)
        {
            string code = BlankComments(text);
            StringBuilder sb = new StringBuilder(code.Length);
            // Each open #if: whether it tests GET_TEST_CASE_ONLY, and whether its branch is dropped.
            Stack<(bool Guard, bool Dropped)> open = new Stack<(bool, bool)>();
            string[] lines = code.Split('\n');
            for (int n = 0; n < lines.Length; n++)
            {
                string line = lines[n];
                bool dropped = open.Any(o => o.Dropped);
                Match d = Regex.Match(line.Trim(), @"^#\s*(ifndef|ifdef|if|else|elif|endif)\b\s*(\w*)");
                if (d.Success)
                {
                    string kind = d.Groups[1].Value;
                    bool guard = d.Groups[2].Value == "GET_TEST_CASE_ONLY";
                    if (kind == "ifndef" || kind == "ifdef") open.Push((guard, guard && kind == "ifndef"));
                    else if (kind == "if") open.Push((false, false));
                    else if (open.Count > 0 && (kind == "else" || kind == "elif")) { (bool g, bool was) = open.Pop(); open.Push((g, g && !was)); }
                    else if (kind == "endif" && open.Count > 0) open.Pop();
                    dropped = true;
                }
                else if (line.TrimStart().StartsWith("#")) dropped = true;
                sb.Append(dropped ? new string(' ', line.Length) : line);
                if (n < lines.Length - 1) sb.Append('\n');
            }
            string visible = Regex.Replace(sb.ToString(), @"\bBEGIN_TEST\b", m => new string(' ', m.Length));
            return Regex.Replace(visible, @"\bEND_TEST\b", m => (endTestAsComma ? "," : " ") + new string(' ', m.Length - 1));
        }

        /// <summary>A spot in a test's text and what is wrong there.</summary>
        public sealed record Problem(int Offset, int Length, string Message);

        private static readonly Regex Include = new(@"#\s*include\s+""([^""]+)""");
        private static readonly Regex ConstantName = new(@"\b[A-Z][A-Z0-9]*_[A-Z0-9_]*\b");

        // Defined by the generated BattleTests.c that gathers every test, not by a header.
        private static readonly string[] BuildDefined = { "GET_TEST_CASE_ONLY" };

        /// <summary>
        /// What a build would trip over in a test at <paramref name="rel"/>: includes that aren't there from its folder,
        /// braces, and constant names that none of the headers it includes declare.
        /// </summary>
        public static List<Problem> Problems(string rel, string text)
        {
            List<Problem> found = new List<Problem>();
            if (string.IsNullOrEmpty(text) || rel == null) return found;
            string code = BlankComments(text);
            string folder = Path.GetDirectoryName(FullPath(rel));

            HashSet<string> declared = new HashSet<string>(BuildDefined, StringComparer.Ordinal);
            foreach (string header in BuildHeaders()) declared.UnionWith(DeclaredIn(header));
            foreach (Match inc in Include.Matches(code))
            {
                string wanted = inc.Groups[1].Value;
                string path = Path.GetFullPath(Path.Combine(folder, wanted.Replace('/', Path.DirectorySeparatorChar)));
                if (!File.Exists(path))
                {
                    string fix = Path.GetFileName(wanted) == "battle_tests.h"
                        ? $" From this folder it is \"{Ups(rel)}battle_tests.h\"."
                        : wanted.Contains("include/") ? $" From this folder it is \"{Ups(rel)}../../{wanted[wanted.IndexOf("include/")..]}\"." : "";
                    found.Add(new Problem(inc.Groups[1].Index, inc.Groups[1].Length, $"\"{wanted}\" isn't there from this folder.{fix}"));
                    continue;
                }
                declared.UnionWith(DeclaredIn(path));
            }
            if (Check(text) is string shape && !shape.StartsWith("The braces"))
                found.Add(new Problem(0, Math.Max(1, text.IndexOf('\n') < 0 ? text.Length : text.IndexOf('\n')), shape));

            Stack<int> open = new Stack<int>();
            for (int i = 0; i < code.Length; i++)
            {
                if (code[i] == '{') open.Push(i);
                else if (code[i] == '}')
                {
                    if (open.Count == 0) found.Add(new Problem(i, 1, "This } closes nothing."));
                    else open.Pop();
                }
            }
            foreach (int at in open) found.Add(new Problem(at, 1, "This { is never closed."));

            string bare = BlankComments(text, keepStrings: false);
            if (declared.Count > BuildDefined.Length)
                foreach (Match m in ConstantName.Matches(bare))
                    if (!declared.Contains(m.Value) && !IsInsideInclude(bare, m.Index))
                        found.Add(new Problem(m.Index, m.Length, $"{m.Value} is not defined in the headers this test includes."));
            return found;
        }

        // Comments, and strings unless kept, become spaces so offsets still match the text.
        private static string BlankComments(string text, bool keepStrings = true) =>
            Regex.Replace(text, @"/\*.*?\*/|//[^\n]*|""(?:[^""\\\n]|\\.)*""",
                m => keepStrings && m.Value.StartsWith("\"") ? m.Value : Regex.Replace(m.Value, @"[^\n]", " "), RegexOptions.Singleline);

        private static bool IsInsideInclude(string code, int at)
        {
            int lineStart = code.LastIndexOf('\n', Math.Max(0, at - 1)) + 1;
            return code.AsSpan(lineStart).TrimStart().StartsWith("#include");
        }

        /// <summary>The names starting with <paramref name="prefix"/> a test at <paramref name="rel"/> can use, sorted.</summary>
        public static List<string> Names(string rel, string prefix)
        {
            HashSet<string> declared = new HashSet<string>(StringComparer.Ordinal);
            foreach (string header in BuildHeaders()) declared.UnionWith(DeclaredIn(header));
            return declared.Where(n => n.StartsWith(prefix, StringComparison.Ordinal)).OrderBy(n => n, StringComparer.Ordinal).ToList();
        }

        /// <summary>The headers build_tests.py puts above every test, read from the script so they follow the checkout.</summary>
        private static IEnumerable<string> BuildHeaders()
        {
            string script = Path.Combine(HgEngineProject.RepoRootWindows ?? "", "scripts", "build_tests.py");
            if (!File.Exists(script)) yield break;
            // BattleTests.c is written to build/battle_tests, so its "../../" is the checkout root.
            string from = Path.Combine(HgEngineProject.RepoRootWindows, "build", "battle_tests");
            foreach (Match inc in Include.Matches(HgEngineFileCache.GetText(script) ?? ""))
                yield return Path.GetFullPath(Path.Combine(from, inc.Groups[1].Value.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static string Ups(string rel) => string.Concat(Enumerable.Repeat("../", rel.Count(c => c == '/')));

        private static readonly Dictionary<string, HashSet<string>> _declaredIn = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Every #define and enum member reachable from a header through its quoted includes, in any #if branch.</summary>
        private static HashSet<string> DeclaredIn(string start)
        {
            if (_declaredIn.TryGetValue(start, out HashSet<string> known)) return known;
            HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Stack<string> pending = new Stack<string>();
            pending.Push(start);
            while (pending.Count > 0)
            {
                string path = pending.Pop();
                if (!seen.Add(path) || !File.Exists(path)) continue;
                string header = BlankComments(HgEngineFileCache.GetText(path) ?? "");
                foreach (Match m in Regex.Matches(header, @"#\s*define[ \t]+([A-Za-z_]\w*)")) names.Add(m.Groups[1].Value);
                foreach (Match body in Regex.Matches(header, @"\benum\b[^{;]*\{([^}]*)\}"))
                    foreach (Match member in Regex.Matches(body.Groups[1].Value, @"(?:^|,)\s*([A-Za-z_]\w*)"))
                        names.Add(member.Groups[1].Value);
                foreach (Match inc in Include.Matches(header))
                    pending.Push(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path), inc.Groups[1].Value.Replace('/', Path.DirectorySeparatorChar))));
            }
            _declaredIn[start] = names;
            return names;
        }

        public static string Write(string rel, string text)
        {
            try { return HgEngineVerifiedWrite.TryWrite(FullPath(rel), rel, text.Replace("\r\n", "\n"), Check, out string why) ? null : why; }
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
            // Includes are relative to the test's folder, so a copy at another depth needs its own number of "../".
            text = Regex.Replace(text, "#include\\s+\"(?:\\.\\./)*battle_tests\\.h\"", "#include \"" + Ups(rel) + "battle_tests.h\"");
            text = Regex.Replace(text, "#include\\s+\"(?:\\.\\./)+include/", "#include \"" + Ups(rel) + "../../include/");
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(full));
                return HgEngineVerifiedWrite.TryWrite(full, rel, text.Replace("\r\n", "\n"), Check, out string error) ? null : error;
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
