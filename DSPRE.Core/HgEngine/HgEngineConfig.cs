using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DSPRE.HgEngine
{
    /// <summary>
    /// hg-engine's build settings: include/config.h for the C code and armips/include/config.s for the assembler.
    /// A setting is a define that is on, or commented out to be off, with the comment block above it as its
    /// description; some carry a value. A setting both files define is changed in both. Only top-level settings
    /// are offered: lines inside an #if belong to another setting's choice.
    /// </summary>
    public static class HgEngineConfig
    {
        public const string HeaderRelPath = "include/config.h";
        public const string AsmRelPath = "armips/include/config.s";

        public sealed class Setting
        {
            public string Name { get; init; }
            public string File { get; init; }
            public string Description { get; init; }
            /// <summary>False when the line is commented out.</summary>
            public bool Enabled { get; set; }
            /// <summary>The value as written, or null for a plain switch.</summary>
            public string Value { get; set; }
            /// <summary>Whether commenting the line out is how it is turned off (config.s values can't be).</summary>
            public bool CanDisable { get; init; }
            /// <summary>A config.s setting config.h also defines; it follows the config.h one.</summary>
            public bool FollowsHeader { get; internal set; }
            internal int Line { get; init; }
            internal bool WasEnabled { get; init; }
            internal string WasValue { get; init; }
        }

        public static bool TryRead(out List<Setting> settings, out string error)
        {
            settings = new List<Setting>();
            error = null;
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout is linked."; return false; }
            string header = Read(HeaderRelPath);
            if (header == null) { error = $"{HeaderRelPath} is missing from the checkout."; return false; }
            settings.AddRange(ReadHeader(header));
            string asm = Read(AsmRelPath);
            if (asm != null)
            {
                HashSet<string> names = settings.Select(x => x.Name).ToHashSet();
                foreach (Setting a in ReadAsm(asm)) { a.FollowsHeader = names.Contains(a.Name); settings.Add(a); }
            }
            return true;
        }

        private static IEnumerable<Setting> ReadHeader(string text)
        {
            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            List<string> comments = new List<string>();
            int depth = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                string t = lines[i].Trim();
                string keyword = Directive(t)?.Keyword;
                // The include guard is the one #if everything sits in.
                if (keyword is "if" or "ifdef" or "ifndef")
                {
                    if (!(i < 3 && keyword == "ifndef")) depth++;
                    comments.Clear();
                    continue;
                }
                if (keyword == "endif") { if (depth > 0) depth--; comments.Clear(); continue; }
                if (keyword is "else" or "elif") { comments.Clear(); continue; }
                Definition m = HeaderDefinition(lines[i]);
                if (m != null && depth == 0)
                {
                    string name = m.Name;
                    if (name == "CONFIG_H" || !IsSettingName(name)) { comments.Clear(); continue; }
                    bool enabled = !m.CommentedOut;
                    string value = m.Value;
                    yield return new Setting
                    {
                        Name = name, File = HeaderRelPath, Description = string.Join(" ", comments), Line = i,
                        Enabled = enabled, WasEnabled = enabled, Value = value, WasValue = value, CanDisable = true,
                    };
                    comments.Clear();
                    continue;
                }
                if (t.StartsWith("//", StringComparison.Ordinal)) comments.Add(t.TrimStart('/').Trim());
                else if (t.Length == 0) comments.Clear();
            }
        }

        private static IEnumerable<Setting> ReadAsm(string text)
        {
            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            List<string> comments = new List<string>();
            for (int i = 0; i < lines.Length; i++)
            {
                string t = lines[i].Trim();
                Definition m = AsmDefinition(lines[i]);
                if (m != null && IsSettingName(m.Name))
                {
                    bool enabled = !m.CommentedOut;
                    // A label is switched by being there; an equ's value is the setting.
                    yield return new Setting
                    {
                        Name = m.Name, File = AsmRelPath, Description = string.Join(" ", comments), Line = i,
                        Enabled = enabled, WasEnabled = enabled, CanDisable = m.IsLabel,
                        Value = m.IsLabel ? null : m.Value, WasValue = m.IsLabel ? null : m.Value,
                    };
                    comments.Clear();
                    continue;
                }
                if (t.StartsWith("//", StringComparison.Ordinal)) comments.Add(t.TrimStart('/').Trim());
                else if (t.Length == 0) comments.Clear();
            }
        }

        /// <summary>
        /// Writes the settings that changed. A config.h setting config.s also defines takes the same state there:
        /// its value for an equ, on or off for a label.
        /// </summary>
        public static bool TryWrite(IReadOnlyList<Setting> settings, out string error)
        {
            error = null;
            List<Setting> changed = settings.Where(s => s.Enabled != s.WasEnabled || s.Value != s.WasValue).ToList();
            if (changed.Count == 0) return true;
            if (changed.Any(s => s.Value != null && s.Value.Trim().Length == 0)) { error = "A setting's value can't be empty."; return false; }

            Dictionary<string, string[]> files = new Dictionary<string, string[]>();
            string[] LinesOf(string rel) => files.TryGetValue(rel, out string[] l) ? l : files[rel] = (Read(rel) ?? "").Replace("\r\n", "\n").Split('\n');

            foreach (Setting s in changed)
            {
                string[] lines = LinesOf(s.File);
                int line = FindLine(lines, s);
                if (line < 0) { error = $"{s.Name} changed in {s.File} since it was read. Reopen the settings."; return false; }
                lines[line] = Rewrite(lines[line], s.File, s.Enabled, s.Value);
                if (s.File != HeaderRelPath) continue;
                // The assembler's copy of the same setting.
                string[] asm = LinesOf(AsmRelPath);
                for (int i = 0; i < asm.Length; i++)
                {
                    if (AsmDefinition(asm[i]) is not Definition copy || copy.Name != s.Name) continue;
                    if (!copy.IsLabel && s.Value != null) asm[i] = Rewrite(asm[i], AsmRelPath, true, s.Value);
                    else if (copy.IsLabel) asm[i] = Rewrite(asm[i], AsmRelPath, s.Enabled, null);
                }
            }
            try
            {
                // Commenting a setting out is the edit, so a define turning into a comment is not a lost comment.
                foreach ((string rel, string[] lines) in files)
                    HgEngineFileCache.WriteText(Path.Combine(HgEngineProject.RepoPathUnc, rel.Replace('/', Path.DirectorySeparatorChar)), string.Join("\n", lines), keepLostComments: false);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { error = ex.Message; return false; }
            return true;
        }

        /// <summary>Returns the setting's current line, or -1 if it's gone, appears twice or has a new value. Lines move
        /// when the file is edited elsewhere, so the line read earlier is only a starting point.</summary>
        private static int FindLine(string[] lines, Setting s)
        {
            bool Holds(int i) => i >= 0 && i < lines.Length && DefinitionOf(lines[i], s.File)?.Name == s.Name;
            int line = Holds(s.Line) ? s.Line : -1;
            if (line < 0)
            {
                int[] found = Enumerable.Range(0, lines.Length).Where(Holds).ToArray();
                if (found.Length != 1) return -1;
                line = found[0];
            }
            Definition m = DefinitionOf(lines[line], s.File);
            string value = s.WasValue != null ? m.Value : null;
            return !m.CommentedOut == s.WasEnabled && value == s.WasValue ? line : -1;
        }

        /// <summary>A setting's line. Marker is the "//" and the space after it when the line is commented out.</summary>
        private sealed record Definition(string Name, int MarkerStart, int MarkerLength, string Value, int ValueStart, int ValueLength, bool IsLabel = false)
        {
            public bool CommentedOut => MarkerLength > 0;
        }

        private static bool IsSettingName(string name) =>
            char.IsAsciiLetterUpper(name[0]) && name.All(c => char.IsAsciiLetterUpper(c) || char.IsAsciiDigit(c) || c == '_');

        private static CDirective Directive(string line) =>
            CLexer.Tokenize(line) is [{ Kind: CTokenKind.Directive } token, ..] ? CDirective.Parse(line, token) : null;

        // A commented-out #define is a setting that is off, so the "//" is read here before the lexer drops it.
        private static Definition HeaderDefinition(string line)
        {
            int code = CodeStart(line, out int indent);
            CDirective define = Directive(line.Substring(code));
            if (define?.Keyword != "define" || define.Name == null || define.Parameters != null) return null;
            bool hasValue = define.Body.Length > 0;
            return new Definition(define.Name, indent, code - indent, hasValue ? define.Body : null,
                code + define.BodyStart, hasValue ? define.BodyEnd - define.BodyStart : 0);
        }

        // Where a line's text starts, past its indent and any "//" that comments it out.
        private static int CodeStart(string line, out int indent)
        {
            indent = 0;
            while (indent < line.Length && char.IsWhiteSpace(line[indent])) indent++;
            int code = indent;
            if (string.CompareOrdinal(line, code, "//", 0, 2) == 0)
            {
                code += 2;
                while (code < line.Length && char.IsWhiteSpace(line[code])) code++;
            }
            return code;
        }

        // armips text: "NAME equ value" or ".definelabel NAME, value", either with a trailing "//" comment.
        private static Definition AsmDefinition(string line)
        {
            int code = CodeStart(line, out int indent);
            int comment = line.IndexOf("//", code, StringComparison.Ordinal);
            string text = (comment < 0 ? line.Substring(code) : line.Substring(code, comment - code)).TrimEnd();
            string[] words = HgEngineMakeFile.Words(text.Replace(",", " "));
            bool isLabel = words.Length >= 3 && words[0] == ".definelabel";
            bool isEqu = !isLabel && words.Length >= 3 && words[1] == "equ";
            if (!isLabel && !isEqu) return null;

            string name = isLabel ? words[1] : words[0];
            if (!name.All(HgEngineMakeFile.IsNameChar)) return null;
            int nameEnd = text.IndexOf(name, isLabel ? ".definelabel".Length : 0, StringComparison.Ordinal) + name.Length;
            int valueAt = isLabel ? text.IndexOf(',', nameEnd) + 1 : text.IndexOf("equ", nameEnd, StringComparison.Ordinal) + 3;
            while (valueAt < text.Length && char.IsWhiteSpace(text[valueAt])) valueAt++;
            string value = text.Substring(valueAt);
            return value.Length == 0 ? null : new Definition(name, indent, code - indent, value, code + valueAt, value.Length, isLabel);
        }

        private static Definition DefinitionOf(string line, string file) => file == HeaderRelPath ? HeaderDefinition(line) : AsmDefinition(line);

        // Turns a line on or off by its leading "//" and swaps in a new value, leaving the rest of the line as it was.
        private static string Rewrite(string line, string file, bool enabled, string value)
        {
            Definition m = DefinitionOf(line, file);
            if (m == null) return line;
            if (value != null && m.Value != null)
                line = line.Substring(0, m.ValueStart) + value.Trim() + line.Substring(m.ValueStart + m.ValueLength);
            if (!m.CommentedOut == enabled) return line;
            return enabled ? line.Remove(m.MarkerStart, m.MarkerLength) : line.Insert(m.MarkerStart, "// ");
        }

        private static string Read(string rel)
        {
            string path = Path.Combine(HgEngineProject.RepoPathUnc, rel.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(path) ? HgEngineFileCache.GetText(path) : null;
        }
    }
}
