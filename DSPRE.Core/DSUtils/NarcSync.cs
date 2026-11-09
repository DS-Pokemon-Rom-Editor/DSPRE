using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using DSPRE.HgEngine;
using NarcAPI;
using static DSPRE.RomInfo;

namespace DSPRE
{
    /// <summary>
    /// Keeps each packed archive and its unpacked working folder from drifting apart unnoticed. Editors work on the
    /// folder and Save ROM packs it over the archive, so an archive replaced outside DSPRE would silently lose to an
    /// older folder. A fingerprint of the archive is kept from the last time DSPRE unpacked or packed it; an archive
    /// that no longer matches it was changed elsewhere, and the user says which side is the newer one.
    /// </summary>
    public static class NarcSync
    {
        public enum Drift
        {
            /// <summary>The archive changed since DSPRE last unpacked or packed it.</summary>
            ArchiveChanged,
            /// <summary>No fingerprint was kept yet, and the archive and folder hold different files.</summary>
            Differs,
        }

        public sealed class Conflict
        {
            public DirNames Dir { get; init; }
            public Drift Drift { get; init; }
            public string PackedPath { get; init; }
            public string UnpackedPath { get; init; }
            /// <summary>The archive's path inside the project, for showing to the user.</summary>
            public string Name { get; init; }
            /// <summary>What the archive holds, from DSPRE's own name for it ("Pokedex data").</summary>
            public string Label
            {
                get
                {
                    string id = Dir.ToString();
                    System.Text.StringBuilder sb = new System.Text.StringBuilder();
                    for (int i = 0; i < id.Length; i++)
                    {
                        if (i > 0 && char.IsUpper(id[i]) && !char.IsUpper(id[i - 1])) sb.Append(' ');
                        sb.Append(i == 0 ? char.ToUpperInvariant(id[i]) : char.IsUpper(id[i]) && i + 1 < id.Length && !char.IsUpper(id[i + 1]) ? char.ToLowerInvariant(id[i]) : id[i]);
                    }
                    return sb.ToString();
                }
            }
        }

        public enum Choice { KeepFolder, UseArchive, Later }

        /// <summary>
        /// Set by the UI: asks which side of each conflict to keep. Called from any thread with whether Save ROM is
        /// waiting on the answer; returns a choice per conflict in the same order, or null when the user cancelled.
        /// </summary>
        public static Func<IReadOnlyList<Conflict>, bool, IReadOnlyList<Choice>> AskHook;

        /// <summary>What settling the archives came to.</summary>
        public sealed class Outcome
        {
            /// <summary>Archives the user left undecided, or all of them when the question was cancelled.</summary>
            public List<string> Undecided { get; } = new List<string>();
            /// <summary>Archives unpacked over their folder, whose editors now show the outside version.</summary>
            public List<string> Unpacked { get; } = new List<string>();
            public bool Settled => Undecided.Count == 0;
        }

        private sealed class Stamp
        {
            public long Size { get; set; }
            public long Written { get; set; }
            public string Sha256 { get; set; }
            /// <summary>When the folder and archive were last in step. A member written later is DSPRE's edit.</summary>
            public long Recorded { get; set; }
            /// <summary>When the folder was last unpacked whole from the archive. A member written later may hold DSPRE's work.</summary>
            public long Extracted { get; set; }
        }

        private static readonly object Gate = new object();
        private static Dictionary<string, Stamp> _stamps;
        private static string _stampsFor;

        private static string StampsPath => string.IsNullOrEmpty(DspreDir) ? null : Path.Combine(DspreDir, "narc-sync.json");

        private static Dictionary<string, Stamp> Stamps()
        {
            string path = StampsPath;
            if (_stamps != null && _stampsFor == path) return _stamps;
            _stampsFor = path;
            _stamps = new Dictionary<string, Stamp>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (path != null && File.Exists(path))
                    _stamps = PlainFiles.ReadJson<Dictionary<string, Stamp>>(path)
                              ?? _stamps;
            }
            catch (Exception ex) when (ex is IOException || ex is JsonException || ex is UnauthorizedAccessException)
            {
                AppLogger.Warn("The archive fingerprints couldn't be read, so they start over: " + ex.Message);
            }
            return _stamps;
        }

        private static void WriteStamps()
        {
            string path = StampsPath;
            if (path == null) return;
            try { PlainFiles.WriteJson(path, _stamps); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                AppLogger.Warn("The archive fingerprints couldn't be saved: " + ex.Message);
            }
        }

        private static bool TryPair(string packedPath, string unpackedPath, out DirNames dir)
        {
            dir = default;
            foreach (DirNames candidate in ArchiveMembers.DirsOfArchive(packedPath))
            {
                if (unpackedPath != null && !ProjectPaths.Same(GameDirs[candidate].unpackedDir, unpackedPath)) continue;
                dir = candidate;
                return true;
            }
            return false;
        }

        private static string Key(string packedPath)
        {
            string root = string.IsNullOrEmpty(WorkDir) ? null : Path.GetFullPath(WorkDir);
            string full = Path.GetFullPath(packedPath);
            return root != null && full.StartsWith(root, StringComparison.OrdinalIgnoreCase)
                ? full.Substring(root.Length).TrimStart('\\', '/').Replace('\\', '/')
                : full;
        }

        private static Stamp Measure(string packedPath, bool hash)
        {
            FileInfo fi = new FileInfo(packedPath);
            Stamp s = new Stamp { Size = fi.Length, Written = fi.LastWriteTimeUtc.Ticks };
            if (hash) s.Sha256 = FileHash.Sha256(packedPath);
            return s;
        }

        private static void Record(string packedPath, bool extracted = false)
        {
            try
            {
                Stamp s = Measure(packedPath, hash: true);
                s.Recorded = DateTime.UtcNow.Ticks;
                lock (Gate)
                {
                    Stamps().TryGetValue(Key(packedPath), out Stamp old);
                    s.Extracted = extracted ? s.Recorded : old?.Extracted ?? 0;
                    Stamps()[Key(packedPath)] = s;
                    WriteStamps();
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                AppLogger.Warn("Couldn't fingerprint " + packedPath + ": " + ex.Message);
            }
        }

        /// <summary>Called by <see cref="Narc.Save"/>: packing the project's own folder over its archive puts the two in step.</summary>
        internal static void NoteSaved(string sourceFolder, string packedPath)
        {
            if (sourceFolder != null && TryPair(packedPath, sourceFolder, out _)) Record(packedPath);
        }

        /// <summary>Called by <see cref="Narc.ExtractToFolder"/>: unpacking an archive into its own folder puts the two in step.</summary>
        internal static void NoteExtracted(string sourceFile, string folder)
        {
            if (sourceFile != null && TryPair(sourceFile, folder, out _)) Record(sourceFile, extracted: true);
        }

        /// <summary>The archive and folder that have drifted apart, for the given archives or all of them.</summary>
        public static List<Conflict> Find(IEnumerable<DirNames> dirs = null)
        {
            List<Conflict> found = new List<Conflict>();
            if (GameDirs == null || string.IsNullOrEmpty(WorkDir)) return found;
            IEnumerable<DirNames> which = dirs ?? GameDirs.Keys;
            foreach (DirNames dir in which.Distinct())
            {
                // hg-engine builds these from its own source, never from the folder.
                if (HgEngineDomains.IsOwned(dir)) continue;
                if (!GameDirs.TryGetValue(dir, out (string packedDir, string unpackedDir) p)) continue;
                try
                {
                    Drift? drift;
                    // An archive still being unpacked would look like a folder that differs.
                    lock (DSUtils.UnpackLockFor(dir))
                    {
                        if (!File.Exists(p.packedDir) || !ArchiveMembers.HasMembers(p.unpackedDir)) continue;
                        drift = Check(p.packedDir, p.unpackedDir);
                        if (drift != null && TakeBuiltMembers(dir, p.packedDir, p.unpackedDir)) drift = null;
                    }
                    if (drift != null)
                        found.Add(new Conflict { Dir = dir, Drift = drift.Value, PackedPath = p.packedDir, UnpackedPath = p.unpackedDir, Name = Key(p.packedDir) });
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is InvalidDataException)
                {
                    AppLogger.Warn("Couldn't compare " + p.packedDir + " with its folder: " + ex.Message);
                }
            }
            return found;
        }

        /// <summary>
        /// The build's copy wins for members DSPRE hasn't edited since the last sync. False when an edit would be lost or the folder couldn't be backed up.
        /// </summary>
        private static bool TakeBuiltMembers(DirNames dir, string packedPath, string folder)
        {
            if (!HgEngineProject.IsActive) return false;
            string archive = HgEngineOwnedFiles.ArchiveOfPath(packedPath);
            // The synthetic overlay is repacked whole from build/a028, outside the rule table.
            HgEngineRule rule = HgEngineOwnedFiles.RuleForArchive(archive);
            bool whole = dir == DirNames.SynthOverlay || rule?.ReplacesWholeArchive == true;
            if (!whole && rule == null) return false;

            long recorded;
            lock (Gate) recorded = Stamps().TryGetValue(Key(packedPath), out Stamp known) ? known.Recorded : 0;
            bool BinEdited(int member)
            {
                string bin = ArchiveMembers.PathOf(dir, member);
                return File.Exists(bin) && File.GetLastWriteTimeUtc(bin).Ticks > recorded;
            }
            // Reading a text bank writes its JSON too, so a newer JSON is an edit only when its text differs from the bank's.
            bool Edited(int member)
            {
                if (BinEdited(member)) return true;
                if (dir != DirNames.TextArchives) return false;
                string json = ROMFiles.TextArchive.GetFilePaths(member).jsonPath;
                return File.Exists(json) && File.GetLastWriteTimeUtc(json).Ticks > recorded && !JsonMatchesBank(json, ArchiveMembers.PathOf(dir, member));
            }

            Narc packed = Narc.Open(packedPath);
            if (packed == null) return false;
            Narc unpacked = Narc.FromFolder(folder);
            try
            {
                // DSPRE saves edits to hg-engine's members in its source, so the build's copy is current. Other members
                // DSPRE edited since the last sync are asked about.
                if (whole && packed.ElementCount != unpacked.ElementCount)
                {
                    // Without a backup the user is asked instead.
                    if (!BackUpFolder(packedPath, folder, packed)) return false;
                    ReplaceFolder(packedPath, packed, folder);
                    return true;
                }
                // A build can add members, such as new text banks. A folder longer than the build holds DSPRE's work.
                if (packed.ElementCount < unpacked.ElementCount) return false;
                List<int> differing = new List<int>();
                for (int i = 0; i < packed.ElementCount; i++)
                    if (i >= unpacked.ElementCount || !packed.GetElementBytes(i).AsSpan().SequenceEqual(unpacked.GetElementBytes(i))) differing.Add(i);
                if (!whole && differing.Any(i => i < unpacked.ElementCount && !HgEngineOwnedFiles.Owns(archive, i) && Edited(i))) return false;

                // The edit check goes by file dates, which a copied or restored folder can get wrong.
                if (differing.Count > 0 && !BackUpFolder(packedPath, folder, packed)) return false;
                foreach (int i in differing)
                {
                    ArchiveMembers.PutBuilt(dir, i, packed.GetElementBytes(i));
                    // Text is read from its JSON first, so the old copy has to go for the new member to be seen.
                    if (dir == DirNames.TextArchives) File.Delete(ROMFiles.TextArchive.GetFilePaths(i).jsonPath);
                }
            }
            finally
            {
                packed.Free();
                unpacked.Free();
            }
            Record(packedPath);
            return true;
        }

        private static bool JsonMatchesBank(string json, string bin)
        {
            if (!File.Exists(bin)) return false;
            // The messages are compared, so a JSON that differs only in layout or line endings still matches.
            try { return ROMFiles.TextArchive.ReadMessagesFrom(json).SequenceEqual(ROMFiles.TextArchive.ReadMessagesFrom(bin)); }
            catch (Exception e) when (e is IOException || e is InvalidDataException) { return false; }
        }

        private static Drift? Check(string packedPath, string folder)
        {
            string key = Key(packedPath);
            Stamp known;
            lock (Gate) Stamps().TryGetValue(key, out known);

            if (known != null)
            {
                Stamp quick = Measure(packedPath, hash: false);
                if (quick.Size == known.Size && quick.Written == known.Written) return null;
                Stamp now = Measure(packedPath, hash: true);
                if (string.Equals(now.Sha256, known.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    now.Recorded = known.Recorded;
                    lock (Gate) { Stamps()[key] = now; WriteStamps(); }
                    return null;
                }
                return Drift.ArchiveChanged;
            }

            // No fingerprint yet: the two agree when they hold the same members.
            if (SameMembers(packedPath, folder)) { Record(packedPath); return null; }
            return Drift.Differs;
        }

        private static bool SameMembers(string packedPath, string folder)
        {
            Narc packed = Narc.Open(packedPath);
            if (packed == null) return true; // Not an archive DSPRE reads; nothing to compare.
            Narc unpacked = Narc.FromFolder(folder);
            try
            {
                if (packed.ElementCount != unpacked.ElementCount) return false;
                for (int i = 0; i < packed.ElementCount; i++)
                    if (!packed.GetElementBytes(i).AsSpan().SequenceEqual(unpacked.GetElementBytes(i))) return false;
                return true;
            }
            finally
            {
                packed.Free();
                unpacked.Free();
            }
        }

        /// <summary>
        /// Asks about any drifted archives among <paramref name="dirs"/> (all when null) and applies the answers.
        /// <paramref name="saving"/> says Save ROM is waiting, which packs every folder over its archive next.
        /// </summary>
        public static Outcome Settle(IEnumerable<DirNames> dirs = null, bool saving = false)
        {
            lock (Asking) return SettleNow(dirs, saving);
        }

        private static Outcome SettleNow(IEnumerable<DirNames> dirs, bool saving)
        {
            using IDisposable run = BackupRun();
            Outcome outcome = new Outcome();
            List<Conflict> conflicts = Find(dirs);
            if (conflicts.Count == 0) return outcome;
            // Save ROM asks again, since it packs every folder over its archive.
            if (!saving) conflicts.RemoveAll(IsPutOff);
            if (conflicts.Count == 0) return outcome;
            IReadOnlyList<Choice> choices = AskHook?.Invoke(conflicts, saving);
            if (choices == null || choices.Count != conflicts.Count)
            {
                foreach (Conflict c in conflicts) PutOff(c);
                outcome.Undecided.AddRange(conflicts.Select(c => c.Name));
                return outcome;
            }

            for (int i = 0; i < conflicts.Count; i++)
            {
                if (choices[i] == Choice.Later) { PutOff(conflicts[i]); outcome.Undecided.Add(conflicts[i].Name); continue; }
                Apply(conflicts[i], choices[i]);
                if (choices[i] == Choice.UseArchive) outcome.Unpacked.Add(conflicts[i].Name);
            }
            return outcome;
        }

        private static readonly object Asking = new object();

        // Archives answered "later", keyed by path with their size and date at the time, so a change asks again.
        private static readonly Dictionary<string, (long Size, long Written)> PutOffs = new Dictionary<string, (long, long)>(StringComparer.OrdinalIgnoreCase);

        private static void PutOff(Conflict c)
        {
            try
            {
                FileInfo fi = new FileInfo(c.PackedPath);
                lock (PutOffs) PutOffs[Path.GetFullPath(c.PackedPath)] = (fi.Length, fi.LastWriteTimeUtc.Ticks);
            }
            // An archive that can't be read now isn't remembered, so the next sync just asks again.
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { }
        }

        private static bool IsPutOff(Conflict c)
        {
            try
            {
                FileInfo fi = new FileInfo(c.PackedPath);
                lock (PutOffs)
                    return PutOffs.TryGetValue(Path.GetFullPath(c.PackedPath), out (long Size, long Written) then)
                        && then == (fi.Length, fi.LastWriteTimeUtc.Ticks);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { return false; }
        }

        /// <summary>
        /// <see cref="Settle"/> for the editors' unpack path. While another question is open this does nothing, since
        /// waiting could hold up the UI thread that is showing it; the archive is checked again next time.
        /// </summary>
        public static void SettleIfFree(IEnumerable<DirNames> dirs)
        {
            if (AskHook == null || !System.Threading.Monitor.TryEnter(Asking)) return;
            try { SettleNow(dirs, saving: false); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is InvalidDataException)
            {
                AppLogger.Error("Settling a changed archive failed: " + ex.Message);
            }
            finally { System.Threading.Monitor.Exit(Asking); }
        }

        /// <summary>
        /// Hashes of the archives whose unpacked folders are in sync, taken before make extracts base/ again. An archive
        /// changed outside DSPRE is left out, so it is still asked about.
        /// </summary>
        public static Dictionary<DirNames, string> InSyncArchives()
        {
            Dictionary<DirNames, string> found = HashUnpackedArchives(_ => true);
            foreach ((DirNames dir, string now) in found.ToList())
            {
                Stamp known;
                lock (Gate) Stamps().TryGetValue(Key(GameDirs[dir].packedDir), out known);
                if (known == null || !string.Equals(now, known.Sha256, StringComparison.OrdinalIgnoreCase)) found.Remove(dir);
            }
            return found;
        }

        /// <summary>The packed archives that have an unpacked folder, by hash, for <see cref="Changed"/> after a build.</summary>
        public static Dictionary<DirNames, string> HashUnpackedArchives(Func<DirNames, bool> include)
        {
            Dictionary<DirNames, string> hashes = new Dictionary<DirNames, string>();
            if (GameDirs == null) return hashes;
            foreach ((DirNames dir, (string packedDir, string unpackedDir) p) in GameDirs)
            {
                if (!include(dir) || string.IsNullOrEmpty(p.packedDir) || !File.Exists(p.packedDir) || !ArchiveMembers.HasMembers(p.unpackedDir)) continue;
                try { hashes[dir] = FileHash.Sha256(p.packedDir); }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { AppLogger.Error($"Couldn't read {p.packedDir}: {ex.Message}"); }
            }
            return hashes;
        }

        /// <summary>The archives in <paramref name="before"/> whose packed file is different now.</summary>
        public static List<DirNames> Changed(Dictionary<DirNames, string> before)
        {
            List<DirNames> changed = new List<DirNames>();
            foreach ((DirNames dir, string hash) in before)
            {
                string packed = GameDirs[dir].packedDir;
                try
                {
                    if (File.Exists(packed) && !string.Equals(FileHash.Sha256(packed), hash, StringComparison.OrdinalIgnoreCase)) changed.Add(dir);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { AppLogger.Error($"Couldn't read {packed}: {ex.Message}"); }
            }
            return changed;
        }

        /// <summary>
        /// After make extracts base/ again, marks each archive in <paramref name="before"/> that the extract changed, so the
        /// next save packs its unpacked folder over it without asking.
        /// </summary>
        public static void TrustFolders(Dictionary<DirNames, string> before)
        {
            foreach (DirNames dir in Changed(before)) Record(GameDirs[dir].packedDir);
        }

        private const int BackupsKept = 20;

        // Answered conflicts and folders replaced by a build are kept apart, so builds can't push out a conflict's backup.
        private const string ConflictBackups = "archives", BuildBackups = "rebuilt";

        [ThreadStatic] private static Dictionary<string, string> _run;

        /// <summary>Until disposed, puts every backup in one folder per kind.</summary>
        public static IDisposable BackupRun()
        {
            if (_run != null) return new OnDispose(null);
            _run = new Dictionary<string, string>();
            return new OnDispose(() => _run = null);
        }

        private static string BackupsFolder(string kind)
        {
            if (_run != null && _run.TryGetValue(kind, out string open)) return open;
            string root = ProjectBackups.Folder(DspreDir, kind);
            string folder = Path.Combine(root, DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
            Directory.CreateDirectory(folder);
            if (_run != null) _run[kind] = folder;
            // Only build backups are pruned: a conflict's backup is kept until the user removes it.
            if (kind != BuildBackups) return folder;
            try
            {
                foreach (string old in Directory.GetDirectories(root).OrderByDescending(d => d, StringComparer.Ordinal).Skip(BackupsKept))
                    Directory.Delete(old, recursive: true);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                AppLogger.Warn("Old archive backups couldn't be removed: " + ex.Message);
            }
            return folder;
        }

        private static string BackupName(string packedPath) => ProjectBackups.FlatName(Key(packedPath));

        /// <summary>Backs up a folder's edited members before <paramref name="incoming"/> replaces them. False when that failed.</summary>
        public static bool BackUpFolder(string packedPath, string unpackedPath, Narc incoming)
        {
            if (!ArchiveMembers.HasMembers(unpackedPath)) return true;
            if (!HoldsEdits(packedPath, unpackedPath, incoming)) return true;
            if (string.IsNullOrEmpty(DspreDir)) return false;
            try
            {
                WriteFolderBackup(BuildBackups, packedPath, unpackedPath);
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                AppLogger.Warn($"Couldn't back up {unpackedPath}: {ex.Message}");
                return false;
            }
        }

        // Without a record of when the folder was unpacked, every member counts as edited.
        private static bool HoldsEdits(string packedPath, string folder, Narc incoming)
        {
            long extracted;
            lock (Gate) extracted = Stamps().TryGetValue(Key(packedPath), out Stamp known) ? known.Extracted : 0;
            if (extracted == 0 || !TryPair(packedPath, folder, out DirNames dir)) return true;
            try
            {
                foreach (string file in Directory.EnumerateFiles(folder))
                {
                    if (!int.TryParse(Path.GetFileName(file), out int member)) continue;
                    if (dir == DirNames.TextArchives)
                    {
                        string json = ROMFiles.TextArchive.GetFilePaths(member).jsonPath;
                        if (File.Exists(json) && File.GetLastWriteTimeUtc(json).Ticks > extracted && !JsonMatchesBank(json, file)) return true;
                    }
                    if (File.GetLastWriteTimeUtc(file).Ticks <= extracted) continue;
                    if (incoming == null || member >= incoming.ElementCount) return true;
                    if (!File.ReadAllBytes(file).AsSpan().SequenceEqual(incoming.GetElementBytes(member))) return true;
                }
                return false;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { return true; }
        }

        private static void WriteFolderBackup(string kind, string packedPath, string unpackedPath)
        {
            Narc folder = Narc.FromFolder(unpackedPath);
            try { PlainFiles.WriteBytes(Path.Combine(BackupsFolder(kind), BackupName(packedPath) + ".folder.narc"), folder.ToBytes()); }
            finally { folder.Free(); }
        }

        // Not ExtractToFolder: it asks before emptying hg-engine's folders, and this runs off the UI thread.
        private static void ReplaceFolder(string packedPath, Narc packed, string folder)
        {
            ArchiveMembers.ReplaceAll(folder, Enumerable.Range(0, packed.ElementCount).Select(packed.GetElementBytes).ToList());
            Record(packedPath, extracted: true);
        }

        private static void Apply(Conflict c, Choice choice)
        {
            if (choice == Choice.KeepFolder)
            {
                // The archive is about to be replaced by the folder, so keep the outside version.
                PlainFiles.Copy(c.PackedPath, Path.Combine(BackupsFolder(ConflictBackups), BackupName(c.PackedPath) + ".archive.narc"), overwrite: true);
                Narc.FromFolder(c.UnpackedPath).Save(c.PackedPath);
                AppLogger.Info($"{c.Name}: kept the unpacked folder and packed it over the archive.");
            }
            else
            {
                // The folder is about to be replaced by the archive, so keep DSPRE's version, packed.
                WriteFolderBackup(ConflictBackups, c.PackedPath, c.UnpackedPath);
                Narc packed = Narc.Open(c.PackedPath) ?? throw new InvalidDataException(c.Name + " isn't a valid archive.");
                try { ReplaceFolder(c.PackedPath, packed, c.UnpackedPath); }
                finally { packed.Free(); }
                AppLogger.Info($"{c.Name}: unpacked the archive over the folder.");
            }
        }
    }
}
