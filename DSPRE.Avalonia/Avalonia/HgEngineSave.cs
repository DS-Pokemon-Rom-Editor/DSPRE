using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSPRE.HgEngine;

namespace DSPRE.Avalonia
{
    /// <summary>
    /// Runs an editor's hg-engine source writes as one save. Nothing reaches disk unless every write
    /// succeeds, and when the save would delete comments the user decides whether they are kept at the
    /// end of their file.
    /// </summary>
    public static class HgEngineSave
    {
        /// <summary>Saved is false with a null error when the user cancelled.</summary>
        public static async Task<(bool saved, string error)> RunAsync(Func<string> write)
        {
            var session = HgEngineWriteSession.Begin();
            try
            {
                string error;
                try { error = write(); }
                catch (Exception ex)
                {
                    AppLogger.Error("hg-engine save failed: " + ex);
                    error = ex.Message;
                }
                // Anything that runs while the question is open must read and write the real files.
                session.StopRecording();
                if (error != null) return (false, error);

                var lost = session.LostComments();
                bool keep = false;
                if (lost.Count > 0)
                {
                    var answer = await DialogHelper.AskThreeWay(Describe(lost), "Comments in source", "Keep comments", "Delete comments");
                    if (answer == DialogHelper.MsgResult.Cancel) return (false, null);
                    keep = answer == DialogHelper.MsgResult.Yes;
                }
                return session.Commit(keep, out string commitError) ? (true, null) : (false, commitError);
            }
            finally { session.Dispose(); }
        }

        private static string Describe(IReadOnlyList<HgEngineLostComment> lost)
        {
            var files = lost.Select(c => Path.GetFileName(c.FilePath)).Distinct().ToList();
            var sb = new StringBuilder();
            sb.Append($"Saving removes {lost.Count} comment{(lost.Count == 1 ? "" : "s")} from {string.Join(", ", files)}:\n\n");
            foreach (var c in lost.Take(6))
            {
                string where = c.Place.Length > 0 ? $"{c.Entry} {c.Place}" : c.Entry;
                string text = c.Text.Length > 80 ? c.Text.Substring(0, 77) + "..." : c.Text;
                sb.Append($"{where}: {text}\n");
            }
            if (lost.Count > 6) sb.Append($"and {lost.Count - 6} more\n");
            sb.Append("\nKeep them as comments at the end of the file?");
            return sb.ToString();
        }
    }
}
