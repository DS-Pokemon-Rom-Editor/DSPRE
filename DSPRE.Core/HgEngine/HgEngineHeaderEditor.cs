using System;

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

        /// <summary>Appends a new array entry before the file's last "};".</summary>
        public static bool TryInsertBeforeFinalCloseBrace(ref string text, string newEntry)
        {
            int insertAt = text.LastIndexOf("};", StringComparison.Ordinal);
            if (insertAt < 0) return false;
            text = string.Concat(text.AsSpan(0, insertAt), newEntry, text.AsSpan(insertAt));
            return true;
        }
    }
}
