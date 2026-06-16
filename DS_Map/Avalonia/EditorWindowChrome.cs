using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Input;
using DSPRE.Editors;

namespace DSPRE.Avalonia
{
    /// <summary>
    /// Shared editor-window chrome, so every editor behaves the same without per-window plumbing:
    ///  • a "●" unsaved-changes marker in the title bar,
    ///  • a Ctrl+S save shortcut, and
    ///  • a close-confirmation guard (prompt before discarding unsaved work).
    /// Attach once from a window that hosts an <see cref="IEditorWithUnsavedChanges"/> VM. Because the
    /// guard lives here, any future editor that calls <see cref="Attach"/> gets it for free — there is no
    /// separate <c>OnClosing</c> to remember (and forget) per view.
    /// </summary>
    public static class EditorWindowChrome
    {
        /// <param name="manageTitle">When true, prefixes the window title with "● " while there are unsaved
        /// changes. Pass FALSE for windows whose Title is data-bound (Title="{Binding Title}") — the chrome
        /// can't set window.Title without fighting the binding; those VMs show their own marker instead.</param>
        /// <param name="confirmClose">Optional VM-driven close flow (e.g. <c>vm.ConfirmCloseAsync</c>) offering
        /// Save / Don't Save / Cancel. Invoked only when there ARE unsaved changes; return true to proceed with
        /// the close. When null, a default Save / Don't Save / Cancel prompt is used (Yes → SaveChanges,
        /// No → DiscardChanges, Cancel → stay open).</param>
        /// <param name="onClosed">Optional cleanup (e.g. <c>vm.Detach</c>) run exactly once when the window is
        /// actually allowed to close — both on the clean path and after a confirmed discard.</param>
        public static void Attach(
            Window window,
            IEditorWithUnsavedChanges vm,
            bool manageTitle = true,
            Func<Task<bool>> confirmClose = null,
            Action onClosed = null)
        {
            if (window == null || vm == null) return;

            if (manageTitle)
            {
                string baseTitle = window.Title ?? "";
                void UpdateTitle() => window.Title = (vm.HasUnsavedChanges ? "● " : "") + baseTitle;
                UpdateTitle();
                if (vm is INotifyPropertyChanged inpc)
                    inpc.PropertyChanged += (_, e) =>
                    {
                        if (e.PropertyName == nameof(IEditorWithUnsavedChanges.HasUnsavedChanges)) UpdateTitle();
                    };
            }

            window.KeyBindings.Add(new KeyBinding
            {
                Gesture = new KeyGesture(Key.S, KeyModifiers.Control),
                Command = new RelayCommand(() => { if (vm.HasUnsavedChanges) vm.SaveChanges(); }),
            });

            // Ctrl+Z / Ctrl+Y for editors that support undo (same opt-in style as Ctrl+S).
            if (vm is ISupportsUndo undo) AttachUndoKeys(window, undo);

            // ── Close guard ───────────────────────────────────────────────────────────────────────
            // One implementation for every editor. `confirmed` flips true once the user has approved the
            // close so the second Closing (raised by our own Close() call) passes straight through.
            bool confirmed = false;
            window.Closing += async (_, e) =>
            {
                if (confirmed) return;

                if (!vm.HasUnsavedChanges)   // nothing to lose — let it close, but still run cleanup once
                {
                    onClosed?.Invoke();
                    return;
                }

                e.Cancel = true;

                bool proceed;
                if (confirmClose != null)
                {
                    proceed = await confirmClose();   // VM owns the Save / Don't Save / Cancel dialog
                }
                else
                {
                    // Same Save / Don't Save / Cancel flow as the ConfirmCloseAsync editors, so every editor
                    // asks the question the same way.
                    var r = await DialogHelper.AskYesNoCancel(
                        $"You have unsaved changes to {vm.UnsavedChangesDescription}. Do you want to save them before closing?",
                        "Unsaved Changes");
                    if (r == DialogHelper.MsgResult.Cancel) return;   // stay open
                    if (r == DialogHelper.MsgResult.Yes) vm.SaveChanges(); else vm.DiscardChanges();
                    proceed = true;
                }

                if (proceed)
                {
                    confirmed = true;
                    onClosed?.Invoke();
                    window.Close();
                }
            };
        }

        /// <summary>Adds Ctrl+Z / Ctrl+Y key bindings driving an <see cref="ISupportsUndo"/>. Used by both
        /// <see cref="Attach"/> (Window editors) and <c>EditorHostWindow</c> (UserControl editors hosted in it).</summary>
        public static void AttachUndoKeys(Window window, ISupportsUndo undo)
        {
            if (window == null || undo == null) return;
            window.KeyBindings.Add(new KeyBinding
            {
                Gesture = new KeyGesture(Key.Z, KeyModifiers.Control),
                Command = new RelayCommand(() => { if (undo.CanUndo) undo.Undo(); }),
            });
            window.KeyBindings.Add(new KeyBinding
            {
                Gesture = new KeyGesture(Key.Y, KeyModifiers.Control),
                Command = new RelayCommand(() => { if (undo.CanRedo) undo.Redo(); }),
            });
        }

        private sealed class RelayCommand : ICommand
        {
            private readonly Action _execute;
            public RelayCommand(Action execute) => _execute = execute;
            public bool CanExecute(object parameter) => true;
            public void Execute(object parameter) => _execute();
            public event EventHandler CanExecuteChanged { add { } remove { } }
        }
    }
}
