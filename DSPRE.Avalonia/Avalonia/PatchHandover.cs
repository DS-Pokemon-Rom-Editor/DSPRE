using System.Threading.Tasks;

namespace DSPRE.Avalonia
{
    /// <summary>
    /// Sends an editor that needs a code patch to that patch in the ROM patch toolbox. Editors never install a patch
    /// themselves: the toolbox entry says what it changes, what it backs up and that it can't be removed, and asks
    /// first. Editors listen for the patch state changing and pick the patch up once it is applied.
    /// </summary>
    internal static class PatchHandover
    {
        /// <param name="key">The patch's toolbox key.</param>
        /// <param name="patchTitle">The patch's title as the toolbox shows it.</param>
        /// <param name="need">What the editor was asked to do, as the start of a sentence.</param>
        /// <param name="title">The editor's title, for the question's window.</param>
        public static async Task OfferAsync(string key, string need, string title)
        {
            string patchTitle = PatchToolboxLogic.PatchTitles.TryGetValue(key, out string named) ? named : key;
            bool open = await DialogHelper.AskTwoWayAsync(
                $"{need} needs the \"{patchTitle}\" patch. It changes the game's code, so it is applied from the ROM patch toolbox, "
                + "where its notes say what it changes and what it backs up. Come back here once it is applied.",
                title, "Open in ROM patch toolbox", "Cancel");
            if (open) AvaloniaEditorLauncher.OpenPatchToolboxAt(key);
        }
    }
}
