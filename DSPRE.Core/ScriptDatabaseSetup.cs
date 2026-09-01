using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LibGit2Sharp;
using static DSPRE.RomInfo;

namespace DSPRE
{
    /// <summary>
    /// Script-command database bootstrap: pulls updates from the scrcmd-database git repo and
    /// initializes the per-ROM database JSONs. Moved out of the WinForms <c>Helpers</c> class, 
    /// this is core logic (LibGit2Sharp is cross-platform; user messages go through
    /// <see cref="AppMessages"/>).
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
                    using (var repo = new Repository(pathToDbRepo))
                    {
                        Remote remote = repo.Network.Remotes.Add("origin", "https://github.com/DS-Pokemon-Rom-Editor/scrcmd-database.git");
                        Commands.Fetch(repo, remote.Name, new string[] { "refs/heads/main:refs/heads/main" }, null, null);

                        // Check if main branch exists
                        Branch main = repo.Branches["main"] ?? repo.CreateBranch("main", repo.Branches["refs/heads/main"].Tip);
                        repo.Branches.Update(main, b => b.TrackedBranch = "refs/remotes/origin/main");
                        Commands.Checkout(repo, main);
                    }
                }

                using (var repo = new Repository(pathToDbRepo))
                {
                    var remote = repo.Network.Remotes["origin"];
                    try
                    {
                        // Reset any changes
                        if (repo.Head.Tip != null)
                        {
                            repo.Reset(ResetMode.Hard);
                        }

                        // Clean up untracked files
                        foreach (var item in repo.RetrieveStatus().Untracked)
                        {
                            string fullPath = Path.Combine(pathToDbRepo, item.FilePath);
                            if (File.Exists(fullPath))
                                File.Delete(fullPath);
                            else if (Directory.Exists(fullPath))
                                Directory.Delete(fullPath, true);
                        }

                        Commands.Fetch(repo, remote.Name, remote.FetchRefSpecs.Select(x => x.Specification), null, null);

                        // Get the remote main branch and force checkout
                        var remoteBranch = repo.Branches["origin/main"];
                        var options = new CheckoutOptions { CheckoutModifiers = CheckoutModifiers.Force };
                        Commands.Checkout(repo, repo.Branches["main"], options);
                        repo.Reset(ResetMode.Hard, remoteBranch.Tip);

                        AppLogger.Info("Script databases updated successfully");
                        if (!silent)
                        {
                            AppMessages.Info("Script database updated successfully.", "Success");
                        }
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Warn($"Could not fetch updates: {ex.Message}");
                        if (!silent)
                        {
                            AppMessages.Warning("Could not fetch database updates. Using local database files.", "Warning");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"Could not access git repository: {ex.Message}");
                if (!silent)
                {
                    AppMessages.Warning("Could not access database repository. Using local database files.", "Warning");
                }
            }
        }

        public static void InitializeScriptDatabase(string romFileName, GameFamilies gameFamily, GameVersions gameVersion)
        {
            string baseFileName = Path.GetFileNameWithoutExtension(romFileName);
            string romFileNameClean = baseFileName.EndsWith("_DSPRE_contents")
                ? baseFileName.Substring(0, baseFileName.Length - "_DSPRE_contents".Length)
                : baseFileName;

            if (SettingsManager.Settings?.automaticallyUpdateDBs == true)   // Settings may be unloaded when running headless
            {
                CheckForDatabaseUpdates();
            }

            string editedDatabasesDir = Path.Combine(AppPaths.DatabasePath, "edited_databases");
            Directory.CreateDirectory(editedDatabasesDir);

            // Create ROM-specific folder
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
                // The base databases come from the scrcmd-database git repo (cloned into
                // AppPaths.DatabasePath) or from a bundled databases/ folder copied by
                // DatabaseSetup. On a fresh machine with no network, neither may exist, 
                // degrade gracefully instead of failing the whole ROM load.
                if (!File.Exists(databaseJsonPath) && SettingsManager.Settings?.automaticallyUpdateDBs != true)
                {
                    CheckForDatabaseUpdates();   // one clone attempt even when auto-update is off
                }
                if (!File.Exists(databaseJsonPath))
                {
                    AppLogger.Error($"Script database not found: {databaseJsonPath}");
                    AppMessages.Error(
                        "The script command database could not be found:\n" + databaseJsonPath +
                        "\n\nIt is normally downloaded automatically (internet required) or bundled as a " +
                        "'databases' folder next to the DSPRE executable.\n\n" +
                        "The ROM will still load, but script editing features will be limited.",
                        "Script database missing");
                    return;
                }
                File.Copy(databaseJsonPath, targetJsonPath);
            }

            try
            {
                ScriptDatabaseJsonLoader.InitializeFromJson(targetJsonPath, gameVersion);

                // Unpack text archives NARC if needed - required for reading Pokemon/Item/Move/Trainer names
                DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.textArchives });

                // Initialize enum dictionaries from ROM data (Pokemon, Items, Moves, Trainers)
                Resources.ScriptDatabase.InitializePokemonNames();
                Resources.ScriptDatabase.InitializeItemNames();
                Resources.ScriptDatabase.InitializeMoveNames();
                Resources.ScriptDatabase.InitializeTrainerNames();

                // Export the enum JSONs for external tools (like Rotom) to use
                // Always regenerate to ensure they match current ROM data
                Resources.ScriptDatabase.ExportEnumJsons(romDatabaseFolder);
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Failed to load script database: {ex.Message}");
                AppMessages.Error("Failed to load script database. Script editing features may be limited.", "Error");
            }
        }
    }
}
