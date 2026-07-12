using System.Threading.Tasks;
using DSPRE.Editors;
using Xunit;

namespace DSPRE.Tests
{
    public class UnsavedChangesSaveContractTests
    {
        [Fact]
        public async Task DefaultAsyncSaveReportsSuccessAfterSynchronousEditorCleansItself()
        {
            IEditorWithUnsavedChanges editor = new TestEditor { HasUnsavedChanges = true };

            bool saved = await editor.SaveChangesAsync();

            Assert.True(saved);
            Assert.False(editor.HasUnsavedChanges);
        }

        [Fact]
        public async Task DefaultAsyncSaveReportsFailureWhenChangesRemain()
        {
            IEditorWithUnsavedChanges editor = new TestEditor
            {
                HasUnsavedChanges = true,
                CleanOnSave = false,
            };

            bool saved = await editor.SaveChangesAsync();

            Assert.False(saved);
            Assert.True(editor.HasUnsavedChanges);
        }

        private sealed class TestEditor : IEditorWithUnsavedChanges
        {
            public bool HasUnsavedChanges { get; set; }
            public bool CleanOnSave { get; set; } = true;
            public string UnsavedChangesDescription => "Test editor";

            public void SaveChanges()
            {
                if (CleanOnSave)
                {
                    HasUnsavedChanges = false;
                }
            }

            public void DiscardChanges() => HasUnsavedChanges = false;
        }
    }
}
