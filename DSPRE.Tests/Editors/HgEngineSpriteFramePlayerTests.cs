using System.Collections.Generic;
using System.Linq;
using DSPRE.HgEngine;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>
    /// Playback semantics for data/SpriteOffsets.c's SpriteFrame[10] idle runs, checked against the shapes
    /// the real file actually contains. The run is one-shot and ends on sprite frame 0; a frameNo below -1
    /// is a counted jump, not a terminator.
    /// </summary>
    public class HgEngineSpriteFramePlayerTests
    {
        private static HgEngineSpriteOffsets.SpriteFrameSlot Slot(int frameNo, int duration, int hShift = 0) =>
            new HgEngineSpriteOffsets.SpriteFrameSlot(frameNo, duration, hShift, 0);

        private static List<HgEngineSpriteOffsets.SpriteFrameSlot> Padded(params HgEngineSpriteOffsets.SpriteFrameSlot[] used)
        {
            var slots = used.ToList();
            while (slots.Count < HgEngineSpriteFramePlayer.MaxFrames) slots.Add(Slot(-1, 0));
            return slots;
        }

        /// <summary>Collects the frame shown on each of the next <paramref name="ticks"/> game frames.</summary>
        private static List<int> Run(HgEngineSpriteFramePlayer player, int ticks)
        {
            var seen = new List<int> { player.SpriteFrame };
            for (int i = 0; i < ticks; i++) { player.Tick(); seen.Add(player.SpriteFrame); }
            return seen;
        }

        [Fact]
        public void SingleStepRunShowsItsFrameThenSettlesOnFrameZero()
        {
            // Venusaur's real front run: one step, then the terminator. Looping the non-negative entries
            // instead pins the sprite on frame 1 forever, which is the reported "frames freeze".
            var player = new HgEngineSpriteFramePlayer();
            player.Start(Padded(Slot(1, 18, -4)));

            Assert.True(player.Active);
            Assert.Equal(1, player.SpriteFrame);
            Assert.Equal(-4, player.HorizontalShift);

            var seen = Run(player, 30);

            Assert.Equal(Enumerable.Repeat(1, 19), seen.Take(19));   // duration 18 holds for 18+1 ticks
            Assert.Equal(0, seen[19]);
            Assert.All(seen.Skip(19), f => Assert.Equal(0, f));
            Assert.False(player.Active);
            Assert.Equal(0, player.HorizontalShift);   // the engine clears xOffset when the run ends
        }

        [Fact]
        public void MultiStepRunWalksEveryStepInOrderThenEndsOnFrameZero()
        {
            // Blastoise's real front run.
            var player = new HgEngineSpriteFramePlayer();
            player.Start(Padded(Slot(1, 6), Slot(0, 10), Slot(1, 10)));

            var seen = Run(player, 40);

            Assert.Equal(Enumerable.Repeat(1, 7), seen.Take(7));
            Assert.Equal(Enumerable.Repeat(0, 11), seen.Skip(7).Take(11));
            Assert.Equal(Enumerable.Repeat(1, 11), seen.Skip(18).Take(11));
            Assert.Equal(0, seen[29]);
            Assert.False(player.Active);
        }

        [Fact]
        public void FrameNoBelowMinusOneJumpsBackTheGivenNumberOfTimesThenFallsThrough()
        {
            // frameNo -2 targets slot 0 ((-frameNo) - 2). The jump entry's duration is a visit count, not a
            // delay: it is reached twice, jumping on the first and falling through on the second.
            var player = new HgEngineSpriteFramePlayer();
            player.Start(Padded(Slot(1, 1), Slot(-2, 2)));

            var seen = Run(player, 8);

            // Two passes over slot 0 at 1+1 ticks each, then the terminator.
            Assert.Equal(new[] { 1, 1, 1, 1, 0, 0, 0, 0, 0 }, seen);
            Assert.False(player.Active);
        }

        [Fact]
        public void ATerminatorInTheFirstSlotNeverStartsTheRun()
        {
            var player = new HgEngineSpriteFramePlayer();
            player.Start(Padded());

            Assert.False(player.Active);
            Assert.Equal(0, player.SpriteFrame);
            Assert.All(Run(player, 10), f => Assert.Equal(0, f));
        }

        [Fact]
        public void NullSlotsRestOnFrameZeroWithoutThrowing()
        {
            var player = new HgEngineSpriteFramePlayer();
            player.Start(null);

            Assert.False(player.Active);
            Assert.All(Run(player, 5), f => Assert.Equal(0, f));
        }

        [Fact]
        public void HorizontalShiftFollowsWhicheverSlotIsOnScreen()
        {
            var player = new HgEngineSpriteFramePlayer();
            player.Start(Padded(Slot(1, 1, -12), Slot(0, 1, 5)));

            Assert.Equal(-12, player.HorizontalShift);
            player.Tick();
            player.Tick();
            Assert.Equal(5, player.HorizontalShift);
            Assert.Equal(0, player.SpriteFrame);
        }

        [Fact]
        public void ReachableFramesCoversEveryDisplayedFramePlusTheFrameZeroTheRunEndsOn()
        {
            var reachable = HgEngineSpriteFramePlayer.ReachableFrames(Padded(Slot(1, 18, -4)));
            Assert.Equal(new[] { 0, 1 }, reachable.OrderBy(f => f));

            // A run that only ever shows frame 1 still ends on frame 0, so both are reachable.
            Assert.Contains(0, HgEngineSpriteFramePlayer.ReachableFrames(Padded(Slot(1, 4), Slot(1, 4))));
            Assert.Equal(new[] { 0 }, HgEngineSpriteFramePlayer.ReachableFrames(Padded()));
        }

        [Fact]
        public void ASelfReferentialJumpChainTerminatesInsteadOfHangingTheUiThread()
        {
            // -2 targets slot 0; a jump entry in slot 0 would spin forever in the engine's own loop.
            var player = new HgEngineSpriteFramePlayer();
            player.Start(Padded(Slot(-2, 0), Slot(-2, 0)));
            player.Tick();

            Assert.False(player.Active);
        }
    }
}
