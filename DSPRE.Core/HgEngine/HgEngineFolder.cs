using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace DSPRE.HgEngine
{
    /// <summary>
    /// Opening an hg-engine checkout as the project: the checks hg-engine's own make would fail on, the
    /// rom.nds it builds from, and the base/ tree DSPRE then opens.
    /// </summary>
    public static class HgEngineFolder
    {
        public const string RomName = "rom.nds";
        public const string GameCode = "IPKE";

        /// <summary>SHA-1 of a clean HeartGold (USA) dump, the ROM hg-engine is made for.</summary>
        public const string CleanRetailSha1 = "4fcded0e2713dc03929845de631d0932ea2b5a37";

        public static string BaseDir(string checkout) => Path.Combine(checkout, HgEngineBase.BaseDirName);

        public static string RomPath(string checkout) => Path.Combine(checkout, RomName);

        /// <summary>A checkout that builds through ds-rom, which is what folder projects need.</summary>
        public static bool IsDsRomCheckout(string folder) =>
            HgEngineProject.LooksLikeCheckout(folder) && HgEngineBase.SupportsDsRomAt(folder);

        /// <summary>Whether base/ has been extracted and built into, so DSPRE can open it.</summary>
        public static bool HasBase(string checkout) => DSUtils.GetFolderType(BaseDir(checkout)) == 2;

        /// <summary>What would stop hg-engine's make before it starts, each with how to fix it. Empty when nothing does.</summary>
        public static List<string> Problems(string checkout)
        {
            var problems = new List<string>();
            string dotGit = Path.Combine(checkout, ".git");
            if (!Directory.Exists(dotGit) && !File.Exists(dotGit))
                problems.Add("It is not a git repository. Clone hg-engine with git rather than downloading it as a zip.");
            if (checkout.IndexOf("onedrive", StringComparison.OrdinalIgnoreCase) >= 0)
                problems.Add("It is inside OneDrive, which hg-engine refuses to build in. Clone it somewhere else.");
            string nitrogfx = Path.Combine(checkout, "tools", "source", "nitrogfx");
            if (!Directory.Exists(nitrogfx) || !Directory.EnumerateFileSystemEntries(nitrogfx).Any())
                problems.Add("The nitrogfx submodule is missing. Run: git submodule update --init");
            return problems;
        }

        /// <summary>
        /// Tools the build needs that the build shell can't find, as a list of names; null when the shell
        /// could not be run at all. cargo only matters until ds-rom has been built once.
        /// </summary>
        public static List<string> MissingTools(string checkout)
        {
            bool needsCargo = !File.Exists(Path.Combine(checkout, "tools", "dsrom")) && !File.Exists(Path.Combine(checkout, "tools", "dsrom.exe"));
            string tools = "make gcc python3 " + (needsCargo ? "cargo " : "");
            string probe = "for t in " + tools + "; do command -v $t >/dev/null 2>&1 || echo $t; done; "
                + "command -v \"${DEVKITARM:+$DEVKITARM/bin/}arm-none-eabi-gcc\" >/dev/null 2>&1 "
                + "|| command -v /mingw64/bin/arm-none-eabi-gcc >/dev/null 2>&1 || echo arm-none-eabi-gcc";
            if (!HgEngineBuild.TryRunShell(probe, out string stdout, out _)) return null;
            return stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        }

        /// <summary>The game code at 0x0C of a ROM file, or null when it can't be read.</summary>
        public static string ReadGameCode(string romPath)
        {
            try
            {
                using var f = File.OpenRead(romPath);
                if (f.Length < 0x10) return null;
                var b = new byte[4];
                f.Position = 0x0C;
                return f.Read(b, 0, 4) == 4 ? Encoding.ASCII.GetString(b) : null;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return null;
            }
        }

        public static string Sha1Of(string path)
        {
            using var f = File.OpenRead(path);
            return Convert.ToHexString(SHA1.HashData(f)).ToLowerInvariant();
        }

        // Their plaintext folders are the editors' own state, rebuilt from the archive only on request.
        private static readonly HashSet<RomInfo.DirNames> KeptAfterBuild = new() { RomInfo.DirNames.textArchives, RomInfo.DirNames.scripts };

        /// <summary>The packed archives the project has unpacked, by hash, taken just before make runs.</summary>
        public static Dictionary<RomInfo.DirNames, string> SnapshotUnpackedArchives()
        {
            var hashes = new Dictionary<RomInfo.DirNames, string>();
            if (RomInfo.gameDirs == null) return hashes;
            foreach (var (dir, paths) in RomInfo.gameDirs)
            {
                if (KeptAfterBuild.Contains(dir) || !Directory.Exists(paths.unpackedDir) || !File.Exists(paths.packedDir)) continue;
                try { hashes[dir] = Sha1Of(paths.packedDir); }
                catch (IOException ex) { AppLogger.Error($"HgEngineFolder: could not read {paths.packedDir}: {ex.Message}"); }
            }
            return hashes;
        }

        /// <summary>
        /// Unpacks again every archive make changed, so editors show what the build made rather than what DSPRE wrote
        /// before it, and drops the cached checkout files. Returns the archives refreshed.
        /// </summary>
        public static List<RomInfo.DirNames> RefreshRebuiltArchives(Dictionary<RomInfo.DirNames, string> before)
        {
            HgEngineFileCache.ClearCache();
            HgEngineSymbolTable.ClearCache();
            HgEngineOwnedFiles.ClearCache();
            HgEngineBuiltPngs.ClearCache();

            var changed = new List<RomInfo.DirNames>();
            foreach (var (dir, hash) in before)
            {
                var paths = RomInfo.gameDirs[dir];
                try
                {
                    if (!File.Exists(paths.packedDir) || Sha1Of(paths.packedDir) == hash) continue;
                    foreach (string file in Directory.GetFiles(paths.unpackedDir)) File.Delete(file);
                    changed.Add(dir);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    AppLogger.Error($"HgEngineFolder: could not refresh {dir}: {ex.Message}");
                }
            }
            if (changed.Count > 0) DSUtils.ForceUnpackNarcs(changed);
            if (RomInfo.OverworldTable != null) RomInfo.ReadOWTable();
            return changed;
        }

        /// <summary>
        /// Copies a HeartGold (USA) ROM into the checkout as rom.nds. The user's file stays where it is. Null on
        /// success, otherwise why it was refused.
        /// </summary>
        public static string ProvideRom(string checkout, string sourceRom)
        {
            string code = ReadGameCode(sourceRom);
            if (code != GameCode)
                return $"hg-engine builds from a HeartGold (USA) ROM, game code {GameCode}. This one is {code ?? "unreadable"}.";
            string target = RomPath(checkout);
            if (File.Exists(target)) return "The checkout already has a rom.nds.";
            try
            {
                File.Copy(sourceRom, target);
                // make re-extracts only when rom.nds is newer than base/arm9.bin, and a copy keeps the source's date.
                File.SetLastWriteTimeUtc(target, DateTime.UtcNow);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return "Could not copy the ROM into the checkout: " + ex.Message;
            }
            return null;
        }
    }
}
