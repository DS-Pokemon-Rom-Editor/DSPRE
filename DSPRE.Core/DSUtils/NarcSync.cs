using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
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
        }

        private static readonly object Gate = new object();
        private static Dictionary<string, Stamp> _stamps;
        private static string _stampsFor;

        private static string StampsPath => string.IsNullOrEmpty(dspreDir) ? null : Path.Combine(dspreDir, "narc-sync.json");

        private static Dictionary<string, Stamp> Stamps()
        {
            string path = StampsPath;
            if (_stamps != null && _stampsFor == path) return _stamps;
            _stampsFor = path;
            _stamps = new Dictionary<string, Stamp>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (path != null && File.Exists(path))
                    _stamps = JsonSerializer.Deserialize<Dictionary<string, Stamp>>(File.ReadAllText(path))
                              ?? _stamps;
            }
            catch (Exception ex) when (ex is IOException || ex is JsonException || ex is UnauthorizedAccessException)
            {
                AppLogger.Warn("The archive fingerprints could not be read, so they start over: " + ex.Message);
            }
            return _stamps;
        }

        private static void WriteStamps()
        {
            string path = StampsPath;
            if (path == null) return;
            try { File.WriteAllText(path, JsonSerializer.Serialize(_stamps, new JsonSerializerOptions { WriteIndented = true })); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                AppLogger.Warn("The archive fingerprints could not be saved: " + ex.Message);
            }
        }

        private static bool TryPair(string packedPath, string unpackedPath, out DirNames dir)
        {
            dir = default;
            if (gameDirs == null || string.IsNullOrEmpty(packedPath)) return false;
            string packed = Path.GetFullPath(packedPath);
            string unpacked = unpackedPath == null ? null : Path.GetFullPath(unpackedPath).TrimEnd('\\', '/');
            foreach (KeyValuePair<DirNames, (string packedDir, string unpackedDir)> kv in gameDirs)
            {
                if (!string.Equals(Path.GetFullPath(kv.Value.packedDir), packed, StringComparison.OrdinalIgnoreCase)) continue;
                if (unpacked != null && !string.Equals(Path.GetFullPath(kv.Value.unpackedDir).TrimEnd('\\', '/'), unpacked, StringComparison.OrdinalIgnoreCase)) continue;
                dir = kv.Key;
                return true;
            }
            return false;
        }

        private static string Key(string packedPath)
        {
            string root = string.IsNullOrEmpty(workDir) ? null : Path.GetFullPath(workDir);
            string full = Path.GetFullPath(packedPath);
            return root != null && full.StartsWith(root, StringComparison.OrdinalIgnoreCase)
                ? full.Substring(root.Length).TrimStart('\\', '/').Replace('\\', '/')
                : full;
        }

        private static Stamp Measure(string packedPath, bool hash)
        {
            FileInfo fi = new FileInfo(packedPath);
            Stamp s = new Stamp { Size = fi.Length, Written = fi.LastWriteTimeUtc.Ticks };
            if (hash)
            {
                using FileStream fs = File.OpenRead(packedPath);
                s.Sha256 = Convert.ToHexString(SHA256.HashData(fs));
            }
            return s;
        }

        private static void Record(string packedPath)
        {
            try
            {
                Stamp s = Measure(packedPath, hash: true);
                lock (Gate) { Stamps()[Key(packedPath)] = s; WriteStamps(); }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                AppLogger.Warn("Could not fingerprint " + packedPath + ": " + ex.Message);
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
            if (sourceFile != null && TryPair(sourceFile, folder, out _)) Record(sourceFile);
        }

        private static bool HasMembers(string folder) => Directory.Exists(folder) && Directory.EnumerateFiles(folder).Any();

        /// <summary>The archive and folder that have drifted apart, for the given archives or all of them.</summary>
        public static List<Conflict> Find(IEnumerable<DirNames> dirs = null)
        {
            List<Conflict> found = new List<Conflict>();
            if (gameDirs == null || string.IsNullOrEmpty(workDir)) return found;
            IEnumerable<DirNames> which = dirs ?? gameDirs.Keys;
            foreach (DirNames dir in which.Distinct())
            {
                // hg-engine builds these from its own source, never from the folder.
                if (HgEngineDomains.IsOwned(dir)) continue;
                if (!gameDirs.TryGetValue(dir, out (string packedDir, string unpackedDir) p)) continue;
                try
                {
                    Drift? drift;
                    // An archive still being unpacked would look like a folder that differs.
                    lock (DSUtils.UnpackLockFor(dir))
                    {
                        if (!File.Exists(p.packedDir) || !HasMembers(p.unpackedDir)) continue;
                        drift = Check(p.packedDir, p.unpackedDir);
                    }
                    if (drift != null)
                        found.Add(new Conflict { Dir = dir, Drift = drift.Value, PackedPath = p.packedDir, UnpackedPath = p.unpackedDir, Name = Key(p.packedDir) });
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is InvalidDataException)
                {
                    AppLogger.Warn("Could not compare " + p.packedDir + " with its folder: " + ex.Message);
                }
            }
            return found;
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
            Outcome outcome = new Outcome();
            List<Conflict> conflicts = Find(dirs);
            if (conflicts.Count == 0) return outcome;
            IReadOnlyList<Choice> choices = AskHook?.Invoke(conflicts, saving);
            if (choices == null || choices.Count != conflicts.Count)
            {
                outcome.Undecided.AddRange(conflicts.Select(c => c.Name));
                return outcome;
            }

            for (int i = 0; i < conflicts.Count; i++)
            {
                if (choices[i] == Choice.Later) { outcome.Undecided.Add(conflicts[i].Name); continue; }
                Apply(conflicts[i], choices[i]);
                if (choices[i] == Choice.UseArchive) outcome.Unpacked.Add(conflicts[i].Name);
            }
            return outcome;
        }

        private static readonly object Asking = new object();

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

        private static void Apply(Conflict c, Choice choice)
        {
            string backups = Path.Combine(dspreDir, "backups", "archives", DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(backups);
            string backupName = c.Name.Replace('/', '_').Replace('\\', '_');
            if (choice == Choice.KeepFolder)
            {
                // The archive is about to be replaced by the folder, so keep the outside version.
                File.Copy(c.PackedPath, Path.Combine(backups, backupName + ".archive.narc"), overwrite: true);
                Narc.FromFolder(c.UnpackedPath).Save(c.PackedPath);
                AppLogger.Info($"{c.Name}: kept the unpacked folder and packed it over the archive.");
            }
            else
            {
                // The folder is about to be replaced by the archive, so keep DSPRE's version, packed.
                Narc folder = Narc.FromFolder(c.UnpackedPath);
                folder.Save(Path.Combine(backups, backupName + ".folder.narc"));
                folder.Free();
                Narc packed = Narc.Open(c.PackedPath) ?? throw new InvalidDataException(c.Name + " is not a valid archive.");
                packed.ExtractToFolder(c.UnpackedPath);
                packed.Free();
                AppLogger.Info($"{c.Name}: unpacked the archive over the folder.");
            }
        }

        /// <summary>Forgets the cached fingerprints, for a project switch.</summary>
        public static void Reset()
        {
            lock (Gate) { _stamps = null; _stampsFor = null; }
        }
    }
}
