using Avalonia.Controls;
using DSPRE.Editors;

namespace DSPRE.Avalonia.Views
{
    /// <summary>
    /// Generic host window for editors authored as <see cref="UserControl"/>s (so they
    /// can be embedded as shell tabs) when they need to be shown standalone. Forwards the
    /// unsaved-changes guard to the hosted control's <see cref="IEditorWithUnsavedChanges"/>
    /// view-model, if any.
    /// </summary>
    public class EditorHostWindow : Window
    {
        private bool _closeConfirmed;

        public EditorHostWindow() { }

        public EditorHostWindow(string title, Control content, double width = 1020, double height = 720)
        {
            Title = title;
            Width = width;
            Height = height;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Content = content;

            // Hosted UserControl editors don't get EditorWindowChrome; forward Ctrl+Z / Ctrl+Y here when
            // their VM supports undo (e.g. the Header editor).
            if (content?.DataContext is DSPRE.Avalonia.ISupportsUndo undo)
                DSPRE.Avalonia.EditorWindowChrome.AttachUndoKeys(this, undo);
        }

        protected override async void OnClosing(WindowClosingEventArgs e)
        {
            if (!_closeConfirmed && Content is Control c && c.DataContext is IEditorWithUnsavedChanges ed && ed.HasUnsavedChanges)
            {
                e.Cancel = true;
                var r = await DialogHelper.AskYesNoCancel(
                    $"You have unsaved changes to {ed.UnsavedChangesDescription}. Do you want to save them before closing?",
                    "Unsaved Changes");
                if (r == DialogHelper.MsgResult.Cancel) return;   // stay open
                if (r == DialogHelper.MsgResult.Yes) ed.SaveChanges(); else ed.DiscardChanges();
                _closeConfirmed = true; Close();
                return;
            }
            base.OnClosing(e);
        }
    }
}
