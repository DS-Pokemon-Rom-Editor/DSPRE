using System;
using System.Linq;
using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>Idle motion for the preview. </summary>
    public class OverworldAnimatorTests
    {
        private static OverworldAnimator For(byte code, MoveFacing facing = MoveFacing.Down,
                                             int rx = 0, int rz = 0, int interval = 0, int seed = 1,
                                             Func<int, int, bool> blocked = null)
            => new OverworldAnimator(OverworldMovements.Find(code), facing, rx, rz, interval, seed, blocked);

        [Fact]
        public void EngineTimingsAreUsed()
        {
            Assert.Equal(new[] { 16, 32, 48, 64 }, OverworldAnimator.RandomWaits);
            Assert.Equal(24, OverworldAnimator.SpinIntervalFrames);
        }

        [Fact]
        public void StaticEventsNeverMove()
        {
            var a = For(0x00, MoveFacing.Left);
            a.Advance(600);
            Assert.Equal(MoveFacing.Left, a.Facing);
            Assert.Equal(0, a.OffsetX);
            Assert.Equal(0, a.OffsetZ);
        }

        [Fact]
        public void FixedFacingSnapsToItsDirection()
        {
            var a = For(0x0f, MoveFacing.Up);      // MV_DOWN
            a.Advance(1);
            Assert.Equal(MoveFacing.Down, a.Facing);
        }

        [Fact]
        public void SpinTurnsOneStepPerInterval()
        {
            var a = For(0x13, MoveFacing.Up);      // clockwise
            a.Advance(OverworldAnimator.SpinIntervalFrames);
            Assert.Equal(MoveFacing.Right, a.Facing);
            a.Advance(OverworldAnimator.SpinIntervalFrames);
            Assert.Equal(MoveFacing.Down, a.Facing);
        }

        [Fact]
        public void SpinDirectionIsHonoured()
        {
            var a = For(0x12, MoveFacing.Up);      // anticlockwise
            a.Advance(OverworldAnimator.SpinIntervalFrames);
            Assert.Equal(MoveFacing.Left, a.Facing);
        }

        [Fact]
        public void LookAroundNeverLeavesItsTile()
        {
            // MV_RND_UL and friends register a DirRnd handler, which turns the sprite off the move
            // status for good. They face different ways but stay put, whatever range they carry.
            var a = For(0x06, MoveFacing.Down, rx: 5, rz: 5, seed: 11);
            a.Advance(6000);
            Assert.Equal(0, a.OffsetX);
            Assert.Equal(0, a.OffsetZ);
            Assert.Contains(a.Facing, new[] { MoveFacing.Up, MoveFacing.Left });
        }

        [Fact]
        public void ARouteWalksAndTurnsBackAtTheEndOfItsRange()
        {
            // MV_RT2 walks the way the event faces until the range stops it, then comes back.
            var a = For(0x14, MoveFacing.Right, rx: 2, rz: 0, seed: 4);
            a.Advance(4000);
            Assert.InRange(a.OffsetX, -2, 2);
            Assert.Equal(0, a.OffsetZ);

            // It really did move at some point rather than sitting still.
            var b = For(0x14, MoveFacing.Right, rx: 2, rz: 0, seed: 4);
            b.Advance(8);
            Assert.Equal(1, b.OffsetX);
        }

        [Fact]
        public void ARouteWithNoRangeStaysPut()
        {
            var a = For(0x14, MoveFacing.Right, rx: 0, rz: 0);
            a.Advance(2000);
            Assert.Equal(0, a.OffsetX);
            Assert.Equal(0, a.OffsetZ);
        }

        [Fact]
        public void ABlockedTileStopsTheStepButNotTheTurn()
        {
            // Everything to the right is blocked, so a walker faces right but never gets there.
            var a = For(0x03, MoveFacing.Down, rx: 3, rz: 3, seed: 2, blocked: (x, z) => x > 0);
            a.Advance(6000);
            Assert.True(a.OffsetX <= 0);
        }

        [Fact]
        public void NothingWalksThroughABlockedMap()
        {
            var a = For(0x03, MoveFacing.Down, rx: 4, rz: 4, seed: 9, blocked: (x, z) => true);
            a.Advance(6000);
            Assert.Equal(0, a.OffsetX);
            Assert.Equal(0, a.OffsetZ);
        }

        [Fact]
        public void MinusOneRangeMeansNoFence()
        {
            // MOVE_LIMIT_NOT: the engine skips the range check entirely on that axis.
            var a = For(0x03, MoveFacing.Down, rx: OverworldAnimator.NoMoveLimit,
                        rz: OverworldAnimator.NoMoveLimit, seed: 6);
            a.Advance(6000);
            Assert.True(Math.Abs(a.OffsetX) + Math.Abs(a.OffsetZ) > 0);
        }

        [Fact]
        public void WanderStaysInsideItsMovementRange()
        {
            var a = For(0x03, MoveFacing.Down, rx: 2, rz: 1, seed: 7);
            a.Advance(6000);
            Assert.InRange(a.OffsetX, -2, 2);
            Assert.InRange(a.OffsetZ, -1, 1);
        }

        [Fact]
        public void WanderWithNoRangeTurnsWithoutDrifting()
        {
            var a = For(0x03, MoveFacing.Down, rx: 0, rz: 0, seed: 3);
            a.Advance(6000);
            Assert.Equal(0, a.OffsetX);
            Assert.Equal(0, a.OffsetZ);
        }

        [Fact]
        public void ConstrainedWanderOnlyUsesItsOwnAxis()
        {
            var a = For(0x04, MoveFacing.Down, rx: 3, rz: 3, seed: 5);   // up/down only
            a.Advance(6000);
            Assert.Equal(0, a.OffsetX);
        }

        [Fact]
        public void ParamOverridesThePace()
        {
            // The glancing and spinning trainer types take their interval from param1, so a spin with
            // an override of 4 has already turned by frame 4 instead of waiting the usual 24.
            var fast = For(0x13, MoveFacing.Up, interval: 4);
            fast.Advance(4);
            Assert.Equal(MoveFacing.Right, fast.Facing);

            var normal = For(0x13, MoveFacing.Up);
            normal.Advance(4);
            Assert.Equal(MoveFacing.Up, normal.Facing);
        }

        [Fact]
        public void SameSeedReplaysIdentically()
        {
            var a = For(0x03, MoveFacing.Down, rx: 3, rz: 3, seed: 42);
            var b = For(0x03, MoveFacing.Down, rx: 3, rz: 3, seed: 42);
            a.Advance(1200);
            b.Advance(1200);
            Assert.Equal(a.Facing, b.Facing);
            Assert.Equal(a.OffsetX, b.OffsetX);
            Assert.Equal(a.OffsetZ, b.OffsetZ);
        }
    }
}
