using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using static DSPRE.RomInfo;

namespace DSPRE.HgEngine
{
    /// <summary>Which shell runs hg-engine's Makefile. The toolchain is POSIX either way; this only
    /// says which one, because the same Windows folder is spelled differently in each.</summary>
    public enum HgEngineShell
    {
        Wsl,
        Msys2,

        /// <summary>The host's own bash: DSPRE running on Linux, macOS or inside WSL.</summary>
        Native,
    }

    /// <summary>
    /// Per-project link to an hg-engine checkout (workDir/dspre_hgengine.json, same pattern as
    /// LabelStore's project file). Distinct from <see cref="RomInfo.isHGE"/>, which detects "this ROM
    /// was built by hg-engine", this is "do we have a source checkout to edit its data through."
    ///
    /// The checkout may live inside WSL or on a Windows drive. hg-engine documents both (its README
    /// has a MSYS2 setup section alongside the Linux one), so neither is a special case.
    /// </summary>
    public static class HgEngineProject
    {
        /// <summary>Where MSYS2 puts its bash unless the user installed it elsewhere.</summary>
        public const string DefaultMsysBash = @"C:\msys64\usr\bin\bash.exe";

        public static bool IsLinked { get; private set; }
        public static bool Enabled { get; private set; }

        /// <summary>Per project: the reminder that an edited source file needs a compile has been turned off.</summary>
        public static bool SuppressManagedFileSaveNotice { get; private set; }
        public static HgEngineShell Shell { get; private set; }

        /// <summary>The checkout root as Windows sees it: a \\wsl.localhost\... UNC path for a WSL
        /// checkout, or an ordinary path like C:\msys64\home\you\git\hg-engine for a local one.</summary>
        public static string RepoRootWindows { get; private set; }

        /// <summary>WSL only. Null for a checkout on a Windows drive built through MSYS2, and null for
        /// one built through WSL from a local drive, where wsl.exe uses the default distro.</summary>
        public static string WslDistro { get; private set; }

        /// <summary>MSYS2 only: which bash runs `make`. Defaults to <see cref="DefaultMsysBash"/>.</summary>
        public static string MsysBashPath { get; private set; }

        /// <summary>Requires the currently open ROM to actually be hg-engine's own build, not just "a
        /// checkout happens to be linked": a stale link must never route another ROM's data through it.</summary>
        public static bool IsActive => IsLinked && Enabled && RomInfo.isHGE;

        /// <summary>Legacy name for <see cref="RepoRootWindows"/>, kept because roughly twenty source
        /// readers call it. It is no longer always a UNC path.</summary>
        public static string RepoPathUnc => RepoRootWindows;

        /// <summary>
        /// The checkout root as the chosen shell spells it, for the `make -C` argument. The same
        /// Windows folder resolves differently per shell, which is the whole reason the shell is
        /// stored rather than guessed: C:\hg is /mnt/c/hg under WSL but /c/hg under MSYS2.
        /// </summary>
        public static string RepoPathPosix
        {
            get
            {
                if (!IsLinked) return null;
                if (Shell == HgEngineShell.Native) return RepoRootWindows;
                if (TryParseWslUncPath(RepoRootWindows, out _, out string posix)) return posix;
                return ToPosix(RepoRootWindows, Shell);
            }
        }

        /// <summary>True when the build will cross the Windows/Linux filesystem boundary, which is
        /// correct but markedly slower than building from inside WSL.</summary>
        public static bool BuildCrossesMountBoundary =>
            IsLinked && (Shell == HgEngineShell.Wsl && !TryParseWslUncPath(RepoRootWindows, out _, out _)
                || Shell == HgEngineShell.Native && RepoRootWindows.StartsWith("/mnt/", StringComparison.Ordinal));

        /// <summary>Off Windows the build always runs in the host's own bash.</summary>
        public static bool HostIsPosix => !OperatingSystem.IsWindows();

        /// <summary>
        /// A stored checkout path as this host can open it. A link written by Windows DSPRE names a WSL
        /// checkout as \\wsl.localhost\distro\... and a Windows-drive one as C:\..., which DSPRE inside
        /// WSL reaches as the plain Linux path and /mnt/c/... respectively.
        /// </summary>
        internal static string HostPath(string stored)
        {
            if (!HostIsPosix || string.IsNullOrWhiteSpace(stored)) return stored;
            if (TryParseWslUncPath(stored, out _, out string posix)) return posix;
            if (stored.Length >= 2 && stored[1] == ':' && char.IsLetter(stored[0])) return ToPosix(stored, HgEngineShell.Wsl);
            return stored;
        }

        /// <summary>Null unless active; the 5 source-backed editors show this so it's never ambiguous
        /// which backend (ROM vs. linked source) is live.</summary>
        public static string BannerText => IsActive
            ? $"Editing hg-engine source: {RepoRootWindows}"
            : null;

        /// <summary>True if rom.nds exists at the checkout's root, required for `make` to build anything.</summary>
        public static bool HasRomNds => IsLinked && File.Exists(Path.Combine(RepoRootWindows, "rom.nds"));

        /// <summary>True if a path looks like an hg-engine checkout root, not a DSPRE project folder.</summary>
        public static bool LooksLikeCheckout(string path) =>
            !string.IsNullOrWhiteSpace(path) && Directory.Exists(path) &&
            File.Exists(Path.Combine(path, "Makefile")) &&
            Directory.Exists(Path.Combine(path, "data")) &&
            Directory.Exists(Path.Combine(path, "armips"));

        /// <summary>True when the path is inside WSL, so the shell is settled and nothing needs asking.</summary>
        public static bool IsWslPath(string path) => TryParseWslUncPath(path, out _, out _);

        // A base tree is deleted whole and re-extracted whenever rom.nds changes, so the settings live in the
        // checkout's .dspre/ folder instead.
        private static string ConfigPath =>
            IsLinked && !string.IsNullOrEmpty(RepoRootWindows) ? FolderConfigPath(RepoRootWindows) : null;

        /// <summary>The checkout a base-tree project belongs to, which is simply its parent.</summary>
        internal static string CheckoutOfBaseProject()
        {
            if (!RomInfo.IsHgEngineBaseProject || string.IsNullOrEmpty(workDir)) return null;
            try { return Directory.GetParent(workDir.TrimEnd(Path.DirectorySeparatorChar))?.FullName; }
            catch (Exception ex) { AppLogger.Error("HgEngineProject.CheckoutOfBaseProject: " + ex.Message); return null; }
        }
        private static string _loadedFor;

        /// <summary>(Re)loads the link state for the currently open project. Call after a ROM is opened/closed.</summary>
        public static void Refresh()
        {
            Reset();
            _loadedFor = workDir;

            // Only an opened hg-engine folder is linked; its base/ tree names the checkout.
            string checkout = CheckoutOfBaseProject();
            if (checkout == null || !LooksLikeCheckout(checkout)) return;
            SetFolderState(checkout, DefaultShellFor(checkout));
            ReadFolderConfig(checkout);
        }

        /// <summary>
        /// Points the link at a checkout opened as a folder project, before its base/ tree is loaded (for the
        /// first build) and after. The shell choice is kept in the checkout's .dspre/ folder.
        /// </summary>
        public static void OpenFolder(string checkout, HgEngineShell shell, string msysBashPath = null)
        {
            Reset();
            _loadedFor = workDir;   // the project still open must not refresh this away before the folder loads
            SetFolderState(checkout.TrimEnd('\\', '/'), HostIsPosix ? HgEngineShell.Native : shell);
            if (!string.IsNullOrWhiteSpace(msysBashPath)) MsysBashPath = msysBashPath;
            Save();
        }

        private static void SetFolderState(string checkout, HgEngineShell shell)
        {
            RepoRootWindows = checkout;
            Shell = shell;
            WslDistro = TryParseWslUncPath(checkout, out string distro, out _) ? distro : null;
            IsLinked = true;
            Enabled = true;
        }

        /// <summary>The name Build and Run passes as make's BUILDROM, so emulator saves follow it; null until chosen.</summary>
        public static string BuildRomName { get; private set; }

        /// <summary>A plain file name ending in .nds, safe to hand to make.</summary>
        public static bool IsSafeBuildRomName(string name) =>
            !string.IsNullOrWhiteSpace(name) && name.EndsWith(".nds", StringComparison.OrdinalIgnoreCase)
            && System.Text.RegularExpressions.Regex.IsMatch(name, @"^[A-Za-z0-9][A-Za-z0-9 ._-]*$");

        public static void SetBuildRomName(string name)
        {
            if (!IsLinked || !IsSafeBuildRomName(name)) return;
            BuildRomName = name;
            Save();
        }

        /// <summary>
        /// The build shell chosen the first time this folder was opened on this kind of host, or null when it hasn't
        /// been. The same checkout can be opened from Linux and from Windows, and each stores the shell it used.
        /// </summary>
        public static HgEngineShell? StoredShellFor(string checkout)
        {
            string path = Path.Combine(checkout, RomInfo.HgEngineDspreFolder, "hgengine.json");
            if (!File.Exists(path)) return null;
            try
            {
                var cfg = JsonSerializer.Deserialize<HgEngineConfig>(File.ReadAllText(path));
                if (cfg == null || !Enum.TryParse(cfg.shell, out HgEngineShell shell)) return null;
                if (HostIsPosix) return shell == HgEngineShell.Native ? shell : null;
                if (shell == HgEngineShell.Native) return null;
                return IsWslPath(checkout) && shell != HgEngineShell.Wsl ? null : shell;
            }
            catch (Exception ex) { AppLogger.Error("HgEngineProject.StoredShellFor: " + ex.Message); return null; }
        }

        private static HgEngineShell DefaultShellFor(string checkout) =>
            HostIsPosix ? HgEngineShell.Native : HgEngineShell.Wsl;

        private static void ReadFolderConfig(string checkout)
        {
            string path = FolderConfigPath(checkout);
            if (!File.Exists(path)) return;
            try
            {
                var cfg = JsonSerializer.Deserialize<HgEngineConfig>(File.ReadAllText(path));
                if (cfg == null) return;
                // The shell only matters on Windows, and a WSL path can only build one way.
                if (!HostIsPosix && !IsWslPath(checkout) && Enum.TryParse(cfg.shell, out HgEngineShell stored) && stored != HgEngineShell.Native)
                    Shell = stored;
                if (!string.IsNullOrWhiteSpace(cfg.msysBashPath)) MsysBashPath = cfg.msysBashPath;
                SuppressManagedFileSaveNotice = cfg.suppressManagedFileSaveNotice;
                BuildRomName = IsSafeBuildRomName(cfg.buildRomName) ? cfg.buildRomName : null;
            }
            catch (Exception ex) { AppLogger.Error("HgEngineProject.ReadFolderConfig: " + ex.Message); }
        }

        /// <summary>The checkout's .dspre/ settings file, which also makes sure .dspre/ ignores itself in git.</summary>
        internal static string FolderConfigPath(string checkout)
        {
            string dir = Path.Combine(checkout, RomInfo.HgEngineDspreFolder);
            try
            {
                Directory.CreateDirectory(dir);
                string ignore = Path.Combine(dir, ".gitignore");
                if (!File.Exists(ignore)) File.WriteAllText(ignore, "*\n");
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                AppLogger.Error("HgEngineProject.FolderConfigPath: " + ex.Message);
            }
            return Path.Combine(dir, "hgengine.json");
        }

        /// <summary>Ensures the state matches the currently open project (workDir may have changed since Refresh).</summary>
        private static void EnsureCurrent() { if (_loadedFor != workDir) Refresh(); }

        /// <summary>
        /// Links to an hg-engine checkout at a Windows-visible path. A \\wsl.localhost\... path settles
        /// the shell on its own; for a path on a Windows drive the caller must say which shell builds
        /// it, because both are valid and the folder is spelled differently in each.
        /// </summary>
        public static bool TryLink(string windowsPath, HgEngineShell shell, string msysBashPath, out string error)
        {
            EnsureCurrent();
            error = null;

            if (string.IsNullOrWhiteSpace(windowsPath) || !Directory.Exists(windowsPath))
            {
                error = "That folder does not exist.";
                return false;
            }
            if (!LooksLikeCheckout(windowsPath))
            {
                error = "That folder is not an hg-engine checkout (no Makefile, data/ and armips/ at its root).";
                return false;
            }

            bool isWslPath = TryParseWslUncPath(windowsPath, out string distro, out _);
            if (isWslPath) shell = HgEngineShell.Wsl;
            if (HostIsPosix) shell = HgEngineShell.Native;

            if (shell == HgEngineShell.Native)
            {
                MsysBashPath = DefaultMsysBash;
                WslDistro = null;
            }
            else if (shell == HgEngineShell.Msys2)
            {
                string bash = string.IsNullOrWhiteSpace(msysBashPath) ? DefaultMsysBash : msysBashPath;
                if (!File.Exists(bash))
                {
                    error = $"MSYS2's bash was not found at {bash}. Install MSYS2 or point DSPRE at its bash.exe.";
                    return false;
                }
                MsysBashPath = bash;
                WslDistro = null;
            }
            else
            {
                MsysBashPath = DefaultMsysBash;
                // Only a UNC checkout names its distro. A local one built through WSL runs in the
                // default distro, which is what wsl.exe uses when no -d is passed.
                WslDistro = isWslPath ? distro : null;
            }

            RepoRootWindows = windowsPath.TrimEnd('\\', '/');
            Shell = shell;
            Enabled = true;
            IsLinked = true;
            HgEngineSymbolTable.ClearCache();
            HgEngineFileCache.ClearCache();
            HgEngineSync.ClearSyncState();
            Save();
            return true;
        }

        /// <summary>Links a checkout whose shell needs no asking: one inside WSL, or any on a POSIX host.</summary>
        public static bool TryLink(string windowsPath, out string error)
            => TryLink(windowsPath, HostIsPosix ? HgEngineShell.Native : HgEngineShell.Wsl, null, out error);

        public static void SetEnabled(bool enabled)
        {
            EnsureCurrent();
            if (!IsLinked) return;
            Enabled = enabled;
            Save();
        }

        public static void SuppressManagedFileSaveNoticeForProject()
        {
            EnsureCurrent();
            if (SuppressManagedFileSaveNotice) return;
            SuppressManagedFileSaveNotice = true;
            Save();
        }

        public static void Unlink()
        {
            EnsureCurrent();
            Reset();
            string path = ConfigPath;
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                try { File.Delete(path); } catch (Exception ex) { AppLogger.Error("HgEngineProject.Unlink: " + ex.Message); }
            }
        }

        private static void Reset()
        {
            IsLinked = false;
            Enabled = false;
            Shell = HgEngineShell.Wsl;
            RepoRootWindows = null;
            WslDistro = null;
            MsysBashPath = DefaultMsysBash;
            SuppressManagedFileSaveNotice = false;
            BuildRomName = null;
            HgEngineSymbolTable.ClearCache();
            HgEngineFileCache.ClearCache();
            HgEngineOwnedFiles.ClearCache();
            HgEngineSync.ClearSyncState();
        }

        private static void Save()
        {
            string path = ConfigPath;
            if (path == null) return;
            try
            {
                var cfg = new HgEngineConfig
                {
                    rootWindows = RepoRootWindows,
                    shell = Shell.ToString(),
                    wslDistro = WslDistro,
                    msysBashPath = MsysBashPath,
                    repoPathPosix = RepoPathPosix,   // written for older DSPRE builds to still read
                    enabled = Enabled,
                    suppressManagedFileSaveNotice = SuppressManagedFileSaveNotice,
                    buildRomName = BuildRomName,
                };
                File.WriteAllText(path, JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex) { AppLogger.Error("HgEngineProject.Save: " + ex.Message); }
        }

        /// <summary>
        /// Spells a Windows path the way the given shell does. WSL mounts drives under /mnt, MSYS2
        /// under the drive letter alone, so C:\hg-engine is /mnt/c/hg-engine or /c/hg-engine.
        /// </summary>
        internal static string ToPosix(string windowsPath, HgEngineShell shell)
        {
            if (string.IsNullOrWhiteSpace(windowsPath)) return null;
            string p = windowsPath.Replace('\\', '/').TrimEnd('/');
            if (p.Length >= 2 && p[1] == ':' && char.IsLetter(p[0]))
            {
                string drive = char.ToLowerInvariant(p[0]).ToString();
                string rest = p.Length > 2 ? p.Substring(2) : "";
                return (shell == HgEngineShell.Msys2 ? "/" : "/mnt/") + drive + rest;
            }
            return p;
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
            public string rootWindows { get; set; }
            public string shell { get; set; }
            public string wslDistro { get; set; }
            public string msysBashPath { get; set; }
            public string repoPathPosix { get; set; }
            public bool enabled { get; set; }
            public bool suppressManagedFileSaveNotice { get; set; }
            public string buildRomName { get; set; }
        }
    }
}
