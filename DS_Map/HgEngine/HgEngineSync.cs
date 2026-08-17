using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NarcAPI;
using static DSPRE.RomInfo;

namespace DSPRE.HgEngine
{
    /// <summary>Rebuilds hg-engine-owned domains from source and re-extracts their narc(s) into the
    /// normal <see cref="RomInfo.gameDirs"/> unpacked-dir paths, so every editor ViewModel keeps reading
    /// the same unpacked-dir files unchanged, just sourced from hg-engine's compiler instead of the
    /// packed ROM.</summary>
    public static class HgEngineSync
    {
        private static readonly object _buildLock = new();

        // Skips the make invocation when nothing changed since the last sync; a DSPRE write always
        // updates the source file's mtime, so this never serves stale data after a save.
        private static readonly Dictionary<HgEngineDomain, DateTime[]> _lastSyncedMtimes = new();
        private static readonly HashSet<HgEngineDomain> _syncedOnceThisSession = new();

        internal static void ClearSyncState() { _lastSyncedMtimes.Clear(); _syncedOnceThisSession.Clear(); }

        private static string[] ExtraInputsFor(HgEngineDomainInfo domain) =>
            domain.Domain == HgEngineDomain.Species ? new[] { "data/learnsets/learnsets.json" } : Array.Empty<string>();

        /// <summary>Splits a requested DirNames list into hg-engine-owned (synced from source here) and
        /// the rest (returned for the caller's normal packed-ROM unpack path).</summary>
        public static List<DirNames> SyncOwnedAndReturnRemaining(List<DirNames> ids)
        {
            var remaining = new List<DirNames>();
            var domainsToSync = new HashSet<HgEngineDomain>();

            foreach (var id in ids)
            {
                var domain = HgEngineDomains.IsOwned(id) ? HgEngineDomains.ForDir(id) : null;
                if (domain != null) domainsToSync.Add(domain.Domain);
                else remaining.Add(id);
            }

            foreach (var domainKey in domainsToSync)
            {
                var domain = HgEngineDomains.All.First(d => d.Domain == domainKey);
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
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout linked."; return false; }

            lock (_buildLock)
            {
                bool stillExtracted;
                DateTime[] currentMtimes = Array.Empty<DateTime>();

                if (domain.SyncOncePerSession)
                {
                    stillExtracted = _syncedOnceThisSession.Contains(domain.Domain) && domain.NarcByDir.Keys.All(dir =>
                        gameDirs.TryGetValue(dir, out var p) && Directory.Exists(p.unpackedDir) && Directory.GetFiles(p.unpackedDir).Length > 0);
                }
                else
                {
                    string[] inputPaths = new[] { domain.SourceFileRelPath }.Concat(ExtraInputsFor(domain)).ToArray();
                    currentMtimes = inputPaths
                        .Select(p => SafeMtime(Path.Combine(HgEngineProject.RepoPathUnc, p.Replace('/', '\\'))))
                        .ToArray();

                    bool unchanged = _lastSyncedMtimes.TryGetValue(domain.Domain, out var cached) && cached.SequenceEqual(currentMtimes);
                    stillExtracted = unchanged && domain.NarcByDir.Keys.All(dir =>
                        gameDirs.TryGetValue(dir, out var p) && Directory.Exists(p.unpackedDir) && Directory.GetFiles(p.unpackedDir).Length > 0);
                }

                if (stillExtracted) return true;

                if (!HgEngineBuild.RunMakeTargets(domain.MakeTargets, out _, out string stderr))
                {
                    error = stderr;
                    return false;
                }

                int speciesCount = -1;   // only needed for the learnsets transform, resolved lazily below

                foreach (var kv in domain.NarcByDir)
                {
                    if (!gameDirs.TryGetValue(kv.Key, out (string packedDir, string unpackedDir) paths))
                        continue;

                    string narcPath = Path.Combine(HgEngineProject.RepoPathUnc, kv.Value.Replace('/', '\\'));
                    if (!File.Exists(narcPath))
                    {
                        error = $"Expected build output not found: {narcPath}";
                        return false;
                    }

                    // hg-engine repurposes this narc slot with a completely different binary layout
                    // (one combined table, not one file per species). See HgEngineLearnsets.
                    if (kv.Key == DirNames.learnsets)
                    {
                        if (speciesCount < 0) speciesCount = ResolveSpeciesCount(domain);
                        if (speciesCount <= 0)
                        {
                            error = "Could not determine species count for the learnsets sync.";
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
                        error = $"Failed to parse built narc: {narcPath}";
                        return false;
                    }
                    opened.ExtractToFolder(paths.unpackedDir);

                    // Some ViewModels read gameDirs[...].packedDir's narc file directly instead of unpackedDir; refresh both.
                    try { File.Copy(narcPath, paths.packedDir, overwrite: true); } catch (IOException) { }
                }

                if (domain.SyncOncePerSession) _syncedOnceThisSession.Add(domain.Domain);
                else _lastSyncedMtimes[domain.Domain] = currentMtimes;
                return true;
            }
        }

        private static DateTime SafeMtime(string path)
        {
            try { return File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue; }
            catch { return DateTime.MinValue; }
        }

        /// <summary>Species count for the learnsets transform: the personalPokeData narc's element count
        /// if this domain owns it (always true today, learnsets only exists alongside Species), else
        /// falls back to whatever's already unpacked.</summary>
        private static int ResolveSpeciesCount(HgEngineDomainInfo domain)
        {
            if (domain.NarcByDir.TryGetValue(DirNames.personalPokeData, out string personalRel))
            {
                string personalPath = Path.Combine(HgEngineProject.RepoPathUnc, personalRel.Replace('/', '\\'));
                Narc personal = File.Exists(personalPath) ? Narc.Open(personalPath) : null;
                if (personal != null) return personal.ElementCount;
            }
            return gameDirs.TryGetValue(DirNames.personalPokeData, out var p) && Directory.Exists(p.unpackedDir)
                ? Directory.GetFiles(p.unpackedDir).Length
                : -1;
        }
    }
}
