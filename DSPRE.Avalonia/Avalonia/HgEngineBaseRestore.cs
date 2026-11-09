using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using DSPRE.Avalonia.Data;
using DSPRE.HgEngine;

namespace DSPRE.Avalonia
{
    /// <summary>Puts DSPRE's edits back into an hg-engine checkout's base/ after make extracts it again from rom.nds.</summary>
    public static class HgEngineBaseRestore
    {
        /// <summary>Builds once so make extracts base/, then writes DSPRE's edits back. False when the real build shouldn't run.</summary>
        /// <param name="reload">Opens base/ again. False when it didn't load.</param>
        /// <param name="closeEditors">Closes open editors, which would otherwise write the old base/ back over the new one.</param>
        public static async Task<bool> RebuildFirstAsync(Window owner, Func<Task<bool>> build, Func<Task<bool>> reload, Action closeEditors)
        {
            const string title = "Build with hg-engine";
            if (!await DialogHelper.AskTwoWayAsync(
                    "rom.nds or hg-engine's tools changed, so make will extract base/ again.\n\n"
                    + "DSPRE builds once to extract it, puts its edits back, then builds again.",
                    title, "Build", "Cancel", owner, SpeechBubble.Exclamation))
                return false;
            return await DialogHelper.GuardAsync(async () =>
            {
                closeEditors();
                Dictionary<RomInfo.DirNames, string> inSync = await Task.Run(NarcSync.InSyncArchives);
                // A rom.nds that isn't the clean dump may have been swapped on purpose, so its archives are asked about.
                bool cleanRom = await Task.Run(() => HgEngineFolder.HasCleanRom(HgEngineProject.RepoRootWindows));
                bool built = await build();
                // Even a failed build may have extracted base/, and then the unpacked folders hold the newer work.
                bool extracted = await Task.Run(() => HgEngineBuild.TryCheckBaseReextract(out bool again) && !again);
                if (extracted && cleanRom) await Task.Run(() => NarcSync.TrustFolders(inSync));
                if (!built)
                {
                    if (extracted) await reload();
                    return false;
                }
                // The open project still describes the old base/, so it is read again before anything is compared.
                return await reload() && await RestoreAsync(owner, title, reload);
            }, NotWrittenBack, title, owner);
        }

        /// <summary>Offers to write back or discard edits lost when base/ was extracted outside DSPRE. False when cancelled.</summary>
        /// <param name="reload">Opens base/ again. False when it didn't load.</param>
        public static async Task<bool> OfferAsync(Window owner, Func<Task<bool>> reload)
        {
            const string title = "Edits missing from base/";
            return await DialogHelper.GuardAsync(async () =>
            {
                HgEngineBaseEdits.Report missing = await Task.Run(HgEngineBaseEdits.Missing);
                if (!missing.AnyToTell) return true;
                if (!missing.Any)
                {
                    // Only edits hg-engine now patches or files that couldn't be read are left.
                    await ShowProblemsAsync(owner, await Task.Run(HgEngineBaseEdits.Forget), title);
                    return true;
                }
                string what = string.Join("\n", missing.CodeFiles.Concat(missing.Files));
                DialogHelper.MsgResult choice = await DialogHelper.AskThreeWayAsync(
                    "base/ was extracted again from rom.nds, and these files lost edits DSPRE made:\n\n" + what
                    + "\n\nWrite DSPRE's edits back into them?", title, "Write back", "Discard edits", owner: owner);
                if (choice == DialogHelper.MsgResult.Cancel) return false;
                if (choice != DialogHelper.MsgResult.Yes)
                {
                    await Task.Run(HgEngineBaseEdits.Forget);
                    return true;
                }
                return await RestoreAsync(owner, title, reload);
            }, NotWrittenBack, title, owner);
        }

        private const string NotWrittenBack = "DSPRE's edits weren't written back into base/:";

        // Opens base/ again after writing, so the open project shows what was put back.
        private static async Task<bool> RestoreAsync(Window owner, string title, Func<Task<bool>> reload)
        {
            HgEngineBaseEdits.Report restored = await Task.Run(HgEngineBaseEdits.Restore);
            await ShowProblemsAsync(owner, restored, title);
            return !restored.Any || await reload();
        }

        private static async Task ShowProblemsAsync(Window owner, HgEngineBaseEdits.Report report, string title)
        {
            List<string> parts = new List<string>();
            if (report.Skipped.Count > 0)
                parts.Add("hg-engine now patches these addresses, so DSPRE's edits to them weren't written back:\n" + string.Join("\n", report.Skipped));
            if (report.Waiting.Count > 0)
                parts.Add("These files are still compressed. Their edits are offered again after the next build:\n" + string.Join("\n", report.Waiting));
            if (report.Failed.Count > 0)
                parts.Add("These couldn't be written back:\n" + string.Join("\n", report.Failed));
            if (parts.Count > 0) await DialogHelper.ShowWarningAsync(string.Join("\n\n", parts), title, owner);
        }
    }
}
