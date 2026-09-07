using System.Threading.Tasks;
using DSPRE.Avalonia;
using DSPRE.Editors;
using Xunit;

namespace DSPRE.Tests.Editors
{
    /// <summary>
    /// Covers the paths through the record-switch guard that resolve without asking the user
    /// anything. The dirty path opens a dialog and needs a live UI, so it is deliberately not
    /// asserted here; see the outstanding validation list for the check that covers it.
    ///
    /// These two cases matter more than they look. Every guarded selection setter calls the guard on
    /// every change, including the ordinary case of moving between records with nothing edited. If
    /// the guard ever stopped short-circuiting there, picking a different trainer would put a prompt
    /// in front of the user for no reason.
    /// </summary>
    public class RecordSwitchGuardTests
    {
        private sealed class FakeEditor : IEditorWithUnsavedChanges
        {
            public bool Dirty;
            public int SaveCalls;
            public int DiscardCalls;

            public bool HasUnsavedChanges => Dirty;
            public string UnsavedChangesDescription => "Fake record 7";
            public void SaveChanges() { SaveCalls++; Dirty = false; }
            public void DiscardChanges() { DiscardCalls++; Dirty = false; }
        }

        [Fact]
        public async Task ANullEditorIsAlwaysSafeToLeave()
        {
            Assert.True(await RecordSwitchGuard.ConfirmLeaveAsync(null));
        }

        [Fact]
        public async Task ACleanEditorIsLeftWithoutPromptingOrTouchingIt()
        {
            var editor = new FakeEditor { Dirty = false };

            bool mayLeave = await RecordSwitchGuard.ConfirmLeaveAsync(editor, null, "trainer");

            Assert.True(mayLeave);
            // Nothing was saved or discarded: a clean switch must not write to the project, and it
            // must not call DiscardChanges either, which in most editors reloads the record from disk.
            Assert.Equal(0, editor.SaveCalls);
            Assert.Equal(0, editor.DiscardCalls);
        }
    }
}
