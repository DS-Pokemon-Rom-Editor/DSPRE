namespace DSPRE.Editors
{
    /// <summary>
    /// Interface for editors that can track unsaved changes.
    /// Implement this on any editor (UserControl or Form) that should participate
    /// in the unsaved changes check when opening a new ROM.
    /// </summary>
    public interface IEditorWithUnsavedChanges
    {
        /// <summary>
        /// Gets whether the editor has unsaved changes.
        /// </summary>
        bool HasUnsavedChanges { get; }

        /// <summary>
        /// Gets a human-readable description of what has unsaved changes.
        /// For example: "Script File 42" or "Text Archive 279"
        /// </summary>
        string UnsavedChangesDescription { get; }

        /// <summary>
        /// Saves the current changes.
        /// </summary>
        void SaveChanges();

        /// <summary>
        /// Discards any unsaved changes and resets the editor state.
        /// </summary>
        void DiscardChanges();
    }
}
