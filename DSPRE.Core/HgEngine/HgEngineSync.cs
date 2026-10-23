using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NarcAPI;
using static DSPRE.RomInfo;

namespace DSPRE.HgEngine
{
    /// <summary>Rebuilds hg-engine-owned domains from source and re-extracts their narc(s) into the
    /// normal <see cref="RomInfo.GameDirs"/> unpacked-dir paths, so every editor ViewModel keeps reading
    /// the same unpacked-dir files unchanged, just sourced from hg-engine's compiler instead of the
    /// packed ROM.</summary>
    public static class HgEngineSync
    {
        private static readonly object BuildLock = new();

        // Skips the make invocation when nothing changed since the last sync; a DSPRE write always
        // updates the source file's mtime, so this never serves stale data after a save.
        private static readonly Dictionary<HgEngineDomain, DateTime[]> LastSyncedMtimes = new();
        private static readonly HashSet<HgEngineDomain> SyncedOnceThisSession = new();

        internal static void ClearSyncState() { LastSyncedMtimes.Clear(); SyncedOnceThisSession.Clear(); _splitLearnsetsFrom = default; }

        private static string[] ExtraInputsFor(HgEngineDomainInfo domain) =>
            domain.Domain == HgEngineDomain.Species ? new[] { "data/learnsets/learnsets.json" } : Array.Empty<string>();

        /// <summary>Splits a requested DirNames list into hg-engine-owned (synced from source here) and
        /// the rest (returned for the caller's normal packed-ROM unpack path).</summary>
        public static List<DirNames> SyncOwnedAndReturnRemaining(List<DirNames> ids)
        {
            // An hg-engine folder project reads base/root, which already holds what the last make built.
            if (RomInfo.IsHgEngineBaseProject)
            {
                if (!ids.Contains(DirNames.Learnsets)) return ids;
                if (!SplitBuiltLearnsets(out string learnsetError)) AppLogger.Error($"hg-engine learnsets: {learnsetError}");
                return ids.Where(id => id != DirNames.Learnsets).ToList();
            }

            List<DirNames> remaining = new List<DirNames>();
            HashSet<HgEngineDomain> domainsToSync = new HashSet<HgEngineDomain>();

            foreach (DirNames id in ids)
            {
                HgEngineDomainInfo domain = HgEngineDomains.IsOwned(id) ? HgEngineDomains.ForDir(id) : null;
                if (domain != null) domainsToSync.Add(domain.Domain);
                else remaining.Add(id);
            }

            foreach (HgEngineDomain domainKey in domainsToSync)
            {
                HgEngineDomainInfo domain = HgEngineDomains.All.First(d => d.Domain == domainKey);
                if (!SyncDomain(domain, out string error))
                    AppLogger.Error($"hg-engine sync failed for {domain.Domain}: {error}");
            }

            return remaining;
        }

        /// <summary>Rebuilds one domain's make target and extracts every narc it produces into the
        /// corresponding gameDirs unpacked dir. `make` invocations against one checkout aren't safe to
        /// overlap, so concurrent syncs serialize on _buildLock.</summary>
        public static bool SyncDomain(HgEngineDomainInfo domain, out string error)
        {
            error = null;
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout is linked."; return false; }

            lock (BuildLock)
            {
                bool stillExtracted;
                DateTime[] currentMtimes = Array.Empty<DateTime>();

                if (domain.SyncOncePerSession)
                {
                    stillExtracted = SyncedOnceThisSession.Contains(domain.Domain) && domain.NarcByDir.Keys.All(dir =>
                        GameDirs.TryGetValue(dir, out (string packedDir, string unpackedDir) p) && Directory.Exists(p.unpackedDir) && Directory.GetFiles(p.unpackedDir).Length > 0);
                }
                else
                {
                    string[] inputPaths = new[] { domain.SourceFileRelPath }.Concat(ExtraInputsFor(domain)).ToArray();
                    currentMtimes = inputPaths
                        .Select(p => SafeMtime(Path.Combine(HgEngineProject.RepoPathUnc, p.Replace('/', Path.DirectorySeparatorChar))))
                        .ToArray();

                    bool unchanged = LastSyncedMtimes.TryGetValue(domain.Domain, out DateTime[] cached) && cached.SequenceEqual(currentMtimes);
                    stillExtracted = unchanged && domain.NarcByDir.Keys.All(dir =>
                        GameDirs.TryGetValue(dir, out (string packedDir, string unpackedDir) p) && Directory.Exists(p.unpackedDir) && Directory.GetFiles(p.unpackedDir).Length > 0);
                }

                if (stillExtracted) return true;

                if (!HgEngineBuild.RunMakeTargets(domain.MakeTargets, out _, out string stderr))
                {
                    error = stderr;
                    return false;
                }

                int speciesCount = -1;   // only needed for the learnsets transform, resolved lazily below

                foreach (KeyValuePair<DirNames, string> kv in domain.NarcByDir)
                {
                    if (!GameDirs.TryGetValue(kv.Key, out (string packedDir, string unpackedDir) paths))
                        continue;

                    string narcPath = Path.Combine(HgEngineProject.RepoPathUnc, kv.Value.Replace('/', Path.DirectorySeparatorChar));
                    if (!File.Exists(narcPath))
                    {
                        error = $"The build didn't make {narcPath}.";
                        return false;
                    }

                    // hg-engine repurposes this narc slot with a completely different binary layout
                    // (one combined table, not one file per species). See HgEngineLearnsets.
                    if (kv.Key == DirNames.Learnsets)
                    {
                        if (speciesCount < 0) speciesCount = ResolveSpeciesCount(domain);
                        if (speciesCount <= 0)
                        {
                            error = "Couldn't work out the species count for the learnsets sync.";
                            return false;
                        }
                        if (!HgEngineLearnsets.Sync(narcPath, HgEngineProject.RepoPathUnc, paths.unpackedDir, speciesCount, out string learnsetError))
                        {
                            error = learnsetError;
                            return false;
                        }
                        continue;
                    }

                    Narc opened = Narc.Open(narcPath);
                    if (opened == null)
                    {
                        error = $"Couldn't read the built NARC: {narcPath}";
                        return false;
                    }
                    opened.ExtractToFolder(paths.unpackedDir);

                    // Some ViewModels read gameDirs[...].packedDir's narc file directly instead of unpackedDir; refresh both.
                    try { PlainFiles.Copy(narcPath, paths.packedDir, overwrite: true); } catch (IOException) { }   // the packed copy is a convenience for older readers; the unpacked folder is already current
                }

                if (domain.SyncOncePerSession) SyncedOnceThisSession.Add(domain.Domain);
                else LastSyncedMtimes[domain.Domain] = currentMtimes;
                return true;
            }
        }

        private static (DateTime, long) _splitLearnsetsFrom;

        /// <summary>
        /// Splits base/root's built a/0/3/3, one table for every species, into the per-species files the editors
        /// read. Done again whenever the build replaced the table or the folder was emptied.
        /// </summary>
        private static bool SplitBuiltLearnsets(out string error)
        {
            error = null;
            (string packed, string unpacked) = GameDirs[DirNames.Learnsets];
            if (!File.Exists(packed)) { error = $"{packed} is missing."; return false; }
            (DateTime, long Length) stamp = (File.GetLastWriteTimeUtc(packed), new FileInfo(packed).Length);
            lock (BuildLock)
            {
                if (stamp == _splitLearnsetsFrom && Directory.Exists(unpacked) && Directory.GetFiles(unpacked).Length > 0) return true;
                Narc personal = File.Exists(GameDirs[DirNames.PersonalPokeData].packedDir) ? Narc.Open(GameDirs[DirNames.PersonalPokeData].packedDir) : null;
                if (personal == null) { error = "Couldn't read the species count from the personal data archive."; return false; }
                if (!HgEngineLearnsets.Sync(packed, HgEngineProject.RepoPathUnc, unpacked, personal.ElementCount, out error)) return false;
                _splitLearnsetsFrom = stamp;
                return true;
            }
        }

        private static DateTime SafeMtime(string path)
        {
            try { return File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue; }
            catch (Exception ex) { AppLogger.WarnOnce("HgEngineSync.SafeMtime", ex); return DateTime.MinValue; }
        }

        /// <summary>Species count for the learnsets split: the personal NARC's count, or what is already
        /// unpacked.</summary>
        private static int ResolveSpeciesCount(HgEngineDomainInfo domain)
        {
            if (domain.NarcByDir.TryGetValue(DirNames.PersonalPokeData, out string personalRel))
            {
                string personalPath = Path.Combine(HgEngineProject.RepoPathUnc, personalRel.Replace('/', Path.DirectorySeparatorChar));
                Narc personal = File.Exists(personalPath) ? Narc.Open(personalPath) : null;
                if (personal != null) return personal.ElementCount;
            }
            return GameDirs.TryGetValue(DirNames.PersonalPokeData, out (string packedDir, string unpackedDir) p) && Directory.Exists(p.unpackedDir)
                ? NarcAPI.Narc.MemberCount(p.unpackedDir)
                : -1;
        }
    }
}
