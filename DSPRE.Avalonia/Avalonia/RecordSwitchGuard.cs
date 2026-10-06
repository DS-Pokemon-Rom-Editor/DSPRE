using System;
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
        /// <param name="question">Replaces "Save them before switching to another ...?" when the record is
        /// being reloaded rather than left.</param>
        public static async Task<bool> ConfirmLeaveAsync(
            IEditorWithUnsavedChanges editor,
            Window owner = null,
            string what = "record",
            string question = null)
        {
            if (editor == null || !editor.HasUnsavedChanges) return true;

            string subject = string.IsNullOrWhiteSpace(editor.UnsavedChangesDescription)
                ? "This " + what
                : editor.UnsavedChangesDescription;

            DialogHelper.MsgResult choice = await DialogHelper.AskThreeWay(
                $"{subject} has unsaved changes.\n\n{question ?? $"Save them before switching to another {what}?"}",
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

        /// <summary>True while <see cref="SnapBack"/> is walking a selector through another value; setters ignore
        /// anything a control writes back during it.</summary>
        public static bool IsSnappingBack { get; private set; }

        /// <summary>
        /// Puts a selector control back on the record still loaded after the editor refused a pick. A binding
        /// skips a value equal to the last one it read from the view model, and the refused pick never was read,
        /// so raising a change alone leaves the control on the refused row. Passing through a neighbouring value
        /// makes the real one count as a change.
        /// </summary>
        public static void SnapBack(Func<int> get, Action<int> set, Action raise)
            => SnapBack(get, set, raise, real => real == 0 ? 1 : real - 1);

        /// <param name="through">The other value to pass through, given the real one.</param>
        public static void SnapBack<T>(Func<T> get, Action<T> set, Action raise, Func<T, T> through)
        {
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (IsSnappingBack) return;
                T real = get();
                IsSnappingBack = true;
                try
                {
                    set(through(real));
                    raise();
                    set(real);
                    raise();
                }
                finally { IsSnappingBack = false; }
            }, global::Avalonia.Threading.DispatcherPriority.Background);
        }

        /// <summary>
        /// Another editor saved a record this one holds unsaved edits to. True to load the saved
        /// version, false to keep the edits here, which then overwrite it when saved.
        /// </summary>
        public static Task<bool> TakeSavedVersionAsync(string subject)
            => DialogHelper.AskTwoWay(
                $"{subject} was saved in another editor, and has unsaved changes here.\n\nWhich version do you want to keep?",
                "Saved Elsewhere", "The saved one", "Mine");
    }
}
