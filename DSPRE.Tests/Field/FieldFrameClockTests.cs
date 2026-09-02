using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>The preview has to keep time with the real game. </summary>
    public class FieldFrameClockTests
    {
        // What a 33.3ms timer really gets on Windows: the next 15.625ms wake-up after it was due.
        private const double RealTick = 0.046875;

        [Fact]
        public void ASecondOfRaggedTicksStillGivesASecondOfFrames()
        {
            var clock = new FieldFrameClock();
            int frames = 0;
            double elapsed = 0;
            while (elapsed < 1.0)
            {
                frames += clock.Tick(RealTick);
                elapsed += RealTick;
            }

            // Twenty one ticks cover 0.984s, so twenty nine or thirty frames. Rounding each tick on its
            // own would have given twenty one, which is the bug this is here to catch.
            Assert.InRange(frames, 29, 30);
        }

        [Fact]
        public void TenSecondsStaysInStepRatherThanDriftingSlow()
        {
            var clock = new FieldFrameClock();
            int frames = 0;
            for (double t = 0; t < 10.0; t += RealTick) frames += clock.Tick(RealTick);

            Assert.InRange(frames, 298, 300);       // thirty a second, give or take the last part-frame
        }

        [Fact]
        public void TicksShorterThanAFrameStillAddUp()
        {
            var clock = new FieldFrameClock();
            int frames = 0;
            for (int i = 0; i < 600; i++) frames += clock.Tick(1 / 600.0);   // a whole second in tiny pieces
            Assert.Equal(30, frames);
        }

        [Fact]
        public void SpeedScalesIt()
        {
            var clock = new FieldFrameClock();
            int frames = 0;
            for (double t = 0; t < 1.0; t += RealTick) frames += clock.Tick(RealTick, 2.0);
            Assert.InRange(frames, 58, 62);       // the loop overshoots a second by one tick
        }

        [Fact]
        public void ComingBackFromALongPauseDoesNotFastForward()
        {
            var clock = new FieldFrameClock();
            Assert.Equal(FieldFrameClock.MostFramesAtOnce, clock.Tick(60.0));
        }

        [Fact]
        public void ResettingForgetsThePartFrame()
        {
            var clock = new FieldFrameClock();
            clock.Tick(RealTick);
            Assert.True(clock.Owed > 0, "there should be part of a frame left over");
            clock.Reset();
            Assert.Equal(0, clock.Owed);
        }

        [Fact]
        public void NothingHappensWithNoTimeOrNoSpeed()
        {
            var clock = new FieldFrameClock();
            Assert.Equal(0, clock.Tick(0));
            Assert.Equal(0, clock.Tick(-1));
            Assert.Equal(0, clock.Tick(1.0, 0));
        }
    }
}
