using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LibGit2Sharp;
using static DSPRE.RomInfo;

namespace DSPRE
{
    /// <summary>
    /// Updates the script command databases from the scrcmd-database repo and sets up each ROM's copy.
    /// </summary>
    public static class ScriptDatabaseSetup
    {
        public static void CheckForDatabaseUpdates(bool silent = true)
        {
            AppLogger.Info("Checking for script database updates...");
            string pathToDbRepo = AppPaths.DatabasePath;

            try
            {
                if (!Repository.IsValid(pathToDbRepo))
                {
                    Repository.Init(pathToDbRepo);
                    using (Repository repo = new Repository(pathToDbRepo))
                    {
                        Remote remote = repo.Network.Remotes.Add("origin", "https://github.com/DS-Pokemon-Rom-Editor/scrcmd-database.git");
                        Commands.Fetch(repo, remote.Name, new string[] { "refs/heads/main:refs/heads/main" }, null, null);

                        Branch main = repo.Branches["main"] ?? repo.CreateBranch("main", repo.Branches["refs/heads/main"].Tip);
                        repo.Branches.Update(main, b => b.TrackedBranch = "refs/remotes/origin/main");
                        Commands.Checkout(repo, main);
                    }
                }

                using (Repository repo = new Repository(pathToDbRepo))
                {
                    Remote remote = repo.Network.Remotes["origin"];
                    try
                    {
                        if (repo.Head.Tip != null)
                        {
                            repo.Reset(ResetMode.Hard);
                        }

                        foreach (StatusEntry item in repo.RetrieveStatus().Untracked)
                        {
                            string fullPath = Path.Combine(pathToDbRepo, item.FilePath);
                            if (File.Exists(fullPath))
                                File.Delete(fullPath);
                            else if (Directory.Exists(fullPath))
                                Directory.Delete(fullPath, true);
                        }

                        Commands.Fetch(repo, remote.Name, remote.FetchRefSpecs.Select(x => x.Specification), null, null);

                        Branch remoteBranch = repo.Branches["origin/main"];
                        CheckoutOptions options = new CheckoutOptions { CheckoutModifiers = CheckoutModifiers.Force };
                        Commands.Checkout(repo, repo.Branches["main"], options);
                        repo.Reset(ResetMode.Hard, remoteBranch.Tip);

                        AppLogger.Info("Script databases updated successfully");
                        if (!silent)
                        {
                            AppMessages.Notice("Script database updated.");
                        }
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Warn($"Couldn't fetch updates: {ex.Message}");
                        if (!silent)
                        {
                            AppMessages.Warning("Couldn't fetch database updates. Using local database files.", "Warning");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"Couldn't access git repository: {ex.Message}");
                if (!silent)
                {
                    AppMessages.Warning("Couldn't access database repository. Using local database files.", "Warning");
                }
            }
        }

        public static void InitializeScriptDatabase(string romFileName, GameFamilies gameFamily, GameVersions gameVersion)
        {
            string baseFileName = Path.GetFileNameWithoutExtension(romFileName);
            string romFileNameClean = baseFileName.EndsWith("_DSPRE_contents")
                ? baseFileName.Substring(0, baseFileName.Length - "_DSPRE_contents".Length)
                : baseFileName;

            if (SettingsManager.Settings?.AutomaticallyUpdateDBs == true)   // Settings may be unloaded when running headless
            {
                CheckForDatabaseUpdates();
            }

            string editedDatabasesDir = Path.Combine(AppPaths.DatabasePath, "edited_databases");
            Directory.CreateDirectory(editedDatabasesDir);

            string romDatabaseFolder = Path.Combine(editedDatabasesDir, romFileNameClean);
            Directory.CreateDirectory(romDatabaseFolder);

            string targetJsonPath = Path.Combine(romDatabaseFolder, "scrcmd_database.json");
            string databaseJsonPath;

            switch (gameFamily)
            {
                case GameFamilies.DP:
                    databaseJsonPath = Path.Combine(AppPaths.DatabasePath, "diamond_pearl_scrcmd_database.json");
                    break;
                case GameFamilies.HGSS:
                    databaseJsonPath = Path.Combine(AppPaths.DatabasePath, "hgss_scrcmd_database.json");
                    break;
                case GameFamilies.Plat:
                    databaseJsonPath = Path.Combine(AppPaths.DatabasePath, "platinum_scrcmd_database.json");
                    break;
                default:
                    throw new Exception("Unknown game family");
            }

            if (!File.Exists(targetJsonPath))
            {
                // The base databases come from the scrcmd-database repo or the bundled databases/ folder; offline on
                // a fresh machine neither may exist, and the ROM still loads.
                if (!File.Exists(databaseJsonPath) && SettingsManager.Settings?.AutomaticallyUpdateDBs != true)
                {
                    CheckForDatabaseUpdates();   // one clone attempt even when auto-update is off
                }
                if (!File.Exists(databaseJsonPath))
                {
                    AppLogger.Error($"Script database not found: {databaseJsonPath}");
                    AppMessages.Error(
                        "The script command database couldn't be found:\n" + databaseJsonPath +
                        "\n\nIt is normally downloaded automatically (internet required) or bundled as a " +
                        "'databases' folder next to the DSPRE executable.\n\n" +
                        "The ROM will still load, but script editing features will be limited.",
                        "Script database missing");
                    return;
                }
                PlainFiles.Copy(databaseJsonPath, targetJsonPath);
            }
            else if (File.Exists(databaseJsonPath))
            {
                try
                {
                    int merged = ScriptDatabaseMerge.Merge(targetJsonPath, databaseJsonPath, AppPaths.DatabasePath);
                    if (merged > 0) AppLogger.Info($"Script database: {merged} entries brought up to date from the shared database.");
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    AppLogger.Warn("Script database couldn't be brought up to date: " + ex.Message);
                }
            }

            try
            {
                ScriptDatabaseJsonLoader.InitializeFromJson(targetJsonPath, gameVersion);

                // The name dictionaries read the text archives.
                DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.TextArchives });

                Resources.ScriptDatabase.InitializePokemonNames();
                Resources.ScriptDatabase.InitializeItemNames();
                Resources.ScriptDatabase.InitializeMoveNames();
                Resources.ScriptDatabase.InitializeTrainerNames();
                Resources.ScriptDatabase.InitializeNpcTradeNames();
                Resources.ScriptDatabase.InitializeTrainerClassNames();

                // Rotom reads these enum JSONs, so they are rewritten to match this ROM.
                Resources.ScriptDatabase.ExportEnumJsons(romDatabaseFolder);
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Couldn't load script database: {ex.Message}");
                AppMessages.Error("The script database couldn't be loaded, so script editing may not work fully.", "Script database not loaded");
            }
        }
    }
}
