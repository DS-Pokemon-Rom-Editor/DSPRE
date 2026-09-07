using System.Threading.Tasks;
using Avalonia.Controls;
using DSPRE.Editors;

namespace DSPRE.Avalonia
{
    /// <summary>
    /// Shared guard for leaving a record that has unsaved changes, used when the user picks a
    /// different header, map, trainer, script or archive inside an editor rather than closing it.
    ///
    /// Closing an editor is already guarded once for everybody by <see cref="EditorWindowChrome"/>.
    /// Switching records is the other way edits get lost, and it was handled in only a handful of
    /// editors, each with its own prompt. This is that prompt in one place so the wording, the button
    /// order and the save-failure behaviour are the same everywhere.
    ///
    /// It offers Save as well as Discard. A guard that can only discard trains people to save first
    /// and then switch, which is the habit the prompt exists to make unnecessary.
    /// </summary>
    public static class RecordSwitchGuard
    {
        /// <summary>
        /// Returns true when the caller may leave the current record, having saved or discarded it.
        /// Returns false when the user cancelled or the save failed, in which case the caller must
        /// leave the current record loaded and put the selection control back where it was.
        /// </summary>
        /// <param name="what">What is being switched, for the prompt: "header", "trainer", "script".</param>
        public static async Task<bool> ConfirmLeaveAsync(
            IEditorWithUnsavedChanges editor,
            Window owner = null,
            string what = "record")
        {
            if (editor == null || !editor.HasUnsavedChanges) return true;

            string subject = string.IsNullOrWhiteSpace(editor.UnsavedChangesDescription)
                ? "This " + what
                : editor.UnsavedChangesDescription;

            var choice = await DialogHelper.AskThreeWay(
                $"{subject} has unsaved changes.\n\nSave them before switching to another {what}?",
                "Unsaved Changes", "Save", "Discard");

            if (choice == DialogHelper.MsgResult.Cancel) return false;

            if (choice == DialogHelper.MsgResult.No)
            {
                editor.DiscardChanges();
                return true;
            }

            string failure = await UnsavedChangesDialog.TrySaveEditorAsync(editor);
            if (failure == null) return true;

            // A failed save must not be treated as permission to move on, or the edit is lost
            // precisely when the user asked for it to be kept.
            await DialogHelper.ShowError(
                $"Could not save {subject}:\n{failure}\n\nStaying on the current {what}.",
                "Save Error", owner);
            return false;
        }
    }
}
