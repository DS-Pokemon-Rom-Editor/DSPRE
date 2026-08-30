using System.Linq;
using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>Pins the movement table against fieldobj_code.h: 0x00-0x38 with no gaps.</summary>
    public class OverworldMovementTests
    {
        [Fact]
        public void CoversEveryCodeTheEngineDefines()
        {
            var values = OverworldMovements.All.Select(m => m.Value).ToArray();
            Assert.Equal(0x39, values.Length);                       // MV_CODE_MAX
            Assert.Equal(Enumerable.Range(0, 0x39).Select(i => (byte)i), values);
        }

        [Fact]
        public void CodesPastTheEngineMaximumAreNotDefined()
        {
            Assert.False(OverworldMovements.IsDefined(0x39));
            Assert.False(OverworldMovements.IsDefined(71));          // DSPRE's dropdown goes this far
            Assert.False(OverworldMovements.IsDefined(OverworldMovements.NotSet));
        }

        [Theory]
        [InlineData(0x00, MoveKind.Static)]
        [InlineData(0x02, MoveKind.TurnRandom)]
        [InlineData(0x03, MoveKind.Wander)]
        [InlineData(0x0f, MoveKind.FaceFixed)]
        [InlineData(0x13, MoveKind.Spin)]
        [InlineData(0x06, MoveKind.TurnRandom)]      // MV_RND_UL registers a DirRnd handler, not a walk
        [InlineData(0x14, MoveKind.Route)]           // MV_RT2 walks back and forth
        [InlineData(0x15, MoveKind.Route)]
        [InlineData(0x2d, MoveKind.TurnRandom)]
        [InlineData(0x33, MoveKind.Special)]
        public void KindsMatchTheEngineNames(byte value, MoveKind expected)
        {
            Assert.Equal(expected, OverworldMovements.Find(value).Kind);
        }

        [Fact]
        public void WanderAxesAreConstrained()
        {
            Assert.Equal(new[] { MoveFacing.Up, MoveFacing.Down },
                         OverworldMovements.Find(0x04).Facings.ToArray());
            Assert.Equal(new[] { MoveFacing.Left, MoveFacing.Right },
                         OverworldMovements.Find(0x05).Facings.ToArray());
            Assert.Equal(4, OverworldMovements.Find(0x03).Facings.Count);
        }

        [Fact]
        public void RouteFollowsTheOrderInTheName()
        {
            // MV_RTURLD: up, right, left, down
            Assert.Equal(new[] { MoveFacing.Up, MoveFacing.Right, MoveFacing.Left, MoveFacing.Down },
                         OverworldMovements.Find(0x15).Facings.ToArray());
            // MV_RTUL: just up and left
            Assert.Equal(new[] { MoveFacing.Up, MoveFacing.Left },
                         OverworldMovements.Find(0x25).Facings.ToArray());
        }

        [Fact]
        public void OnlyThreeCodesActuallyWalkAtRandom()
        {
            // fieldobj_movedata.c gives MV_RND, MV_RND_V and MV_RND_H a walking handler; every other
            // "random" code gets DirRnd, which switches movement off and only turns the sprite.
            var walking = OverworldMovements.All.Where(m => m.Kind == MoveKind.Wander)
                                                .Select(m => m.Value).ToArray();
            Assert.Equal(new byte[] { 0x03, 0x04, 0x05 }, walking);
        }

        [Fact]
        public void WalkBackAndForthTakesItsDirectionFromTheEvent()
        {
            // MV_RT2 reads the event's own facing rather than carrying a direction list.
            var rt2 = OverworldMovements.Find(0x14);
            Assert.True(rt2.RouteFollowsEventFacing);
            Assert.False(OverworldMovements.Find(0x15).RouteFollowsEventFacing);
        }

        [Fact]
        public void SpinDirectionsDiffer()
        {
            Assert.False(OverworldMovements.Find(0x12).SpinClockwise);
            Assert.True(OverworldMovements.Find(0x13).SpinClockwise);
        }
    }
}
