using System.Threading;
using DSPRE.Avalonia;
using Xunit;

namespace DSPRE.Tests.Editors
{
    public class ByteStateUndoTests
    {
        private byte[] _state = { 1 };
        private int _raised;

        private ByteStateUndo Make() => new ByteStateUndo(() => (byte[])_state.Clone(), b => _state = b, () => _raised++);

        [Fact]
        public void UndoAndRedoWalkTheRecordedStates()
        {
            var undo = Make();
            Assert.False(undo.CanUndo);
            _state = new byte[] { 2 }; undo.Record();
            Thread.Sleep(600);
            _state = new byte[] { 3 }; undo.Record();
            undo.Undo();
            Assert.Equal(new byte[] { 2 }, _state);
            undo.Undo();
            Assert.Equal(new byte[] { 1 }, _state);
            Assert.False(undo.CanUndo);
            undo.Redo(); undo.Redo();
            Assert.Equal(new byte[] { 3 }, _state);
            Assert.False(undo.CanRedo);
            Assert.True(_raised > 0);
        }

        [Fact]
        public void QuickEditsFoldIntoOneStepAndNoOpsAreIgnored()
        {
            var undo = Make();
            undo.Record();
            Assert.False(undo.CanUndo);
            _state = new byte[] { 2 }; undo.Record();
            _state = new byte[] { 3 }; undo.Record();
            undo.Undo();
            Assert.Equal(new byte[] { 1 }, _state);
            Assert.False(undo.CanUndo);
        }

        [Fact]
        public void AnEditAfterUndoDropsTheRedo()
        {
            var undo = Make();
            _state = new byte[] { 2 }; undo.Record();
            undo.Undo();
            _state = new byte[] { 5 }; undo.Record();
            Assert.False(undo.CanRedo);
            undo.Undo();
            Assert.Equal(new byte[] { 1 }, _state);
        }
    }
}
