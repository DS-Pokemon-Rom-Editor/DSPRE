using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using DSPRE.Avalonia.ViewModels.Shell;
using DSPRE.Tracking;

namespace DSPRE.Avalonia.Views.Shell
{
    /// <summary>What the summary bar's and the changes panel's buttons do, with the questions they ask.</summary>
    public static class TrackingActions
    {
        private static readonly FilePickerFileType[] RomFilter = { new FilePickerFileType("DS ROM") { Patterns = new[] { "*.nds" } } };

        public static async Task ReportAsync(Window owner, string error, string title)
        {
            if (error != null) await DialogHelper.ShowErrorAsync(error, title, owner);
        }

        public static async Task StartAsync(Window owner, ChangesViewModel vm)
        {
            if (owner == null || vm == null) return;
            string nds = await PickBaseRomAsync(owner);
            if (nds == null) return;
            string folder = TrackingSetup.DefaultFolder();
            if (Directory.Exists(folder) && Directory.EnumerateFileSystemEntries(folder).Any())
            {
                if (File.Exists(ChangesManifest.PathIn(folder)))
                {
                    if (await DialogHelper.AskTwoWayAsync($"{Path.GetFileName(folder)} already holds changes. Open it instead?", "Start tracking", "Open it", "Cancel", owner))
                        await OpenAsync(owner, vm, folder);
                    return;
                }
                folder = await DialogHelper.OpenFolderAsync(owner, "Choose an empty folder for the changes");
                if (folder == null) return;
            }
            await RunAsync(vm, async () =>
            {
                (ChangeTracker tracker, string error) = (null, null);
                using (Busy("Starting to track changes…", "The first time reads every file of the ROM and the original."))
                    (tracker, error) = await TrackingSetup.StartNewAsync(folder, nds);
                if (error != null)
                {
                    await DialogHelper.ShowErrorAsync(error, "Start tracking", owner);
                    return;
                }
                await vm.RefreshViewAsync();
                vm.Message = null;
                SaveNotice.Show("Tracking changes in " + Path.GetFileName(folder));
                if (vm.CanCommit) await CommitAsync(owner, vm, "Start tracking " + RomInfo.GetGameDisplayName());
            });
        }

        /// <summary>The ROM changes are measured against: the .nds the project came from, or one the user picks.</summary>
        private static async Task<string> PickBaseRomAsync(Window owner, string asking = null)
        {
            string source = TrackingSetup.SourceRom();
            if (source != null && asking == null)
            {
                DialogHelper.MsgResult answer = await DialogHelper.AskThreeWayAsync(
                    $"Changes are measured against the original ROM. Use {Path.GetFileName(source)}?\n\nIf that file is already edited, choose the unedited ROM instead, so the edits in it are tracked too.",
                    "Original ROM", "Use it", "Choose another…", "Cancel");
                if (answer == DialogHelper.MsgResult.Cancel) return null;
                if (answer == DialogHelper.MsgResult.Yes) return source;
            }
            string nds = await DialogHelper.OpenFileAsync(owner, asking ?? "Choose the original ROM", RomFilter);
            if (nds == null) return null;
            if (TrackingSetup.WhyNotBase(nds) is string why)
            {
                await DialogHelper.ShowErrorAsync(why, "Original ROM", owner);
                return null;
            }
            return nds;
        }

        public static async Task OpenFolderAsync(Window owner, ChangesViewModel vm)
        {
            if (owner == null || vm == null) return;
            string folder = await DialogHelper.OpenFolderAsync(owner, "Choose a changes folder");
            if (folder != null) await OpenAsync(owner, vm, folder);
        }

        /// <summary>Links a changes folder, asking for its ROM or moving it to this project's ROM when needed.</summary>
        public static async Task OpenAsync(Window owner, ChangesViewModel vm, string folder, string nds = null)
        {
            await RunAsync(vm, async () =>
            {
                TrackingSetup.OpenResult result;
                ChangesManifest manifest;
                string error;
                using (Busy("Opening the changes folder…", "Reading the original ROM and the changes."))
                    (result, manifest, error) = await TrackingSetup.OpenAsync(folder, nds);
                switch (result)
                {
                    case TrackingSetup.OpenResult.Failed:
                        await DialogHelper.ShowErrorAsync(error, "Open a changes folder", owner);
                        return;
                    case TrackingSetup.OpenResult.NeedsBaseRom:
                    {
                        string picked = await DialogHelper.OpenFileAsync(owner, $"Choose {manifest.Rom ?? "the original ROM"}", RomFilter);
                        if (picked == null) return;
                        if (BaseRom.Sha1Of(picked) != manifest.BaseSha1
                            && !await DialogHelper.AskTwoWayAsync($"That isn't the ROM the changes were made from ({manifest.Rom}). Apply them to this project anyway, and move the folder to that ROM?",
                                "Different ROM", "Apply and move", "Cancel", owner))
                            return;
                        if (BaseRom.Sha1Of(picked) == manifest.BaseSha1)
                        {
                            await OpenAsync(owner, vm, folder, picked);
                            return;
                        }
                        await MoveAsync(owner, vm, folder, picked);
                        return;
                    }
                    case TrackingSetup.OpenResult.OtherRom:
                    {
                        DialogHelper.MsgResult answer = await DialogHelper.AskThreeWayAsync(
                            $"This project wasn't made from {manifest.Rom}, or from a ROM DSPRE built from {manifest.ProjectName ?? "this changes folder"}.\n\n"
                            + "Link it as it is if it's a build of this hack. Apply and move uses this ROM as the new original instead.",
                            "Different ROM", "Link as is", "Apply and move", "Cancel");
                        if (answer == DialogHelper.MsgResult.Cancel) return;
                        if (answer == DialogHelper.MsgResult.Yes)
                        {
                            await OpenLinkedAsIsAsync(owner, vm, folder);
                            return;
                        }
                        string mine = await PickBaseRomAsync(owner);
                        if (mine != null) await MoveAsync(owner, vm, folder, mine);
                        return;
                    }
                }
                await vm.RefreshViewAsync();
                vm.Message = null;
                SaveNotice.Show("Tracking changes in " + Path.GetFileName(folder));
                if (vm.HasIncoming || vm.HasConflicts) vm.PanelOpen = true;
            });
        }

        private static async Task OpenLinkedAsIsAsync(Window owner, ChangesViewModel vm, string folder)
        {
            TrackingSetup.OpenResult result;
            string error;
            using (Busy("Opening the changes folder…", "Reading the original ROM and the changes."))
                (result, _, error) = await TrackingSetup.OpenAsync(folder, linkAsIs: true);
            await vm.RefreshViewAsync();
            if (result != TrackingSetup.OpenResult.Opened)
            {
                await DialogHelper.ShowErrorAsync(error ?? "The changes folder couldn't be linked.", "Open a changes folder", owner);
                return;
            }
            vm.Message = null;
            SaveNotice.Show("Tracking changes in " + Path.GetFileName(folder));
            if (vm.HasIncoming || vm.HasConflicts) vm.PanelOpen = true;
        }

        private static async Task MoveAsync(Window owner, ChangesViewModel vm, string folder, string nds)
        {
            List<string> problems;
            string error;
            using (Busy("Applying the changes to this ROM…"))
                (_, problems, error) = await TrackingSetup.MoveToThisRomAsync(folder, nds, conflicts => ConflictDialog.AskAsync(owner, conflicts));
            await vm.RefreshViewAsync();
            if (error != null)
            {
                await DialogHelper.ShowErrorAsync(error, "Different ROM", owner);
                return;
            }
            if (problems?.Count > 0)
                await DialogHelper.ShowCopyableTextAsync("These changes couldn't be applied:\n\n" + string.Join("\n", problems), "Some changes skipped", owner);
            SaveNotice.Show("Applied and moved " + Path.GetFileName(folder));
        }

        public static async Task CloneAsync(Window owner, ChangesViewModel vm, string url = null)
        {
            if (owner == null || vm == null) return;
            url ??= await DialogHelper.PromptTextAsync("Repo address (https://github.com/you/your-hack)", "Clone from git", "", owner);
            if (string.IsNullOrWhiteSpace(url)) return;
            string name = url.TrimEnd('/').Split('/', '\\', ':').LastOrDefault()?.Replace(".git", "") ?? "changes";
            string folder = Path.Combine(Path.GetDirectoryName(TrackingSetup.DefaultFolder()) ?? "", name);
            bool taken = Directory.Exists(folder) && Directory.EnumerateFileSystemEntries(folder).Any();
            DialogHelper.MsgResult where = taken ? DialogHelper.MsgResult.No : await DialogHelper.AskThreeWayAsync(
                "Clone into " + folder + "?", "Clone from git", "Clone here", "Somewhere else…", "Cancel");
            if (where == DialogHelper.MsgResult.Cancel) return;
            if (where == DialogHelper.MsgResult.No)
            {
                string parent = await DialogHelper.OpenFolderAsync(owner, taken ? $"{name} exists; choose where to put the clone" : "Choose where to put the clone");
                if (parent == null) return;
                folder = Path.Combine(parent, name);
            }
            string error = null;
            await RunAsync(vm, async () =>
            {
                CliRunner.Result r;
                using (Busy("Cloning " + name + "…"))
                    r = await GitCli.CloneAsync(url.Trim(), folder);
                if (!r.Success) error = r.Message;
            });
            if (error != null)
            {
                await DialogHelper.ShowErrorAsync(error, "Clone from git", owner);
                return;
            }
            await OpenAsync(owner, vm, folder);
        }

        private static readonly FilePickerFileType[] PatchFilter = { new FilePickerFileType("DSPRE patch") { Patterns = new[] { "*" + DsPatch.Extension } } };

        /// <summary>The vanilla ROM an xdelta starts from, since that's what players have: the base ROM when it's vanilla, else one the user picks.</summary>
        private static async Task<string> VanillaRomAsync(Window owner, ChangeTracker tracker)
        {
            string vanilla = VanillaDumps.NameOf(tracker.Manifest.BaseSha1);
            string source = TrackingSetup.SourceRom();
            if (vanilla != null && source != null && string.Equals(BaseRom.Sha1Of(source), tracker.Manifest.BaseSha1, StringComparison.OrdinalIgnoreCase))
            {
                DialogHelper.MsgResult use = await DialogHelper.AskThreeWayAsync(
                    $"Make the xdelta from the vanilla ROM this project started from?\n\n{vanilla}\n{Path.GetFileName(source)}",
                    "Xdelta base ROM", "Use it", "Choose another…", "Cancel");
                if (use == DialogHelper.MsgResult.Yes) return source;
                if (use != DialogHelper.MsgResult.No) return null;
            }
            string picked = await DialogHelper.OpenFileAsync(owner, vanilla != null ? "Choose the vanilla ROM: " + vanilla : "Choose a vanilla ROM", RomFilter);
            if (picked == null) return null;
            if (VanillaDumps.NameOf(BaseRom.Sha1Of(picked)) == null
                && !await DialogHelper.AskTwoWayAsync("That ROM isn't a known vanilla dump. Players need this exact ROM to apply the xdelta. Use it anyway?",
                    "Xdelta base ROM", "Use it", "Cancel", owner))
                return null;
            return picked;
        }

        /// <summary>Makes a release: a DSPRE patch, an xdelta, a changelog and a GitHub release, as picked.</summary>
        public static async Task ExportPatchAsync(Window owner, ChangesViewModel vm)
        {
            ChangeTracker tracker = ChangeTracker.Current;
            if (owner == null || tracker == null) return;
            if (vm.HasChanged || vm.HasUntracked)
            {
                if (!await DialogHelper.AskTwoWayAsync("Some changes aren't committed. The release includes them too.", "Make a release", "Continue", "Cancel", owner)) return;
            }
            string remote = vm.FolderPath == null ? null : await GitCli.RemoteUrlAsync(vm.FolderPath);
            string noGitHub = !GitHubCli.IsAvailable ? "Needs the GitHub CLI"
                : remote == null || !remote.Contains("github.com", StringComparison.OrdinalIgnoreCase) ? "The changes folder isn't on GitHub" : null;
            string vanilla = VanillaDumps.NameOf(tracker.Manifest.BaseSha1);
            string source = TrackingSetup.SourceRom();
            string xdeltaBase = vanilla == null ? "This project's base ROM isn't vanilla, so you'll pick one."
                : "From the vanilla ROM: " + vanilla + (source != null ? "\n" + Path.GetFileName(source) : "");
            ReleaseWindow.Choice choice = await ReleaseWindow.ShowAsync(owner, noGitHub, tracker, xdeltaBase);
            if (choice == null) return;
            // A release is one folder named after it, holding every file it makes.
            string stem = ChangesJson.Slug((tracker.Manifest.ProjectName ?? RomInfo.ProjectName) + (choice.Name == null ? "" : "-" + choice.Name));
            string parent = await DialogHelper.OpenFolderAsync(owner, "Where to put the " + stem + " folder");
            if (parent == null) return;
            string folder = Path.Combine(parent, stem);
            if (Directory.Exists(folder) && Directory.EnumerateFileSystemEntries(folder).Any()
                && !await DialogHelper.AskTwoWayAsync(stem + " already has files. Replace the ones this release makes?", "Make a release", "Replace", "Cancel", owner))
                return;
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, stem + DsPatch.Extension);

            // The xdelta goes from the original ROM to a build of this project, so both come first.
            string original = null, built = null, builtSha1 = null;
            if (choice.Xdelta)
            {
                original = await VanillaRomAsync(owner, tracker);
                if (original == null) return;
                built = Path.Combine(Path.GetTempPath(), "dspre-release-" + Guid.NewGuid().ToString("N") + ".nds");
                if (owner is not MainWindowView main || !await main.BuildRomAsync(built)) return;
                builtSha1 = BaseRom.Sha1Of(built);
            }
            await RunAsync(vm, async () =>
            {
                List<string> files = new List<string>();
                try
                {
                    using (Busy(choice.Patch ? "Exporting the patch…" : "Making the xdelta…"))
                    {
                        if (choice.Patch)
                        {
                            await Task.Run(() => DsPatch.Export(tracker, path, choice.Name, builtSha1));
                            files.Add(path);
                        }
                        if (built != null)
                        {
                            string xdelta = Path.ChangeExtension(path, ".xdelta");
                            await Task.Run(() => PlainFiles.WriteBytes(xdelta, Vcdiff.Encode(File.ReadAllBytes(original), File.ReadAllBytes(built))));
                            files.Add(xdelta);
                        }
                    }
                }
                finally
                {
                    if (built != null && File.Exists(built)) File.Delete(built);
                }
                string notes = choice.Notes;
                if (choice.Changelog)
                {
                    using (Busy("Writing the changelog…"))
                    {
                        // Written before the release is tagged, so it covers changes since the previous release.
                        (string text, List<(string File, byte[] Png)> pictures) = await ChangelogWindow.ForReleaseAsync(tracker, choice.Name, true, notes);
                        string changelog = Path.Combine(folder, "changelog.md");
                        PlainFiles.WriteBytes(changelog, System.Text.Encoding.UTF8.GetBytes(ChangelogWindow.Rewrite(text, ChangelogWindow.PictureFolder(choice.Name), "pictures")));
                        ChangelogWindow.SavePictures(Path.Combine(folder, "pictures"), pictures);
                        files.Add(changelog);
                        // GitHub shows the title itself; its notes are the release notes, then the changelog without pictures.
                        if (choice.GitHub) notes = ChangelogWindow.WithoutTitle((await ChangelogWindow.ForReleaseAsync(tracker, choice.Name, false, notes)).Text);
                    }
                }
                bool tagged = false;
                if (choice.Name != null && vm.IsRepository && !(await GitCli.TagsAsync(vm.FolderPath)).Contains(choice.Name))
                    tagged = (await GitCli.TagAsync(vm.FolderPath, choice.Name)).Success;
                if (choice.GitHub)
                {
                    // The release uses the folder's pushed commit and tag, so they go up first.
                    CliRunner.Result r;
                    using (Busy("Publishing the release…"))
                    {
                        r = vm.HasAhead || tagged ? await GitCli.PushAsync(vm.FolderPath) : new CliRunner.Result { ExitCode = 0 };
                        if (r.Success) r = await GitHubCli.ReleaseAsync(vm.FolderPath, choice.Name, notes, files.Where(f => f.EndsWith(DsPatch.Extension, StringComparison.Ordinal) || f.EndsWith(".xdelta", StringComparison.Ordinal)));
                    }
                    if (!r.Success)
                    {
                        await DialogHelper.ShowErrorAsync("The files were saved, but the GitHub release wasn't made:\n" + r.Message, "Make a release", owner);
                        return;
                    }
                }
                SaveNotice.Show("Made " + stem + ": " + string.Join(", ", files.Select(Path.GetFileName)));
            });
        }

        /// <summary>Applies a .dspatch onto the open project after showing what it would do.</summary>
        public static async Task ApplyPatchAsync(Window owner, ChangesViewModel vm)
        {
            if (owner == null || !AvaloniaEditorLauncher.IsRomLoaded) return;
            string path = await DialogHelper.OpenFileAsync(owner, "Apply a DSPRE patch", PatchFilter);
            if (path == null) return;
            IReadOnlyList<UnsavedChangesDialog.UnsavedEditorInfo> unsaved = OpenEditors.GetUnsavedEditors(owner as MainWindowView);
            if (unsaved.Count > 0)
            {
                await DialogHelper.ShowErrorAsync("Save or discard your edits in " + string.Join(", ", unsaved.Select(u => u.EditorName).Distinct()) + " first. Applying would replace what they show.",
                    "Patch not applied", owner);
                return;
            }
            DsPatch.Plan plan;
            try
            {
                using (Busy("Reading the patch…", "Checking each change against this project."))
                    plan = await Task.Run(() => DsPatch.PlanApply(path, _ => false));
            }
            catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is System.Text.Json.JsonException)
            {
                await DialogHelper.ShowErrorAsync(ex.Message, "Patch not applied", owner);
                return;
            }
            string what = (plan.Info.ProjectName ?? Path.GetFileNameWithoutExtension(path)) + (plan.Info.Release != null ? " " + plan.Info.Release : "");
            Dictionary<(string Record, string Path), bool> usePatch = await PatchWindow.ShowAsync(owner, plan, what);
            if (usePatch == null) return;
            await RunAsync(vm, async () =>
            {
                string error = null;
                bool ok;
                using (Busy("Applying the patch…", "Open editors reload what changes."))
                {
                    if (usePatch.ContainsValue(true))
                        plan = await Task.Run(() => DsPatch.PlanApply(path, c => usePatch.TryGetValue((c.Record, c.Path), out bool take) && take));
                    ok = await Task.Run(() => DsPatch.Apply(plan, out error));
                }
                if (!ok)
                {
                    await DialogHelper.ShowErrorAsync(error ?? "The patch couldn't be applied.", "Patch not applied", owner);
                    return;
                }
                ReopenStaleEditors(owner, plan);
                SaveNotice.Show("Applied " + what);
            });
        }

        /// <summary>Puts back every file the last apply in this project changed.</summary>
        public static async Task UndoApplyAsync(Window owner, ChangesViewModel vm)
        {
            if (owner == null || !ChangeTracker.CanUndoApply) return;
            IReadOnlyList<UnsavedChangesDialog.UnsavedEditorInfo> unsaved = OpenEditors.GetUnsavedEditors(owner as MainWindowView);
            if (unsaved.Count > 0)
            {
                await DialogHelper.ShowErrorAsync("Save or discard your edits in " + string.Join(", ", unsaved.Select(u => u.EditorName).Distinct()) + " first. Undoing would replace what they show.",
                    "Apply not undone", owner);
                return;
            }
            List<TrackedFile> touched = ChangeTracker.LastApply.Before.Keys.ToList();
            if (!await DialogHelper.AskTwoWayAsync("Put back the " + Plural.Of(touched.Count, "file") + " the last apply changed?", "Undo apply", "Undo apply", "Cancel", owner)) return;
            await RunAsync(vm, async () =>
            {
                string error = null;
                bool ok;
                using (Busy("Undoing the apply…", "Open editors reload what changes."))
                    ok = await Task.Run(() => ChangeTracker.UndoLastApply(out error));
                if (!ok)
                {
                    await DialogHelper.ShowErrorAsync(error ?? "The apply couldn't be undone.", "Apply not undone", owner);
                    return;
                }
                ChangeKinds kinds = ChangeKinds.ForOpenGame();
                List<(ChangeKind, string)> records = new List<(ChangeKind, string)>();
                foreach (TrackedFile f in touched)
                    if (kinds.TryOwner(f, out ChangeKind kind, out string id)) records.Add((kind, id));
                ReopenStaleEditors(owner, records.Distinct());
                SaveNotice.Show("Undid the last apply");
            });
        }

        public static void ShowSetup(Window owner, ChangesViewModel vm)
        {
            if (vm == null) return;
            _ = TrackingSetupWindow.ShowAsync(owner, vm);
        }

        public static Task CommitAsync(Window owner, ChangesViewModel vm) => CommitAsync(owner, vm, null);

        private static async Task CommitAsync(Window owner, ChangesViewModel vm, string suggested)
        {
            if (vm == null || !vm.CanCommit) return;
            string message = await CommitDialog.AskAsync(owner, vm.CommitCount, suggested);
            if (string.IsNullOrWhiteSpace(message)) return;
            string error = await vm.CommitAsync(message);
            if (error == GitMessages.NeedsIdentity)
            {
                if (await IdentityDialog.AskAsync(owner)) error = await vm.CommitAsync(message);
                else return;
            }
            await ReportAsync(owner, error, "Not committed");
        }

        public static async Task PullAsync(Window owner, ChangesViewModel vm)
        {
            if (vm == null) return;
            GitSync.PullResult result = await vm.PullAsync();
            if (result.Outcome == GitSync.Outcome.NeedsCommit)
            {
                if (!await DialogHelper.AskTwoWayAsync("Commit your changes before pulling, so nothing of yours is overwritten.", "Pull", "Commit…", "Cancel", owner)) return;
                await CommitAsync(owner, vm);
                // Ask git again, because the panel's rows may be stale.
                result = await vm.PullAsync();
                if (result.Outcome == GitSync.Outcome.NeedsCommit) return;
            }
            switch (result.Outcome)
            {
                case GitSync.Outcome.Failed:
                    await ReportAsync(owner, result.Error, "Not pulled");
                    return;
                case GitSync.Outcome.NeedsChoices:
                    await MergeWindow.ShowAsync(owner, vm, result.Merge);
                    break;
            }
            if (vm.HasIncoming || vm.HasConflicts) vm.PanelOpen = true;
        }

        /// <summary>Reopens the conflict window for a merge left open.</summary>
        public static async Task ResumeMergeAsync(Window owner, ChangesViewModel vm)
        {
            ChangeTracker tracker = ChangeTracker.Current;
            if (vm == null || tracker == null) return;
            GitSync.PullResult result = await GitSync.ResumeAsync(tracker);
            await vm.RefreshAsync();
            if (result.Outcome == GitSync.Outcome.NeedsChoices) await MergeWindow.ShowAsync(owner, vm, result.Merge);
            else await ReportAsync(owner, result.Error, "Merge not finished");
        }

        public static async Task PushAsync(Window owner, ChangesViewModel vm)
        {
            if (vm == null) return;
            string error = await vm.PushAsync();
            if (error == GitMessages.PushedFirst)
            {
                if (await DialogHelper.AskTwoWayAsync(error, "Not pushed", "Pull", "Cancel", owner)) await PullAsync(owner, vm);
                return;
            }
            await ReportAsync(owner, error, "Not pushed");
        }

        /// <summary>Applies what came in; records also changed here are asked about first.</summary>
        public static async Task ApplyIncomingAsync(Window owner, ChangesViewModel vm)
        {
            if (vm == null) return;
            List<IncomingRecord> clean = vm.Incoming.Select(r => r.Incoming).ToList();
            List<IncomingRecord> clashing = vm.Conflicts.Where(r => r.Incoming != null).Select(r => r.Incoming).ToList();
            if (clashing.Count > 0)
            {
                DialogHelper.MsgResult answer = await DialogHelper.AskThreeWayAsync(
                    $"{Plural.Of(clashing.Count, "record")} {(clashing.Count == 1 ? "has" : "have")} both local and incoming changes:\n\n" + string.Join("\n", clashing.Take(12).Select(c => c.Title))
                    + (clashing.Count > 12 ? "\n…" : ""),
                    "Changed in both places", "Use incoming", "Keep local", "Decide later");
                if (answer == DialogHelper.MsgResult.Yes) clean.AddRange(clashing);
                else if (answer == DialogHelper.MsgResult.No) await vm.KeepProjectAsync(clashing);
            }
            if (clean.Count == 0) return;
            IReadOnlyList<UnsavedChangesDialog.UnsavedEditorInfo> unsaved = OpenEditors.GetUnsavedEditors(owner as MainWindowView);
            if (unsaved.Count > 0)
            {
                await DialogHelper.ShowErrorAsync("Save or discard your edits in " + string.Join(", ", unsaved.Select(u => u.EditorName).Distinct()) + " first. Applying would replace what they show.",
                    "Changes not applied", owner);
                return;
            }
            string applyError;
            using (Busy("Applying " + Plural.Of(clean.Count, "change") + "…", "Open editors reload what changes."))
                applyError = await vm.ApplyIncomingAsync(clean);
            await ReportAsync(owner, applyError, "Changes not applied");
            if (applyError == null) ReopenStaleEditors(owner, clean);
        }

        // Editors that read their records once at open, by the view or view model type the window holds.
        private static readonly Dictionary<string, string[]> ReadOnceEditors = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["trainer"] = new[] { "TrainerEditorViewModel" },
            ["personal"] = new[] { "PokemonEditorViewModel" },
            ["learnset"] = new[] { "PokemonEditorViewModel" },
            ["evolution"] = new[] { "PokemonEditorViewModel" },
            ["egg-moves"] = new[] { "PokemonEditorViewModel" },
            ["move"] = new[] { "MoveDataEditorView" },
            ["item"] = new[] { "ItemEditorViewModel" },
            ["trade"] = new[] { "TradeEditorView" },
            ["encounters"] = new[] { "WildEditorDPPtViewModel", "WildEditorHGSSViewModel" },
        };

        /// <summary>Closes editors that would keep showing the old values, and opens them again at an applied record.</summary>
        private static void ReopenStaleEditors(Window owner, IReadOnlyList<IncomingRecord> applied) =>
            ReopenStaleEditors(owner, applied.Select(r => (r.Kind, r.Id)));

        private static void ReopenStaleEditors(Window owner, DsPatch.Plan plan)
        {
            ChangeKinds kinds = ChangeKinds.ForOpenGame();
            List<(ChangeKind, string)> records = new List<(ChangeKind, string)>();
            foreach (TrackedFile f in plan.Writes.Keys)
                if (kinds.TryOwner(f, out ChangeKind kind, out string id)) records.Add((kind, id));
            ReopenStaleEditors(owner, records.Distinct());
        }

        private static void ReopenStaleEditors(Window owner, IEnumerable<(ChangeKind Kind, string Id)> applied)
        {
            if (global::Avalonia.Application.Current?.ApplicationLifetime is not global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop) return;
            foreach (IGrouping<string, (ChangeKind Kind, string Id)> kind in applied.GroupBy(r => r.Kind.Name))
            {
                if (!ReadOnceEditors.TryGetValue(kind.Key, out string[] types)) continue;
                List<Window> stale = desktop.Windows.Where(w => !ReferenceEquals(w, owner) && TypeNames(w).Any(types.Contains)).ToList();
                if (stale.Count == 0) continue;
                foreach (Window w in stale) w.Close();
                (ChangeKind firstKind, string firstId) = kind.First();
                OpenRecord(owner, null, new ChangesViewModel.ChangeRow { Kind = firstKind, Id = firstId });
            }
        }

        private static IEnumerable<string> TypeNames(Window w)
        {
            yield return w.GetType().Name;
            if (w.Content != null) yield return w.Content.GetType().Name;
            if (w.DataContext != null) yield return w.DataContext.GetType().Name;
            if ((w.Content as Control)?.DataContext is object inner) yield return inner.GetType().Name;
        }

        /// <summary>Opens the record in its editor, or the JSON when no editor shows it.</summary>
        public static void OpenRecord(Window owner, ChangesViewModel vm, ChangesViewModel.ChangeRow row)
        {
            if (row?.Kind == null) return;
            int.TryParse(row.Id, out int id);
            string folder = row.Kind.Folder;
            Action open = row.Kind.Name switch
            {
                "text" => () => AvaloniaEditorLauncher.OpenTextEditor(id),
                "script" => () => AvaloniaEditorLauncher.OpenScriptEditor(id),
                "trainer" => () => AvaloniaEditorLauncher.OpenTrainerEditor(id),
                "personal" or "learnset" or "evolution" or "egg-moves" => () => AvaloniaEditorLauncher.OpenPokemonEditor(id),
                "move" => () => AvaloniaEditorLauncher.OpenMoveDataEditor(id),
                "item" => () => AvaloniaEditorLauncher.OpenItemEditor(id),
                "trade" => () => AvaloniaEditorLauncher.OpenTradeEditor(id),
                "encounters" => () => AvaloniaEditorLauncher.OpenWildEditor(id),
                "header" => () => AvaloniaEditorLauncher.OpenHeaderEditor(id),
                "map-file" => () => AvaloniaEditorLauncher.OpenMapEditor(id),
                "events" => () => AvaloniaEditorLauncher.OpenEventEditor(id),
                "matrix" => () => AvaloniaEditorLauncher.OpenMatrixEditor(id),
                _ => null,
            };
            if (open != null)
            {
                open();
                return;
            }
            string path = row.Paths?.FirstOrDefault(p => p.EndsWith(".json", StringComparison.Ordinal)) ?? row.Paths?.FirstOrDefault();
            if (path != null && vm?.FolderPath != null) SystemShell.RevealInFileManager(Path.Combine(vm.FolderPath, path));
        }

        /// <summary>Shows the main window's busy card until disposed.</summary>
        private static IDisposable Busy(string text, string hint = "")
        {
            MainWindowViewModel main = (global::Avalonia.Application.Current?.ApplicationLifetime
                as global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow?.DataContext as MainWindowViewModel;
            if (main == null || main.IsBusy) return new BusyScope(null);
            main.BusyText = text;
            main.BusyHint = hint;
            main.IsBusy = true;
            return new BusyScope(main);
        }

        private sealed class BusyScope : IDisposable
        {
            private MainWindowViewModel _main;
            public BusyScope(MainWindowViewModel main) { _main = main; }
            public void Dispose()
            {
                if (_main != null) _main.IsBusy = false;
                _main = null;
            }
        }

        private static async Task RunAsync(ChangesViewModel vm, Func<Task> work)
        {
            try { await work(); }
            catch (Exception ex)
            {
                CrashReporter.LogHandled(ex);
                await DialogHelper.ShowErrorAsync("Track changes failed:\n" + ex.Message, "Track changes");
            }
            finally { await vm.RefreshViewAsync(); }
        }
    }

    /// <summary>Asks for a commit message.</summary>
    public static class CommitDialog
    {
        public static async Task<string> AskAsync(Window owner, int files, string suggested)
        {
            TaskCompletionSource<string> result = new TaskCompletionSource<string>();
            Window win = new Window { Title = "Commit changes", Width = 460, Height = 240, WindowStartupLocation = WindowStartupLocation.CenterOwner };
            TextBox message = new TextBox
            {
                Text = suggested ?? "",
                PlaceholderText = "What changed",
                AcceptsReturn = true,
                TextWrapping = global::Avalonia.Media.TextWrapping.Wrap,
                Margin = new global::Avalonia.Thickness(12, 0, 12, 8),
            };
            TextBlock count = new TextBlock { Text = files == 0 ? "Folder files only" : Plural.Of(files, "record") + " ticked in the changes panel.", Margin = new global::Avalonia.Thickness(12, 12, 12, 6) };
            Button commit = new Button { Content = "Commit", MinWidth = 80, IsEnabled = !string.IsNullOrWhiteSpace(suggested) };
            Button cancel = new Button { Content = "Cancel", MinWidth = 80, IsCancel = true };
            message.TextChanged += (_, _) => commit.IsEnabled = !string.IsNullOrWhiteSpace(message.Text);
            commit.Click += (_, _) => { result.TrySetResult(message.Text?.Trim()); win.Close(); };
            cancel.Click += (_, _) => { result.TrySetResult(null); win.Close(); };
            win.Closed += (_, _) => result.TrySetResult(null);
            StackPanel buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 6, Margin = new global::Avalonia.Thickness(12, 0, 12, 12) };
            buttons.Children.Add(commit);
            buttons.Children.Add(cancel);
            win.Content = DialogSizing.Body(message, buttons, count);
            DialogSizing.Grow(win, message);
            message.AttachedToVisualTree += (_, _) => message.Focus();
            await win.ShowDialog(owner);
            return await result.Task;
        }
    }

    /// <summary>Asks for the name and email git puts on commits, when it has none.</summary>
    public static class IdentityDialog
    {
        public static async Task<bool> AskAsync(Window owner)
        {
            string name = await DialogHelper.PromptTextAsync("git puts a name on each commit. Your name:", "Commit name", "", owner);
            if (string.IsNullOrWhiteSpace(name)) return false;
            string email = await DialogHelper.PromptTextAsync("And an email (GitHub's noreply address works):", "Commit email", "", owner);
            if (string.IsNullOrWhiteSpace(email)) return false;
            string folder = ChangeTracker.Current?.Folder;
            bool everywhere = folder == null || !await DialogHelper.AskTwoWayAsync("Use this name for this changes folder only, or for every git repo on this computer?",
                "Commit name", "This folder only", "Every repo", owner);
            CliRunner.Result r = await GitCli.SetIdentityAsync(name, email, everywhere ? null : folder);
            if (!r.Success) await DialogHelper.ShowErrorAsync(r.Message, "Commit name", owner);
            return r.Success;
        }
    }

    /// <summary>Lists clashes between a changes folder and the ROM it's applied to, and lets the user pick per item.</summary>
    public static class ConflictDialog
    {
        private sealed class Item
        {
            public Conflict Conflict { get; init; }
            public CheckBox Box { get; init; }
        }

        public static async Task<Func<Conflict, bool>> AskAsync(Window owner, IReadOnlyList<Conflict> conflicts)
        {
            TaskCompletionSource<Func<Conflict, bool>> result = new TaskCompletionSource<Func<Conflict, bool>>();
            Window win = new Window { Title = "Changes that clash", Width = 760, Height = 520, WindowStartupLocation = WindowStartupLocation.CenterOwner };
            List<Item> items = new List<Item>();
            StackPanel list = new StackPanel { Spacing = 6, Margin = new global::Avalonia.Thickness(12, 0) };
            foreach (Conflict c in conflicts)
            {
                CheckBox box = new CheckBox { IsChecked = true, Content = $"{c.Record}  {c.Path}" };
                TextBlock detail = new TextBlock
                {
                    Text = $"Was {Show(c.Was)}, changes folder has {Show(c.Now)}, this ROM has {Show(c.Rom)}",
                    FontSize = 11,
                    TextWrapping = global::Avalonia.Media.TextWrapping.Wrap,
                    Margin = new global::Avalonia.Thickness(28, 0, 0, 0),
                };
                list.Children.Add(box);
                list.Children.Add(detail);
                items.Add(new Item { Conflict = c, Box = box });
            }
            TextBlock intro = new TextBlock
            {
                Text = "This ROM has something else where these changes were made. Ticked ones take the incoming value.",
                TextWrapping = global::Avalonia.Media.TextWrapping.Wrap,
                Margin = new global::Avalonia.Thickness(12, 12, 12, 8),
            };
            Button all = new Button { Content = "Tick all" }, none = new Button { Content = "Untick all" };
            all.Click += (_, _) => items.ForEach(i => i.Box.IsChecked = true);
            none.Click += (_, _) => items.ForEach(i => i.Box.IsChecked = false);
            Button apply = new Button { Content = "Apply", MinWidth = 80 }, cancel = new Button { Content = "Cancel", MinWidth = 80, IsCancel = true };
            apply.Click += (_, _) =>
            {
                HashSet<(string, string)> take = new HashSet<(string, string)>(items.Where(i => i.Box.IsChecked == true).Select(i => (i.Conflict.Record, i.Conflict.Path)));
                result.TrySetResult(c => take.Contains((c.Record, c.Path)));
                win.Close();
            };
            cancel.Click += (_, _) => { result.TrySetResult(null); win.Close(); };
            win.Closed += (_, _) => result.TrySetResult(null);
            DockPanel buttons = new DockPanel { Margin = new global::Avalonia.Thickness(12) };
            StackPanel left = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            left.Children.Add(all);
            left.Children.Add(none);
            StackPanel right = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, HorizontalAlignment = HorizontalAlignment.Right };
            right.Children.Add(apply);
            right.Children.Add(cancel);
            DockPanel.SetDock(left, Dock.Left);
            buttons.Children.Add(left);
            buttons.Children.Add(right);
            ScrollViewer scroll = new ScrollViewer { Content = list };
            win.Content = DialogSizing.Body(scroll, buttons, intro);
            DialogSizing.Grow(win, scroll);
            await win.ShowDialog(owner);
            return await result.Task;
        }

        private static string Show(JsonNode node)
        {
            string text = node == null ? "nothing" : node is JsonValue v && v.TryGetValue(out string s) ? s : node.ToJsonString();
            return text.Length > 120 ? text.Substring(0, 120) + "…" : text;
        }
    }
}
