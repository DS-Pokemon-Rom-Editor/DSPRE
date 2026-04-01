using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace DSPRE.Editors
{
    /// <summary>
    /// Registry for tracking standalone editor windows (Forms opened with Show()).
    /// Used to check for unsaved changes and close all editors when switching ROMs.
    /// </summary>
    public static class OpenEditorsRegistry
    {
        private static readonly HashSet<Form> _openEditors = new HashSet<Form>();

        /// <summary>
        /// Registers a standalone editor form. Call this when the editor is opened.
        /// The editor will be automatically unregistered when it closes.
        /// </summary>
        /// <param name="editor">The editor form to register.</param>
        public static void Register(Form editor)
        {
            if (editor == null) return;

            if (_openEditors.Add(editor))
            {
                editor.FormClosed += OnEditorClosed;
                AppLogger.Debug($"OpenEditorsRegistry: Registered {editor.GetType().Name}");
            }
        }

        /// <summary>
        /// Unregisters a standalone editor form.
        /// </summary>
        /// <param name="editor">The editor form to unregister.</param>
        public static void Unregister(Form editor)
        {
            if (editor == null) return;

            if (_openEditors.Remove(editor))
            {
                editor.FormClosed -= OnEditorClosed;
                AppLogger.Debug($"OpenEditorsRegistry: Unregistered {editor.GetType().Name}");
            }
        }

        private static void OnEditorClosed(object sender, FormClosedEventArgs e)
        {
            if (sender is Form form)
            {
                _openEditors.Remove(form);
                form.FormClosed -= OnEditorClosed;
                AppLogger.Debug($"OpenEditorsRegistry: Auto-unregistered {form.GetType().Name} on close");
            }
        }

        /// <summary>
        /// Gets all currently open standalone editors.
        /// </summary>
        public static IEnumerable<Form> GetAll() => _openEditors.ToList();

        /// <summary>
        /// Gets all currently open editors that implement IEditorWithUnsavedChanges.
        /// </summary>
        public static IEnumerable<IEditorWithUnsavedChanges> GetEditorsWithUnsavedChangesSupport()
        {
            return _openEditors
                .OfType<IEditorWithUnsavedChanges>()
                .ToList();
        }

        /// <summary>
        /// Gets all editors that currently have unsaved changes.
        /// </summary>
        public static IEnumerable<(Form form, IEditorWithUnsavedChanges editor)> GetEditorsWithUnsavedChanges()
        {
            return _openEditors
                .Where(f => f is IEditorWithUnsavedChanges e && e.HasUnsavedChanges)
                .Select(f => (f, (IEditorWithUnsavedChanges)f))
                .ToList();
        }

        /// <summary>
        /// Checks if any registered editor has unsaved changes.
        /// </summary>
        public static bool AnyHasUnsavedChanges()
        {
            return _openEditors.Any(f => f is IEditorWithUnsavedChanges e && e.HasUnsavedChanges);
        }

        /// <summary>
        /// Closes all registered standalone editors.
        /// </summary>
        /// <param name="force">If true, closes without prompting for unsaved changes.</param>
        public static void CloseAll(bool force = false)
        {
            AppLogger.Info($"OpenEditorsRegistry: Closing all {_openEditors.Count} registered editors");

            // Create a copy to avoid modification during iteration
            var editorsToClose = _openEditors.ToList();

            foreach (var editor in editorsToClose)
            {
                try
                {
                    if (force && editor is IEditorWithUnsavedChanges unsavedEditor)
                    {
                        unsavedEditor.DiscardChanges();
                    }
                    editor.Close();
                }
                catch (Exception ex)
                {
                    AppLogger.Error($"OpenEditorsRegistry: Error closing {editor.GetType().Name}: {ex.Message}");
                }
            }

            _openEditors.Clear();
        }

        /// <summary>
        /// Gets the count of currently registered editors.
        /// </summary>
        public static int Count => _openEditors.Count;
    }
}
