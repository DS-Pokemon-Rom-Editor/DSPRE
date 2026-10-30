using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using DSPRE.HgEngine;
using DSPRE.Tracking.Kinds;

namespace DSPRE.Tracking
{
    /// <summary>Starting, linking and stopping change tracking for the open project.</summary>
    public static class TrackingSetup
    {
        public const string FolderSuffix = "-dspre_changes";

        private const string GitIgnore = "*.nds\n*_DSPRE_contents/\n";
        private const string GitAttributes = "*.json text eol=lf\n*.txt text eol=lf\n*.png binary\n*.bin binary\n*.nsbmd binary\n*.nsbtx binary\n*.bdhc binary\n";

        /// <summary>Why the open project can't be tracked, or null.</summary>
        public static string WhyNot()
        {
            if (string.IsNullOrEmpty(RomInfo.WorkDir)) return "Open a ROM first.";
            if (RomInfo.IsHGE || RomInfo.IsHgEngineBaseProject || HgEngineProject.IsActive)
                return "hg-engine projects are tracked through their own checkout.";
            return null;
        }

        public static string ProjectKey => Path.GetFullPath(RomInfo.WorkDir).TrimEnd('\\', '/');

        /// <summary>The .nds next to the project folder it was extracted from, if it is still there.</summary>
        public static string SourceRom()
        {
            string folder = ProjectKey;
            string name = Path.GetFileName(folder);
            if (!name.EndsWith(RomInfo.FolderSuffix, StringComparison.Ordinal)) return null;
            string nds = Path.Combine(Path.GetDirectoryName(folder) ?? "", name.Substring(0, name.Length - RomInfo.FolderSuffix.Length) + ".nds");
            return File.Exists(nds) ? nds : null;
        }

        public static string DefaultFolder()
        {
            string folder = ProjectKey;
            string name = Path.GetFileName(folder);
            string rom = name.EndsWith(RomInfo.FolderSuffix, StringComparison.Ordinal) ? name.Substring(0, name.Length - RomInfo.FolderSuffix.Length) : name;
            return Path.Combine(Path.GetDirectoryName(folder) ?? "", rom + FolderSuffix);
        }

        /// <summary>The changes folder linked to the open project, if any.</summary>
        public static string LinkedFolder()
        {
            if (SettingsManager.Settings?.ChangesFolders.TryGetValue(ProjectKey, out string folder) == true && Directory.Exists(folder)) return folder;
            SyncState state = SyncState.Load(RomInfo.DspreDir.TrimEnd('\\', '/'));
            return state.Repo != null && Directory.Exists(state.Repo) ? state.Repo : null;
        }

        public static bool Declined => SettingsManager.Settings?.TrackingDeclined.Contains(ProjectKey) == true;

        public static void Decline()
        {
            if (SettingsManager.Settings == null || Declined) return;
            SettingsManager.Settings.TrackingDeclined.Add(ProjectKey);
            SettingsManager.Save();
        }

        /// <summary>Checks that an .nds is the same game and language as the open project.</summary>
        public static string WhyNotBase(string nds)
        {
            if (!File.Exists(nds)) return "That ROM file doesn't exist.";
            if (!BaseRom.TryReadHeader(nds, out string code, out int _)) return "That file isn't a DS ROM.";
            if (!string.Equals(code, RomInfo.RomID, StringComparison.Ordinal))
                return $"That ROM is {code}, but this project is {RomInfo.RomID}.";
            return null;
        }

        /// <summary>Makes a new changes folder for the open project and exports every difference from <paramref name="baseNds"/>.</summary>
        public static async Task<(ChangeTracker Tracker, string Error)> StartNewAsync(string folder, string baseNds)
        {
            if (WhyNot() is string why) return (null, why);
            if (WhyNotBase(baseNds) is string bad) return (null, bad);
            if (Directory.Exists(folder) && Directory.EnumerateFileSystemEntries(folder).Any(e => Path.GetFileName(e) != ".git"))
                return (null, "That folder isn't empty. Pick an empty folder, or open it as a changes folder instead.");
            if (Inside(folder, ProjectKey)) return (null, "The changes folder can't be inside the project folder.");

            (string sha, string root, string error) = await Task.Run(() => PrepareBase(baseNds)).ConfigureAwait(false);
            if (error != null) return (null, error);
            BaseRom.TryReadHeader(baseNds, out string code, out int revision);
            ChangesManifest manifest = new ChangesManifest
            {
                Game = RomInfo.GameVersion.ToString(),
                Code = code,
                Revision = revision,
                Rom = Path.GetFileName(baseNds),
                BaseSha1 = sha,
                ProjectId = ChangesManifest.NewProjectId(),
                ProjectName = RomInfo.ProjectName,
            };
            Directory.CreateDirectory(folder);
            WriteFolderFiles(folder, manifest);
            if (GitCli.IsAvailable && !GitCli.IsRepository(folder)) await GitCli.InitAsync(folder).ConfigureAwait(false);

            Link(folder, sha, baseNds);
            ChangeTracker tracker = ChangeTracker.Start(folder, manifest, root);
            await Task.Run(tracker.ExportEverything).ConfigureAwait(false);
            return (tracker, null);
        }

        public enum OpenResult { Opened, NeedsBaseRom, OtherRom, Failed }

        /// <summary>
        /// Links an existing changes folder. Its records then show as incoming. NeedsBaseRom means this machine
        /// doesn't know where its base ROM is; OtherRom means this project wasn't made from that ROM.
        /// </summary>
        public static async Task<(OpenResult Result, ChangesManifest Manifest, string Error)> OpenAsync(string folder, string baseNds = null, bool linkAsIs = false)
        {
            if (WhyNot() is string why) return (OpenResult.Failed, null, why);
            if (!ChangesManifest.TryRead(folder, out ChangesManifest manifest, out string error)) return (OpenResult.Failed, null, error);
            if (!string.Equals(manifest.Code, RomInfo.RomID, StringComparison.Ordinal))
                return (OpenResult.Failed, manifest, $"That changes folder is for {manifest.Describe()}, but this project is {RomInfo.RomID}.");
            if (Inside(folder, ProjectKey)) return (OpenResult.Failed, manifest, "The changes folder can't be inside the project folder.");

            baseNds ??= await Task.Run(() => KnownBaseRom(manifest.BaseSha1)).ConfigureAwait(false);
            if (baseNds == null) return (OpenResult.NeedsBaseRom, manifest, null);
            (string sha, string root, string prepError) = await Task.Run(() => PrepareBase(baseNds)).ConfigureAwait(false);
            if (prepError != null) return (OpenResult.Failed, manifest, prepError);
            if (sha != manifest.BaseSha1) return (OpenResult.OtherRom, manifest, null);
            if (!linkAsIs && !await Task.Run(() => MadeFromThisProject(manifest)).ConfigureAwait(false)) return (OpenResult.OtherRom, manifest, null);

            // Folders from before project ids get one now, so builds of them can be recognised.
            if (manifest.ProjectId == null)
            {
                manifest = new ChangesManifest
                {
                    Game = manifest.Game, Code = manifest.Code, Revision = manifest.Revision, Rom = manifest.Rom, BaseSha1 = manifest.BaseSha1,
                    ProjectId = ChangesManifest.NewProjectId(), ProjectName = RomInfo.ProjectName,
                };
                manifest.Write(folder);
            }
            WriteFolderFiles(folder, manifest, keepExisting: true);
            Link(folder, sha, baseNds);
            ChangeTracker tracker = ChangeTracker.Start(folder, manifest, root);
            await Task.Run(tracker.Refresh).ConfigureAwait(false);
            return (OpenResult.Opened, manifest, null);
        }

        /// <summary>
        /// Applies a changes folder made from another ROM onto the open project, then moves the folder to
        /// <paramref name="baseNds"/>, the ROM this project was made from.
        /// </summary>
        public static async Task<(IReadOnlyList<Conflict> Conflicts, List<string> Problems, string Error)> MoveToThisRomAsync(string folder, string baseNds,
            Func<IReadOnlyList<Conflict>, Task<Func<Conflict, bool>>> decide)
        {
            if (WhyNotBase(baseNds) is string bad) return (null, null, bad);
            if (!ChangesManifest.TryRead(folder, out ChangesManifest old, out string error)) return (null, null, error);
            (string sha, string root, string prepError) = await Task.Run(() => PrepareBase(baseNds)).ConfigureAwait(false);
            if (prepError != null) return (null, null, prepError);
            BaseRom.TryReadHeader(baseNds, out string code, out int revision);
            ChangesManifest moved = new ChangesManifest { Game = old.Game, Code = code, Revision = revision, Rom = Path.GetFileName(baseNds), BaseSha1 = sha, ProjectId = old.ProjectId ?? ChangesManifest.NewProjectId(), ProjectName = old.ProjectName ?? RomInfo.ProjectName };

            ChangeTracker tracker = ChangeTracker.Start(folder, moved, root);
            List<Conflict> conflicts = new List<Conflict>();
            List<string> problems = null;
            await Task.Run(() => tracker.PlanApplyAll(c => { conflicts.Add(c); return true; }, out problems)).ConfigureAwait(false);
            Func<Conflict, bool> choice = conflicts.Count == 0 ? _ => true : await decide(conflicts).ConfigureAwait(false);
            if (choice == null)
            {
                ChangeTracker.Stop();
                return (conflicts, problems, "Nothing was changed.");
            }
            Dictionary<TrackedFile, byte[]> writes = await Task.Run(() => tracker.PlanApplyAll(choice, out problems)).ConfigureAwait(false);
            if (!tracker.TryWrite(writes, out string writeError))
            {
                ChangeTracker.Stop();
                return (conflicts, problems, writeError);
            }
            moved.Write(folder);
            Link(folder, sha, baseNds);
            await Task.Run(tracker.ExportEverything).ConfigureAwait(false);
            return (conflicts, problems, null);
        }

        /// <summary>
        /// True when the open project already belongs to this changes folder, came from its original ROM, or came from a ROM
        /// built from it here. A project without its .nds beside it can't be checked, so it's given the benefit of the doubt.
        /// </summary>
        private static bool MadeFromThisProject(ChangesManifest manifest)
        {
            SyncState state = SyncState.Load(RomInfo.DspreDir.TrimEnd('\\', '/'));
            if (manifest.ProjectId != null && state.ProjectId == manifest.ProjectId) return true;
            string source = SourceRom();
            if (source == null) return true;
            string sha = BaseRom.Sha1Of(source);
            if (sha == manifest.BaseSha1) return true;
            return manifest.ProjectId != null && SettingsManager.Settings?.BuiltRoms.TryGetValue(sha, out string project) == true && project == manifest.ProjectId;
        }

        /// <summary>Remembers a ROM built while tracking, so opening it later links it to its changes folder.</summary>
        public static void RecordBuild(string nds)
        {
            string project = ChangeTracker.Current?.Manifest.ProjectId;
            if (project == null || SettingsManager.Settings == null || !File.Exists(nds)) return;
            SettingsManager.Settings.BuiltRoms[BaseRom.Sha1Of(nds)] = project;
            SettingsManager.Save();
        }

        /// <summary>Resumes tracking on project load, when a folder is linked and its base ROM is known.</summary>
        public static async Task<string> ResumeAsync()
        {
            if (WhyNot() != null) return null;
            string folder = LinkedFolder();
            if (folder == null) return null;
            (OpenResult result, ChangesManifest _, string error) = await OpenAsync(folder).ConfigureAwait(false);
            return result switch
            {
                OpenResult.Opened => null,
                OpenResult.NeedsBaseRom => "The changes folder's original ROM isn't on this machine. Open Track changes to choose it.",
                OpenResult.OtherRom => "This project wasn't made from the changes folder's ROM.",
                _ => error,
            };
        }

        public static void Unlink()
        {
            ChangeTracker.Stop();
            // The project remembers its folder too, and would link again at the next load.
            string projectDir = RomInfo.DspreDir?.TrimEnd('\\', '/');
            if (!string.IsNullOrEmpty(projectDir))
            {
                SyncState state = SyncState.Load(projectDir);
                if (state.Repo != null)
                {
                    state.Repo = null;
                    state.Save(projectDir);
                }
            }
            if (SettingsManager.Settings == null) return;
            SettingsManager.Settings.ChangesFolders.Remove(ProjectKey);
            SettingsManager.Save();
        }

        private static bool Inside(string path, string root) =>
            (Path.GetFullPath(path).TrimEnd('\\', '/') + Path.DirectorySeparatorChar)
            .StartsWith(Path.GetFullPath(root).TrimEnd('\\', '/') + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

        private static string KnownBaseRom(string sha)
        {
            if (SettingsManager.Settings?.BaseRoms.TryGetValue(sha, out string nds) == true && File.Exists(nds)) return nds;
            string source = SourceRom();
            return source != null && BaseRom.Sha1Of(source) == sha ? source : null;
        }

        private static (string Sha, string Root, string Error) PrepareBase(string nds)
        {
            try
            {
                string sha = BaseRom.Sha1Of(nds);
                return BaseRom.TryExtract(nds, sha, RomInfo.IsDsRomProject, out string root, out string error) ? (sha, root, null) : (sha, null, error);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return (null, null, "The ROM couldn't be read: " + ex.Message);
            }
        }

        private static void Link(string folder, string sha, string nds)
        {
            if (SettingsManager.Settings == null) return;
            SettingsManager.Settings.ChangesFolders[ProjectKey] = Path.GetFullPath(folder);
            SettingsManager.Settings.BaseRoms[sha] = Path.GetFullPath(nds);
            SettingsManager.Settings.TrackingDeclined.Remove(ProjectKey);
            SettingsManager.Save();
        }

        private static void WriteFolderFiles(string folder, ChangesManifest manifest, bool keepExisting = false)
        {
            if (!keepExisting || !File.Exists(ChangesManifest.PathIn(folder))) manifest.Write(folder);
            if (!File.Exists(Path.Combine(folder, ".gitignore"))) PlainFiles.WriteText(Path.Combine(folder, ".gitignore"), GitIgnore);
            if (!File.Exists(Path.Combine(folder, ".gitattributes"))) PlainFiles.WriteText(Path.Combine(folder, ".gitattributes"), GitAttributes);
            ChangeSchemas.Write(Path.Combine(folder, ".schema"));
        }
    }
}
