using DSPRE.Avalonia;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>
    /// Pins down the pure undo/redo sequencing that backs every editor's undo support: capture/undo/redo
    /// ordering, redo-branch clearing, dirty tracking around save, and burst coalescing.
    /// Snapshots are reference-tracked, so the tests use distinct string instances as states.
    /// </summary>
    public class UndoHistoryTests
    {
        private static string S(string s) => new string(s.ToCharArray());   // force a fresh instance

        [Fact]
        public void FreshHistory_HasNothingToUndoOrRedo_AndIsClean()
        {
            var h = new UndoHistory<string>();
            h.Reset(S("base"));
            Assert.False(h.CanUndo);
            Assert.False(h.CanRedo);
            Assert.False(h.IsDirty);
        }

        [Fact]
        public void Capture_EnablesUndo_AndUndoReturnsPreviousState()
        {
            var h = new UndoHistory<string>();
            var a = S("a");
            h.Reset(a);
            h.Capture(S("b"));
            Assert.True(h.CanUndo);
            Assert.True(h.IsDirty);
            Assert.Same(a, h.Undo());      // back to the exact baseline instance
            Assert.False(h.IsDirty);       // ...so we're clean again
            Assert.True(h.CanRedo);
        }

        [Fact]
        public void Redo_ReappliesUndoneState()
        {
            var h = new UndoHistory<string>();
            h.Reset(S("a"));
            var b = S("b");
            h.Capture(b);
            h.Undo();
            Assert.Same(b, h.Redo());
            Assert.False(h.CanRedo);
        }

        [Fact]
        public void Capture_ClearsTheRedoBranch()
        {
            var h = new UndoHistory<string>();
            h.Reset(S("a"));
            h.Capture(S("b"));
            h.Undo();                      // redo now available
            Assert.True(h.CanRedo);
            h.Capture(S("c"));             // new edit kills the redo branch
            Assert.False(h.CanRedo);
        }

        [Fact]
        public void MultiLevel_UndoRedo_WalksTheStack()
        {
            var h = new UndoHistory<string>();
            var a = S("a"); var b = S("b"); var c = S("c");
            h.Reset(a);
            h.Capture(b);
            h.Capture(c);
            Assert.Same(b, h.Undo());
            Assert.Same(a, h.Undo());
            Assert.False(h.CanUndo);
            Assert.Same(b, h.Redo());
            Assert.Same(c, h.Redo());
        }

        [Fact]
        public void Coalesce_DoesNotAddAnUndoLevel_ButUpdatesCurrent()
        {
            var h = new UndoHistory<string>();
            var a = S("a");
            h.Reset(a);
            h.Capture(S("b"));              // level 1
            h.Capture(S("b2"), coalesce: true);   // same step, just newer state
            Assert.Same(a, h.Undo());      // one undo jumps straight back past the whole burst
            Assert.False(h.CanUndo);
        }

        [Fact]
        public void MarkSaved_MakesCurrentStateClean_ButKeepsHistory()
        {
            var h = new UndoHistory<string>();
            h.Reset(S("a"));
            h.Capture(S("b"));
            h.MarkSaved();
            Assert.False(h.IsDirty);       // saved here
            Assert.True(h.CanUndo);        // ...but can still undo past the save
            h.Undo();
            Assert.True(h.IsDirty);        // undoing past the saved point is dirty again
        }

        [Fact]
        public void Reset_ClearsEverything()
        {
            var h = new UndoHistory<string>();
            h.Reset(S("a"));
            h.Capture(S("b"));
            h.Reset(S("fresh"));
            Assert.False(h.CanUndo);
            Assert.False(h.CanRedo);
            Assert.False(h.IsDirty);
        }
    }
}
