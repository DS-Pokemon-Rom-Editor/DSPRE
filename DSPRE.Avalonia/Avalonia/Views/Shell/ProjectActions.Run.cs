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
        internal async System.Threading.Tasks.Task BuildAndRunCoreAsync()
        {
            if (!AvaloniaEditorLauncher.IsRomLoaded) return;
            // Anything unsaved in an open editor would otherwise be missing from the build.
            if (!await UnsavedChangesDialog.ShowIfNeededAsync(_window, OpenEditors.GetUnsavedEditors(_window))) return;

            (EmulatorKind Kind, string Path)? emulator = Emulators.Preferred() ?? await EmulatorPickerView.AskAsync(_window);
            if (emulator == null) return;

            string rom;
            if (RomInfo.IsHgEngineBaseProject)
            {
                string name = HgEngineProject.BuildRomName;
                if (name == null)
                {
                    name = await DialogHelper.PromptTextAsync(
                        "File name for the ROM that Build and run makes. Emulators keep saves by ROM name, so keep it the same between runs.",
                        "Build and run", "test.nds", _window);
                    if (name == null) return;
                    if (!HgEngineProject.IsSafeBuildRomName(name))
                    {
                        await DialogHelper.ShowErrorAsync("Use a plain file name ending in .nds.\n\n"
                            + "It takes letters, numbers, spaces, dots, dashes and underscores.", "Build and run", _window);
                        return;
                    }
                    HgEngineProject.SetBuildRomName(name);
                }
                if (!await BuildHgEngineFolderAsync(name)) return;
                rom = System.IO.Path.Combine(HgEngineProject.RepoRootWindows, name);
            }
            else
            {
                rom = ProjectFiles.BuildAndRunRomPath();
                if (!await BuildRomAsync(rom)) return;
            }

            if (!await OfferTestSaveAsync(emulator.Value.Kind, emulator.Value.Path, rom)) return;

            string error = Emulators.Launch(emulator.Value.Kind, emulator.Value.Path, rom);
            if (error != null) { await DialogHelper.ShowErrorAsync(error, "Build and run", _window); return; }
            if (_window.DataContext is MainWindowViewModel vm) vm.StatusText = $"Running {System.IO.Path.GetFileName(rom)} in {Emulators.DisplayName(emulator.Value.Kind)}.";
        }

        // Asks whether to start from the project's chosen save; false when the run is cancelled.
        private async System.Threading.Tasks.Task<bool> OfferTestSaveAsync(EmulatorKind kind, string emulatorPath, string rom)
        {
            string save = TestSaves.For(RomInfo.WorkDir);
            if (string.IsNullOrEmpty(save)) return true;
            if (!TestSaves.Supports(kind))
            {
                if (_window.DataContext is MainWindowViewModel note)
                    note.StatusText = $"Save not copied: {Emulators.DisplayName(kind)} keeps its own saves.";
                return true;
            }
            DialogHelper.MsgResult answer = await DialogHelper.AskThreeWayAsync(
                $"Start from {System.IO.Path.GetFileName(save)}?\n\nCarry on keeps the save the emulator already has for this ROM.",
                "Build and run", "Start from it", "Carry on");
            if (answer == DialogHelper.MsgResult.Cancel) return false;
            if (answer != DialogHelper.MsgResult.Yes) return true;
            try
            {
                TestSaves.Place(kind, emulatorPath, rom, save);
                return true;
            }
            catch (System.Exception ex)
            {
                await DialogHelper.ShowErrorAsync("The save couldn't be put in place. " + ex.Message, "Build and run", _window);
                return false;
            }
        }
    }
}
