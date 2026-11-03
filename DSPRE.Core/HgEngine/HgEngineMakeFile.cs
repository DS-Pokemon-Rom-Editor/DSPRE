using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace DSPRE.HgEngine
{
    /// <summary>One makefile read into its assignments, includes and rules.</summary>
    public sealed class HgEngineMakeFile
    {
        /// <summary><c>NAME := value</c>, or <c>NAME += value</c> when Appends is set.</summary>
        public readonly record struct Assignment(string Name, string Value, bool Appends);

        /// <summary><c>target: prerequisites</c> and the recipe lines under it. Text is all of it, as written.</summary>
        public sealed record Rule(string Target, string Prerequisites, string Body, string Text);

        public List<Assignment> Assignments { get; } = new();
        public List<string> Includes { get; } = new();
        public List<Rule> Rules { get; } = new();

        private static readonly ConditionalWeakTable<string, HgEngineMakeFile> Parsed = new();

        /// <summary>The parse of <paramref name="text"/>, shared while the same text is in use.</summary>
        public static HgEngineMakeFile For(string text) => Parsed.GetValue(text ?? "", t => new HgEngineMakeFile(t));

        private HgEngineMakeFile(string text)
        {
            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            // Assignments and includes sit inside ifeq blocks too, indented like recipe lines.
            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith("include ", StringComparison.Ordinal) || trimmed.StartsWith("include\t", StringComparison.Ordinal)) Includes.Add(trimmed.Substring(8).Trim());
                else if (TryAssignment(trimmed, out Assignment assignment)) Assignments.Add(assignment);
            }

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.Length == 0 || line[0] == '\t' || line[0] == '#' || TryAssignment(line.Trim(), out _)) continue;

                int colon = RuleColon(line);
                if (colon <= 0) continue;
                int first = i + 1, last = first;
                // A recipe can carry blank and comment lines between its commands, as the battle graphics one does.
                while (last < lines.Length && (lines[last].Length == 0 || lines[last][0] == '\t' || lines[last][0] == '#' || lines[last].Trim().Length == 0)) last++;
                string body = string.Join("\n", lines[first..last]);
                Rules.Add(new Rule(line.Substring(0, colon).Trim(), line.Substring(colon + 1).Trim(), body, line + "\n" + body));
                i = last - 1;
            }
        }

        private static bool TryAssignment(string line, out Assignment assignment)
        {
            assignment = default;
            int at = 0;
            while (at < line.Length && (char.IsLetterOrDigit(line[at]) || line[at] == '_')) at++;
            if (at == 0) return false;
            string name = line.Substring(0, at);
            while (at < line.Length && line[at] is ' ' or '\t') at++;
            bool appends = at < line.Length && line[at] == '+';
            if (appends || (at < line.Length && line[at] == ':')) at++;
            if (at >= line.Length || line[at] != '=') return false;
            assignment = new Assignment(name, line.Substring(at + 1).Trim(), appends);
            return true;
        }

        // The colon that ends a rule's targets, outside any $(...), or -1. ":=" is an assignment.
        private static int RuleColon(string line)
        {
            int depth = 0;
            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] == '(') depth++;
                else if (line[i] == ')') depth--;
                else if (line[i] == ':' && depth == 0) return i + 1 < line.Length && line[i + 1] == '=' ? -1 : i;
            }
            return -1;
        }

        /// <summary>The first value of <paramref name="name"/> that is one word, or null.</summary>
        public string Value(string name)
        {
            foreach (Assignment a in Assignments)
                if (a.Name == name && !a.Appends && a.Value.Length > 0 && !a.Value.Any(char.IsWhiteSpace)) return a.Value;
            return null;
        }

        /// <summary>Every assignment of a name that ends in <paramref name="suffix"/>, with the suffix cut off.</summary>
        public IEnumerable<(string Stem, string Value)> Suffixed(string suffix)
        {
            foreach (Assignment a in Assignments)
                if (!a.Appends && a.Name.Length > suffix.Length && a.Name.EndsWith(suffix, StringComparison.Ordinal))
                    yield return (a.Name.Substring(0, a.Name.Length - suffix.Length), a.Value);
        }

        /// <summary>The text split on whitespace, so commands across lines read as one run of words.</summary>
        public static string[] Words(string text) =>
            (text ?? "").Split((char[])null, StringSplitOptions.RemoveEmptyEntries);

        /// <summary>Whether the words appear in a row. The last only has to start with what is given.</summary>
        public static bool HasRun(string[] words, params string[] run) => RunStart(words, 0, run) >= 0;

        /// <summary>Where <paramref name="run"/> next appears from <paramref name="from"/> on, or -1.</summary>
        public static int RunStart(string[] words, int from, params string[] run)
        {
            for (int i = from; i + run.Length <= words.Length; i++)
            {
                int k = 0;
                while (k < run.Length - 1 && words[i + k] == run[k]) k++;
                if (k == run.Length - 1 && words[i + k].StartsWith(run[k], StringComparison.Ordinal)) return i;
            }
            return -1;
        }

        /// <summary>X for a word that is exactly <c>$(X)</c>, or null.</summary>
        public static string ReferenceName(string word)
        {
            if (word == null || !word.StartsWith("$(", StringComparison.Ordinal) || !word.EndsWith(')')) return null;
            string name = word.Substring(2, word.Length - 3);
            return name.Length > 0 && name.All(IsNameChar) ? name : null;
        }

        /// <summary>Replaces each <c>$(X)</c> that <paramref name="value"/> has an answer for, leaving the others as written.</summary>
        public static string Expand(string text, Func<string, string> value)
        {
            for (int depth = 0; depth < 8 && text.Contains("$(", StringComparison.Ordinal); depth++)
            {
                string next = ExpandOnce(text, value);
                if (next == text) break;
                text = next;
            }
            return text;
        }

        private static string ExpandOnce(string text, Func<string, string> value)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            int at = 0;
            while (at < text.Length)
            {
                int open = text.IndexOf("$(", at, StringComparison.Ordinal);
                int close = open < 0 ? -1 : text.IndexOf(')', open);
                if (close < 0) break;
                string name = text.Substring(open + 2, close - open - 2);
                sb.Append(text, at, open - at);
                // $(wildcard $(DIR)/*) has a reference inside it, so scanning carries on just past the "$(".
                if (name.Length == 0 || !name.All(IsNameChar))
                {
                    sb.Append("$(");
                    at = open + 2;
                    continue;
                }
                string replaced = value(name);
                if (replaced != null) sb.Append(replaced);
                else sb.Append(text, open, close + 1 - open);
                at = close + 1;
            }
            return sb.Append(text, at, text.Length - at).ToString();
        }

        /// <summary>The digits in <c>prefix123suffix</c>, or -1 when the text isn't that shape.</summary>
        public static int NumberBetween(string text, string prefix, string suffix)
        {
            if (text.Length <= prefix.Length + suffix.Length || !text.StartsWith(prefix, StringComparison.Ordinal) || !text.EndsWith(suffix, StringComparison.Ordinal)) return -1;
            string digits = text.Substring(prefix.Length, text.Length - prefix.Length - suffix.Length);
            return IsDigits(digits) && int.TryParse(digits, out int number) ? number : -1;
        }

        /// <summary>Letters, digits and underscore.</summary>
        public static bool IsNameChar(char c) => char.IsLetterOrDigit(c) || c == '_';

        /// <summary>Whether <paramref name="text"/> is one or more digits.</summary>
        public static bool IsDigits(string text) => text.Length > 0 && text.All(char.IsAsciiDigit);
    }
}
