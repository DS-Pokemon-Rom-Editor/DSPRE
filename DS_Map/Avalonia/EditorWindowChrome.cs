using System;
using System.ComponentModel;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Input;
using DSPRE.Editors;

namespace DSPRE.Avalonia
{
    /// <summary>
    /// Shared editor-window chrome: a "●" unsaved-changes marker in the title bar and a Ctrl+S save
    /// shortcut. Attach once from a window that hosts an <see cref="IEditorWithUnsavedChanges"/> VM —
    /// one line keeps every editor consistent without per-window plumbing.
    /// </summary>
    public static class EditorWindowChrome
    {
        /// <param name="manageTitle">When true, prefixes the window title with "● " while there are unsaved
        /// changes. Pass FALSE for windows whose Title is data-bound (Title="{Binding Title}") — the chrome
        /// can't set window.Title without fighting the binding; those VMs show their own marker instead.</param>
        public static void Attach(Window window, IEditorWithUnsavedChanges vm, bool manageTitle = true)
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
