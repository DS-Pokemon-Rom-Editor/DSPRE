using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using DSPRE.Editors;

namespace DSPRE.Avalonia
{
    /// <summary>
    /// Inspects the currently-open Avalonia editor windows. Used by the app-quit guard: each editor has its
    /// own close-confirmation (see EditorWindowChrome), but quitting the app force-closes every window via
    /// <c>desktop.Shutdown()</c>, which bypasses those guards — so before quitting we ask here whether any
    /// editor still holds unsaved work.
    /// </summary>
    public static class OpenEditors
    {
        /// <summary>Descriptions of every open editor that currently has unsaved changes (empty = safe to quit).</summary>
        public static IReadOnlyList<string> UnsavedDescriptions()
        {
            var list = new List<string>();
            if (global::Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
                return list;

            foreach (var w in desktop.Windows)
            {
                // Regular editor windows carry the VM as their own DataContext; UserControl-based editors
                // hosted in EditorHostWindow carry it on the hosted content instead.
                var ed = w.DataContext as IEditorWithUnsavedChanges
                         ?? (w.Content as Control)?.DataContext as IEditorWithUnsavedChanges;
                if (ed != null && ed.HasUnsavedChanges)
                    list.Add(ed.UnsavedChangesDescription);
            }
            return list;
        }
    }
}
