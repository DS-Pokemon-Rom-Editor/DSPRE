using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using DSPRE.Avalonia.Views;
using DSPRE.Editors;

namespace DSPRE.Avalonia
{
    /// <summary>Enumerates Avalonia's embedded and standalone editors for project-change guards.</summary>
    public static class OpenEditors
    {
        public static IReadOnlyList<UnsavedChangesDialog.UnsavedEditorInfo> GetUnsavedEditors(
            MainWindowView mainWindow = null)
        {
            var result = new List<UnsavedChangesDialog.UnsavedEditorInfo>();
            var seen = new HashSet<IEditorWithUnsavedChanges>();

            if (mainWindow != null)
            {
                foreach (var embedded in mainWindow.GetEmbeddedEditors())
                {
                    AddIfDirty(result, seen, embedded.EditorName, embedded.Editor);
                }
            }

            if (global::Avalonia.Application.Current?.ApplicationLifetime
                is not IClassicDesktopStyleApplicationLifetime desktop)
            {
                return result;
            }

            foreach (var window in desktop.Windows.ToList())
            {
                if (ReferenceEquals(window, mainWindow)) continue;

                var editor = GetEditor(window);
                if (editor == null) continue;
                AddIfDirty(result, seen, GetWindowEditorName(window, editor), editor);
            }

            return result;
        }

        /// <summary>Returns dirty standalone-window descriptions for the legacy host shutdown guard.</summary>
        public static IReadOnlyList<string> UnsavedDescriptions()
            => GetUnsavedEditors().Select(info => info.ToString()).ToList();

        public static void CloseEditorWindows(MainWindowView mainWindow = null)
        {
            if (global::Avalonia.Application.Current?.ApplicationLifetime
                is not IClassicDesktopStyleApplicationLifetime desktop)
            {
                return;
            }

            foreach (var window in desktop.Windows.ToList())
            {
                if (ReferenceEquals(window, mainWindow)) continue;
                if (GetEditor(window) != null) window.Close();
            }
        }

        private static void AddIfDirty(
            ICollection<UnsavedChangesDialog.UnsavedEditorInfo> result,
            ISet<IEditorWithUnsavedChanges> seen,
            string editorName,
            IEditorWithUnsavedChanges editor)
        {
            if (editor == null || !editor.HasUnsavedChanges || !seen.Add(editor)) return;
            result.Add(new UnsavedChangesDialog.UnsavedEditorInfo
            {
                EditorName = editorName,
                Description = editor.UnsavedChangesDescription,
                Editor = editor,
            });
        }

        private static IEditorWithUnsavedChanges GetEditor(Window window)
            => window?.DataContext as IEditorWithUnsavedChanges
                ?? (window?.Content as Control)?.DataContext as IEditorWithUnsavedChanges;

        private static string GetWindowEditorName(Window window, IEditorWithUnsavedChanges editor)
        {
            string title = window?.Title?.Trim() ?? string.Empty;
            while (title.StartsWith("●", StringComparison.Ordinal))
            {
                title = title.Substring(1).TrimStart();
            }

            return string.IsNullOrWhiteSpace(title) ? editor.GetType().Name : title;
        }
    }
}
