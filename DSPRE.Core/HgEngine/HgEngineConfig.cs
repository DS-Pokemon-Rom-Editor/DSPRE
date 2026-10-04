using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

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

        private static readonly Regex HeaderDefine = new(@"^(\s*)(//\s*)?#define\s+(\w+)(?:[ \t]+(\S.*?))?\s*(//.*)?$");
        private static readonly Regex AsmEqu = new(@"^(\s*)(//\s*)?(\w+)\s+equ\s+(\S.*?)\s*(//.*)?$");
        private static readonly Regex AsmLabel = new(@"^(\s*)(//\s*)?\.definelabel\s+(\w+)\s*,\s*(\S.*?)\s*(//.*)?$");

        public static bool TryRead(out List<Setting> settings, out string error)
        {
            settings = new List<Setting>();
            error = null;
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout linked."; return false; }
            string header = Read(HeaderRelPath);
            if (header == null) { error = $"{HeaderRelPath} is missing from the checkout."; return false; }
            settings.AddRange(ReadHeader(header));
            string asm = Read(AsmRelPath);
            if (asm != null)
            {
                var names = settings.Select(x => x.Name).ToHashSet();
                foreach (var a in ReadAsm(asm)) { a.FollowsHeader = names.Contains(a.Name); settings.Add(a); }
            }
            return true;
        }

        private static IEnumerable<Setting> ReadHeader(string text)
        {
            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            var comments = new List<string>();
            int depth = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                string t = lines[i].Trim();
                // The include guard is the one #if everything sits in.
                if (Regex.IsMatch(t, @"^#\s*if(n?def)?\b"))
                {
                    if (!(i < 3 && t.StartsWith("#ifndef", StringComparison.Ordinal))) depth++;
                    comments.Clear();
                    continue;
                }
                if (Regex.IsMatch(t, @"^#\s*endif\b")) { if (depth > 0) depth--; comments.Clear(); continue; }
                if (Regex.IsMatch(t, @"^#\s*(else|elif)\b")) { comments.Clear(); continue; }
                Match m = HeaderDefine.Match(lines[i]);
                if (m.Success && depth == 0)
                {
                    string name = m.Groups[3].Value;
                    if (name == "CONFIG_H" || !Regex.IsMatch(name, "^[A-Z][A-Z0-9_]*$")) { comments.Clear(); continue; }
                    bool enabled = !m.Groups[2].Success;
                    string value = m.Groups[4].Success ? m.Groups[4].Value.Trim() : null;
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
            var comments = new List<string>();
            for (int i = 0; i < lines.Length; i++)
            {
                string t = lines[i].Trim();
                Match equ = AsmEqu.Match(lines[i]), label = AsmLabel.Match(lines[i]);
                Match m = equ.Success ? equ : label.Success ? label : null;
                if (m != null && Regex.IsMatch(m.Groups[3].Value, "^[A-Z][A-Z0-9_]*$"))
                {
                    bool enabled = !m.Groups[2].Success;
                    // A label is switched by being there; an equ's value is the setting.
                    yield return new Setting
                    {
                        Name = m.Groups[3].Value, File = AsmRelPath, Description = string.Join(" ", comments), Line = i,
                        Enabled = enabled, WasEnabled = enabled, CanDisable = label.Success,
                        Value = equ.Success ? m.Groups[4].Value.Trim() : null, WasValue = equ.Success ? m.Groups[4].Value.Trim() : null,
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
            var changed = settings.Where(s => s.Enabled != s.WasEnabled || s.Value != s.WasValue).ToList();
            if (changed.Count == 0) return true;
            if (changed.Any(s => s.Value != null && s.Value.Trim().Length == 0)) { error = "A setting's value can't be empty."; return false; }

            var files = new Dictionary<string, string[]>();
            string[] LinesOf(string rel) => files.TryGetValue(rel, out var l) ? l : files[rel] = (Read(rel) ?? "").Replace("\r\n", "\n").Split('\n');

            foreach (var s in changed)
            {
                var lines = LinesOf(s.File);
                if (s.Line >= lines.Length) { error = $"{s.File} changed since it was read. Reopen the settings."; return false; }
                lines[s.Line] = Rewrite(lines[s.Line], s.File, s.Enabled, s.Value);
                if (s.File != HeaderRelPath) continue;
                // The assembler's copy of the same setting.
                var asm = LinesOf(AsmRelPath);
                for (int i = 0; i < asm.Length; i++)
                {
                    Match equ = AsmEqu.Match(asm[i]), label = AsmLabel.Match(asm[i]);
                    if (equ.Success && equ.Groups[3].Value == s.Name && s.Value != null) asm[i] = Rewrite(asm[i], AsmRelPath, true, s.Value);
                    else if (label.Success && label.Groups[3].Value == s.Name) asm[i] = Rewrite(asm[i], AsmRelPath, s.Enabled, null);
                }
            }
            try
            {
                // Commenting a setting out is the edit, so a define turning into a comment is not a lost comment.
                foreach (var (rel, lines) in files)
                    HgEngineFileCache.WriteText(Path.Combine(HgEngineProject.RepoPathUnc, rel.Replace('/', Path.DirectorySeparatorChar)), string.Join("\n", lines), keepLostComments: false);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { error = ex.Message; return false; }
            return true;
        }

        // Turns a line on or off by its leading "//" and swaps in a new value, leaving the rest of the line as it was.
        private static string Rewrite(string line, string file, bool enabled, string value)
        {
            Match m = file == HeaderRelPath ? HeaderDefine.Match(line) : AsmEqu.Match(line) is { Success: true } e ? e : AsmLabel.Match(line);
            if (!m.Success) return line;
            if (value != null && m.Groups[4].Success)
                line = line.Substring(0, m.Groups[4].Index) + value.Trim() + line.Substring(m.Groups[4].Index + m.Groups[4].Length);
            bool on = !m.Groups[2].Success;
            if (on == enabled) return line;
            int at = m.Groups[1].Length;
            return enabled ? line.Remove(at, m.Groups[2].Length) : line.Insert(at, "// ");
        }

        private static string Read(string rel)
        {
            string path = Path.Combine(HgEngineProject.RepoPathUnc, rel.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(path) ? HgEngineFileCache.GetText(path) : null;
        }
    }
}
