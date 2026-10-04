using System;

namespace DSPRE.HgEngine
{
    /// <summary>
    /// Every source edit is read back before it is written: the new text goes through the same reader the editor
    /// loads with, and the write only happens if that reader sees what the edit meant. A file whose layout DSPRE
    /// misreads is then left alone instead of being damaged.
    /// </summary>
    internal static class HgEngineVerifiedWrite
    {
        /// <param name="check">Reads the new text and returns why it doesn't hold the intended values, or null.</param>
        public static bool TryWrite(string path, string relPath, string newText, Func<string, string> check, out string error, bool keepLostComments = true)
        {
            string problem;
            try { problem = check(newText); }
            catch (Exception e) when (e is FormatException || e is ArgumentException || e is InvalidOperationException || e is IndexOutOfRangeException)
            { problem = e.Message; }
            if (problem != null)
            {
                error = $"{relPath} would not read back as saved ({problem}), so it was left unchanged.";
                return false;
            }
            HgEngineFileCache.WriteText(path, newText, keepLostComments: keepLostComments);
            error = null;
            return true;
        }
    }
}
