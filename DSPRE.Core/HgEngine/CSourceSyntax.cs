using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace DSPRE.HgEngine
{
    public enum CTokenKind { Identifier, Number, String, Char, Punct, Directive, Comment }

    /// <summary>One C token: where it is in the source and what kind it is.</summary>
    public readonly struct CToken
    {
        public readonly CTokenKind Kind;
        public readonly int Start, Length;
        public CToken(CTokenKind kind, int start, int length) { Kind = kind; Start = start; Length = length; }
        public int End => Start + Length;
        public string Text(string source) => source.Substring(Start, Length);
        public bool Is(string source, string text) => Length == text.Length && string.CompareOrdinal(source, Start, text, 0, Length) == 0;
    }

    /// <summary>
    /// Splits C source into tokens the way a compiler would see them: comments, string and character literals and
    /// preprocessor lines (with their continuations) are each one token, so braces, commas and quotes inside them never
    /// count as code. The tokens keep their source positions, so an edit replaces exact spans and leaves the rest of the
    /// file as written.
    /// </summary>
    public static class CLexer
    {
        private static readonly string[] Puncts =
        {
            "...", "<<=", ">>=", "->", "++", "--", "<<", ">>", "<=", ">=", "==", "!=", "&&", "||",
            "+=", "-=", "*=", "/=", "%=", "&=", "^=", "|=", "##",
        };

        public static List<CToken> Tokenize(string text, bool keepComments = false) => Tokenize(text, 0, text.Length, keepComments);

        /// <summary>Tokens of <paramref name="text"/> from <paramref name="start"/> up to <paramref name="end"/>.</summary>
        public static List<CToken> Tokenize(string text, int start, int end, bool keepComments = false)
        {
            List<CToken> tokens = new List<CToken>();
            int i = Math.Max(0, start), n = Math.Min(text.Length, end);
            bool lineStart = true;
            for (int k = i - 1; k >= 0 && text[k] != '\n'; k--)
                if (!char.IsWhiteSpace(text[k])) { lineStart = false; break; }
            while (i < n)
            {
                char c = text[i];
                if (c == '\n') { lineStart = true; i++; continue; }
                if (char.IsWhiteSpace(c)) { i++; continue; }

                if (c == '/' && i + 1 < n && text[i + 1] == '/')
                {
                    int stop = text.IndexOf('\n', i, n - i);
                    if (stop < 0) stop = n;
                    if (keepComments) tokens.Add(new CToken(CTokenKind.Comment, i, stop - i));
                    i = stop;
                    continue;
                }
                if (c == '/' && i + 1 < n && text[i + 1] == '*')
                {
                    int stop = i + 2 < n ? text.IndexOf("*/", i + 2, n - i - 2, StringComparison.Ordinal) : -1;
                    stop = stop < 0 ? n : stop + 2;
                    if (keepComments) tokens.Add(new CToken(CTokenKind.Comment, i, stop - i));
                    i = stop;
                    continue;
                }
                if (c == '#' && lineStart)
                {
                    int stop = Math.Min(n, DirectiveEnd(text, i));
                    tokens.Add(new CToken(CTokenKind.Directive, i, stop - i));
                    i = stop;
                    continue;
                }
                lineStart = false;

                if (c == '"' || c == '\'')
                {
                    int j = i + 1;
                    while (j < n && text[j] != c && text[j] != '\n')
                    {
                        if (text[j] == '\\') j++;
                        j++;
                    }
                    j = Math.Min(n, j + 1);
                    tokens.Add(new CToken(c == '"' ? CTokenKind.String : CTokenKind.Char, i, j - i));
                    i = j;
                    continue;
                }
                if (char.IsDigit(c) || (c == '.' && i + 1 < n && char.IsDigit(text[i + 1])))
                {
                    int j = i + 1;
                    while (j < n)
                    {
                        char d = text[j];
                        if (char.IsLetterOrDigit(d) || d == '_' || d == '.') { j++; continue; }
                        if ((d == '+' || d == '-') && (text[j - 1] is 'e' or 'E' or 'p' or 'P') && !IsHex(text, i)) { j++; continue; }
                        break;
                    }
                    tokens.Add(new CToken(CTokenKind.Number, i, j - i));
                    i = j;
                    continue;
                }
                if (char.IsLetter(c) || c == '_')
                {
                    int j = i + 1;
                    while (j < n && (char.IsLetterOrDigit(text[j]) || text[j] == '_')) j++;
                    tokens.Add(new CToken(CTokenKind.Identifier, i, j - i));
                    i = j;
                    continue;
                }
                int len = 1;
                foreach (string p in Puncts)
                    if (string.CompareOrdinal(text, i, p, 0, p.Length) == 0) { len = p.Length; break; }
                tokens.Add(new CToken(CTokenKind.Punct, i, len));
                i += len;
            }
            return tokens;
        }

        private static bool IsHex(string text, int start) => start + 1 < text.Length && text[start] == '0' && (text[start + 1] == 'x' || text[start + 1] == 'X');

        // A directive runs to the end of its line, through backslash continuations and block comments that cross lines.
        private static int DirectiveEnd(string text, int i)
        {
            int n = text.Length;
            while (i < n)
            {
                char c = text[i];
                if (c == '\n')
                {
                    int k = i - 1;
                    if (k >= 0 && text[k] == '\r') k--;
                    if (k >= 0 && text[k] == '\\') { i++; continue; }
                    return i > 0 && text[i - 1] == '\r' ? i - 1 : i;
                }
                if (c == '/' && i + 1 < n && text[i + 1] == '*')
                {
                    int end = text.IndexOf("*/", i + 2, StringComparison.Ordinal);
                    i = end < 0 ? n : end + 2;
                    continue;
                }
                // A line comment ends the directive; it is its own token.
                if (c == '/' && i + 1 < n && text[i + 1] == '/') return i;
                i++;
            }
            return n;
        }
    }

    /// <summary>Evaluates the preprocessor conditions an element sits under, the way the compiler would.</summary>
    public static class CConditions
    {
        private static readonly System.Text.RegularExpressions.Regex Defined =
            new(@"\bdefined\s*(?:\(\s*([A-Za-z_]\w*)\s*\)|([A-Za-z_]\w*))");

        /// <summary>True when every condition holds, false when one doesn't, null when one can't be evaluated.
        /// An undefined name is 0, as in C.</summary>
        public static bool? Evaluate(IEnumerable<string> conditions, Func<string, bool> isDefined, Func<string, int?> value)
        {
            foreach (string condition in conditions)
            {
                string expr = Defined.Replace(condition, m => isDefined(m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value) ? "1" : "0");
                if (!HgEngineSourceExpression.TryEvaluate(expr, n => value(n) ?? 0, out int result)) return null;
                if (result == 0) return false;
            }
            return true;
        }
    }

    /// <summary>The names include/config.h defines and their values, for deciding which #if branches compile.</summary>
    public static class HgEngineConfigState
    {
        private const string ConfigRelPath = "include/config.h";
        private static (string Text, HashSet<string> Names) _cache;

        public static HashSet<string> DefinedNames()
        {
            if (!HgEngineProject.IsActive) return new HashSet<string>();
            string path = System.IO.Path.Combine(HgEngineProject.RepoPathUnc, ConfigRelPath.Replace('/', System.IO.Path.DirectorySeparatorChar));
            if (!System.IO.File.Exists(path)) return new HashSet<string>();
            string text = HgEngineFileCache.GetText(path);
            if (ReferenceEquals(_cache.Text, text)) return _cache.Names;
            HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
            foreach (CToken t in CLexer.Tokenize(text))
            {
                if (t.Kind != CTokenKind.Directive) continue;
                Match m = System.Text.RegularExpressions.Regex.Match(t.Text(text), @"^#\s*define\s+([A-Za-z_]\w*)");
                if (m.Success) names.Add(m.Groups[1].Value);
            }
            _cache = (text, names);
            return names;
        }

        /// <summary>Whether an element under <paramref name="conditions"/> compiles with this checkout's config.h;
        /// null when a condition can't be evaluated.</summary>
        public static bool? Compiles(IEnumerable<string> conditions)
        {
            HashSet<string> names = DefinedNames();
            HgEngineSymbolTable values = HgEngineSymbolTable.Load(ConfigRelPath);
            return CConditions.Evaluate(conditions, names.Contains, n => values != null && values.TryGetValue(n, out int v) ? v : (names.Contains(n) ? 1 : null));
        }
    }

    /// <summary>A designator in front of an initializer element: <c>.field</c> or <c>[index]</c>.</summary>
    public sealed class CDesignator
    {
        public bool IsField { get; init; }
        /// <summary>The field name, or the index expression's text (<c>ABILITY_STENCH</c>, <c>3</c>, <c>0 ... 4</c>).</summary>
        public string Text { get; init; }
        public int Start { get; init; }
        public int End { get; init; }
    }

    /// <summary>One element of a brace initializer.</summary>
    public sealed class CInitItem
    {
        /// <summary>From the first designator (or the value) to the end of the value, comments excluded.</summary>
        public int Start, End;
        public int ValueStart, ValueEnd;
        public List<CDesignator> Designators { get; } = new();
        /// <summary>The value when it is itself a brace list.</summary>
        public CInitList List;
        /// <summary>The position for an element with no designator or a numeric index; -1 when an index names a symbol.</summary>
        public int Position;
        /// <summary>The preprocessor conditions the element sits under, outermost first; empty when it always compiles.</summary>
        public List<string> Conditions { get; } = new();

        public string FieldName => Designators.Count > 0 && Designators[0].IsField ? Designators[0].Text : null;
        public string IndexText => Designators.Count > 0 && !Designators[0].IsField ? Designators[0].Text : null;
        public string ValueText(string source) => source.Substring(ValueStart, ValueEnd - ValueStart);
        public bool IsConditional => Conditions.Count > 0;
    }

    /// <summary>A brace initializer and its elements.</summary>
    public sealed class CInitList
    {
        public int Open, Close;
        public List<CInitItem> Items { get; } = new();

        /// <summary>The element whose first designator is <c>.name</c>.</summary>
        public CInitItem Field(string name) => Items.FirstOrDefault(i => i.FieldName == name);
    }

    /// <summary>A file-scope declaration with a brace initializer: <c>const T name[...] = { ... };</c>.</summary>
    public sealed class CDeclaration
    {
        public string Name { get; init; }
        /// <summary>Where the declaration's first token starts.</summary>
        public int Start { get; init; }
        /// <summary>The bracketed dimensions after the name, as written (<c>[]</c>, <c>[SWARM_MAP_COUNT][2]</c>).</summary>
        public List<string> Dimensions { get; } = new();
        public CInitList Init { get; init; }
        /// <summary>Just past the terminating semicolon.</summary>
        public int End { get; init; }
        public List<string> Conditions { get; } = new();
    }

    /// <summary>Reads brace initializers and the declarations that own them from tokenized C.</summary>
    public sealed class CSourceFile
    {
        public string Text { get; }
        public List<CToken> Tokens { get; }
        private readonly Dictionary<int, int> _tokenAt = new();
        private List<CDeclaration> _declarations;

        public CSourceFile(string text) : this(text, 0, text?.Length ?? 0) { }

        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<string, CSourceFile> _parsed = new();

        /// <summary>The parse of <paramref name="text"/>, shared while the same text is in use (the file cache hands out
        /// one string per file version), so large tables aren't tokenized on every read.</summary>
        public static CSourceFile For(string text) => _parsed.GetValue(text ?? "", t => new CSourceFile(t));

        /// <summary>Only the tokens from <paramref name="start"/> up to <paramref name="end"/>, for reading one entry of a
        /// large file without tokenizing all of it.</summary>
        public CSourceFile(string text, int start, int end)
        {
            Text = text ?? "";
            Tokens = CLexer.Tokenize(Text, start, end);
            for (int i = 0; i < Tokens.Count; i++) _tokenAt[Tokens[i].Start] = i;
        }

        private bool Is(int t, string s) => t >= 0 && t < Tokens.Count && Tokens[t].Kind == CTokenKind.Punct && Tokens[t].Is(Text, s);

        /// <summary>The token index of the bracket matching the one at <paramref name="open"/>, or -1.</summary>
        public int Match(int open)
        {
            string o = Tokens[open].Text(Text);
            string c = o == "{" ? "}" : o == "(" ? ")" : o == "[" ? "]" : null;
            if (c == null) return -1;
            int depth = 0;
            for (int t = open; t < Tokens.Count; t++)
            {
                if (Tokens[t].Kind != CTokenKind.Punct) continue;
                if (Tokens[t].Is(Text, o)) depth++;
                else if (Tokens[t].Is(Text, c) && --depth == 0) return t;
            }
            return -1;
        }

        /// <summary>Every file-scope declaration with a brace initializer, in file order.</summary>
        public IReadOnlyList<CDeclaration> Declarations => _declarations ??= FindDeclarations();

        public CDeclaration Find(string name) => Declarations.FirstOrDefault(d => d.Name == name);

        private List<CDeclaration> FindDeclarations()
        {
            List<CDeclaration> result = new List<CDeclaration>();
            List<string> conditions = new List<string>();
            int depth = 0, statementStart = 0;
            for (int t = 0; t < Tokens.Count; t++)
            {
                CToken tok = Tokens[t];
                if (tok.Kind == CTokenKind.Directive) { if (depth == 0) Condition(conditions, tok.Text(Text)); statementStart = t + 1; continue; }
                if (tok.Kind != CTokenKind.Punct) continue;
                if (Is(t, "{")) { depth++; continue; }
                if (Is(t, "}")) { depth = Math.Max(0, depth - 1); if (depth == 0) statementStart = t + 1; continue; }
                if (depth > 0) continue;
                if (Is(t, ";")) { statementStart = t + 1; continue; }
                if (!Is(t, "=") || !Is(t + 1, "{")) continue;

                // name [dims] = { ... };
                int nameTok = t - 1;
                List<string> dims = new List<string>();
                while (nameTok >= statementStart && Is(nameTok, "]"))
                {
                    int open = nameTok;
                    int d = 0;
                    for (; open >= statementStart; open--)
                    {
                        if (Is(open, "]")) d++;
                        else if (Is(open, "[") && --d == 0) break;
                    }
                    if (open < statementStart) break;
                    dims.Insert(0, Text.Substring(Tokens[open].Start, Tokens[nameTok].End - Tokens[open].Start));
                    nameTok = open - 1;
                }
                if (nameTok < statementStart || Tokens[nameTok].Kind != CTokenKind.Identifier) continue;

                int close = Match(t + 1);
                if (close < 0) break;
                CInitList init = ParseList(t + 1, close);
                int end = close + 1;
                if (Is(end, ";")) end++;
                CDeclaration decl = new CDeclaration
                {
                    Name = Tokens[nameTok].Text(Text),
                    Start = Tokens[statementStart].Start,
                    Init = init,
                    End = Tokens[end - 1].End,
                };
                decl.Dimensions.AddRange(dims);
                decl.Conditions.AddRange(conditions);
                result.Add(decl);
                t = end - 1;
                statementStart = end;
            }
            return result;
        }

        /// <summary>Parses the brace list that starts at source position <paramref name="openBrace"/>.</summary>
        public CInitList ListAt(int openBrace)
        {
            if (!_tokenAt.TryGetValue(openBrace, out int t) || !Is(t, "{")) return null;
            int close = Match(t);
            return close < 0 ? null : ParseList(t, close);
        }

        private CInitList ParseList(int open, int close)
        {
            CInitList list = new CInitList { Open = Tokens[open].Start, Close = Tokens[close].Start };
            List<string> conditions = new List<string>();
            int position = 0;
            int t = open + 1;
            while (t < close)
            {
                if (Tokens[t].Kind == CTokenKind.Directive) { Condition(conditions, Tokens[t].Text(Text)); t++; continue; }
                if (Is(t, ",")) { t++; continue; }

                // One element runs to the next comma or directive at this depth.
                int end = t;
                int depth = 0;
                for (; end < close; end++)
                {
                    if (Tokens[end].Kind == CTokenKind.Directive && depth == 0) break;
                    if (Tokens[end].Kind != CTokenKind.Punct) continue;
                    string p = Tokens[end].Text(Text);
                    if (p is "{" or "(" or "[") depth++;
                    else if (p is "}" or ")" or "]") depth--;
                    else if (p == "," && depth == 0) break;
                }
                CInitItem item = ParseItem(t, end, ref position);
                item.Conditions.AddRange(conditions);
                list.Items.Add(item);
                t = end;
            }
            return list;
        }

        private CInitItem ParseItem(int first, int end, ref int position)
        {
            CInitItem item = new CInitItem { Start = Tokens[first].Start, End = Tokens[end - 1].End };
            int t = first;
            while (t < end)
            {
                if (Is(t, ".") && t + 1 < end && Tokens[t + 1].Kind == CTokenKind.Identifier)
                {
                    item.Designators.Add(new CDesignator { IsField = true, Text = Tokens[t + 1].Text(Text), Start = Tokens[t].Start, End = Tokens[t + 1].End });
                    t += 2;
                    continue;
                }
                if (Is(t, "["))
                {
                    int close = Match(t);
                    if (close < 0 || close >= end) break;
                    string inner = close > t + 1 ? Text.Substring(Tokens[t + 1].Start, Tokens[close - 1].End - Tokens[t + 1].Start) : "";
                    item.Designators.Add(new CDesignator { IsField = false, Text = inner, Start = Tokens[t].Start, End = Tokens[close].End });
                    t = close + 1;
                    continue;
                }
                break;
            }
            if (item.Designators.Count > 0 && Is(t, "=")) t++;
            else { item.Designators.Clear(); t = first; }

            item.ValueStart = t < end ? Tokens[t].Start : item.End;
            item.ValueEnd = item.End;
            if (Is(t, "{") && Match(t) == end - 1) item.List = ParseList(t, end - 1);

            if (item.Designators.Count == 0) item.Position = position;
            else if (!item.Designators[0].IsField)
                item.Position = int.TryParse(item.Designators[0].Text.Trim(), out int p) ? p
                    : item.Designators[0].Text.Trim().StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                      && int.TryParse(item.Designators[0].Text.Trim().Substring(2), System.Globalization.NumberStyles.HexNumber, null, out int h) ? h : -1;
            else item.Position = position;
            position = item.Position >= 0 ? item.Position + 1 : position + 1;
            return item;
        }

        // #if/#ifdef/#ifndef open a condition, #elif/#else replace it, #endif closes it.
        private static void Condition(List<string> stack, string directive)
        {
            string d = directive.TrimStart('#').TrimStart();
            string word = new string(d.TakeWhile(char.IsLetter).ToArray());
            string rest = d.Substring(word.Length).Trim();
            switch (word)
            {
                case "if": stack.Add(rest); break;
                case "ifdef": stack.Add("defined(" + rest + ")"); break;
                case "ifndef": stack.Add("!defined(" + rest + ")"); break;
                case "elif": if (stack.Count > 0) stack[^1] = "!(" + stack[^1] + ") && (" + rest + ")"; else stack.Add(rest); break;
                case "else": if (stack.Count > 0) stack[^1] = "!(" + stack[^1] + ")"; break;
                case "endif": if (stack.Count > 0) stack.RemoveAt(stack.Count - 1); break;
            }
        }

        /// <summary>The text of a value with comments removed and whitespace collapsed, for comparing spellings.</summary>
        public static string Normalize(string value)
        {
            StringBuilder sb = new StringBuilder();
            List<CToken> tokens = CLexer.Tokenize(value);
            for (int i = 0; i < tokens.Count; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(tokens[i].Text(value));
            }
            return sb.ToString();
        }
    }
}
