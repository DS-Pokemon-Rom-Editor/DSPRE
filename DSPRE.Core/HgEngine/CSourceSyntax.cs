using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
        /// <summary>True when every condition holds, false when one doesn't, null when one can't be evaluated.
        /// An undefined name is 0, as in C.</summary>
        public static bool? Evaluate(IEnumerable<string> conditions, Func<string, bool> isDefined, Func<string, int?> value)
        {
            foreach (string condition in conditions)
            {
                if (!HgEngineSourceExpression.TryEvaluate(ResolveDefined(condition, isDefined), n => value(n) ?? 0, out int result)) return null;
                if (result == 0) return false;
            }
            return true;
        }

        /// <summary>The condition with each <c>defined X</c> and <c>defined(X)</c> replaced by 1 or 0.</summary>
        public static string ResolveDefined(string condition, Func<string, bool> isDefined)
        {
            List<CToken> tokens = CLexer.Tokenize(condition);
            StringBuilder sb = new StringBuilder();
            for (int t = 0; t < tokens.Count; t++)
            {
                if (sb.Length > 0) sb.Append(' ');
                if (!tokens[t].Is(condition, "defined")) { sb.Append(tokens[t].Text(condition)); continue; }
                int name = t + 1;
                bool bracketed = name < tokens.Count && tokens[name].Is(condition, "(");
                if (bracketed) name++;
                if (name >= tokens.Count) { sb.Append('0'); break; }
                sb.Append(isDefined(tokens[name].Text(condition)) ? '1' : '0');
                t = bracketed ? name + 1 : name;
            }
            return sb.ToString();
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
            foreach (CDirective d in CSourceFile.For(text).Directives)
                if (d.Keyword == "define" && d.Name != null) names.Add(d.Name);
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

        /// <summary>The first element called <c>.name</c> at any depth, in file order.</summary>
        public CInitItem FindField(string name)
        {
            foreach (CInitItem item in Items)
            {
                if (item.FieldName == name) return item;
                if (item.List?.FindField(name) is CInitItem found) return found;
            }
            return null;
        }
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

    /// <summary>A <c>[name] = { ... }</c> element. Close is -1 when its brace has no match.</summary>
    public readonly record struct CEntry(string Name, int Open, int Close);

    /// <summary>A preprocessor line: its keyword and what follows it, continuation lines joined.</summary>
    public sealed class CDirective
    {
        public string Keyword { get; private init; }
        public string Rest { get; private init; }
        public int Start { get; private init; }
        public int End { get; private init; }
        /// <summary>The name a #define, #undef, #ifdef or #ifndef is about.</summary>
        public string Name { get; private init; }
        /// <summary>A function-like #define's parameters; null on every other directive.</summary>
        public List<string> Parameters { get; private init; }
        /// <summary>A #define's value; empty when it has none.</summary>
        public string Body { get; private init; }
        public int BodyStart { get; private init; }
        public int BodyEnd { get; private init; }

        public static CDirective Parse(string text, CToken directive)
        {
            List<CToken> tokens = CLexer.Tokenize(text, directive.Start + 1, directive.End);
            while (tokens.Count > 0 && tokens[^1].Is(text, "\\")) tokens.RemoveAt(tokens.Count - 1);
            string keyword = tokens.Count > 0 ? tokens[0].Text(text) : "";
            int restAt = tokens.Count > 1 ? 1 : tokens.Count;
            string name = tokens.Count > 1 && tokens[1].Kind == CTokenKind.Identifier ? tokens[1].Text(text) : null;

            int body = 2;
            List<string> parameters = null;
            if (keyword == "define" && tokens.Count > 2 && tokens[2].Is(text, "(") && tokens[2].Start == tokens[1].End)
            {
                parameters = new List<string>();
                int t = 3;
                for (; t < tokens.Count && !tokens[t].Is(text, ")"); t++)
                    if (tokens[t].Kind == CTokenKind.Identifier) parameters.Add(tokens[t].Text(text));
                body = t + 1;
            }
            int bodyStart = body < tokens.Count ? tokens[body].Start : directive.End;
            int bodyEnd = body < tokens.Count ? tokens[^1].End : directive.End;
            return new CDirective
            {
                Keyword = keyword,
                Rest = restAt < tokens.Count ? Joined(text, tokens[restAt].Start, tokens[^1].End) : "",
                Start = directive.Start,
                End = directive.End,
                Name = name,
                Parameters = parameters,
                Body = keyword == "define" ? Joined(text, bodyStart, bodyEnd) : "",
                BodyStart = bodyStart,
                BodyEnd = bodyEnd,
            };
        }

        private static string Joined(string text, int start, int end) =>
            text.Substring(start, end - start).Replace("\\\r\n", " ").Replace("\\\n", " ").Trim();
    }

    /// <summary>One member of a struct: <c>u8 slowStart : 1;</c>. Bits is 0 when it isn't a bitfield.</summary>
    public readonly record struct CMember(string Type, string Name, int Bits);

    /// <summary>A struct definition. The tag and the typedef name both find it.</summary>
    public sealed class CStruct
    {
        public List<string> Names { get; } = new();
        public int Open { get; init; }
        public int Close { get; init; }
        public List<CMember> Members { get; } = new();
    }

    /// <summary>A call or macro use: where it sits and where each argument is.</summary>
    public sealed record CCall(int Start, int End, List<(int Start, int End)> Args);

    /// <summary>Reads brace initializers and the declarations that own them from tokenized C.</summary>
    public sealed class CSourceFile
    {
        public string Text { get; }
        public List<CToken> Tokens { get; }
        private readonly Dictionary<int, int> _tokenAt = new();
        private List<CDeclaration> _declarations;
        private List<CEntry> _entries;
        private List<CDirective> _directives;
        private List<CStruct> _structs;

        public CSourceFile(string text) : this(text, 0, text?.Length ?? 0) { }

        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<string, CSourceFile> Parsed = new();

        /// <summary>The parse of <paramref name="text"/>, shared while the same text is in use (the file cache hands out
        /// one string per file version), so large tables aren't tokenized on every read.</summary>
        public static CSourceFile For(string text) => Parsed.GetValue(text ?? "", t => new CSourceFile(t));

        /// <summary>Only the tokens from <paramref name="start"/> up to <paramref name="end"/>, for reading one entry of a
        /// large file without tokenizing all of it.</summary>
        public CSourceFile(string text, int start, int end)
        {
            Text = text ?? "";
            Tokens = CLexer.Tokenize(Text, start, end);
            for (int i = 0; i < Tokens.Count; i++) _tokenAt[Tokens[i].Start] = i;
        }

        private bool Is(int t, string s) => t >= 0 && t < Tokens.Count && Tokens[t].Kind == CTokenKind.Punct && Tokens[t].Is(Text, s);

        /// <summary>The source position of the '}' matching the code '{' at <paramref name="openBrace"/>, or -1.</summary>
        public int MatchingBrace(int openBrace)
        {
            if (!_tokenAt.TryGetValue(openBrace, out int t) || !Is(t, "{")) return -1;
            int close = Match(t);
            return close < 0 ? -1 : Tokens[close].Start;
        }

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

        /// <summary>Every <c>[name] = {</c> in the code, nested ones included, in file order. This also finds a lone
        /// entry cut from its table, which no declaration owns.</summary>
        public IReadOnlyList<CEntry> Entries => _entries ??= FindEntries();

        /// <summary>The entries not inside another entry's braces.</summary>
        public IEnumerable<CEntry> OuterEntries
        {
            get
            {
                int lastClose = -1;
                foreach (CEntry entry in Entries)
                {
                    if (entry.Open < lastClose || entry.Close < 0) continue;
                    lastClose = entry.Close;
                    yield return entry;
                }
            }
        }

        /// <summary>The first entry called <paramref name="name"/>.</summary>
        public CEntry? FindEntry(string name)
        {
            foreach (CEntry entry in Entries)
                if (entry.Name == name) return entry;
            return null;
        }

        private List<CEntry> FindEntries()
        {
            List<CEntry> entries = new List<CEntry>();
            for (int t = 0; t < Tokens.Count; t++)
            {
                if (!Is(t, "[")) continue;
                // "name[N] = {" and "name[A][B] = {" declare an array. Only a designator opens "[".
                if (t > 0 && (Tokens[t - 1].Kind == CTokenKind.Identifier || Is(t - 1, "]"))) continue;
                int bracket = Match(t);
                if (bracket < 0 || !Is(bracket + 1, "=") || !Is(bracket + 2, "{")) continue;
                int brace = Match(bracket + 2);
                string name = Text.Substring(Tokens[t].End, Tokens[bracket].Start - Tokens[t].End).Trim();
                entries.Add(new CEntry(name, Tokens[bracket + 2].Start, brace < 0 ? -1 : Tokens[brace].Start));
            }
            return entries;
        }

        /// <summary>Every preprocessor line, in file order.</summary>
        public IReadOnlyList<CDirective> Directives =>
            _directives ??= Tokens.Where(t => t.Kind == CTokenKind.Directive).Select(t => CDirective.Parse(Text, t)).ToList();

        /// <summary>The first #define of <paramref name="name"/>.</summary>
        public CDirective FindDefine(string name) => Directives.FirstOrDefault(d => d.Keyword == "define" && d.Name == name);

        /// <summary>The paths of the file's quoted #includes.</summary>
        public IEnumerable<string> Includes => Directives.Where(d => d.Keyword == "include" && d.Rest.StartsWith('"')).Select(d => d.Rest.Trim('"'));

        /// <summary>The quoted #includes and where each path starts, in file order.</summary>
        public IEnumerable<(string Path, int Start)> IncludePaths
        {
            get
            {
                foreach (CDirective d in Directives)
                    if (d.Keyword == "include" && d.Rest.StartsWith('"'))
                        yield return (d.Rest.Trim('"'), Text.IndexOf('"', d.Start) + 1);
            }
        }

        /// <summary>Every enum member's name, in any #if branch.</summary>
        public IEnumerable<string> EnumMembers
        {
            get
            {
                for (int t = 0; t < Tokens.Count; t++)
                {
                    if (Tokens[t].Kind != CTokenKind.Identifier || !Tokens[t].Is(Text, "enum")) continue;
                    int open = t + 1;
                    if (open < Tokens.Count && Tokens[open].Kind == CTokenKind.Identifier) open++;
                    if (!Is(open, "{")) continue;
                    int close = Match(open);
                    if (close < 0) continue;
                    bool atName = true;
                    int depth = 0;
                    for (int k = open + 1; k < close; k++)
                    {
                        if (Is(k, "(") || Is(k, "[")) depth++;
                        else if (Is(k, ")") || Is(k, "]")) depth--;
                        else if (Is(k, ",") && depth == 0) atName = true;
                        else if (atName && Tokens[k].Kind == CTokenKind.Identifier) { atName = false; yield return Tokens[k].Text(Text); }
                    }
                    t = close;
                }
            }
        }

        /// <summary>Every struct definition at file scope, in file order.</summary>
        public IReadOnlyList<CStruct> Structs => _structs ??= FindStructs();

        public CStruct FindStruct(string name) => Structs.FirstOrDefault(s => s.Names.Contains(name));

        private List<CStruct> FindStructs()
        {
            List<CStruct> structs = new List<CStruct>();
            for (int t = 0; t < Tokens.Count; t++)
            {
                if (Tokens[t].Kind != CTokenKind.Identifier || !Tokens[t].Is(Text, "struct")) continue;
                int open = t + 1;
                while (open < Tokens.Count && Tokens[open].Kind == CTokenKind.Identifier) open++;
                if (!Is(open, "{")) continue;
                int close = Match(open);
                if (close < 0) continue;

                CStruct found = new CStruct { Open = Tokens[open].Start, Close = Tokens[close].Start };
                for (int n = t + 1; n < open; n++) found.Names.Add(Tokens[n].Text(Text));
                if (close + 1 < Tokens.Count && Tokens[close + 1].Kind == CTokenKind.Identifier) found.Names.Add(Tokens[close + 1].Text(Text));
                ReadMembers(found, open + 1, close);
                structs.Add(found);
                t = close;
            }
            return structs;
        }

        private void ReadMembers(CStruct target, int from, int close)
        {
            int start = from, depth = 0;
            for (int t = from; t < close; t++)
            {
                if (Is(t, "{")) depth++;
                else if (Is(t, "}")) depth--;
                else if (Is(t, ";") && depth == 0)
                {
                    ReadMember(target, start, t);
                    start = t + 1;
                }
            }
        }

        // "u8 a : 1, b : 1": the type runs to the first name, and each comma-separated declarator shares it.
        private void ReadMember(CStruct target, int start, int end)
        {
            string type = null;
            int declarator = start;
            for (int t = start; t <= end; t++)
            {
                if (t < end && !Is(t, ",")) continue;
                int name = -1, bits = 0;
                for (int k = declarator; k < t; k++)
                {
                    if (Is(k, ":")) { if (k + 1 < t) int.TryParse(Tokens[k + 1].Text(Text), out bits); break; }
                    if (Is(k, "[")) break;
                    if (Tokens[k].Kind == CTokenKind.Identifier) name = k;
                }
                if (name >= 0)
                {
                    type ??= name > start ? Normalize(Text.Substring(Tokens[start].Start, Tokens[name - 1].End - Tokens[start].Start)) : "";
                    target.Members.Add(new CMember(type, Tokens[name].Text(Text), bits));
                }
                declarator = t + 1;
            }
        }

        /// <summary>The braces of <paramref name="name"/>'s body, or null.</summary>
        public (int Open, int Close)? FunctionBody(string name)
        {
            for (int t = 0; t + 1 < Tokens.Count; t++)
            {
                if (Tokens[t].Kind != CTokenKind.Identifier || !Tokens[t].Is(Text, name) || !Is(t + 1, "(")) continue;
                int parameters = Match(t + 1);
                if (parameters < 0 || !Is(parameters + 1, "{")) continue;
                int close = Match(parameters + 1);
                if (close >= 0) return (Tokens[parameters + 1].Start, Tokens[close].Start);
            }
            return null;
        }

        /// <summary>Every use of <paramref name="name"/> followed by an argument list, in file order.</summary>
        public List<CCall> Calls(string name)
        {
            List<CCall> calls = new List<CCall>();
            for (int t = 0; t + 1 < Tokens.Count; t++)
            {
                if (Tokens[t].Kind != CTokenKind.Identifier || !Tokens[t].Is(Text, name) || !Is(t + 1, "(")) continue;
                int close = Match(t + 1);
                if (close < 0) continue;
                List<(int Start, int End)> args = new List<(int Start, int End)>();
                int first = t + 2, depth = 0;
                for (int k = first; k <= close; k++)
                {
                    if (k < close && (Is(k, "(") || Is(k, "[") || Is(k, "{"))) depth++;
                    else if (k < close && (Is(k, ")") || Is(k, "]") || Is(k, "}"))) depth--;
                    if (k < close && (depth != 0 || !Is(k, ","))) continue;
                    if (k > first) args.Add((Tokens[first].Start, Tokens[k - 1].End));
                    first = k + 1;
                }
                calls.Add(new CCall(Tokens[t].Start, Tokens[close].End, args));
            }
            return calls;
        }

        /// <summary>The name in a <c>.name =</c> opening the range, or null when it opens with anything else.</summary>
        public static string LeadingField(string text, int start, int end)
        {
            List<CToken> tokens = CLexer.Tokenize(text, start, Math.Min(end, start + 256));
            bool named = tokens.Count >= 3 && tokens[0].Is(text, ".") && tokens[1].Kind == CTokenKind.Identifier && tokens[2].Is(text, "=");
            return named ? tokens[1].Text(text) : null;
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
