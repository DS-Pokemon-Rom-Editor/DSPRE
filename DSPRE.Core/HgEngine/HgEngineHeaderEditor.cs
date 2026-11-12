using System;
using System.Collections.Generic;

namespace DSPRE.HgEngine
{
    /// <summary>Text-editing primitives shared by the Item/Move/Species "add new entry" expansion
    /// classes: replace a #define's value, insert before a #define, insert before the final "};".</summary>
    internal static class HgEngineHeaderEditor
    {
        /// <summary>Replaces a #define's value. Fails (no mutation) if the name isn't found.</summary>
        public static bool TryReplaceDefineValue(ref string text, string defineName, string newValue)
        {
            CDirective define = CSourceFile.For(text).FindDefine(defineName);
            if (define == null || define.Body.Length == 0) return false;
            text = string.Concat(text.AsSpan(0, define.BodyStart), newValue, text.AsSpan(define.BodyEnd));
            return true;
        }

        /// <summary>Inserts before the line that defines <paramref name="anchorDefineName"/>.</summary>
        public static bool TryInsertBeforeDefine(ref string text, string anchorDefineName, string newLines)
        {
            CDirective define = CSourceFile.For(text).FindDefine(anchorDefineName);
            if (define == null || define.Body.Length == 0) return false;
            text = string.Concat(text.AsSpan(0, define.Start), newLines, text.AsSpan(define.Start));
            return true;
        }

        public static string MatchTableSpacing(string text, string row)
        {
            int braceIndex = FinalCloseBraceIndex(text);
            if (braceIndex < 0) return "\n" + row;
            int indentStart = IndentStart(text, braceIndex);
            if (indentStart == 0 || text[indentStart - 1] != '\n') return "\n" + row;
            int prevLineEnd = indentStart - 1;
            while (prevLineEnd > 0 && (text[prevLineEnd - 1] == ' ' || text[prevLineEnd - 1] == '\t' || text[prevLineEnd - 1] == '\r')) prevLineEnd--;
            return prevLineEnd == 0 || text[prevLineEnd - 1] == '\n' ? row + "\n" : "\n" + row;
        }

        private static int FinalCloseBraceIndex(string source)
        {
            List<CToken> tokens = CSourceFile.For(source).Tokens;
            bool Punct(int t, string s) => tokens[t].Kind == CTokenKind.Punct && tokens[t].Is(source, s);
            int close = tokens.Count - 1;
            while (close > 0 && !(Punct(close, ";") && Punct(close - 1, "}"))) close--;
            return close <= 0 ? -1 : tokens[close - 1].Start;
        }

        private static int IndentStart(string source, int index)
        {
            while (index > 0 && (source[index - 1] == ' ' || source[index - 1] == '\t')) index--;
            return index;
        }

        public static bool TryInsertBeforeFinalCloseBrace(ref string text, string newEntry)
        {
            string source = text;
            int insertAt = FinalCloseBraceIndex(source);
            if (insertAt < 0) return false;
            int indentStart = IndentStart(source, insertAt);
            // Keep the brace's indent unless code shares its line.
            if (indentStart == 0 || source[indentStart - 1] == '\n') insertAt = indentStart;
            else if (!newEntry.StartsWith('\n')) newEntry = "\n" + newEntry;
            text = string.Concat(text.AsSpan(0, insertAt), newEntry, text.AsSpan(insertAt));
            return true;
        }
    }
}
