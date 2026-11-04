using System.Collections.Generic;
using System.Linq;

namespace DSPRE.HgEngine
{
    /// <summary>One armips statement line: its code words, with comments left out and ".name" as one word.</summary>
    public sealed class AsmLine
    {
        /// <summary>Where the line starts.</summary>
        public int Start { get; init; }
        /// <summary>Where its newline is, or the end of the text.</summary>
        public int End { get; init; }
        public List<CToken> Words { get; } = new();

        public bool Is(string source, int word, string text) => word < Words.Count && Words[word].Is(source, text);
    }

    public enum AsmDefinitionKind
    {
        /// <summary><c>NAME equ value</c></summary>
        Equ,
        /// <summary><c>.equ NAME, value</c></summary>
        DotEqu,
        /// <summary><c>.definelabel NAME, value</c></summary>
        DefineLabel,
    }

    /// <summary>A named value and where its name and value sit. <see cref="Line"/> indexes <see cref="AsmSourceFile.Lines"/>.</summary>
    public sealed record AsmDefinition(AsmDefinitionKind Kind, string Name, int NameStart, int NameEnd, int ValueStart, int ValueEnd, int Line)
    {
        public string Value(string source) => source.Substring(ValueStart, ValueEnd - ValueStart);
    }

    /// <summary>A <c>.macro</c>: its name, parameters, the lines up to its end, and the word that ended it.</summary>
    public sealed record AsmMacro(string Name, List<string> Parameters, List<AsmLine> Body, string End);

    /// <summary>
    /// armips source read through <see cref="CLexer"/>, so strings and block and line comments are found the same way as in
    /// C. A statement is a line, and ';' starts a comment as well.
    /// </summary>
    public sealed class AsmSourceFile
    {
        public string Text { get; }
        /// <summary>The lines that hold code, in order.</summary>
        public List<AsmLine> Lines { get; } = new();
        private List<AsmDefinition> _definitions;
        private List<AsmMacro> _macros;

        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<string, AsmSourceFile> Parsed = new();

        /// <summary>Reads <paramref name="text"/> once and shares the result while that string is in use.</summary>
        public static AsmSourceFile For(string text) => Parsed.GetValue(text ?? "", t => new AsmSourceFile(t, 0, t.Length));

        /// <summary>Reads only the text from <paramref name="start"/> up to <paramref name="end"/>, such as one line.</summary>
        public AsmSourceFile(string text, int start, int end)
        {
            Text = text ?? "";
            AsmLine line = null;
            int pos = start;
            while (CLexer.FirstToken(Text, pos, end) is CToken token)
            {
                int lineStart = token.Start == 0 ? 0 : Text.LastIndexOf('\n', token.Start - 1) + 1;
                if (line == null || line.Start != lineStart)
                {
                    int newline = Text.IndexOf('\n', token.Start);
                    line = new AsmLine { Start = lineStart, End = newline < 0 ? Text.Length : newline };
                }
                if (token.Kind == CTokenKind.Punct && token.Is(Text, ";")) { pos = line.End; continue; }
                pos = token.End;

                List<CToken> words = line.Words;
                if (words.Count == 0) Lines.Add(line);
                // A dot right before a name is part of it: .equ, .word, .macro.
                if (token.Kind == CTokenKind.Identifier && words.Count > 0 && words[^1].Kind == CTokenKind.Punct
                    && words[^1].Is(Text, ".") && words[^1].End == token.Start)
                    words[^1] = new CToken(CTokenKind.Identifier, words[^1].Start, token.End - words[^1].Start);
                else words.Add(token);
            }
        }

        /// <summary>Every <c>equ</c>, <c>.equ</c> and <c>.definelabel</c>, in file order.</summary>
        public IReadOnlyList<AsmDefinition> Definitions => _definitions ??= FindDefinitions();

        /// <summary>The definition in <paramref name="text"/> from <paramref name="start"/> up to <paramref name="end"/>, such as
        /// one line, or null.</summary>
        public static AsmDefinition DefinitionIn(string text, int start, int end) => new AsmSourceFile(text, start, end).Definitions.FirstOrDefault();

        private List<AsmDefinition> FindDefinitions()
        {
            List<AsmDefinition> found = new List<AsmDefinition>();
            for (int i = 0; i < Lines.Count; i++)
            {
                List<CToken> w = Lines[i].Words;
                AsmDefinitionKind kind;
                int name;
                if (Lines[i].Is(Text, 0, ".equ")) { kind = AsmDefinitionKind.DotEqu; name = 1; }
                else if (Lines[i].Is(Text, 0, ".definelabel")) { kind = AsmDefinitionKind.DefineLabel; name = 1; }
                else if (Lines[i].Is(Text, 1, "equ")) { kind = AsmDefinitionKind.Equ; name = 0; }
                else continue;
                if (name >= w.Count || w[name].Kind != CTokenKind.Identifier) continue;
                int value = 2;
                if (kind != AsmDefinitionKind.Equ && Lines[i].Is(Text, value, ",")) value++;
                if (value >= w.Count) continue;
                found.Add(new AsmDefinition(kind, w[name].Text(Text), w[name].Start, w[name].End, w[value].Start, w[^1].End, i));
            }
            return found;
        }

        /// <summary>Every <c>.macro</c> that reaches a <c>.endmacro</c> or <c>.endm</c>, in file order.</summary>
        public IReadOnlyList<AsmMacro> Macros => _macros ??= FindMacros();

        private List<AsmMacro> FindMacros()
        {
            List<AsmMacro> macros = new List<AsmMacro>();
            for (int i = 0; i < Lines.Count; i++)
            {
                List<CToken> w = Lines[i].Words;
                if (!Lines[i].Is(Text, 0, ".macro") || w.Count < 2 || w[1].Kind != CTokenKind.Identifier) continue;
                int end = i + 1;
                while (end < Lines.Count && !Lines[end].Is(Text, 0, ".endmacro") && !Lines[end].Is(Text, 0, ".endm")) end++;
                if (end >= Lines.Count) break;
                List<string> parameters = w.Skip(2).Where(t => t.Kind == CTokenKind.Identifier).Select(t => t.Text(Text)).ToList();
                macros.Add(new AsmMacro(w[1].Text(Text), parameters, Lines.GetRange(i + 1, end - i - 1), Lines[end].Words[0].Text(Text)));
                i = end;
            }
            return macros;
        }
    }
}
