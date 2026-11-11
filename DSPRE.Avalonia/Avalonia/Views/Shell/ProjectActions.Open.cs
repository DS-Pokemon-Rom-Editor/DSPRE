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
        private readonly MainWindowView _window;

        public ProjectActions(MainWindowView window) => _window = window;

        private async System.Threading.Tasks.Task<bool> RefuseUnsupportedAsync(string path)
        {
            if (ProjectWindows.OpenElsewhere(path))
            {
                await DialogHelper.ShowInfoAsync("This project is already open in another DSPRE window.", "Open ROM");
                return true;
            }
            string why = AvaloniaRomLoader.WhyUnsupported(path);
            if (why == null) return false;
            await DialogHelper.ShowErrorAsync(why, "Open ROM", _window);
            return true;
        }

            // Picked and checked first, so a refused open never costs the open project its edits.
        internal async System.Threading.Tasks.Task<string> PickRomAsync()
        {
            IReadOnlyList<IStorageFile> files = await _window.StorageProvider.OpenFilePickerAsync(new global::Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = "Open ROM",
                AllowMultiple = false,
                FileTypeFilter = new[] { new global::Avalonia.Platform.Storage.FilePickerFileType("NDS ROM") { Patterns = new[] { "*.nds" } } }
            });
            return files != null && files.Count > 0 ? files[0].TryGetLocalPath() : null;
        }

        internal async System.Threading.Tasks.Task<string> PickFolderAsync()
        {
            IReadOnlyList<IStorageFolder> folders = await _window.StorageProvider.OpenFolderPickerAsync(new global::Avalonia.Platform.Storage.FolderPickerOpenOptions
            {
                Title = "Open extracted ROM folder", AllowMultiple = false
            });
            return folders != null && folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
        }

        internal async System.Threading.Tasks.Task OpenRomCoreAsync()
        {
            string path = await PickRomAsync();
            if (string.IsNullOrEmpty(path) || await RefuseUnsupportedAsync(path)) return;
            if (!await ConfirmWslProjectAsync(path)) return;

            bool? reExtract = await CheckExtractedDataChoiceAsync(path);
            if (reExtract == null) return;   // user aborted
            if (!await _window.ConfirmProjectCloseAsync()) return;
            OpenEditors.CloseEditorWindows(_window);
            await LoadRomAsync(err0 => { bool ok = AvaloniaRomLoader.LoadFromFile(path, out string er, reExtract.Value); err0(er); return ok; }, sourcePath: path);
        }

        // False reuses the folder extracted before, true extracts again, null cancels.
        private async System.Threading.Tasks.Task<bool?> CheckExtractedDataChoiceAsync(string ndsPath)
        {
            int folderType = AvaloniaRomLoader.PeekFolderType(ndsPath);
            if (folderType == -1) return false;   // nothing extracted yet, nothing to ask

            string message = folderType == 0
                ? "This ROM was extracted before. Use that folder?"
                : "This ROM was extracted before, by a DSPRE older than 1.15.0. Use that folder?";
            message += "\n\nExtracting again replaces it, and any edits in it are lost.";

            DialogHelper.MsgResult choice = await DialogHelper.AskThreeWayAsync(message, "Extracted before",
                "Use it", "Extract again");
            if (choice == DialogHelper.MsgResult.Cancel) return null;
            if (choice == DialogHelper.MsgResult.Yes) return false;

            bool confirmReExtract = await DialogHelper.AskYesNoAsync(
                "Extract the ROM again? Edits in the extracted folder are lost.", "Extract again?");
            return confirmReExtract ? (bool?)true : null;
        }

        internal async System.Threading.Tasks.Task OpenFolderCoreAsync()
        {
            string path = await PickFolderAsync();
            if (string.IsNullOrEmpty(path)) return;

            if (HgEngineProject.CheckoutFor(path) is string checkout)
            {
                await OpenHgEngineFolderCoreAsync(checkout);
                return;
            }

            if (await RefuseUnsupportedAsync(path)) return;
            if (!await ConfirmWslProjectAsync(path)) return;
            if (!await _window.ConfirmProjectCloseAsync()) return;
            OpenEditors.CloseEditorWindows(_window);
            await LoadFolderAsync(path, recordRecent: true);
        }

        internal async System.Threading.Tasks.Task<string> PickHgEngineFolderAsync()
        {
            IReadOnlyList<IStorageFolder> folders = await _window.StorageProvider.OpenFolderPickerAsync(new global::Avalonia.Platform.Storage.FolderPickerOpenOptions
            {
                Title = "Open hg-engine folder", AllowMultiple = false
            });
            string path = folders != null && folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
            if (string.IsNullOrEmpty(path)) return null;
            if (HgEngineProject.LooksLikeCheckout(path)) return path;
            await DialogHelper.ShowErrorAsync("That folder isn't an hg-engine checkout: no Makefile, data/ and armips/.", "Open hg-engine folder", _window);
            return null;
        }

        // askToClose is false when this folder was just chosen over the open project.
        internal async System.Threading.Tasks.Task OpenHgEngineFolderCoreAsync(string checkout, bool askToClose = true)
        {
            checkout = checkout.TrimEnd('\\', '/');
            const string title = "Open hg-engine folder";
            if (!HgEngineFolder.IsDsRomCheckout(checkout))
            {
                await DialogHelper.ShowErrorAsync("This checkout is too old for hg-engine's ds-rom build. Update it first.", title, _window);
                return;
            }
            if (!await ConfirmWslProjectAsync(checkout, hgEngine: true)) return;
            HgEngineShell? stored = HgEngineProject.StoredShellFor(checkout);

            HgEngineShell? shell = stored ?? await ViewModels.Tools.HgEngineLinkViewModel.AskShellAsync(checkout);
            if (shell == null) return;
            HgEngineProject.OpenFolder(checkout, shell.Value);
            try
            {
                if (!HgEngineFolder.HasBase(checkout))
                {
                    if (!await EnsureBuildableAsync(checkout)) return;
                    if (!await DialogHelper.AskYesNoAsync("This hg-engine folder isn't built yet. Build it now?", title)) return;
                    if (!await new CompileRomView().BuildAsync(_window)) return;
                    if (!HgEngineFolder.HasBase(checkout))
                    {
                        await DialogHelper.ShowErrorAsync("The build finished but left no base/ folder to open.", title, _window);
                        return;
                    }
                }

                bool close = askToClose
                    ? await _window.ConfirmProjectCloseAsync()
                    : await UnsavedChangesDialog.ShowIfNeededAsync(_window, OpenEditors.GetUnsavedEditors(_window));
                if (!close) return;
                OpenEditors.CloseEditorWindows(_window);
                await LoadHgEngineBaseAsync(checkout, recordRecent: true);
            }
            // Every way out, including a cancel, leaves the link matching the project that is open.
            finally { HgEngineProject.Refresh(); }
            if (RomInfo.IsHgEngineBaseProject && HgEngineProject.IsActive) await OfferBaseEditsBackAsync();
        }

        // Run just before make, so a folder built elsewhere still opens.
        private async System.Threading.Tasks.Task<bool> EnsureBuildableAsync(string checkout)
        {
            string why = HgEngineFolder.WhyNotBuildable(checkout, out bool needsRom);
            if (needsRom)
            {
                if (!await ProvideRomAsync(checkout)) return false;
                why = HgEngineFolder.WrongRom(checkout);
            }
            if (why == null) return true;
            await DialogHelper.ShowErrorAsync(why, "Build with hg-engine", _window);
            return false;
        }

        private static async System.Threading.Tasks.Task<bool> ConfirmWslProjectAsync(string path, bool hgEngine = false)
        {
            if (!System.OperatingSystem.IsWindows() || !WslPaths.IsWslPath(path)) return true;
            string key = path.TrimEnd('\\', '/');
            List<string> shown = SettingsManager.Settings.WslNoticeShown;
            if (shown.Exists(p => string.Equals(p, key, System.StringComparison.OrdinalIgnoreCase))) return true;

            string message = hgEngine
                ? "Opening a project inside WSL from Windows is slow. hg-engine already builds inside WSL, and the Linux version of DSPRE runs there at full speed. "
                : "Opening a project inside WSL from Windows is slow. The Linux version of DSPRE runs inside WSL at full speed. ";
            if (!await DialogHelper.AskYesNoAsync(message + WslLinuxAdvice, "This project is inside WSL")) return false;
            shown.Add(key);
            SettingsManager.Save();
            return true;
        }

        private const string WslLinuxAdvice =
            "In a WSL terminal:\n\n"
            + "mkdir -p ~/dspre && cd ~/dspre\n"
            + "curl -LO https://github.com/DS-Pokemon-Rom-Editor/DSPRE/releases/download/canary-avalonia/DSPRE-Avalonia-linux-x64-canary.tar.gz\n"
            + "tar -xzf DSPRE-Avalonia-linux-x64-canary.tar.gz\n"
            + "chmod +x DSPRE.Avalonia\n"
            + "./DSPRE.Avalonia\n\n"
            + "Continue here on Windows anyway?";

        private async System.Threading.Tasks.Task<bool> ProvideRomAsync(string checkout)
        {
            if (!await DialogHelper.AskYesNoAsync("No rom.nds in this folder. Copy in a HeartGold (USA) ROM?", "Open hg-engine folder"))
                return false;
            IReadOnlyList<IStorageFile> files = await _window.StorageProvider.OpenFilePickerAsync(new global::Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = "Choose a HeartGold (USA) ROM", AllowMultiple = false,
                FileTypeFilter = new[] { new global::Avalonia.Platform.Storage.FilePickerFileType("Nintendo DS ROM") { Patterns = new[] { "*.nds" } } },
            });
            string rom = files != null && files.Count > 0 ? files[0].TryGetLocalPath() : null;
            if (string.IsNullOrEmpty(rom)) return false;
            string error = HgEngineFolder.ProvideRom(checkout, rom);
            if (error != null)
            {
                await DialogHelper.ShowErrorAsync(error, "Open hg-engine folder", _window);
                return false;
            }
            if (!HgEngineFolder.HasCleanRom(checkout))
                await DialogHelper.ShowInfoAsync("Not a clean HeartGold (USA) dump. hg-engine builds on it anyway.", "Open hg-engine folder");
            return true;
        }

        internal async System.Threading.Tasks.Task OpenRecentCoreAsync(string path)
        {
            if ((System.IO.File.Exists(path) || System.IO.Directory.Exists(path)) && await RefuseUnsupportedAsync(path)) return;

            if (System.IO.File.Exists(path))
            {
                if (!await ConfirmWslProjectAsync(path)) return;
                bool? reExtract = await CheckExtractedDataChoiceAsync(path);
                if (reExtract == null) return;   // user aborted
                if (!await _window.ConfirmProjectCloseAsync()) return;
                OpenEditors.CloseEditorWindows(_window);
                await LoadRomAsync(err0 => { bool ok = AvaloniaRomLoader.LoadFromFile(path, out string er, reExtract.Value); err0(er); return ok; }, sourcePath: path);
            }
            else if (System.IO.Directory.Exists(path) && HgEngineProject.CheckoutFor(path) is string checkout)
            {
                await OpenHgEngineFolderCoreAsync(checkout);
            }
            else if (System.IO.Directory.Exists(path))
            {
                if (!await ConfirmWslProjectAsync(path)) return;
                if (!await _window.ConfirmProjectCloseAsync()) return;
                OpenEditors.CloseEditorWindows(_window);
                await LoadFolderAsync(path, recordRecent: true);
            }
            else
            {
                SettingsManager.RemoveRecentProject(path);
                if (string.Equals(SettingsManager.Settings.OpenDefaultRom, path, System.StringComparison.OrdinalIgnoreCase))
                {
                    SettingsManager.Settings.OpenDefaultRom = "";
                    SettingsManager.Save();
                }
                (_window.DataContext as MainWindowViewModel)?.RefreshRecents();
                await DialogHelper.ShowErrorAsync("This project no longer exists and was removed from the recent list:\n" + path, "Open recent");
            }
        }

        // An archive replaced while DSPRE was closed would lose to its unpacked folder at save.
        private static async System.Threading.Tasks.Task SettleChangedArchivesAsync(MainWindowViewModel vm)
        {
            if (vm != null)
            {
                vm.BusyText = "Checking unpacked archives…";
                vm.BusyHint = "";
                vm.IsBusy = true;
            }
            try { await BusyOverlay.RunLockedAsync(() => NarcSync.Settle()); }
            catch (System.Exception ex) { AppLogger.Error("Checking unpacked archives failed: " + ex.Message); }
            finally { if (vm != null) vm.IsBusy = false; }
        }

        // sourcePath is only used to spot a WSL path for the busy hint.
        private async System.Threading.Tasks.Task LoadRomAsync(System.Func<System.Action<string>, bool> load, string sourcePath = null)
        {
            MainWindowViewModel vm = _window.DataContext as MainWindowViewModel;
            string openBefore = RomInfo.WorkDir, claimedBefore = ProjectWindows.HeldPath;
            if (!ProjectWindows.TryClaim(sourcePath))
            {
                await DialogHelper.ShowInfoAsync("This project is already open in another DSPRE window.", "Open ROM");
                return;
            }
            if (vm != null)
            {
                vm.BusyText = "Opening ROM…";
                vm.BusyHint = WslPaths.IsWslPath(sourcePath)
                    ? "Projects in WSL load slower"
                    : "";
                vm.IsBusy = true;
            }
            ProjectSession.Starting();
            string error = null;
            bool ok;
            try
            {
                ok = await BusyOverlay.RunLockedAsync(() => load(e => error = e));
            }
            finally
            {
                if (vm != null) vm.IsBusy = false;
            }
            vm?.RefreshRomState();
            if (!ok)
            {
                ProjectWindows.Release();
                // A load that fails early leaves the previous project open, so it keeps its claim.
                if (claimedBefore != null && openBefore != null && RomInfo.WorkDir == openBefore) ProjectWindows.TryClaim(claimedBefore);
                if (vm != null) vm.StatusText = "ROM load failed.";
                await DialogHelper.ShowErrorAsync(error ?? "Couldn't load the ROM.", "Open ROM");
                if (vm != null) await vm.Changes.OnProjectLoadedAsync();
                return;
            }
            if (vm != null) vm.StatusText = $"Loaded {RomInfo.ProjectName ?? "project"}";
            ProjectSession.Loaded();
            await SettleChangedArchivesAsync(vm);

            if (global::Avalonia.Application.Current?.ApplicationLifetime
                is global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                foreach (WelcomeView w in System.Linq.Enumerable.ToList(
                             System.Linq.Enumerable.OfType<WelcomeView>(desktop.Windows)))
                    w.Close();
            }

            _window.RefreshGameIcon();

            vm?.RefreshHgEngineState();

            string offerFolder = RomInfo.IsHGE ? await HandleHgEngineDetectedAsync() : null;
            if (offerFolder == "") offerFolder = await PickHgEngineFolderAsync();
            // Switching now keeps this ROM's setup prompts from showing for a project being left.
            if (!string.IsNullOrEmpty(offerFolder))
            {
                await OpenHgEngineFolderCoreAsync(offerFolder, askToClose: false);
                return;
            }
            await _window.Maps.EnsureSetupAsync();
            if (vm != null) await vm.Changes.OnProjectLoadedAsync();
            if (SettingsManager.Settings?.GuidedTourShown == false)
            {
                SettingsManager.Settings.GuidedTourShown = true;
                SettingsManager.Save();
                if (await DialogHelper.AskTwoWayAsync(
                    "A short tour shows the main window and the editor menus. Tools > Guided tour shows it again later.",
                    "New to DSPRE?", "Show me around", "Not now"))
                    GuidedTour.Start(_window);
            }
        }

        // Leftover members in a/0/2/8 shift every hg-engine table without crashing.
        private async System.Threading.Tasks.Task WarnIfCodeTablesShiftedAsync()
        {
            try
            {
                HgEngineCodeAddons.Layout layout = await System.Threading.Tasks.Task.Run(() => HgEngineCodeAddons.Describe());
                if (layout == null || layout.IsHealthy || layout.TableBlockStart < 0) return;

                // The fix is a beta editor, so a real fault switches it on.
                BetaEditors.AllowBecauseNeeded("HgeRomReviewView");
                (_window.DataContext as MainWindowViewModel)?.RefreshHgEngineState();

                await DialogHelper.ShowInfoAsync(
                    layout.Summary + "\n\nFile > hg-engine > hg-engine ROM review shows which member holds what, and "
                    + "can repair the order. It has been switched on for this ROM because it is needed.",
                    "hg-engine tables are out of place");
            }
            catch (System.Exception ex) { AppLogger.Error("WarnIfCodeTablesShiftedAsync: " + ex.Message); }
        }

        // Returns the checkout to open instead, "" to pick one, or null to stay on the ROM.
        private async System.Threading.Tasks.Task<string> HandleHgEngineDetectedAsync()
        {
            await WarnIfCodeTablesShiftedAsync();
            if (RomInfo.IsHgEngineBaseProject) return null;

            string beside = HgEngineProject.CheckoutBesideProject();
            if (beside != null && HgEngineFolder.IsDsRomCheckout(beside))
            {
                bool openBeside = await DialogHelper.AskYesNoAsync(
                    $"This ROM was built in the hg-engine folder {beside}.\n\n"
                    + "As a ROM, editors for data hg-engine builds stay disabled. Open the folder instead?",
                    "hg-engine ROM");
                return openBeside ? beside : null;
            }
            bool pick = await DialogHelper.AskYesNoAsync(
                "This is an hg-engine ROM.\n\n"
                + "As a ROM, editors for data hg-engine builds stay disabled, and edits aren't in its source. "
                + "Open its hg-engine folder instead?",
                "hg-engine ROM");
            return pick ? "" : null;
        }

        private async System.Threading.Tasks.Task<bool> ReloadHgEngineBaseAsync()
        {
            OpenEditors.CloseEditorWindows(_window);
            try { return await LoadHgEngineBaseAsync(HgEngineProject.RepoRootWindows, recordRecent: false); }
            finally { HgEngineProject.Refresh(); }
        }

        private async System.Threading.Tasks.Task<bool> LoadHgEngineBaseAsync(string checkout, bool recordRecent)
        {
            bool loaded = await LoadFolderAsync(HgEngineFolder.BaseDir(checkout), recordRecent: false, sourcePath: checkout);
            // Records the checkout, not base/, so reopening runs these checks again.
            if (loaded && recordRecent) SettingsManager.RecordRecentProject(checkout);
            return loaded;
        }

        internal async System.Threading.Tasks.Task<bool> LoadFolderAsync(string folder, bool recordRecent, string sourcePath = null)
        {
            bool loaded = false;
            await LoadRomAsync(err0 =>
            {
                loaded = AvaloniaRomLoader.LoadFromFolder(folder, out string er, recordRecent);
                err0(er);
                return loaded;
            }, sourcePath: sourcePath ?? folder);
            return loaded;
        }
    }
}
