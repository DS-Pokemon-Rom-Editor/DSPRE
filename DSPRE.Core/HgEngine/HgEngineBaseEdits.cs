using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;

namespace DSPRE.HgEngine
{
    /// <summary>
    /// Keeps DSPRE's writes to base/ and base_dsrom/ outside the archives in .dspre/ and puts them back after make
    /// re-extracts base/.
    /// </summary>
    public static class HgEngineBaseEdits
    {
        public sealed class CodeSpan
        {
            public long Offset { get; set; }
            public string Bytes { get; set; }
        }

        public sealed class Journal
        {
            /// <summary>Code spans by file, relative to the checkout.</summary>
            public Dictionary<string, List<CodeSpan>> Code { get; set; } = new Dictionary<string, List<CodeSpan>>(StringComparer.OrdinalIgnoreCase);

            /// <summary>Whole files by path relative to the checkout, with the write time make compares.</summary>
            public Dictionary<string, long> Files { get; set; } = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

            /// <summary>When the base/ folder these edits were checked against was created. A new one means make extracted it again.</summary>
            public long BaseCreated { get; set; }
        }

        /// <summary>Code spans and files that base/ no longer holds.</summary>
        public sealed class Report
        {
            public List<string> CodeFiles { get; } = new List<string>();
            public int Spans { get; set; }
            public List<string> Files { get; } = new List<string>();
            /// <summary>Spans hg-engine now patches, dropped on Restore or Forget.</summary>
            public List<string> Skipped { get; } = new List<string>();
            /// <summary>Code files still compressed, offered again at the next build or folder open.</summary>
            public List<string> Waiting { get; } = new List<string>();
            /// <summary>Files that couldn't be read or written, with the reason.</summary>
            public List<string> Failed { get; } = new List<string>();
            /// <summary>Whether a file couldn't be read, so base/ is checked again next time.</summary>
            public bool Unread { get; set; }
            public bool Any => Spans > 0 || Files.Count > 0;

            /// <summary>Anything worth telling the user about: edits to write back, or edits that can't be.</summary>
            public bool AnyToTell => Any || Skipped.Count > 0 || Failed.Count > 0;
        }

        private enum Mode { Check, Restore, Forget }

        private static readonly object Gate = new object();
        private static bool _listening;

        // Per thread, so a save on another thread during a write-back is still kept.
        [ThreadStatic] private static bool _restoring;

        /// <summary>Starts recording DSPRE's writes. Safe to call more than once.</summary>
        public static void Listen()
        {
            lock (Gate)
            {
                if (_listening) return;
                _listening = true;
            }
            ProjectWrites.CodeWritten += OnCodeWritten;
            ProjectWrites.Written += OnWritten;
        }

        private static string Checkout => RomInfo.IsHgEngineBaseProject && HgEngineProject.IsActive ? HgEngineProject.RepoRootWindows : null;

        // make deletes base/ and creates it again when it extracts, so a new creation time marks a fresh extract. Files
        // written by make during a normal build don't change that time, so they are never offered as lost edits.
        private static long BaseCreated(string checkout)
        {
            try { return Directory.GetCreationTimeUtc(HgEngineFolder.BaseDir(checkout)).Ticks; }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { return 0; }
        }

        private static string Folder(string checkout) => Path.Combine(checkout, RomInfo.HgEngineDspreFolder, "base-edits");

        private static string JournalPath(string checkout) => Path.Combine(Folder(checkout), "edits.json");

        private static string JournalName(string checkout) => ProjectPaths.RelativeKey(checkout, JournalPath(checkout));

        private static string CopyPath(string checkout, string relative) => Path.Combine(Folder(checkout), "files", relative);

        /// <summary>The path relative to the checkout when it is inside base/ or base_dsrom/, else null.</summary>
        private static string Relative(string checkout, string path)
        {
            foreach (string tree in new[] { HgEngineBase.BaseDirName, HgEngineBase.DsRomDirName })
                if (ProjectPaths.IsInside(path, Path.Combine(checkout, tree))) return ProjectPaths.RelativeKey(checkout, Path.GetFullPath(path));
            return null;
        }

        private static void OnCodeWritten(string path, IReadOnlyList<(long Offset, int Length)> ranges, string by)
        {
            string checkout = Checkout;
            if (checkout == null || _restoring) return;
            string relative = Relative(checkout, path);
            if (relative == null) return;
            try
            {
                lock (Gate)
                {
                    Journal journal = Read(checkout);
                    // Writing a compressed copy back replaces the whole file, so the spans for the flat file no longer apply.
                    if (StillCompressed(path)) journal.Code.Remove(relative);
                    else
                    {
                        if (!journal.Code.TryGetValue(relative, out List<CodeSpan> spans)) journal.Code[relative] = spans = new List<CodeSpan>();
                        foreach ((long offset, int length) in ranges.Where(r => r.Length > 0)) Merge(spans, path, offset, length);
                    }
                    Write(checkout, journal);
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is JsonException || ex is FormatException)
            {
                NotKept(relative, ex);
            }
        }

        // Merged spans are read back from the file, which has the latest bytes for all of them.
        private static void Merge(List<CodeSpan> spans, string path, long offset, int length)
        {
            long start = offset, end = offset + length;
            foreach (CodeSpan s in spans.ToList())
            {
                long spanEnd = s.Offset + Convert.FromBase64String(s.Bytes).Length;
                if (s.Offset > end || spanEnd < start) continue;
                start = Math.Min(start, s.Offset);
                end = Math.Max(end, spanEnd);
                spans.Remove(s);
            }
            if (start >= new FileInfo(path).Length) return;
            byte[] bytes = DSUtils.ReadFromFile(path, start, end - start);
            if (bytes == null || bytes.Length == 0) return;
            spans.Add(new CodeSpan { Offset = start, Bytes = Convert.ToBase64String(bytes) });
            spans.Sort((a, b) => a.Offset.CompareTo(b.Offset));
        }

        private static void OnWritten(string path)
        {
            string checkout = Checkout;
            if (checkout == null || _restoring) return;
            string relative = Relative(checkout, path);
            if (relative == null) return;
            // Code is kept as spans and archives as unpacked folders.
            if (HgEngineWriteGuard.TryIdentify(path, out int _) || IsPackedArchive(path)) return;
            if (MakeWrites(path)) return;
            string name = Path.GetFileName(path);
            if (DSUtils.IsSideFile(name)) return;
            try
            {
                lock (Gate)
                {
                    string copy = CopyPath(checkout, relative);
                    Journal journal = Read(checkout);
                    if (File.Exists(path))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(copy));
                        PlainFiles.Copy(path, copy, overwrite: true);
                        journal.Files[relative] = File.GetLastWriteTimeUtc(path).Ticks;
                    }
                    else
                    {
                        // Moved or deleted, so there is nothing to put back.
                        if (!journal.Files.Remove(relative)) return;
                        if (File.Exists(copy)) File.Delete(copy);
                    }
                    Write(checkout, journal);
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is JsonException)
            {
                NotKept(relative, ex);
            }
        }

        private static int _toldNotKept;

        // Warns once a session, since without the copy the next re-extract loses the edit.
        private static void NotKept(string relative, Exception ex)
        {
            AppLogger.Warn($"hg-engine: couldn't keep a copy of the edit to {relative}: {ex.Message}");
            if (Interlocked.Exchange(ref _toldNotKept, 1) == 0)
                AppMessages.Warning("DSPRE couldn't back up its edits to base/, so they'll be lost the next time make extracts base/. "
                    + "Details are in the log.", "Edits to base/");
        }

        // make writes these itself on every build, so DSPRE's copy would only fight it.
        private static bool MakeWrites(string path) =>
            HgEngineOwnedFiles.RuleForArchive(HgEngineOwnedFiles.ArchiveOfPath(path)) != null;

        private static bool IsPackedArchive(string path) => ArchiveMembers.DirsOfArchive(path).Any();

        /// <summary>What DSPRE wrote that base/ no longer holds.</summary>
        public static Report Missing() => Compare(Mode.Check);

        /// <summary>Writes back what <see cref="Missing"/> reports.</summary>
        public static Report Restore() => Compare(Mode.Restore);

        /// <summary>Drops what <see cref="Missing"/> reports, for when base/ is kept as it is.</summary>
        public static Report Forget() => Compare(Mode.Forget);

        private static Report Compare(Mode mode)
        {
            Report report = new Report();
            string checkout = Checkout;
            if (checkout == null) return report;
            lock (Gate)
            {
                Journal journal;
                try { journal = Read(checkout); }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is JsonException)
                {
                    report.Failed.Add(JournalName(checkout) + ": " + ex.Message);
                    AppLogger.Warn("hg-engine: couldn't read the saved base/ edits: " + ex.Message);
                    return report;
                }

                // Claims are read again, since changed hg-engine tools are often why base/ was extracted.
                HgEngineClaimedRanges.ClearCache();
                long baseCreated = BaseCreated(checkout);
                if (mode == Mode.Check && journal.BaseCreated != 0 && journal.BaseCreated == baseCreated) return report;
                bool changed = false;
                _restoring = mode == Mode.Restore;
                try
                {
                    using (ProjectWrites.By("hg-engine: DSPRE's base/ edits written back"))
                    {
                        foreach ((string relative, List<CodeSpan> spans) in journal.Code)
                            changed |= Guarded(relative, report, () => CompareCode(checkout, relative, spans, mode, report));
                        foreach (string relative in journal.Files.Keys.ToList())
                            changed |= Guarded(relative, report, () => CompareFile(checkout, relative, journal, mode, report));
                    }
                }
                finally { _restoring = false; }

                // Records this base/ as checked, so the same differences aren't offered until make's next extract.
                // A file still compressed or unreadable is checked again later.
                if ((mode != Mode.Check || !report.AnyToTell) && report.Waiting.Count == 0 && !report.Unread && journal.BaseCreated != baseCreated)
                {
                    journal.BaseCreated = baseCreated;
                    changed = true;
                }
                if (!changed) return report;
                foreach (string empty in journal.Code.Where(kv => kv.Value.Count == 0).Select(kv => kv.Key).ToList()) journal.Code.Remove(empty);
                try { Write(checkout, journal); }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    report.Failed.Add(JournalName(checkout) + ": " + ex.Message);
                }
            }
            return report;
        }

        // One file that can't be read or written is listed and the rest carry on.
        private static bool Guarded(string relative, Report report, Func<bool> compare)
        {
            try { return compare(); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is FormatException)
            {
                report.Failed.Add($"{relative}: {ex.Message}");
                report.Unread = true;
                AppLogger.Warn($"hg-engine: {relative} couldn't be checked against the kept edits: {ex.Message}");
                return false;
            }
        }

        /// <summary>True when the kept spans changed.</summary>
        private static bool CompareCode(string checkout, string relative, List<CodeSpan> spans, Mode mode, Report report)
        {
            string path = Path.Combine(checkout, relative);
            if (!File.Exists(path)) return false;
            // A fresh extract is still compressed until make decompresses it, and DSPRE's offsets are for the flat file.
            if (StillCompressed(path)) { report.Waiting.Add(relative); return false; }
            bool code = HgEngineWriteGuard.TryIdentify(path, out int overlay);
            bool missing = false;
            List<CodeSpan> dropped = new List<CodeSpan>();
            foreach (CodeSpan span in spans)
            {
                byte[] kept = Convert.FromBase64String(span.Bytes);
                if (Matches(path, span.Offset, kept)) continue;
                if (span.Offset + kept.Length > new FileInfo(path).Length)
                {
                    // Writing it would lengthen a file that the extract made shorter.
                    report.Failed.Add($"{relative} at 0x{span.Offset:X}: the edit runs past the end of the file");
                    continue;
                }
                // Checks the claimed ranges directly, since the write guard would also show its refusal.
                if (code && HgEngineClaimedRanges.Claiming(overlay, span.Offset, kept.Length) != null)
                {
                    report.Skipped.Add($"{relative} at 0x{span.Offset:X}");
                    if (mode != Mode.Check) dropped.Add(span);
                    continue;
                }
                missing = true;
                report.Spans++;
                if (mode == Mode.Restore) DSUtils.WriteToFile(path, kept, (uint)span.Offset);
                else if (mode == Mode.Forget) dropped.Add(span);
            }
            if (missing) report.CodeFiles.Add(relative);
            foreach (CodeSpan s in dropped) spans.Remove(s);
            return dropped.Count > 0;
        }

        private static bool StillCompressed(string path)
        {
            if (!HgEngineWriteGuard.TryIdentify(path, out int overlay)) return false;
            if (overlay >= 0) return OverlayUtils.IsStillCompressed(overlay);
            return ProjectPaths.Same(path, RomInfo.Arm9Path)
                && ARM9.IsStillCompressed();
        }

        private static bool Matches(string path, long offset, byte[] kept)
        {
            if (offset + kept.Length > new FileInfo(path).Length) return false;
            byte[] now = DSUtils.ReadFromFile(path, offset, kept.Length);
            return now != null && now.AsSpan().SequenceEqual(kept);
        }

        /// <summary>True when the kept files changed.</summary>
        private static bool CompareFile(string checkout, string relative, Journal journal, Mode mode, Report report)
        {
            string copy = CopyPath(checkout, relative);
            string path = Path.Combine(checkout, relative);
            if (!File.Exists(copy) || MakeWrites(path))
            {
                journal.Files.Remove(relative);
                if (File.Exists(copy)) File.Delete(copy);
                return true;
            }
            if (File.Exists(path) && File.ReadAllBytes(path).AsSpan().SequenceEqual(File.ReadAllBytes(copy))) return false;
            report.Files.Add(relative);
            if (mode == Mode.Forget)
            {
                journal.Files.Remove(relative);
                File.Delete(copy);
                return true;
            }
            if (mode != Mode.Restore) return false;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            PlainFiles.Copy(copy, path, overwrite: true);
            // Keeps the original date so make still rebuilds the file when its sources are newer.
            File.SetLastWriteTimeUtc(path, new DateTime(journal.Files[relative], DateTimeKind.Utc));
            return false;
        }

        private static Journal Read(string checkout)
        {
            string path = JournalPath(checkout);
            if (!File.Exists(path)) return new Journal();
            Journal journal = PlainFiles.ReadJson<Journal>(path) ?? new Journal();
            journal.Code = new Dictionary<string, List<CodeSpan>>(journal.Code ?? new Dictionary<string, List<CodeSpan>>(), StringComparer.OrdinalIgnoreCase);
            journal.Files = new Dictionary<string, long>(journal.Files ?? new Dictionary<string, long>(), StringComparer.OrdinalIgnoreCase);
            return journal;
        }

        private static void Write(string checkout, Journal journal)
        {
            if (journal.BaseCreated == 0) journal.BaseCreated = BaseCreated(checkout);
            HgEngineProject.DspreFolder(checkout);
            Directory.CreateDirectory(Folder(checkout));
            PlainFiles.WriteJson(JournalPath(checkout), journal);
        }
    }
}
