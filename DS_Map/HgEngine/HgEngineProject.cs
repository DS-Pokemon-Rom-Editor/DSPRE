using System;
using System.IO;
using System.Text.Json;
using static DSPRE.RomInfo;

namespace DSPRE.HgEngine
{
    /// <summary>
    /// Per-project link to an hg-engine WSL checkout (workDir/dspre_hgengine.json, same pattern as
    /// LabelStore's project file). Distinct from <see cref="RomInfo.isHGE"/>, which detects "this ROM
    /// was built by hg-engine", this is "do we have a source checkout to edit its data through."
    /// </summary>
    public static class HgEngineProject
    {
        public static bool IsLinked { get; private set; }
        public static bool Enabled { get; private set; }
        public static string WslDistro { get; private set; }
        public static string RepoPathPosix { get; private set; }   // e.g. /home/mixone/romhacking/hg-engine

        /// <summary>Requires the currently open ROM to actually be hg-engine's own build, not just "a
        /// checkout happens to be linked": a stale link must never route another ROM's data through it.</summary>
        public static bool IsActive => IsLinked && Enabled && RomInfo.isHGE;

        /// <summary>Windows-visible path to the linked repo, for plain File I/O against data/*.c source text.</summary>
        public static string RepoPathUnc => IsLinked
            ? $@"\\wsl.localhost\{WslDistro}{RepoPathPosix.Replace('/', '\\')}"
            : null;

        /// <summary>Null unless active; the 5 source-backed editors show this so it's never ambiguous
        /// which backend (ROM vs. linked source) is live.</summary>
        public static string BannerText => IsActive
            ? $"Editing hg-engine source: {WslDistro}:{RepoPathPosix}"
            : null;

        /// <summary>True if rom.nds exists at the checkout's root, required for `make` to build anything.</summary>
        public static bool HasRomNds => IsLinked && File.Exists(Path.Combine(RepoPathUnc, "rom.nds"));

        /// <summary>True if a path looks like an hg-engine checkout root, not a DSPRE project folder.</summary>
        public static bool LooksLikeCheckout(string path) =>
            !string.IsNullOrWhiteSpace(path) && Directory.Exists(path) &&
            File.Exists(Path.Combine(path, "Makefile")) &&
            Directory.Exists(Path.Combine(path, "data")) &&
            Directory.Exists(Path.Combine(path, "armips"));

        private static string ConfigPath => string.IsNullOrEmpty(workDir) ? null : Path.Combine(workDir, "dspre_hgengine.json");
        private static string _loadedFor;

        /// <summary>(Re)loads the link state for the currently open project. Call after a ROM is opened/closed.</summary>
        public static void Refresh()
        {
            IsLinked = false; Enabled = false; WslDistro = null; RepoPathPosix = null;
            _loadedFor = workDir;
            HgEngineSymbolTable.ClearCache();
            HgEngineFileCache.ClearCache();
            HgEngineSync.ClearSyncState();

            string path = ConfigPath;
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
            try
            {
                var cfg = JsonSerializer.Deserialize<HgEngineConfig>(File.ReadAllText(path));
                if (cfg == null || string.IsNullOrWhiteSpace(cfg.repoPathPosix) || string.IsNullOrWhiteSpace(cfg.wslDistro)) return;
                WslDistro = cfg.wslDistro;
                RepoPathPosix = cfg.repoPathPosix;
                Enabled = cfg.enabled;
                IsLinked = true;
            }
            catch (Exception ex) { AppLogger.Error("HgEngineProject.Refresh: " + ex.Message); }
        }

        /// <summary>Ensures the state matches the currently open project (workDir may have changed since Refresh).</summary>
        private static void EnsureCurrent() { if (_loadedFor != workDir) Refresh(); }

        /// <summary>Links to an hg-engine checkout given its Windows-visible path (a \\wsl.localhost\Distro\...
        /// or \\wsl$\Distro\... UNC path, as returned by a folder picker). Parses out the WSL distro + POSIX path.</summary>
        public static bool TryLink(string uncPath, out string error)
        {
            EnsureCurrent();
            error = null;
            if (!TryParseWslUncPath(uncPath, out string distro, out string posixPath))
            {
                error = "That doesn't look like a WSL path (expected \\\\wsl.localhost\\<Distro>\\...).";
                return false;
            }
            WslDistro = distro;
            RepoPathPosix = posixPath;
            Enabled = true;
            IsLinked = true;
            HgEngineSymbolTable.ClearCache();
            HgEngineFileCache.ClearCache();
            HgEngineSync.ClearSyncState();
            Save();
            return true;
        }

        public static void SetEnabled(bool enabled)
        {
            EnsureCurrent();
            if (!IsLinked) return;
            Enabled = enabled;
            Save();
        }

        public static void Unlink()
        {
            EnsureCurrent();
            IsLinked = false; Enabled = false; WslDistro = null; RepoPathPosix = null;
            HgEngineSymbolTable.ClearCache();
            HgEngineFileCache.ClearCache();
            HgEngineSync.ClearSyncState();
            string path = ConfigPath;
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                try { File.Delete(path); } catch (Exception ex) { AppLogger.Error("HgEngineProject.Unlink: " + ex.Message); }
            }
        }

        private static void Save()
        {
            string path = ConfigPath;
            if (path == null) return;
            try
            {
                var cfg = new HgEngineConfig { wslDistro = WslDistro, repoPathPosix = RepoPathPosix, enabled = Enabled };
                File.WriteAllText(path, JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex) { AppLogger.Error("HgEngineProject.Save: " + ex.Message); }
        }

        internal static bool TryParseWslUncPath(string path, out string distro, out string posixPath)
        {
            distro = null; posixPath = null;
            if (string.IsNullOrWhiteSpace(path)) return false;
            foreach (string prefix in new[] { @"\\wsl.localhost\", @"\\wsl$\" })
            {
                if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    string rest = path.Substring(prefix.Length).TrimEnd('\\');
                    int sep = rest.IndexOf('\\');
                    if (sep < 0) { distro = rest; posixPath = "/"; return true; }
                    distro = rest.Substring(0, sep);
                    posixPath = "/" + rest.Substring(sep + 1).Replace('\\', '/');
                    return true;
                }
            }
            return false;
        }

        private sealed class HgEngineConfig
        {
            public string wslDistro { get; set; }
            public string repoPathPosix { get; set; }
            public bool enabled { get; set; }
        }
    }
}
