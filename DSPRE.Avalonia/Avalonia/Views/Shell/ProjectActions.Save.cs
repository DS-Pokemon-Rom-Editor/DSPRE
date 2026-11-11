using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using DSPRE.Avalonia.ViewModels;
using DSPRE.Editors;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.Views.Shell
{
    internal sealed partial class ProjectActions
    {
        internal async System.Threading.Tasks.Task SaveRomCoreAsync()
        {
            if (!AvaloniaEditorLauncher.IsRomLoaded) return;
            // Anything unsaved in an open editor would otherwise be missing from the build.
            if (!await UnsavedChangesDialog.ShowIfNeededAsync(_window, OpenEditors.GetUnsavedEditors(_window))) return;
            if (RomInfo.IsHgEngineBaseProject)
            {
                if (await BuildHgEngineFolderAsync(HgEngineProject.BuildRomName)) await OfferPatchCreditsAsync();
                return;
            }
            IStorageFile file = await _window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save ROM",
                DefaultExtension = "nds",
                SuggestedFileName = (RomInfo.ProjectName ?? "rom") + ".nds",
                FileTypeChoices = new[] { new FilePickerFileType("NDS ROM") { Patterns = new[] { "*.nds" } } }
            });
            string path = file?.TryGetLocalPath();
            if (string.IsNullOrEmpty(path)) return;
            if (await BuildRomAsync(path)) await OfferPatchCreditsAsync();
        }

        private async System.Threading.Tasks.Task OfferPatchCreditsAsync()
        {
            if (!PatchToolboxLogic.CreditsOfferDue()) return;
            List<string> keys;
            try { keys = await System.Threading.Tasks.Task.Run(() => PatchToolboxLogic.AppliedCreditKeys(PatchToolboxLogic.GetPatchStatuses())); }
            catch (System.Exception ex) { AppLogger.Warn("Patch credits check failed: " + ex.Message); return; }
            if (keys.Count == 0) return;

            (bool generate, bool stopAsking) = await DialogHelper.AskWithCheckAsync(
                "This ROM's patches have no credits yet. Generate template credits?\n\n" +
                "The ROM patch toolbox can also generate them later.",
                "Patch credits", "Generate credits", "Not now", "Don't ask again", isChecked: true, owner: _window);
            if (generate || stopAsking) PatchToolboxLogic.MarkCreditsHandled();
            if (generate) await DialogHelper.ShowCopyableTextAsync(PatchToolboxLogic.CreditsText(keys), "Credits", _window);
        }

        public async System.Threading.Tasks.Task<bool> BuildRomAsync(string path)
        {
            await OfferToClearArm9CompressionMarkAsync();
            string error = null;
            bool ok = await WriteProjectFilesAsync("Saving ROM…", "Repacking the project into a playable .nds file.",
                () => ProjectFiles.PackRom(path), e => error = e);
            MainWindowViewModel vm = _window.DataContext as MainWindowViewModel;
            if (ok)
            {
                if (vm != null) vm.StatusText = "ROM built: " + path;
                AppLogger.Info("ROM built successfully: " + path);
                try { await System.Threading.Tasks.Task.Run(() => DSPRE.Tracking.TrackingSetup.RecordBuild(path)); }
                catch (System.Exception ex) when (ex is System.IO.IOException || ex is System.UnauthorizedAccessException) { AppLogger.Warn("Track changes: the build couldn't be recorded: " + ex.Message); }
                return true;
            }
            if (vm != null) vm.StatusText = "ROM build failed.";
            if (error != null) await DialogHelper.ShowErrorAsync(error, "Save ROM", _window);
            return false;
        }

        // Builds with make after writing into base/, so the ROM is what hg-engine makes of them.
        private async System.Threading.Tasks.Task<bool> BuildHgEngineFolderAsync(string buildRom)
        {
            string error = null;
            string checkout = HgEngineProject.RepoRootWindows;
            if (!await EnsureBuildableAsync(checkout)) return false;
            // Asked before any write: one to base/arm9.bin would hide a newer rom.nds from make.
            bool reextracts = await System.Threading.Tasks.Task.Run(() => HgEngineBuild.TryCheckBaseReextract(out bool again) && again);
            if (reextracts)
            {
                if (!await RebuildBaseFirstAsync(buildRom)) return false;
            }
            else if (!await OfferBaseEditsBackAsync()) return false;
            bool ok = await WriteProjectFilesAsync("Building with hg-engine…", "Writing the project's files into base/ for make.",
                () => { HgEngineBuildCache.Invalidate(checkout); return true; }, e => error = e);
            if (!ok)
            {
                if (error != null) await DialogHelper.ShowErrorAsync(error, "Build", _window);
                return false;
            }
            Dictionary<RomInfo.DirNames, string> before = await System.Threading.Tasks.Task.Run(HgEngineFolder.SnapshotUnpackedArchives);
            bool built = await new CompileRomView().BuildAsync(_window, buildRom);
            // What make rebuilt replaces DSPRE's copies, so editors and the next save start from it.
            int refreshed = built ? (await System.Threading.Tasks.Task.Run(() => HgEngineFolder.RefreshRebuiltArchives(before))).Count : 0;
            if (built) await RotomTool.RefreshHgEngineCommonScriptsAsync();
            if (_window.DataContext is MainWindowViewModel vm)
                vm.StatusText = built ? "Built " + (buildRom ?? "test.nds") + " with make." + (refreshed > 0 ? $" {refreshed} archives reloaded from the build." : "")
                    : "The hg-engine build failed.";
            return built;
        }

        private System.Threading.Tasks.Task<bool> RebuildBaseFirstAsync(string buildRom) =>
            HgEngineBaseRestore.RebuildFirstAsync(_window, () => new CompileRomView().BuildAsync(_window, buildRom), ReloadHgEngineBaseAsync,
                () => OpenEditors.CloseEditorWindows(_window));

        private System.Threading.Tasks.Task<bool> OfferBaseEditsBackAsync() => HgEngineBaseRestore.OfferAsync(_window, ReloadHgEngineBaseAsync);

        // Reports a script compile failure itself and hands any other to fail.
        private async System.Threading.Tasks.Task<bool> WriteProjectFilesAsync(string busyText, string busyHint,
            System.Func<bool> finish, System.Action<string> fail)
        {
            MainWindowViewModel vm = _window.DataContext as MainWindowViewModel;
            if (vm != null)
            {
                vm.BusyText = busyText;
                vm.BusyHint = busyHint;
                vm.IsBusy = true;
            }
            string error = null;
            bool ok;
            try
            {
                if (RomInfo.HasRotomProject && RotomTool.IsAvailable)
                {
                    if (vm != null) vm.BusyHint = "Compiling changed scripts.";
                    RotomTool.Result compiled = await RotomTool.CompileProjectAsync();
                    if (!compiled.Success)
                    {
                        if (vm != null) { vm.IsBusy = false; vm.StatusText = "ROM build stopped: scripts didn't compile."; }
                        await DialogHelper.ShowErrorAsync("These scripts didn't compile, so no ROM was written:\n\n"
                                                     + string.Join("\n", RotomTool.FailureLines(compiled))
                                                     + "\n\nFix them and save again.",
                                                     "Save ROM", RotomTool.FormatDetails(compiled));
                        return false;
                    }
                    if (vm != null) vm.BusyHint = busyHint;
                }

                // Folders are packed over their archives below, so outside changes are settled first.
                NarcSync.Outcome archives = await BusyOverlay.RunLockedAsync(() => NarcSync.Settle(saving: true));
                string doing = busyText.TrimEnd('…');
                if (!archives.Settled)
                {
                    if (vm != null) { vm.IsBusy = false; vm.StatusText = doing + " cancelled. Nothing was written."; }
                    return false;
                }
                if (archives.Unpacked.Count > 0)
                {
                    if (vm != null) vm.IsBusy = false;
                    bool saveNow = await DialogHelper.AskTwoWayAsync(
                        "The newer archive was unpacked over its folder for:\n\n" + string.Join("\n", archives.Unpacked)
                        + "\n\nYou can look it over in its editors first, or carry on now.",
                        doing, "Carry on", "Review first");
                    if (!saveNow)
                    {
                        if (vm != null) vm.StatusText = doing + " paused so the unpacked archives can be reviewed. Nothing was written.";
                        return false;
                    }
                    if (vm != null) vm.IsBusy = true;
                }

                ok = await BusyOverlay.RunLockedAsync(() =>
                {
                    try
                    {
                        if (!ProjectFiles.WriteBack(out error)) return false;
                        return finish();
                    }
                    catch (System.Exception ex) { error = ex.Message; return false; }
                });
            }
            finally
            {
                if (vm != null) vm.IsBusy = false;
            }
            if (!ok) fail(error ?? "Building the ROM failed. See the log for details.");
            return ok;
        }

        // A legacy arm9.bin decompressed in place keeps its mark, which stops the ROM on hardware.
        private async System.Threading.Tasks.Task OfferToClearArm9CompressionMarkAsync()
        {
            try
            {
                if (RomInfo.IsDsRomProject || !ARM9.IsFlatButMarked()) return;
                if (await DialogHelper.AskYesNoAsync(
                        "This ROM's ARM9 is decompressed but still marked compressed.\n\n" +
                        "It won't run on a real DS like that. Fix the mark?", "ARM9 compression mark", _window))
                    ARM9.ClearCompressionMark();
            }
            catch (System.Exception ex) when (ex is System.IO.IOException || ex is System.UnauthorizedAccessException)
            {
                AppLogger.Warn("ARM9 compression mark check failed: " + ex.Message);
            }
        }

        internal async System.Threading.Tasks.Task ConvertDsRomAsync()
        {
            if (!AvaloniaEditorLauncher.IsRomLoaded) return;
            if (RomInfo.IsDsRomProject)
            {
                await DialogHelper.ShowInfoAsync("This project is already in ds-rom format.", "Convert to ds-rom");
                return;
            }
            // Every path an open editor holds changes, so editors close first and the project reopens.
            if (!await UnsavedChangesDialog.ShowIfNeededAsync(_window, OpenEditors.GetUnsavedEditors(_window))) return;
            OpenEditors.CloseEditorWindows(_window);
            RotomLanguageServerClient.StopAll();

            string folder = RomInfo.WorkDir.TrimEnd('\\', '/');
            MainWindowViewModel vm = _window.DataContext as MainWindowViewModel;
            if (vm != null)
            {
                vm.BusyText = "Converting to ds-rom…";
                vm.BusyHint = "Backs up the project first";
                vm.IsBusy = true;
            }
            int result;
            try { result = await BusyOverlay.RunLockedAsync(() => ProjectFiles.ConvertToDsRom(folder)); }
            finally { if (vm != null) vm.IsBusy = false; }

            if (result != 1) return;
            await LoadFolderAsync(folder, recordRecent: false);
        }

        internal async System.Threading.Tasks.Task CompileRomCoreAsync()
        {
            if (!HgEngineProject.IsActive) return;
            if (!await UnsavedChangesDialog.ShowIfNeededAsync(_window, OpenEditors.GetUnsavedEditors(_window))) return;
            if (RomInfo.IsHgEngineBaseProject) { await BuildHgEngineFolderAsync(null); return; }
            await new CompileRomView().ShowAndRunAsync(_window);
        }
    }
}
