using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

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
            List<CToken> tokens = CLexer.Tokenize(text);
            int begin = tokens.Count(t => t.Is(text, "BEGIN_TEST")), end = tokens.Count(t => t.Is(text, "END_TEST"));
            if (begin != end || begin > 1) return "A test file holds one BEGIN_TEST { ... } END_TEST block, or one { ... }, on its own.";
            // The build pastes each file into one array, so what it sees has to be a single { ... },.
            string built = AsBuilt(text).Trim();
            if (!built.StartsWith('{') || !built.EndsWith(',')) return "Once built, the test must come out as { ... }, so the next test can follow it.";
            int depth = 0;
            foreach (CToken token in tokens)
            {
                if (token.Kind != CTokenKind.Punct) continue;
                if (token.Is(text, "{")) depth++;
                else if (token.Is(text, "}") && --depth < 0) break;
            }
            return depth == 0 ? null : "The braces don't match up.";
        }

        private static string AsBuilt(string code) => Visible(code, endTestAsComma: true);

        /// <summary>
        /// The test as BattleTests.c compiles it, kept the same length so offsets match: comments, directives and
        /// GET_TEST_CASE_ONLY sections become spaces, BEGIN_TEST is empty and END_TEST becomes a comma when asked.
        /// </summary>
        public static string Visible(string text, bool endTestAsComma = false)
        {
            string code = BlankComments(text);
            StringBuilder sb = new StringBuilder(code);
            void Blank(int start, int end)
            {
                for (int i = start; i < end; i++)
                    if (sb[i] != '\n' && sb[i] != '\r') sb[i] = ' ';
            }

            // Each open #if: whether it tests GET_TEST_CASE_ONLY, and whether its branch is dropped.
            Stack<(bool Guard, bool Dropped)> open = new Stack<(bool, bool)>();
            foreach (CToken token in CLexer.Tokenize(code))
            {
                if (token.Kind == CTokenKind.Directive)
                {
                    CDirective d = CDirective.Parse(code, token);
                    bool guard = d.Name == "GET_TEST_CASE_ONLY";
                    if (d.Keyword == "ifndef" || d.Keyword == "ifdef") open.Push((guard, guard && d.Keyword == "ifndef"));
                    else if (d.Keyword == "if") open.Push((false, false));
                    else if (open.Count > 0 && (d.Keyword == "else" || d.Keyword == "elif")) { (bool g, bool was) = open.Pop(); open.Push((g, g && !was)); }
                    else if (d.Keyword == "endif" && open.Count > 0) open.Pop();
                    Blank(token.Start, token.End);
                }
                else if (open.Any(o => o.Dropped) || token.Is(code, "BEGIN_TEST")) Blank(token.Start, token.End);
                else if (token.Is(code, "END_TEST"))
                {
                    Blank(token.Start, token.End);
                    if (endTestAsComma) sb[token.Start] = ',';
                }
            }
            return sb.ToString();
        }

        /// <summary>A spot in a test's text and what is wrong there.</summary>
        public sealed record Problem(int Offset, int Length, string Message);

        // TYPE_FIRE: capitals, digits and at least one underscore after the first letter.
        private static bool IsConstantName(string name) =>
            name.Length > 1 && char.IsAsciiLetterUpper(name[0]) && name.Contains('_') && name.All(c => char.IsAsciiLetterUpper(c) || char.IsAsciiDigit(c) || c == '_');

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
            foreach ((string wanted, int at) in CSourceFile.For(code).IncludePaths)
            {
                string path = Path.GetFullPath(Path.Combine(folder, wanted.Replace('/', Path.DirectorySeparatorChar)));
                if (!File.Exists(path))
                {
                    string fix = Path.GetFileName(wanted) == "battle_tests.h"
                        ? $" From this folder it is \"{Ups(rel)}battle_tests.h\"."
                        : wanted.Contains("include/") ? $" From this folder it is \"{Ups(rel)}../../{wanted[wanted.IndexOf("include/")..]}\"." : "";
                    found.Add(new Problem(at, wanted.Length, $"\"{wanted}\" isn't there from this folder.{fix}"));
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
                foreach (CToken t in CodeTokens(bare))
                {
                    string name = t.Text(bare);
                    if (t.Kind == CTokenKind.Identifier && IsConstantName(name) && !declared.Contains(name))
                        found.Add(new Problem(t.Start, t.Length, $"{name} isn't defined in the headers this test includes."));
                }
            return found;
        }

        // The tokens of the code and of each directive, leaving out #include paths.
        private static IEnumerable<CToken> CodeTokens(string code)
        {
            foreach (CToken token in CLexer.Tokenize(code))
            {
                if (token.Kind != CTokenKind.Directive) { yield return token; continue; }
                if (CDirective.Parse(code, token).Keyword == "include") continue;
                foreach (CToken inner in CLexer.Tokenize(code, token.Start + 1, token.End)) yield return inner;
            }
        }

        // Comments, and strings unless kept, become spaces so offsets still match the text.
        private static string BlankComments(string text, bool keepStrings = true)
        {
            StringBuilder sb = new StringBuilder(text);
            void Blank(CToken t)
            {
                for (int i = t.Start; i < t.End; i++)
                    if (sb[i] != '\n') sb[i] = ' ';
            }
            foreach (CToken comment in CLexer.Comments(text)) Blank(comment);
            if (!keepStrings)
                foreach (CToken token in CLexer.Tokenize(text))
                    if (token.Kind == CTokenKind.String) Blank(token);
            return sb.ToString();
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
            // A Python file, so its C text is read line by line. The script's own "// #include" lines are for disabled tests.
            foreach (string line in (HgEngineFileCache.GetText(script) ?? "").Split('\n'))
            {
                int include = line.IndexOf("#include \"", StringComparison.Ordinal);
                if (include < 0) continue;
                int open = line.IndexOf('"', include), close = line.IndexOf('"', open + 1);
                string header = close > open ? line.Substring(open + 1, close - open - 1) : "";
                if (header.Length > 0 && !header.Contains('{'))
                    yield return Path.GetFullPath(Path.Combine(from, header.Replace('/', Path.DirectorySeparatorChar)));
            }
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
                CSourceFile header = CSourceFile.For(HgEngineFileCache.GetText(path) ?? "");
                foreach (CDirective d in header.Directives)
                    if (d.Keyword == "define" && d.Name != null) names.Add(d.Name);
                names.UnionWith(header.EnumMembers);
                foreach ((string include, int _) in header.IncludePaths)
                    pending.Push(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path), include.Replace('/', Path.DirectorySeparatorChar))));
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
            if (!rel.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '.' or '/' or '-') || rel.Contains("..")) return "Use letters, numbers, _ and / in the name.";
            string full;
            try { full = FullPath(rel); } catch (ArgumentException ex) { return ex.Message; }
            if (File.Exists(full)) return "There is already a test with that name.";
            string why = Check(text);
            if (why != null) return why;
            // Includes are relative to the test's folder, so a copy at another depth needs its own number of "../".
            foreach ((string include, int at) in CSourceFile.For(text).IncludePaths.Reverse().ToList())
            {
                string bare = include;
                while (bare.StartsWith("../", StringComparison.Ordinal)) bare = bare.Substring(3);
                string moved = bare == "battle_tests.h" ? Ups(rel) + bare
                    : bare != include && bare.StartsWith("include/", StringComparison.Ordinal) ? Ups(rel) + "../../" + bare : include;
                text = text.Remove(at, include.Length).Insert(at, moved);
            }
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
