using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels.Shell;
using DSPRE.HgEngine;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.Views.Shell
{
    /// <summary>Reads archives back out of the ROM over their unpacked folders, then reloads the project.</summary>
    public partial class MainWindowView
    {
        private async void UnpackBuildingArchives_Click(object sender, RoutedEventArgs e)
        {
            List<DirNames> dirs = new List<DirNames>
            {
                DirNames.ExteriorBuildingModels, DirNames.BuildingConfigFiles, DirNames.BuildingTextures, DirNames.AreaData,
            };
            if (GameFamily == GameFamilies.HGSS) dirs.Add(DirNames.InteriorBuildingModels);
            await RunRomOperationAsync(() => UnpackArchivesAsync(dirs,
                "Unpack building archives",
                "Building models, their textures and settings, and area data are read again from the ROM's archives. "
                + "Changes to them since the ROM was last saved are lost."));
        }

        private async void UnpackAllArchives_Click(object sender, RoutedEventArgs e)
        {
            await RunRomOperationAsync(() => UnpackArchivesAsync(Enum.GetValues<DirNames>().ToList(),
                "Unpack all archives",
                "Every archive is read again from the ROM. Changes to any of them since the ROM was last saved are lost. "
                + "This can take a while."));
        }

        private async System.Threading.Tasks.Task UnpackArchivesAsync(List<DirNames> dirs, string title, string what)
        {
            if (!AvaloniaEditorLauncher.IsRomLoaded) return;
            if (HgEngineProject.IsActive)
            {
                await DialogHelper.ShowInfoAsync("Not available for hg-engine projects.", title);
                return;
            }
            if (!await DialogHelper.AskYesNoAsync(what + "\n\nOpen editors close first and the project reloads afterwards.\n\nContinue?", title, this))
                return;
            // Unsaved editor changes are offered for saving first, as when another project is opened.
            if (!await ConfirmProjectCloseAsync(openingAnother: false)) return;
            OpenEditors.CloseEditorWindows(this);

            string folder = WorkDir?.TrimEnd('\\', '/');
            MainWindowViewModel vm = DataContext as MainWindowViewModel;
            if (vm != null)
            {
                vm.BusyText = title + "…";
                vm.BusyHint = "";
                vm.IsBusy = true;
            }
            List<string> failed = new List<string>();
            try
            {
                await BusyOverlay.RunLockedAsync(() =>
                {
                    List<DirNames> cleared = ClearUnpackedFolders(dirs, failed);
                    DSUtils.ForceUnpackNarcs(cleared);
                    return cleared.Count;
                });
            }
            finally { if (vm != null) vm.IsBusy = false; }
            if (failed.Count > 0)
                await DialogHelper.ShowErrorAsync("These folders couldn't be emptied, so they were left as they were:" + Environment.NewLine + Environment.NewLine
                    + string.Join(Environment.NewLine, failed), title);

            // Every editor and cache read the old folders; reloading the project starts them all afresh.
            await LoadRomAsync(err0 => { bool ok = AvaloniaRomLoader.LoadFromFolder(folder, out string er, recordRecent: false); err0(er); return ok; },
                sourcePath: folder);
            if (vm != null) vm.StatusText = title + ": done.";
        }

        // Emptied here, after the user agreed, so the extraction never stops to ask about each folder from a
        // worker thread. A folder whose archive is missing is kept: there would be nothing to read it back from.
        private static List<DirNames> ClearUnpackedFolders(List<DirNames> dirs, List<string> failed)
        {
            List<DirNames> cleared = new List<DirNames>();
            foreach (DirNames dir in dirs)
            {
                if (!GameDirs.TryGetValue(dir, out (string packedDir, string unpackedDir) paths)) continue;
                if (!System.IO.File.Exists(paths.packedDir)) continue;
                try
                {
                    if (System.IO.Directory.Exists(paths.unpackedDir)) System.IO.Directory.Delete(paths.unpackedDir, true);
                    cleared.Add(dir);
                }
                catch (Exception e) when (e is System.IO.IOException || e is UnauthorizedAccessException)
                {
                    failed.Add($"{paths.unpackedDir}: {e.Message}");
                }
            }
            return cleared;
        }
    }
}
