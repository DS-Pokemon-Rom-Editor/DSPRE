using System.Threading.Tasks;

namespace DSPRE.Editors
{
    /// <summary>
    /// An editor the unsaved-changes check asks before another ROM opens or DSPRE closes.
    /// </summary>
    public interface IEditorWithUnsavedChanges
    {
        bool HasUnsavedChanges { get; }

        /// <summary>What has unsaved changes, such as "Script file 42".</summary>
        string UnsavedChangesDescription { get; }

        void SaveChanges();

        /// <summary>Saves and completes when the save has finished; false when it was cancelled, failed or left
        /// unsaved changes.</summary>
        Task<bool> SaveChangesAsync()
        {
            SaveChanges();
            return Task.FromResult(!HasUnsavedChanges);
        }

        void DiscardChanges();
    }
}
