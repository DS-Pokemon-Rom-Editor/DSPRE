using System;
using System.IO;
using System.Text;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia
{
    /// <summary>
    /// UI-agnostic ROM loader for the Avalonia shell, the counterpart to the WinForms MainProgram open-ROM flow.
    /// Unpacks a .nds (or opens an already-extracted folder), reads the game code from the header and constructs
    /// <see cref="RomInfo"/> (which populates the static RomInfo.* state the Avalonia editors read). This class
    /// itself has no UI: the caller decides (via <see cref="PeekFolderType"/>) whether to prompt the user about
    /// reusing or re-extracting existing data, then passes that choice in as <c>reExtract</c>.
    /// </summary>
    public static class AvaloniaRomLoader
    {
        /// <summary>
        /// Returns the folder-type code (see <see cref="DSUtils.GetFolderType"/>) for the work dir a given
        /// .nds path would unpack to, without touching anything: -1 means no existing extracted data.
        /// </summary>
        public static int PeekFolderType(string ndsPath) => DSUtils.GetFolderType(DSUtils.WorkDirPathFromFile(ndsPath));

        /// <summary>Load from a .nds file: unpack to its work dir (or reuse/re-extract existing data), then open it.</summary>
        /// <param name="reExtract">If existing extracted data is found, delete it and unpack fresh instead of reusing it.</param>
        public static bool LoadFromFile(string ndsPath, out string error, bool reExtract = false)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(ndsPath) || !File.Exists(ndsPath)) { error = "ROM file not found."; return false; }

            string workDir = DSUtils.WorkDirPathFromFile(ndsPath);
            int existing = DSUtils.GetFolderType(workDir);
            if (existing == -1)   // not already extracted → unpack
            {
                AppLogger.Info($"Unpacking {ndsPath} → {workDir}");
                if (!DSUtils.UnpackRom(ndsPath, workDir)) { error = "Unpacking the ROM failed."; return false; }
            }
            else if (reExtract)
            {
                AppLogger.Info($"Re-extracting {ndsPath}: deleting old data at {workDir}");
                // The script editor's language server runs inside this folder and would keep it from being deleted.
                RotomLanguageServerClient.StopAll();
                try { Directory.Delete(workDir, true); }
                catch (IOException)
                {
                    error = $"Concurrent access detected: make sure no other process is using {workDir} while DSPRE is running.";
                    return false;
                }
                if (!DSUtils.UnpackRom(ndsPath, workDir)) { error = "Unpacking the ROM failed."; return false; }
            }
            else AppLogger.Info($"Reusing existing extracted data at {workDir}");

            bool ok = LoadFromFolder(workDir, out error, recordRecent: false);
            if (ok) SettingsManager.RecordRecentProject(ndsPath);   // remember what the USER opened
            return ok;
        }

        /// <summary>Open an already-extracted ROM folder (ds-rom or ndstool layout).</summary>
        public static bool LoadFromFolder(string folder, out string error) => LoadFromFolder(folder, out error, recordRecent: true);

        public static bool LoadFromFolder(string folder, out string error, bool recordRecent)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) { error = "Folder not found."; return false; }

            int type = DSUtils.GetFolderType(folder);
            if (type == -1) { error = "The selected folder is not a valid extracted ROM folder."; return false; }

            string gameCode = ReadGameCode(folder, type);
            if (string.IsNullOrEmpty(gameCode)) { error = "Could not read the game code from the ROM header."; return false; }
            // Refused before anything is reset, so the ROM already open keeps working.
            if (!RomInfo.IsSupportedGameCode(gameCode)) { error = UnsupportedMessage(gameCode); return false; }

            // Sprites are kept by overworld number, which means a different ROM's are a different
            // picture under the same number.
            OverworldSprites.ClearCache();
            // The font and its letter numbering belong to the ROM that was open, not the new one.
            Views.Controls.FieldMessageBoxView.Font = null;
            Views.Controls.FieldMessageBoxView.Frame = null;
            Views.Controls.FieldMenuWindowView.Font = null;
            Views.Controls.FieldMenuWindowView.Frame = null;
            Views.Controls.FieldMenuWindowView.Colours = null;
            Views.Controls.PoketchView.Screen = null;
            Views.Controls.HgssTouchScreenView.Screen = null;
            Views.Controls.HgssTouchScreenView.Font = null;
            Views.Controls.HgssTouchScreenView.IconFont = null;
            Data.SoundArchive.Reset();
            ROMFiles.FieldFontCharacters.Reset();
            // Unsaved label edits were made for the project being closed.
            Data.LabelStore.DiscardDraft();

            try { _ = new RomInfo(gameCode, folder); }   // populates the static RomInfo.* (gameFamily, workDir, gameDirs, …)
            catch (Exception ex) { error = "Failed to initialise ROM data: " + ex.Message; AppLogger.Error(error); return false; }

            if (gameFamily == GameFamilies.NULL) { error = "Unsupported ROM (Gen IV Pokémon only)."; return false; }
            AppLogger.Info($"ROM loaded: {RomInfo.romID} ({RomInfo.projectName})");
            if (recordRecent) SettingsManager.RecordRecentProject(folder);
            return true;
        }

        /// <summary>
        /// Why a .nds file or extracted folder can't be opened, read from its header alone, or null when it
        /// can. Lets the caller refuse before closing anything of the project that is open now.
        /// </summary>
        public static string WhyUnsupported(string path)
        {
            string gameCode = null;
            try
            {
                if (File.Exists(path))
                {
                    using FileStream fs = File.OpenRead(path);
                    byte[] b = new byte[4];
                    fs.Position = 0x0C;
                    if (fs.Read(b, 0, 4) == 4) gameCode = Encoding.ASCII.GetString(b);
                }
                else if (Directory.Exists(path))
                {
                    int type = DSUtils.GetFolderType(path);
                    if (type == -1) return null;   // the loader says what is wrong with the folder
                    gameCode = ReadGameCode(path, type);
                }
            }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
            if (string.IsNullOrEmpty(gameCode)) return null;
            return RomInfo.IsSupportedGameCode(gameCode) ? null : UnsupportedMessage(gameCode);
        }

        private static string UnsupportedMessage(string gameCode) =>
            $"This ROM ({gameCode.Trim('\0')}) is not supported. DSPRE opens Gen IV Pokémon ROMs only.";

        private static string ReadGameCode(string folder, int folderType)
        {
            if (folderType == 0)   // ds-rom → header.yaml
                return YamlUtils.ReadGameCodeFromHeaderYaml(Path.Combine(folder, "header.yaml"))?.gamecode;
            string meta = folderType == 2 ? DSUtils.HgEngineDsRomMetaDir(folder) : null;
            if (meta != null && !File.Exists(Path.Combine(folder, "header.bin")))
                return YamlUtils.ReadGameCodeFromHeaderYaml(Path.Combine(meta, "header.yaml"))?.gamecode;
            try   // ndstool → header.bin: the 4-char game code is at offset 0x0C
            {
                byte[] b = File.ReadAllBytes(Path.Combine(folder, "header.bin"));
                return b.Length >= 16 ? Encoding.ASCII.GetString(b, 12, 4) : null;
            }
            catch { return null; }
        }
    }
}
