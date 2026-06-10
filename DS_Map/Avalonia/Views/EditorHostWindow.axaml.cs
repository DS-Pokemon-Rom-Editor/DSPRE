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

        public EditorHostWindow(string title, Control content, double width = 900, double height = 700)
        {
            Title = title;
            Width = width;
            Height = height;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Content = content;
        }

        protected override async void OnClosing(WindowClosingEventArgs e)
        {
            if (!_closeConfirmed && Content is Control c && c.DataContext is IEditorWithUnsavedChanges ed && ed.HasUnsavedChanges)
            {
                e.Cancel = true;
                bool discard = await DialogHelper.AskYesNo(
                    $"Discard unsaved changes to {ed.UnsavedChangesDescription}?", "Unsaved Changes");
                if (discard) { _closeConfirmed = true; ed.DiscardChanges(); Close(); }
                return;
            }
            base.OnClosing(e);
        }
    }
}
