using DSPRE.Avalonia.Data;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>
    /// Pins the CATS cell-actor playback timeline (NNS_G2dTickCellAnimation semantics): each frame is held for its
    /// duration, then the next plays; at the end a FORWARD_LOOP sequence wraps while a FORWARD (once) clamps to the
    /// last frame and reports Finished. Also checks per-frame SRT exposure and sequence switching.
    /// </summary>
    public class CellActorTests
    {
        private static CellSequence Seq(bool loop, params (int cell, int dur)[] fr)
        {
            var f = new CFrame[fr.Length];
            for (int i = 0; i < fr.Length; i++) f[i] = new CFrame(fr[i].cell, fr[i].dur, 0, 0, 0, 1, 1);
            return new CellSequence { Frames = f, Loop = loop };
        }

        [Fact]
        public void HoldsEachFrameForItsDuration()
        {
            var a = new CellActor(new[] { Seq(false, (10, 2), (11, 3)) });
            Assert.Equal(10, a.CellIndex);
            a.Tick(); Assert.Equal(10, a.CellIndex);   // frame 0 held for 2 ticks
            a.Tick(); Assert.Equal(11, a.CellIndex);   // 2nd tick → advance to frame 1
            a.Tick(); a.Tick(); Assert.Equal(11, a.CellIndex);
        }

        [Fact]
        public void OnceClampsAndFinishes()
        {
            var a = new CellActor(new[] { Seq(false, (10, 1), (11, 1)) });
            a.Tick();                       // → frame 1
            Assert.False(a.Finished);
            a.Tick();                       // past end → clamp to last, finished
            Assert.True(a.Finished);
            Assert.Equal(11, a.CellIndex);
            a.Tick(); Assert.Equal(11, a.CellIndex);   // stays put
        }

        [Fact]
        public void LoopWrapsAndNeverFinishes()
        {
            var a = new CellActor(new[] { Seq(true, (10, 1), (11, 1)) });
            a.Tick(); Assert.Equal(11, a.CellIndex);
            a.Tick(); Assert.Equal(10, a.CellIndex);   // wrapped
            Assert.False(a.Finished);
        }

        [Fact]
        public void SetSeqRestartsOnNewSequence()
        {
            var a = new CellActor(new[] { Seq(false, (10, 1)), Seq(true, (20, 1), (21, 1)) });
            a.Tick(); Assert.True(a.Finished);
            a.SetSeq(1);
            Assert.False(a.Finished);
            Assert.Equal(20, a.CellIndex);
            Assert.Equal(2, a.SeqCount);
        }

        [Fact]
        public void ExposesPerFrameSrt()
        {
            var s = new CellSequence { Frames = new[] { new CFrame(5, 1, 8, -4, 90, 2.0, 0.5) }, Loop = false };
            var a = new CellActor(new[] { s });
            Assert.Equal(8, a.FrameX);
            Assert.Equal(-4, a.FrameY);
            Assert.Equal(90, a.FrameRotDeg);
            Assert.Equal(2.0, a.FrameScaleX);
            Assert.Equal(0.5, a.FrameScaleY);
        }
    }
}
