using System;
using System.IO;
using System.Text;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia
{
    /// <summary>
    /// UI-agnostic ROM loader for the Avalonia shell — the counterpart to the WinForms MainProgram open-ROM flow.
    /// Unpacks a .nds (or opens an already-extracted folder), reads the game code from the header and constructs
    /// <see cref="RomInfo"/> (which populates the static RomInfo.* state the Avalonia editors read). Prompts are
    /// avoided: an already-extracted folder is reused, otherwise the ROM is unpacked fresh.
    /// </summary>
    public static class AvaloniaRomLoader
    {
        /// <summary>Load from a .nds file: unpack to its work dir (or reuse existing extracted data), then open it.</summary>
        public static bool LoadFromFile(string ndsPath, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(ndsPath) || !File.Exists(ndsPath)) { error = "ROM file not found."; return false; }

            string workDir = DSUtils.WorkDirPathFromFile(ndsPath);
            if (DSUtils.GetFolderType(workDir) == -1)   // not already extracted → unpack
            {
                AppLogger.Info($"Unpacking {ndsPath} → {workDir}");
                if (!DSUtils.UnpackRom(ndsPath, workDir)) { error = "Unpacking the ROM failed."; return false; }
            }
            else AppLogger.Info($"Reusing existing extracted data at {workDir}");
            return LoadFromFolder(workDir, out error);
        }

        /// <summary>Open an already-extracted ROM folder (ds-rom or ndstool layout).</summary>
        public static bool LoadFromFolder(string folder, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) { error = "Folder not found."; return false; }

            int type = DSUtils.GetFolderType(folder);
            if (type == -1) { error = "The selected folder is not a valid extracted ROM folder."; return false; }

            string gameCode = ReadGameCode(folder, type);
            if (string.IsNullOrEmpty(gameCode)) { error = "Could not read the game code from the ROM header."; return false; }

            try { _ = new RomInfo(gameCode, folder); }   // populates the static RomInfo.* (gameFamily, workDir, gameDirs, …)
            catch (Exception ex) { error = "Failed to initialise ROM data: " + ex.Message; AppLogger.Error(error); return false; }

            if (gameFamily == GameFamilies.NULL) { error = "Unsupported ROM (Gen IV Pokémon only)."; return false; }
            AppLogger.Info($"ROM loaded: {RomInfo.romID} ({RomInfo.projectName})");
            return true;
        }

        private static string ReadGameCode(string folder, int folderType)
        {
            if (folderType == 0)   // ds-rom → header.yaml
                return YamlUtils.ReadGameCodeFromHeaderYaml(Path.Combine(folder, "header.yaml"))?.gamecode;
            try   // ndstool → header.bin: the 4-char game code is at offset 0x0C
            {
                var b = File.ReadAllBytes(Path.Combine(folder, "header.bin"));
                return b.Length >= 16 ? Encoding.ASCII.GetString(b, 12, 4) : null;
            }
            catch { return null; }
        }
    }
}
