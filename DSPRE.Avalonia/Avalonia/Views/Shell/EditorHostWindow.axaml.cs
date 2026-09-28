using Avalonia.Controls;
using Avalonia.Input;
using DSPRE.Editors;

namespace DSPRE.Avalonia.Views.Shell
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

        private static readonly System.Collections.Generic.Dictionary<string, (double, double)> MinimumFor = new()
        {
            ["Mart Editor"] = (860, 500), ["Header Editor"] = (1080, 560), ["Matrix Editor"] = (720, 500),
            ["Map Editor"] = (1000, 600), ["Event Editor"] = (1000, 600), ["Vs. Seeker Rematch Editor"] = (720, 460),
            ["Camera Editor"] = (820, 480),
        };

        public EditorHostWindow() { }

        public EditorHostWindow(string title, Control content, double width = 900, double height = 700)
        {
            Title = title;
            // An editor listed here needs that much room to show its content, so it opens at least that big;
            // anything else gets a general floor that never exceeds its own opening size.
            if (MinimumFor.TryGetValue(title ?? "", out var m))
            {
                width = System.Math.Max(width, m.Item1);
                height = System.Math.Max(height, m.Item2);
                MinWidth = m.Item1;
                MinHeight = m.Item2;
            }
            else
            {
                MinWidth = System.Math.Min(width, 600);
                MinHeight = System.Math.Min(height, 400);
            }
            Width = width;
            Height = height;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Content = content;

            if (content?.DataContext is IEditorWithUnsavedChanges editor)
            {
                string baseTitle = title ?? "";
                void UpdateTitle() => Title = (editor.HasUnsavedChanges ? "● " : "") + baseTitle;
                UpdateTitle();
                if (editor is System.ComponentModel.INotifyPropertyChanged inpc)
                    inpc.PropertyChanged += (_, e) =>
                    {
                        if (e.PropertyName == nameof(IEditorWithUnsavedChanges.HasUnsavedChanges)) UpdateTitle();
                    };

                KeyBindings.Add(new KeyBinding
                {
                    Gesture = new KeyGesture(Key.S, KeyModifiers.Control),
                    Command = new DSPRE.Avalonia.EditorWindowChrome.RelayCommand(() =>
                    {
                        if (editor.HasUnsavedChanges)
                            _ = DSPRE.Avalonia.EditorWindowChrome.TrySaveChangesAsync(editor, "saving");
                    }),
                });
            }

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
                if (!await global::DSPRE.Avalonia.UnsavedChangesDialog.ShowIfNeededAsync(this, ed, Title))
                    return;
                _closeConfirmed = true; Close();
                return;
            }
            base.OnClosing(e);
        }
    }
}
